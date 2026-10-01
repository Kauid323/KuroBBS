using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class TopicDetailPage : Page
    {
        public TopicViewModel ViewModel { get; private set; }

        public TopicDetailPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new TopicViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;

            if (e.NavigationMode != NavigationMode.Back)
            {
                string topicId = string.Empty;
                var topicItem = e.Parameter as TopicItem;
                if (topicItem != null)
                {
                    topicId = topicItem.TopicId;
                    ViewModel.Topic = new TopicDetailResult
                    {
                        TopicId = topicItem.TopicId,
                        TopicName = topicItem.TopicName,
                        TopicIcon = topicItem.TopicIcon,
                        Remark = topicItem.Remark,
                        DiscussCnt = topicItem.DiscussCnt,
                        BrowseCnt = topicItem.BrowseCnt
                    };
                }
                else
                {
                    var paramStr = e.Parameter as string;
                    if (!string.IsNullOrEmpty(paramStr))
                    {
                        topicId = paramStr;
                    }
                }

                if (!string.IsNullOrEmpty(topicId))
                {
                    await ViewModel.InitializeAsync(topicId);
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= OnBackPressed;
        }

        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;

            if (!KuroBBS.Helpers.BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }

            if (this.Frame != null && this.Frame.CanGoBack)
            {
                e.Handled = true;
                this.Frame.GoBack();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnTopicPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicPivot != null && ViewModel != null && !string.IsNullOrEmpty(ViewModel.TopicId))
            {
                int index = TopicPivot.SelectedIndex;
                if (index >= 0)
                {
                    await ViewModel.EnsureTabLoadedAsync(index);
                }
            }
        }

        private void OnPostItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as PostItem;
            if (item != null)
            {
                Frame.Navigate(typeof(PostDetailPage), item);
            }
        }

        private void OnAuthorTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var author = element.Tag as PostAuthor;
                if (author != null && !string.IsNullOrEmpty(author.UserId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), author);
                    e.Handled = true;
                    return;
                }

                string userId = element.Tag as string;
                if (!string.IsNullOrEmpty(userId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), userId);
                    e.Handled = true;
                }
            }
        }

        private void OnListViewLoaded(object sender, RoutedEventArgs e)
        {
            var lv = sender as ListView;
            if (lv == null) return;
            var sv = FindScrollViewer(lv);
            if (sv != null)
            {
                sv.ViewChanged += async (s, args) =>
                {
                    var scroller = s as ScrollViewer;
                    if (scroller == null) return;
                    if (scroller.ScrollableHeight > 0 && (scroller.VerticalOffset >= scroller.ScrollableHeight - 350 || (scroller.VerticalOffset / scroller.ScrollableHeight >= 0.75)))
                    {
                        if (ViewModel != null && !ViewModel.IsLoadingMore && ViewModel.HasMore)
                        {
                            await ViewModel.LoadMoreAsync();
                        }
                    }
                };
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer) return (ScrollViewer)child;
                var sub = FindScrollViewer(child);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
