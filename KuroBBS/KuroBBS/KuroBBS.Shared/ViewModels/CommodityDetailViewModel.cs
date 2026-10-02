using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    /// <summary>
    /// 商品详情页 + 下单。
    ///
    /// 购买流程（严格按 kuroshop 抓到的真实调用顺序）：
    ///   1. /encourage/commodity/detail         取详情与限购
    ///   2. /user/role/findRoleList?gameId=     取该游戏下的角色，用户选一个
    ///   3. /encourage/order/create             下单
    ///   4. /encourage/order/findOrderDetail    拉订单结果展示
    /// </summary>
    public class CommodityDetailViewModel : ViewModelBase
    {
        public ObservableCollection<ShopGameRole> Roles { get; private set; }

        private string _commodityCode;
        public string CommodityCode
        {
            get { return _commodityCode; }
            set { _commodityCode = value; OnPropertyChanged(); }
        }

        private ShopCommodityDetail _detail;
        public ShopCommodityDetail Detail
        {
            get { return _detail; }
            set
            {
                _detail = value;
                OnPropertyChanged();
                OnPropertyChanged("HasDetail");
                OnPropertyChanged("Name");
                OnPropertyChanged("PriceText");
                OnPropertyChanged("PictureUrl");
                OnPropertyChanged("GameName");
                OnPropertyChanged("DescLines");
                OnPropertyChanged("LimitText");
                OnPropertyChanged("HasLimit");
                OnPropertyChanged("CanBuy");
            }
        }

        private ShopGameRole _selectedRole;
        public ShopGameRole SelectedRole
        {
            get { return _selectedRole; }
            set { _selectedRole = value; OnPropertyChanged(); }
        }

        private ShopOrderDetail _order;
        public ShopOrderDetail Order
        {
            get { return _order; }
            set { _order = value; OnPropertyChanged(); OnPropertyChanged("HasOrder"); }
        }

        private int _buyNum = 1;
        public int BuyNum
        {
            get { return _buyNum; }
            set
            {
                if (value < 1) value = 1;
                _buyNum = value;
                OnPropertyChanged();
                OnPropertyChanged("TotalPriceText");
            }
        }

        private bool _isBuying;
        public bool IsBuying
        {
            get { return _isBuying; }
            set { _isBuying = value; OnPropertyChanged(); }
        }

        public bool HasDetail { get { return _detail != null; } }
        public bool HasOrder { get { return _order != null; } }
        public string Name { get { return _detail != null ? _detail.CommodityName : ""; } }
        public string PriceText { get { return _detail != null ? _detail.PriceText : ""; } }
        public string PictureUrl { get { return _detail != null ? _detail.PictureUrl : ""; } }
        public string GameName { get { return _detail != null ? _detail.GameName : ""; } }
        public bool HasLimit { get { return _detail != null && _detail.HasLimit; } }
        public string LimitText { get { return _detail != null ? _detail.LimitText : ""; } }
        public bool CanBuy { get { return _detail != null && _detail.CanBuy; } }
        public System.Collections.Generic.List<string> DescLines
        {
            get { return _detail != null ? _detail.DescLines : new System.Collections.Generic.List<string>(); }
        }
        public string TotalPriceText
        {
            get
            {
                if (_detail == null) return "";
                return (_detail.CommodityPrice * _buyNum) + " 金币";
            }
        }

        public ICommand BuyCommand { get; private set; }

        public CommodityDetailViewModel()
        {
            Roles = new ObservableCollection<ShopGameRole>();
            BuyCommand = new RelayCommand(async p => await BuyAsync());
        }

        public async Task LoadAsync(string commodityCode)
        {
            CommodityCode = commodityCode;
            StatusMessage = "";
            Order = null;
            IsBusy = true;
            try
            {
                Detail = await KuroShopService.Instance.GetCommodityDetailAsync(commodityCode);
                if (Detail == null)
                {
                    StatusMessage = "商品信息加载失败";
                    return;
                }

                var roles = await KuroShopService.Instance.FindRoleListAsync(Detail.GameId);
                Roles.Clear();
                if (roles != null)
                {
                    foreach (var r in roles) Roles.Add(r);
                }

                // 默认选中服务端标记的默认角色，否则第一个
                ShopGameRole pick = null;
                foreach (var r in Roles)
                {
                    if (r.IsDefault) { pick = r; break; }
                }
                SelectedRole = pick ?? (Roles.Count > 0 ? Roles[0] : null);

                if (Roles.Count == 0)
                {
                    StatusMessage = "未绑定该游戏角色，请先去绑定游戏角色";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载失败：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task BuyAsync()
        {
            if (Detail == null) return;
            if (IsBuying) return;

            if (SelectedRole == null)
            {
                StatusMessage = "请先选择接收奖励的游戏角色";
                return;
            }

            IsBuying = true;
            StatusMessage = "";
            try
            {
                var result = await KuroShopService.Instance.CreateOrderAsync(
                    Detail.CommodityCode, _buyNum, SelectedRole.RoleId, Detail.GameId);

                if (!result.Success || string.IsNullOrEmpty(result.OrderCode))
                {
                    // 服务端会把「金币不足 / 已达限购 / 已兑完」等原因放在 msg 里
                    StatusMessage = string.IsNullOrEmpty(result.Message)
                        ? "兑换失败" : result.Message;
                    return;
                }

                Order = await KuroShopService.Instance.FindOrderDetailAsync(result.OrderCode);
                if (Order == null)
                {
                    StatusMessage = "兑换成功，但订单详情拉取失败（订单号 " + result.OrderCode + "）";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "兑换异常：" + ex.Message;
            }
            finally
            {
                IsBuying = false;
            }
        }
    }
}
