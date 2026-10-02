using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    /// <summary>
    /// 消息中心 API 封装。所有端点均来自真实抓包 / kurobbs_apis.json，未臆造任何接口或字段。
    /// 注意：@我的 暂无对应接口（抓包与 147 条 API 文档均无），对应方法保持为空实现（占位）。
    /// </summary>
    public sealed class KuroMessageService
    {
        private static KuroMessageService _instance;
        public static KuroMessageService Instance
        {
            get { return _instance ?? (_instance = new KuroMessageService()); }
        }

        private static readonly Regex PostIdRegex =
            new Regex(@"(?:/post[s]?/|postId=|post_id=)(\d{10,})", RegexOptions.IgnoreCase);

        #region 未读汇总

        public sealed class MessageUnreadSummary
        {
            public int CommentUnread;
            public int LikeUnread;
            public int FanUnread;
            public int NoticeUnread;
            public int TotalUnread
            {
                get { return CommentUnread + LikeUnread + FanUnread + NoticeUnread; }
            }
        }

        /// <summary>拉取各分类未读数（/user/notice/list）。</summary>
        /// <remarks>
        /// 真实抓包（message/[1144] 等）中该接口必带 pageIndex/pageSize。
        /// 之前传空 body，服务端按「缺少必填参数」返回 code 102「服务器外部错误」，
        /// 导致主界面消息红点与未读汇总始终加载失败。
        /// </remarks>
        public async Task<MessageUnreadSummary> GetUnreadAsync()
        {
            var summary = new MessageUnreadSummary();
            var parameters = new Dictionary<string, string>
            {
                { "pageIndex", "1" },
                { "pageSize", "10" }
            };
            var json = await KuroApiClient.Instance.PostFormAsync("/user/notice/list", parameters);
            if (json == null || !json.ContainsKey("data") || json.GetNamedValue("data").ValueType != JsonValueType.Object)
                return summary;

            var data = json.GetNamedObject("data");
            summary.CommentUnread = (int)GetNumber(data, "commentUnReadCount", 0);
            summary.LikeUnread = (int)GetNumber(data, "likeUnReadCount", 0);
            summary.FanUnread = (int)GetNumber(data, "fanUnReadCount", 0);
            // 通知类：聊天 + 系统/活动（help）。doc 中字段名为 helpUnReadCount，旧抓包为 chatUnReadCount。
            int chat = (int)GetNumber(data, "chatUnReadCount", 0);
            int help = (int)GetNumber(data, "helpUnReadCount", 0);
            summary.NoticeUnread = chat + help;
            return summary;
        }

        #endregion

        #region 通知（会话 + 列表）

        /// <summary>通知会话列表（GET /user/notice/senders）。</summary>
        public async Task<List<MessageSenderItem>> GetNoticeSendersAsync()
        {
            var list = new List<MessageSenderItem>();
            var json = await KuroApiClient.Instance.GetAsync("/user/notice/senders");
            if (json == null || !json.ContainsKey("data") || json.GetNamedValue("data").ValueType != JsonValueType.Array)
                return list;

            var arr = json.GetNamedArray("data");
            foreach (var v in arr)
            {
                if (v.ValueType != JsonValueType.Object) continue;
                var o = v.GetObject();
                list.Add(new MessageSenderItem
                {
                    SenderId = GetString(o, "senderId", ""),
                    SenderName = GetString(o, "senderName", "通知"),
                    SenderType = GetString(o, "senderType", ""),
                    Icon = GetString(o, "icon", ""),
                    Title = GetString(o, "title", ""),
                    PointNum = GetString(o, "pointNum", ""),
                    NoticeTime = GetString(o, "noticeTime", ""),
                    ShowTime = GetString(o, "showTime", ""),
                    ShowNoticeTime = GetString(o, "showNoticeTime", ""),
                    Type = GetString(o, "type", "")
                });
            }
            return list;
        }

        /// <summary>某会话下的通知列表（POST /user/notice/help/page）。</summary>
        public async Task<MessageNoticePage> GetNoticeListAsync(string senderId, string senderType, int pageIndex = 1, int pageSize = 20)
        {
            var page = new MessageNoticePage();
            var parameters = new Dictionary<string, string>
            {
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "senderId", senderId ?? "" },
                { "senderType", senderType ?? "" },
                { "loadSenderName", "false" }
            };
            var json = await KuroApiClient.Instance.PostFormAsync("/user/notice/help/page", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                page.HasNext = GetNumber(data, "hasNext", 0) == 1 || GetBool(data, "hasNext", false);
                if (data.ContainsKey("noticeVos") && data.GetNamedValue("noticeVos").ValueType == JsonValueType.Array)
                {
                    foreach (var v in data.GetNamedArray("noticeVos"))
                    {
                        if (v.ValueType == JsonValueType.Object)
                            page.Items.Add(ParseNoticeItem(v.GetObject()));
                    }
                }
            }
            return page;
        }

        #endregion

        #region 评论和回复 / 点赞

        public async Task<MessageNoticePage> GetCommentNoticeAsync(int pageIndex = 1, int pageSize = 20)
        {
            return await GetNoticePageAsync("/user/notice/comment/page", pageIndex, pageSize);
        }

        public async Task<MessageNoticePage> GetLikeNoticeAsync(int pageIndex = 1, int pageSize = 20)
        {
            var parameters = new Dictionary<string, string>
            {
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "scan", "0" }
            };
            return await GetNoticePageAsync("/user/notice/like/page", parameters);
        }

        private async Task<MessageNoticePage> GetNoticePageAsync(string endpoint, int pageIndex, int pageSize)
        {
            var parameters = new Dictionary<string, string>
            {
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };
            return await GetNoticePageAsync(endpoint, parameters);
        }

        private async Task<MessageNoticePage> GetNoticePageAsync(string endpoint, Dictionary<string, string> parameters)
        {
            var page = new MessageNoticePage();
            var json = await KuroApiClient.Instance.PostFormAsync(endpoint, parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                page.HasNext = GetNumber(data, "hasNext", 0) == 1 || GetBool(data, "hasNext", false);
                if (data.ContainsKey("noticeVos") && data.GetNamedValue("noticeVos").ValueType == JsonValueType.Array)
                {
                    foreach (var v in data.GetNamedArray("noticeVos"))
                    {
                        if (v.ValueType == JsonValueType.Object)
                            page.Items.Add(ParseNoticeItem(v.GetObject()));
                    }
                }
            }
            return page;
        }

        #endregion

        #region 新增粉丝

        public async Task<MessageFanPage> GetFansAsync(int pageIndex = 1, int pageSize = 20, int type = 1)
        {
            var page = new MessageFanPage();
            var parameters = new Dictionary<string, string>
            {
                { "pageNo", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "type", type.ToString() }
            };
            var json = await KuroApiClient.Instance.PostFormAsync("/user/fans", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                if (data.ContainsKey("fansInfo") && data.GetNamedValue("fansInfo").ValueType == JsonValueType.Array)
                {
                    var arr = data.GetNamedArray("fansInfo");
                    foreach (var v in arr)
                    {
                        if (v.ValueType != JsonValueType.Object) continue;
                        var o = v.GetObject();
                        var item = new MessageFanItem
                        {
                            FollowUserId = GetString(o, "followUserId", ""),
                            UserId = GetString(o, "userId", ""),
                            UserName = GetString(o, "userName", "用户"),
                            UserUrl = GetString(o, "userUrl", ""),
                            UserHeadCode = GetString(o, "userHeadCode", ""),
                            Signature = GetString(o, "signature", "这个人很懒，还没有签名。"),
                            FansCount = (int)GetNumber(o, "fansCount", 0),
                            PostCount = (int)GetNumber(o, "postCount", 0),
                            IsFollow = (int)GetNumber(o, "isFollow", 0),
                            IsNew = (int)GetNumber(o, "isNew", 0),
                            MutualFollow = (int)GetNumber(o, "mutualFollow", 0),
                            UserFollowId = GetString(o, "userFollowId", "")
                        };
                        item.IsFollowBool = item.IsFollow == 2;
                        page.Items.Add(item);
                    }
                    page.HasNext = arr.Count >= pageSize;
                }
            }
            return page;
        }

        #endregion

        #region 回复我的评论（getReplyList）

        public async Task<MessageReplyListResult> GetReplyListAsync(string postCommentId, string postId, int pageIndex = 1, int pageSize = 50)
        {
            var result = new MessageReplyListResult();
            if (string.IsNullOrEmpty(postCommentId) || string.IsNullOrEmpty(postId))
                return result;

            var parameters = new Dictionary<string, string>
            {
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "postCommentId", postCommentId },
                { "postId", postId }
            };
            var json = await KuroApiClient.Instance.PostFormAsync("/forum/comment/getReplyList", parameters);
            if (json == null || !json.ContainsKey("data") || json.GetNamedValue("data").ValueType != JsonValueType.Object)
                return result;

            var data = json.GetNamedObject("data");
            if (data.ContainsKey("postComment") && data.GetNamedValue("postComment").ValueType == JsonValueType.Object)
            {
                var pcObj = data.GetNamedObject("postComment");
                result.PostComment = ParseNoticeItem(pcObj, true);
                // postComment 的正文是富文本块数组 commentContent（含表情 token），
                // 必须解析成 runs 才能渲染；直接读 commentContent 字符串会得到空值。
                string pcText;
                result.PostComment.ContentRuns = ExtractContentRuns(pcObj, "commentContent", out pcText);
                if (string.IsNullOrEmpty(result.PostComment.NoticeContent))
                    result.PostComment.NoticeContent = pcText;
            }
            if (data.ContainsKey("replys") && data.GetNamedValue("replys").ValueType == JsonValueType.Array)
            {
                foreach (var v in data.GetNamedArray("replys"))
                {
                    if (v.ValueType != JsonValueType.Object) continue;
                    var o = v.GetObject();
                    string replyText;
                    var replyRuns = ExtractContentRuns(o, "replyContent", out replyText);
                    result.Replys.Add(new MessageReplyItem
                    {
                        ReplyId = GetString(o, "replyId", ""),
                        CommentId = GetString(o, "commentId", ""),
                        PostCommentId = GetString(o, "postCommentId", postCommentId),
                        PostId = GetString(o, "postId", postId),
                        UserId = GetString(o, "userId", ""),
                        UserName = GetString(o, "userName", "用户"),
                        ToUserId = GetString(o, "toUserId", ""),
                        ToUserName = GetString(o, "toUserName", ""),
                        ReplyContent = replyText,
                        ContentRuns = replyRuns,
                        IsPublisher = GetNumber(o, "isPublisher", 0) == 1 || GetBool(o, "isPublisher", false),
                        ReplyTime = GetString(o, "replyTime", GetString(o, "showTime", "")),
                        UserHeadUrl = GetString(o, "userHeadUrl", GetString(o, "headUrl", ""))
                    });
                }
            }
            result.HasNext = GetNumber(data, "hasNext", 0) == 1 || GetBool(data, "hasNext", false);
            return result;
        }

        #endregion

        #region 已读 / 清除（尽力而为，失败不抛异常）

        public async Task ReadNoticeAsync(string ids)
        {
            try
            {
                var p = new Dictionary<string, string> { { "ids", ids ?? "" } };
                await KuroApiClient.Instance.PostFormAsync("/user/notice/readNotice", p);
            }
            catch { }
        }

        public async Task CleanAsync(string type, string ids)
        {
            try
            {
                var p = new Dictionary<string, string> { { "type", type ?? "" }, { "ids", ids ?? "" } };
                await KuroApiClient.Instance.PostFormAsync("/user/notice/clean", p);
            }
            catch { }
        }

        #endregion

        #region 解析辅助

        private MessageNoticeItem ParseNoticeItem(JsonObject o, bool isReplyComment = false)
        {
            var item = new MessageNoticeItem
            {
                Id = GetString(o, "id", GetString(o, "noticeId", "")),
                ContentId = GetString(o, "contentId", ""),
                DetailType = (int)GetNumber(o, "detailType", 0),
                GameId = (int)GetNumber(o, "gameId", 0),
                GameName = GetString(o, "gameName", ""),
                NoticeContent = GetString(o, "noticeContent", GetString(o, "content", "")),
                NoticeContentV2 = GetString(o, "noticeContentV2", ""),
                NoticeTitle = GetString(o, "noticeTitle", GetString(o, "title", "")),
                NoticeTitleV2 = GetString(o, "noticeTitleV2", ""),
                NumCount = (int)GetNumber(o, "numCount", 0),
                PostCommentId = GetString(o, "postCommentId", GetString(o, "commentId", "")),
                PostCommentReplyId = GetString(o, "postCommentReplyId", ""),
                PostId = GetString(o, "postId", ""),
                ReadState = (int)GetNumber(o, "readState", 0),
                // 【注意】**不要**把 picUrl 当头像兜底：它是通知配图（…/notice/xxx.jpg），
                // 不是头像（头像在 …/avatar/ 或 …/headCode/ 下）。
                // 系统通知（如「库街区小助手」）没有 sendUserHeadUrl，此时留空，
                // 由 NoticeListPage 用会话(sender)的 icon 兜底 —— 否则头像位会显示
                // 一张不相干的通知配图，看起来就像"头像加载错了"。
                SendUserHeadUrl = GetString(o, "sendUserHeadUrl", GetString(o, "headUrl", GetString(o, "userHeadUrl", ""))),
                SendUserId = GetString(o, "sendUserId", GetString(o, "userId", "")),
                SendUserName = GetString(o, "sendUserName", GetString(o, "userName", "")),
                ShowTime = GetString(o, "showTime", GetString(o, "noticeTime", GetString(o, "commentTime", GetString(o, "replyTime", "")))),
                Source = GetString(o, "source", ""),
                TitleCanView = GetString(o, "titleCanView", ""),
                ImageInfo = GetString(o, "imageInfo", ""),
                // 通知配图（picUrl）。它跟头像无关，单独存放，由 NoticeListPage 在正文下方展示。
                PicUrl = GetString(o, "picUrl", ""),
                NoticeStatus = GetString(o, "noticeStatus", ""),
                PushTime = GetString(o, "pushTime", ""),
                MergeNoticeIds = GetString(o, "mergeNoticeIds", ""),
                MergeSendUserIds = GetString(o, "mergeSendUserIds", ""),
                H5MergeNoticeIds = GetString(o, "h5MergeNoticeIds", "")
            };

            // 解析跳转帖子 id：优先 postId -> contentId -> 从 link/linkTarget 提取
            string targetPostId = "";
            if (!string.IsNullOrEmpty(item.PostId))
                targetPostId = item.PostId;
            else if (!string.IsNullOrEmpty(item.ContentId))
                targetPostId = item.ContentId;

            string link = GetString(o, "link", GetString(o, "linkTarget", ""));
            if (string.IsNullOrEmpty(targetPostId) && !string.IsNullOrEmpty(link))
            {
                var m = PostIdRegex.Match(link);
                if (m.Success) targetPostId = m.Groups[1].Value;
                else item.TargetUrl = link;
            }
            item.TargetPostId = targetPostId;
            return item;
        }

        /// <summary>
        /// 从 commentContent / replyContent 取值。该字段既可能是纯文本字符串，
        /// 也可能是富文本块数组（[{ "children":[{content,type,target}], "contentType":1 }]）。
        /// 返回可直接喂给 RichTextBlock（helpers:InlineRunsHelper.RunsSource）的 runs，
        /// 并通过 plainText 回传纯文本兜底。
        /// </summary>
        private List<PostTextRun> ExtractContentRuns(JsonObject obj, string key, out string plainText)
        {
            plainText = "";
            var runs = new List<PostTextRun>();
            if (obj == null || !obj.ContainsKey(key)) return runs;

            var val = obj.GetNamedValue(key);
            if (val.ValueType == JsonValueType.String)
            {
                runs = KuroEmojiService.Instance.ParseTextToRuns(val.GetString());
            }
            else if (val.ValueType == JsonValueType.Array)
            {
                runs = KuroEmojiService.Instance.ParseBlocksJsonToRuns(val.Stringify());
            }

            var sb = new System.Text.StringBuilder();
            foreach (var r in runs)
            {
                if (r != null && !string.IsNullOrEmpty(r.Text)) sb.Append(r.Text);
            }
            plainText = sb.ToString().Trim();
            return runs;
        }

        private string GetString(JsonObject obj, string key, string defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber().ToString();
            }
            return defVal;
        }

        private double GetNumber(JsonObject obj, string key, double defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Number) return val.GetNumber();
                if (val.ValueType == JsonValueType.String)
                {
                    double d;
                    if (double.TryParse(val.GetString(), out d)) return d;
                }
            }
            return defVal;
        }

        private bool GetBool(JsonObject obj, string key, bool defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber() == 1;
                if (val.ValueType == JsonValueType.String)
                {
                    bool b;
                    if (bool.TryParse(val.GetString(), out b)) return b;
                }
            }
            return defVal;
        }

        #endregion
    }

    public sealed class MessageNoticePage
    {
        public List<MessageNoticeItem> Items { get; private set; }
        public bool HasNext { get; set; }
        public MessageNoticePage() { Items = new List<MessageNoticeItem>(); }
    }

    public sealed class MessageFanPage
    {
        public List<MessageFanItem> Items { get; private set; }
        public bool HasNext { get; set; }
        public MessageFanPage() { Items = new List<MessageFanItem>(); }
    }
}
