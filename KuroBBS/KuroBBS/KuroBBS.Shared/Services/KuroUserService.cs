using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroUserService
    {
        private static KuroUserService _instance;
        public static KuroUserService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroUserService();
                }
                return _instance;
            }
        }

        public async Task<UserProfile> GetUserProfileAsync()
        {
            var profile = new UserProfile
            {
                UserName = SettingsHelper.UserName,
                UserId = SettingsHelper.UserId,
                IsLoggedIn = SettingsHelper.IsLoggedIn
            };

            if (!SettingsHelper.IsLoggedIn)
            {
                return profile;
            }

            return await GetUserProfileDetailAsync(SettingsHelper.UserId);
        }

        public async Task<UserProfile> GetUserProfileDetailAsync(string userId)
        {
            var profile = new UserProfile
            {
                UserId = userId ?? "",
                UserName = !string.IsNullOrEmpty(userId) ? ("用户_" + userId) : "用户",
                IsLoggedIn = SettingsHelper.IsLoggedIn
            };

            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(userId))
            {
                parameters["viewUserId"] = userId;
            }

            var json = await KuroApiClient.Instance.PostFormAsync("/user/mineV2", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                var userObj = (data.ContainsKey("mine") && data.GetNamedValue("mine").ValueType == JsonValueType.Object)
                    ? data.GetNamedObject("mine") 
                    : data;

                profile.UserId = GetString(userObj, "userId", profile.UserId);
                profile.UserName = GetString(userObj, "userName", profile.UserName);
                profile.AvatarUrl = GetString(userObj, "headUrl", "");
                profile.HeadFrameUrl = GetString(userObj, "headFrameUrl", "");
                profile.Signature = GetString(userObj, "signature", "这个人很懒，还没有签名。");
                profile.IpRegion = GetString(userObj, "ipRegion", "未知");
                profile.FollowingCount = (int)GetNumber(userObj, "followCount", 0);
                profile.FansCount = (int)GetNumber(userObj, "fansCount", 0);
                profile.PostCount = (int)GetNumber(userObj, "postCount", 0);
                profile.LikeCount = (int)GetNumber(userObj, "likeCount", 0);
                profile.IsFollow = GetNumber(userObj, "isFollow", 0) == 1 || GetBool(userObj, "isFollow", false);
                bool isSelf = !string.IsNullOrEmpty(SettingsHelper.UserId) && (profile.UserId == SettingsHelper.UserId);
                profile.IsLoginUser = isSelf;
                profile.Gender = (int)GetNumber(userObj, "gender", 0);
                profile.RegisterTime = GetString(userObj, "registerTime", "");

                if (isSelf)
                {
                    SettingsHelper.UserId = profile.UserId;
                    SettingsHelper.UserName = profile.UserName;
                }
            }

            if (profile.IsLoginUser)
            {
                profile.GoldCount = await KuroSignInService.Instance.GetTotalGoldAsync();
            }

            return profile;
        }

        public async Task<List<PostItem>> GetUserPostsAsync(string userId, int pageIndex = 1, int pageSize = 15)
        {
            var list = new List<PostItem>();
            if (string.IsNullOrEmpty(userId)) return list;

            var parameters = new Dictionary<string, string>
            {
                { "searchType", "1" },
                { "type", "2" },
                { "otherUserId", userId },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/forum/getMinePost", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("postList") && dataObj.GetNamedValue("postList").ValueType == JsonValueType.Array)
                {
                    var postArr = dataObj.GetNamedArray("postList");
                    foreach (var pVal in postArr)
                    {
                        if (pVal.ValueType != JsonValueType.Object) continue;
                        var pObj = pVal.GetObject();
                        var post = KuroForumService.Instance.ParseCommunityPostItem(pObj, 2);
                        if (post != null)
                        {
                            list.Add(post);
                        }
                    }
                }
            }

            return list;
        }

        public async Task<List<PostItem>> GetUserCollectionsAsync(string userId, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<PostItem>();
            if (string.IsNullOrEmpty(userId)) return list;

            var parameters = new Dictionary<string, string>
            {
                { "viewUserId", userId },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() },
                { "searchType", "2" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/user/center/myCollect", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("postList") && dataObj.GetNamedValue("postList").ValueType == JsonValueType.Array)
                {
                    var postArr = dataObj.GetNamedArray("postList");
                    foreach (var pVal in postArr)
                    {
                        if (pVal.ValueType != JsonValueType.Object) continue;
                        var pObj = pVal.GetObject();
                        var post = KuroForumService.Instance.ParseCommunityPostItem(pObj, 2);
                        if (post != null)
                        {
                            list.Add(post);
                        }
                    }
                }
            }

            return list;
        }

        public async Task<List<UserCommentNoticeItem>> GetUserCommentsAsync(string userId, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<UserCommentNoticeItem>();
            if (string.IsNullOrEmpty(userId)) return list;

            var parameters = new Dictionary<string, string>
            {
                { "viewUserId", userId },
                { "pageIndex", pageIndex.ToString() },
                { "pageSize", pageSize.ToString() }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/user/center/myComment", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("noticeVos") && dataObj.GetNamedValue("noticeVos").ValueType == JsonValueType.Array)
                {
                    var arr = dataObj.GetNamedArray("noticeVos");
                    foreach (var itemVal in arr)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var obj = itemVal.GetObject();
                        var item = new UserCommentNoticeItem();
                        item.PostId = GetString(obj, "postId", "");
                        item.PostCommentId = GetString(obj, "postCommentId", "");
                        item.PostCommentReplyId = GetString(obj, "postCommentReplyId", "0");
                        item.SendUserId = GetString(obj, "sendUserId", "");
                        item.SendUserName = GetString(obj, "sendUserName", "");
                        item.SendUserHeadUrl = GetString(obj, "sendUserHeadUrl", "");
                        item.TargetUserName = GetString(obj, "userName", "");
                        item.ShowTime = GetString(obj, "showTime", "");
                        item.GameName = GetString(obj, "gameName", "");
                        item.GameId = (int)GetNumber(obj, "gameId", 2);
                        item.ForumId = (int)GetNumber(obj, "forumId", 4);
                        item.DetailType = (int)GetNumber(obj, "detailType", 0);
                        item.LikeCount = (int)GetNumber(obj, "numCount", (int)GetNumber(obj, "likeCount", 0));
                        item.IsLiked = GetBool(obj, "isLike", GetBool(obj, "isLiked", false)) || GetNumber(obj, "isLike", 0) == 1;
                        item.ImageUrl = GetString(obj, "imageUrl", GetString(obj, "picUrl", ""));

                        string noticeContent = GetString(obj, "noticeContent", "");
                        string noticeContentV2 = GetString(obj, "noticeContentV2", "");
                        string noticeTitle = GetString(obj, "noticeTitle", "");
                        string noticeTitleV2 = GetString(obj, "noticeTitleV2", "");

                        item.NoticeContent = noticeContent;
                        if (!string.IsNullOrEmpty(noticeContentV2))
                        {
                            item.ContentRuns = KuroEmojiService.Instance.ParseBlocksJsonToRuns(noticeContentV2);
                        }
                        else if (!string.IsNullOrEmpty(noticeContent))
                        {
                            item.ContentRuns = KuroEmojiService.Instance.ParseTextToRuns(noticeContent);
                        }

                        item.NoticeTitle = noticeTitle;
                        if (!string.IsNullOrEmpty(noticeTitleV2))
                        {
                            item.TitleRuns = KuroEmojiService.Instance.ParseBlocksJsonToRuns(noticeTitleV2);
                        }
                        else if (!string.IsNullOrEmpty(noticeTitle))
                        {
                            item.TitleRuns = KuroEmojiService.Instance.ParseTextToRuns(noticeTitle);
                        }

                        list.Add(item);
                    }
                }
            }

            return list;
        }

        public async Task<bool> FollowUserAsync(string userId, bool follow)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            var parameters = new Dictionary<string, string>
            {
                { "followUserId", userId },
                { "operateType", follow ? "1" : "2" },
                { "followSource", "4" }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/user/followUser", parameters);
            return json != null && json.ContainsKey("code") && json.GetNamedNumber("code") == 200;
        }

        public async Task<List<GameRoleCard>> GetUserDefaultRolesAsync(string userId)
        {
            var roles = new List<GameRoleCard>();
            if (string.IsNullOrEmpty(userId)) return roles;

            var parameters = new Dictionary<string, string>
            {
                { "queryUserId", userId }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/user/role/findUserDefaultRole", parameters);
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var dataObj = json.GetNamedObject("data");
                if (dataObj.ContainsKey("defaultRoleList") && dataObj.GetNamedValue("defaultRoleList").ValueType == JsonValueType.Array)
                {
                    var arr = dataObj.GetNamedArray("defaultRoleList");
                    foreach (var itemVal in arr)
                    {
                        if (itemVal.ValueType != JsonValueType.Object) continue;
                        var obj = itemVal.GetObject();
                        var role = ParseGameRoleObj(obj);
                        if (role != null)
                        {
                            roles.Add(role);
                        }
                    }
                }
                else if (dataObj.ContainsKey("defaultRole") && dataObj.GetNamedValue("defaultRole").ValueType == JsonValueType.Object)
                {
                    var obj = dataObj.GetNamedObject("defaultRole");
                    var role = ParseGameRoleObj(obj);
                    if (role != null)
                    {
                        roles.Add(role);
                    }
                }
            }

            return roles;
        }

        public async Task<List<GameRoleCard>> GetGameRolesAsync()
        {
            var roles = new List<GameRoleCard>();
            var json = await KuroApiClient.Instance.PostFormAsync("/gamer/role/list", new Dictionary<string, string>());
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
            {
                var arr = json.GetNamedArray("data");
                foreach (var itemVal in arr)
                {
                    if (itemVal.ValueType != JsonValueType.Object) continue;
                    var obj = itemVal.GetObject();
                    var role = ParseGameRoleObj(obj);
                    if (role != null)
                    {
                        roles.Add(role);
                    }
                }
            }

            return roles;
        }

        private GameRoleCard ParseGameRoleObj(JsonObject obj)
        {
            if (obj == null) return null;
            var role = new GameRoleCard();
            role.RoleId = GetString(obj, "roleId", "");
            role.RoleName = GetString(obj, "roleName", "指挥官");
            role.ServerId = GetString(obj, "serverId", "");
            role.ServerName = GetString(obj, "serverName", "官方服");
            role.GameId = (int)GetNumber(obj, "gameId", 2);
            role.GameName = role.GameId == 2 ? "战双帕弥什" : (role.GameId == 3 ? "鸣潮" : "游戏");
            role.GameThemeColor = role.GameId == 2 ? "#00A3FF" : (role.GameId == 3 ? "#F5A623" : "#00A3FF");
            role.IsDefault = GetBool(obj, "isDefault", false);

            string levelStr = GetString(obj, "gameLevel", GetString(obj, "roleLevel", ""));
            if (string.IsNullOrEmpty(levelStr))
            {
                int lvlNum = (int)GetNumber(obj, "level", (int)GetNumber(obj, "gameLevel", (int)GetNumber(obj, "roleLevel", 1)));
                levelStr = lvlNum.ToString();
            }
            int lvl;
            int.TryParse(levelStr, out lvl);
            role.Level = lvl > 0 ? lvl : 1;

            if (role.GameId == 2 && role.Level >= 120)
            {
                role.IsMedalLevel = true;
                role.LevelDisplay = "荣耀 " + (role.Level - 120 + 1);
            }
            else
            {
                role.IsMedalLevel = false;
                role.LevelDisplay = "Lv." + role.Level;
            }

            role.HeadPhotoUrl = GetString(obj, "headPhotoUrl", "");
            role.GameHeadUrl = GetString(obj, "gameHeadUrl", "");
            role.AvatarUrl = !string.IsNullOrEmpty(role.HeadPhotoUrl) ? role.HeadPhotoUrl : (!string.IsNullOrEmpty(role.GameHeadUrl) ? role.GameHeadUrl : GetString(obj, "roleHeadUrl", GetString(obj, "headUrl", "")));

            string score = GetString(obj, "roleScore", "");
            role.RoleScore = score;
            role.EnergyDisplay = !string.IsNullOrEmpty(score) ? ("战力 " + score) : GetString(obj, "energy", "");

            int activeDay = (int)GetNumber(obj, "activeDay", 0);
            role.ActiveDay = activeDay;
            role.ActiveDayDisplay = activeDay > 0 ? (activeDay + " 天") : "-";

            int roleNum = (int)GetNumber(obj, "roleNum", 0);
            role.RoleNum = roleNum;
            role.RoleNumLabel = (role.GameId == 2 ? "构造体" : (role.GameId == 3 ? "共鸣者" : "角色"));

            int achieve = (int)GetNumber(obj, "achievementCount", 0);
            role.AchievementCount = achieve.ToString();

            double fashionPct = GetNumber(obj, "fashionCollectionPercent", 0);
            role.FashionPercentDisplay = fashionPct > 0 ? ((fashionPct * 100).ToString("0.#") + "%") : "0%";

            double phantomPct = GetNumber(obj, "phantomPercent", 0);
            role.PhantomPercentDisplay = phantomPct > 0 ? ((phantomPct * 100).ToString("0.#") + "%") : "0%";

            // 1. 游戏天数 (两游戏通用)
            role.Stat1Label = "游戏天数";
            role.Stat1Value = activeDay > 0 ? (activeDay + " 天") : "0 天";

            if (role.GameId == 2) 
            {
                // 战双帕弥什: 游戏天数、角色总评分、角色数量、涂装收集率
                role.Stat2Label = "角色总评分";
                role.Stat2Value = !string.IsNullOrEmpty(score) ? score : "0";

                role.Stat3Label = "角色数量";
                role.Stat3Value = roleNum > 0 ? roleNum.ToString() : "0";

                role.Stat4Label = "涂装收集率";
                role.Stat4Value = role.FashionPercentDisplay;
            }
            else 
            {
                // 鸣潮: 游戏天数、成就数、角色数量、声骸收集进度
                role.Stat2Label = "成就数";
                role.Stat2Value = achieve.ToString();

                role.Stat3Label = "角色数量";
                role.Stat3Value = roleNum > 0 ? roleNum.ToString() : "0";

                role.Stat4Label = "声骸收集进度";
                role.Stat4Value = role.PhantomPercentDisplay;
            }

            return role;
        }

        private string GetString(JsonObject obj, string key, string defVal)
        {
            if (obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber().ToString();
            }
            return defVal;
        }

        private double GetNumber(JsonObject obj, string key, double defVal)
        {
            if (obj.ContainsKey(key))
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
            if (obj.ContainsKey(key))
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean();
                if (val.ValueType == JsonValueType.String)
                {
                    bool b;
                    if (bool.TryParse(val.GetString(), out b)) return b;
                }
            }
            return defVal;
        }
    }
}
