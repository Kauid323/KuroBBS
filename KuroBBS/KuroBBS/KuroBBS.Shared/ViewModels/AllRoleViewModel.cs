using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class AllRoleViewModel : ViewModelBase
    {
        public GameRoleCard Role { get; private set; }
        public ObservableCollection<HaruCharacterInfo> Roles { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public int RoleColumns { get; private set; }
        public double CardWidth { get; private set; }
        public double CardHeight { get; private set; }

        public string ServerId { get { return _serverId; } }
        public string RoleId { get { return _roleId; } }

        private string _serverId;
        private string _roleId;
        private double _availableWidth;

        public AllRoleViewModel()
        {
            Roles = new ObservableCollection<HaruCharacterInfo>();
            RoleColumns = SettingsHelper.AllRoleColumns;
            try
            {
                var bounds = Windows.UI.Xaml.Window.Current.Bounds;
                if (bounds.Width > 0)
                {
                    UpdateLayoutWidth(bounds.Width - 32);
                }
                else
                {
                    CardWidth = 108;
                    CardHeight = 146;
                }
            }
            catch
            {
                CardWidth = 108;
                CardHeight = 146;
            }
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
        }

        public void UpdateLayoutWidth(double availableWidth)
        {
            if (availableWidth <= 0) return;
            _availableWidth = availableWidth;
            int cols = Math.Max(1, RoleColumns);
            // Strictly divide availableWidth across cols with floor so all cols fit exactly in 1 row without wrapping early
            double slotWidth = Math.Floor(availableWidth / cols);
            CardWidth = Math.Max(70, slotWidth);
            CardHeight = Math.Floor(CardWidth * 1.35);
            OnPropertyChanged("CardWidth");
            OnPropertyChanged("CardHeight");
        }

        public async Task InitializeAsync(GameRoleCard role)
        {
            Role = role;
            _serverId = role != null ? role.ServerId : "";
            _roleId = role != null ? role.RoleId : "";
            OnPropertyChanged("Role");
            OnPropertyChanged("ServerId");
            OnPropertyChanged("RoleId");
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(_roleId)) return;
            IsBusy = true;
            StatusMessage = "正在加载角色列表...";
            try
            {
                ObservableCollection<HaruCharacterInfo> roles = await HaruRoleService.Instance.GetRoleIndexAsync(_serverId, _roleId);
                Roles.Clear();
                foreach (HaruCharacterInfo role in roles) Roles.Add(role);
                StatusMessage = Roles.Count > 0 ? "" : "暂时没有找到角色数据";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "加载失败，请稍后重试";
                KuroLogger.Error("HARU_ROLE_INDEX", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
