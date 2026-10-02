using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    /// <summary>
    /// 商品详情 + 兑换。
    /// 导航参数为 commodityCode（string）。
    /// </summary>
    public sealed partial class CommodityDetailPage : Page
    {
        public CommodityDetailViewModel ViewModel { get; private set; }

        public CommodityDetailPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new CommodityDetailViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;

            // 返回导航会复用缓存的实例，不必重新拉详情
            if (e.NavigationMode == NavigationMode.Back) return;

            string commodityCode = e.Parameter as string;
            if (string.IsNullOrEmpty(commodityCode))
            {
                ViewModel.StatusMessage = "缺少商品编号";
                return;
            }

            await ViewModel.LoadAsync(commodityCode);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnBackPressed;
            base.OnNavigatedFrom(e);
        }

        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;
            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
                e.Handled = true;
            }
        }

        private void OnIncreaseClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            // 有明确限购时不允许超过限购次数
            int max = 1;
            if (ViewModel.Detail != null && ViewModel.Detail.CommodityLimit > 0)
            {
                max = ViewModel.Detail.CommodityLimit;
            }
            else if (ViewModel.Detail != null && ViewModel.Detail.TotalSurplusStock > 0)
            {
                max = (int)Math.Min(ViewModel.Detail.TotalSurplusStock, 99);
            }

            if (ViewModel.BuyNum < max) ViewModel.BuyNum++;
        }

        private void OnDecreaseClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            if (ViewModel.BuyNum > 1) ViewModel.BuyNum--;
        }
    }
}
