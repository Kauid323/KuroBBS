using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class WikiItemListPage : Page
    {
        public WikiItemListViewModel ViewModel { get; set; }
        private int _wikiType = 9;
        private int _catalogueId = 0;
        private string _title = "图鉴列表";

        public WikiItemListPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new WikiItemListViewModel();
            this.DataContext = ViewModel;
            SizeChanged += WikiItemListPage_SizeChanged;
        }

        private void WikiItemListPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 减掉 GridView 的 Padding（左右各 12）
            ViewModel.UpdateLayoutWidth(e.NewSize.Width - 24);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back) return;

            if (e.Parameter != null)
            {
                // Format: "wikiType|catalogueId|title"
                string paramStr = e.Parameter.ToString();
                string[] parts = paramStr.Split('|');
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out _wikiType);
                    int.TryParse(parts[1], out _catalogueId);
                    if (parts.Length >= 3)
                    {
                        _title = parts[2];
                    }
                }
            }

            await ViewModel.LoadItemsAsync(_wikiType, _catalogueId, _title);
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
            await ViewModel.LoadItemsAsync(_wikiType, _catalogueId, _title, forceRefresh: true);
        }

        private void OnResetFilterClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetFilter();
        }

        private void OnTagTapped(object sender, TappedRoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var tag = btn.Tag as WikiTagItemViewModel;
            if (tag != null)
            {
                ViewModel.ToggleTag(tag);
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var record = e.ClickedItem as WikiItemRecord;
            if (record == null) return;
            NavigateToRecord(record);
        }

        private void OnItemRecordTapped(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var record = elem.Tag as WikiItemRecord;
            if (record == null) return;
            NavigateToRecord(record);
        }

        private void NavigateToRecord(WikiItemRecord record)
        {
            if (record == null) return;

            // 1. 有 entryId 直接进条目详情。
            if (!string.IsNullOrEmpty(record.EntryId) && record.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, record.EntryId));
                return;
            }

            // 2. 其余交给全软件统一的内链解析器
            //    （/item/、/post/、/topic/、/user/、?fid=&sid=、站外链接一网打尽）。
            if (!string.IsNullOrEmpty(record.LinkUrl))
            {
                var target = KuroBBS.Helpers.KuroLinkResolver.Resolve(record.LinkUrl, record.Title);
                if (target.Kind == KuroBBS.Helpers.KuroLinkKind.WikiCatalogue)
                {
                    // 图鉴列表要用当前 wikiType，避免跨游戏串台。
                    Frame.Navigate(typeof(WikiItemListPage),
                        string.Format("{0}|{1}|{2}", _wikiType, target.Id, target.Title));
                    return;
                }

                if (KuroBBS.Helpers.KuroLinkNavigator.Navigate(Frame, target, _wikiType))
                {
                    return;
                }
            }
        }
    }
}
