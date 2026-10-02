using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroForumService
    {
        private static KuroForumService _instance;
        public static KuroForumService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroForumService();
                }
                return _instance;
            }
        }

        public async Task<List<PostItem>> GetCommunityPostsAsync(int gameId, int forumId = 0, int searchType = 3, int pageIndex = 1, int pageSize = 20)
        {
            if (gameId <= 0) gameId = 3;
            if (forumId <= 0)
            {
                forumId = (gameId == 3) ? 9 : 2;
            }

            KuroLogger.Loading("COMMUNITY_FEED", string.Format("Loading community posts: GameId={0}, ForumId={1}, SearchType={2} (Page={3}, PageSize={4})", 
                gameId, forumId, searchType, pageIndex, pageSize));
            var result = new List<PostItem>();

            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "forumId", forumId.ToString() },
                { "searchType", searchType.ToString() },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/list", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("postList") && dataObj.GetNamedValue("postList").ValueType == JsonValueType.Array)
                {
                    var postListArray = dataObj.GetNamedArray("postList");
                    foreach (var itemVal in postListArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var obj = itemVal.GetObject();
                        var post = ParseCommunityPostItem(obj, gameId);
                        if (post != null)
                        {
                            result.Add(post);
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<PostItem>> GetOfficialEventsAsync(int gameId, int eventType, int pageNo = 1, int pageSize = 10)
        {
            if (gameId <= 0) gameId = 2;
            string typeName = eventType == 1 ? "活动" : (eventType == 3 ? "公告" : "资讯");
            KuroLogger.Loading("OFFICIAL_EVENTS", string.Format("Loading official events ({0}): GameId={1}, EventType={2} (Page={3}, PageSize={4})", typeName, gameId, eventType, pageNo, pageSize));
            var result = new List<PostItem>();

            var parameters = new Dictionary<string, string>
            {
                { "eventType", eventType.ToString() },
                { "gameId", gameId.ToString() },
                { "pageNo", pageNo.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/companyEvent/findEventList", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("list") && dataObj.GetNamedValue("list").ValueType == JsonValueType.Array)
                {
                    var listArray = dataObj.GetNamedArray("list");
                    foreach (var itemVal in listArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var obj = itemVal.GetObject();
                        var post = ParseOfficialEventItem(obj, gameId);
                        if (post != null)
                        {
                            result.Add(post);
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<PostItem>> GetNewsListAsync(int gameId, int pageIndex = 1, int pageSize = 10)
        {
            return await GetOfficialEventsAsync(gameId, 1, pageIndex, pageSize);
        }

        public async Task<PostItem> GetPostDetailAsync(string postId, int gameId)
        {
            if (string.IsNullOrEmpty(postId)) return null;

            var parameters = new Dictionary<string, string>
            {
                { "postId", postId },
                { "reqSource", "1" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/getPostDetail", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                var postObj = (dataObj.ContainsKey("postDetail") && dataObj.GetNamedValue("postDetail").ValueType == JsonValueType.Object)
                    ? dataObj.GetNamedObject("postDetail") 
                    : dataObj;
                var post = ParsePostDetailItem(postObj, gameId);
                if (post != null)
                {
                    if (string.IsNullOrEmpty(post.PostId))
                    {
                        post.PostId = postId;
                    }
                    if (dataObj.ContainsKey("isFollow"))
                    {
                        post.IsFollow = GetBoolean(dataObj, "isFollow", post.IsFollow);
                        if (post.Author != null) post.Author.IsFollow = post.IsFollow;
                    }
                    if (dataObj.ContainsKey("isLike"))
                    {
                        post.IsLiked = GetBoolean(dataObj, "isLike", post.IsLiked);
                    }
                    if (dataObj.ContainsKey("isCollect"))
                    {
                        post.IsCollected = GetBoolean(dataObj, "isCollect", post.IsCollected) || GetNumber(dataObj, "isCollect", 0) == 1;
                    }
                    return post;
                }
            }

            return null;
        }

        public async Task<List<PostCommentItem>> GetCommentsAsync(string postId, int gameId, int pageIndex = 1, int pageSize = 20, int sortType = 1)
        {
            if (string.IsNullOrEmpty(postId))
            {
                KuroLogger.Warn("COMMENTS_FEED_SKIP", "Cannot load comments: PostId is empty");
                return new List<PostCommentItem>();
            }

            KuroLogger.Loading("COMMENTS_FEED", string.Format("Loading comments: PostId={0}, GameId={1}, Page={2}", postId, gameId, pageIndex));
            var result = new List<PostCommentItem>();
            var seenIds = new HashSet<string>();

            var parameters = new Dictionary<string, string>
            {
                { "postId", postId },
                { "gameId", gameId.ToString() },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "sortType", sortType.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/comment/getPostCommentListV2", parameters);
            if (json != null && json.ContainsKey("data"))
            {
                var dataVal = json.GetNamedValue("data");
                if (dataVal.ValueType == JsonValueType.Array)
                {
                    ParseCommentArray(dataVal.GetArray(), postId, result, seenIds);
                }
                else if (dataVal.ValueType == JsonValueType.Object)
                {
                    var dataObj = dataVal.GetObject();

                    // 1. Stick Comments (置顶)
                    if (dataObj.ContainsKey("stickComments") && dataObj.GetNamedValue("stickComments").ValueType == JsonValueType.Array)
                    {
                        ParseCommentArray(dataObj.GetNamedArray("stickComments"), postId, result, seenIds, gameId);
                    }

                    // 2. Hot Comments (热评)
                    if (dataObj.ContainsKey("hotComments") && dataObj.GetNamedValue("hotComments").ValueType == JsonValueType.Array)
                    {
                        ParseCommentArray(dataObj.GetNamedArray("hotComments"), postId, result, seenIds, gameId);
                    }

                    // 3. Regular Comments
                    string[] possibleKeys = new string[] { "postCommentList", "commentList", "comments", "rootComments", "list", "records", "commentsList", "replyVos" };
                    foreach (var key in possibleKeys)
                    {
                        if (dataObj.ContainsKey(key) && dataObj.GetNamedValue(key).ValueType == JsonValueType.Array)
                        {
                            ParseCommentArray(dataObj.GetNamedArray(key), postId, result, seenIds, gameId);
                        }
                    }
                }
            }

            KuroLogger.Loading("COMMENTS_RENDER", string.Format("Rendered {0} comments for PostId={1}", result.Count, postId));
            return result;
        }

        private void ParseCommentArray(JsonArray array, string postId, List<PostCommentItem> result, HashSet<string> seenIds, int gameId = 2, int forumId = 4)
        {
            if (array == null) return;
            foreach (var itemVal in array)
            {
                if (itemVal.ValueType != JsonValueType.Object) continue;
                var obj = itemVal.GetObject();
                var comment = ParseCommentItem(obj, postId, gameId, forumId);
                if (comment != null)
                {
                    string key = !string.IsNullOrEmpty(comment.CommentId) ? comment.CommentId : (comment.UserName + comment.Content);
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (seenIds.Contains(key)) continue;
                        seenIds.Add(key);
                    }
                    result.Add(comment);
                }
            }
        }

        public async Task<bool> LikePostAsync(string postId, string toUserId, int forumId, int gameId, int postType, bool isLikedNow)
        {
            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId > 0 ? gameId.ToString() : "2" },
                { "forumId", forumId > 0 ? forumId.ToString() : "3" },
                { "postType", postType > 0 ? postType.ToString() : "1" },
                { "likeType", "1" },
                { "postId", postId ?? "" },
                { "operateType", isLikedNow ? "2" : "1" },
                { "toUserId", toUserId ?? "" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/like", parameters);
            return json != null && json.ContainsKey("code") && json.GetNamedNumber("code") == 200;
        }

        public async Task<bool> LikeCommentAsync(string postId, string commentId, string replyId, string toUserId, int gameId, int forumId, bool isLikedNow)
        {
            var parameters = new Dictionary<string, string>
            {
                { "postId", postId ?? "" },
                { "postCommentId", commentId ?? "0" },
                { "postCommentReplyId", replyId ?? "0" },
                { "toUserId", toUserId ?? "" },
                { "gameId", gameId > 0 ? gameId.ToString() : "2" },
                { "forumId", forumId > 0 ? forumId.ToString() : "4" },
                { "likeType", "2" },
                { "postType", "1" },
                { "operateType", isLikedNow ? "2" : "1" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/like", parameters);
            return json != null && json.ContainsKey("code") && json.GetNamedNumber("code") == 200;
        }

        public async Task<bool> CollectPostAsync(string postId, string toUserId, bool isCollectedNow)
        {
            if (string.IsNullOrEmpty(postId)) return false;

            var parameters = new Dictionary<string, string>
            {
                { "postId", postId },
                { "toUserId", toUserId ?? "" },
                { "operateType", isCollectedNow ? "2" : "1" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/collect", parameters);
            return json != null && json.ContainsKey("code") && json.GetNamedNumber("code") == 200;
        }

        public async Task<bool> CreateCommentAsync(string postId, int gameId, string content, string parentCommentId = "0")
        {
            var parameters = new Dictionary<string, string>
            {
                { "postId", postId },
                { "gameId", gameId.ToString() },
                { "content", content },
                { "parentCommentId", parentCommentId }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/comment/createCommentV2", parameters);
            return json != null && json.ContainsKey("code") && json.GetNamedNumber("code") == 200;
        }

        public PostItem ParseCommunityPostItem(JsonObject obj, int defaultGameId)
        {
            try
            {
                var post = new PostItem();
                post.PostId = GetString(obj, "postId", GetString(obj, "id", ""));
                post.GameId = (int)GetNumber(obj, "gameId", defaultGameId);
                post.GameName = post.GameId == 2 ? "战双帕弥什" : post.GameId == 3 ? "鸣潮" : GetString(obj, "gameName", "综合");
                post.Title = GetString(obj, "postTitle", GetString(obj, "title", ""));
                // Content summary & text extraction
                string rawPostContent = GetString(obj, "postContent", "");
                string cleanTextContent = "";
                var seenImgUrls = new HashSet<string>();

                if (!string.IsNullOrEmpty(rawPostContent))
                {
                    if (rawPostContent.TrimStart().StartsWith("["))
                    {
                        try
                        {
                            JsonArray contentArr;
                            if (JsonArray.TryParse(rawPostContent, out contentArr))
                            {
                                var sb = new System.Text.StringBuilder();
                                foreach (var blockVal in contentArr)
                                {
                                    if (blockVal.ValueType != JsonValueType.Object) continue;
                                    var bObj = blockVal.GetObject();
                                    int cType = (int)GetNumber(bObj, "contentType", 1);
                                    if (cType == 1) // text
                                    {
                                        if (bObj.ContainsKey("children") && bObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                                        {
                                            var children = bObj.GetNamedArray("children");
                                            foreach (var chVal in children)
                                            {
                                                if (chVal.ValueType != JsonValueType.Object) continue;
                                                var chObj = chVal.GetObject();
                                                string cText = GetString(chObj, "content", "");
                                                if (!string.IsNullOrEmpty(cText)) sb.Append(cText);
                                            }
                                        }
                                        else
                                        {
                                            string cText = GetString(bObj, "content", "");
                                            if (!string.IsNullOrEmpty(cText)) sb.Append(cText);
                                        }
                                        sb.AppendLine();
                                    }
                                    else if (cType == 2) // image in postContent
                                    {
                                        string imgUrl = GetString(bObj, "url", "");
                                        if (!string.IsNullOrEmpty(imgUrl) && !seenImgUrls.Contains(imgUrl))
                                        {
                                            seenImgUrls.Add(imgUrl);
                                            if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = imgUrl;
                                            post.ImageList.Add(new PostImage
                                            {
                                                Url = imgUrl,
                                                Width = (int)GetNumber(bObj, "imgWidth", 0),
                                                Height = (int)GetNumber(bObj, "imgHeight", 0)
                                            });
                                        }
                                    }
                                }
                                cleanTextContent = sb.ToString().Trim();
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(cleanTextContent))
                    {
                        cleanTextContent = KuroPostPublishHelper.StripHtml(rawPostContent);
                    }
                }

                post.FullContent = !string.IsNullOrEmpty(cleanTextContent) ? cleanTextContent : rawPostContent;
                post.ContentSummary = !string.IsNullOrEmpty(post.FullContent) ? post.FullContent : post.Title;
                if (string.IsNullOrEmpty(post.Title)) post.Title = post.ContentSummary;

                post.LikeCount = (int)GetNumber(obj, "likeCount", 0);
                post.CommentCount = (int)GetNumber(obj, "commentCount", 0);
                post.CollectCount = (int)GetNumber(obj, "collectCount", 0);
                post.IsLiked = GetBoolean(obj, "isLike", false);
                post.IsCollected = GetBoolean(obj, "isCollect", GetBoolean(obj, "isCollected", false)) || GetNumber(obj, "isCollect", 0) == 1;
                post.Author.IsFollow = GetBoolean(obj, "isFollow", false);
                post.IsFollow = post.Author.IsFollow;
                post.PostTimeStr = GetString(obj, "showTime", "刚刚");

                post.Author.UserId = GetString(obj, "userId", "");
                post.Author.UserName = GetString(obj, "userName", "漫游者");
                post.Author.AvatarUrl = GetString(obj, "userHeadUrl", GetString(obj, "headUrl", GetString(obj, "avatarUrl", "")));
                post.Author.IpRegion = GetString(obj, "ipRegion", "未知");

                if (obj.ContainsKey("user") && obj.GetNamedValue("user").ValueType == JsonValueType.Object)
                {
                    var uObj = obj.GetNamedObject("user");
                    post.Author.UserId = GetString(uObj, "userId", post.Author.UserId);
                    post.Author.UserName = GetString(uObj, "userName", post.Author.UserName);
                    var uHead = GetString(uObj, "headUrl", GetString(uObj, "userHeadUrl", GetString(uObj, "avatarUrl", "")));
                    if (!string.IsNullOrEmpty(uHead)) post.Author.AvatarUrl = uHead;
                    post.Author.IpRegion = GetString(uObj, "ipRegion", post.Author.IpRegion);
                }

                post.VideoId = GetString(obj, "videoId", "");
                if (obj.ContainsKey("videoInfo") && obj.GetNamedValue("videoInfo").ValueType == JsonValueType.Object)
                {
                    var vObj = obj.GetNamedObject("videoInfo");
                    post.VideoId = GetString(vObj, "videoId", post.VideoId);
                    post.VideoUrl = GetString(vObj, "videoUrl", GetString(vObj, "url", ""));
                    post.VideoCoverUrl = GetString(vObj, "coverUrl", "");
                    post.VideoDuration = GetNumber(vObj, "duration", 0);
                }

                // Parse coverImages
                if (obj.ContainsKey("coverImages") && obj.GetNamedValue("coverImages").ValueType == JsonValueType.Array)
                {
                    var covers = obj.GetNamedArray("coverImages");
                    foreach (var c in covers)
                    {
                        if (c.ValueType != JsonValueType.Object) continue;
                        var cObj = c.GetObject();
                        var u = GetString(cObj, "url", "");
                        if (!string.IsNullOrEmpty(u))
                        {
                            if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = u;
                            if (!seenImgUrls.Contains(u))
                            {
                                seenImgUrls.Add(u);
                                post.ImageList.Add(new PostImage { 
                                    Url = u,
                                    Width = (int)GetNumber(cObj, "imgWidth", 0),
                                    Height = (int)GetNumber(cObj, "imgHeight", 0)
                                });
                            }
                        }
                    }
                }

                // Parse imgContent if cover is not found or extra images exist
                if (obj.ContainsKey("imgContent") && obj.GetNamedValue("imgContent").ValueType == JsonValueType.Array)
                {
                    var imgs = obj.GetNamedArray("imgContent");
                    foreach (var img in imgs)
                    {
                        if (img.ValueType != JsonValueType.Object) continue;
                        var iObj = img.GetObject();
                        var u = GetString(iObj, "url", "");
                        if (!string.IsNullOrEmpty(u))
                        {
                            if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = u;
                            if (!seenImgUrls.Contains(u))
                            {
                                seenImgUrls.Add(u);
                                post.ImageList.Add(new PostImage { 
                                    Url = u,
                                    Width = (int)GetNumber(iObj, "imgWidth", 0),
                                    Height = (int)GetNumber(iObj, "imgHeight", 0)
                                });
                            }
                        }
                    }
                }

                // Parse topicList
                if (obj.ContainsKey("topicList") && obj.GetNamedValue("topicList").ValueType == JsonValueType.Array)
                {
                    var topics = obj.GetNamedArray("topicList");
                    foreach (var tVal in topics)
                    {
                        if (tVal.ValueType != JsonValueType.Object) continue;
                        var tObj = tVal.GetObject();
                        string tName = GetString(tObj, "topicName", "");
                        string tId = GetString(tObj, "topicId", "");
                        if (!string.IsNullOrEmpty(tName))
                        {
                            post.Topics.Add(new TopicItem
                            {
                                TopicId = tId,
                                TopicName = tName,
                                GameId = post.GameId
                            });
                        }
                    }
                }

                return post;
            }
            catch
            {
                return null;
            }
        }

        private PostItem ParseEventPostItem(JsonObject obj, int defaultGameId)
        {
            try
            {
                var post = new PostItem();
                post.PostId = GetString(obj, "postId", GetString(obj, "id", ""));
                post.GameId = (int)GetNumber(obj, "gameId", defaultGameId);
                post.GameName = post.GameId == 2 ? "战双帕弥什" : post.GameId == 3 ? "鸣潮" : "库街区官方";
                post.Title = GetString(obj, "postTitle", GetString(obj, "title", ""));
                post.CoverUrl = GetString(obj, "coverUrl", "");

                // Format timestamp
                double pubTime = GetNumber(obj, "publishTime", GetNumber(obj, "firstPublishTime", 0));
                if (pubTime > 0)
                {
                    try
                    {
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var dt = epoch.AddMilliseconds(pubTime).ToLocalTime();
                        post.PostTimeStr = dt.ToString("yyyy-MM-dd HH:mm");
                    }
                    catch
                    {
                        post.PostTimeStr = "官方发布";
                    }
                }
                else
                {
                    post.PostTimeStr = "官方活动";
                }

                post.ContentSummary = post.Title;
                post.Author.UserName = post.GameName + "官方";
                post.Author.IpRegion = "官方发布";

                if (!string.IsNullOrEmpty(post.CoverUrl))
                {
                    post.ImageList.Add(new PostImage { Url = post.CoverUrl });
                }

                return post;
            }
            catch
            {
                return null;
            }
        }

        private PostItem ParsePostDetailItem(JsonObject obj, int defaultGameId)
        {
            try
            {
                var post = new PostItem();
                post.PostId = GetString(obj, "postId", GetString(obj, "id", GetString(obj, "gamePostId", "")));
                post.GameId = (int)GetNumber(obj, "gameId", defaultGameId);
                post.ForumId = (int)GetNumber(obj, "gameForumId", GetNumber(obj, "forumId", 3));
                post.PostType = (int)GetNumber(obj, "postType", 1);
                post.GameName = post.GameId == 2 ? "战双帕弥什" : post.GameId == 3 ? "鸣潮" : "库街区";
                post.Title = GetString(obj, "postTitle", GetString(obj, "title", ""));

                // Content parsing: prioritize rich HTML (postH5Content / postNewH5Content) for styled colored text, headings & emojis
                string content = "";
                var seenImgUrls = new HashSet<string>();

                string h5Content = GetString(obj, "postH5Content", GetString(obj, "postNewH5Content", ""));
                if (!string.IsNullOrWhiteSpace(h5Content))
                {
                    var parsedBlocks = KuroHtmlPostParser.ParseHtml(h5Content);
                    if (parsedBlocks != null && parsedBlocks.Count > 0)
                    {
                        post.ContentBlocks = parsedBlocks;
                        var sb = new System.Text.StringBuilder();
                        foreach (var b in parsedBlocks)
                        {
                            if (b.IsImage || b.IsBanner)
                            {
                                if (!string.IsNullOrEmpty(b.ImageUrl) && !seenImgUrls.Contains(b.ImageUrl))
                                {
                                    seenImgUrls.Add(b.ImageUrl);
                                    if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = b.ImageUrl;
                                    post.ImageList.Add(new PostImage { Url = b.ImageUrl, Width = b.ImageWidth, Height = b.ImageHeight });
                                }
                            }
                            else if (b.Runs != null)
                            {
                                foreach (var r in b.Runs)
                                {
                                    if (!string.IsNullOrEmpty(r.Text)) sb.Append(r.Text);
                                }
                                sb.AppendLine();
                            }
                        }
                        content = sb.ToString().Trim();
                    }
                }

                // Fallback to structured postContent array if H5 parsing did not yield content blocks
                if (post.ContentBlocks.Count == 0 && obj.ContainsKey("postContent") && obj.GetNamedValue("postContent").ValueType == JsonValueType.Array)
                {
                    var pContents = obj.GetNamedArray("postContent");
                    var sb = new System.Text.StringBuilder();

                    foreach (var pItem in pContents)
                    {
                        if (pItem.ValueType != JsonValueType.Object) continue;
                        var pObj = pItem.GetObject();
                        int cType = (int)GetNumber(pObj, "contentType", 1);

                        if (cType == 1)
                        {
                            // Text block
                            var block = new PostContentBlock
                            {
                                BlockType = ContentBlockType.Text,
                                RawContent = GetString(pObj, "content", "")
                            };

                            if (pObj.ContainsKey("children") && pObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                            {
                                block.Runs = KuroEmojiService.Instance.ParseChildrenToRuns(pObj.GetNamedArray("children"));
                            }
                            else if (!string.IsNullOrEmpty(block.RawContent))
                            {
                                block.Runs = KuroEmojiService.Instance.ParseTextToRuns(block.RawContent);
                            }

                            if (!string.IsNullOrEmpty(block.RawContent))
                            {
                                sb.AppendLine(block.RawContent);
                            }

                            post.ContentBlocks.Add(block);
                        }
                        else if (cType == 2 || cType == 4)
                        {
                            // Image or Banner block
                            string imgUrl = GetString(pObj, "url", "");
                            int w = (int)GetNumber(pObj, "imgWidth", 0);
                            int h = (int)GetNumber(pObj, "imgHeight", 0);

                            if (!string.IsNullOrEmpty(imgUrl))
                            {
                                var block = new PostContentBlock
                                {
                                    BlockType = (cType == 4) ? ContentBlockType.Banner : ContentBlockType.Image,
                                    ImageUrl = imgUrl,
                                    ImageWidth = w,
                                    ImageHeight = h
                                };
                                post.ContentBlocks.Add(block);

                                if (!seenImgUrls.Contains(imgUrl))
                                {
                                    seenImgUrls.Add(imgUrl);
                                    if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = imgUrl;
                                    post.ImageList.Add(new PostImage { Url = imgUrl, Width = w, Height = h });
                                }
                            }
                        }
                        else if (cType == 3)
                        {
                            // Video block
                            if (string.IsNullOrEmpty(post.VideoId) && pObj.ContainsKey("videoId"))
                            {
                                post.VideoId = GetString(pObj, "videoId", "");
                            }
                            if (string.IsNullOrEmpty(post.VideoUrl) && pObj.ContainsKey("url"))
                            {
                                post.VideoUrl = GetString(pObj, "url", "");
                            }
                        }
                    }

                    content = sb.ToString().Trim();
                }
                else if (post.ContentBlocks.Count == 0 && obj.ContainsKey("postContent") && obj.GetNamedValue("postContent").ValueType == JsonValueType.String)
                {
                    content = obj.GetNamedString("postContent", "");
                    if (!string.IsNullOrEmpty(content))
                    {
                        var block = new PostContentBlock
                        {
                            BlockType = ContentBlockType.Text,
                            RawContent = content,
                            Runs = KuroEmojiService.Instance.ParseTextToRuns(content)
                        };
                        post.ContentBlocks.Add(block);
                    }
                }

                if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(h5Content))
                {
                    content = CleanHtml(h5Content);
                }

                post.FullContent = content;
                post.ContentSummary = !string.IsNullOrEmpty(post.FullContent) ? post.FullContent : post.Title;
                if (string.IsNullOrEmpty(post.Title)) post.Title = post.ContentSummary;

                post.PostTimeStr = GetString(obj, "showTime", GetString(obj, "postTime", "刚刚"));
                double pubTime = GetNumber(obj, "createTimestamp", GetNumber(obj, "publishTime", 0));
                if (pubTime > 0)
                {
                    try
                    {
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var dt = epoch.AddMilliseconds(pubTime).ToLocalTime();
                        post.PostTimeStr = dt.ToString("yyyy-MM-dd HH:mm");
                    }
                    catch { }
                }

                post.LikeCount = (int)GetNumber(obj, "likeCount", 0);
                post.CommentCount = (int)GetNumber(obj, "commentCount", 0);
                post.CollectCount = (int)GetNumber(obj, "collectCount", 0);
                post.IsLiked = GetBoolean(obj, "isLike", false);
                post.IsCollected = GetBoolean(obj, "isCollect", GetBoolean(obj, "isCollected", false)) || GetNumber(obj, "isCollect", 0) == 1;
                post.Author.IsFollow = GetBoolean(obj, "isFollow", false);
                post.IsFollow = post.Author.IsFollow;

                post.Author.UserId = GetString(obj, "postUserId", GetString(obj, "userId", ""));
                post.Author.UserName = GetString(obj, "userName", "漫游者");
                post.Author.AvatarUrl = GetString(obj, "headUrl", GetString(obj, "userHeadUrl", GetString(obj, "avatarUrl", "")));
                post.Author.IpRegion = GetString(obj, "ipRegion", "未知");

                if (obj.ContainsKey("user") && obj.GetNamedValue("user").ValueType == JsonValueType.Object)
                {
                    var uObj = obj.GetNamedObject("user");
                    post.Author.UserId = GetString(uObj, "userId", post.Author.UserId);
                    post.Author.UserName = GetString(uObj, "userName", post.Author.UserName);
                    var uHead = GetString(uObj, "headUrl", GetString(uObj, "userHeadUrl", GetString(uObj, "avatarUrl", "")));
                    if (!string.IsNullOrEmpty(uHead))
                    {
                        post.Author.AvatarUrl = uHead;
                    }
                    post.Author.IpRegion = GetString(uObj, "ipRegion", post.Author.IpRegion);
                }

                post.VideoId = GetString(obj, "videoId", post.VideoId);
                if (obj.ContainsKey("videoInfo") && obj.GetNamedValue("videoInfo").ValueType == JsonValueType.Object)
                {
                    var vObj = obj.GetNamedObject("videoInfo");
                    post.VideoId = GetString(vObj, "videoId", post.VideoId);
                    post.VideoUrl = GetString(vObj, "videoUrl", GetString(vObj, "url", ""));
                    post.VideoCoverUrl = GetString(vObj, "coverUrl", "");
                    post.VideoDuration = GetNumber(vObj, "duration", 0);
                }

                // Additional image sources (coverImages, imgContent, coverUrl)
                if (obj.ContainsKey("coverImages") && obj.GetNamedValue("coverImages").ValueType == JsonValueType.Array)
                {
                    var covers = obj.GetNamedArray("coverImages");
                    foreach (var c in covers)
                    {
                        if (c.ValueType != JsonValueType.Object) continue;
                        var u = GetString(c.GetObject(), "url", GetString(c.GetObject(), "sourceUrl", ""));
                        if (!string.IsNullOrEmpty(u) && !seenImgUrls.Contains(u))
                        {
                            seenImgUrls.Add(u);
                            if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = u;
                            post.ImageList.Add(new PostImage { Url = u });
                        }
                    }
                }
                
                if (obj.ContainsKey("imgContent") && obj.GetNamedValue("imgContent").ValueType == JsonValueType.Array)
                {
                    var imgs = obj.GetNamedArray("imgContent");
                    foreach (var img in imgs)
                    {
                        if (img.ValueType != JsonValueType.Object) continue;
                        var u = GetString(img.GetObject(), "url", "");
                        if (!string.IsNullOrEmpty(u) && !seenImgUrls.Contains(u))
                        {
                            seenImgUrls.Add(u);
                            if (string.IsNullOrEmpty(post.CoverUrl)) post.CoverUrl = u;
                            post.ImageList.Add(new PostImage { Url = u });
                        }
                    }
                }

                if (obj.ContainsKey("coverUrl"))
                {
                    var cUrl = GetString(obj, "coverUrl", "");
                    if (!string.IsNullOrEmpty(cUrl) && !seenImgUrls.Contains(cUrl))
                    {
                        seenImgUrls.Add(cUrl);
                        post.CoverUrl = cUrl;
                        post.ImageList.Add(new PostImage { Url = cUrl });
                    }
                }

                // Fallback: If no structured content blocks were generated, synthesize from FullContent and ImageList
                if (post.ContentBlocks.Count == 0)
                {
                    if (!string.IsNullOrEmpty(post.FullContent))
                    {
                        post.ContentBlocks.Add(new PostContentBlock
                        {
                            BlockType = ContentBlockType.Text,
                            RawContent = post.FullContent,
                            Runs = KuroEmojiService.Instance.ParseTextToRuns(post.FullContent)
                        });
                    }

                    foreach (var img in post.ImageList)
                    {
                        post.ContentBlocks.Add(new PostContentBlock
                        {
                            BlockType = ContentBlockType.Image,
                            ImageUrl = img.Url,
                            ImageWidth = img.Width,
                            ImageHeight = img.Height
                        });
                    }
                }

                // Parse Topics / Tags
                var topicList = new List<TopicItem>();
                var seenTopicNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (obj.ContainsKey("topicList") && obj.GetNamedValue("topicList").ValueType == JsonValueType.Array)
                {
                    var tArray = obj.GetNamedArray("topicList");
                    foreach (var tVal in tArray)
                    {
                        if (tVal.ValueType != JsonValueType.Object) continue;
                        var tObj = tVal.GetObject();
                        var topic = new TopicItem
                        {
                            TopicId = GetString(tObj, "topicId", GetString(tObj, "id", "")),
                            TopicName = GetString(tObj, "topicName", GetString(tObj, "name", "")).Trim().TrimStart('#').TrimEnd('#'),
                            TopicIcon = GetString(tObj, "topicIcon", GetString(tObj, "icon", "")),
                            GameId = (int)GetNumber(tObj, "gameId", post.GameId)
                        };
                        if (!string.IsNullOrEmpty(topic.TopicName) && !seenTopicNames.Contains(topic.TopicName))
                        {
                            seenTopicNames.Add(topic.TopicName);
                            topicList.Add(topic);
                        }
                    }
                }
                else if (obj.ContainsKey("topics") && obj.GetNamedValue("topics").ValueType == JsonValueType.Array)
                {
                    var tArray = obj.GetNamedArray("topics");
                    foreach (var tVal in tArray)
                    {
                        if (tVal.ValueType != JsonValueType.Object) continue;
                        var tObj = tVal.GetObject();
                        var topic = new TopicItem
                        {
                            TopicId = GetString(tObj, "topicId", GetString(tObj, "id", "")),
                            TopicName = GetString(tObj, "topicName", GetString(tObj, "name", "")).Trim().TrimStart('#').TrimEnd('#'),
                            TopicIcon = GetString(tObj, "topicIcon", GetString(tObj, "icon", "")),
                            GameId = (int)GetNumber(tObj, "gameId", post.GameId)
                        };
                        if (!string.IsNullOrEmpty(topic.TopicName) && !seenTopicNames.Contains(topic.TopicName))
                        {
                            seenTopicNames.Add(topic.TopicName);
                            topicList.Add(topic);
                        }
                    }
                }
                else if (obj.ContainsKey("topicVo") && obj.GetNamedValue("topicVo").ValueType == JsonValueType.Object)
                {
                    var tObj = obj.GetNamedObject("topicVo");
                    var topic = new TopicItem
                    {
                        TopicId = GetString(tObj, "topicId", GetString(tObj, "id", "")),
                        TopicName = GetString(tObj, "topicName", GetString(tObj, "name", "")).Trim().TrimStart('#').TrimEnd('#'),
                        TopicIcon = GetString(tObj, "topicIcon", GetString(tObj, "icon", "")),
                        GameId = (int)GetNumber(tObj, "gameId", post.GameId)
                    };
                    if (!string.IsNullOrEmpty(topic.TopicName) && !seenTopicNames.Contains(topic.TopicName))
                    {
                        seenTopicNames.Add(topic.TopicName);
                        topicList.Add(topic);
                    }
                }

                // Fallback / Supplementary: extract hashtag patterns (#话题#) from FullContent
                if (!string.IsNullOrEmpty(post.FullContent))
                {
                    var hashMatches = System.Text.RegularExpressions.Regex.Matches(post.FullContent, @"#([^#\s\r\n]{2,30})#");
                    foreach (System.Text.RegularExpressions.Match hm in hashMatches)
                    {
                        string tName = hm.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(tName) && !seenTopicNames.Contains(tName))
                        {
                            seenTopicNames.Add(tName);
                            topicList.Add(new TopicItem
                            {
                                TopicId = "",
                                TopicName = tName,
                                GameId = post.GameId
                            });
                        }
                    }
                }

                post.Topics = topicList;

                return post;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从评论/回复的富文本块数组（commentContent / replyContent）中抽取图片块。
        /// 图片块的契约（已验证）：{ "contentType":2, "url":"...", "imgWidth":1920, "imgHeight":1080, "isAbnormal":false }
        /// contentType==1 为文字/表情块，不在此处理。
        /// </summary>
        private void AppendContentImages(JsonArray blocks, List<PostImage> target)
        {
            if (blocks == null || target == null) return;
            foreach (var bVal in blocks)
            {
                if (bVal.ValueType != JsonValueType.Object) continue;
                var bObj = bVal.GetObject();

                int ct = (int)GetNumber(bObj, "contentType", 1);
                string url = GetString(bObj, "url", "");
                if (ct != 2 || string.IsNullOrEmpty(url)) continue;

                bool isAbnormal = GetBoolean(bObj, "isAbnormal", false);
                if (isAbnormal) continue; // 被平台判定为异常的图片不渲染

                target.Add(new PostImage
                {
                    Url = url,
                    Width = (int)GetNumber(bObj, "imgWidth", 0),
                    Height = (int)GetNumber(bObj, "imgHeight", 0)
                });
            }
        }

        private PostCommentItem ParseCommentItem(JsonObject obj, string postId, int gameId = 2, int forumId = 4)
        {
            try
            {
                var comment = new PostCommentItem();
                comment.CommentId = GetString(obj, "commentId", GetString(obj, "id", GetString(obj, "replyId", "")));
                comment.PostId = postId;
                comment.GameId = (int)GetNumber(obj, "gameId", gameId);
                comment.ForumId = (int)GetNumber(obj, "gameForumId", (int)GetNumber(obj, "forumId", forumId));

                // Robust content parsing (handles string or array of blocks)
                string content = "";
                if (obj.ContainsKey("commentContent"))
                {
                    var cVal = obj.GetNamedValue("commentContent");
                    if (cVal.ValueType == JsonValueType.String)
                    {
                        content = cVal.GetString();
                        comment.ContentRuns = KuroEmojiService.Instance.ParseTextToRuns(content);
                    }
                    else if (cVal.ValueType == JsonValueType.Array)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var blockVal in cVal.GetArray())
                        {
                            if (blockVal.ValueType == JsonValueType.Object)
                            {
                                var bObj = blockVal.GetObject();
                                if (bObj.ContainsKey("children") && bObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                                {
                                    var runs = KuroEmojiService.Instance.ParseChildrenToRuns(bObj.GetNamedArray("children"));
                                    comment.ContentRuns.AddRange(runs);
                                    foreach (var r in runs)
                                    {
                                        if (!string.IsNullOrEmpty(r.Text)) sb.Append(r.Text);
                                    }
                                }
                                else if (bObj.ContainsKey("content"))
                                {
                                    var cStr = GetString(bObj, "content", "");
                                    if (!string.IsNullOrEmpty(cStr))
                                    {
                                        var runs = KuroEmojiService.Instance.ParseTextToRuns(cStr);
                                        comment.ContentRuns.AddRange(runs);
                                        sb.Append(cStr);
                                    }
                                }
                            }
                            else if (blockVal.ValueType == JsonValueType.String)
                            {
                                var str = blockVal.GetString();
                                var runs = KuroEmojiService.Instance.ParseTextToRuns(str);
                                comment.ContentRuns.AddRange(runs);
                                sb.Append(str);
                            }
                        }
                        content = sb.ToString().Trim();
                        AppendContentImages(cVal.GetArray(), comment.ImageList);
                    }
                }

                if (string.IsNullOrEmpty(content) && obj.ContainsKey("replyContent"))
                {
                    var cVal = obj.GetNamedValue("replyContent");
                    if (cVal.ValueType == JsonValueType.String)
                    {
                        content = cVal.GetString();
                        comment.ContentRuns = KuroEmojiService.Instance.ParseTextToRuns(content);
                    }
                    else if (cVal.ValueType == JsonValueType.Array)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var blockVal in cVal.GetArray())
                        {
                            if (blockVal.ValueType == JsonValueType.Object)
                            {
                                var bObj = blockVal.GetObject();
                                if (bObj.ContainsKey("children") && bObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                                {
                                    var runs = KuroEmojiService.Instance.ParseChildrenToRuns(bObj.GetNamedArray("children"));
                                    comment.ContentRuns.AddRange(runs);
                                    foreach (var r in runs)
                                    {
                                        if (!string.IsNullOrEmpty(r.Text)) sb.Append(r.Text);
                                    }
                                }
                                else if (bObj.ContainsKey("content"))
                                {
                                    var cStr = GetString(bObj, "content", "");
                                    if (!string.IsNullOrEmpty(cStr))
                                    {
                                        var runs = KuroEmojiService.Instance.ParseTextToRuns(cStr);
                                        comment.ContentRuns.AddRange(runs);
                                        sb.Append(cStr);
                                    }
                                }
                            }
                            else if (blockVal.ValueType == JsonValueType.String)
                            {
                                var str = blockVal.GetString();
                                var runs = KuroEmojiService.Instance.ParseTextToRuns(str);
                                comment.ContentRuns.AddRange(runs);
                                sb.Append(str);
                            }
                        }
                        content = sb.ToString().Trim();
                        AppendContentImages(cVal.GetArray(), comment.ImageList);
                    }
                }

                if (string.IsNullOrEmpty(content))
                {
                    content = GetString(obj, "replyContentStr", 
                              GetString(obj, "content", 
                              GetString(obj, "postContent", 
                              GetString(obj, "text", ""))));
                }

                // If content contains html tags, clean it
                if (!string.IsNullOrEmpty(content) && content.Contains("<"))
                {
                    content = CleanHtml(content);
                }

                comment.Content = content;

                // Robust time formatting
                double createTs = GetNumber(obj, "createTimestamp", GetNumber(obj, "createTime", GetNumber(obj, "publishTime", 0)));
                if (createTs > 1000000000)
                {
                    try
                    {
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        var dt = epoch.AddMilliseconds(createTs).ToLocalTime();
                        comment.CreateTimeStr = dt.ToString("MM-dd HH:mm");
                    }
                    catch
                    {
                        comment.CreateTimeStr = GetString(obj, "commentTime", GetString(obj, "replyTime", GetString(obj, "showTime", "刚刚")));
                    }
                }
                else
                {
                    comment.CreateTimeStr = GetString(obj, "commentTime", GetString(obj, "replyTime", GetString(obj, "showTime", GetString(obj, "createTimeStr", "刚刚"))));
                }

                comment.Floor = (int)GetNumber(obj, "floor", 0);
                comment.LikeCount = (int)GetNumber(obj, "likeCount", GetNumber(obj, "praiseCount", 0));
                comment.IsLiked = GetBoolean(obj, "isLike", GetBoolean(obj, "isLiked", false));
                comment.ReplyCount = (int)GetNumber(obj, "replyCount", GetNumber(obj, "commentCount", 0));

                // Nested Replies / 楼中楼 (replyVos)
                if (obj.ContainsKey("replyVos") && obj.GetNamedValue("replyVos").ValueType == JsonValueType.Array)
                {
                    var replyArray = obj.GetNamedArray("replyVos");
                    foreach (var rVal in replyArray)
                    {
                        if (rVal.ValueType != JsonValueType.Object) continue;
                        var rObj = rVal.GetObject();

                        var reply = new PostReplyItem();
                        reply.ReplyId = GetString(rObj, "replyId", GetString(rObj, "id", ""));
                        reply.PostCommentId = GetString(rObj, "postCommentId", comment.CommentId);
                        reply.PostId = postId;
                        reply.GameId = comment.GameId;
                        reply.ForumId = comment.ForumId;
                        reply.UserId = GetString(rObj, "userId", "");
                        reply.UserName = GetString(rObj, "userName", "漫游者");
                        reply.AvatarUrl = GetString(rObj, "userHeadUrl", GetString(rObj, "headUrl", GetString(rObj, "avatarUrl", "")));
                        reply.ToUserId = GetString(rObj, "toUserId", "");
                        reply.ToUserName = GetString(rObj, "toUserName", "");
                        reply.IpRegion = GetString(rObj, "ipRegion", "");
                        reply.LikeCount = (int)GetNumber(rObj, "likeCount", 0);
                        reply.IsLiked = GetBoolean(rObj, "isLike", GetBoolean(rObj, "isLiked", false));
                        reply.ReplyTimeStr = GetString(rObj, "replyTime", GetString(rObj, "showTime", ""));

                        string rContent = "";
                        if (rObj.ContainsKey("replyContent") && rObj.GetNamedValue("replyContent").ValueType == JsonValueType.Array)
                        {
                            var rBlocks = rObj.GetNamedArray("replyContent");
                            var sb = new System.Text.StringBuilder();
                            foreach (var bVal in rBlocks)
                            {
                                if (bVal.ValueType != JsonValueType.Object) continue;
                                var bObj = bVal.GetObject();
                                if (bObj.ContainsKey("children") && bObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                                {
                                    var runs = KuroEmojiService.Instance.ParseChildrenToRuns(bObj.GetNamedArray("children"));
                                    reply.ContentRuns.AddRange(runs);
                                    foreach (var r in runs)
                                    {
                                        if (!string.IsNullOrEmpty(r.Text)) sb.Append(r.Text);
                                    }
                                }
                                else if (bObj.ContainsKey("content"))
                                {
                                    var cStr = GetString(bObj, "content", "");
                                    if (!string.IsNullOrEmpty(cStr))
                                    {
                                        var runs = KuroEmojiService.Instance.ParseTextToRuns(cStr);
                                        reply.ContentRuns.AddRange(runs);
                                        sb.Append(cStr);
                                    }
                                }
                            }
                            rContent = sb.ToString().Trim();
                            AppendContentImages(rBlocks, reply.ImageList);
                        }

                        if (string.IsNullOrEmpty(rContent) && rObj.ContainsKey("replyContentStr"))
                        {
                            rContent = GetString(rObj, "replyContentStr", "");
                            if (reply.ContentRuns.Count == 0 && !string.IsNullOrEmpty(rContent))
                            {
                                reply.ContentRuns.AddRange(KuroEmojiService.Instance.ParseTextToRuns(rContent));
                            }
                        }

                        reply.ReplyText = rContent;
                        comment.Replies.Add(reply);
                    }
                }

                // Robust User Info Parsing
                comment.UserName = GetString(obj, "userName", GetString(obj, "toUserName", GetString(obj, "nickname", GetString(obj, "nickName", "漫游者"))));
                comment.UserId = GetString(obj, "userId", GetString(obj, "toUserId", GetString(obj, "commentUserId", "")));
                comment.AvatarUrl = GetString(obj, "userHeadUrl", GetString(obj, "headUrl", GetString(obj, "avatarUrl", "")));
                comment.IpRegion = GetString(obj, "ipRegion", GetString(obj, "ipLocation", "未知"));

                if (obj.ContainsKey("user") && obj.GetNamedValue("user").ValueType == JsonValueType.Object)
                {
                    var userObj = obj.GetNamedObject("user");
                    comment.UserId = GetString(userObj, "userId", comment.UserId);
                    comment.UserName = GetString(userObj, "userName", GetString(userObj, "nickname", comment.UserName));
                    var uHead = GetString(userObj, "headUrl", GetString(userObj, "userHeadUrl", GetString(userObj, "avatarUrl", "")));
                    if (!string.IsNullOrEmpty(uHead))
                    {
                        comment.AvatarUrl = uHead;
                    }
                    comment.IpRegion = GetString(userObj, "ipRegion", comment.IpRegion);
                }

                return comment;
            }
            catch
            {
                return null;
            }
        }

        private PostItem ParseOfficialEventItem(JsonObject obj, int defaultGameId)
        {
            try
            {
                var post = new PostItem();
                post.PostId = GetString(obj, "postId", GetString(obj, "id", ""));
                post.GameId = (int)GetNumber(obj, "gameId", defaultGameId);
                post.GameName = post.GameId == 2 ? "战双帕弥什" : (post.GameId == 3 ? "鸣潮" : "官方");
                post.Title = GetString(obj, "postTitle", GetString(obj, "title", ""));
                post.CoverUrl = GetString(obj, "coverUrl", "");
                if (!string.IsNullOrEmpty(post.CoverUrl))
                {
                    post.ImageList.Add(new PostImage { Url = post.CoverUrl });
                }

                int evType = (int)GetNumber(obj, "eventType", 1);
                string tag = evType == 1 ? "【活动】" : (evType == 3 ? "【公告】" : "【资讯】");
                post.ContentSummary = tag + " " + post.Title;

                double ts = GetNumber(obj, "publishTime", GetNumber(obj, "firstPublishTime", 0));
                if (ts > 0)
                {
                    try
                    {
                        var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ts).ToLocalTime();
                        post.PostTimeStr = dt.ToString("MM-dd HH:mm");
                    }
                    catch
                    {
                        post.PostTimeStr = "近期发布";
                    }
                }
                else
                {
                    post.PostTimeStr = "官方发布";
                }

                post.Author.UserId = "0";
                post.Author.UserName = post.GameName + "官方";
                post.Author.AvatarUrl = "https://prod-alicdn-community.kurobbs.com/game/zhanshuangIcon.png";
                post.Author.IpRegion = tag;
                post.Author.IsOfficial = true;

                return post;
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("PARSE_EVENT_ERR", "Error parsing official event item: " + ex.Message);
                return null;
            }
        }

        private string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
                .Replace("&nbsp;", " ")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&amp;", "&")
                .Trim();
        }

        private string GetString(JsonObject obj, string key, string defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String)
                {
                    return val.GetString();
                }
                else if (val.ValueType == JsonValueType.Number)
                {
                    double num = val.GetNumber();
                    if (num % 1 == 0 && num >= long.MinValue && num <= long.MaxValue)
                    {
                        return ((long)num).ToString();
                    }
                    return num.ToString();
                }
                else if (val.ValueType == JsonValueType.Boolean)
                {
                    return val.GetBoolean().ToString().ToLowerInvariant();
                }
            }
            return defVal;
        }

        private double GetNumber(JsonObject obj, string key, double defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Number)
                {
                    return val.GetNumber();
                }
                else if (val.ValueType == JsonValueType.String)
                {
                    double res;
                    if (double.TryParse(val.GetString(), out res))
                    {
                        return res;
                    }
                }
                else if (val.ValueType == JsonValueType.Boolean)
                {
                    return val.GetBoolean() ? 1 : 0;
                }
            }
            return defVal;
        }

        private bool GetBoolean(JsonObject obj, string key, bool defVal)
        {
            if (obj != null && obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Boolean)
                {
                    return val.GetBoolean();
                }
                else if (val.ValueType == JsonValueType.Number)
                {
                    return val.GetNumber() != 0;
                }
                else if (val.ValueType == JsonValueType.String)
                {
                    string s = val.GetString();
                    if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") return true;
                    if (s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0") return false;
                }
            }
            return defVal;
        }

        public async Task<SearchConfigResult> GetSearchConfigAsync(int gameId)
        {
            var result = new SearchConfigResult();
            var json = await KuroApiClient.Instance.GetAsync("/config/search/getSearchConfig?gameId=" + gameId);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                result.DefaultWord = dataObj.ContainsKey("defaultWord") ? dataObj.GetNamedString("defaultWord") : "";

                if (dataObj.ContainsKey("defaultWordList") && dataObj.GetNamedValue("defaultWordList").ValueType == JsonValueType.Array)
                {
                    var arr = dataObj.GetNamedArray("defaultWordList");
                    foreach (var item in arr)
                    {
                        if (item.ValueType == JsonValueType.String)
                            result.DefaultWordList.Add(item.GetString());
                    }
                }

                if (dataObj.ContainsKey("searchList") && dataObj.GetNamedValue("searchList").ValueType == JsonValueType.Array)
                {
                    var arr = dataObj.GetNamedArray("searchList");
                    foreach (var item in arr)
                    {
                        if (item.ValueType == JsonValueType.Object)
                        {
                            var o = item.GetObject();
                            result.SearchList.Add(new HotSearchItem
                            {
                                GameId = o.ContainsKey("gameId") ? (int)o.GetNamedNumber("gameId") : gameId,
                                KeyWord = o.ContainsKey("keyWord") ? o.GetNamedString("keyWord") : "",
                                WordId = o.ContainsKey("wordId") ? o.GetNamedString("wordId") : "",
                                OrderSeq = o.ContainsKey("orderSeq") ? (int)o.GetNamedNumber("orderSeq") : 0,
                                LinkTarget = o.ContainsKey("linkTarget") ? o.GetNamedString("linkTarget") : "",
                                LinkType = o.ContainsKey("linkType") ? (int)o.GetNamedNumber("linkType") : 0
                            });
                        }
                    }
                }
            }
            return result;
        }

        public async Task<List<TopicItem>> GetTopicHotListAsync(int gameId, int type = 4, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<TopicItem>();
            var json = await KuroApiClient.Instance.GetAsync(string.Format("/forum/app/topic/hotlist?type={0}&pageIndex={1}&pageSize={2}&gameId={3}", type, pageIndex, pageSize, gameId));
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
            {
                var arr = json.GetNamedArray("data");
                int rank = (pageIndex - 1) * pageSize + 1;
                foreach (var item in arr)
                {
                    if (item.ValueType == JsonValueType.Object)
                    {
                        var o = item.GetObject();
                        list.Add(new TopicItem
                        {
                            TopicId = o.ContainsKey("topicId") ? o.GetNamedString("topicId") : "",
                            TopicName = o.ContainsKey("topicName") ? o.GetNamedString("topicName") : "",
                            TopicIcon = o.ContainsKey("topicIcon") ? o.GetNamedString("topicIcon") : "",
                            Remark = o.ContainsKey("remark") ? o.GetNamedString("remark") : "",
                            BrowseCnt = o.ContainsKey("browseCnt") ? o.GetNamedString("browseCnt") : "0",
                            DiscussCnt = o.ContainsKey("discussCnt") ? o.GetNamedString("discussCnt") : "0",
                            GameId = gameId,
                            TopRank = rank++
                        });
                    }
                }
            }
            return list;
        }

        public async Task<List<GameWikiItem>> GetGameWikiListAsync(int gameId)
        {
            var list = new List<GameWikiItem>();
            try
            {
                var json = await KuroApiClient.Instance.PostFormAsync("/config/getGameWiki", new Dictionary<string, string>());
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var data = json.GetNamedObject("data");
                    if (data.ContainsKey("gameWikiVoMap") && data.GetNamedValue("gameWikiVoMap").ValueType == JsonValueType.Object)
                    {
                        var voMap = data.GetNamedObject("gameWikiVoMap");
                        string key = gameId.ToString();
                        if (voMap.ContainsKey(key) && voMap.GetNamedValue(key).ValueType == JsonValueType.Array)
                        {
                            var arr = voMap.GetNamedArray(key);
                            foreach (var item in arr)
                            {
                                if (item.ValueType == JsonValueType.Object)
                                {
                                    var o = item.GetObject();
                                    list.Add(new GameWikiItem
                                    {
                                        Id = o.ContainsKey("id") ? (int)o.GetNamedNumber("id") : 0,
                                        WikiName = o.ContainsKey("wikiName") ? o.GetNamedString("wikiName") : "",
                                        WikiType = o.ContainsKey("wikiType") ? (int)o.GetNamedNumber("wikiType") : 2,
                                        IconUrl = o.ContainsKey("iconUrl") ? o.GetNamedString("iconUrl") : "",
                                        WebIconUrl = o.ContainsKey("webIconUrl") ? o.GetNamedString("webIconUrl") : "",
                                        Url = o.ContainsKey("url") ? o.GetNamedString("url") : "",
                                        CustomSchemeUrl = o.ContainsKey("customSchemeUrl") ? o.GetNamedString("customSchemeUrl") : "",
                                        PostId = o.ContainsKey("postId") ? o.GetNamedString("postId") : "",
                                        PostTitle = o.ContainsKey("postTitle") ? o.GetNamedString("postTitle") : "",
                                        ShowRedPoint = o.ContainsKey("showRedPoint") ? o.GetNamedBoolean("showRedPoint") : false,
                                        AppForce = o.ContainsKey("appForce") ? o.GetNamedBoolean("appForce") : false,
                                        IsNeedToken = o.ContainsKey("isNeedToken") ? (int)o.GetNamedNumber("isNeedToken") : 0,
                                        GameId = gameId
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("GET_GAME_WIKI_ERROR", "Failed to fetch game wiki list: " + ex.Message);
            }
            return list;
        }

        public async Task<CompositeSearchResult> SearchCompositeAsync(int gameId, string keyword, int searchSort = 1, int postType = 0, int searchTime = 0, int pageIndex = 1, int pageSize = 20)
        {
            var result = new CompositeSearchResult();
            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "keyword", keyword ?? "" },
                { "searchSort", searchSort > 0 ? searchSort.ToString() : "1" },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };
            if (postType > 0) parameters["postType"] = postType.ToString();
            if (searchTime > 0) parameters["searchTime"] = searchTime.ToString();

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/search/v2/join", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");

                // Parse posts
                if (dataObj.ContainsKey("post") && dataObj.GetNamedValue("post").ValueType == JsonValueType.Object)
                {
                    var postObj = dataObj.GetNamedObject("post");
                    result.HasNext = postObj.ContainsKey("hasNext") && postObj.GetNamedBoolean("hasNext");
                    if (postObj.ContainsKey("postList") && postObj.GetNamedValue("postList").ValueType == JsonValueType.Array)
                    {
                        var postListArray = postObj.GetNamedArray("postList");
                        foreach (var itemVal in postListArray)
                        {
                            if (itemVal.ValueType != JsonValueType.Object) continue;
                            var post = ParseCommunityPostItem(itemVal.GetObject(), gameId);
                            if (post != null) result.Posts.Add(post);
                        }
                    }
                }

                // Parse wiki
                if (dataObj.ContainsKey("wiki") && dataObj.GetNamedValue("wiki").ValueType == JsonValueType.Array)
                {
                    var wikiArray = dataObj.GetNamedArray("wiki");
                    foreach (var itemVal in wikiArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var o = itemVal.GetObject();
                        result.Wikis.Add(new WikiSearchItem
                        {
                            Id = o.ContainsKey("id") ? (long)o.GetNamedNumber("id") : 0,
                            Title = o.ContainsKey("title") ? o.GetNamedString("title") : "",
                            CoverImgUrl = o.ContainsKey("coverImgUrl") ? o.GetNamedString("coverImgUrl") : "",
                            Catalogue = o.ContainsKey("catalogue") ? o.GetNamedString("catalogue") : "",
                            LinkUrl = o.ContainsKey("linkUrl") ? o.GetNamedString("linkUrl") : ""
                        });
                    }
                }
            }
            return result;
        }

        public async Task<List<PostItem>> SearchPostsAsync(int gameId, string keyword, int forumType, int searchSort = 1, int postType = 0, int searchTime = 0, int pageIndex = 1, int pageSize = 20)
        {
            var result = new List<PostItem>();
            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "keyword", keyword ?? "" },
                { "forumType", forumType.ToString() },
                { "searchSort", searchSort > 0 ? searchSort.ToString() : "1" },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };
            if (postType > 0) parameters["postType"] = postType.ToString();
            if (searchTime > 0) parameters["searchTime"] = searchTime.ToString();

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/search/v2/post", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("postList") && dataObj.GetNamedValue("postList").ValueType == JsonValueType.Array)
                {
                    var postListArray = dataObj.GetNamedArray("postList");
                    foreach (var itemVal in postListArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var post = ParseCommunityPostItem(itemVal.GetObject(), gameId);
                        if (post != null) result.Add(post);
                    }
                }
            }
            return result;
        }

        public async Task<List<WikiSearchItem>> SearchWikiAsync(int gameId, string keyword, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<WikiSearchItem>();
            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "keyword", keyword ?? "" },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/catalogue/item/searchWiki", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
            {
                var wikiArray = json.GetNamedArray("data");
                foreach (var itemVal in wikiArray)
                {
                    if (itemVal.ValueType != JsonValueType.Object) continue;
                    var o = itemVal.GetObject();
                    list.Add(new WikiSearchItem
                    {
                        Id = o.ContainsKey("id") ? (long)o.GetNamedNumber("id") : 0,
                        Title = o.ContainsKey("title") ? o.GetNamedString("title") : "",
                        CoverImgUrl = o.ContainsKey("coverImgUrl") ? o.GetNamedString("coverImgUrl") : "",
                        Catalogue = o.ContainsKey("catalogue") ? o.GetNamedString("catalogue") : "",
                        LinkUrl = o.ContainsKey("linkUrl") ? o.GetNamedString("linkUrl") : ""
                    });
                }
            }
            return list;
        }

        public async Task<List<TopicItem>> SearchTopicsAsync(int gameId, string keyword, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<TopicItem>();
            string encodedKeyword = Uri.EscapeDataString(keyword ?? "");
            
            // Try /forum/app/topic/search first
            var json = await KuroApiClient.Instance.GetAsync(string.Format("/forum/app/topic/search?gameId={0}&keyword={1}&pageIndex={2}&pageSize={3}", 
                gameId, encodedKeyword, pageIndex, pageSize));

            // Fallback to /forum/app/topic/searchTopic if /search returned null/no data
            if (json == null || !json.ContainsKey("data") || json.GetNamedValue("data").ValueType != JsonValueType.Array || json.GetNamedArray("data").Count == 0)
            {
                var fallbackJson = await KuroApiClient.Instance.GetAsync(string.Format("/forum/app/topic/searchTopic?gameId={0}&keyword={1}&pageIndex={2}&pageSize={3}", 
                    gameId, encodedKeyword, pageIndex, pageSize));
                if (fallbackJson != null && fallbackJson.ContainsKey("data") && fallbackJson.GetNamedValue("data").ValueType == JsonValueType.Array)
                {
                    json = fallbackJson;
                }
            }

            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
            {
                var arr = json.GetNamedArray("data");
                foreach (var item in arr)
                {
                    if (item.ValueType == JsonValueType.Object)
                    {
                        var o = item.GetObject();
                        list.Add(new TopicItem
                        {
                            TopicId = o.ContainsKey("topicId") ? o.GetNamedString("topicId") : "",
                            TopicName = o.ContainsKey("topicName") ? o.GetNamedString("topicName") : "",
                            TopicIcon = o.ContainsKey("topicIcon") ? o.GetNamedString("topicIcon") : "",
                            Remark = o.ContainsKey("remark") ? o.GetNamedString("remark") : "",
                            BrowseCnt = o.ContainsKey("browseCnt") ? o.GetNamedString("browseCnt") : "0",
                            DiscussCnt = o.ContainsKey("discussCnt") ? o.GetNamedString("discussCnt") : "0",
                            GameId = gameId
                        });
                    }
                }
            }
            return list;
        }

        public async Task<List<UserSearchItem>> SearchUsersAsync(int gameId, string keyword, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<UserSearchItem>();
            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "keyword", keyword ?? "" },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/user/searchUser", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("userList") && dataObj.GetNamedValue("userList").ValueType == JsonValueType.Array)
                {
                    var userArray = dataObj.GetNamedArray("userList");
                    foreach (var itemVal in userArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var o = itemVal.GetObject();
                        list.Add(new UserSearchItem
                        {
                            UserId = o.ContainsKey("userId") ? o.GetNamedString("userId") : "",
                            UserName = o.ContainsKey("userName") ? o.GetNamedString("userName") : "",
                            HeadUrl = o.ContainsKey("headUrl") ? o.GetNamedString("headUrl") : "",
                            HeadFrameUrl = o.ContainsKey("headFrameUrl") ? o.GetNamedString("headFrameUrl") : "",
                            Signature = o.ContainsKey("signature") ? o.GetNamedString("signature") : "",
                            IsFollow = o.ContainsKey("isFollow") ? (int)o.GetNamedNumber("isFollow") : 0
                        });
                    }
                }
            }
            return list;
        }

        public async Task<TopicDetailResult> GetTopicDetailAsync(string topicId, int type = 1, int pageIndex = 1, int pageSize = 20)
        {
            var result = new TopicDetailResult { TopicId = topicId };
            var json = await KuroApiClient.Instance.GetAsync(string.Format("/forum/app/topic/detail?type={0}&loadFlag=true&pageIndex={1}&pageSize={2}&topicId={3}", 
                type, pageIndex, pageSize, topicId));
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                result.TopicId = dataObj.ContainsKey("topicId") ? dataObj.GetNamedString("topicId") : topicId;
                result.TopicName = dataObj.ContainsKey("topicName") ? dataObj.GetNamedString("topicName") : "";
                result.TopicIcon = dataObj.ContainsKey("topicIcon") ? dataObj.GetNamedString("topicIcon") : "";
                result.Remark = dataObj.ContainsKey("remark") ? dataObj.GetNamedString("remark") : "";
                result.BrowseCnt = dataObj.ContainsKey("browseCnt") ? dataObj.GetNamedString("browseCnt") : "0";
                result.DiscussCnt = dataObj.ContainsKey("discussCnt") ? dataObj.GetNamedString("discussCnt") : "0";
                result.GameId = dataObj.ContainsKey("gameId") ? (int)dataObj.GetNamedNumber("gameId") : 2;
                result.Status = dataObj.ContainsKey("status") && dataObj.GetNamedBoolean("status");
                result.HasNext = dataObj.ContainsKey("hasNext") && dataObj.GetNamedNumber("hasNext") == 1;

                if (dataObj.ContainsKey("posts") && dataObj.GetNamedValue("posts").ValueType == JsonValueType.Array)
                {
                    var postsArray = dataObj.GetNamedArray("posts");
                    foreach (var itemVal in postsArray)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var post = ParseCommunityPostItem(itemVal.GetObject(), result.GameId);
                        if (post != null) result.Posts.Add(post);
                    }
                }
            }
            return result;
        }

        public async Task<PublishPostResult> PublishPostAsync(string title, int gameId, int gameForumId, string rawContent, List<PostEditorImage> images, List<TopicItem> topics = null, string draftId = "")
        {
            var result = new PublishPostResult();

            if (string.IsNullOrWhiteSpace(title))
            {
                result.Message = "请输入帖子标题";
                return result;
            }

            if (string.IsNullOrWhiteSpace(rawContent) && (images == null || images.Count == 0))
            {
                result.Message = "请输入帖子内容或添加图片";
                return result;
            }

            KuroLogger.Loading("POST_PUBLISH", string.Format("Publishing post: Title='{0}', GameId={1}, ForumId={2}, ImagesCount={3}, TopicsCount={4}",
                title, gameId, gameForumId, images != null ? images.Count : 0, topics != null ? topics.Count : 0));

            string contentJson = KuroPostPublishHelper.BuildContentJson(rawContent, images);
            string h5Content = KuroPostPublishHelper.BuildH5Content(rawContent, images);
            string postDraftId = Guid.NewGuid().ToString() + "_" + (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;

            string topicIdsStr = "";
            if (topics != null && topics.Count > 0)
            {
                var idList = new List<string>();
                foreach (var t in topics)
                {
                    if (!string.IsNullOrEmpty(t.TopicId)) idList.Add(t.TopicId);
                }
                topicIdsStr = string.Join(",", idList);
            }

            var parameters = new Dictionary<string, string>
            {
                { "postTitle", title.Trim() },
                { "postType", "1" },
                { "gameId", gameId.ToString() },
                { "gameForumId", gameForumId.ToString() },
                { "publishType", "0" },
                { "showRange", "0" },
                { "isCopyright", "false" },
                { "isAiContent", "false" },
                { "draftId", draftId ?? "" },
                { "postDraftId", postDraftId },
                { "chatUserIds", "" },
                { "topicIds", topicIdsStr },
                { "topicIdList", topicIdsStr },
                { "content", contentJson },
                { "h5Content", h5Content },
                { "newH5Content", h5Content }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/postPublish", parameters);
            if (json != null)
            {
                int code = json.ContainsKey("code") ? (int)json.GetNamedNumber("code") : -1;
                result.Code = code;
                result.Message = json.ContainsKey("msg") ? json.GetNamedString("msg") : "";

                if (code == 200)
                {
                    result.Success = true;
                    if (json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                    {
                        var dataObj = json.GetNamedObject("data");
                        if (dataObj.ContainsKey("postId"))
                        {
                            result.PostId = dataObj.GetNamedString("postId");
                        }
                    }
                    KuroLogger.Info("POST_PUBLISH_SUCCESS", "Post published successfully! PostId: " + result.PostId);
                }
                else
                {
                    KuroLogger.Warn("POST_PUBLISH_FAIL", string.Format("Post publish failed with code {0}: {1}", code, result.Message));
                }
            }
            else
            {
                result.Message = "网络请求失败，请检查网络连接";
                KuroLogger.Error("POST_PUBLISH_ERROR", "Post publish failed - network response was null");
            }

            return result;
        }
    }
}
