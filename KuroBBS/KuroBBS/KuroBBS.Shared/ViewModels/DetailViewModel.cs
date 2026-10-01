using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class DetailViewModel : ViewModelBase
    {
        public GameRoleCard Role { get; private set; }
        public HaruRoleSummary Summary { get; private set; }
        public HaruAccountInfo Account { get; private set; }
        public ObservableCollection<HaruDailyItem> DailyItems { get; private set; }
        public ObservableCollection<HaruCharacterInfo> Characters { get; private set; }
        public HaruFashionInfo Fashion { get; private set; }
        public bool HasData { get { return Account != null && !string.IsNullOrEmpty(Account.RoleName); } }

        public ICommand RefreshCommand { get; private set; }

        private string _serverId;
        private string _roleId;

        public DetailViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            Summary = new HaruRoleSummary();
            Account = new HaruAccountInfo();
            DailyItems = new ObservableCollection<HaruDailyItem>();
            Characters = new ObservableCollection<HaruCharacterInfo>();
            Fashion = new HaruFashionInfo();
        }

        public async Task InitializeAsync(GameRoleCard role)
        {
            Role = role;
            _serverId = role != null ? role.ServerId : "";
            _roleId = role != null ? role.RoleId : "";
            OnPropertyChanged("Role");
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(_roleId)) return;
            IsBusy = true;
            StatusMessage = "正在加载战双角色数据...";
            try
            {
                HaruDetailData data = await HaruRoleService.Instance.GetDetailAsync(_serverId, _roleId);
                Summary = data.Summary;
                Account = data.Account;
                Fashion = data.Fashion;
                DailyItems.Clear();
                foreach (HaruDailyItem item in data.DailyItems) DailyItems.Add(item);
                Characters.Clear();
                foreach (HaruCharacterInfo item in data.Characters) Characters.Add(item);
                OnPropertyChanged("Summary");
                OnPropertyChanged("Account");
                OnPropertyChanged("Fashion");
                OnPropertyChanged("HasData");
                StatusMessage = data.Account != null && !string.IsNullOrEmpty(data.Account.RoleName) ? "" : "暂时没有找到该角色的战绩数据";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "加载失败，请稍后重试";
                KuroLogger.Error("HARU_DETAIL", ex.Message, ex);
            }
            finally { IsBusy = false; }
        }
    }
}
