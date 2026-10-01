using System;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class CharacterDetailViewModel : ViewModelBase
    {
        public CharacterDetailNavParams NavParams { get; private set; }
        public HaruCharacterDetail Detail { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public CharacterDetailViewModel()
        {
            Detail = new HaruCharacterDetail();
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
        }

        public async Task InitializeAsync(CharacterDetailNavParams navParams)
        {
            NavParams = navParams;
            if (navParams != null)
            {
                Detail.Body.BodyName = navParams.BodyName ?? "";
                Detail.Body.RoleName = navParams.RoleName ?? "";
                Detail.Body.IconUrl = navParams.IconUrl ?? "";
                Detail.Body.BodyId = navParams.CharacterId;
                OnPropertyChanged("Detail");
            }
            OnPropertyChanged("NavParams");
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsBusy || NavParams == null || string.IsNullOrEmpty(NavParams.RoleId)) return;
            IsBusy = true;
            StatusMessage = "正在加载构造体详情...";
            try
            {
                HaruCharacterDetail detail = await HaruRoleService.Instance.GetCharacterDetailAsync(
                    NavParams.ServerId,
                    NavParams.RoleId,
                    NavParams.CharacterId);

                if (detail != null && detail.Body != null && detail.Body.BodyId > 0)
                {
                    Detail = detail;
                    OnPropertyChanged("Detail");
                    StatusMessage = "";
                }
                else
                {
                    StatusMessage = "未获取到角色详细数据";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "详情加载失败，请稍后重试";
                KuroLogger.Error("HARU_ROLE_DETAIL", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
