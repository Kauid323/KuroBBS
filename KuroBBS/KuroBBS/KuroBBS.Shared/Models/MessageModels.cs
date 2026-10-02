using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace KuroBBS.Models
{
    /// <summary>
    /// 通知会话（/user/notice/senders 返回的每条记录）。
    /// 点击后进入该会话的通知列表页（/user/notice/help/page）。
    /// </summary>
    public sealed class MessageSenderItem : INotifyPropertyChanged
    {
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderType { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string PointNum { get; set; }     // 该会话未读数（字符串，原样展示）
        public string NoticeTime { get; set; }
        public string ShowTime { get; set; }
        public string ShowNoticeTime { get; set; }
        public string Type { get; set; }

        /// <summary>未读数 > 0 时为 true（控制红点显隐）。</summary>
        public bool HasPoint
        {
            get
            {
                int n;
                return int.TryParse(PointNum, out n) && n > 0;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    /// <summary>
    /// 通用的消息通知条目，覆盖 通知(help)/评论和回复(comment)/点赞(like) 三种列表。
    /// 字段全部来源于真实抓包，未臆造任何字段。
    /// </summary>
    public sealed class MessageNoticeItem : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string ContentId { get; set; }
        public int DetailType { get; set; }          // 1=评论/点赞我的帖子, 2=回复我的评论(含 postCommentReplyId)
        public int GameId { get; set; }
        public string GameName { get; set; }
        public string NoticeContent { get; set; }     // 正文（可能含表情 token，如 _[/太棒了]）
        public string NoticeContentV2 { get; set; }   // 结构化正文（JSON 数组）
        public string NoticeTitle { get; set; }       // 标题，如「赞了我的评论」「回复了我的评论」
        public string NoticeTitleV2 { get; set; }
        public int NumCount { get; set; }             // 点赞数等
        public string PostCommentId { get; set; }     // 定位评论用
        public string PostCommentReplyId { get; set; }// 回复我的评论时存在
        public string PostId { get; set; }            // 跳转帖子详情用
        public int ReadState { get; set; }            // 0=未读 1=已读
        public string SendUserHeadUrl { get; set; }
        public string SendUserId { get; set; }
        public string SendUserName { get; set; }
        public string ShowTime { get; set; }
        public string Source { get; set; }
        public string TitleCanView { get; set; }
        public string ImageInfo { get; set; }

        /// <summary>
        /// 通知配图（/user/notice/help/page 等返回的 picUrl，如 …/notice/xxx.png）。
        /// 注意：它是**通知的配图**，不是头像 —— 头像用 SendUserHeadUrl。
        /// NoticeListPage 会在正文下方把它作为缩略图展示。
        /// </summary>
        public string PicUrl { get; set; }

        /// <summary>有配图时为 true（控制配图显隐）。</summary>
        public bool HasPic
        {
            get { return !string.IsNullOrEmpty(PicUrl); }
        }

        public string NoticeStatus { get; set; }
        public string PushTime { get; set; }
        public string MergeNoticeIds { get; set; }
        public string MergeSendUserIds { get; set; }
        public string H5MergeNoticeIds { get; set; }

        /// <summary>
        /// 富文本 runs（含表情图片）。getReplyList 的 postComment.commentContent 是块数组，
        /// 必须解析成 runs 交给 RichTextBlock 渲染，光靠 NoticeContent 会是空字符串。
        /// </summary>
        public List<PostTextRun> ContentRuns { get; set; }

        /// <summary>跳转用的帖子 id（优先 postId/contentId，否则从 link/linkTarget 中解析）。</summary>
        public string TargetPostId { get; set; }
        /// <summary>若通知指向站外/非帖子链接，则存于此（用于 Launcher 跳转）。</summary>
        public string TargetUrl { get; set; }

        /// <summary>是否为「回复我的评论」（detailType=2 且带 postCommentReplyId），点击走 getReplyList。</summary>
        public bool IsReplyToMe
        {
            get { return DetailType == 2 && !string.IsNullOrEmpty(PostCommentReplyId); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    /// <summary>
    /// 新增粉丝条目（/user/fans type=1）。
    /// </summary>
    public sealed class MessageFanItem : INotifyPropertyChanged
    {
        public string FollowUserId { get; set; }   // 关注关系 id
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserUrl { get; set; }        // 头像
        public string UserHeadCode { get; set; }
        public string Signature { get; set; }
        public int FansCount { get; set; }
        public int PostCount { get; set; }
        public int IsFollow { get; set; }          // 2=已关注
        public int IsNew { get; set; }
        public int MutualFollow { get; set; }      // 1=互相关注
        public string UserFollowId { get; set; }

        private bool _isFollow;
        public bool IsFollowBool
        {
            get { return _isFollow; }
            set { _isFollow = value; OnChanged("IsFollowBool"); OnChanged("FollowButtonText"); }
        }

        public string FollowButtonText
        {
            get { return _isFollow ? "已关注" : "回关"; }
        }

        public bool CanFollowBack
        {
            get { return !_isFollow; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    /// <summary>
    /// getReplyList 单条回复。
    /// </summary>
    public sealed class MessageReplyItem : INotifyPropertyChanged
    {
        public string ReplyId { get; set; }
        public string CommentId { get; set; }
        public string PostCommentId { get; set; }
        public string PostId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string ToUserId { get; set; }
        public string ToUserName { get; set; }
        public string ReplyContent { get; set; }
        public bool IsPublisher { get; set; }       // 是否楼主
        public string ReplyTime { get; set; }
        public string UserHeadUrl { get; set; }

        /// <summary>
        /// 富文本 runs（含表情图片）。getReplyList 的 replyContent 是块数组
        /// （[{"children":[{"content":"谢谢你","type":1},{"content":"_[/开心]","target":"…","type":2}],…}]），
        /// 直接当字符串读会得到空值 → 回复正文全部不显示。
        /// </summary>
        public List<PostTextRun> ContentRuns { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string p) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(p)); }
    }

    /// <summary>
    /// getReplyList 返回结构：被回复的原评论(postComment) + 回复列表(replys)。
    /// </summary>
    public sealed class MessageReplyListResult
    {
        public MessageNoticeItem PostComment { get; set; }
        public List<MessageReplyItem> Replys { get; set; }
        public bool HasNext { get; set; }

        public MessageReplyListResult()
        {
            Replys = new List<MessageReplyItem>();
        }
    }
}
