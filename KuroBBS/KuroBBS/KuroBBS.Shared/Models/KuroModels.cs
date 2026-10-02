using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace KuroBBS.Models
{
    public class KuroApiResponse<T>
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public bool Success { get; set; }
        public T Data { get; set; }
    }

    public class PostAuthor
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string HeadBorderUrl { get; set; }
        public string IpRegion { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsFollow { get; set; }
    }

    public class PostImage
    {
        /// <summary>缩略图固定高度（像素）。评论区一律按这个高度出图，避免大图把列表撑爆。</summary>
        public const double ThumbHeight = 90;

        /// <summary>缩略图最长边（像素）。列表里只解到这么大，肉眼足够清晰又极省内存。</summary>
        public const int ThumbDecodeWidth = 200;

        public string Url { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>是否为竖图（高 &gt; 宽）。竖图在列表里限高会更矮，需要单独限制。</summary>
        public bool IsPortrait { get { return Height > Width && Width > 0; } }

        /// <summary>
        /// 列表内的显示宽度（像素）。以 680 为内容区宽度上限，等比缩放；
        /// 尺寸缺失（0）时回退 360，保证不会算成 0 宽。
        /// </summary>
        public double DisplayWidth
        {
            get
            {
                if (Width <= 0 || Height <= 0) return 360;
                const double maxW = 680;
                return Width <= maxW ? Width : maxW;
            }
        }

        /// <summary>
        /// 列表内的显示高度（像素），按真实宽高比换算，并限高。
        /// 竖长图（如 1080×2046、1320×2856）按 420 限高，横图按 300 限高。
        /// 这样 <c>Image</c> 在加载前就有正确占位尺寸，列表不会跳动。
        /// </summary>
        public double DisplayHeight
        {
            get
            {
                if (Width <= 0 || Height <= 0) return 360;
                double maxH = IsPortrait ? 420 : 300;
                double h = DisplayWidth * ((double)Height / Width);
                return h <= maxH ? h : maxH;
            }
        }

        /// <summary>
        /// 缩略图显示宽度（像素）。固定高度 <see cref="ThumbHeight"/>，按真实宽高比反推宽度。
        /// 尺寸缺失时按 4:3 估一个，保证不会算成 0。
        /// </summary>
        public double ThumbWidth
        {
            get
            {
                const double h = ThumbHeight;
                if (Width <= 0 || Height <= 0) return h * 4 / 3;
                double w = h * ((double)Width / Height);
                // 极端长条图（全景/截长图）宽度会离谱，夹一下
                if (w < 40) return 40;
                if (w > 400) return 400;
                return w;
            }
        }
    }

    public enum ContentBlockType
    {
        Text = 1,
        Image = 2,
        Video = 3,
        Banner = 4,
        Heading = 5
    }

    public class PostTextRun
    {
        public bool IsEmoji { get; set; }
        public string Text { get; set; }
        public string EmojiUrl { get; set; }
        public string EmojiName { get; set; }
        public string TargetId { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
        public bool IsStrikethrough { get; set; }
        public string ColorHex { get; set; }
        public double? FontSize { get; set; }
    }

    public class PostContentBlock
    {
        public ContentBlockType BlockType { get; set; }
        public string RawContent { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public string ImageUrl { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public bool IsHeading { get; set; }
        public int HeadingLevel { get; set; }

        public bool IsText { get { return BlockType == ContentBlockType.Text || BlockType == ContentBlockType.Heading; } }
        public bool IsImage { get { return BlockType == ContentBlockType.Image; } }
        public bool IsBanner { get { return BlockType == ContentBlockType.Banner; } }

        public PostContentBlock()
        {
            Runs = new List<PostTextRun>();
        }
    }

    public class PostItem : INotifyPropertyChanged
    {
        public string PostId { get; set; }
        public int GameId { get; set; }
        public int ForumId { get; set; }
        public int PostType { get; set; }
        public string GameName { get; set; }
        public string Title { get; set; }
        public string ContentSummary { get; set; }
        public string FullContent { get; set; }
        public string CoverUrl { get; set; }
        public bool HasCover { get { return !string.IsNullOrEmpty(CoverUrl); } }
        public string VideoId { get; set; }
        public string VideoUrl { get; set; }
        public bool HasVideo { get { return !string.IsNullOrEmpty(VideoUrl) || !string.IsNullOrEmpty(VideoId); } }
        public double VideoDuration { get; set; }
        public string VideoCoverUrl { get; set; }
        public List<PostImage> ImageList { get; set; }
        public List<PostContentBlock> ContentBlocks { get; set; }
        public List<TopicItem> Topics { get; set; }
        public bool HasTopics { get { return Topics != null && Topics.Count > 0; } }
        public PostAuthor Author { get; set; }
        public string PostTimeStr { get; set; }

        private int _likeCount;
        public int LikeCount
        {
            get { return _likeCount; }
            set { _likeCount = value; OnPropertyChanged(); }
        }

        private int _commentCount;
        public int CommentCount
        {
            get { return _commentCount; }
            set { _commentCount = value; OnPropertyChanged(); }
        }

        private bool _isLiked;
        public bool IsLiked
        {
            get { return _isLiked; }
            set { _isLiked = value; OnPropertyChanged(); }
        }

        private bool _isFollow;
        public bool IsFollow
        {
            get { return _isFollow; }
            set { _isFollow = value; OnPropertyChanged(); }
        }

        private bool _isCollected;
        public bool IsCollected
        {
            get { return _isCollected; }
            set { _isCollected = value; OnPropertyChanged(); }
        }

        private int _collectCount;
        public int CollectCount
        {
            get { return _collectCount; }
            set { _collectCount = value; OnPropertyChanged(); }
        }

        public PostItem()
        {
            ImageList = new List<PostImage>();
            ContentBlocks = new List<PostContentBlock>();
            Topics = new List<TopicItem>();
            Author = new PostAuthor();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class PostReplyItem : INotifyPropertyChanged
    {
        public string ReplyId { get; set; }
        public string PostCommentId { get; set; }
        public string PostId { get; set; }
        public int GameId { get; set; }
        public int ForumId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string ToUserId { get; set; }
        public string ToUserName { get; set; }
        public bool HasToUser { get { return !string.IsNullOrEmpty(ToUserName); } }
        public string ReplyText { get; set; }
        public List<PostTextRun> ContentRuns { get; set; }
        public List<PostImage> ImageList { get; set; }
        public bool HasImages { get { return ImageList != null && ImageList.Count > 0; } }
        public string ReplyTimeStr { get; set; }
        public string IpRegion { get; set; }

        private int _likeCount;
        public int LikeCount
        {
            get { return _likeCount; }
            set { _likeCount = value; OnPropertyChanged(); OnPropertyChanged("LikeCountText"); }
        }

        private bool _isLiked;
        public bool IsLiked
        {
            get { return _isLiked; }
            set { _isLiked = value; OnPropertyChanged(); OnPropertyChanged("LikeIcon"); OnPropertyChanged("LikeColor"); }
        }

        public string LikeCountText { get { return LikeCount > 0 ? LikeCount.ToString() : "赞"; } }
        public string LikeIcon { get { return IsLiked ? "♥" : "♡"; } }
        public string LikeColor { get { return IsLiked ? "#FF453A" : "#8E8E93"; } }

        public PostReplyItem()
        {
            ContentRuns = new List<PostTextRun>();
            ImageList = new List<PostImage>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PostCommentItem : INotifyPropertyChanged
    {
        public string CommentId { get; set; }
        public string PostId { get; set; }
        public int GameId { get; set; }
        public int ForumId { get; set; }
        public int Floor { get; set; }
        public string FloorText { get { return Floor > 0 ? string.Format("#{0}", Floor) : ""; } }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string Content { get; set; }
        public List<PostTextRun> ContentRuns { get; set; }
        public List<PostImage> ImageList { get; set; }
        public bool HasImages { get { return ImageList != null && ImageList.Count > 0; } }
        public List<PostReplyItem> Replies { get; set; }
        public bool HasReplies { get { return Replies != null && Replies.Count > 0; } }
        public string CreateTimeStr { get; set; }
        public string IpRegion { get; set; }

        private int _likeCount;
        public int LikeCount
        {
            get { return _likeCount; }
            set { _likeCount = value; OnPropertyChanged(); OnPropertyChanged("LikeCountText"); }
        }

        private bool _isLiked;
        public bool IsLiked
        {
            get { return _isLiked; }
            set { _isLiked = value; OnPropertyChanged(); OnPropertyChanged("LikeIcon"); OnPropertyChanged("LikeColor"); }
        }

        public string LikeCountText { get { return LikeCount > 0 ? LikeCount.ToString() : "赞"; } }
        public string LikeIcon { get { return IsLiked ? "♥" : "♡"; } }
        public string LikeColor { get { return IsLiked ? "#FF453A" : "#8E8E93"; } }

        public int ReplyCount { get; set; }
        public string ReplyCountText { get { return ReplyCount > 0 ? string.Format("共 {0} 条回复", ReplyCount) : ""; } }

        /// <summary>
        /// 身份标签（来自 getPostCommentListV2 的 newIdentifyNames[]，回退 identifyNames 字符串）。
        /// 例如「鸣潮WIKI成员」「摄影师」「攻略作者」「考据帝」。用于评论者名字后的身份徽标。
        /// </summary>
        public List<string> IdentifyNames { get; set; }
        public bool HasIdentifyNames { get { return IdentifyNames != null && IdentifyNames.Count > 0; } }

        /// <summary>身份标签的分类（identifyClassify，接口原样保留，暂只作数据字段）。</summary>
        public int IdentifyClassify { get; set; }

        /// <summary>是否为官方账号（isOfficial=1）。</summary>
        public bool IsOfficial { get; set; }

        /// <summary>是否为楼主/发布者（isPublisher=1）。</summary>
        public bool IsPublisher { get; set; }

        /// <summary>身份标签合并文本，供单行显示用（顿号分隔）。</summary>
        public string IdentifyText
        {
            get
            {
                if (!HasIdentifyNames) return "";
                return string.Join("、", IdentifyNames);
            }
        }

        /// <summary>
        /// 楼中楼是否还有更多回复未展开。getPostCommentListV2 只内联返回少量 replyVos，
        /// 完整列表要走 /forum/comment/getReplyList 分页拉取。
        /// 当 ReplyCount &gt; 已加载条数时置 true，UI 显示「展开更多回复」。
        /// </summary>
        private bool _hasMoreReplies;
        public bool HasMoreReplies
        {
            get { return _hasMoreReplies; }
            set { _hasMoreReplies = value; OnPropertyChanged(); OnPropertyChanged("ReplyExpandText"); }
        }

        /// <summary>「展开更多回复」按钮文案，附带剩余条数。</summary>
        public string ReplyExpandText
        {
            get
            {
                int remain = ReplyCount - (Replies != null ? Replies.Count : 0);
                return remain > 0 ? string.Format("展开更多回复（{0}）", remain) : "加载更多回复";
            }
        }

        private bool _isLoadingReplies;
        public bool IsLoadingReplies
        {
            get { return _isLoadingReplies; }
            set { _isLoadingReplies = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// getReplyList 的分页游标，从 1 开始。
        /// 第 1 页可能包含 commentList 已内联返回的 replyVos，服务端按 replyId 去重，重复项不会重复加入。
        /// </summary>
        public int ReplyPageIndex { get; set; }

        public PostCommentItem()
        {
            ContentRuns = new List<PostTextRun>();
            ImageList = new List<PostImage>();
            Replies = new List<PostReplyItem>();
            IdentifyNames = new List<string>();
            ReplyPageIndex = 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>供 ViewModel 主动触发通知（如补齐回复后刷新按钮文案）。</summary>
        public void OnPropertyChangedPublic(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }
    }

    public class UserCommentNoticeItem : INotifyPropertyChanged
    {
        public string PostId { get; set; }
        public string PostCommentId { get; set; }
        public string PostCommentReplyId { get; set; }
        public string NoticeTitle { get; set; }
        public List<PostTextRun> TitleRuns { get; set; }
        public string NoticeContent { get; set; }
        public List<PostTextRun> ContentRuns { get; set; }
        public string SendUserId { get; set; }
        public string SendUserName { get; set; }
        public string SendUserHeadUrl { get; set; }
        public string TargetUserName { get; set; }
        public string ShowTime { get; set; }
        public string GameName { get; set; }
        public int GameId { get; set; }
        public int ForumId { get; set; }
        public int DetailType { get; set; }
        public string ImageUrl { get; set; }
        public bool HasImage { get { return !string.IsNullOrEmpty(ImageUrl); } }

        private int _likeCount;
        public int LikeCount
        {
            get { return _likeCount; }
            set { _likeCount = value; OnPropertyChanged(); OnPropertyChanged("LikeCountText"); }
        }

        private bool _isLiked;
        public bool IsLiked
        {
            get { return _isLiked; }
            set { _isLiked = value; OnPropertyChanged(); OnPropertyChanged("LikeIcon"); OnPropertyChanged("LikeColor"); }
        }

        public string LikeCountText { get { return LikeCount > 0 ? LikeCount.ToString() : "赞"; } }
        public string LikeIcon { get { return IsLiked ? "♥" : "♡"; } }
        public string LikeColor { get { return IsLiked ? "#FF453A" : "#8E8E93"; } }

        public UserCommentNoticeItem()
        {
            TitleRuns = new List<PostTextRun>();
            ContentRuns = new List<PostTextRun>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SignInDayInfo
    {
        public string Date { get; set; }
        public int DayNumber { get; set; }
        public bool IsSignedIn { get; set; }
        public bool IsCurrentDay { get; set; }
        public string RewardName { get; set; }
        public string RewardIcon { get; set; }
        public int RewardCount { get; set; }
        public string RewardCountDisplay
        {
            get { return RewardCount > 0 ? "x" + RewardCount : ""; }
        }
        public string StatusText
        {
            get { return IsSignedIn ? "已领取" : (IsCurrentDay ? "今日可领" : ""); }
        }
    }

    public class SignInClaimRecord
    {
        public string GoodsId { get; set; }
        public string GoodsName { get; set; }
        public int GoodsNum { get; set; }
        public string GoodsUrl { get; set; }
        public string OrderCode { get; set; }
        public bool SendState { get; set; }
        public string SignInDate { get; set; }
        public string GoodsNumText { get { return "x" + GoodsNum; } }
    }

    public class CommendFollowItem : INotifyPropertyChanged
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserHeadUrl { get; set; }
        public string UserSign { get; set; }
        private bool _isFollow;
        public bool IsFollow
        {
            get { return _isFollow; }
            set
            {
                if (_isFollow != value)
                {
                    _isFollow = value;
                    OnPropertyChanged();
                    OnPropertyChanged("FollowBtnText");
                }
            }
        }
        public string FollowBtnText { get { return _isFollow ? "已关注" : "+ 关注"; } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SignInStatus
    {
        public int GameId { get; set; }
        public string GameName { get; set; }
        public bool IsSignedInToday { get; set; }
        public int ConsecutiveDays { get; set; }
        public int TotalSignInDays { get; set; }
        public int MonthTotalDays { get; set; }
        public int ReplenishCardCount { get; set; }
        public string EventStartTimes { get; set; }
        public string ReqMonth { get; set; }
        public List<SignInDayInfo> MonthRecords { get; set; }

        public SignInStatus()
        {
            MonthRecords = new List<SignInDayInfo>();
            ReqMonth = DateTime.Now.Month.ToString("D2");
        }
    }

    public class SignInResult
    {
        public bool Success { get; set; }
        public bool AlreadySignedIn { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public string RewardSummary { get; set; }
        public string TomorrowRewardSummary { get; set; }
    }

    public class GameRoleCard
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public int GameId { get; set; }
        public string GameName { get; set; }
        public int Level { get; set; }
        public string LevelDisplay { get; set; }
        public bool IsMedalLevel { get; set; }
        public string AvatarUrl { get; set; }
        public string HeadPhotoUrl { get; set; }
        public string GameHeadUrl { get; set; }
        public string Signature { get; set; }
        public string AchievementCount { get; set; }
        public string EnergyDisplay { get; set; }
        public string RoleScore { get; set; }
        public bool IsDefault { get; set; }
        public int ActiveDay { get; set; }
        public string ActiveDayDisplay { get; set; }
        public int RoleNum { get; set; }
        public string RoleNumLabel { get; set; }
        public string FashionPercentDisplay { get; set; }
        public string PhantomPercentDisplay { get; set; }
        public string GameThemeColor { get; set; }

        // Game-specific 4 display stats:
        // 战双: 游戏天数, 角色总评分, 角色数量, 涂装收集率
        // 鸣潮: 游戏天数, 成就数, 角色数量, 声骸收集进度
        public string Stat1Label { get; set; }
        public string Stat1Value { get; set; }

        public string Stat2Label { get; set; }
        public string Stat2Value { get; set; }

        public string Stat3Label { get; set; }
        public string Stat3Value { get; set; }

        public string Stat4Label { get; set; }
        public string Stat4Value { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1} ({2})", GameName, RoleName, ServerName);
        }
    }

    public class UserProfile : INotifyPropertyChanged
    {
        private string _userId;
        public string UserId
        {
            get { return _userId; }
            set { _userId = value; OnPropertyChanged(); }
        }

        private string _userName;
        public string UserName
        {
            get { return _userName; }
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _avatarUrl;
        public string AvatarUrl
        {
            get { return _avatarUrl; }
            set { _avatarUrl = value; OnPropertyChanged(); }
        }

        private string _headFrameUrl;
        public string HeadFrameUrl
        {
            get { return _headFrameUrl; }
            set { _headFrameUrl = value; OnPropertyChanged(); }
        }

        private string _signature;
        public string Signature
        {
            get { return _signature; }
            set { _signature = value; OnPropertyChanged(); }
        }

        private string _ipRegion;
        public string IpRegion
        {
            get { return _ipRegion; }
            set { _ipRegion = value; OnPropertyChanged(); }
        }

        private int _followingCount;
        public int FollowingCount
        {
            get { return _followingCount; }
            set { _followingCount = value; OnPropertyChanged(); }
        }

        private int _fansCount;
        public int FansCount
        {
            get { return _fansCount; }
            set { _fansCount = value; OnPropertyChanged(); }
        }

        private int _postCount;
        public int PostCount
        {
            get { return _postCount; }
            set { _postCount = value; OnPropertyChanged(); }
        }

        private int _likeCount;
        public int LikeCount
        {
            get { return _likeCount; }
            set { _likeCount = value; OnPropertyChanged(); }
        }

        private int _collectCount;
        public int CollectCount
        {
            get { return _collectCount; }
            set { _collectCount = value; OnPropertyChanged(); }
        }

        private int _goldCount;
        public int GoldCount
        {
            get { return _goldCount; }
            set { _goldCount = value; OnPropertyChanged(); }
        }

        private bool _isFollow;
        public bool IsFollow
        {
            get { return _isFollow; }
            set 
            { 
                _isFollow = value; 
                OnPropertyChanged(); 
                OnPropertyChanged("FollowButtonLabel");
                OnPropertyChanged("FollowButtonIcon");
            }
        }

        public string FollowButtonLabel
        {
            get { return _isFollow ? "已关注" : "+ 关注"; }
        }

        public string FollowButtonIcon
        {
            get { return _isFollow ? "Contact2" : "AddFriend"; }
        }

        private bool _isLoginUser;
        public bool IsLoginUser
        {
            get { return _isLoginUser; }
            set { _isLoginUser = value; OnPropertyChanged(); }
        }

        private int _gender;
        public int Gender
        {
            get { return _gender; }
            set { _gender = value; OnPropertyChanged(); }
        }

        private string _registerTime;
        public string RegisterTime
        {
            get { return _registerTime; }
            set { _registerTime = value; OnPropertyChanged(); }
        }

        public bool IsLoggedIn { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class HotSearchItem
    {
        public int GameId { get; set; }
        public string KeyWord { get; set; }
        public string WordId { get; set; }
        public int OrderSeq { get; set; }
        public string LinkTarget { get; set; }
        public int LinkType { get; set; }
    }

    public class SearchConfigResult
    {
        public string DefaultWord { get; set; }
        public List<string> DefaultWordList { get; set; }
        public List<HotSearchItem> SearchList { get; set; }

        public SearchConfigResult()
        {
            DefaultWordList = new List<string>();
            SearchList = new List<HotSearchItem>();
        }
    }

    public class TopicItem
    {
        public string TopicId { get; set; }
        public string TopicName { get; set; }
        public string TopicIcon { get; set; }
        public string Remark { get; set; }
        public string BrowseCnt { get; set; }
        public string DiscussCnt { get; set; }
        public string DiscussNum { get { return DiscussCnt; } set { DiscussCnt = value; } }
        public string BrowseNum { get { return BrowseCnt; } set { BrowseCnt = value; } }
        public int GameId { get; set; }
        public int TopRank { get; set; }

        public string TopRankText
        {
            get
            {
                if (TopRank <= 0) return "";
                if (TopRank < 10) return "TOP 0" + TopRank;
                return "TOP " + TopRank;
            }
        }

        public string RankNumberText
        {
            get { return TopRank > 0 ? TopRank.ToString() : ""; }
        }

        public bool IsTopThree
        {
            get { return TopRank >= 1 && TopRank <= 3; }
        }

        public string DiscussCountText
        {
            get { return string.IsNullOrEmpty(DiscussCnt) ? "0 讨论" : DiscussCnt + " 讨论"; }
        }

        public string BrowseCountText
        {
            get { return string.IsNullOrEmpty(BrowseCnt) ? "0 浏览" : BrowseCnt + " 浏览"; }
        }

        public string FormattedTag
        {
            get { return "#" + TopicName + "#"; }
        }

        public string SummaryText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Remark)) return Remark;
                return string.Format("{0} · {1}", DiscussCountText, BrowseCountText);
            }
        }
    }

    public class GameWikiItem
    {
        public int Id { get; set; }
        public string WikiName { get; set; }
        public int WikiType { get; set; }
        public string IconUrl { get; set; }
        public string WebIconUrl { get; set; }
        public string DisplayIcon
        {
            get { return !string.IsNullOrEmpty(IconUrl) ? IconUrl : WebIconUrl; }
        }
        public string Url { get; set; }
        public string CustomSchemeUrl { get; set; }
        public string PostId { get; set; }
        public string PostTitle { get; set; }
        public bool ShowRedPoint { get; set; }
        public bool AppForce { get; set; }
        public int IsNeedToken { get; set; }
        public int GameId { get; set; }
    }

    public class WikiSearchItem
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string CoverImgUrl { get; set; }
        public string Catalogue { get; set; }
        public string LinkUrl { get; set; }

        public string CleanTitle
        {
            get
            {
                if (string.IsNullOrEmpty(Title)) return "";
                return Title.Replace("<em>", "").Replace("</em>", "");
            }
        }
    }

    public class UserFollowItem : INotifyPropertyChanged
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public string HeadFrameUrl { get; set; }
        public string Signature { get; set; }
        public int FansCount { get; set; }
        public int PostCount { get; set; }
        public int IdentifyClassify { get; set; }
        public List<string> IdentifyNames { get; set; }
        public bool HasIdentityNames { get { return IdentifyNames != null && IdentifyNames.Count > 0; } }
        public string IdentifyText { get { return HasIdentityNames ? string.Join("、", IdentifyNames) : ""; } }
        public string StatsText { get { return string.Format("粉丝 {0} · 动态 {1}", FansCount, PostCount); } }
        public bool MutualFollow { get; set; }

        private bool _isFollow;
        public bool IsFollow
        {
            get { return _isFollow; }
            set
            {
                if (_isFollow != value)
                {
                    _isFollow = value;
                    OnPropertyChanged();
                    OnPropertyChanged("FollowButtonText");
                }
            }
        }

        private bool _isUpdatingFollow;
        public bool IsUpdatingFollow
        {
            get { return _isUpdatingFollow; }
            set
            {
                if (_isUpdatingFollow != value)
                {
                    _isUpdatingFollow = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FollowButtonText { get { return IsFollow ? "已关注" : "关注"; } }

        public UserFollowItem()
        {
            IdentifyNames = new List<string>();
            Signature = "这个人很懒，还没有签名。";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class UserSearchItem : INotifyPropertyChanged
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string HeadUrl { get; set; }
        public string HeadFrameUrl { get; set; }
        public string Signature { get; set; }
        
        private int _isFollow;
        public int IsFollow
        {
            get { return _isFollow; }
            set
            {
                if (_isFollow != value)
                {
                    _isFollow = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsFollowed");
                    OnPropertyChanged("FollowButtonLabel");
                }
            }
        }

        public bool IsFollowed { get { return _isFollow == 1; } }
        public string FollowButtonLabel { get { return IsFollowed ? "已关注" : "+ 关注"; } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class CompositeSearchResult
    {
        public List<PostItem> Posts { get; set; }
        public List<WikiSearchItem> Wikis { get; set; }
        public bool HasNext { get; set; }

        public CompositeSearchResult()
        {
            Posts = new List<PostItem>();
            Wikis = new List<WikiSearchItem>();
        }
    }

    public class TopicDetailResult
    {
        public string TopicId { get; set; }
        public string TopicName { get; set; }
        public string TopicIcon { get; set; }
        public string Remark { get; set; }
        public string BrowseCnt { get; set; }
        public string DiscussCnt { get; set; }
        public string DiscussNum { get { return DiscussCnt; } set { DiscussCnt = value; } }
        public string BrowseNum { get { return BrowseCnt; } set { BrowseCnt = value; } }
        public int GameId { get; set; }
        public bool Status { get; set; }
        public List<PostItem> Posts { get; set; }
        public bool HasNext { get; set; }

        public string DiscussCountText
        {
            get { return string.IsNullOrEmpty(DiscussCnt) ? "0" : DiscussCnt; }
        }

        public string BrowseCountText
        {
            get { return string.IsNullOrEmpty(BrowseCnt) ? "0" : BrowseCnt; }
        }

        public TopicDetailResult()
        {
            Posts = new List<PostItem>();
        }
    }

    public class ForumCategoryItem
    {
        public int ForumId { get; set; }
        public string Name { get; set; }
        public int GameId { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class PublishPostResult
    {
        public bool Success { get; set; }
        public string PostId { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }

    public class EmojiEntryItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImgUrl { get; set; }
        public string TagText { get { return "_[/" + Name + "]"; } }
    }

    public class EmojiPackageGroup
    {
        public string PackageId { get; set; }
        public string PackageName { get; set; }
        public List<EmojiEntryItem> Emojis { get; set; }

        public EmojiPackageGroup()
        {
            Emojis = new List<EmojiEntryItem>();
        }
    }

    #region Kuro Wiki Models

    public class WikiBannerItem
    {
        public string Title { get; set; }
        public string Describe { get; set; }
        public string Url { get; set; }
        public int LinkType { get; set; }
        public int CatalogueId { get; set; }
        public string EntryId { get; set; }
        public string LinkUrl { get; set; }
        public bool Active { get; set; }
    }

    public class WikiShortcutItem
    {
        public string Title { get; set; }
        public string IconUrl { get; set; }
        public string IconGlyph { get; set; }
        public int CatalogueId { get; set; }
        public string EntryId { get; set; }
        public string LinkUrl { get; set; }
        public int LinkType { get; set; }

        /// <summary>
        /// 该模块在主页下挂载的子目录节点（图鉴/攻略 等分组节点才有）。
        /// 若 CatalogueId 指向的是分组节点（getPage 返回空），
        /// 需要引导用户进入目录树逐级选择，而不是直接打开空列表。
        /// </summary>
        public List<WikiCatalogueNode> Children { get; set; }

        public bool HasChildren { get { return Children != null && Children.Count > 0; } }

        /// <summary>
        /// 模块目标目录（最终应导航到的 catalogueId）。
        ///
        /// 背景：主页核心模块（图鉴/游戏攻略/剧情/主题影音…）官方前端在点击时，
        /// 会直接落到它 content.children 里 <c>active:true</c> 的那个子目录，
        /// 而不是父级分组节点（父级 getPage 恒为空）。
        /// 例如：
        ///   图鉴(1024)   → 机体图鉴(1030, active)
        ///   游戏攻略(1456) → 版本攻略(1465, active)
        ///   剧情(1252)   → 主线剧情(1253, active)
        ///   主题影音(1029) → 节日贺图(1089, active)
        ///
        /// 若模块自身就指向一个叶子目录（content.children 为空，或没有 active 子节点），
        /// 则回退为 CatalogueId。
        /// </summary>
        public int TargetCatalogueId { get; set; }

        public WikiShortcutItem()
        {
            Children = new List<WikiCatalogueNode>();
        }
    }

    public class WikiAnnouncementItem
    {
        /// <summary>公告分组名，如「公告」「更新日志」。</summary>
        public string Name { get; set; }
        /// <summary>linkCard 的标题（顶部反馈卡片标题，可能为空）。</summary>
        public string Title { get; set; }
        /// <summary>linkCard 的副标题（顶部反馈卡片描述，可能为空）。</summary>
        public string Content { get; set; }
        /// <summary>linkCard 配图。</summary>
        public string ImgUrl { get; set; }
        public string LinkUrl { get; set; }
        public int LinkType { get; set; }
        /// <summary>公告是否启用（对应接口 announcement[].active）。</summary>
        public bool Active { get; set; }
        /// <summary>是否展示顶部 linkCard（对应接口 linkCardVisible）。</summary>
        public bool LinkCardVisible { get; set; }
        /// <summary>
        /// 公告正文（接口 announcement[].content，HTML 已转为纯文本）。
        /// 这是公告的实际内容，「更新日志」等条目主要靠它展示。
        /// </summary>
        public string Body { get; set; }
        /// <summary>
        /// 列表标题：优先用 linkCard.title，为空时回退到分组名 Name，
        /// 避免「更新日志」这类无 linkCard 标题的条目渲染成空白。
        /// </summary>
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title)) return Title;
                if (!string.IsNullOrWhiteSpace(Name)) return Name;
                return "公告";
            }
        }
        /// <summary>正文纯文本是否非空（供 XAML 控制正文段落可见性）。</summary>
        public bool HasBody { get { return !string.IsNullOrWhiteSpace(Body); } }
    }

    public class WikiContributorItem
    {
        public string Uid { get; set; }
        public string UserName { get; set; }
        public string UserHeadUrl { get; set; }
        public string UserCenterUrl { get; set; }
        public string Score { get; set; }
        public int Sort { get; set; }
        public string RankDisplay
        {
            get { return "TOP " + (Sort + 1); }
        }
    }

    public class WikiCatalogueNode
    {
        public int Id { get; set; }
        public int Key { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
        public int Level { get; set; }
        public int Sort { get; set; }

        /// <summary>主页模块的 content.children 中，官方前端默认选中的子目录（active:true）。</summary>
        public bool Active { get; set; }

        public List<WikiCatalogueNode> Children { get; set; }
        public bool HasChildren { get { return Children != null && Children.Count > 0; } }
        public bool IsLeaf { get { return !HasChildren; } }

        public WikiCatalogueNode()
        {
            Children = new List<WikiCatalogueNode>();
        }
    }

    public class WikiItemRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string EntryId { get; set; }
        public string IconUrl { get; set; }
        public string CornerMarkUrl { get; set; }
        public string Level { get; set; }
        public List<string> RelateTagIds { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public int CatalogueId { get; set; }
        public int LinkType { get; set; }
        public string LinkUrl { get; set; }

        public string LevelDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(Level)) return Level;
                return "";
            }
        }

        public string LevelColor
        {
            get
            {
                if (Level == "S" || Level == "5★" || Level == "SSR" || Level == "金色" || Level == "6★") return "#FFD700";
                if (Level == "A" || Level == "4★" || Level == "SR" || Level == "紫色" || Level == "5星") return "#A335EE";
                if (Level == "B" || Level == "3★" || Level == "R" || Level == "蓝色" || Level == "4星") return "#0070DD";
                return "#007ACC";
            }
        }

        public WikiItemRecord()
        {
            RelateTagIds = new List<string>();
        }
    }

    public class WikiTagNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public List<WikiTagNode> Children { get; set; }
        public bool IsSelected { get; set; }

        public WikiTagNode()
        {
            Children = new List<WikiTagNode>();
        }
    }

    public class WikiRoleStatItem
    {
        public string Name { get; set; }
        public string MinValue { get; set; }
        public string MaxValue { get; set; }
        public string DisplayValue
        {
            get
            {
                if (!string.IsNullOrEmpty(MinValue) && !string.IsNullOrEmpty(MaxValue) && MinValue != MaxValue)
                    return MinValue + " ~ " + MaxValue;
                return !string.IsNullOrEmpty(MaxValue) ? MaxValue : MinValue;
            }
        }
    }

    public class WikiKeyValueItem
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public WikiKeyValueItem() { }
        public WikiKeyValueItem(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public class WikiRoleGearItem
    {
        public string Title { get; set; }
        public string Name { get; set; }
        public string ImgUrl { get; set; }
        public string EntryId { get; set; }
        public int LinkType { get; set; }
        public bool HasLink { get { return !string.IsNullOrEmpty(EntryId) && EntryId != "0"; } }
    }

    public class WikiRoleCardInfo
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string RobotType { get; set; }
        public string RobotTypeImage { get; set; }
        public string RoleAvatar { get; set; }
        public string RoleFigure { get; set; }
        public string RoleQuotes { get; set; }
        public string RoleIntroduce { get; set; }
        public bool HasRoleIntroduce { get { return !string.IsNullOrWhiteSpace(RoleIntroduce); } }
        public string RoleInitQuality { get; set; }
        public bool HasQualityImage
        {
            get
            {
                return !string.IsNullOrEmpty(RoleInitQuality) && (RoleInitQuality.StartsWith("http://") || RoleInitQuality.StartsWith("https://"));
            }
        }
        public bool HasQualityText
        {
            get
            {
                return !string.IsNullOrEmpty(RoleInitQuality) && !HasQualityImage;
            }
        }
        public string CampIcon { get; set; }

        public List<WikiKeyValueItem> EnergyList { get; set; }
        public bool HasEnergy { get { return EnergyList != null && EnergyList.Count > 0; } }

        public List<WikiKeyValueItem> FeatureList { get; set; }
        public bool HasFeatures { get { return FeatureList != null && FeatureList.Count > 0; } }

        public string EffectDescription { get; set; }
        public string EffectIcon { get; set; }
        public bool HasEffect { get { return !string.IsNullOrEmpty(EffectDescription); } }

        public List<WikiRoleStatItem> Stats { get; set; }
        public bool HasStats { get { return Stats != null && Stats.Count > 0; } }

        public List<WikiKeyValueItem> ProfileList { get; set; }
        public bool HasProfiles { get { return ProfileList != null && ProfileList.Count > 0; } }

        public List<WikiRoleGearItem> GearList { get; set; }
        public bool HasGears { get { return GearList != null && GearList.Count > 0; } }

        public WikiRoleCardInfo()
        {
            EnergyList = new List<WikiKeyValueItem>();
            FeatureList = new List<WikiKeyValueItem>();
            Stats = new List<WikiRoleStatItem>();
            ProfileList = new List<WikiKeyValueItem>();
            GearList = new List<WikiRoleGearItem>();
        }
    }

    public class WikiSkillRowItem
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public string TypeTag { get; set; }
        public string Description { get; set; }
        public string ColorHex { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
        public bool HasTypeTag { get { return !string.IsNullOrEmpty(TypeTag); } }
        public bool HasName { get { return !string.IsNullOrEmpty(Name); } }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }

        public WikiSkillRowItem()
        {
            Runs = new List<PostTextRun>();
        }
    }

    public class WikiVoiceRowItem
    {
        public string Tag { get; set; }
        public string Text { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }

        public WikiVoiceRowItem()
        {
            Runs = new List<PostTextRun>();
        }
    }

    public class WikiSectionItem : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Content { get; set; }
        public string IconUrl { get; set; }
        public string ImageUrl { get; set; }
        public List<WikiSectionItem> Children { get; set; }
        public List<PostTextRun> Runs { get; set; }

        /// <summary>
        /// 段内「有序内容块」（文字 / 图片 / 标题）。
        /// 只有它才能保证图片出现在正文里的正确位置 —— 以前一个 section 只存
        /// 一张 ImageUrl + 一坨扁平 Runs，于是多图段落的图片全被挤到末尾。
        /// </summary>
        public List<PostContentBlock> Blocks { get; set; }

        public bool HasChildren { get { return Children != null && Children.Count > 0; } }
        public bool HasImage { get { return !string.IsNullOrEmpty(ImageUrl); } }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
        public bool HasSubtitle { get { return !string.IsNullOrEmpty(Subtitle); } }
        public bool HasContent { get { return !string.IsNullOrEmpty(Content); } }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }
        public bool HasBlocks { get { return Blocks != null && Blocks.Count > 0; } }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged("ExpandGlyph");
                    OnPropertyChanged("ContentVisibility");
                }
            }
        }

        public string ExpandGlyph
        {
            get { return _isExpanded ? "" : ""; }
        }

        public Visibility ContentVisibility
        {
            get { return _isExpanded ? Visibility.Visible : Visibility.Collapsed; }
        }

        public WikiSectionItem()
        {
            Children = new List<WikiSectionItem>();
            Runs = new List<PostTextRun>();
            Blocks = new List<PostContentBlock>();
            _isExpanded = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WikiRotationStep
    {
        public int StepIndex { get; set; }
        public string StepText { get; set; }
        public string ActionType { get; set; }
        public bool IsKeyAction { get; set; }
    }

    public class WikiRotationLine
    {
        public string Title { get; set; }
        public string RawText { get; set; }
        public List<WikiRotationStep> Steps { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public bool HasSteps { get { return Steps != null && Steps.Count > 0; } }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }

        public WikiRotationLine()
        {
            Steps = new List<WikiRotationStep>();
            Runs = new List<PostTextRun>();
        }
    }

    public class WikiEquipRecommendItem
    {
        public string Name { get; set; }
        public string Count { get; set; }
        public string IconUrl { get; set; }
        public string Category { get; set; }
        public string Note { get; set; }
        public string SubInfo { get; set; }
        public string EntryId { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
        public bool HasLink { get { return !string.IsNullOrEmpty(EntryId) && EntryId != "0"; } }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }
        public bool HasNote { get { return !string.IsNullOrEmpty(Note); } }
        public bool HasSubInfo { get { return !string.IsNullOrEmpty(SubInfo); } }

        public WikiEquipRecommendItem()
        {
            Runs = new List<PostTextRun>();
        }
    }

    public class WikiEquipRecommendGroup
    {
        public string Title { get; set; }
        public List<WikiEquipRecommendItem> Items { get; set; }
        public string Tip { get; set; }
        public List<PostTextRun> TipRuns { get; set; }
        public bool HasTipRuns { get { return TipRuns != null && TipRuns.Count > 0; } }

        public WikiEquipRecommendGroup()
        {
            Items = new List<WikiEquipRecommendItem>();
            TipRuns = new List<PostTextRun>();
        }
    }

    public class WikiTabItem : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string RawContent { get; set; }
        public string CleanText { get; set; }
        public string BigImageUrl { get; set; }
        public List<string> ImageList { get; set; }
        public ObservableCollection<WikiSectionItem> Sections { get; set; }
        public List<WikiSkillRowItem> SkillRows { get; set; }
        public List<WikiVoiceRowItem> VoiceRows { get; set; }
        public List<WikiEquipRecommendGroup> EquipGroups { get; set; }
        public List<WikiRotationLine> RotationLines { get; set; }
        public List<PostTextRun> Runs { get; set; }
        public string LinkUrl { get; set; }
        public string LinkEntryId { get; set; }
        public string LinkTitle { get; set; }

        public bool HasLink { get { return !string.IsNullOrEmpty(LinkUrl) || (!string.IsNullOrEmpty(LinkEntryId) && LinkEntryId != "0"); } }
        public bool HasBigImage { get { return !string.IsNullOrEmpty(BigImageUrl); } }
        public bool HasSections { get { return Sections != null && Sections.Count > 0; } }
        public bool HasSkillRows { get { return SkillRows != null && SkillRows.Count > 0; } }
        public bool HasVoiceRows { get { return VoiceRows != null && VoiceRows.Count > 0; } }
        public bool HasEquipGroups { get { return EquipGroups != null && EquipGroups.Count > 0; } }
        public bool HasRotationLines { get { return RotationLines != null && RotationLines.Count > 0; } }
        public bool HasRuns { get { return Runs != null && Runs.Count > 0; } }
        public bool HasImages { get { return ImageList != null && ImageList.Count > 0 && !HasBigImage && !HasSections && !HasSkillRows && !HasVoiceRows && !HasEquipGroups && !HasRotationLines; } }
        public bool HasCleanText { get { return !string.IsNullOrEmpty(CleanText) && !HasSections && !HasSkillRows && !HasVoiceRows && !HasEquipGroups && !HasRotationLines; } }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public WikiTabItem()
        {
            ImageList = new List<string>();
            Sections = new ObservableCollection<WikiSectionItem>();
            SkillRows = new List<WikiSkillRowItem>();
            VoiceRows = new List<WikiVoiceRowItem>();
            EquipGroups = new List<WikiEquipRecommendGroup>();
            RotationLines = new List<WikiRotationLine>();
            Runs = new List<PostTextRun>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WikiStrategyItem
    {
        public string Title { get; set; }
        public string BgUrl { get; set; }
        public string EntryId { get; set; }
        public int LinkType { get; set; }
        public bool HasLink { get { return !string.IsNullOrEmpty(EntryId) && EntryId != "0"; } }
    }

    public class WikiAudioItem
    {
        public string Title { get; set; }
        public string Script { get; set; }
        public string PlayUrl { get; set; }
        public long FileSize { get; set; }
    }

    public class WikiAudioTab
    {
        public string Title { get; set; }
        public List<WikiAudioItem> Audios { get; set; }
        public WikiAudioTab()
        {
            Audios = new List<WikiAudioItem>();
        }
    }

    /// <summary>
    /// 意识手册专用 - 单条属性（生命/攻击/会心/防御）
    /// </summary>
    public class ConsciousnessStatItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string IconUrl { get; set; }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
    }

    /// <summary>
    /// 意识手册专用 - 套装技能效果（2件套 / 4件套）
    /// </summary>
    public class ConsciousnessSetEffect
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public bool HasTitle { get { return !string.IsNullOrEmpty(Title); } }
        public bool HasBody { get { return !string.IsNullOrEmpty(Body); } }
    }

    /// <summary>
    /// 意识手册专用 - 突破素材中的单项材料
    /// </summary>
    public class ConsciousnessMaterialItem
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public string Count { get; set; }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
        public bool HasCount { get { return !string.IsNullOrEmpty(Count); } }

        public string CountDisplay
        {
            get { return string.IsNullOrEmpty(Count) ? "" : "×" + Count; }
        }
    }

    /// <summary>
    /// 意识手册专用 - 突破阶段（突破1 ~ 突破4）
    /// </summary>
    public class ConsciousnessBreakStage
    {
        public string Stage { get; set; }
        public List<ConsciousnessMaterialItem> Materials { get; set; }
        public bool HasMaterials { get { return Materials != null && Materials.Count > 0; } }

        public ConsciousnessBreakStage()
        {
            Materials = new List<ConsciousnessMaterialItem>();
        }
    }

    /// <summary>
    /// 意识手册专用 - 意识故事
    /// </summary>
    public class ConsciousnessStory : INotifyPropertyChanged
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public bool HasBody { get { return !string.IsNullOrWhiteSpace(Body); } }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged("ExpandGlyph");
                    OnPropertyChanged("BodyVisibility");
                }
            }
        }

        public string ExpandGlyph
        {
            get { return _isExpanded ? "" : ""; }
        }

        public Visibility BodyVisibility
        {
            get { return _isExpanded ? Visibility.Visible : Visibility.Collapsed; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 意识手册专用 - 使用心得推荐卡片（缩略图 + 标题）
    /// </summary>
    public class ConsciousnessTipCard
    {
        public string Title { get; set; }
        public string IconUrl { get; set; }
        public bool HasTitle { get { return !string.IsNullOrEmpty(Title); } }
        public bool HasIcon { get { return !string.IsNullOrEmpty(IconUrl); } }
    }

    /// <summary>
    /// 意识手册专用 - 立绘页签（1/4号位 等）
    /// </summary>
    public class ConsciousnessIllustration
    {
        public string Title { get; set; }
        public string TabIconUrl { get; set; }
        public string ImageUrl { get; set; }
        public string Painter { get; set; }
        public bool HasImage { get { return !string.IsNullOrEmpty(ImageUrl); } }
        public bool HasPainter { get { return !string.IsNullOrEmpty(Painter); } }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 意识手册专用 - 条目整体的结构化数据
    /// </summary>
    public class ConsciousnessInfo : INotifyPropertyChanged
    {
        // 基础资料
        public string Rarity { get; set; }          // ★★★★★★
        public int RarityStars { get; set; }        // 6
        public string LevelCap { get; set; }        // 45
        public string ApplicableTo { get; set; }    // 构造体
        public string Traits { get; set; }          // 物理强化
        public string Acquire { get; set; }         // 商店等渠道获取
        public string CoverImageUrl { get; set; }

        public bool HasCoverImage { get { return !string.IsNullOrEmpty(CoverImageUrl); } }
        public bool HasRarity { get { return !string.IsNullOrEmpty(Rarity); } }
        public bool HasLevelCap { get { return !string.IsNullOrEmpty(LevelCap); } }
        public bool HasApplicableTo { get { return !string.IsNullOrEmpty(ApplicableTo); } }
        public bool HasTraits { get { return !string.IsNullOrEmpty(Traits); } }
        public bool HasAcquire { get { return !string.IsNullOrEmpty(Acquire); } }

        // 属性（初始/最大）
        public List<ConsciousnessStatItem> Stats { get; set; }
        public bool HasStats { get { return Stats != null && Stats.Count > 0; } }
        public List<ConsciousnessStatItem> PrimaryStats { get; set; }   // 1/2/3位
        public List<ConsciousnessStatItem> SecondaryStats { get; set; } // 4/5/6位
        public bool HasPrimaryStats { get { return PrimaryStats != null && PrimaryStats.Count > 0; } }
        public bool HasSecondaryStats { get { return SecondaryStats != null && SecondaryStats.Count > 0; } }

        // 套装技能效果
        public List<ConsciousnessSetEffect> SetEffects { get; set; }
        public bool HasSetEffects { get { return SetEffects != null && SetEffects.Count > 0; } }

        // 突破素材
        public List<ConsciousnessBreakStage> BreakStages { get; set; }
        public bool HasBreakStages { get { return BreakStages != null && BreakStages.Count > 0; } }

        // 意识故事
        public List<ConsciousnessStory> Stories { get; set; }
        public bool HasStories { get { return Stories != null && Stories.Count > 0; } }

        // 意识使用心得
        public List<ConsciousnessTipCard> Tips { get; set; }
        public bool HasTips { get { return Tips != null && Tips.Count > 0; } }
        public string TipsTitle { get; set; }

        // 意识立绘
        public List<ConsciousnessIllustration> Illustrations { get; set; }
        public bool HasIllustrations { get { return Illustrations != null && Illustrations.Count > 0; } }

        private ConsciousnessIllustration _selectedIllustration;
        public ConsciousnessIllustration SelectedIllustration
        {
            get { return _selectedIllustration; }
            set
            {
                if (_selectedIllustration != value)
                {
                    if (_selectedIllustration != null) _selectedIllustration.IsSelected = false;
                    _selectedIllustration = value;
                    if (_selectedIllustration != null) _selectedIllustration.IsSelected = true;
                    OnPropertyChanged();
                    OnPropertyChanged("ActiveIllustrationImage");
                    OnPropertyChanged("HasActiveIllustrationImage");
                    OnPropertyChanged("ActiveIllustrationPainter");
                    OnPropertyChanged("HasActiveIllustrationPainter");
                }
            }
        }

        public string ActiveIllustrationImage
        {
            get { return _selectedIllustration != null ? _selectedIllustration.ImageUrl : ""; }
        }

        public bool HasActiveIllustrationImage
        {
            get { return !string.IsNullOrEmpty(ActiveIllustrationImage); }
        }

        public string ActiveIllustrationPainter
        {
            get { return _selectedIllustration != null ? _selectedIllustration.Painter : ""; }
        }

        public bool HasActiveIllustrationPainter
        {
            get { return !string.IsNullOrEmpty(ActiveIllustrationPainter); }
        }

        public ConsciousnessInfo()
        {
            Stats = new List<ConsciousnessStatItem>();
            PrimaryStats = new List<ConsciousnessStatItem>();
            SecondaryStats = new List<ConsciousnessStatItem>();
            SetEffects = new List<ConsciousnessSetEffect>();
            BreakStages = new List<ConsciousnessBreakStage>();
            Stories = new List<ConsciousnessStory>();
            Tips = new List<ConsciousnessTipCard>();
            Illustrations = new List<ConsciousnessIllustration>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WikiDetailComponent : INotifyPropertyChanged
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Size { get; set; }
        public string Content { get; set; }
        public string CleanText { get; set; }
        public string ImageUrl { get; set; }
        public List<string> ImageList { get; set; }
        public List<PostTextRun> Runs { get; set; }

        /// <summary>
        /// 非 tabs 组件（basic-component / text-component）正文里的 &lt;details&gt; 折叠树。
        /// 例如「常见问题FAQ」「战斗系统」整篇都是 kr-collapse 折叠块，
        /// 以前走扁平 Runs 解析，折叠结构丢失且图片全部错位到末尾。
        /// </summary>
        public ObservableCollection<WikiSectionItem> Sections { get; set; }
        public bool HasSections { get { return Sections != null && Sections.Count > 0; } }

        private bool _isCollapsed;
        public bool IsCollapsed
        {
            get { return _isCollapsed; }
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsExpanded");
                    OnPropertyChanged("ExpandGlyph");
                    OnPropertyChanged("BodyVisibility");
                }
            }
        }

        public bool IsExpanded
        {
            get { return !_isCollapsed; }
        }

        public string ExpandGlyph
        {
            get { return _isCollapsed ? "" : ""; }
        }

        public Visibility BodyVisibility
        {
            get { return _isCollapsed ? Visibility.Collapsed : Visibility.Visible; }
        }

        public bool CanCollapse
        {
            get { return !string.IsNullOrEmpty(Title) && (Title.Contains("展开") || Title.Contains("详情") || _isCollapsed); }
        }

        public WikiRoleCardInfo RoleInfo { get; set; }
        public ObservableCollection<WikiTabItem> Tabs { get; set; }
        public List<WikiStrategyItem> Strategies { get; set; }
        public List<WikiAudioTab> AudioTabs { get; set; }

        /// <summary>意识手册专用结构化数据（非空即代表渲染意识版式）</summary>
        public ConsciousnessInfo Consciousness { get; set; }
        public bool HasConsciousnessInfo { get { return Consciousness != null; } }

        private WikiTabItem _selectedTab;
        public WikiTabItem SelectedTab
        {
            get { return _selectedTab; }
            set
            {
                if (_selectedTab != value)
                {
                    if (_selectedTab != null) _selectedTab.IsSelected = false;
                    _selectedTab = value;
                    if (_selectedTab != null) _selectedTab.IsSelected = true;
                    OnPropertyChanged();
                    OnPropertyChanged("ActiveTabCleanText");
                    OnPropertyChanged("ActiveTabRuns");
                    OnPropertyChanged("HasActiveTabRuns");
                    OnPropertyChanged("ActiveTabImages");
                    OnPropertyChanged("HasActiveTabImages");
                    OnPropertyChanged("ActiveTabBigImage");
                    OnPropertyChanged("HasActiveTabBigImage");
                    OnPropertyChanged("ActiveTabSections");
                    OnPropertyChanged("HasActiveTabSections");
                    OnPropertyChanged("ActiveTabSkillRows");
                    OnPropertyChanged("HasActiveTabSkillRows");
                    OnPropertyChanged("ActiveTabVoiceRows");
                    OnPropertyChanged("HasActiveTabVoiceRows");
                    OnPropertyChanged("ActiveTabEquipGroups");
                    OnPropertyChanged("HasActiveTabEquipGroups");
                    OnPropertyChanged("ActiveTabRotationLines");
                    OnPropertyChanged("HasActiveTabRotationLines");
                    OnPropertyChanged("HasActiveTabSimpleText");
                    OnPropertyChanged("ActiveTabLinkUrl");
                    OnPropertyChanged("ActiveTabLinkEntryId");
                    OnPropertyChanged("ActiveTabLinkTitle");
                    OnPropertyChanged("HasActiveTabLink");
                }
            }
        }

        public bool HasActiveTabLink
        {
            get { return _selectedTab != null && _selectedTab.HasLink; }
        }

        public string ActiveTabLinkTitle
        {
            get { return _selectedTab != null ? _selectedTab.LinkTitle : ""; }
        }

        public string ActiveTabLinkEntryId
        {
            get { return _selectedTab != null ? _selectedTab.LinkEntryId : ""; }
        }

        public string ActiveTabLinkUrl
        {
            get { return _selectedTab != null ? _selectedTab.LinkUrl : ""; }
        }

        public string ActiveTabCleanText
        {
            get { return _selectedTab != null ? _selectedTab.CleanText : CleanText; }
        }

        public List<PostTextRun> ActiveTabRuns
        {
            get { return _selectedTab != null ? _selectedTab.Runs : Runs; }
        }

        public bool HasActiveTabRuns
        {
            get { return ActiveTabRuns != null && ActiveTabRuns.Count > 0; }
        }

        public string ActiveTabBigImage
        {
            get { return _selectedTab != null ? _selectedTab.BigImageUrl : ""; }
        }

        public bool HasActiveTabBigImage
        {
            get { return !string.IsNullOrEmpty(ActiveTabBigImage); }
        }

        public ObservableCollection<WikiSectionItem> ActiveTabSections
        {
            get { return _selectedTab != null ? _selectedTab.Sections : null; }
        }

        public bool HasActiveTabSections
        {
            get { return ActiveTabSections != null && ActiveTabSections.Count > 0; }
        }

        public List<WikiSkillRowItem> ActiveTabSkillRows
        {
            get { return _selectedTab != null ? _selectedTab.SkillRows : null; }
        }

        public bool HasActiveTabSkillRows
        {
            get { return ActiveTabSkillRows != null && ActiveTabSkillRows.Count > 0; }
        }

        public List<WikiVoiceRowItem> ActiveTabVoiceRows
        {
            get { return _selectedTab != null ? _selectedTab.VoiceRows : null; }
        }

        public bool HasActiveTabVoiceRows
        {
            get { return ActiveTabVoiceRows != null && ActiveTabVoiceRows.Count > 0; }
        }

        public List<WikiEquipRecommendGroup> ActiveTabEquipGroups
        {
            get { return _selectedTab != null ? _selectedTab.EquipGroups : null; }
        }

        public bool HasActiveTabEquipGroups
        {
            get { return ActiveTabEquipGroups != null && ActiveTabEquipGroups.Count > 0; }
        }

        public List<WikiRotationLine> ActiveTabRotationLines
        {
            get { return _selectedTab != null ? _selectedTab.RotationLines : null; }
        }

        public bool HasActiveTabRotationLines
        {
            get { return ActiveTabRotationLines != null && ActiveTabRotationLines.Count > 0; }
        }

        public bool HasActiveTabSimpleText
        {
            get
            {
                if (HasActiveTabSections || HasActiveTabSkillRows || HasActiveTabVoiceRows || HasActiveTabEquipGroups || HasActiveTabRotationLines || HasActiveTabLink)
                {
                    return false;
                }
                return _selectedTab != null ? _selectedTab.HasCleanText : HasCleanText;
            }
        }

        public List<string> ActiveTabImages
        {
            get { return _selectedTab != null ? _selectedTab.ImageList : ImageList; }
        }

        public bool HasActiveTabImages
        {
            get { return _selectedTab != null ? _selectedTab.HasImages : HasImages; }
        }

        public bool IsSpecializedComponent
        {
            // HasSections 也算「专用版式」：一旦正文被解析成折叠树，
            // 就不要再渲染扁平的 Runs / ImageList，否则内容会出现两遍、
            // 且那一份扁平内容里的图片位置是错的。
            get { return HasRoleInfo || HasTabs || HasStrategies || HasAudios || HasConsciousnessInfo || HasSections; }
        }

        public bool HasRoleInfo { get { return RoleInfo != null; } }
        public bool HasTabs { get { return Tabs != null && Tabs.Count > 0; } }
        public bool HasStrategies { get { return Strategies != null && Strategies.Count > 0; } }
        public bool HasAudios { get { return AudioTabs != null && AudioTabs.Count > 0; } }
        public bool HasImages { get { return !IsSpecializedComponent && ImageList != null && ImageList.Count > 0; } }
        public bool HasSingleImage { get { return !IsSpecializedComponent && !string.IsNullOrEmpty(ImageUrl); } }
        public bool HasCleanText { get { return !IsSpecializedComponent && !string.IsNullOrEmpty(CleanText); } }
        public bool HasRuns { get { return !IsSpecializedComponent && Runs != null && Runs.Count > 0; } }

        public WikiDetailComponent()
        {
            ImageList = new List<string>();
            Tabs = new ObservableCollection<WikiTabItem>();
            Strategies = new List<WikiStrategyItem>();
            AudioTabs = new List<WikiAudioTab>();
            Runs = new List<PostTextRun>();
            Sections = new ObservableCollection<WikiSectionItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WikiDetailModule
    {
        public string Title { get; set; }
        public List<WikiDetailComponent> Components { get; set; }

        public WikiDetailModule()
        {
            Components = new List<WikiDetailComponent>();
        }
    }

    public class WikiEntryDetail
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string OrgFullName { get; set; }
        public string Title { get; set; }
        public string LastUpdateTime { get; set; }
        public string LastEditUserName { get; set; }
        public int BrowseCount { get; set; }
        public string CoverImageUrl { get; set; }
        public List<WikiDetailModule> Modules { get; set; }
        public List<WikiContributorItem> Contributors { get; set; }

        public WikiEntryDetail()
        {
            Modules = new List<WikiDetailModule>();
            Contributors = new List<WikiContributorItem>();
        }
    }

    public class WikiHomepageData
    {
        public int WikiType { get; set; }
        public string GameName { get; set; }
        public List<WikiBannerItem> Banners { get; set; }
        public List<WikiAnnouncementItem> Announcements { get; set; }
        public List<WikiShortcutItem> Shortcuts { get; set; }
        public List<WikiShortcutItem> MainModules { get; set; }
        public List<WikiShortcutItem> SideModules { get; set; }
        public List<WikiContributorItem> Contributors { get; set; }

        public WikiHomepageData()
        {
            Banners = new List<WikiBannerItem>();
            Announcements = new List<WikiAnnouncementItem>();
            Shortcuts = new List<WikiShortcutItem>();
            MainModules = new List<WikiShortcutItem>();
            SideModules = new List<WikiShortcutItem>();
            Contributors = new List<WikiContributorItem>();
        }
    }

    #endregion
}
