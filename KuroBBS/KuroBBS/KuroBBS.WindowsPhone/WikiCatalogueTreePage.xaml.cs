using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class WikiCatalogueTreePage : Page
    {
        public WikiCatalogueTreeViewModel ViewModel { get; set; }
        private int _wikiType = 9;

        public WikiCatalogueTreePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new WikiCatalogueTreeViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back) return;

            if (e.Parameter != null)
            {
                int wt;
                if (int.TryParse(e.Parameter.ToString(), out wt))
                {
                    _wikiType = wt;
                }
            }

            await ViewModel.LoadTreeAsync(_wikiType);
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
            await ViewModel.LoadTreeAsync(_wikiType, forceRefresh: true);
        }

        private void OnRootCategoryTapped(object sender, TappedRoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var node = btn.Tag as WikiCatalogueNode;
            if (node != null)
            {
                ViewModel.SelectedCategory = node;
            }
        }

        private void OnCatalogueNodeTapped(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var node = elem.Tag as WikiCatalogueNode;
            if (node == null) return;

            if (node.HasChildren)
            {
                // If clicked node has children, show its children
                ViewModel.SelectedCategory = node;
            }
            else
            {
                // Leaf node -> Open Item List page
                Frame.Navigate(typeof(WikiItemListPage), string.Format("{0}|{1}|{2}", _wikiType, node.Id, node.Name));
            }
        }
    }
}
