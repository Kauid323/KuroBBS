using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    /// <summary>
    /// 任务中心：成长任务 / 每日任务 + 金币流水。
    /// 导航参数为 int gameId（0 = 不区分游戏，与 [627] 真实请求一致）。
    /// </summary>
    public sealed partial class TaskCenterPage : Page
    {
        public TaskCenterViewModel ViewModel { get; private set; }

        public TaskCenterPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new TaskCenterViewModel();
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += OnBackPressed;

            // 返回本页时不重新拉数据，避免刚才看完的列表被刷掉
            if (e.NavigationMode == NavigationMode.Back) return;

            int gameId = 0;
            if (e.Parameter is int)
            {
                gameId = (int)e.Parameter;
            }
            else if (e.Parameter != null)
            {
                int.TryParse(e.Parameter.ToString(), out gameId);
            }

            await ViewModel.LoadAsync(gameId);
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
    }
}
