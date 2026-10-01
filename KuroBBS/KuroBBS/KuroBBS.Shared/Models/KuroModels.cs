using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        public string Url { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
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

        public PostCommentItem()
        {
            ContentRuns = new List<PostTextRun>();
            Replies = new List<PostReplyItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
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
}
