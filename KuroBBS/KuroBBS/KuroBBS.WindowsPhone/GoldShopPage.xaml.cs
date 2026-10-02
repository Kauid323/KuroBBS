using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    /// <summary>
    /// 金币商店：分类（全部 / 鸣潮 / 战双帕弥什 / 库街区）来自服务端，
    /// 点商品进 CommodityDetailPage，右上「任务中心」进 TaskCenterPage。
    /// </summary>
    public sealed partial class GoldShopPage : Page
    {
        public GoldShopViewModel ViewModel { get; private set; }

        public GoldShopPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new GoldShopViewModel();
            this.DataContext = ViewModel;
            SizeChanged += GoldShopPage_SizeChanged;
        }

        private void GoldShopPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 减掉 Grid 的左右 Margin（各 16）
            ViewModel.UpdateLayoutWidth(e.NewSize.Width - 32);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;

            // 从商品详情页返回时带上 refresh 标记，用来刷新余额（下单后金币会变）
            if (e.NavigationMode == NavigationMode.Back)
            {
                await ViewModel.RefreshGoldAsync();
                return;
            }

            await ViewModel.LoadAllAsync();

            // 数据到位后再把 Pivot 选中项与 ViewModel 对齐一次，
            // 防止 Pivot 先于数据初始化而停在空选中态。
            if (KindPivot != null && ViewModel.SelectedKind != null)
            {
                KindPivot.SelectedItem = ViewModel.SelectedKind;
            }
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

        private void OnKindSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pivot = sender as Pivot;
            if (pivot == null || ViewModel == null) return;

            var kind = pivot.SelectedItem as ShopKind;
            // Pivot 初始化时可能先给个 null，忽略掉，否则会把已选中的分类清掉
            if (kind == null) return;

            ViewModel.SelectedKind = kind;
        }

        private void OnCommodityItemClick(object sender, ItemClickEventArgs e)
        {
            var commodity = e.ClickedItem as ShopCommodity;
            if (commodity == null || string.IsNullOrEmpty(commodity.CommodityCode)) return;

            Frame.Navigate(typeof(CommodityDetailPage), commodity.CommodityCode);
        }

        private void OnGoToTaskCenterClick(object sender, RoutedEventArgs e)
        {
            // gameId=0 表示不区分游戏，与任务接口真实请求一致
            Frame.Navigate(typeof(TaskCenterPage), 0);
        }
    }
}
