using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class PostDetailViewModel : ViewModelBase
    {
        private PostItem _post;
        public PostItem Post
        {
            get { return _post; }
            set
            {
                if (_post != null)
                {
                    _post.PropertyChanged -= Post_PropertyChanged;
                }
                _post = value;
                if (_post != null)
                {
                    _post.PropertyChanged += Post_PropertyChanged;
                }
                OnPropertyChanged();
                OnPropertyChanged("LikeButtonLabel");
                OnPropertyChanged("CollectButtonLabel");
                OnPropertyChanged("CollectButtonIcon");
                OnPropertyChanged("HasVideo");
            }
        }

        private void Post_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LikeCount" || e.PropertyName == "IsLiked")
            {
                OnPropertyChanged("LikeButtonLabel");
            }
            if (e.PropertyName == "CollectCount" || e.PropertyName == "IsCollected")
            {
                OnPropertyChanged("CollectButtonLabel");
                OnPropertyChanged("CollectButtonIcon");
            }
        }

        public string LikeButtonLabel
        {
            get
            {
                if (Post == null) return "点赞";
                if (Post.LikeCount > 0)
                {
                    return Post.IsLiked ? string.Format("已赞 {0}", Post.LikeCount) : string.Format("赞 {0}", Post.LikeCount);
                }
                return Post.IsLiked ? "已赞" : "点赞";
            }
        }

        public string CollectButtonLabel
        {
            get
            {
                if (Post == null) return "收藏";
                if (Post.CollectCount > 0)
                {
                    return Post.IsCollected ? string.Format("已收藏 {0}", Post.CollectCount) : string.Format("收藏 {0}", Post.CollectCount);
                }
                return Post.IsCollected ? "已收藏" : "收藏";
            }
        }

        public string CollectButtonIcon
        {
            get
            {
                return (Post != null && Post.IsCollected) ? "Favorite" : "UnFavorite";
            }
        }

        private ObservableCollection<PostCommentItem> _comments;
        public ObservableCollection<PostCommentItem> Comments
        {
            get { return _comments; }
            set { _comments = value; OnPropertyChanged(); }
        }

        private string _inputCommentText;
        public string InputCommentText
        {
            get { return _inputCommentText; }
            set { _inputCommentText = value; OnPropertyChanged(); }
        }

        private int _currentPage = 1;

        // Video Player Properties
        private string _videoPlayUrl;
        public string VideoPlayUrl
        {
            get { return _videoPlayUrl; }
            set
            {
                _videoPlayUrl = value;
                OnPropertyChanged();
                OnPropertyChanged("HasVideo");
            }
        }

        private string _videoCoverUrl;
        public string VideoCoverUrl
        {
            get { return _videoCoverUrl; }
            set { _videoCoverUrl = value; OnPropertyChanged(); }
        }

        private ObservableCollection<VideoPlayInfoItem> _availableQualities;
        public ObservableCollection<VideoPlayInfoItem> AvailableQualities
        {
            get { return _availableQualities; }
            set { _availableQualities = value; OnPropertyChanged(); }
        }

        private VideoPlayInfoItem _selectedQuality;
        public VideoPlayInfoItem SelectedQuality
        {
            get { return _selectedQuality; }
            set
            {
                _selectedQuality = value;
                OnPropertyChanged();
                OnPropertyChanged("SelectedQualityLabel");
            }
        }

        public string SelectedQualityLabel
        {
            get
            {
                if (_selectedQuality != null && !string.IsNullOrEmpty(_selectedQuality.QualityName))
                {
                    return _selectedQuality.QualityName;
                }
                return "画质";
            }
        }

        private string _playerHtml;
        public string PlayerHtml
        {
            get { return _playerHtml; }
            set
            {
                _playerHtml = value;
                OnPropertyChanged();
                OnPropertyChanged("HasVideo");
            }
        }

        private string _playAuth;
        public string PlayAuth
        {
            get { return _playAuth; }
            set { _playAuth = value; OnPropertyChanged(); }
        }

        public bool HasVideo
        {
            get
            {
                return !string.IsNullOrEmpty(_videoPlayUrl) || !string.IsNullOrEmpty(_playerHtml) || (Post != null && Post.HasVideo);
            }
        }

        private bool _isVideoResolving = false;
        public bool IsVideoResolving
        {
            get { return _isVideoResolving; }
            set { _isVideoResolving = value; OnPropertyChanged(); }
        }

        private bool _hasStartedPlayback = false;
        public bool HasStartedPlayback
        {
            get { return _hasStartedPlayback; }
            set { _hasStartedPlayback = value; OnPropertyChanged(); }
        }

        private bool _isVideoPlaying = false;
        public bool IsVideoPlaying
        {
            get { return _isVideoPlaying; }
            set
            {
                _isVideoPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged("PlayPauseIcon");
            }
        }

        public string PlayPauseIcon
        {
            get { return _isVideoPlaying ? "⏸" : "▶"; }
        }

        private double _currentPositionSeconds = 0;
        public double CurrentPositionSeconds
        {
            get { return _currentPositionSeconds; }
            set
            {
                _currentPositionSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged("CurrentTimeText");
            }
        }

        private double _totalDurationSeconds = 0;
        public double TotalDurationSeconds
        {
            get { return _totalDurationSeconds; }
            set
            {
                _totalDurationSeconds = value;
                OnPropertyChanged();
                OnPropertyChanged("TotalTimeText");
            }
        }

        public string CurrentTimeText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Math.Max(0, _currentPositionSeconds));
                return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
            }
        }

        public string TotalTimeText
        {
            get
            {
                var ts = TimeSpan.FromSeconds(Math.Max(0, _totalDurationSeconds));
                return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
            }
        }

        private bool _isControlsVisible = true;
        public bool IsControlsVisible
        {
            get { return _isControlsVisible; }
            set { _isControlsVisible = value; OnPropertyChanged(); }
        }

        private bool _isFullScreen = false;
        public bool IsFullScreen
        {
            get { return _isFullScreen; }
            set 
            { 
                _isFullScreen = value; 
                OnPropertyChanged(); 
                OnPropertyChanged("FullScreenIcon");
                OnPropertyChanged("IsBottomBarVisible");
            }
        }

        public string FullScreenIcon
        {
            get { return _isFullScreen ? "🗗" : "🗖"; }
        }

        // Image Viewer Properties
        private bool _isImageViewerOpen = false;
        public bool IsImageViewerOpen
        {
            get { return _isImageViewerOpen; }
            set 
            { 
                _isImageViewerOpen = value; 
                OnPropertyChanged(); 
                OnPropertyChanged("IsBottomBarVisible");
            }
        }

        public bool IsBottomBarVisible
        {
            get { return !_isFullScreen && !_isImageViewerOpen; }
        }

        private bool _isFollowed = false;
        public bool IsFollowed
        {
            get { return _isFollowed; }
            set 
            { 
                _isFollowed = value; 
                OnPropertyChanged(); 
                OnPropertyChanged("FollowButtonLabel");
                OnPropertyChanged("FollowButtonIcon");
            }
        }

        public string FollowButtonLabel
        {
            get { return _isFollowed ? "已关注" : "关注"; }
        }

        public string FollowButtonIcon
        {
            get { return _isFollowed ? "Contact2" : "AddFriend"; }
        }

        private string _selectedImageUrl;
        public string SelectedImageUrl
        {
            get { return _selectedImageUrl; }
            set { _selectedImageUrl = value; OnPropertyChanged(); }
        }

        private bool _isLoadingComments = false;
        public bool IsLoadingComments
        {
            get { return _isLoadingComments; }
            set { _isLoadingComments = value; OnPropertyChanged(); }
        }

        private bool _hasMoreComments = true;
        public bool HasMoreComments
        {
            get { return _hasMoreComments; }
            set { _hasMoreComments = value; OnPropertyChanged(); }
        }

        public ICommand LikeCommand { get; private set; }
        public ICommand CollectCommand { get; private set; }
        public ICommand ToggleFollowCommand { get; private set; }
        public ICommand SendCommentCommand { get; private set; }
        public ICommand RefreshCommentsCommand { get; private set; }
        public ICommand LoadMoreCommentsCommand { get; private set; }
        public ICommand ToggleCommentLikeCommand { get; private set; }
        public ICommand ToggleReplyLikeCommand { get; private set; }
        public ICommand OpenImageCommand { get; private set; }
        public ICommand CloseImageCommand { get; private set; }
        public ICommand TogglePlayCommand { get; private set; }
        public ICommand ToggleControlsCommand { get; private set; }
        public ICommand ToggleFullScreenCommand { get; private set; }
        public ICommand SelectQualityCommand { get; private set; }

        public event EventHandler RequestPlayToggle;
        public event EventHandler RequestFullScreenToggle;
        public event Action<VideoPlayInfoItem> RequestQualityChange;

        public PostDetailViewModel(PostItem initialPost)
        {
            Post = initialPost ?? new PostItem();
            Comments = new ObservableCollection<PostCommentItem>();
            AvailableQualities = new ObservableCollection<VideoPlayInfoItem>();

            LikeCommand = new RelayCommand(async () => await ToggleLikeAsync());
            CollectCommand = new RelayCommand(async () => await ToggleCollectAsync());
            ToggleFollowCommand = new RelayCommand(async () => await ToggleFollowAsync());
            SendCommentCommand = new RelayCommand(async () => await SubmitCommentAsync());
            RefreshCommentsCommand = new RelayCommand(async () => await LoadCommentsAsync(true));
            LoadMoreCommentsCommand = new RelayCommand(async () => await LoadMoreCommentsAsync());
            ToggleCommentLikeCommand = new RelayCommand<PostCommentItem>(async (c) => await ToggleCommentLikeAsync(c));
            ToggleReplyLikeCommand = new RelayCommand<PostReplyItem>(async (r) => await ToggleReplyLikeAsync(r));
            _isFollowed = Post != null && (Post.IsFollow || (Post.Author != null && Post.Author.IsFollow));

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

            TogglePlayCommand = new RelayCommand(() =>
            {
                if (RequestPlayToggle != null)
                {
                    RequestPlayToggle(this, EventArgs.Empty);
                }
            });

            ToggleControlsCommand = new RelayCommand(() =>
            {
                IsControlsVisible = !IsControlsVisible;
            });

            ToggleFullScreenCommand = new RelayCommand(() =>
            {
                if (RequestFullScreenToggle != null)
                {
                    RequestFullScreenToggle(this, EventArgs.Empty);
                }
            });

            SelectQualityCommand = new RelayCommand<VideoPlayInfoItem>((q) =>
            {
                ChangeQuality(q);
            });
        }

        public void ChangeQuality(VideoPlayInfoItem quality)
        {
            if (quality == null || quality == SelectedQuality) return;
            SelectedQuality = quality;
            VideoPlayUrl = quality.PlayUrl;
            if (RequestQualityChange != null)
            {
                RequestQualityChange(quality);
            }
        }

        public async Task LoadPostDetailAndCommentsAsync()
        {
            if (Post == null || string.IsNullOrEmpty(Post.PostId))
            {
                KuroLogger.Warn("POST_DETAIL_NULL", "Cannot load post detail: Post or PostId is empty");
                return;
            }

            IsBusy = true;
            StatusMessage = "加载帖子正文与评论...";

            try
            {
                string origPostId = Post.PostId;
                int origGameId = Post.GameId;
                string origAvatar = Post.Author != null ? Post.Author.AvatarUrl : "";
                string origUserName = Post.Author != null ? Post.Author.UserName : "";
                string origVideoId = Post.VideoId;

                var detail = await KuroForumService.Instance.GetPostDetailAsync(origPostId, origGameId);
                if (detail != null)
                {
                    if (string.IsNullOrEmpty(detail.PostId)) detail.PostId = origPostId;
                    if (detail.GameId <= 0) detail.GameId = origGameId;
                    if (string.IsNullOrEmpty(detail.Author.AvatarUrl) && !string.IsNullOrEmpty(origAvatar))
                    {
                        detail.Author.AvatarUrl = origAvatar;
                    }
                    if (string.IsNullOrEmpty(detail.Author.UserName) && !string.IsNullOrEmpty(origUserName))
                    {
                        detail.Author.UserName = origUserName;
                    }
                    if (string.IsNullOrEmpty(detail.VideoId) && !string.IsNullOrEmpty(origVideoId))
                    {
                        detail.VideoId = origVideoId;
                    }
                    Post = detail;
                    IsFollowed = detail.IsFollow || (detail.Author != null && detail.Author.IsFollow);
                }

                // Video Parsing using Aliyun VOD resolution algorithm
                if (Post != null && !string.IsNullOrEmpty(Post.VideoId))
                {
                    IsVideoResolving = true;
                    VideoCoverUrl = !string.IsNullOrEmpty(Post.VideoCoverUrl) ? Post.VideoCoverUrl : Post.CoverUrl;
                    var videoRes = await KuroVideoService.Instance.ResolveVideoAsync(Post.VideoId);
                    if (videoRes != null)
                    {
                        PlayAuth = videoRes.PlayAuth;
                        PlayerHtml = videoRes.PlayerHtml;

                        AvailableQualities.Clear();
                        foreach (var q in videoRes.PlayList)
                        {
                            AvailableQualities.Add(q);
                        }

                        SelectedQuality = videoRes.BestQuality ?? videoRes.PlayList.FirstOrDefault();
                        VideoPlayUrl = SelectedQuality != null ? SelectedQuality.PlayUrl : videoRes.BestPlayUrl;

                        if (!string.IsNullOrEmpty(videoRes.CoverUrl))
                        {
                            VideoCoverUrl = videoRes.CoverUrl;
                        }
                        if (videoRes.Duration > 0)
                        {
                            TotalDurationSeconds = videoRes.Duration;
                        }
                        KuroLogger.Loading("VIDEO_LOAD_SUCCESS", "Video stream bound: " + VideoPlayUrl);
                    }
                    IsVideoResolving = false;
                }

                await LoadCommentsAsync(true);
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("POST_DETAIL_ERR", "Error loading post detail/comments: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                IsVideoResolving = false;
            }
        }

        public async Task LoadCommentsAsync(bool resetPage = false)
        {
            if (Post == null || string.IsNullOrEmpty(Post.PostId)) return;
            if (IsLoadingComments) return;

            if (resetPage)
            {
                _currentPage = 1;
                _hasMoreComments = true;
            }

            IsLoadingComments = true;
            try
            {
                var list = await KuroForumService.Instance.GetCommentsAsync(Post.PostId, Post.GameId, _currentPage, 20);
                if (resetPage)
                {
                    Comments.Clear();
                }

                foreach (var item in list)
                {
                    Comments.Add(item);
                }
                _hasMoreComments = list.Count >= 20;
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("COMMENTS_ERR", "Error loading comments: " + ex.Message);
            }
            finally
            {
                IsLoadingComments = false;
            }
        }

        public async Task LoadMoreCommentsAsync()
        {
            if (IsLoadingComments || !_hasMoreComments || Post == null || string.IsNullOrEmpty(Post.PostId)) return;
            _currentPage++;
            await LoadCommentsAsync(false);
        }

        public async Task ToggleCommentLikeAsync(PostCommentItem comment)
        {
            if (comment == null) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先登录账号后再点赞", "提示");
                return;
            }

            bool wasLiked = comment.IsLiked;
            string postId = !string.IsNullOrEmpty(comment.PostId) ? comment.PostId : (Post != null ? Post.PostId : "");
            int gameId = comment.GameId > 0 ? comment.GameId : (Post != null ? Post.GameId : 2);
            int forumId = comment.ForumId > 0 ? comment.ForumId : 4;

            bool success = await KuroForumService.Instance.LikeCommentAsync(
                postId,
                comment.CommentId,
                "0",
                comment.UserId,
                gameId,
                forumId,
                wasLiked);

            if (success)
            {
                comment.IsLiked = !wasLiked;
                comment.LikeCount += comment.IsLiked ? 1 : -1;
                if (comment.LikeCount < 0) comment.LikeCount = 0;
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }

        public async Task ToggleReplyLikeAsync(PostReplyItem reply)
        {
            if (reply == null) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先登录账号后再点赞", "提示");
                return;
            }

            bool wasLiked = reply.IsLiked;
            string postId = !string.IsNullOrEmpty(reply.PostId) ? reply.PostId : (Post != null ? Post.PostId : "");
            int gameId = reply.GameId > 0 ? reply.GameId : (Post != null ? Post.GameId : 2);
            int forumId = reply.ForumId > 0 ? reply.ForumId : 4;

            bool success = await KuroForumService.Instance.LikeCommentAsync(
                postId,
                reply.PostCommentId,
                reply.ReplyId,
                reply.UserId,
                gameId,
                forumId,
                wasLiked);

            if (success)
            {
                reply.IsLiked = !wasLiked;
                reply.LikeCount += reply.IsLiked ? 1 : -1;
                if (reply.LikeCount < 0) reply.LikeCount = 0;
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }

        public async Task ToggleLikeAsync()
        {
            if (Post == null || string.IsNullOrEmpty(Post.PostId)) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先在主界面登录账号后再点赞", "提示");
                return;
            }

            bool wasLiked = Post.IsLiked;
            string toUserId = (Post.Author != null && !string.IsNullOrEmpty(Post.Author.UserId)) ? Post.Author.UserId : "";
            int forumId = Post.ForumId > 0 ? Post.ForumId : 3;
            int gameId = Post.GameId > 0 ? Post.GameId : 2;
            int postType = Post.PostType > 0 ? Post.PostType : 1;

            bool ok = await KuroForumService.Instance.LikePostAsync(Post.PostId, toUserId, forumId, gameId, postType, wasLiked);
            if (ok)
            {
                Post.IsLiked = !wasLiked;
                Post.LikeCount += Post.IsLiked ? 1 : -1;
                if (Post.LikeCount < 0) Post.LikeCount = 0;
                NotificationHelper.ShowNotification(Post.IsLiked ? "点赞成功" : "已取消点赞", "提示");
                StatusMessage = Post.IsLiked ? "已点赞" : "已取消点赞";
            }
            else
            {
                NotificationHelper.ShowNotification("点赞失败，请重试", "提示");
            }
        }

        public async Task ToggleCollectAsync()
        {
            if (Post == null || string.IsNullOrEmpty(Post.PostId)) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先在主界面登录账号", "提示");
                return;
            }

            bool wasCollected = Post.IsCollected;
            string toUserId = (Post.Author != null && !string.IsNullOrEmpty(Post.Author.UserId)) ? Post.Author.UserId : "";

            bool ok = await KuroForumService.Instance.CollectPostAsync(Post.PostId, toUserId, wasCollected);
            if (ok)
            {
                Post.IsCollected = !wasCollected;
                Post.CollectCount += Post.IsCollected ? 1 : -1;
                if (Post.CollectCount < 0) Post.CollectCount = 0;
                NotificationHelper.ShowNotification(Post.IsCollected ? "收藏成功" : "已取消收藏", "提示");
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }

        public async Task ToggleFollowAsync()
        {
            if (Post == null || Post.Author == null || string.IsNullOrEmpty(Post.Author.UserId)) return;
            if (!SettingsHelper.IsLoggedIn)
            {
                NotificationHelper.ShowNotification("请先在主界面登录账号", "提示");
                return;
            }

            bool newFollow = !_isFollowed;
            bool success = await KuroUserService.Instance.FollowUserAsync(Post.Author.UserId, newFollow);
            if (success)
            {
                IsFollowed = newFollow;
                if (Post.Author != null) Post.Author.IsFollow = newFollow;
                Post.IsFollow = newFollow;
                NotificationHelper.ShowNotification(newFollow ? "关注成功" : "已取消关注", Post.Author.UserName);
            }
            else
            {
                NotificationHelper.ShowNotification("操作失败，请重试", "提示");
            }
        }

        public async Task SubmitCommentAsync()
        {
            if (string.IsNullOrWhiteSpace(InputCommentText))
            {
                StatusMessage = "请输入评论内容";
                return;
            }

            if (!SettingsHelper.IsLoggedIn)
            {
                StatusMessage = "请先在【我的】页面登录后再发表评论";
                return;
            }

            IsBusy = true;
            StatusMessage = "正在发布评论...";
            bool ok = await KuroForumService.Instance.CreateCommentAsync(Post.PostId, Post.GameId, InputCommentText.Trim());
            if (ok)
            {
                InputCommentText = "";
                StatusMessage = "评论发表成功！";
                await LoadCommentsAsync(true);
            }
            else
            {
                StatusMessage = "发表失败，请重试";
            }
            IsBusy = false;
        }
    }
}
