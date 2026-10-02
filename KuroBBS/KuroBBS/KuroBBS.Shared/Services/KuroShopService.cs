using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    /// <summary>
    /// 库洛金币商城 / 任务中心的接口封装。
    ///
    /// 端点全部来自 kuroshop/ 下抓到的真实报文（方括号编号对应文件名）：
    ///   [605] GET  /encourage/commodity/getKindInfo        商品分类
    ///   [606] POST /encourage/gold/getTotalGold            金币余额
    ///   [608] POST /encourage/commodity/list               商品列表（kindId 分页）
    ///   [627] POST /encourage/level/getTaskProcess         任务进度
    ///   [632] POST /encourage/gold/getGoldLogs             金币流水（type=1 收入 / 2 支出）
    ///   [641] POST /encourage/commodity/detail             商品详情
    ///   [648] POST /user/role/findRoleList                 游戏角色列表
    ///   [651] POST /encourage/order/create                 下单
    ///   [653] GET  /encourage/order/findOrderDetail?orderCode=  订单详情
    ///
    /// 注意：getKindInfo 与 findOrderDetail 是 GET，其余是 POST（form 表单）。
    /// </summary>
    public class KuroShopService
    {
        private static KuroShopService _instance;
        public static KuroShopService Instance
        {
            get
            {
                if (_instance == null) _instance = new KuroShopService();
                return _instance;
            }
        }

        private const string Tag = "SHOP";

        // ---------- 分类 ----------

        /// <summary>商品分类。服务端返回 0=全部 / 2=战双帕弥什 / 3=鸣潮 / 1=库街区。</summary>
        public async Task<List<ShopKind>> GetKindInfoAsync()
        {
            var list = new List<ShopKind>();
            try
            {
                var json = await KuroApiClient.Instance.GetAsync("/encourage/commodity/getKindInfo");
                var data = GetDataObject(json);
                if (data == null) return list;

                if (data.ContainsKey("kinds") && data.GetNamedValue("kinds").ValueType == JsonValueType.Array)
                {
                    foreach (var v in data.GetNamedArray("kinds"))
                    {
                        if (v.ValueType != JsonValueType.Object) continue;
                        var o = v.GetObject();
                        list.Add(new ShopKind
                        {
                            Id = (int)GetNumber(o, "id", 0),
                            Name = GetString(o, "name", ""),
                            IsMust = GetBoolean(o, "isMust", false)
                        });
                    }
                }
                KuroLogger.Trace("[" + Tag + "] KINDS count=" + list.Count);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_KINDS_ERROR", ex.Message);
            }
            return list;
        }

        // ---------- 余额 ----------

        /// <summary>金币余额（data.goldNum）。</summary>
        public async Task<int> GetTotalGoldAsync()
        {
            try
            {
                var json = await KuroApiClient.Instance.PostFormAsync(
                    "/encourage/gold/getTotalGold", new Dictionary<string, string>());
                var data = GetDataObject(json);
                if (data == null) return 0;
                return (int)GetNumber(data, "goldNum", 0);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_GOLD_ERROR", ex.Message);
                return 0;
            }
        }

        // ---------- 商品列表 ----------

        /// <summary>商品列表。kindId=0 为全部；分页 pageIndex 从 1 开始。</summary>
        public async Task<List<ShopCommodity>> GetCommodityListAsync(int kindId, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<ShopCommodity>();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "kindId", kindId.ToString() },
                    { "pageIndex", pageIndex.ToString() },
                    { "pageSize", pageSize.ToString() }
                };
                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/commodity/list", parameters);
                var data = GetDataObject(json);
                if (data == null) return list;

                if (data.ContainsKey("commodityList") && data.GetNamedValue("commodityList").ValueType == JsonValueType.Array)
                {
                    foreach (var v in data.GetNamedArray("commodityList"))
                    {
                        if (v.ValueType != JsonValueType.Object) continue;
                        var o = v.GetObject();
                        list.Add(new ShopCommodity
                        {
                            CommodityCode = GetString(o, "commodityCode", ""),
                            CommodityName = GetString(o, "commodityName", ""),
                            CommodityPrice = (int)GetNumber(o, "commodityPrice", 0),
                            CommodityStatus = (int)GetNumber(o, "commodityStatus", 0),
                            GameId = (int)GetNumber(o, "gameId", 0),
                            GameName = GetString(o, "gameName", ""),
                            IsSellout = GetBoolean(o, "isSellout", false),
                            KindId = (int)GetNumber(o, "kindId", 0),
                            KindName = GetString(o, "kindName", ""),
                            PictureUrl = GetString(o, "pictureUrl", ""),
                            ProductId = GetString(o, "productId", ""),
                            ProductType = (int)GetNumber(o, "productType", 0),
                            SaleTime = (long)GetNumber(o, "saleTime", 0),
                            TotalStock = (long)GetNumber(o, "totalStock", 0),
                            TotalSurplusStock = (long)GetNumber(o, "totalSurplusStock", 0)
                        });
                    }
                }
                KuroLogger.Trace("[" + Tag + "] LIST kind=" + kindId + " page=" + pageIndex + " count=" + list.Count);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_LIST_ERROR", ex.Message);
            }
            return list;
        }

        // ---------- 商品详情 ----------

        public async Task<ShopCommodityDetail> GetCommodityDetailAsync(string commodityCode)
        {
            if (string.IsNullOrEmpty(commodityCode)) return null;
            try
            {
                var parameters = new Dictionary<string, string> { { "commodityCode", commodityCode } };
                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/commodity/detail", parameters);
                var data = GetDataObject(json);
                if (data == null) return null;

                var d = new ShopCommodityDetail
                {
                    CommodityCode = GetString(data, "commodityCode", commodityCode),
                    CommodityName = GetString(data, "commodityName", ""),
                    CommodityDesc = GetString(data, "commodityDesc", ""),
                    CommodityPrice = (int)GetNumber(data, "commodityPrice", 0),
                    CommodityStatus = (int)GetNumber(data, "commodityStatus", 0),
                    CommodityType = (int)GetNumber(data, "commodityType", 0),
                    CommodityLimit = (int)GetNumber(data, "commodityLimit", 0),
                    CommodityLimitStrategy = (int)GetNumber(data, "commodityLimitStrategy", 0),
                    CommodityLimitType = (int)GetNumber(data, "commodityLimitType", 0),
                    CurrentUserLimitBuy = (int)GetNumber(data, "currentUserLimitBuy", 0),
                    GameId = (int)GetNumber(data, "gameId", 0),
                    GameName = GetString(data, "gameName", ""),
                    KindId = (int)GetNumber(data, "kindId", 0),
                    KindName = GetString(data, "kindName", ""),
                    PictureUrl = GetString(data, "pictureUrl", ""),
                    ProductId = GetString(data, "productId", ""),
                    ProductType = (int)GetNumber(data, "productType", 0),
                    PropType = (int)GetNumber(data, "propType", 0),
                    IsSellout = GetBoolean(data, "isSellout", false),
                    TotalStock = (long)GetNumber(data, "totalStock", 0),
                    TotalSurplusStock = (long)GetNumber(data, "totalSurplusStock", 0),
                    SaleTime = (long)GetNumber(data, "saleTime", 0),
                    ShelveTime = (long)GetNumber(data, "shelveTime", 0),
                    OffShelveTime = (long)GetNumber(data, "offShelveTime", 0),
                    CreateTime = (long)GetNumber(data, "createTime", 0)
                };
                return d;
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_DETAIL_ERROR", ex.Message);
                return null;
            }
        }

        // ---------- 任务中心 ----------

        /// <summary>
        /// 任务进度。注意 [627] 的真实请求体带 gameId 与 userId 两个字段。
        /// </summary>
        public async Task<ShopTaskProcess> GetTaskProcessAsync(int gameId, string userId)
        {
            var result = new ShopTaskProcess();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "userId", string.IsNullOrEmpty(userId) ? SettingsHelper.UserId : userId }
                };
                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/level/getTaskProcess", parameters);
                var data = GetDataObject(json);
                if (data == null) return result;

                result.CurrentDailyGold = (int)GetNumber(data, "currentDailyGold", 0);
                result.MaxDailyGold = (int)GetNumber(data, "maxDailyGold", 0);
                result.GrowTask = ParseTaskArray(data, "growTask");
                result.DailyTask = ParseTaskArray(data, "dailyTask");

                KuroLogger.Trace("[" + Tag + "] TASKS grow=" + result.GrowTask.Count + " daily=" + result.DailyTask.Count);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_TASK_ERROR", ex.Message);
            }
            return result;
        }

        private static List<ShopTaskItem> ParseTaskArray(JsonObject data, string key)
        {
            var list = new List<ShopTaskItem>();
            if (data == null || !data.ContainsKey(key)) return list;
            if (data.GetNamedValue(key).ValueType != JsonValueType.Array) return list;

            foreach (var v in data.GetNamedArray(key))
            {
                if (v.ValueType != JsonValueType.Object) continue;
                var o = v.GetObject();
                list.Add(new ShopTaskItem
                {
                    Remark = GetString(o, "remark", ""),
                    GainGold = (int)GetNumber(o, "gainGold", 0),
                    CompleteTimes = (int)GetNumber(o, "completeTimes", 0),
                    NeedActionTimes = (int)GetNumber(o, "needActionTimes", 0),
                    Process = GetNumber(o, "process", 0),
                    Times = (int)GetNumber(o, "times", 0),
                    SkipType = (int)GetNumber(o, "skipType", 0)
                });
            }
            return list;
        }

        /// <summary>金币流水。type=1 为收入、2 为支出（与真实抓取一致）。</summary>
        public async Task<List<GoldLogItem>> GetGoldLogsAsync(int type, int pageIndex = 1, int pageSize = 20)
        {
            var list = new List<GoldLogItem>();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "pageIndex", pageIndex.ToString() },
                    { "pageSize", pageSize.ToString() },
                    { "type", type.ToString() }
                };
                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/gold/getGoldLogs", parameters);
                var data = GetDataObject(json);
                if (data == null) return list;

                if (data.ContainsKey("logList") && data.GetNamedValue("logList").ValueType == JsonValueType.Array)
                {
                    foreach (var v in data.GetNamedArray("logList"))
                    {
                        if (v.ValueType != JsonValueType.Object) continue;
                        var o = v.GetObject();
                        list.Add(new GoldLogItem
                        {
                            Remark = GetString(o, "remark", ""),
                            Gold = (int)GetNumber(o, "gold", 0),
                            CreateTime = (long)GetNumber(o, "createTime", 0),
                            AllowClick = GetBoolean(o, "allowClick", false)
                        });
                    }
                }
                KuroLogger.Trace("[" + Tag + "] LOGS type=" + type + " count=" + list.Count);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_LOGS_ERROR", ex.Message);
            }
            return list;
        }

        // ---------- 游戏角色（下单目标） ----------

        /// <summary>
        /// 游戏角色列表。注意 [648] 的 data 是**数组**而不是对象，这里单独处理。
        /// </summary>
        public async Task<List<ShopGameRole>> FindRoleListAsync(int gameId)
        {
            var list = new List<ShopGameRole>();
            try
            {
                var parameters = new Dictionary<string, string> { { "gameId", gameId.ToString() } };
                var json = await KuroApiClient.Instance.PostFormAsync("/user/role/findRoleList", parameters);
                if (json == null || !json.ContainsKey("data")) return list;
                if (json.GetNamedValue("data").ValueType != JsonValueType.Array) return list;

                foreach (var v in json.GetNamedArray("data"))
                {
                    if (v.ValueType != JsonValueType.Object) continue;
                    var o = v.GetObject();
                    list.Add(new ShopGameRole
                    {
                        RoleId = GetString(o, "roleId", ""),
                        RoleName = GetString(o, "roleName", ""),
                        GameId = GetString(o, "gameId", gameId.ToString()),
                        GameHeadUrl = GetString(o, "gameHeadUrl", ""),
                        ServerId = GetString(o, "serverId", ""),
                        ServerName = GetString(o, "serverName", ""),
                        GameLevel = GetString(o, "gameLevel", ""),
                        UserId = GetString(o, "userId", ""),
                        IsDefault = GetBoolean(o, "isDefault", false)
                    });
                }
                KuroLogger.Trace("[" + Tag + "] ROLES gameId=" + gameId + " count=" + list.Count);
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_ROLES_ERROR", ex.Message);
            }
            return list;
        }

        // ---------- 下单 / 订单 ----------

        /// <summary>
        /// 下单。真实请求体（[651]）为：
        /// commodityCode &amp; commodityNum &amp; roleId &amp; geeTestData &amp; province &amp; city &amp; area
        /// &amp; detail &amp; mobile &amp; receiver &amp; gameId
        /// —— 后七个是实物收货信息，虚拟商品留空即可，但字段必须带上。
        /// </summary>
        public async Task<ShopOrderResult> CreateOrderAsync(string commodityCode, int commodityNum,
                                                            string roleId, int gameId)
        {
            var result = new ShopOrderResult();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "commodityCode", commodityCode ?? "" },
                    { "commodityNum", commodityNum.ToString() },
                    { "roleId", roleId ?? "" },
                    { "geeTestData", "" },
                    { "province", "" },
                    { "city", "" },
                    { "area", "" },
                    { "detail", "" },
                    { "mobile", "" },
                    { "receiver", "" },
                    { "gameId", gameId.ToString() }
                };

                var json = await KuroApiClient.Instance.PostFormAsync("/encourage/order/create", parameters);
                if (json == null)
                {
                    result.Message = "网络异常，请稍后重试";
                    return result;
                }

                // 业务失败（金币不足 / 已兑完）时 code 非 200，msg 里带原因
                int code = (int)(json.ContainsKey("code") && json.GetNamedValue("code").ValueType == JsonValueType.Number
                    ? json.GetNamedNumber("code") : 0);
                result.Success = code == 200;
                result.Message = json.ContainsKey("msg") && json.GetNamedValue("msg").ValueType == JsonValueType.String
                    ? json.GetNamedString("msg") : "";

                if (result.Success)
                {
                    var data = GetDataObject(json);
                    if (data != null)
                    {
                        result.OrderCode = GetString(data, "orderCode", "");
                        result.GeeTest = GetBoolean(data, "geeTest", false);
                    }
                }

                KuroLogger.Trace("[" + Tag + "] ORDER code=" + code + " orderCode=" + result.OrderCode + " msg=" + result.Message);
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                KuroLogger.Error(Tag + "_ORDER_ERROR", ex.Message);
            }
            return result;
        }

        public async Task<ShopOrderDetail> FindOrderDetailAsync(string orderCode)
        {
            if (string.IsNullOrEmpty(orderCode)) return null;
            try
            {
                var json = await KuroApiClient.Instance.GetAsync(
                    "/encourage/order/findOrderDetail?orderCode=" + Uri.EscapeDataString(orderCode));
                var data = GetDataObject(json);
                if (data == null) return null;

                return new ShopOrderDetail
                {
                    OrderCode = GetString(data, "orderCode", orderCode),
                    CommodityCode = GetString(data, "commodityCode", ""),
                    CommodityName = GetString(data, "commodityName", ""),
                    PictureUrl = GetString(data, "pictureUrl", ""),
                    CommodityNum = (int)GetNumber(data, "commodityNum", 0),
                    CommodityPrice = (int)GetNumber(data, "commodityPrice", 0),
                    CommodityCost = (int)GetNumber(data, "commodityCost", 0),
                    CommodityType = (int)GetNumber(data, "commodityType", 0),
                    OrderStatus = (int)GetNumber(data, "orderStatus", 0),
                    GameName = GetString(data, "gameName", ""),
                    RoleId = GetString(data, "roleId", ""),
                    RoleName = GetString(data, "roleName", ""),
                    UserId = GetString(data, "userId", ""),
                    CreateTime = (long)GetNumber(data, "createTime", 0),
                    PayTime = (long)GetNumber(data, "payTime", 0),
                    HasOrderDescription = GetBoolean(data, "hasOrderDescription", false)
                };
            }
            catch (Exception ex)
            {
                KuroLogger.Error(Tag + "_ORDER_DETAIL_ERROR", ex.Message);
                return null;
            }
        }

        // ---------- 内部辅助 ----------

        private static JsonObject GetDataObject(JsonObject json)
        {
            if (json == null) return null;
            if (!json.ContainsKey("data")) return null;
            if (json.GetNamedValue("data").ValueType != JsonValueType.Object) return null;
            return json.GetNamedObject("data");
        }

        private static string GetString(JsonObject obj, string key, string defVal)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            var v = obj.GetNamedValue(key);
            if (v.ValueType == JsonValueType.String) return obj.GetNamedString(key);
            if (v.ValueType == JsonValueType.Number) return v.GetNumber().ToString();
            return defVal;
        }

        private static double GetNumber(JsonObject obj, string key, double defVal)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            var v = obj.GetNamedValue(key);
            if (v.ValueType == JsonValueType.Number) return obj.GetNamedNumber(key);
            if (v.ValueType == JsonValueType.String)
            {
                double d;
                if (double.TryParse(obj.GetNamedString(key), out d)) return d;
            }
            return defVal;
        }

        private static bool GetBoolean(JsonObject obj, string key, bool defVal)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            var v = obj.GetNamedValue(key);
            if (v.ValueType == JsonValueType.Boolean) return obj.GetNamedBoolean(key);
            if (v.ValueType == JsonValueType.String)
            {
                bool b;
                if (bool.TryParse(obj.GetNamedString(key), out b)) return b;
            }
            return defVal;
        }
    }
}
