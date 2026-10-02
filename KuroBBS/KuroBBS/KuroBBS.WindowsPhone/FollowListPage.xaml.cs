using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS
{
    public sealed partial class FollowListPage : Page, INotifyPropertyChanged
    {
        private const int PageSize = 20;
        private int _pageNo = 1;
        private bool _hasMore = true;
        private bool _isLoading;
        private bool _hasLoadedOnce;
        private string _ownerUserId;
        private string _loadedUserId;
        private ScrollViewer _listScrollViewer;

        public ObservableCollection<UserFollowItem> Users { get; private set; }

        public bool IsLoading
        {
            get { return _isLoading; }
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                    OnPropertyChanged("ShowEmptyState");
                    OnPropertyChanged("ShowEndOfList");
                }
            }
        }

        public bool ShowEmptyState
        {
            get { return _hasLoadedOnce && !IsLoading && Users.Count == 0; }
        }

        public bool ShowEndOfList
        {
            get { return _hasLoadedOnce && !IsLoading && Users.Count > 0 && !_hasMore; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public FollowListPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            Users = new ObservableCollection<UserFollowItem>();
            DataContext = this;
            Loaded += OnPageLoaded;
            FollowListView.Loaded += OnFollowListLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            HookListScrollViewer();
        }

        private void OnFollowListLoaded(object sender, RoutedEventArgs e)
        {
            HookListScrollViewer();
        }

        private void HookListScrollViewer()
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(FollowListView);
            if (scrollViewer == null || scrollViewer == _listScrollViewer) return;

            _listScrollViewer = scrollViewer;
            _listScrollViewer.ViewChanged += (sender, args) =>
            {
                if (_listScrollViewer.ScrollableHeight > 0 &&
                    _listScrollViewer.VerticalOffset >= _listScrollViewer.ScrollableHeight - 320)
                {
                    var ignored = LoadMoreAsync();
                }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnHardwareBackPressed;

            string userId = e.Parameter as string;
            if (string.IsNullOrEmpty(userId)) return;

            _ownerUserId = userId;
            if (e.NavigationMode == NavigationMode.Back && _loadedUserId == userId) return;
            if (_loadedUserId != userId || e.NavigationMode == NavigationMode.New)
            {
                await LoadFirstPageAsync();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
            base.OnNavigatedFrom(e);
        }

        private async System.Threading.Tasks.Task LoadFirstPageAsync()
        {
            if (IsLoading || string.IsNullOrEmpty(_ownerUserId)) return;

            IsLoading = true;
            _hasLoadedOnce = false;
            Users.Clear();
            OnPropertyChanged("ShowEmptyState");
            _pageNo = 1;
            _hasMore = true;

            try
            {
                var result = await KuroUserService.Instance.GetUserFollowPageAsync(_ownerUserId, _pageNo, PageSize);
                if (result.IsSuccess)
                {
                    foreach (var user in result.Users) Users.Add(user);
                    _hasMore = result.HasNext;
                    _loadedUserId = _ownerUserId;
                }
                else
                {
                    NotificationHelper.ShowNotification("关注列表加载失败，请重试", "提示");
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_FOLLOW_LIST_ERR", "Failed loading follow list: " + ex.Message);
                NotificationHelper.ShowNotification("关注列表加载失败，请重试", "提示");
            }
            finally
            {
                _hasLoadedOnce = true;
                IsLoading = false;
                OnPropertyChanged("ShowEmptyState");
                OnPropertyChanged("ShowEndOfList");
            }
        }

        private async System.Threading.Tasks.Task LoadMoreAsync()
        {
            if (IsLoading || !_hasMore || string.IsNullOrEmpty(_ownerUserId)) return;

            IsLoading = true;
            int nextPage = _pageNo + 1;
            try
            {
                var result = await KuroUserService.Instance.GetUserFollowPageAsync(_ownerUserId, nextPage, PageSize);
                if (result.IsSuccess)
                {
                    foreach (var user in result.Users) Users.Add(user);
                    _pageNo = nextPage;
                    _hasMore = result.HasNext;
                }
                else
                {
                    KuroLogger.Warn("USER_FOLLOW_MORE_ERR", "Follow list page request returned no data.");
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_FOLLOW_MORE_ERR", "Failed loading next follow page: " + ex.Message);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged("ShowEmptyState");
                OnPropertyChanged("ShowEndOfList");
            }
        }

        private async void OnFollowButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            var user = button != null ? button.DataContext as UserFollowItem : null;
            if (user == null || user.IsUpdatingFollow) return;

            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先在主界面登录账号", "提示");
                return;
            }

            user.IsUpdatingFollow = true;
            bool newFollow = !user.IsFollow;
            try
            {
                bool success = await KuroUserService.Instance.FollowUserAsync(user.UserId, newFollow);
                if (success)
                {
                    user.IsFollow = newFollow;
                    NotificationHelper.ShowNotification(newFollow ? "关注成功" : "已取消关注", user.UserName);
                }
                else
                {
                    NotificationHelper.ShowNotification("操作失败，请重试", "提示");
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_FOLLOW_TOGGLE_ERR", "Failed to change follow state: " + ex.Message);
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
            finally
            {
                user.IsUpdatingFollow = false;
            }
        }

        private void OnUserTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var user = element != null ? element.DataContext as UserFollowItem : null;
            if (user != null && !string.IsNullOrEmpty(user.UserId))
            {
                Frame.Navigate(typeof(UserProfilePage), user.UserId);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;
            if (!BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
                e.Handled = true;
            }
        }

        private static T FindVisualChild<T>(Windows.UI.Xaml.DependencyObject parent) where T : Windows.UI.Xaml.DependencyObject
        {
            if (parent == null) return null;
            int count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T) return (T)child;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
