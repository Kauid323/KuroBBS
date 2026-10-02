using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    /// <summary>
    /// 金币商店页。
    /// 分类来自服务端 getKindInfo（0=全部 2=战双帕弥什 3=鸣潮 1=库街区），
    /// 不做客户端硬编码 —— 服务端将来加减分类这里自动跟随。
    /// </summary>
    public class GoldShopViewModel : ViewModelBase
    {
        public ObservableCollection<ShopKind> Kinds { get; private set; }
        public ObservableCollection<ShopCommodity> Commodities { get; private set; }

        private int _goldNum;
        public int GoldNum
        {
            get { return _goldNum; }
            set { _goldNum = value; OnPropertyChanged(); OnPropertyChanged("GoldText"); }
        }

        public string GoldText { get { return _goldNum.ToString(); } }

        private ShopKind _selectedKind;
        public ShopKind SelectedKind
        {
            get { return _selectedKind; }
            set
            {
                if (_selectedKind == value) return;
                _selectedKind = value;
                OnPropertyChanged();
                // 换分类就重新拉列表
                var ignore = LoadCommoditiesAsync(true);
            }
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get { return _isEmpty; }
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }

        private int _pageIndex = 1;
        private const int PageSize = 20;
        private bool _hasMore = true;
        private bool _loadingMore;

        public bool HasMore
        {
            get { return _hasMore; }
            set { _hasMore = value; OnPropertyChanged(); }
        }

        /// <summary>每行列数，来自设置（商品列表也走它，和图鉴/意识手册一致）。</summary>
        public int GridColumns { get; private set; }

        private double _itemWidth;
        public double ItemWidth
        {
            get { return _itemWidth; }
            set { _itemWidth = value; OnPropertyChanged(); }
        }

        private double _itemHeight;
        public double ItemHeight
        {
            get { return _itemHeight; }
            set { _itemHeight = value; OnPropertyChanged(); }
        }

        public GoldShopViewModel()
        {
            Kinds = new ObservableCollection<ShopKind>();
            Commodities = new ObservableCollection<ShopCommodity>();
            RefreshCommand = new RelayCommand(async p => await LoadAllAsync());
            LoadMoreCommand = new RelayCommand(async p => await LoadCommoditiesAsync(false));

            GridColumns = SettingsHelper.GridColumns;
            // 商品卡片宽高比 ≈ 1.26（图 110 + 名/价/库存 ~105），接近旧 170x215
            const double aspect = 215.0 / 170.0;
            try
            {
                var bounds = Windows.UI.Xaml.Window.Current.Bounds;
                double w = bounds.Width > 0 ? bounds.Width - 32 : 0;
                UpdateLayoutWidth(w, aspect);
            }
            catch
            {
                UpdateLayoutWidth(432, aspect);
            }
        }

        public void UpdateLayoutWidth(double availableWidth, double aspect = 215.0 / 170.0)
        {
            if (availableWidth <= 0) return;
            GridColumns = SettingsHelper.GridColumns;
            OnPropertyChanged("GridColumns");

            double w = GridLayoutHelper.CalcItemWidth(availableWidth, GridColumns, 140);
            ItemWidth = w;
            ItemHeight = GridLayoutHelper.CalcItemHeight(w, aspect);
        }

        /// <summary>首次进入：拉分类 + 余额 + 第一个分类的商品。</summary>
        public async Task LoadAllAsync()
        {
            StatusMessage = "";
            IsBusy = true;
            try
            {
                await LoadKindsAsync();
                var goldTask = KuroShopService.Instance.GetTotalGoldAsync();
                var listTask = LoadCommoditiesAsync(true);
                await goldTask;
                GoldNum = goldTask.Result;
                await listTask;
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

        private async Task LoadKindsAsync()
        {
            var kinds = await KuroShopService.Instance.GetKindInfoAsync();
            if (kinds == null || kinds.Count == 0)
            {
                // 服务端没给分类时兜一个「全部」，避免页面空白
                kinds = new List<ShopKind> { new ShopKind { Id = 0, Name = "全部", IsMust = true } };
            }

            Kinds.Clear();
            foreach (var k in kinds) Kinds.Add(k);

            if (_selectedKind == null)
            {
                _selectedKind = Kinds[0];
                OnPropertyChanged("SelectedKind");
            }
        }

        /// <summary>refresh=true 重新从第一页开始；false 为加载更多。</summary>
        public async Task LoadCommoditiesAsync(bool refresh)
        {
            if (_loadingMore) return;
            if (_selectedKind == null) return;

            if (refresh)
            {
                _pageIndex = 1;
                _hasMore = true;
                Commodities.Clear();
            }
            else if (!_hasMore)
            {
                return;
            }

            _loadingMore = true;
            IsBusy = true;
            try
            {
                var list = await KuroShopService.Instance.GetCommodityListAsync(_selectedKind.Id, _pageIndex, PageSize);
                if (list == null) list = new List<ShopCommodity>();

                foreach (var c in list) Commodities.Add(c);

                // 返回数不足一页即视为没有更多
                HasMore = list.Count >= PageSize;
                if (list.Count > 0) _pageIndex++;

                IsEmpty = Commodities.Count == 0;
            }
            catch (Exception ex)
            {
                StatusMessage = "加载商品失败：" + ex.Message;
            }
            finally
            {
                _loadingMore = false;
                IsBusy = false;
            }
        }

        /// <summary>只刷新余额（下单返回后调用，避免整页重载）。</summary>
        public async Task RefreshGoldAsync()
        {
            GoldNum = await KuroShopService.Instance.GetTotalGoldAsync();
        }
    }
}
