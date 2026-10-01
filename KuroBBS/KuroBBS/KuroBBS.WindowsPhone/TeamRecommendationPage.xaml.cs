using System;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.Services;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class TeamRecommendationPage : Page
    {
        public TeamRecommendationViewModel ViewModel { get; private set; }

        public TeamRecommendationPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new TeamRecommendationViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;

            if (e.NavigationMode != NavigationMode.Back)
            {
                var navParams = e.Parameter as HaruTeamNavParams;
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

            if (CharacterPickerOverlay.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                CharacterPickerOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            if (!KuroBBS.Helpers.BackPressHelper.CanHandleBackPress())
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
                KuroLogger.Info("NAV_BACK", "TeamRecommendationPage returning to previous page");
                Frame.GoBack();
            }
            else if (ViewModel != null && ViewModel.NavParams != null)
            {
                KuroLogger.Warn("NAV_BACK", "TeamRecommendationPage back stack empty, fallback navigation to CharacterDetailPage");
                Frame.Navigate(typeof(CharacterDetailPage), new CharacterDetailNavParams
                {
                    ServerId = ViewModel.NavParams.ServerId,
                    RoleId = ViewModel.NavParams.RoleId,
                    CharacterId = ViewModel.NavParams.CharacterId,
                    RoleName = ViewModel.NavParams.CharacterName,
                    BodyName = ViewModel.NavParams.BodyName
                });
            }
        }

        private async void OnModeAllClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SwitchModeAsync(0);
        }

        private async void OnModeWarZoneClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SwitchModeAsync(1);
        }

        private async void OnModeBossClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SwitchModeAsync(2);
        }

        private async void OnSortHotClick(object sender, RoutedEventArgs e)
        {
            ViewModel.SortType = 0;
            await ViewModel.LoadDataAsync(true);
        }

        private async void OnSortNewClick(object sender, RoutedEventArgs e)
        {
            ViewModel.SortType = 1;
            await ViewModel.LoadDataAsync(true);
        }

        private void OnToggleCharacterPickerClick(object sender, RoutedEventArgs e)
        {
            CharacterPickerOverlay.Visibility = CharacterPickerOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnCloseCharacterPickerClick(object sender, RoutedEventArgs e)
        {
            CharacterPickerOverlay.Visibility = Visibility.Collapsed;
        }

        private async void OnCharacterItemClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null)
            {
                var character = btn.DataContext as HaruTeamFilterCharacter;
                if (character != null)
                {
                    CharacterPickerOverlay.Visibility = Visibility.Collapsed;
                    await ViewModel.SelectCharacterAsync(character);
                }
            }
        }

        private async void OnLoadMoreClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadMoreAsync();
        }
    }
}
