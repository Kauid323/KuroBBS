using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    /// <summary>
    /// 消息中心 ViewModel：持有 5 个 tab 的数据、未读汇总、加载/翻页/回关逻辑。
    /// 导航由各页面的 code-behind 负责（与 MainPage 既有约定一致）。
    /// </summary>
    public class MessageHubViewModel : ViewModelBase
    {
        #region 未读汇总（主界面红点）

        private int _commentUnread;
        public int CommentUnread { get { return _commentUnread; } set { _commentUnread = value; OnPropertyChanged(); OnPropertyChanged("TotalUnread"); OnPropertyChanged("HasUnread"); OnPropertyChanged("BadgeText"); } }

        private int _likeUnread;
        public int LikeUnread { get { return _likeUnread; } set { _likeUnread = value; OnPropertyChanged(); OnPropertyChanged("TotalUnread"); OnPropertyChanged("HasUnread"); OnPropertyChanged("BadgeText"); } }

        private int _fanUnread;
        public int FanUnread { get { return _fanUnread; } set { _fanUnread = value; OnPropertyChanged(); OnPropertyChanged("TotalUnread"); OnPropertyChanged("HasUnread"); OnPropertyChanged("BadgeText"); } }

        private int _noticeUnread;
        public int NoticeUnread { get { return _noticeUnread; } set { _noticeUnread = value; OnPropertyChanged(); OnPropertyChanged("TotalUnread"); OnPropertyChanged("HasUnread"); OnPropertyChanged("BadgeText"); } }

        public int TotalUnread { get { return CommentUnread + LikeUnread + FanUnread + NoticeUnread; } }
        public bool HasUnread { get { return TotalUnread > 0; } }

        /// <summary>红点徽标文本：超过 99 显示 99+。</summary>
        public string BadgeText
        {
            get
            {
                int t = TotalUnread;
                return t <= 0 ? "" : (t > 99 ? "99+" : t.ToString());
            }
        }

        #endregion

        #region 集合

        public ObservableCollection<MessageSenderItem> NoticeSenders { get; private set; }
        public ObservableCollection<MessageNoticeItem> NoticeListItems { get; private set; }   // 通知列表页（某会话）
        public ObservableCollection<MessageNoticeItem> CommentNotices { get; private set; }
        public ObservableCollection<MessageNoticeItem> LikeNotices { get; private set; }
        public ObservableCollection<MessageNoticeItem> AtMeNotices { get; private set; }        // @我的（占位，无接口）
        public ObservableCollection<MessageFanItem> Fans { get; private set; }
        public ObservableCollection<MessageReplyItem> ReplyItems { get; private set; }          // 回复我的评论
        public MessageNoticeItem ReplyPostComment { get; private set; }

        #endregion

        #region 翻页状态

        private int _commentPage = 0;
        private int _likePage = 0;
        private int _fanPage = 0;
        private int _noticeListPage = 0;
        private bool _commentHasNext;
        private bool _likeHasNext;
        private bool _fanHasNext;
        private bool _noticeListHasNext;
        private string _noticeListSenderId;
        private string _noticeListSenderType;

        public bool CommentHasNext { get { return _commentHasNext; } set { _commentHasNext = value; OnPropertyChanged(); } }
        public bool LikeHasNext { get { return _likeHasNext; } set { _likeHasNext = value; OnPropertyChanged(); } }
        public bool FanHasNext { get { return _fanHasNext; } set { _fanHasNext = value; OnPropertyChanged(); } }
        public bool NoticeListHasNext { get { return _noticeListHasNext; } set { _noticeListHasNext = value; OnPropertyChanged(); } }

        #endregion

        public MessageHubViewModel()
        {
            NoticeSenders = new ObservableCollection<MessageSenderItem>();
            NoticeListItems = new ObservableCollection<MessageNoticeItem>();
            CommentNotices = new ObservableCollection<MessageNoticeItem>();
            LikeNotices = new ObservableCollection<MessageNoticeItem>();
            AtMeNotices = new ObservableCollection<MessageNoticeItem>();
            Fans = new ObservableCollection<MessageFanItem>();
            ReplyItems = new ObservableCollection<MessageReplyItem>();
        }

        #region 加载

        public async Task LoadUnreadAsync()
        {
            if (!SettingsHelper.IsLoggedIn) return;
            var summary = await KuroMessageService.Instance.GetUnreadAsync();
            if (summary == null) return;
            NoticeUnread = summary.NoticeUnread;
            CommentUnread = summary.CommentUnread;
            LikeUnread = summary.LikeUnread;
            FanUnread = summary.FanUnread;
        }

        public async Task LoadNoticeSendersAsync()
        {
            if (!SettingsHelper.IsLoggedIn) return;
            IsBusy = true;
            try
            {
                var list = await KuroMessageService.Instance.GetNoticeSendersAsync();
                NoticeSenders.Clear();
                foreach (var s in list) NoticeSenders.Add(s);
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_SENDERS", "加载通知会话失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        public async Task LoadNoticeListAsync(string senderId, string senderType, bool reset = true, string fallbackAvatarUrl = null)
        {
            if (!SettingsHelper.IsLoggedIn) return;
            if (reset)
            {
                _noticeListPage = 0;
                _noticeListSenderId = senderId;
                _noticeListSenderType = senderType;
            }
            IsBusy = true;
            try
            {
                var page = await KuroMessageService.Instance.GetNoticeListAsync(senderId, senderType, _noticeListPage + 1);
                if (page != null)
                {
                    if (reset) NoticeListItems.Clear();
                    foreach (var it in page.Items)
                    {
                        // 系统通知没有 sendUserHeadUrl，用会话(sender)的 icon 兜底，
                        // 否则通知列表里的头像位是空的（或显示错图）。
                        if (string.IsNullOrEmpty(it.SendUserHeadUrl) && !string.IsNullOrEmpty(fallbackAvatarUrl))
                            it.SendUserHeadUrl = fallbackAvatarUrl;
                        NoticeListItems.Add(it);
                    }
                    _noticeListPage++;
                    NoticeListHasNext = page.HasNext;
                }
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_NOTICE_LIST", "加载通知列表失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        public async Task LoadCommentNoticesAsync(bool reset = true)
        {
            if (!SettingsHelper.IsLoggedIn) return;
            if (reset) _commentPage = 0;
            IsBusy = true;
            try
            {
                var page = await KuroMessageService.Instance.GetCommentNoticeAsync(_commentPage + 1);
                if (page != null)
                {
                    if (reset) CommentNotices.Clear();
                    foreach (var it in page.Items) CommentNotices.Add(it);
                    _commentPage++;
                    CommentHasNext = page.HasNext;
                }
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_COMMENT", "加载评论和回复失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        public async Task LoadLikeNoticesAsync(bool reset = true)
        {
            if (!SettingsHelper.IsLoggedIn) return;
            if (reset) _likePage = 0;
            IsBusy = true;
            try
            {
                var page = await KuroMessageService.Instance.GetLikeNoticeAsync(_likePage + 1);
                if (page != null)
                {
                    if (reset) LikeNotices.Clear();
                    foreach (var it in page.Items) LikeNotices.Add(it);
                    _likePage++;
                    LikeHasNext = page.HasNext;
                }
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_LIKE", "加载点赞失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        /// <summary>@我的：抓包与 API 文档均无对应接口，暂不发起网络请求（占位）。</summary>
        public async Task LoadAtMeAsync()
        {
            if (!SettingsHelper.IsLoggedIn) return;
            await Task.Yield();
            AtMeNotices.Clear();
            StatusMessage = "暂无 @我的 消息（该接口未抓包，暂不支持）";
        }

        public async Task LoadFansAsync(bool reset = true)
        {
            if (!SettingsHelper.IsLoggedIn) return;
            if (reset) _fanPage = 0;
            IsBusy = true;
            try
            {
                var page = await KuroMessageService.Instance.GetFansAsync(_fanPage + 1, 20, 1);
                if (page != null)
                {
                    if (reset) Fans.Clear();
                    foreach (var it in page.Items) Fans.Add(it);
                    _fanPage++;
                    FanHasNext = page.HasNext;
                }
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_FANS", "加载新增粉丝失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        public async Task LoadReplyListAsync(string postCommentId, string postId)
        {
            if (!SettingsHelper.IsLoggedIn) return;
            IsBusy = true;
            try
            {
                var result = await KuroMessageService.Instance.GetReplyListAsync(postCommentId, postId);
                if (result != null)
                {
                    ReplyPostComment = result.PostComment;
                    OnPropertyChanged("ReplyPostComment");
                    ReplyItems.Clear();
                    foreach (var r in result.Replys) ReplyItems.Add(r);
                }
            }
            catch (System.Exception ex)
            {
                KuroLogger.Error("MSG_REPLY", "加载回复列表失败: " + ex.Message, ex);
            }
            finally { IsBusy = false; }
        }

        #endregion

        #region 操作

        /// <summary>回关（新增粉丝 tab 的按钮）。</summary>
        public async Task FollowBackAsync(MessageFanItem fan)
        {
            if (fan == null || string.IsNullOrEmpty(fan.UserId)) return;
            bool ok = await KuroUserService.Instance.FollowUserAsync(fan.UserId, true);
            if (ok)
            {
                fan.IsFollowBool = true;
                fan.MutualFollow = 1;
            }
        }

        #endregion
    }
}
