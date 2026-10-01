using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class TeamRecommendationViewModel : ViewModelBase
    {
        private HaruTeamNavParams _navParams;
        public HaruTeamNavParams NavParams
        {
            get { return _navParams; }
            set { _navParams = value; OnPropertyChanged(); }
        }

        private ObservableCollection<HaruTeamFilterCharacter> _filterCharacters;
        public ObservableCollection<HaruTeamFilterCharacter> FilterCharacters
        {
            get { return _filterCharacters; }
            set { _filterCharacters = value; OnPropertyChanged(); }
        }

        private HaruTeamFilterCharacter _selectedCharacter;
        public HaruTeamFilterCharacter SelectedCharacter
        {
            get { return _selectedCharacter; }
            set
            {
                if (_selectedCharacter != value)
                {
                    _selectedCharacter = value;
                    OnPropertyChanged();
                    OnPropertyChanged("CharacterDisplayName");
                }
            }
        }

        public string CharacterDisplayName
        {
            get
            {
                if (SelectedCharacter != null) return SelectedCharacter.DisplayName;
                if (NavParams != null)
                {
                    if (!string.IsNullOrEmpty(NavParams.CharacterName) && !string.IsNullOrEmpty(NavParams.BodyName) && NavParams.CharacterName != NavParams.BodyName)
                        return NavParams.CharacterName + " · " + NavParams.BodyName;
                    return NavParams.BodyName ?? NavParams.CharacterName ?? "构造体";
                }
                return "构造体";
            }
        }

        private ObservableCollection<HaruTeamItem> _teams;
        public ObservableCollection<HaruTeamItem> Teams
        {
            get { return _teams; }
            set { _teams = value; OnPropertyChanged(); }
        }

        private int _sortType = 0; // 0: 推荐/最热, 1: 最新
        public int SortType
        {
            get { return _sortType; }
            set { _sortType = value; OnPropertyChanged(); OnPropertyChanged("SortTypeText"); }
        }

        public string SortTypeText
        {
            get { return _sortType == 0 ? "最热" : "最新"; }
        }

        private int _modeType = 0; // 0: 全部/常规, 1: 纷争战区, 2: 幻痛囚笼
        public int ModeType
        {
            get { return _modeType; }
            set
            {
                if (_modeType != value)
                {
                    _modeType = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsModeAll");
                    OnPropertyChanged("IsModeWarZone");
                    OnPropertyChanged("IsModeBoss");
                }
            }
        }

        public bool IsModeAll { get { return _modeType == 0; } }
        public bool IsModeWarZone { get { return _modeType == 1; } }
        public bool IsModeBoss { get { return _modeType == 2; } }

        private bool _isLoadingMore;
        public bool IsLoadingMore
        {
            get { return _isLoadingMore; }
            set { _isLoadingMore = value; OnPropertyChanged(); }
        }

        private bool _hasMore = true;
        public bool HasMore
        {
            get { return _hasMore; }
            set { _hasMore = value; OnPropertyChanged(); }
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get { return _currentPage; }
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }

        public TeamRecommendationViewModel()
        {
            Teams = new ObservableCollection<HaruTeamItem>();
            FilterCharacters = new ObservableCollection<HaruTeamFilterCharacter>();
            RefreshCommand = new RelayCommand(async () => await LoadDataAsync(true));
            LoadMoreCommand = new RelayCommand(async () => await LoadMoreAsync());
        }

        public async Task InitializeAsync(HaruTeamNavParams navParams)
        {
            NavParams = navParams;
            CurrentPage = 1;
            HasMore = true;
            StatusMessage = null;

            KuroLogger.Loading("TEAM_INIT", string.Format("Initializing Team Recommendations for role: {0}, charId: {1}", 
                navParams != null ? navParams.RoleId : "", 
                navParams != null ? navParams.CharacterId : 0));

            // Load filter characters in background
            if (FilterCharacters.Count == 0)
            {
                try
                {
                    var chars = await HaruRoleService.Instance.GetTeamFilterCharactersAsync(
                        navParams.ServerId, 
                        navParams.RoleId, 
                        navParams.UserId);

                    FilterCharacters.Clear();
                    foreach (var c in chars)
                    {
                        FilterCharacters.Add(c);
                    }

                    if (SelectedCharacter == null && navParams.CharacterId > 0)
                    {
                        SelectedCharacter = FilterCharacters.FirstOrDefault(c => c.CharacterId == navParams.CharacterId);
                    }
                }
                catch (Exception ex)
                {
                    KuroLogger.Error("TEAM_FILTER_ERROR", "Failed to load filter characters: " + ex.Message);
                }
            }

            await LoadDataAsync(false);
        }

        public async Task LoadDataAsync(bool isRefresh = false)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = null;

            if (isRefresh)
            {
                CurrentPage = 1;
                HasMore = true;
            }

            try
            {
                int charId = SelectedCharacter != null ? SelectedCharacter.CharacterId : (NavParams != null ? NavParams.CharacterId : 0);
                string serverId = NavParams != null ? NavParams.ServerId : "";
                string roleId = NavParams != null ? NavParams.RoleId : "";
                string userId = NavParams != null ? NavParams.UserId : "";

                var result = await HaruRoleService.Instance.GetTeamListAsync(
                    serverId,
                    roleId,
                    userId,
                    charId,
                    CurrentPage,
                    SortType,
                    ModeType,
                    1);

                if (isRefresh || CurrentPage == 1)
                {
                    Teams.Clear();
                }

                if (result != null && result.List != null)
                {
                    foreach (var team in result.List)
                    {
                        Teams.Add(team);
                    }

                    if (result.List.Count == 0)
                    {
                        HasMore = false;
                        if (Teams.Count == 0)
                        {
                            StatusMessage = "暂无该角色编队推荐方案";
                        }
                    }
                    else
                    {
                        HasMore = result.List.Count >= 5;
                    }
                }
                else
                {
                    HasMore = false;
                    if (Teams.Count == 0)
                    {
                        StatusMessage = "暂无该角色编队推荐方案";
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("TEAM_LOAD_ERROR", "Failed to load team list: " + ex.Message);
                StatusMessage = "加载编队方案失败: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadMoreAsync()
        {
            if (IsBusy || IsLoadingMore || !HasMore) return;
            IsLoadingMore = true;

            try
            {
                CurrentPage++;
                int charId = SelectedCharacter != null ? SelectedCharacter.CharacterId : (NavParams != null ? NavParams.CharacterId : 0);
                string serverId = NavParams != null ? NavParams.ServerId : "";
                string roleId = NavParams != null ? NavParams.RoleId : "";
                string userId = NavParams != null ? NavParams.UserId : "";

                var result = await HaruRoleService.Instance.GetTeamListAsync(
                    serverId,
                    roleId,
                    userId,
                    charId,
                    CurrentPage,
                    SortType,
                    ModeType,
                    1);

                if (result != null && result.List != null && result.List.Count > 0)
                {
                    foreach (var team in result.List)
                    {
                        Teams.Add(team);
                    }
                    HasMore = result.List.Count >= 5;
                }
                else
                {
                    HasMore = false;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("TEAM_LOADMORE_ERROR", "Failed to load more teams: " + ex.Message);
                CurrentPage--;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        public async Task SelectCharacterAsync(HaruTeamFilterCharacter character)
        {
            if (character == null) return;
            SelectedCharacter = character;
            CurrentPage = 1;
            HasMore = true;
            await LoadDataAsync(true);
        }

        public async Task SwitchModeAsync(int modeType)
        {
            if (ModeType == modeType) return;
            ModeType = modeType;
            CurrentPage = 1;
            HasMore = true;
            await LoadDataAsync(true);
        }

        public async Task ToggleSortAsync()
        {
            SortType = SortType == 0 ? 1 : 0;
            CurrentPage = 1;
            HasMore = true;
            await LoadDataAsync(true);
        }
    }
}
