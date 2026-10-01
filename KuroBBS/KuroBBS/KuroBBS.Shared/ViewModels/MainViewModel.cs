using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<PostItem> _communityPosts;
        public ObservableCollection<PostItem> CommunityPosts
        {
            get { return _communityPosts; }
            set { _communityPosts = value; OnPropertyChanged(); }
        }

        private ObservableCollection<PostItem> _newsList;
        public ObservableCollection<PostItem> NewsList
        {
            get { return _newsList; }
            set { _newsList = value; OnPropertyChanged(); }
        }

        // Backward compatibility
        public ObservableCollection<PostItem> Posts
        {
            get { return _communityPosts; }
            set { _communityPosts = value; OnPropertyChanged(); }
        }

        private ObservableCollection<GameRoleCard> _roles;
        public ObservableCollection<GameRoleCard> Roles
        {
            get { return _roles; }
            set { _roles = value; OnPropertyChanged(); }
        }

        private SignInStatus _signInInfo;
        public SignInStatus SignInInfo
        {
            get { return _signInInfo; }
            set { _signInInfo = value; OnPropertyChanged(); }
        }

        private UserProfile _profile;
        public UserProfile Profile
        {
            get { return _profile; }
            set { _profile = value; OnPropertyChanged(); }
        }

        private int _selectedGameId = 3;
        public int SelectedGameId
        {
            get { return _selectedGameId <= 0 ? 3 : _selectedGameId; }
            set
            {
                int validatedId = (value == 2) ? 2 : 3;
                if (_selectedGameId != validatedId)
                {
                    _selectedGameId = validatedId;
                    _selectedSubForum = 0;
                    OnPropertyChanged();
                    OnPropertyChanged("SelectedGameName");
                    OnPropertyChanged("SubForumName0");
                    OnPropertyChanged("SubForumName1");
                    OnPropertyChanged("SubForumName2");
                    OnPropertyChanged("SubForumName3");
                    OnPropertyChanged("SubForumName4");
                    OnPropertyChanged("SelectedSubForum");
                    OnPropertyChanged("IsSubForum0");
                    OnPropertyChanged("IsSubForum1");
                    OnPropertyChanged("IsSubForum2");
                    OnPropertyChanged("IsSubForum3");
                    OnPropertyChanged("IsSubForum4");
                    OnPropertyChanged("IsEdenForum");
                    SettingsHelper.DefaultGameId = validatedId;
                    KuroLogger.Loading("SWITCH_GAME", "User selected GameId: " + validatedId + " (" + SelectedGameName + ")");
                    var t = ReloadFeedsAsync();
                }
            }
        }

        public string SelectedGameName
        {
            get
            {
                if (_selectedGameId == 2) return "战双帕弥什";
                return "鸣潮";
            }
        }

        // Sub-forums: 0: 推荐, 1: 伊甸闲庭, 2: 攻略, 3: 同人图, 4: 同人文
        private int _selectedSubForum = 0;
        public int SelectedSubForum
        {
            get { return _selectedSubForum; }
            set
            {
                if (_selectedSubForum != value)
                {
                    _selectedSubForum = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsSubForum0");
                    OnPropertyChanged("IsSubForum1");
                    OnPropertyChanged("IsSubForum2");
                    OnPropertyChanged("IsSubForum3");
                    OnPropertyChanged("IsSubForum4");
                    OnPropertyChanged("IsEdenForum");
                    OnPropertyChanged("IsSortableForum");
                    OnPropertyChanged("SelectedSubForumName");
                    KuroLogger.Loading("SWITCH_SUBFORUM", "Selected SubForum: " + value + " (" + SelectedSubForumName + ")");
                    var t = LoadCommunityPostsAsync(true);
                }
            }
        }

        public string SubForumName0 { get { return "推荐"; } }
        public string SubForumName1 { get { return _selectedGameId == 2 ? "伊甸闲庭" : "综合讨论"; } }
        public string SubForumName2 { get { return "攻略"; } }
        public string SubForumName3 { get { return _selectedGameId == 2 ? "同人图" : "同人"; } }
        public string SubForumName4 { get { return _selectedGameId == 2 ? "同人文" : "Cosplay"; } }

        public string SelectedSubForumName
        {
            get
            {
                switch (_selectedSubForum)
                {
                    case 1: return SubForumName1;
                    case 2: return SubForumName2;
                    case 3: return SubForumName3;
                    case 4: return SubForumName4;
                    default: return SubForumName0;
                }
            }
        }

        public bool IsSubForum0 { get { return _selectedSubForum == 0; } }
        public bool IsSubForum1 { get { return _selectedSubForum == 1; } }
        public bool IsSubForum2 { get { return _selectedSubForum == 2; } }
        public bool IsSubForum3 { get { return _selectedSubForum == 3; } }
        public bool IsSubForum4 { get { return _selectedSubForum == 4; } }
        public bool IsSortableForum { get { return _selectedSubForum > 0; } }
        public bool IsEdenForum { get { return _selectedSubForum > 0; } }

        // Official Event Types: 1: 活动, 3: 公告, 2: 资讯
        private int _selectedOfficialEventType = 1;
        public int SelectedOfficialEventType
        {
            get { return _selectedOfficialEventType; }
            set
            {
                if (_selectedOfficialEventType != value)
                {
                    _selectedOfficialEventType = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsOfficialEvent1");
                    OnPropertyChanged("IsOfficialEvent3");
                    OnPropertyChanged("IsOfficialEvent2");
                    OnPropertyChanged("SelectedOfficialEventTypeName");
                    KuroLogger.Loading("SWITCH_OFFICIAL_TAB", "Selected Official EventType: " + value + " (" + SelectedOfficialEventTypeName + ")");
                    var t = LoadNewsListAsync(true);
                }
            }
        }

        public bool IsOfficialEvent1 { get { return _selectedOfficialEventType == 1; } }
        public bool IsOfficialEvent3 { get { return _selectedOfficialEventType == 3; } }
        public bool IsOfficialEvent2 { get { return _selectedOfficialEventType == 2; } }

        public string SelectedOfficialEventTypeName
        {
            get
            {
                if (_selectedOfficialEventType == 3) return "公告";
                if (_selectedOfficialEventType == 2) return "资讯";
                return "活动";
            }
        }

        // Sort types: 3: 默认热门, 2: 最新回复, 1: 最新发布
        private int _selectedSearchType = 3;
        public int SelectedSearchType
        {
            get { return _selectedSearchType; }
            set
            {
                if (_selectedSearchType != value)
                {
                    _selectedSearchType = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsSortHot");
                    OnPropertyChanged("IsSortLatestReply");
                    OnPropertyChanged("IsSortLatestPublish");
                    OnPropertyChanged("SelectedSearchTypeName");
                    KuroLogger.Loading("SWITCH_SORT", "Selected Sort: " + SelectedSearchTypeName);
                    var t = LoadCommunityPostsAsync(true);
                }
            }
        }

        public bool IsSortHot { get { return _selectedSearchType == 3; } }
        public bool IsSortLatestReply { get { return _selectedSearchType == 2; } }
        public bool IsSortLatestPublish { get { return _selectedSearchType == 1; } }

        public string SelectedSearchTypeName
        {
            get
            {
                if (_selectedSearchType == 2) return "最新回复";
                if (_selectedSearchType == 1) return "最新发布";
                return "热门";
            }
        }

        private string _inputToken;
        public string InputToken
        {
            get { return _inputToken; }
            set { _inputToken = value; OnPropertyChanged(); }
        }

        // Login Mode: 0 = SMS Code, 1 = Token Direct
        private int _loginMode = 0;
        public int LoginMode
        {
            get { return _loginMode; }
            set
            {
                if (_loginMode != value)
                {
                    _loginMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsSmsLoginMode");
                    OnPropertyChanged("IsTokenLoginMode");
                }
            }
        }

        public bool IsSmsLoginMode { get { return _loginMode == 0; } }
        public bool IsTokenLoginMode { get { return _loginMode == 1; } }

        private string _loginMobile = "";
        public string LoginMobile
        {
            get { return _loginMobile; }
            set { _loginMobile = value; OnPropertyChanged(); }
        }

        private string _loginSmsCode = "";
        public string LoginSmsCode
        {
            get { return _loginSmsCode; }
            set { _loginSmsCode = value; OnPropertyChanged(); }
        }

        private int _countdownSeconds = 0;
        public int CountdownSeconds
        {
            get { return _countdownSeconds; }
            set
            {
                _countdownSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged("CountdownButtonText");
                OnPropertyChanged("IsCountingDown");
            }
        }

        public bool IsCountingDown { get { return _countdownSeconds > 0; } }

        public string CountdownButtonText
        {
            get { return _countdownSeconds > 0 ? string.Format("{0}s", _countdownSeconds) : "获取验证码"; }
        }

        private bool _isGeetestVisible = false;
        public bool IsGeetestVisible
        {
            get { return _isGeetestVisible; }
            set { _isGeetestVisible = value; OnPropertyChanged(); }
        }

        private string _geetestHtml = "";
        public string GeetestHtml
        {
            get { return _geetestHtml; }
            set { _geetestHtml = value; OnPropertyChanged(); }
        }

        public event EventHandler<string> RequestNavigateGeetest;

        private GameRoleCard _selectedRole;
        public GameRoleCard SelectedRole
        {
            get { return _selectedRole; }
            set
            {
                if (_selectedRole != value)
                {
                    _selectedRole = value;
                    OnPropertyChanged();
                    OnPropertyChanged("SelectedRoleDisplayName");
                    OnPropertyChanged("HasSelectedRole");
                    if (_selectedRole != null)
                    {
                        if (_selectedRole.GameId != _selectedGameId)
                        {
                            SelectedGameId = _selectedRole.GameId;
                        }
                        var t = LoadSignInInfoAsync();
                    }
                }
            }
        }

        public bool HasSelectedRole { get { return _selectedRole != null; } }

        public string SelectedRoleDisplayName
        {
            get
            {
                if (_selectedRole == null) return "未选择角色 (点击切换)";
                return string.Format("{0} · {1} ({2})", _selectedRole.GameName, _selectedRole.RoleName, _selectedRole.ServerName);
            }
        }

        private int _communityPage = 1;
        private int _newsPage = 1;
        private bool _hasMoreCommunity = true;
        private bool _hasMoreNews = true;

        private readonly HashSet<string> _communityPostIds = new HashSet<string>();
        private readonly HashSet<string> _newsPostIds = new HashSet<string>();

        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommunityCommand { get; private set; }
        public ICommand LoadMoreNewsCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }
        public ICommand SelectGameCommand { get; private set; }
        public ICommand SelectSubForumCommand { get; private set; }
        public ICommand SelectSortCommand { get; private set; }
        public ICommand CycleSortCommand { get; private set; }
        public ICommand SelectOfficialEventCommand { get; private set; }
        public ICommand ExecuteSignInCommand { get; private set; }
        public ICommand SaveTokenCommand { get; private set; }
        public ICommand SetLoginModeCommand { get; private set; }
        public ICommand RequestSmsCodeCommand { get; private set; }
        public ICommand LoginWithSmsCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand SelectRoleCommand { get; private set; }
        public ICommand CloseGeetestCommand { get; private set; }

        public MainViewModel()
        {
            CommunityPosts = new ObservableCollection<PostItem>();
            NewsList = new ObservableCollection<PostItem>();
            Roles = new ObservableCollection<GameRoleCard>();
            SignInInfo = new SignInStatus { GameName = "鸣潮", ConsecutiveDays = 0 };
            Profile = new UserProfile { UserName = SettingsHelper.UserName, IsLoggedIn = SettingsHelper.IsLoggedIn };
            _selectedGameId = SettingsHelper.DefaultGameId;
            InputToken = SettingsHelper.Token;

            RefreshCommand = new RelayCommand(async () => await RefreshAllAsync());
            LoadMoreCommunityCommand = new RelayCommand(async () => await LoadMoreCommunityAsync());
            LoadMoreNewsCommand = new RelayCommand(async () => await LoadMoreNewsAsync());
            LoadMoreCommand = new RelayCommand(async () => await LoadMoreCommunityAsync());

            SelectGameCommand = new RelayCommand(async p =>
            {
                int gId;
                if (p != null && int.TryParse(p.ToString(), out gId))
                {
                    SelectedGameId = gId;
                    if (Roles.Count > 0)
                    {
                        foreach (var r in Roles)
                        {
                            if (r.GameId == gId) { _selectedRole = r; OnPropertyChanged("SelectedRole"); OnPropertyChanged("SelectedRoleDisplayName"); OnPropertyChanged("HasSelectedRole"); break; }
                        }
                    }
                    await ReloadFeedsAsync();
                    await LoadSignInInfoAsync();
                }
            });

            SelectSubForumCommand = new RelayCommand(async p =>
            {
                int fId;
                if (p != null && int.TryParse(p.ToString(), out fId))
                {
                    SelectedSubForum = fId;
                    await LoadCommunityPostsAsync(true);
                }
            });

            SelectSortCommand = new RelayCommand(async p =>
            {
                int sType;
                if (p != null && int.TryParse(p.ToString(), out sType))
                {
                    SelectedSearchType = sType;
                    await LoadCommunityPostsAsync(true);
                }
            });

            CycleSortCommand = new RelayCommand(async () =>
            {
                if (SelectedSearchType == 3) SelectedSearchType = 2;
                else if (SelectedSearchType == 2) SelectedSearchType = 1;
                else SelectedSearchType = 3;
                await LoadCommunityPostsAsync(true);
            });

            SelectOfficialEventCommand = new RelayCommand(async p =>
            {
                int evType;
                if (p != null && int.TryParse(p.ToString(), out evType))
                {
                    SelectedOfficialEventType = evType;
                    await LoadNewsListAsync(true);
                }
            });

            SetLoginModeCommand = new RelayCommand(p =>
            {
                int m;
                if (p != null && int.TryParse(p.ToString(), out m))
                {
                    LoginMode = m;
                }
            });

            RequestSmsCodeCommand = new RelayCommand(async () => await OnRequestSmsCodeClickedAsync());
            LoginWithSmsCommand = new RelayCommand(async () => await OnLoginWithSmsClickedAsync());
            LogoutCommand = new RelayCommand(async () => await OnLogoutClickedAsync());
            CloseGeetestCommand = new RelayCommand(() => IsGeetestVisible = false);

            SelectRoleCommand = new RelayCommand(p =>
            {
                var role = p as GameRoleCard;
                if (role != null)
                {
                    SelectedRole = role;
                    KuroLogger.Loading("ROLE_SELECTED", "User switched active role to: " + role.RoleName + " (" + role.ServerName + ")");
                }
            });

            ExecuteSignInCommand = new RelayCommand(async () => await ExecuteSignInAsync());
            SaveTokenCommand = new RelayCommand(async () => await SaveTokenAndReloadAsync());

            KuroLogger.Loading("VIEWMODEL_INIT", "MainViewModel initialized");
        }

        public async Task InitializeAsync()
        {
            KuroLogger.ThreadInfo("UI Navigate", "MainPage NavigatedTo - Starting initial data synchronization");
            var _ = KuroEmojiService.Instance.InitializeAsync();
            await RefreshAllAsync();
        }

        public async Task RefreshUserProfileIfLoggedInAsync()
        {
            if (!SettingsHelper.IsLoggedIn)
            {
                if (Profile != null && Profile.IsLoggedIn)
                {
                    Profile = new UserProfile { UserName = "未登录用户", IsLoggedIn = false };
                    Roles.Clear();
                    SelectedRole = null;
                }
                return;
            }

            // If already loaded and valid, don't spam network requests upon returning from sub-pages
            if (Profile != null && Profile.IsLoggedIn && !string.IsNullOrEmpty(Profile.UserId) && Roles.Count > 0)
            {
                return;
            }

            await LoadUserAndSignInAsync();
        }

        private async Task RunOnUIThread(Action action)
        {
            try
            {
                Windows.UI.Core.CoreDispatcher dispatcher = null;
                if (Windows.UI.Xaml.Window.Current != null)
                {
                    dispatcher = Windows.UI.Xaml.Window.Current.Dispatcher;
                }
                else if (Windows.ApplicationModel.Core.CoreApplication.MainView != null && Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow != null)
                {
                    dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
                }

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                {
                    await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => action());
                    return;
                }
            }
            catch
            {
            }

            action();
        }

        public async Task RefreshAllAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "正在并发同步社区数据...";
            KuroLogger.Loading("SYNC_ALL", "Starting multi-threaded concurrent data synchronization");

            try
            {
                var feedsTask = ReloadFeedsAsync();
                var userTask = LoadUserAndSignInAsync();

                await Task.WhenAll(feedsTask, userTask);
                StatusMessage = "数据同步完成";
                KuroLogger.Loading("SYNC_DONE", string.Format("Sync completed. Loaded {0} community posts, {1} news, {2} roles", CommunityPosts.Count, NewsList.Count, Roles.Count));
            }
            catch (Exception ex)
            {
                StatusMessage = "加载失败: " + ex.Message;
                KuroLogger.Error("SYNC_ERROR", "Full sync error: " + ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadUserAndSignInAsync()
        {
            await LoadProfileAndRolesAsync();
            await LoadSignInInfoAsync();
        }

        public async Task ReloadFeedsAsync()
        {
            var t1 = LoadCommunityPostsAsync(true);
            var t2 = LoadNewsListAsync(true);
            await Task.WhenAll(t1, t2);
        }

        private int GetActualForumId()
        {
            if (SelectedGameId == 2) // 战双帕弥什
            {
                switch (SelectedSubForum)
                {
                    case 0: return 2; // 推荐
                    case 1: return 3; // 伊甸闲庭
                    case 2: return 4; // 攻略
                    case 3: return 5; // 同人图
                    case 4: return 16; // 同人文
                    default: return 2;
                }
            }
            else // 鸣潮 (gameId == 3)
            {
                switch (SelectedSubForum)
                {
                    case 0: return 9; // 推荐
                    case 1: return 10; // 综合讨论
                    case 2: return 12; // 攻略
                    case 3: return 11; // 同人
                    case 4: return 17; // Cosplay
                    default: return 9;
                }
            }
        }

        private bool _isLoadingCommunity = false;

        public async Task LoadCommunityPostsAsync(bool resetPage = false)
        {
            if (resetPage)
            {
                _communityPage = 1;
                _hasMoreCommunity = true;
            }

            if (!_hasMoreCommunity && !resetPage) return;
            if (_isLoadingCommunity) return;
            _isLoadingCommunity = true;

            try
            {
                int forumId = GetActualForumId();
                var list = await KuroForumService.Instance.GetCommunityPostsAsync(SelectedGameId, forumId, SelectedSearchType, _communityPage, 20);
                
                await RunOnUIThread(() =>
                {
                    if (resetPage)
                    {
                        _communityPostIds.Clear();
                        CommunityPosts.Clear();
                    }

                    if (list == null || list.Count == 0)
                    {
                        _hasMoreCommunity = false;
                    }
                    else
                    {
                        int newItemsCount = 0;
                        foreach (var item in list)
                        {
                            string idKey = !string.IsNullOrEmpty(item.PostId) ? item.PostId : item.Title;
                            if (!string.IsNullOrEmpty(idKey))
                            {
                                if (_communityPostIds.Contains(idKey)) continue;
                                _communityPostIds.Add(idKey);
                            }

                            CommunityPosts.Add(item);
                            newItemsCount++;
                        }

                        if (newItemsCount == 0)
                        {
                            _hasMoreCommunity = false;
                        }
                    }
                });

                KuroLogger.Loading("COMMUNITY_RENDER", string.Format("Rendered {0} unique community posts (Total: {1})", list != null ? list.Count : 0, CommunityPosts.Count));
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("COMMUNITY_LOAD_ERR", "Failed to load community posts: " + ex.Message);
            }
            finally
            {
                _isLoadingCommunity = false;
            }
        }

        public async Task LoadMoreCommunityAsync()
        {
            if (_isLoadingCommunity || !_hasMoreCommunity) return;
            _communityPage++;
            KuroLogger.Loading("COMMUNITY_PAGE_MORE", string.Format("Auto-loading community page {0} for {1}", _communityPage, SelectedSubForumName));
            await LoadCommunityPostsAsync(false);
        }

        public async Task LoadNewsListAsync(bool resetPage = false)
        {
            if (resetPage)
            {
                _newsPage = 1;
                _hasMoreNews = true;
            }

            if (!_hasMoreNews && !resetPage) return;

            var list = await KuroForumService.Instance.GetOfficialEventsAsync(SelectedGameId, SelectedOfficialEventType, _newsPage, 10);

            await RunOnUIThread(() =>
            {
                if (resetPage)
                {
                    _newsPostIds.Clear();
                    NewsList.Clear();
                }

                if (list == null || list.Count == 0)
                {
                    _hasMoreNews = false;
                }
                else
                {
                    int newItemsCount = 0;
                    foreach (var item in list)
                    {
                        string idKey = !string.IsNullOrEmpty(item.PostId) ? item.PostId : item.Title;
                        if (!string.IsNullOrEmpty(idKey))
                        {
                            if (_newsPostIds.Contains(idKey)) continue;
                            _newsPostIds.Add(idKey);
                        }

                        NewsList.Add(item);
                        newItemsCount++;
                    }

                    if (newItemsCount == 0)
                    {
                        _hasMoreNews = false;
                    }
                }
            });

            KuroLogger.Loading("NEWS_RENDER", string.Format("Rendered {0} unique news items (Total: {1})", list != null ? list.Count : 0, NewsList.Count));
        }

        public async Task LoadMoreNewsAsync()
        {
            if (IsBusy || !_hasMoreNews) return;
            IsBusy = true;
            _newsPage++;
            KuroLogger.Loading("NEWS_PAGE_MORE", "Loading news page " + _newsPage);
            await LoadNewsListAsync(false);
            IsBusy = false;
        }

        public async Task LoadProfileAndRolesAsync()
        {
            KuroLogger.Loading("LOAD_USER", "Fetching user profile and bound roles");
            var profile = await KuroUserService.Instance.GetUserProfileAsync();
            var rolesList = await KuroUserService.Instance.GetGameRolesAsync();

            await RunOnUIThread(() =>
            {
                Profile = profile;
                Roles.Clear();
                foreach (var r in rolesList)
                {
                    Roles.Add(r);
                }

                if (SelectedRole == null || !Roles.Contains(SelectedRole))
                {
                    GameRoleCard match = null;
                    foreach (var r in Roles)
                    {
                        if (r.GameId == SelectedGameId)
                        {
                            if (match == null || r.IsDefault) match = r;
                        }
                    }
                    SelectedRole = match ?? (Roles.Count > 0 ? Roles[0] : null);
                }
            });
        }

        public async Task LoadSignInInfoAsync()
        {
            var role = SelectedRole;
            if (role == null && Roles.Count > 0)
            {
                foreach (var r in Roles)
                {
                    if (r.GameId == SelectedGameId) { role = r; break; }
                }
                if (role == null) role = Roles[0];
            }

            string serverId = role != null ? role.ServerId : "";
            string roleId = role != null ? role.RoleId : "";

            KuroLogger.Loading("SIGNIN_STATUS", string.Format("Querying sign-in status for {0} (Role={1}, Server={2})", SelectedGameName, roleId, serverId));
            var info = await KuroSignInService.Instance.GetSignInStatusAsync(SelectedGameId, serverId, roleId, Profile != null ? Profile.UserId : "");
            await RunOnUIThread(() =>
            {
                SignInInfo = info;
            });
        }

        public async Task ExecuteSignInAsync()
        {
            if (!SettingsHelper.IsLoggedIn)
            {
                StatusMessage = "请先在【我的】页面填入 Token 进行登录";
                KuroLogger.Warn("SIGNIN_ABORT", "Sign-in aborted: User not logged in");
                return;
            }

            var role = SelectedRole;
            if (role == null && Roles.Count == 0)
            {
                await LoadProfileAndRolesAsync();
                role = SelectedRole;
            }

            if (role == null && Roles.Count > 0)
            {
                foreach (var r in Roles)
                {
                    if (r.GameId == SelectedGameId) { role = r; break; }
                }
                if (role == null) role = Roles[0];
            }

            if (role == null)
            {
                StatusMessage = "未找到已绑定的游戏角色，请先在官方 App 绑定角色";
                KuroLogger.Warn("SIGNIN_NO_ROLE", "No bound game role found for sign in");
                return;
            }

            IsBusy = true;
            string serverId = role.ServerId ?? "";
            string roleId = role.RoleId ?? "";
            string reqMonth = (SignInInfo != null && !string.IsNullOrEmpty(SignInInfo.ReqMonth)) ? SignInInfo.ReqMonth : DateTime.Now.Month.ToString("D2");
            KuroLogger.Loading("SIGNIN_EXEC", string.Format("Executing daily sign-in for Role={0} ({1}), Server={2}, GameId={3}, Month={4}", role.RoleName, roleId, serverId, SelectedGameId, reqMonth));
            var result = await KuroSignInService.Instance.ExecuteDailySignInAsync(SelectedGameId, serverId, roleId, Profile.UserId, reqMonth);
            
            if (result != null && result.Success && !result.AlreadySignedIn)
            {
                StatusMessage = "签到成功！奖励已发放至 " + role.RoleName;
                string toastDesc = !string.IsNullOrEmpty(result.RewardSummary) ? result.RewardSummary : "奖励已发放至角色邮箱";
                NotificationHelper.ShowToast("库街区每日签到", string.Format("【{0}】签到成功！{1}", role.RoleName, toastDesc));
                KuroLogger.Info("SIGNIN_SUCCESS", "Sign in successful! " + toastDesc);
                await LoadSignInInfoAsync();
            }
            else if (result != null && result.AlreadySignedIn)
            {
                StatusMessage = "今日已完成签到 (" + role.RoleName + ")";
                NotificationHelper.ShowToast("库街区每日签到", string.Format("【{0}】今日已完成签到，无需重复打卡", role.RoleName));
                KuroLogger.Info("SIGNIN_ALREADY", "User already signed in today for " + role.RoleName);
                await LoadSignInInfoAsync();
            }
            else
            {
                string errMsg = result != null && !string.IsNullOrEmpty(result.Message) ? result.Message : "未知错误";
                StatusMessage = "签到提示: " + errMsg;
                NotificationHelper.ShowToast("库街区每日签到", string.Format("【{0}】签到提示: {1}", role.RoleName, errMsg));
                KuroLogger.Warn("SIGNIN_FAILED", "Sign-in returned unsuccessful: " + errMsg);
            }

            IsBusy = false;
        }

        public async Task SaveTokenAndReloadAsync()
        {
            SettingsHelper.Token = InputToken != null ? InputToken.Trim() : "";
            KuroLogger.Info("TOKEN_SAVED", "New Kuro Token saved to LocalSettings");
            StatusMessage = "登录凭证已保存，正在重新加载...";
            await RefreshAllAsync();
        }

        public async Task OnRequestSmsCodeClickedAsync()
        {
            if (IsCountingDown) return;

            string mobile = LoginMobile != null ? LoginMobile.Trim() : "";
            if (string.IsNullOrEmpty(mobile) || mobile.Length < 11)
            {
                StatusMessage = "请输入正确的 11 位手机号码";
                KuroLogger.Warn("SMS_INPUT", "Invalid mobile phone number input: " + mobile);
                return;
            }

            IsBusy = true;
            StatusMessage = "正在请求发送验证码...";

            var result = await KuroAuthService.Instance.RequestSmsCodeAsync(mobile, "");
            IsBusy = false;

            if (result.NeedGeetest)
            {
                TriggerGeetest(result.CaptchaId);
            }
            else if (result.Success)
            {
                StatusMessage = "验证码已发送至 " + mobile;
                StartCountdown();
            }
            else
            {
                StatusMessage = "发送失败: " + result.Message;
                // If it might be an unhandled risk, let user try Geetest
                if (!string.IsNullOrEmpty(result.Message) && (result.Message.Contains("安全") || result.Message.Contains("频繁") || result.Message.Contains("验证")))
                {
                    TriggerGeetest(KuroAuthService.DefaultCaptchaId);
                }
            }
        }

        public void TriggerGeetest(string captchaId)
        {
            if (string.IsNullOrEmpty(captchaId))
            {
                captchaId = KuroAuthService.DefaultCaptchaId;
            }

            KuroLogger.Loading("GEETEST_OPEN", "Opening Geetest GT4 Verification Modal (CaptchaId: " + captchaId + ")");
            GeetestHtml = BuildGeetestHtml(captchaId);
            IsGeetestVisible = true;

            if (RequestNavigateGeetest != null)
            {
                RequestNavigateGeetest(this, GeetestHtml);
            }
        }

        public async Task OnGeetestValidatedAsync(string validateJson)
        {
            IsGeetestVisible = false;
            IsBusy = true;
            StatusMessage = "人机验证通过，正在下发短信验证码...";
            KuroLogger.Info("GEETEST_VALIDATED", "Geetest GT4 passed. Requesting SMS code with verification payload.");

            string mobile = LoginMobile != null ? LoginMobile.Trim() : "";
            var result = await KuroAuthService.Instance.RequestSmsCodeAsync(mobile, validateJson);
            IsBusy = false;

            if (result.Success)
            {
                StatusMessage = "验证码已成功发送至 " + mobile;
                StartCountdown();
            }
            else
            {
                StatusMessage = "发送失败: " + result.Message;
            }
        }

        public void OnGeetestFailed(string errMessage)
        {
            IsGeetestVisible = false;
            StatusMessage = "极验验证失败或已取消: " + errMessage;
            KuroLogger.Warn("GEETEST_FAIL", "Geetest challenge aborted or failed: " + errMessage);
        }

        public async Task OnLoginWithSmsClickedAsync()
        {
            string mobile = LoginMobile != null ? LoginMobile.Trim() : "";
            string code = LoginSmsCode != null ? LoginSmsCode.Trim() : "";

            if (string.IsNullOrEmpty(mobile) || mobile.Length < 11)
            {
                StatusMessage = "请输入正确的 11 位手机号码";
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                StatusMessage = "请输入短信验证码";
                return;
            }

            IsBusy = true;
            StatusMessage = "正在登录库街区...";

            var result = await KuroAuthService.Instance.LoginWithSmsAsync(mobile, code);
            IsBusy = false;

            if (result.Success)
            {
                StatusMessage = "登录成功！欢迎 " + result.UserName;
                InputToken = result.Token;
                LoginSmsCode = "";
                await RefreshAllAsync();
            }
            else
            {
                StatusMessage = "登录失败: " + result.Message;
            }
        }

        public async Task OnLogoutClickedAsync()
        {
            IsBusy = true;
            StatusMessage = "正在退出登录...";
            await KuroAuthService.Instance.LogoutAsync();
            InputToken = "";
            LoginMobile = "";
            LoginSmsCode = "";
            CountdownSeconds = 0;
            Profile = new UserProfile { UserName = "未登录", IsLoggedIn = false };
            Roles.Clear();
            SignInInfo = new SignInStatus { GameName = SelectedGameName, ConsecutiveDays = 0 };
            StatusMessage = "已退出登录";
            IsBusy = false;
            await RefreshAllAsync();
        }

        private async void StartCountdown()
        {
            CountdownSeconds = 60;
            while (CountdownSeconds > 0)
            {
                await Task.Delay(1000);
                CountdownSeconds--;
            }
        }

        public static string BuildGeetestHtml(string captchaId)
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta http-equiv='X-UA-Compatible' content='IE=edge' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; -ms-touch-action: pan-y; touch-action: pan-y; }
        html, body {
            width: 100%;
            height: 100%;
            background-color: #121218;
            color: #FFFFFF;
            font-family: 'Segoe UI', 'Microsoft YaHei', sans-serif;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            overflow: hidden;
        }
        #captcha-box {
            width: 100%;
            min-height: 220px;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
            padding: 8px;
        }
        .status-text {
            font-size: 13px;
            color: #8E8E93;
            margin-bottom: 12px;
            text-align: center;
        }
        .retry-btn {
            display: none;
            margin-top: 10px;
            padding: 8px 20px;
            background: #00A3FF;
            color: white;
            border: none;
            font-size: 13px;
            cursor: pointer;
        }
    </style>
    <script>
        window._gtStatus = 'loading';
        window._gtData = '';

        function sendNotify(type, payload) {
            window._gtStatus = type;
            window._gtData = payload || '';
            try {
                if (window.external && window.external.notify) {
                    window.external.notify(JSON.stringify({ type: type, data: payload }));
                }
            } catch(e) {}
        }

        window.getGeetestState = function() {
            var s = window._gtStatus;
            var d = window._gtData;
            if (s === 'success' || s === 'error' || s === 'close') {
                window._gtStatus = 'consumed';
            }
            return JSON.stringify({ type: s, data: d });
        };

        window.onerror = function(msg, url, line, col, err) {
            var detail = 'JS Error: ' + msg + ' (' + (url || 'inline') + ':' + line + ':' + col + ')';
            sendNotify('error', detail);
            var tip = document.getElementById('status-tip');
            if (tip) tip.innerText = detail;
            var btn = document.getElementById('retry-btn');
            if (btn) btn.style.display = 'block';
            return false;
        };

        // Polyfill Object.assign
        if (typeof Object.assign !== 'function') {
            Object.assign = function(target) {
                if (target == null) throw new TypeError('Cannot convert undefined or null to object');
                var to = Object(target);
                for (var index = 1; index < arguments.length; index++) {
                    var nextSource = arguments[index];
                    if (nextSource != null) {
                        for (var nextKey in nextSource) {
                            if (Object.prototype.hasOwnProperty.call(nextSource, nextKey)) {
                                to[nextKey] = nextSource[nextKey];
                            }
                        }
                    }
                }
                return to;
            };
        }

        // Polyfill ES6 Promise for IE11 Chakra
        (function(w) {
            if (typeof w.Promise === 'function') return;
            function P(fn) {
                var st = 'pending', val, def = [];
                function h(handler) {
                    if (st === 'pending') { def.push(handler); return; }
                    setTimeout(function() {
                        var cb = (st === 'fulfilled') ? handler.onFulfilled : handler.onRejected;
                        if (!cb) {
                            if (st === 'fulfilled') handler.resolve(val);
                            else handler.reject(val);
                            return;
                        }
                        try { handler.resolve(cb(val)); } catch(e) { handler.reject(e); }
                    }, 0);
                }
                function res(n) {
                    if (n && (typeof n === 'object' || typeof n === 'function')) {
                        var t = n.then;
                        if (typeof t === 'function') { t.call(n, res, rej); return; }
                    }
                    st = 'fulfilled'; val = n;
                    for (var i = 0; i < def.length; i++) h(def[i]);
                }
                function rej(r) {
                    st = 'rejected'; val = r;
                    for (var i = 0; i < def.length; i++) h(def[i]);
                }
                this.then = function(onF, onR) {
                    return new P(function(r, j) { h({ onFulfilled: onF, onRejected: onR, resolve: r, reject: j }); });
                };
                this['catch'] = function(onR) { return this.then(null, onR); };
                try { fn(res, rej); } catch(e) { rej(e); }
            }
            P.resolve = function(v) { return new P(function(r) { r(v); }); };
            P.reject = function(e) { return new P(function(r, j) { j(e); }); };
            P.all = function(arr) {
                return new P(function(resolve, reject) {
                    var out = [], rem = arr.length;
                    if (rem === 0) return resolve(out);
                    function item(i) {
                        return function(v) { out[i] = v; if (--rem === 0) resolve(out); };
                    }
                    for (var i = 0; i < arr.length; i++) P.resolve(arr[i]).then(item(i), reject);
                });
            };
            w.Promise = P;
        })(window);
    </script>
</head>
<body>
    <div id='captcha-box'>
        <div class='status-text' id='status-tip'>正在安全加载极验人机验证...</div>
        <button class='retry-btn' id='retry-btn' onclick='initCaptcha()'>重新加载</button>
    </div>

    <script>
        var currentCaptchaId = '" + captchaId + @"';
        var cdnList = [
            'https://static.geetest.com/v4/gt4.js',
            'https://gcaptcha4.geetest.com/gt4.js',
            'https://static.geevisit.com/v4/gt4.js'
        ];
        var cdnIdx = 0;

        function tryNextCdn() {
            if (typeof window.initGeetest4 === 'function') {
                runGeetest();
                return;
            }
            if (cdnIdx >= cdnList.length) {
                var tip = document.getElementById('status-tip');
                if (tip) tip.innerText = '极验组件加载失败，请检查网络后点击重试';
                var btn = document.getElementById('retry-btn');
                if (btn) btn.style.display = 'inline-block';
                sendNotify('error', 'All Geetest CDN endpoints failed to load');
                return;
            }
            var targetSrc = cdnList[cdnIdx++];
            sendNotify('log', 'Loading Geetest SDK: ' + targetSrc);
            var s = document.createElement('script');
            s.type = 'text/javascript';
            s.src = targetSrc;
            s.async = true;
            s.onload = s.onreadystatechange = function() {
                if (!this.readyState || this.readyState === 'loaded' || this.readyState === 'complete') {
                    s.onload = s.onreadystatechange = null;
                    sendNotify('log', 'Geetest script loaded from ' + targetSrc);
                    if (typeof window.initGeetest4 === 'function') {
                        runGeetest();
                    } else {
                        tryNextCdn();
                    }
                }
            };
            s.onerror = function() {
                sendNotify('log', 'Failed to load script from ' + targetSrc);
                tryNextCdn();
            };
            (document.head || document.body).appendChild(s);
        }

        function initCaptcha() {
            var tip = document.getElementById('status-tip');
            if (tip) {
                tip.innerText = '正在初始化极验安全验证...';
                tip.style.display = 'block';
            }
            var btn = document.getElementById('retry-btn');
            if (btn) btn.style.display = 'none';

            if (typeof window.initGeetest4 === 'function') {
                runGeetest();
            } else {
                cdnIdx = 0;
                tryNextCdn();
            }
        }

        function runGeetest() {
            try {
                sendNotify('log', 'Calling initGeetest4 (CaptchaId: ' + currentCaptchaId + ')');
                initGeetest4({
                    captchaId: currentCaptchaId,
                    product: 'bind',
                    riskType: 'slide',
                    language: 'zho',
                    https: true,
                    protocol: 'https://',
                    timeout: 15000
                }, function (gt) {
                    window.captchaObj = gt;
                    sendNotify('log', 'Geetest instance initialized');
                    gt.appendTo('#captcha-box');
                    
                    gt.onReady(function () {
                        sendNotify('ready', 'Geetest GT4 UI is ready');
                        var tip = document.getElementById('status-tip');
                        if (tip) tip.style.display = 'none';
                        if (typeof gt.showCaptcha === 'function') {
                            try { gt.showCaptcha(); } catch(e) {}
                        } else if (typeof gt.showBox === 'function') {
                            try { gt.showBox(); } catch(e) {}
                        }
                    }).onSuccess(function () {
                        var validate = gt.getValidate();
                        if (validate) {
                            if (!validate.captcha_id) {
                                validate.captcha_id = currentCaptchaId;
                            }
                            sendNotify('success', JSON.stringify(validate));
                        }
                    }).onError(function (err) {
                        var msg = (err && (err.msg || err.message)) ? (err.msg || err.message) : JSON.stringify(err);
                        sendNotify('error', msg);
                    }).onClose(function () {
                        sendNotify('close', '');
                    });
                });
            } catch(ex) {
                sendNotify('error', 'runGeetest error: ' + (ex.message || ex.toString()));
            }
        }

        sendNotify('log', 'Geetest HTML mounted, starting initCaptcha');
        initCaptcha();
    </script>
</body>
</html>";
        }
    }
}

