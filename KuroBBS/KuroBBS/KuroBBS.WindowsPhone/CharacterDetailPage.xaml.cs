using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class CharacterDetailPage : Page
    {
        public CharacterDetailViewModel ViewModel { get; private set; }

        public CharacterDetailPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            ViewModel = new CharacterDetailViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            if (e.NavigationMode != NavigationMode.Back)
            {
                var navParams = e.Parameter as CharacterDetailNavParams;
                if (navParams != null)
                {
                    await ViewModel.InitializeAsync(navParams);
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;
            if (!BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            ReturnToPreviousPage();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            ReturnToPreviousPage();
        }

        private void OnTeamRecommendClick(object sender, RoutedEventArgs e)
        {
            if (Frame == null || ViewModel == null) return;

            int charId = ViewModel.NavParams != null ? ViewModel.NavParams.CharacterId : (ViewModel.Detail != null && ViewModel.Detail.Body != null ? ViewModel.Detail.Body.BodyId : 0);
            string serverId = ViewModel.NavParams != null ? ViewModel.NavParams.ServerId : "";
            string roleId = ViewModel.NavParams != null ? ViewModel.NavParams.RoleId : "";
            string roleName = ViewModel.NavParams != null ? ViewModel.NavParams.RoleName : (ViewModel.Detail != null && ViewModel.Detail.Body != null ? ViewModel.Detail.Body.RoleName : "");
            string bodyName = ViewModel.NavParams != null ? ViewModel.NavParams.BodyName : (ViewModel.Detail != null && ViewModel.Detail.Body != null ? ViewModel.Detail.Body.BodyName : "");

            KuroBBS.Services.KuroLogger.Info("NAV_TEAM", string.Format("Navigating to TeamRecommendationPage for character {0} ({1})", bodyName, charId));
            Frame.Navigate(typeof(TeamRecommendationPage), new HaruTeamNavParams
            {
                ServerId = serverId,
                RoleId = roleId,
                UserId = KuroBBS.Helpers.SettingsHelper.UserId,
                CharacterId = charId,
                CharacterName = roleName,
                BodyName = bodyName
            });
        }

        private void ReturnToPreviousPage()
        {
            if (Frame == null) return;
            if (Frame.CanGoBack)
            {
                KuroBBS.Services.KuroLogger.Info("NAV_BACK", "CharacterDetailPage returning to previous page");
                Frame.GoBack();
            }
            else if (ViewModel != null && ViewModel.NavParams != null)
            {
                KuroBBS.Services.KuroLogger.Warn("NAV_BACK", "CharacterDetailPage back stack empty, fallback navigation");
                Frame.Navigate(typeof(AllRolePage), new GameRoleCard
                {
                    ServerId = ViewModel.NavParams.ServerId,
                    RoleId = ViewModel.NavParams.RoleId,
                    RoleName = ViewModel.NavParams.RoleName ?? ""
                });
            }
        }
    }
}
