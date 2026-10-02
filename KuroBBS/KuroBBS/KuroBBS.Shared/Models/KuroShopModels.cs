using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KuroBBS.Models
{
    /// <summary>
    /// 库洛金币商城 / 任务中心的数据模型。
    ///
    /// 所有字段均逐一核对自 kuroshop/ 目录下抓到的真实响应，不存在臆造字段：
    ///   [605] /encourage/commodity/getKindInfo
    ///   [606] /encourage/gold/getTotalGold
    ///   [608] /encourage/commodity/list
    ///   [627] /encourage/level/getTaskProcess
    ///   [632] /encourage/gold/getGoldLogs
    ///   [641] /encourage/commodity/detail
    ///   [648] /user/role/findRoleList
    ///   [651] /encourage/order/create
    ///   [653] /encourage/order/findOrderDetail
    /// </summary>

    /// <summary>商品分类：0=全部 2=战双帕弥什 3=鸣潮 1=库街区（顺序以服务端返回为准）。</summary>
    public class ShopKind
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsMust { get; set; }
    }

    public class ShopCommodity
    {
        public string CommodityCode { get; set; }
        public string CommodityName { get; set; }
        public int CommodityPrice { get; set; }
        /// <summary>1=在售（其余视为不可售）。</summary>
        public int CommodityStatus { get; set; }
        public int GameId { get; set; }
        public string GameName { get; set; }
        public bool IsSellout { get; set; }
        public int KindId { get; set; }
        public string KindName { get; set; }
        public string PictureUrl { get; set; }
        public string ProductId { get; set; }
        public int ProductType { get; set; }
        public long SaleTime { get; set; }
        public long TotalStock { get; set; }
        public long TotalSurplusStock { get; set; }

        public string PriceText { get { return CommodityPrice + " 金币"; } }
        public string StockText { get { return "剩 " + TotalSurplusStock; } }
        public bool CanBuy { get { return CommodityStatus == 1 && !IsSellout && TotalSurplusStock > 0; } }
    }

    public class ShopCommodityDetail
    {
        public string CommodityCode { get; set; }
        public string CommodityName { get; set; }
        public string CommodityDesc { get; set; }
        public int CommodityPrice { get; set; }
        public int CommodityStatus { get; set; }
        public int CommodityType { get; set; }
        /// <summary>限购数量（commodityLimit）。</summary>
        public int CommodityLimit { get; set; }
        public int CommodityLimitStrategy { get; set; }
        public int CommodityLimitType { get; set; }
        /// <summary>当前用户已购次数（currentUserLimitBuy）。</summary>
        public int CurrentUserLimitBuy { get; set; }
        public int GameId { get; set; }
        public string GameName { get; set; }
        public int KindId { get; set; }
        public string KindName { get; set; }
        public string PictureUrl { get; set; }
        public string ProductId { get; set; }
        public int ProductType { get; set; }
        public int PropType { get; set; }
        public bool IsSellout { get; set; }
        public long TotalStock { get; set; }
        public long TotalSurplusStock { get; set; }
        public long SaleTime { get; set; }
        public long ShelveTime { get; set; }
        public long OffShelveTime { get; set; }
        public long CreateTime { get; set; }

        public string PriceText { get { return CommodityPrice + " 金币"; } }
        public bool CanBuy { get { return CommodityStatus == 1 && !IsSellout && TotalSurplusStock > 0; } }

        /// <summary>限购文案：只在真的有限购时才显示。</summary>
        public bool HasLimit { get { return CommodityLimit > 0; } }
        public string LimitText
        {
            get
            {
                if (CommodityLimit <= 0) return "";
                return string.Format("限购 {0} 次（已兑 {1}）", CommodityLimit, CurrentUserLimitBuy);
            }
        }

        /// <summary>描述按行拆开，便于逐段展示。</summary>
        public List<string> DescLines
        {
            get
            {
                var lines = new List<string>();
                if (string.IsNullOrEmpty(CommodityDesc)) return lines;
                foreach (var raw in CommodityDesc.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
                {
                    string l = raw.Trim();
                    if (!string.IsNullOrEmpty(l)) lines.Add(l);
                }
                return lines;
            }
        }
    }

    /// <summary>任务中心里的单个任务（growTask / dailyTask 共用）。</summary>
    public class ShopTaskItem
    {
        public string Remark { get; set; }
        public int GainGold { get; set; }
        public int CompleteTimes { get; set; }
        public int NeedActionTimes { get; set; }
        public double Process { get; set; }
        public int Times { get; set; }
        /// <summary>跳转类型，服务端原样下发；0=无跳转。</summary>
        public int SkipType { get; set; }

        public bool IsDone { get { return Process >= 1.0; } }
        public string GoldText { get { return "+" + GainGold; } }
        public string ProgressText
        {
            get { return string.Format("{0}/{1}", CompleteTimes, NeedActionTimes); }
        }
        public string StatusText { get { return IsDone ? "已完成" : "进行中"; } }
    }

    public class ShopTaskProcess
    {
        public int CurrentDailyGold { get; set; }
        public int MaxDailyGold { get; set; }
        public List<ShopTaskItem> GrowTask { get; set; }
        public List<ShopTaskItem> DailyTask { get; set; }

        public bool HasGrowTask { get { return GrowTask != null && GrowTask.Count > 0; } }
        public bool HasDailyTask { get { return DailyTask != null && DailyTask.Count > 0; } }
        public string DailyGoldText { get { return string.Format("{0}/{1}", CurrentDailyGold, MaxDailyGold); } }

        public ShopTaskProcess()
        {
            GrowTask = new List<ShopTaskItem>();
            DailyTask = new List<ShopTaskItem>();
        }
    }

    /// <summary>金币流水（getGoldLogs）。</summary>
    public class GoldLogItem
    {
        public string Remark { get; set; }
        public int Gold { get; set; }
        public long CreateTime { get; set; }
        public bool AllowClick { get; set; }

        public string GoldText { get { return (Gold >= 0 ? "+" : "") + Gold; } }
        public bool IsIncome { get { return Gold >= 0; } }

        public string TimeText
        {
            get
            {
                if (CreateTime <= 0) return "";
                try
                {
                    var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds(CreateTime).ToLocalTime();
                    return dt.ToString("MM-dd HH:mm");
                }
                catch
                {
                    return "";
                }
            }
        }
    }

    /// <summary>下单结果（order/create）。</summary>
    public class ShopOrderResult
    {
        public string OrderCode { get; set; }
        public bool GeeTest { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>订单详情（order/findOrderDetail）。</summary>
    public class ShopOrderDetail
    {
        public string OrderCode { get; set; }
        public string CommodityCode { get; set; }
        public string CommodityName { get; set; }
        public string PictureUrl { get; set; }
        public int CommodityNum { get; set; }
        public int CommodityPrice { get; set; }
        public int CommodityCost { get; set; }
        public int CommodityType { get; set; }
        /// <summary>1=成功。</summary>
        public int OrderStatus { get; set; }
        public string GameName { get; set; }
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string UserId { get; set; }
        public long CreateTime { get; set; }
        public long PayTime { get; set; }
        public bool HasOrderDescription { get; set; }

        public string StatusText { get { return OrderStatus == 1 ? "兑换成功" : "处理中"; } }
        public string CostText { get { return CommodityCost + " 金币"; } }
    }

    /// <summary>游戏角色（user/role/findRoleList），下单时要选一个。</summary>
    public class ShopGameRole
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string GameId { get; set; }
        public string GameHeadUrl { get; set; }
        public string ServerId { get; set; }
        public string ServerName { get; set; }
        public string GameLevel { get; set; }
        public string UserId { get; set; }
        public bool IsDefault { get; set; }

        /// <summary>下拉/列表里显示：「那够吧 · 星火服 Lv.85」。</summary>
        public string DisplayText
        {
            get
            {
                string s = RoleName ?? "";
                if (!string.IsNullOrEmpty(ServerName)) s += " · " + ServerName;
                if (!string.IsNullOrEmpty(GameLevel)) s += " Lv." + GameLevel;
                return s;
            }
        }
    }

    /// <summary>金币余额（getTotalGold）。</summary>
    public class GoldBalance
    {
        public int GoldNum { get; set; }
        public string GoldText { get { return GoldNum.ToString(); } }
    }
}
