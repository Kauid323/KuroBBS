using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class DetailPage : Page
    {
        public DetailViewModel ViewModel { get; private set; }

        public DetailPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new DetailViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            if (e.NavigationMode != NavigationMode.Back)
            {
                GameRoleCard role = e.Parameter as GameRoleCard;
                await ViewModel.InitializeAsync(role);
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

        private void OnViewAllRolesClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Role != null)
            {
                Frame.Navigate(typeof(AllRolePage), ViewModel.Role);
            }
        }

        private void OnCharacterCardTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var charInfo = element.Tag as HaruCharacterInfo;
                if (charInfo != null && ViewModel != null && ViewModel.Role != null)
                {
                    Frame.Navigate(typeof(CharacterDetailPage), new CharacterDetailNavParams
                    {
                        ServerId = ViewModel.Role.ServerId,
                        RoleId = ViewModel.Role.RoleId,
                        CharacterId = charInfo.BodyId,
                        BodyName = charInfo.BodyName,
                        IconUrl = charInfo.IconUrl
                    });
                }
            }
        }
    }
}
