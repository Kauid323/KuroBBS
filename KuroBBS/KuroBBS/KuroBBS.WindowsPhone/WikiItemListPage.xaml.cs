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

            if (!string.IsNullOrEmpty(record.EntryId) && record.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, record.EntryId));
            }
            else if (!string.IsNullOrEmpty(record.LinkUrl))
            {
                // Parse link URL
                if (record.LinkUrl.Contains("/item/"))
                {
                    int itemIdx = record.LinkUrl.IndexOf("/item/");
                    string sub = record.LinkUrl.Substring(itemIdx + 6);
                    int qIdx = sub.IndexOf('?');
                    string entryId = qIdx >= 0 ? sub.Substring(0, qIdx) : sub;
                    Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, entryId));
                }
            }
        }
    }
}
