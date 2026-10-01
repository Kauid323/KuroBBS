using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class UserProfileViewModel : ViewModelBase
    {
        private string _userId;
        public string UserId
        {
            get { return _userId; }
            set { _userId = value; OnPropertyChanged(); }
        }

        private UserProfile _profile;
        public UserProfile Profile
        {
            get { return _profile; }
            set
            {
                _profile = value;
                OnPropertyChanged();
                OnPropertyChanged("CanFollow");
                OnPropertyChanged("HeaderDisplayTitle");
            }
        }

        public string HeaderDisplayTitle
        {
            get
            {
                if (_profile != null && !string.IsNullOrEmpty(_profile.UserName))
                {
                    return _profile.UserName;
                }
                return "用户详情";
            }
        }

        public bool CanFollow
        {
            get
            {
                if (_profile == null || string.IsNullOrEmpty(_profile.UserId)) return false;
                if (!string.IsNullOrEmpty(SettingsHelper.UserId) && _profile.UserId == SettingsHelper.UserId) return false;
                return true;
            }
        }

        private ObservableCollection<PostItem> _posts;
        public ObservableCollection<PostItem> Posts
        {
            get { return _posts; }
            set { _posts = value; OnPropertyChanged(); }
        }

        private ObservableCollection<PostItem> _collections;
        public ObservableCollection<PostItem> Collections
        {
            get { return _collections; }
            set { _collections = value; OnPropertyChanged(); }
        }

        private ObservableCollection<UserCommentNoticeItem> _userComments;
        public ObservableCollection<UserCommentNoticeItem> UserComments
        {
            get { return _userComments; }
            set { _userComments = value; OnPropertyChanged(); }
        }

        private ObservableCollection<GameRoleCard> _gameRoles;
        public ObservableCollection<GameRoleCard> GameRoles
        {
            get { return _gameRoles; }
            set 
            { 
                _gameRoles = value; 
                OnPropertyChanged(); 
                OnPropertyChanged("HasGameRoles");
            }
        }

        public bool HasGameRoles
        {
            get { return _gameRoles != null && _gameRoles.Count > 0; }
        }

        private bool _isLoadingPosts = false;
        public bool IsLoadingPosts
        {
            get { return _isLoadingPosts; }
            set { _isLoadingPosts = value; OnPropertyChanged(); }
        }

        private bool _hasNoPosts = false;
        public bool HasNoPosts
        {
            get { return _hasNoPosts; }
            set { _hasNoPosts = value; OnPropertyChanged(); }
        }

        private bool _isLoadingCollections = false;
        public bool IsLoadingCollections
        {
            get { return _isLoadingCollections; }
            set { _isLoadingCollections = value; OnPropertyChanged(); }
        }

        private bool _hasNoCollections = false;
        public bool HasNoCollections
        {
            get { return _hasNoCollections; }
            set { _hasNoCollections = value; OnPropertyChanged(); }
        }

        private bool _isLoadingComments = false;
        public bool IsLoadingComments
        {
            get { return _isLoadingComments; }
            set { _isLoadingComments = value; OnPropertyChanged(); }
        }

        private bool _hasNoComments = false;
        public bool HasNoComments
        {
            get { return _hasNoComments; }
            set { _hasNoComments = value; OnPropertyChanged(); }
        }

        private bool _isLoadingRoles = false;
        public bool IsLoadingRoles
        {
            get { return _isLoadingRoles; }
            set { _isLoadingRoles = value; OnPropertyChanged(); }
        }

        private bool _hasNoRoles = false;
        public bool HasNoRoles
        {
            get { return _hasNoRoles; }
            set { _hasNoRoles = value; OnPropertyChanged(); }
        }

        private int _currentPage = 1;
        private bool _hasMorePosts = true;

        private int _collectionsPage = 1;
        private bool _hasMoreCollections = true;

        private int _commentsPage = 1;
        private bool _hasMoreComments = true;

        private bool _isImageViewerOpen;
        public bool IsImageViewerOpen
        {
            get { return _isImageViewerOpen; }
            set { _isImageViewerOpen = value; OnPropertyChanged(); }
        }

        private string _selectedImageUrl;
        public string SelectedImageUrl
        {
            get { return _selectedImageUrl; }
            set { _selectedImageUrl = value; OnPropertyChanged(); }
        }

        public ICommand ToggleFollowCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMorePostsCommand { get; private set; }
        public ICommand LoadMoreCollectionsCommand { get; private set; }
        public ICommand LoadMoreCommentsCommand { get; private set; }
        public ICommand ToggleCommentLikeCommand { get; private set; }
        public ICommand OpenImageCommand { get; private set; }
        public ICommand CloseImageCommand { get; private set; }

        public UserProfileViewModel()
        {
            Profile = new UserProfile();
            Posts = new ObservableCollection<PostItem>();
            Collections = new ObservableCollection<PostItem>();
            UserComments = new ObservableCollection<UserCommentNoticeItem>();
            GameRoles = new ObservableCollection<GameRoleCard>();

            ToggleFollowCommand = new RelayCommand(async () => await ToggleFollowAsync());
            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            LoadMorePostsCommand = new RelayCommand(async () => await LoadMorePostsAsync());
            LoadMoreCollectionsCommand = new RelayCommand(async () => await LoadMoreCollectionsAsync());
            LoadMoreCommentsCommand = new RelayCommand(async () => await LoadMoreCommentsAsync());
            ToggleCommentLikeCommand = new RelayCommand<UserCommentNoticeItem>(async (item) => await ToggleCommentLikeAsync(item));
            OpenImageCommand = new RelayCommand<string>((url) =>
            {
                if (!string.IsNullOrEmpty(url))
                {
                    SelectedImageUrl = url;
                    IsImageViewerOpen = true;
                }
            });
            CloseImageCommand = new RelayCommand(() =>
            {
                IsImageViewerOpen = false;
                SelectedImageUrl = null;
            });
        }

        public async Task InitializeAsync(string userId, PostAuthor initialAuthor = null)
        {
            _userId = userId ?? "";

            if (initialAuthor != null)
            {
                Profile.UserId = initialAuthor.UserId;
                Profile.UserName = initialAuthor.UserName;
                Profile.AvatarUrl = initialAuthor.AvatarUrl;
                Profile.IpRegion = initialAuthor.IpRegion;
                Profile.IsFollow = initialAuthor.IsFollow;
                OnPropertyChanged("HeaderDisplayTitle");
                OnPropertyChanged("CanFollow");
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                Profile.UserId = userId;
                Profile.UserName = "用户_" + userId;
                OnPropertyChanged("HeaderDisplayTitle");
            }

            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            _currentPage = 1;
            _hasMorePosts = true;
            _collectionsPage = 1;
            _hasMoreCollections = true;
            _commentsPage = 1;
            _hasMoreComments = true;

            try
            {
                KuroLogger.Loading("USER_PROFILE_LOAD", "Loading profile for UserId: " + _userId);
                var fullProfile = await KuroUserService.Instance.GetUserProfileDetailAsync(_userId);
                if (fullProfile != null)
                {
                    Profile = fullProfile;
                }

                // Load posts
                IsLoadingPosts = true;
                var postList = await KuroUserService.Instance.GetUserPostsAsync(_userId, _currentPage, 15);
                Posts.Clear();
                foreach (var p in postList)
                {
                    Posts.Add(p);
                }
                HasNoPosts = (Posts.Count == 0);
                _hasMorePosts = postList.Count >= 15;
                IsLoadingPosts = false;

                // Load collections
                IsLoadingCollections = true;
                var collList = await KuroUserService.Instance.GetUserCollectionsAsync(_userId, _collectionsPage, 20);
                Collections.Clear();
                foreach (var c in collList)
                {
                    Collections.Add(c);
                }
                HasNoCollections = (Collections.Count == 0);
                _hasMoreCollections = collList.Count >= 20;
                IsLoadingCollections = false;

                // Load comments
                IsLoadingComments = true;
                var commList = await KuroUserService.Instance.GetUserCommentsAsync(_userId, _commentsPage, 20);
                UserComments.Clear();
                foreach (var comm in commList)
                {
                    UserComments.Add(comm);
                }
                HasNoComments = (UserComments.Count == 0);
                _hasMoreComments = commList.Count >= 20;
                IsLoadingComments = false;

                // Load user default roles (via POST /user/role/findUserDefaultRole)
                IsLoadingRoles = true;
                var roles = await KuroUserService.Instance.GetUserDefaultRolesAsync(_userId);
                if ((roles == null || roles.Count == 0) && (Profile.IsLoginUser || Profile.UserId == SettingsHelper.UserId))
                {
                    roles = await KuroUserService.Instance.GetGameRolesAsync();
                }

                GameRoles.Clear();
                if (roles != null)
                {
                    foreach (var r in roles)
                    {
                        GameRoles.Add(r);
                    }
                }
                HasNoRoles = (GameRoles.Count == 0);
                OnPropertyChanged("HasGameRoles");
                IsLoadingRoles = false;
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_PROFILE_ERR", "Failed loading user profile: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                IsLoadingPosts = false;
                IsLoadingCollections = false;
                IsLoadingComments = false;
                IsLoadingRoles = false;
                OnPropertyChanged("CanFollow");
                OnPropertyChanged("HeaderDisplayTitle");
            }
        }

        public async Task LoadMorePostsAsync()
        {
            if (IsLoadingPosts || !_hasMorePosts || string.IsNullOrEmpty(_userId)) return;

            IsLoadingPosts = true;
            try
            {
                _currentPage++;
                var nextPosts = await KuroUserService.Instance.GetUserPostsAsync(_userId, _currentPage, 15);
                if (nextPosts.Count > 0)
                {
                    foreach (var p in nextPosts)
                    {
                        Posts.Add(p);
                    }
                    _hasMorePosts = nextPosts.Count >= 15;
                }
                else
                {
                    _hasMorePosts = false;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_POSTS_MORE_ERR", "Failed loading more user posts: " + ex.Message);
            }
            finally
            {
                IsLoadingPosts = false;
            }
        }

        public async Task LoadMoreCollectionsAsync()
        {
            if (IsLoadingCollections || !_hasMoreCollections || string.IsNullOrEmpty(_userId)) return;

            IsLoadingCollections = true;
            try
            {
                _collectionsPage++;
                var nextColls = await KuroUserService.Instance.GetUserCollectionsAsync(_userId, _collectionsPage, 20);
                if (nextColls.Count > 0)
                {
                    foreach (var c in nextColls)
                    {
                        Collections.Add(c);
                    }
                    _hasMoreCollections = nextColls.Count >= 20;
                }
                else
                {
                    _hasMoreCollections = false;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_COLL_MORE_ERR", "Failed loading more user collections: " + ex.Message);
            }
            finally
            {
                IsLoadingCollections = false;
            }
        }

        public async Task LoadMoreCommentsAsync()
        {
            if (IsLoadingComments || !_hasMoreComments || string.IsNullOrEmpty(_userId)) return;

            IsLoadingComments = true;
            try
            {
                _commentsPage++;
                var nextComms = await KuroUserService.Instance.GetUserCommentsAsync(_userId, _commentsPage, 20);
                if (nextComms.Count > 0)
                {
                    foreach (var comm in nextComms)
                    {
                        UserComments.Add(comm);
                    }
                    _hasMoreComments = nextComms.Count >= 20;
                }
                else
                {
                    _hasMoreComments = false;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("USER_COMM_MORE_ERR", "Failed loading more user comments: " + ex.Message);
            }
            finally
            {
                IsLoadingComments = false;
            }
        }

        public async Task ToggleCommentLikeAsync(UserCommentNoticeItem item)
        {
            if (item == null) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先登录账号后再点赞", "提示");
                return;
            }

            bool wasLiked = item.IsLiked;
            bool success = await KuroForumService.Instance.LikeCommentAsync(
                item.PostId,
                item.PostCommentId,
                item.PostCommentReplyId,
                item.SendUserId,
                item.GameId > 0 ? item.GameId : 2,
                item.ForumId > 0 ? item.ForumId : 4,
                wasLiked);

            if (success)
            {
                item.IsLiked = !wasLiked;
                item.LikeCount += item.IsLiked ? 1 : -1;
                if (item.LikeCount < 0) item.LikeCount = 0;
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }

        private async Task ToggleFollowAsync()
        {
            if (Profile == null || string.IsNullOrEmpty(Profile.UserId)) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先在主界面登录账号", "提示");
                return;
            }

            bool newFollow = !Profile.IsFollow;
            bool success = await KuroUserService.Instance.FollowUserAsync(Profile.UserId, newFollow);
            if (success)
            {
                Profile.IsFollow = newFollow;
                Profile.FansCount += newFollow ? 1 : -1;
                if (Profile.FansCount < 0) Profile.FansCount = 0;
                NotificationHelper.ShowNotification(newFollow ? "关注成功" : "已取消关注", Profile.UserName);
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }
    }
}
