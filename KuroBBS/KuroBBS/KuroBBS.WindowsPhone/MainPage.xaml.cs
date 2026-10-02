using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            KuroBBS.Services.KuroLogger.Loading("MAIN_PAGE", "MainPage UI Component created");

            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;

            if (GeetestWebView != null)
            {
                GeetestWebView.NavigationStarting += (s, args) =>
                {
                    KuroBBS.Services.KuroLogger.Loading("GEETEST_NAV", "WebView NavigationStarting: " + (args.Uri != null ? args.Uri.ToString() : "NavigateToString"));
                };
                GeetestWebView.DOMContentLoaded += (s, args) =>
                {
                    KuroBBS.Services.KuroLogger.Loading("GEETEST_DOM", "WebView DOMContentLoaded");
                };
                GeetestWebView.NavigationCompleted += (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        KuroBBS.Services.KuroLogger.Info("GEETEST_NAV_OK", "WebView NavigationCompleted successfully");
                        StartGeetestPolling();
                    }
                    else
                    {
                        KuroBBS.Services.KuroLogger.Warn("GEETEST_NAV_FAIL", "WebView NavigationCompleted with WebErrorStatus: " + args.WebErrorStatus);
                    }
                };
                GeetestWebView.NavigationFailed += (s, args) =>
                {
                    KuroBBS.Services.KuroLogger.Error("GEETEST_NAV_ERR", "WebView NavigationFailed with WebErrorStatus: " + args.WebErrorStatus, null);
                };
            }

            ViewModel.RequestNavigateGeetest += async (s, html) =>
            {
                if (GeetestWebView != null && !string.IsNullOrEmpty(html))
                {
                    // Delay slightly to ensure XAML layout measurement for WebView has completed
                    await System.Threading.Tasks.Task.Delay(100);
                    GeetestWebView.NavigateToString(html);
                }
            };

#if WINDOWS_PHONE_APP
            Windows.Phone.UI.Input.HardwareButtons.BackPressed += MainPage_BackPressed;
#endif
        }

#if WINDOWS_PHONE_APP
        private void MainPage_BackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            if (ViewModel != null && ViewModel.IsGeetestVisible)
            {
                e.Handled = true;
                ViewModel.IsGeetestVisible = false;
                KuroBBS.Services.KuroLogger.Info("GEETEST_CLOSE", "Geetest modal closed by hardware Back button");
            }
        }
#endif

        private async void OnGeetestScriptNotify(object sender, NotifyEventArgs e)
        {
            try
            {
                string raw = e.Value;
                KuroBBS.Services.KuroLogger.Loading("GEETEST_NOTIFY", "WebView ScriptNotify: " + raw);

                Windows.Data.Json.JsonObject obj;
                if (Windows.Data.Json.JsonObject.TryParse(raw, out obj))
                {
                    string type = obj.ContainsKey("type") ? obj.GetNamedString("type") : "";
                    string data = obj.ContainsKey("data") ? obj.GetNamedString("data") : "";

                    if (type == "success")
                    {
                        await ViewModel.OnGeetestValidatedAsync(data);
                    }
                    else if (type == "close")
                    {
                        ViewModel.IsGeetestVisible = false;
                    }
                    else if (type == "error")
                    {
                        ViewModel.OnGeetestFailed(data);
                    }
                    else if (type == "ready")
                    {
                        KuroBBS.Services.KuroLogger.Info("GEETEST_READY", "Geetest GT4 UI is now rendered and ready for user slide");
                    }
                    else if (type == "log")
                    {
                        KuroBBS.Services.KuroLogger.Loading("GEETEST_JS", data);
                    }
                }
                else
                {
                    // Direct validate json payload
                    if (!string.IsNullOrEmpty(raw) && (raw.Contains("lot_number") || raw.Contains("pass_token")))
                    {
                        await ViewModel.OnGeetestValidatedAsync(raw);
                    }
                }
            }
            catch (Exception ex)
            {
                KuroBBS.Services.KuroLogger.Error("GEETEST_NOTIFY_ERR", "ScriptNotify handling error: " + ex.Message, ex);
            }
        }

        private async void StartGeetestPolling()
        {
            while (ViewModel != null && ViewModel.IsGeetestVisible)
            {
                await System.Threading.Tasks.Task.Delay(500);
                if (ViewModel == null || !ViewModel.IsGeetestVisible || GeetestWebView == null) break;

                try
                {
                    string stateJson = await GeetestWebView.InvokeScriptAsync("eval", new string[] { "window.getGeetestState ? window.getGeetestState() : ''" });
                    if (!string.IsNullOrEmpty(stateJson) && stateJson != "{}")
                    {
                        Windows.Data.Json.JsonObject obj;
                        if (Windows.Data.Json.JsonObject.TryParse(stateJson, out obj))
                        {
                            string type = obj.ContainsKey("type") ? obj.GetNamedString("type") : "";
                            string data = obj.ContainsKey("data") ? obj.GetNamedString("data") : "";

                            if (type == "success")
                            {
                                KuroBBS.Services.KuroLogger.Info("GEETEST_POLL_OK", "Geetest validated via polling bridge");
                                await ViewModel.OnGeetestValidatedAsync(data);
                                break;
                            }
                            else if (type == "close")
                            {
                                ViewModel.IsGeetestVisible = false;
                                break;
                            }
                            else if (type == "error")
                            {
                                KuroBBS.Services.KuroLogger.Warn("GEETEST_POLL_ERR", "Geetest error via polling bridge: " + data);
                                ViewModel.OnGeetestFailed(data);
                                break;
                            }
                            else if (type == "ready")
                            {
                                KuroBBS.Services.KuroLogger.Info("GEETEST_POLL_READY", "Geetest GT4 UI ready via polling bridge");
                            }
                            else if (type == "log")
                            {
                                KuroBBS.Services.KuroLogger.Loading("GEETEST_POLL_LOG", data);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode != NavigationMode.Back)
            {
                await ViewModel.InitializeAsync();
            }
            else
            {
                await ViewModel.RefreshUserProfileIfLoggedInAsync();
            }
        }

        private void OnLoginButtonClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(LoginPage));
        }

        private void OnCreatePostClick(object sender, RoutedEventArgs e)
        {
            int gameId = ViewModel != null ? ViewModel.SelectedGameId : 2;
            this.Frame.Navigate(typeof(CreatePostPage), gameId);
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            int gameId = ViewModel != null ? ViewModel.SelectedGameId : 2;
            this.Frame.Navigate(typeof(SearchPage), gameId);
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        /// <summary>签到页的「库洛币余额」卡片 → 金币商店。</summary>
        private void OnGoToGoldShopClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(GoldShopPage));
        }

        /// <summary>签到页的「任务中心」入口 → 任务中心页。gameId=0 = 不区分游戏。</summary>
        private void OnGoToTaskCenterClick(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(TaskCenterPage), 0);
        }

        private void OnGoToSignInClick(object sender, RoutedEventArgs e)
        {
            if (MainPivot != null && MainPivot.Items != null)
            {
                foreach (var item in MainPivot.Items)
                {
                    var pivotItem = item as PivotItem;
                    if (pivotItem != null && object.Equals(pivotItem.Header, "签到"))
                    {
                        MainPivot.SelectedItem = pivotItem;
                        break;
                    }
                }
            }
        }

        private async void OnFollowCommendClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var item = btn.Tag as CommendFollowItem;
            if (item == null || string.IsNullOrEmpty(item.UserId)) return;

            bool targetFollow = !item.IsFollow;
            bool success = await KuroBBS.Services.KuroUserService.Instance.FollowUserAsync(item.UserId, targetFollow);
            if (success)
            {
                item.IsFollow = targetFollow;
            }
        }

        private void OnSelectMingChaoClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SelectedGameId = 3;
        }

        private void OnSelectZhanShuangClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SelectedGameId = 2;
        }

        private void OnMyProfileTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Profile != null && !string.IsNullOrEmpty(ViewModel.Profile.UserId))
            {
                this.Frame.Navigate(typeof(UserProfilePage), ViewModel.Profile.UserId);
                e.Handled = true;
            }
            else
            {
                string myUserId = Helpers.SettingsHelper.UserId;
                if (!string.IsNullOrEmpty(myUserId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), myUserId);
                    e.Handled = true;
                }
            }
        }

        private void OnPostItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as PostItem;
            if (post != null)
            {
                this.Frame.Navigate(typeof(PostDetailPage), post);
            }
        }

        private async void OnGameWikiItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as GameWikiItem;
            if (item == null) return;

            // 1. Daily Sign-in redirection to Sign-in Pivot
            if (item.WikiName == "每日签到" || item.WikiName == "每日补给" || item.WikiName.Contains("签到") || item.WikiName.Contains("补给"))
            {
                OnGoToSignInClick(null, null);
                return;
            }

            // 2. Post Detail navigation (wikiType == 1 or has PostId)
            if (!string.IsNullOrEmpty(item.PostId))
            {
                var post = new PostItem
                {
                    PostId = item.PostId,
                    Title = !string.IsNullOrEmpty(item.PostTitle) ? item.PostTitle : item.WikiName,
                    GameId = item.GameId
                };
                this.Frame.Navigate(typeof(PostDetailPage), post);
                return;
            }

            // 3. 先解析 url 的「具体内链」：/item/（条目详情）、/post/（帖子）、
            //    /catalogue/list?fid=&sid=（目录列表）等。
            //    必须在下面那条「凡是 wiki.kurobbs.com 就进 Wiki 主页」的兜底之前判断，
            //    否则像「版本攻略」这种 url = .../pns/item/1539291460079370240 的条目
            //    会被误判成「进 Wiki 主页」而丢掉具体条目。
            if (!string.IsNullOrEmpty(item.Url))
            {
                var target = KuroBBS.Helpers.KuroLinkResolver.Resolve(item.Url, item.WikiName);
                if (target != null && target.IsResolved && target.Kind != KuroBBS.Helpers.KuroLinkKind.External)
                {
                    int wikiType = item.GameId == 3 ? 9 : 2;
                    if (KuroBBS.Helpers.KuroLinkNavigator.Navigate(this.Frame, target, wikiType))
                    {
                        return;
                    }
                }
            }

            // 4. Native Built-in page navigations (Wiki 主页入口)
            if (item.WikiName == "WIKI" || item.WikiName.ToUpper().Contains("WIKI") || (!string.IsNullOrEmpty(item.Url) && item.Url.Contains("wiki.kurobbs.com")))
            {
                int gameId = item.GameId > 0 ? item.GameId : (ViewModel != null ? ViewModel.SelectedGameId : 2);
                if (gameId == 3)
                {
                    this.Frame.Navigate(typeof(GameWikiMcPage));
                }
                else
                {
                    this.Frame.Navigate(typeof(GameWikiPnsPage));
                }
                return;
            }

            if (item.WikiName == "编队推荐")
            {
                this.Frame.Navigate(typeof(TeamRecommendationPage));
                return;
            }
            if (item.WikiName == "作战数据" || item.WikiName == "数据终端" || item.WikiName.Contains("作战数据") || item.WikiName.Contains("数据终端"))
            {
                var role = ViewModel != null ? ViewModel.SelectedRole : null;
                if (role == null && ViewModel != null && ViewModel.Roles != null && ViewModel.Roles.Count > 0)
                {
                    foreach (var r in ViewModel.Roles)
                    {
                        if (r.GameId == item.GameId || r.GameId == ViewModel.SelectedGameId)
                        {
                            role = r;
                            break;
                        }
                    }
                    if (role == null) role = ViewModel.Roles[0];
                }

                this.Frame.Navigate(typeof(DetailPage), role);
                return;
            }

            // 6. Web URL / Tool（站外链接 / 自定义 scheme，交给系统浏览器）
            string targetUrl = !string.IsNullOrEmpty(item.Url) ? item.Url : item.CustomSchemeUrl;
            if (!string.IsNullOrEmpty(targetUrl))
            {
                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(targetUrl));
                }
                catch (Exception ex)
                {
                    KuroBBS.Services.KuroLogger.Error("LAUNCH_WIKI_URL_ERROR", "Failed to launch url: " + targetUrl + ", ex: " + ex.Message);
                }
            }
        }

        private void OnAuthorTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var author = element.Tag as PostAuthor;
                if (author != null && !string.IsNullOrEmpty(author.UserId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), author);
                    e.Handled = true;
                    return;
                }

                string userId = element.Tag as string;
                if (!string.IsNullOrEmpty(userId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), userId);
                    e.Handled = true;
                }
            }
        }

        private void OnRoleItemClick(object sender, ItemClickEventArgs e)
        {
            var role = e.ClickedItem as GameRoleCard;
            if (role != null && ViewModel != null)
            {
                ViewModel.SelectedRole = role;
                KuroBBS.Services.KuroLogger.Info("ROLE_SWITCH", "User tapped role card: " + role.RoleName + " (" + role.ServerName + ")");
                this.Frame.Navigate(typeof(DetailPage), role);
            }
        }

        private async void OnCommunityListLoaded(object sender, RoutedEventArgs e)
        {
            var lv = sender as ListView;
            if (lv == null) return;

            for (int i = 0; i < 10; i++)
            {
                var sv = FindScrollViewer(lv);
                if (sv != null)
                {
                    sv.ViewChanged -= CommunityScrollViewer_ViewChanged;
                    sv.ViewChanged += CommunityScrollViewer_ViewChanged;
                    KuroBBS.Services.KuroLogger.Loading("SCROLL_HOOK", "Community ListView ScrollViewer hooked for infinite scroll");
                    break;
                }
                await System.Threading.Tasks.Task.Delay(100);
            }
        }

        private async void OnNewsListLoaded(object sender, RoutedEventArgs e)
        {
            var lv = sender as ListView;
            if (lv == null) return;

            for (int i = 0; i < 10; i++)
            {
                var sv = FindScrollViewer(lv);
                if (sv != null)
                {
                    sv.ViewChanged -= NewsScrollViewer_ViewChanged;
                    sv.ViewChanged += NewsScrollViewer_ViewChanged;
                    KuroBBS.Services.KuroLogger.Loading("SCROLL_HOOK", "News ListView ScrollViewer hooked for infinite scroll");
                    break;
                }
                await System.Threading.Tasks.Task.Delay(100);
            }
        }

        private bool _isUpdatingCommandBar = false;

        private void CommunityScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            // Trigger auto load more when within 400px of bottom or scrolled past 75%
            if (sv.ScrollableHeight > 0 && (sv.VerticalOffset >= sv.ScrollableHeight - 400 || (sv.VerticalOffset / sv.ScrollableHeight >= 0.75)))
            {
                if (ViewModel != null)
                {
                    var t = ViewModel.LoadMoreCommunityAsync();
                }
            }

            HandleCommandBarScrollMode(sv.VerticalOffset);
        }

        private void NewsScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            if (sv.ScrollableHeight > 0 && (sv.VerticalOffset >= sv.ScrollableHeight - 400 || (sv.VerticalOffset / sv.ScrollableHeight >= 0.75)))
            {
                if (ViewModel != null)
                {
                    var t = ViewModel.LoadMoreNewsAsync();
                }
            }

            HandleCommandBarScrollMode(sv.VerticalOffset);
        }

        private void HandleCommandBarScrollMode(double verticalOffset)
        {
            if (MainCommandBar == null || _isUpdatingCommandBar) return;

            if (verticalOffset > 40)
            {
                if (MainCommandBar.IsOpen || MainCommandBar.ClosedDisplayMode != AppBarClosedDisplayMode.Minimal)
                {
                    _isUpdatingCommandBar = true;
                    try
                    {
                        MainCommandBar.IsOpen = false;
                        MainCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;
                    }
                    finally
                    {
                        _isUpdatingCommandBar = false;
                    }
                }
            }
            else if (verticalOffset <= 5 && MainCommandBar.ClosedDisplayMode != AppBarClosedDisplayMode.Compact)
            {
                _isUpdatingCommandBar = true;
                try
                {
                    MainCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
                }
                finally
                {
                    _isUpdatingCommandBar = false;
                }
            }
        }

        private void OnSubForumTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null && border.Tag != null)
            {
                int index;
                if (int.TryParse(border.Tag.ToString(), out index) && ViewModel != null)
                {
                    ViewModel.SelectedSubForum = index;
                }
            }
        }

        private void OnOfficialEventTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null && border.Tag != null)
            {
                int evType;
                if (int.TryParse(border.Tag.ToString(), out evType) && ViewModel != null)
                {
                    ViewModel.SelectedOfficialEventType = evType;
                }
            }
        }

        private void OnCycleSortTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.CycleSortCommand != null)
            {
                ViewModel.CycleSortCommand.Execute(null);
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer) return (ScrollViewer)child;
                var sub = FindScrollViewer(child);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
