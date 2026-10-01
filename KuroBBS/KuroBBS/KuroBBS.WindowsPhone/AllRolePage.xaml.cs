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
    public sealed partial class AllRolePage : Page
    {
        public AllRoleViewModel ViewModel { get; private set; }

        public AllRolePage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new AllRoleViewModel();
            DataContext = ViewModel;
            SizeChanged += AllRolePage_SizeChanged;
        }

        private void AllRolePage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel != null && e.NewSize.Width > 0)
            {
                ViewModel.UpdateLayoutWidth(e.NewSize.Width - 32);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            if (e.NavigationMode != NavigationMode.Back || ViewModel == null || ViewModel.Roles.Count == 0)
            {
                var role = e.Parameter as GameRoleCard ?? (ViewModel != null ? ViewModel.Role : null);
                if (role != null)
                {
                    await ViewModel.InitializeAsync(role);
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

        private void ReturnToPreviousPage()
        {
            if (Frame == null) return;
            if (Frame.CanGoBack)
            {
                KuroBBS.Services.KuroLogger.Info("NAV_BACK", "AllRolePage returning to previous page");
                Frame.GoBack();
            }
            else if (ViewModel != null && ViewModel.Role != null)
            {
                KuroBBS.Services.KuroLogger.Warn("NAV_BACK", "Back stack was empty; navigating to DetailPage");
                Frame.Navigate(typeof(DetailPage), ViewModel.Role);
            }
        }

        private void OnCharacterCardTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var charInfo = element.Tag as HaruCharacterInfo;
                if (charInfo != null && ViewModel != null)
                {
                    Frame.Navigate(typeof(CharacterDetailPage), new CharacterDetailNavParams
                    {
                        ServerId = ViewModel.ServerId,
                        RoleId = ViewModel.RoleId,
                        CharacterId = charInfo.BodyId,
                        BodyName = charInfo.BodyName,
                        IconUrl = charInfo.IconUrl
                    });
                }
            }
        }
    }
}
