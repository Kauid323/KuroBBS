using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private int _allRoleColumns;
        private string _cacheSizeSummary = "正在计算缓存大小...";
        private bool _isClearing = false;

        public ObservableCollection<int> ColumnOptions { get; private set; }

        public int AllRoleColumns
        {
            get { return _allRoleColumns; }
            set
            {
                if (value < 2 || value > 5) value = 3;
                if (_allRoleColumns == value) return;
                _allRoleColumns = value;
                SettingsHelper.AllRoleColumns = value;
                OnPropertyChanged();
                OnPropertyChanged("ColumnSummary");
            }
        }

        /// <summary>
        /// 「每行显示数量」——全局列数别名，与 AllRoleColumns 同一个存储 key，
        /// 给 WikiItemListPage / GoldShopPage 等列表页用，名字更贴切。
        /// </summary>
        public int GridColumns
        {
            get { return _allRoleColumns; }
            set { AllRoleColumns = value; }
        }

        public string ColumnSummary
        {
            get { return "列表每行显示 " + AllRoleColumns + " 个（全部角色 / 图鉴与意识手册 / 金币商店）"; }
        }

        public string CacheSizeSummary
        {
            get { return _cacheSizeSummary; }
            set { _cacheSizeSummary = value; OnPropertyChanged(); }
        }

        public bool IsClearing
        {
            get { return _isClearing; }
            set { _isClearing = value; OnPropertyChanged(); }
        }

        public ICommand ClearCacheCommand { get; private set; }

        public SettingsViewModel()
        {
            ColumnOptions = new ObservableCollection<int> { 2, 3, 4, 5 };
            _allRoleColumns = SettingsHelper.AllRoleColumns;

            ClearCacheCommand = new RelayCommand(async () => await ClearCacheAsync());

            var t = RefreshCacheSizeAsync();
        }

        public async Task RefreshCacheSizeAsync()
        {
            long bytes = await KuroImageCache.Instance.GetCacheSizeBytesAsync();
            double mb = bytes / (1024.0 * 1024.0);
            CacheSizeSummary = string.Format("当前图片与数据缓存: {0:F2} MB", mb);
        }

        public async Task ClearCacheAsync()
        {
            if (IsClearing) return;
            IsClearing = true;
            CacheSizeSummary = "正在清理全部缓存...";

            await KuroImageCache.Instance.ClearAllCacheAsync();
            await RefreshCacheSizeAsync();
            IsClearing = false;
            NotificationHelper.ShowNotification("全部本地图片与临时缓存已清空", "清理成功");
        }
    }
}
