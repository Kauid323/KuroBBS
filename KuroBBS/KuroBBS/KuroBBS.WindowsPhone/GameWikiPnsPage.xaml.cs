using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class GameWikiPnsPage : Page
    {
        public GameWikiViewModel ViewModel { get; set; }

        public GameWikiPnsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new GameWikiViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back) return;

            if (!ViewModel.HasLoaded)
            {
                await ViewModel.LoadHomepageAsync(2);
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
            await ViewModel.LoadHomepageAsync(2, forceRefresh: true);
        }

        private void OnTreeClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WikiCatalogueTreePage), "2");
        }

        private void OnSearchToggleClick(object sender, RoutedEventArgs e)
        {
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
                Frame.Navigate(typeof(WikiItemListPage), "2|" + banner.CatalogueId + "|" + banner.Title);
            }
            else if (!string.IsNullOrEmpty(banner.EntryId) && banner.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), "2|" + banner.EntryId);
            }
        }

        private void OnAnnouncementTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            var ann = element.Tag as WikiAnnouncementItem;
            if (ann == null) return;

            if (!string.IsNullOrEmpty(ann.LinkUrl))
            {
                ParseAndNavigateUrl(ann.LinkUrl, ann.Title);
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
                Frame.Navigate(typeof(WikiEntryDetailPage), "2|" + searchItem.Id);
            }
        }

        private void NavigateByShortcut(WikiShortcutItem item)
        {
            if (item.CatalogueId > 0)
            {
                Frame.Navigate(typeof(WikiItemListPage), "2|" + item.CatalogueId + "|" + item.Title);
            }
            else if (!string.IsNullOrEmpty(item.EntryId) && item.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), "2|" + item.EntryId);
            }
            else if (!string.IsNullOrEmpty(item.LinkUrl))
            {
                ParseAndNavigateUrl(item.LinkUrl, item.Title);
            }
        }

        private void ParseAndNavigateUrl(string url, string title)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                if (url.Contains("/item/"))
                {
                    int itemIdx = url.IndexOf("/item/");
                    string sub = url.Substring(itemIdx + 6);
                    int qIdx = sub.IndexOf('?');
                    string entryId = qIdx >= 0 ? sub.Substring(0, qIdx) : sub;
                    Frame.Navigate(typeof(WikiEntryDetailPage), "2|" + entryId);
                    return;
                }

                if (url.Contains("sid="))
                {
                    int sidIdx = url.IndexOf("sid=");
                    string sub = url.Substring(sidIdx + 4);
                    int ampIdx = sub.IndexOf('&');
                    string sidStr = ampIdx >= 0 ? sub.Substring(0, ampIdx) : sub;
                    int catId;
                    if (int.TryParse(sidStr, out catId))
                    {
                        Frame.Navigate(typeof(WikiItemListPage), "2|" + catId + "|" + title);
                        return;
                    }
                }

                if (url.Contains("fid="))
                {
                    int fidIdx = url.IndexOf("fid=");
                    string sub = url.Substring(fidIdx + 4);
                    int ampIdx = sub.IndexOf('&');
                    string fidStr = ampIdx >= 0 ? sub.Substring(0, ampIdx) : sub;
                    int catId;
                    if (int.TryParse(fidStr, out catId))
                    {
                        Frame.Navigate(typeof(WikiItemListPage), "2|" + catId + "|" + title);
                        return;
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }
    }
}
