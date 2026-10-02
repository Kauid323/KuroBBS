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
    /// <summary>
    /// 指挥官贡献榜页面：一个 Pivot，周榜 / 月榜 / 总榜。
    ///
    /// 数据来自 POST /wiki/core/score/record/getTop10List（type=2/3/4），
    /// 点某一行跳转到该用户的个人主页（person-center?id=）。
    /// </summary>
    public sealed partial class WikiContributorRankPage : Page
    {
        private int _wikiType = 2;
        public WikiRankViewModel ViewModel { get; set; }

        public WikiContributorRankPage()
        {
            this.InitializeComponent();

            // 缓存页面实例：从别的页面返回本页时复用，配合 ViewModel 的 HasLoaded 判断
            // 做到「不重复请求」（否则每次返回都会重建页面 + 重新拉三次榜单）。
            this.NavigationCacheMode = NavigationCacheMode.Required;

            ViewModel = new WikiRankViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            HardwareButtons.BackPressed -= OnHardwareBackPressed;
            HardwareButtons.BackPressed += OnHardwareBackPressed;

            // 参数：wikiType（2=战双 / 9=鸣潮），兼容 string / int
            if (e.Parameter is string)
            {
                int t;
                if (int.TryParse((string)e.Parameter, out t)) _wikiType = t;
            }
            else if (e.Parameter is int)
            {
                _wikiType = (int)e.Parameter;
            }

            await ViewModel.LoadAsync(_wikiType);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnHardwareBackPressed;
            base.OnNavigatedFrom(e);
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;
            if (Frame != null && Frame.CanGoBack)
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync(_wikiType, forceRefresh: true);
        }

        private void OnRowTapped(object sender, TappedRoutedEventArgs e)
        {
            var el = sender as FrameworkElement;
            if (el == null) return;
            var item = el.Tag as WikiContributorItem;
            if (item == null) return;

            // 优先用 userCenterUrl（https://www.kurobbs.com/person-center?id=xxx）
            // 走统一内链解析 → UserProfilePage。
            if (!string.IsNullOrEmpty(item.UserCenterUrl))
            {
                try
                {
                    if (KuroLinkNavigator.Navigate(Frame, item.UserCenterUrl, _wikiType)) return;
                }
                catch { }
            }

            // 回退：直接用 uid。
            if (!string.IsNullOrEmpty(item.Uid))
            {
                try { Frame.Navigate(typeof(UserProfilePage), item.Uid); }
                catch { }
            }
        }
    }
}
