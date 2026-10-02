using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class WikiEntryDetailPage : Page
    {
        public WikiEntryDetailViewModel ViewModel { get; set; }
        private int _wikiType = 9;
        private string _entryId = "";

        public WikiEntryDetailPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new WikiEntryDetailViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back) return;

            string newEntryId = "";
            int newWikiType = 9;

            if (e.Parameter != null)
            {
                // Format: "wikiType|entryId"
                string paramStr = e.Parameter.ToString();
                string[] parts = paramStr.Split('|');
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out newWikiType);
                    newEntryId = parts[1];
                }
                else
                {
                    newEntryId = paramStr;
                }
            }

            if (!string.IsNullOrEmpty(newEntryId))
            {
                _wikiType = newWikiType;
                _entryId = newEntryId;
                await ViewModel.LoadDetailAsync(_wikiType, _entryId);
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
            if (!string.IsNullOrEmpty(_entryId))
            {
                await ViewModel.LoadDetailAsync(_wikiType, _entryId, forceRefresh: true);
            }
        }

        private void OnTabButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var tab = btn.DataContext as WikiTabItem;
            if (tab == null || ViewModel == null || ViewModel.Modules == null) return;

            foreach (var mod in ViewModel.Modules)
            {
                if (mod.Components == null) continue;
                foreach (var comp in mod.Components)
                {
                    if (comp.Tabs != null && comp.Tabs.Contains(tab))
                    {
                        comp.SelectedTab = tab;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 意识手册 - 立绘页签切换（1/4号位、2/5号位 ...）
        /// </summary>
        private void OnConsciousnessIllustrationClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var ill = btn.DataContext as ConsciousnessIllustration;
            if (ill == null || ViewModel == null || ViewModel.Modules == null) return;

            foreach (var mod in ViewModel.Modules)
            {
                if (mod.Components == null) continue;
                foreach (var comp in mod.Components)
                {
                    if (comp.Consciousness != null && comp.Consciousness.Illustrations != null
                        && comp.Consciousness.Illustrations.Contains(ill))
                    {
                        comp.Consciousness.SelectedIllustration = ill;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 意识手册 - 意识故事折叠展开
        /// </summary>
        private void OnConsciousnessStoryTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var story = elem.DataContext as ConsciousnessStory;
            if (story != null)
            {
                story.IsExpanded = !story.IsExpanded;
            }
        }

        private void OnComponentHeaderTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var comp = elem.DataContext as WikiDetailComponent;
            if (comp != null && comp.CanCollapse)
            {
                comp.IsCollapsed = !comp.IsCollapsed;
            }
        }

        private void OnSectionHeaderTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var sec = elem.DataContext as WikiSectionItem;
            if (sec != null)
            {
                sec.IsExpanded = !sec.IsExpanded;
            }
        }

        private void OnStrategyItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var strat = elem.DataContext as WikiStrategyItem;
            if (strat != null && !string.IsNullOrEmpty(strat.EntryId) && strat.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, strat.EntryId));
            }
        }

        private void OnGearItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var gear = elem.DataContext as WikiRoleGearItem;
            if (gear != null && !string.IsNullOrEmpty(gear.EntryId) && gear.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, gear.EntryId));
            }
        }

        private void OnEquipRecommendItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var item = elem.DataContext as WikiEquipRecommendItem;
            if (item != null && !string.IsNullOrEmpty(item.EntryId) && item.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, item.EntryId));
            }
        }

        private async void OnActiveTabLinkTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var comp = elem.DataContext as WikiDetailComponent;
            if (comp != null && comp.HasActiveTabLink)
            {
                if (!string.IsNullOrEmpty(comp.ActiveTabLinkEntryId) && comp.ActiveTabLinkEntryId != "0")
                {
                    Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, comp.ActiveTabLinkEntryId));
                }
                else if (!string.IsNullOrEmpty(comp.ActiveTabLinkUrl))
                {
                    try
                    {
                        var uri = new Uri(comp.ActiveTabLinkUrl);
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }
    }
}

