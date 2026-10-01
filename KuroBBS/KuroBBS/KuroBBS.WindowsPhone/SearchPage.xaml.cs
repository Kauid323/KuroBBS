using System;
using Windows.Phone.UI.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class SearchPage : Page
    {
        public SearchViewModel ViewModel { get; private set; }

        public SearchPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new SearchViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;

            if (e.NavigationMode != NavigationMode.Back)
            {
                int initialGameId = 2;
                if (e.Parameter is int)
                {
                    initialGameId = (int)e.Parameter;
                }
                else
                {
                    string paramStr = e.Parameter as string;
                    int gId;
                    if (!string.IsNullOrEmpty(paramStr) && int.TryParse(paramStr, out gId))
                    {
                        initialGameId = gId;
                    }
                }

                await ViewModel.InitializeAsync(initialGameId);
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

            if (ViewModel != null && ViewModel.IsFilterDrawerOpen)
            {
                ViewModel.IsFilterDrawerOpen = false;
                e.Handled = true;
                return;
            }

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

        private async void OnSelectZhanshuangClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SwitchGameAsync(2);
        }

        private async void OnSelectMingchaoClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SwitchGameAsync(3);
        }

        private void OnSearchInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null && SearchInputBox != null)
            {
                ViewModel.Keyword = SearchInputBox.Text;
            }
        }

        private async void OnSearchInputKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                this.Focus(FocusState.Programmatic);
                string text = SearchInputBox != null ? SearchInputBox.Text : ViewModel.Keyword;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ViewModel.Keyword = text.Trim();
                    await ViewModel.ExecuteSearchAsync(text.Trim());
                }
                else
                {
                    await ViewModel.ExecuteSearchAsync(null);
                }
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.HasSearched)
            {
                ViewModel.HasSearched = false;
                ViewModel.Keyword = string.Empty;
                if (SearchInputBox != null)
                {
                    SearchInputBox.Text = string.Empty;
                }
            }
            else if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnSearchPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchPivot != null && ViewModel != null && ViewModel.HasSearched)
            {
                int index = SearchPivot.SelectedIndex;
                if (index >= 0)
                {
                    await ViewModel.EnsureTabLoadedAsync(index);
                }
            }
        }

        private void OnOpenFilterDrawerClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsFilterDrawerOpen = true;
        }

        private void OnCloseFilterDrawerClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsFilterDrawerOpen = false;
        }

        private async void OnRecentSearchItemClick(object sender, ItemClickEventArgs e)
        {
            var query = e.ClickedItem as string;
            if (!string.IsNullOrEmpty(query))
            {
                if (SearchInputBox != null)
                {
                    SearchInputBox.Text = query;
                }
                await ViewModel.ExecuteSearchAsync(query);
            }
        }

        private void OnClearRecentSearchesClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearRecentSearches();
        }

        private async void OnHotSearchItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as HotSearchItem;
            if (item != null && !string.IsNullOrEmpty(item.KeyWord))
            {
                if (SearchInputBox != null)
                {
                    SearchInputBox.Text = item.KeyWord;
                }
                await ViewModel.ExecuteSearchAsync(item.KeyWord);
            }
        }

        private async void OnSortRadioChecked(object sender, RoutedEventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb != null && rb.Tag != null && ViewModel != null)
            {
                int sortVal;
                if (int.TryParse(rb.Tag.ToString(), out sortVal) && ViewModel.FilterSort != sortVal)
                {
                    ViewModel.FilterSort = sortVal;
                    await ViewModel.ApplyFiltersAsync();
                }
            }
        }

        private async void OnPostTypeRadioChecked(object sender, RoutedEventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb != null && rb.Tag != null && ViewModel != null)
            {
                int typeVal;
                if (int.TryParse(rb.Tag.ToString(), out typeVal) && ViewModel.FilterPostType != typeVal)
                {
                    ViewModel.FilterPostType = typeVal;
                    await ViewModel.ApplyFiltersAsync();
                }
            }
        }

        private async void OnTimeRadioChecked(object sender, RoutedEventArgs e)
        {
            var rb = sender as RadioButton;
            if (rb != null && rb.Tag != null && ViewModel != null)
            {
                int timeVal;
                if (int.TryParse(rb.Tag.ToString(), out timeVal) && ViewModel.FilterTime != timeVal)
                {
                    ViewModel.FilterTime = timeVal;
                    await ViewModel.ApplyFiltersAsync();
                }
            }
        }

        private async void OnResetFiltersClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.ResetFiltersAndReloadAsync();
        }

        private async void OnApplyFiltersClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.ApplyFiltersAsync();
        }

        private void OnPostItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as PostItem;
            if (post != null)
            {
                Frame.Navigate(typeof(PostDetailPage), post);
            }
        }

        private void OnTopicListItemClick(object sender, ItemClickEventArgs e)
        {
            var topic = e.ClickedItem as TopicItem;
            if (topic != null)
            {
                Frame.Navigate(typeof(TopicDetailPage), topic);
            }
        }

        private void OnUserListItemClick(object sender, ItemClickEventArgs e)
        {
            var user = e.ClickedItem as UserSearchItem;
            if (user != null && !string.IsNullOrEmpty(user.UserId))
            {
                Frame.Navigate(typeof(UserProfilePage), user.UserId);
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

        // ListView loaded handlers for infinite scroll
        private void OnCompositeListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void OnStrategyListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void OnFanartListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void OnWikiListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void OnTopicListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void OnUserListViewLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll(sender as ListView);
        }

        private void HookInfiniteScroll(ListView listView)
        {
            if (listView == null) return;
            var sv = FindScrollViewer(listView);
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
