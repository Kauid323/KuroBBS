using System;
using System.Threading.Tasks;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; private set; }

        public LoginPage()
        {
            this.InitializeComponent();
            ViewModel = new LoginViewModel();
            this.DataContext = ViewModel;

            ViewModel.LoginCompleted += OnLoginCompleted;
            ViewModel.RequestNavigateGeetest += OnRequestNavigateGeetest;
            GeetestWebView.NavigationStarting += OnGeetestNavigationStarting;
            GeetestWebView.DOMContentLoaded += OnGeetestDomContentLoaded;
            GeetestWebView.NavigationCompleted += OnGeetestNavigationCompleted;
            GeetestWebView.NavigationFailed += OnGeetestNavigationFailed;
        }

        private async void OnRequestNavigateGeetest(object sender, string html)
        {
            await Task.Delay(100);
            if (GeetestWebView != null && !string.IsNullOrEmpty(html))
            {
                KuroBBS.Services.KuroLogger.Loading("GEETEST_NAV", "LoginPage NavigateToString");
                GeetestWebView.NavigateToString(html);
            }
        }

        private void OnGeetestNavigationStarting(Windows.UI.Xaml.Controls.WebView sender, Windows.UI.Xaml.Controls.WebViewNavigationStartingEventArgs args)
        {
            KuroBBS.Services.KuroLogger.Loading("GEETEST_NAV_START", "LoginPage WebView navigation started");
        }

        private void OnGeetestDomContentLoaded(Windows.UI.Xaml.Controls.WebView sender, Windows.UI.Xaml.Controls.WebViewDOMContentLoadedEventArgs args)
        {
            KuroBBS.Services.KuroLogger.Loading("GEETEST_DOM", "LoginPage WebView DOMContentLoaded");
        }

        private void OnGeetestNavigationCompleted(Windows.UI.Xaml.Controls.WebView sender, Windows.UI.Xaml.Controls.WebViewNavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                KuroBBS.Services.KuroLogger.Info("GEETEST_NAV_OK", "LoginPage WebView navigation completed");
                StartGeetestPolling();
            }
            else
            {
                KuroBBS.Services.KuroLogger.Warn("GEETEST_NAV_FAIL", "LoginPage WebView navigation failed: " + args.WebErrorStatus);
            }
        }

        private void OnGeetestNavigationFailed(object sender, Windows.UI.Xaml.Controls.WebViewNavigationFailedEventArgs e)
        {
            KuroBBS.Services.KuroLogger.Error("GEETEST_NAV_ERR", "LoginPage WebView NavigationFailed: " + e.WebErrorStatus, null);
        }

        private async void OnGeetestScriptNotify(object sender, Windows.UI.Xaml.Controls.NotifyEventArgs e)
        {
            KuroBBS.Services.KuroLogger.Loading("GEETEST_NOTIFY", "LoginPage WebView ScriptNotify: " + e.Value);
            JsonObject obj;
            if (JsonObject.TryParse(e.Value, out obj))
            {
                string type = obj.ContainsKey("type") ? obj.GetNamedString("type") : "";
                string data = obj.ContainsKey("data") ? obj.GetNamedString("data") : "";
                if (type == "success") await ViewModel.OnGeetestValidatedAsync(data);
                else if (type == "close") ViewModel.IsGeetestVisible = false;
                else if (type == "error") ViewModel.OnGeetestFailed(data);
                else if (type == "ready") KuroBBS.Services.KuroLogger.Info("GEETEST_READY", "LoginPage Geetest UI ready");
                else if (type == "log") KuroBBS.Services.KuroLogger.Loading("GEETEST_JS", data);
            }
            else if (!string.IsNullOrEmpty(e.Value) && e.Value.Contains("lot_number")) await ViewModel.OnGeetestValidatedAsync(e.Value);
        }

        private async void StartGeetestPolling()
        {
            while (ViewModel.IsGeetestVisible)
            {
                await Task.Delay(500);
                if (!ViewModel.IsGeetestVisible || GeetestWebView == null) break;
                try
                {
                    string state = await GeetestWebView.InvokeScriptAsync("eval", new[] { "window.getGeetestState ? window.getGeetestState() : ''" });
                    JsonObject obj;
                    if (JsonObject.TryParse(state, out obj))
                    {
                        string type = obj.ContainsKey("type") ? obj.GetNamedString("type") : "";
                        string data = obj.ContainsKey("data") ? obj.GetNamedString("data") : "";
                        if (type == "success") { KuroBBS.Services.KuroLogger.Info("GEETEST_POLL_OK", "LoginPage Geetest validated via polling"); await ViewModel.OnGeetestValidatedAsync(data); break; }
                        if (type == "close") { ViewModel.IsGeetestVisible = false; break; }
                        if (type == "error") { ViewModel.OnGeetestFailed(data); break; }
                        if (type == "ready") KuroBBS.Services.KuroLogger.Info("GEETEST_POLL_READY", "LoginPage Geetest UI ready via polling");
                    }
                }
                catch { }
            }
        }

        private void OnCloseGeetestClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsGeetestVisible = false;
        }

        private async void OnLoginCompleted(bool success, string userName)
        {
            if (success)
            {
                // Brief pause to show success state before returning
                await Task.Delay(800);
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            ViewModel.Cleanup();
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
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
