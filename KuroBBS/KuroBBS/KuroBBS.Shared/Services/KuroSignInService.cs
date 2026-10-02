using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroSignInService
    {
        private static KuroSignInService _instance;
        public static KuroSignInService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroSignInService();
                }
                return _instance;
            }
        }

        public async Task<SignInStatus> GetSignInStatusAsync(int gameId, string serverId, string roleId, string userId)
        {
            var status = new SignInStatus
            {
                GameId = gameId,
                GameName = gameId == 2 ? "战双帕弥什" : (gameId == 3 ? "鸣潮" : "游戏")
            };

            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "serverId", serverId ?? "" },
                { "roleId", roleId ?? "" },
                { "userId", userId ?? SettingsHelper.UserId }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/initSignInV2", parameters);
            if (json == null || !json.ContainsKey("data") || json.GetNamedValue("data").ValueType != JsonValueType.Object)
            {
                json = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/initSignIn", parameters);
            }

            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                status.IsSignedInToday = GetBoolean(data, "isSigIn", false) || GetBoolean(data, "isSignIn", false) || GetBoolean(data, "hasSignIn", false);
                status.ConsecutiveDays = (int)GetNumber(data, "sigInNum", (int)GetNumber(data, "serialDays", (int)GetNumber(data, "continueDays", 0)));
                status.TotalSignInDays = (int)GetNumber(data, "sigInNum", (int)GetNumber(data, "totalDays", status.ConsecutiveDays));
                status.MonthTotalDays = (int)GetNumber(data, "monthTotalDays", 30);
                status.ReplenishCardCount = (int)GetNumber(data, "replenishCardCount", (int)GetNumber(data, "replenishNum", 0));

                string eventStart = GetString(data, "eventStartTimes", "");
                status.EventStartTimes = eventStart;
                if (!string.IsNullOrEmpty(eventStart) && eventStart.Length >= 7)
                {
                    status.ReqMonth = eventStart.Substring(5, 2);
                }
                else
                {
                    status.ReqMonth = DateTime.Now.Month.ToString("D2");
                }

                if (data.ContainsKey("signInGoodsConfigs") && data.GetNamedValue("signInGoodsConfigs").ValueType == JsonValueType.Array)
                {
                    var goodsArr = data.GetNamedArray("signInGoodsConfigs");
                    int dayIdx = 1;
                    foreach (var rVal in goodsArr)
                    {
                        if (rVal.ValueType != JsonValueType.Object) continue;
                        var rObj = rVal.GetObject();
                        int goodsNum = (int)GetNumber(rObj, "goodsNum", (int)GetNumber(rObj, "gainScore", 1));
                        string gUrl = GetString(rObj, "goodsUrl", GetString(rObj, "url", GetString(rObj, "iconUrl", GetString(rObj, "imgUrl", ""))));
                        string gName = GetString(rObj, "goodsName", GetString(rObj, "name", "物资补给"));
                        bool isGained = GetBoolean(rObj, "isGain", false) 
                                     || GetBoolean(rObj, "isSigIn", false) 
                                     || GetBoolean(rObj, "isSignIn", false)
                                     || (dayIdx <= status.TotalSignInDays);

                        status.MonthRecords.Add(new SignInDayInfo
                        {
                            Date = dayIdx + "日",
                            DayNumber = dayIdx,
                            IsSignedIn = isGained,
                            RewardName = gName,
                            RewardIcon = gUrl,
                            RewardCount = goodsNum
                        });
                        dayIdx++;
                    }
                }
                else if (data.ContainsKey("records") && data.GetNamedValue("records").ValueType == JsonValueType.Array)
                {
                    var recArr = data.GetNamedArray("records");
                    int dayIdx = 1;
                    foreach (var rVal in recArr)
                    {
                        if (rVal.ValueType != JsonValueType.Object) continue;
                        var rObj = rVal.GetObject();
                        int goodsNum = (int)GetNumber(rObj, "goodsNum", (int)GetNumber(rObj, "gainScore", 1));
                        string gUrl = GetString(rObj, "goodsUrl", GetString(rObj, "url", GetString(rObj, "iconUrl", GetString(rObj, "imgUrl", ""))));
                        string gName = GetString(rObj, "goodsName", GetString(rObj, "name", "物资补给"));
                        bool isGained = GetBoolean(rObj, "isSigIn", false) 
                                     || GetBoolean(rObj, "isSignIn", false) 
                                     || GetBoolean(rObj, "isGain", false)
                                     || (dayIdx <= status.TotalSignInDays);

                        status.MonthRecords.Add(new SignInDayInfo
                        {
                            Date = dayIdx + "日",
                            DayNumber = dayIdx,
                            IsSignedIn = isGained,
                            RewardName = gName,
                            RewardIcon = gUrl,
                            RewardCount = goodsNum
                        });
                        dayIdx++;
                    }
                }
            }

            // Also check /user/signIn/info for user-level sign in state
            try
            {
                var userSignInJson = await KuroApiClient.Instance.PostFormAsync("/user/signIn/info", new Dictionary<string, string> { { "gameId", gameId.ToString() } });
                if (userSignInJson != null && userSignInJson.ContainsKey("data") && userSignInJson.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var uData = userSignInJson.GetNamedObject("data");
                    bool hasSignIn = GetBoolean(uData, "hasSignIn", false);
                    int continueDays = (int)GetNumber(uData, "continueDays", 0);
                    if (hasSignIn)
                    {
                        status.IsSignedInToday = true;
                    }
                    if (continueDays > status.ConsecutiveDays)
                    {
                        status.ConsecutiveDays = continueDays;
                    }
                }
            }
            catch
            {
            }

            return status;
        }

        public async Task<SignInResult> ExecuteDailySignInAsync(int gameId, string serverId, string roleId, string userId, string reqMonth = null)
        {
            if (string.IsNullOrEmpty(reqMonth))
            {
                reqMonth = DateTime.Now.Month.ToString("D2");
            }

            var result = new SignInResult
            {
                Success = false,
                Message = "签到失败"
            };

            var parameters = new Dictionary<string, string>
            {
                { "gameId", gameId.ToString() },
                { "serverId", serverId ?? "" },
                { "roleId", roleId ?? "" },
                { "userId", userId ?? SettingsHelper.UserId },
                { "reqMonth", reqMonth }
            };

            var json = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/v2", parameters);
            if (json != null && json.ContainsKey("code"))
            {
                int code = (int)json.GetNamedNumber("code");
                result.Code = code;
                result.Message = json.ContainsKey("msg") ? json.GetNamedString("msg") : "";

                if (code == 200)
                {
                    result.Success = true;
                    if (string.IsNullOrEmpty(result.Message)) result.Message = "签到成功！";

                    if (json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                    {
                        var d = json.GetNamedObject("data");
                        if (d.ContainsKey("todayList") && d.GetNamedValue("todayList").ValueType == JsonValueType.Array)
                        {
                            var tList = d.GetNamedArray("todayList");
                            if (tList.Count > 0 && tList[0].ValueType == JsonValueType.Object)
                            {
                                var item = tList[0].GetObject();
                                int num = (int)GetNumber(item, "goodsNum", 0);
                                result.RewardSummary = num > 0 ? ("已领取奖励 (数量: " + num + ")") : "已成功领取每日签到奖励";
                            }
                        }
                    }
                    return result;
                }

                if (code == 1511)
                {
                    result.Success = true;
                    result.AlreadySignedIn = true;
                    result.Message = "今日已完成签到，请勿重复签到";
                    return result;
                }

                KuroLogger.Warn("SIGNIN_FAIL_RESP", string.Format("Sign-in v2 response code: {0}, msg: {1}", code, result.Message));

                // Fallback attempt without reqMonth if v2 fails
                if (code == 1501)
                {
                    var v1Params = new Dictionary<string, string>
                    {
                        { "gameId", gameId.ToString() },
                        { "serverId", serverId ?? "" },
                        { "roleId", roleId ?? "" },
                        { "userId", userId ?? SettingsHelper.UserId }
                    };
                    var jsonV1 = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/signIn", v1Params);
                    if (jsonV1 != null && jsonV1.ContainsKey("code"))
                    {
                        int c1 = (int)jsonV1.GetNamedNumber("code");
                        result.Code = c1;
                        result.Message = jsonV1.ContainsKey("msg") ? jsonV1.GetNamedString("msg") : "";
                        if (c1 == 200)
                        {
                            result.Success = true;
                            return result;
                        }
                        if (c1 == 1511)
                        {
                            result.Success = true;
                            result.AlreadySignedIn = true;
                            result.Message = "今日已完成签到，请勿重复签到";
                            return result;
                        }
                    }
                }
            }
            return result;
        }

        public async Task<int> GetTotalGoldAsync()
        {
            var json = await KuroApiClient.Instance.PostFormAsync("/encourage/gold/getTotalGold", new Dictionary<string, string>());
            if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
            {
                var data = json.GetNamedObject("data");
                return (int)GetNumber(data, "totalGold", 0);
            }
            return 0;
        }

        public async Task<List<SignInClaimRecord>> QuerySignInRecordsAsync(int gameId, string serverId, string roleId, string userId)
        {
            var list = new List<SignInClaimRecord>();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "serverId", serverId ?? "" },
                    { "roleId", roleId ?? "" },
                    { "userId", userId ?? SettingsHelper.UserId }
                };

                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/queryRecordV2", parameters);
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
                {
                    var arr = json.GetNamedArray("data");
                    foreach (var val in arr)
                    {
                        if (val.ValueType == JsonValueType.Object)
                        {
                            var o = val.GetObject();
                            list.Add(new SignInClaimRecord
                            {
                                GoodsId = GetString(o, "goodsId", ""),
                                GoodsName = GetString(o, "goodsName", "补给物品"),
                                GoodsNum = (int)GetNumber(o, "goodsNum", 1),
                                GoodsUrl = GetString(o, "goodsUrl", ""),
                                OrderCode = GetString(o, "orderCode", ""),
                                SendState = GetBoolean(o, "sendState", true),
                                SignInDate = GetString(o, "sigInDate", "")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("QUERY_SIGNIN_RECORDS_ERROR", "Error querying records: " + ex.Message);
            }
            return list;
        }

        public async Task<List<CommendFollowItem>> GetCommendFollowListAsync(int gameId, string serverId, string roleId, string userId)
        {
            var list = new List<CommendFollowItem>();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "serverId", serverId ?? "" },
                    { "roleId", roleId ?? "" },
                    { "userId", userId ?? SettingsHelper.UserId }
                };

                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/signIn/commendFollow", parameters);
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
                {
                    var arr = json.GetNamedArray("data");
                    foreach (var val in arr)
                    {
                        if (val.ValueType == JsonValueType.Object)
                        {
                            var o = val.GetObject();
                            list.Add(new CommendFollowItem
                            {
                                UserId = GetString(o, "userId", ""),
                                UserName = GetString(o, "userName", ""),
                                UserHeadUrl = GetString(o, "userHeadUrl", ""),
                                UserSign = GetString(o, "userSign", ""),
                                IsFollow = GetBoolean(o, "isFollow", false)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("GET_COMMEND_FOLLOW_ERROR", "Error getting commend follows: " + ex.Message);
            }
            return list;
        }

        private string GetString(JsonObject obj, string key, string defVal)
        {
            if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.String)
            {
                return obj.GetNamedString(key);
            }
            return defVal;
        }

        private double GetNumber(JsonObject obj, string key, double defVal)
        {
            if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.Number)
            {
                return obj.GetNamedNumber(key);
            }
            return defVal;
        }

        private bool GetBoolean(JsonObject obj, string key, bool defVal)
        {
            if (obj.ContainsKey(key) && obj.GetNamedValue(key).ValueType == JsonValueType.Boolean)
            {
                return obj.GetNamedBoolean(key);
            }
            return defVal;
        }
    }
}
