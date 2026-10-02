using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class GameWikiMcPage : Page
    {
        public GameWikiViewModel ViewModel { get; set; }

        public GameWikiMcPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new GameWikiViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 页面被缓存（Required），OnNavigatedTo 会多次触发；先退订再订阅，避免重复挂载。
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
            HardwareButtons.BackPressed += OnHardwareBackPressed;

            if (e.NavigationMode == NavigationMode.Back) return;

            if (!ViewModel.HasLoaded)
            {
                await ViewModel.LoadHomepageAsync(9);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
            base.OnNavigatedFrom(e);
        }

        /// <summary>
        /// 返回键优先处理搜索框：只有当用户确实点开了搜索（输入框持有焦点）时，
        /// 返回键才「先收起键盘」；否则一律执行页面返回。平时输入框
        /// IsTabStop=false 且不持有焦点，所以关闭 toast / 菜单后系统不会
        /// 把焦点还原到它身上，键盘也就不会被无故顶起。
        /// </summary>
        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;

            if (SearchInputBox != null && SearchInputBox.FocusState != FocusState.Unfocused)
            {
                // 收起键盘：清掉焦点并让系统隐藏输入面板（WP 会自动处理返回键 -> 键盘）。
                SearchInputBox.IsTabStop = false;
                KuroSoftKeyboardHelper.DismissFor(this);
                e.Handled = true;
                return;
            }

            if (Frame != null && Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadHomepageAsync(9, forceRefresh: true);
        }

        private void OnTreeClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WikiCatalogueTreePage), "9");
        }

        private void OnSearchToggleClick(object sender, RoutedEventArgs e)
        {
            // 只有用户主动点「搜索」时才把焦点交给输入框（此时才需要键盘）。
            // 其余时刻 SearchInputBox 的 IsTabStop=false，系统就不会在弹窗/菜单
            // 关闭时把焦点「还原」到它身上，从而避免键盘被无故顶起。
            SearchInputBox.IsTabStop = true;
            SearchInputBox.Focus(FocusState.Programmatic);
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
            {
                await ViewModel.SearchAsync(ViewModel.SearchQuery);
            }
        }

        private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (!string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
                {
                    await ViewModel.SearchAsync(ViewModel.SearchQuery);
                }
            }
        }

        private void OnCloseSearchClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSearching = false;
            ViewModel.SearchQuery = "";
            // 退出搜索：把焦点从输入框移走并恢复 IsTabStop=false，
            // 这样后续弹窗/菜单关闭时系统不会再把焦点还原到它、把键盘顶起来。
            SearchInputBox.IsTabStop = false;
            KuroSoftKeyboardHelper.ClearFocusIfFocused(this, SearchInputBox);
        }

        private void OnShortcutItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as WikiShortcutItem;
            if (item == null) return;
            NavigateByShortcut(item);
        }

        private void OnBannerTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var banner = element.Tag as WikiBannerItem;
            if (banner == null) return;

            if (banner.CatalogueId > 0)
            {
                Frame.Navigate(typeof(WikiItemListPage), "9|" + banner.CatalogueId + "|" + banner.Title);
            }
            else if (!string.IsNullOrEmpty(banner.EntryId) && banner.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), "9|" + banner.EntryId);
            }
        }

        private async void OnAnnouncementTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var ann = element.Tag as WikiAnnouncementItem;
            if (ann == null) return;

            if (!string.IsNullOrEmpty(ann.LinkUrl))
            {
                // Can parse entryId or catalogueId if URL has query params
                ParseAndNavigateUrl(ann.LinkUrl, ann.DisplayTitle);
            }
            else if (ann.HasBody)
            {
                // 弹窗前先把搜索框的焦点卸掉，否则对话框关闭时系统会把焦点
                // 「还原」到搜索框，导致关闭 toast 的瞬间软键盘被顶起来。
                KuroSoftKeyboardHelper.ClearFocusIfFocused(this, SearchInputBox);

                // 「更新日志」这类条目没有跳转链接，点击后弹出完整正文。
                var dialog = new ContentDialog
                {
                    Title = ann.DisplayTitle,
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = ann.Body,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 13
                        },
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MaxHeight = 420
                    },
                    PrimaryButtonText = "关闭"
                };
                await dialog.ShowAsync();

                // 对话框关闭后再次确保焦点不在搜索框上（返回键关闭对话框时尤其需要）。
                KuroSoftKeyboardHelper.DismissFor(this);
            }
        }

        private void OnSearchResultTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var searchItem = element.Tag as WikiSearchItem;
            if (searchItem == null) return;

            if (searchItem.Id > 0)
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), "9|" + searchItem.Id);
            }
        }

        private void NavigateByShortcut(WikiShortcutItem item)
        {
            // 优先落到模块的「目标目录」——即 content.children 里 active:true 的子目录。
            // 官方前端就是直接打开这个子目录（图鉴→机体图鉴、攻略→版本攻略、
            // 剧情→主线剧情、主题影音→节日贺图），而不是父级分组节点。
            int targetId = item.TargetCatalogueId > 0 ? item.TargetCatalogueId : item.CatalogueId;

            if (targetId > 0)
            {
                string title = item.Title;
                // 若目标不是模块自身，用子目录名作为标题，更贴近官方表现。
                if (targetId != item.CatalogueId && item.Children != null)
                {
                    foreach (var c in item.Children)
                    {
                        if (c != null && c.Id == targetId && !string.IsNullOrEmpty(c.Name))
                        {
                            title = c.Name;
                            break;
                        }
                    }
                }
                Frame.Navigate(typeof(WikiItemListPage), "9|" + targetId + "|" + title);
            }
            else if (!string.IsNullOrEmpty(item.EntryId) && item.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), "9|" + item.EntryId);
            }
            else if (!string.IsNullOrEmpty(item.LinkUrl))
            {
                ParseAndNavigateUrl(item.LinkUrl, item.Title);
            }
        }

        private void ParseAndNavigateUrl(string url, string title)
        {
            if (string.IsNullOrEmpty(url)) return;

            // 全软件统一的内链解析：/item/、/post/、/topic/、/user/、?fid=&sid=、站外链接。
            // 鸣潮的 wikiType = 9。
            try
            {
                KuroBBS.Helpers.KuroLinkNavigator.Navigate(Frame, url, 9, title);
            }
            catch
            {
                // Fallback
            }
        }
    }
}
