using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class SearchViewModel : ViewModelBase
    {
        private const string KeyRecentSearches = "Kuro_RecentSearches";

        private int _gameId = 2;
        public int GameId
        {
            get { return _gameId; }
            set
            {
                if (_gameId != value)
                {
                    _gameId = value;
                    OnPropertyChanged();
                    OnPropertyChanged("GameName");
                    OnPropertyChanged("IsZhanshuang");
                    OnPropertyChanged("IsMingchao");
                }
            }
        }

        public string GameName { get { return GameId == 3 ? "鸣潮" : "战双"; } }
        public bool IsZhanshuang { get { return GameId == 2; } }
        public bool IsMingchao { get { return GameId == 3; } }

        private string _keyword;
        public string Keyword
        {
            get { return _keyword; }
            set
            {
                _keyword = value;
                OnPropertyChanged();
                OnPropertyChanged("HasKeyword");
            }
        }

        public bool HasKeyword { get { return !string.IsNullOrWhiteSpace(_keyword); } }

        private string _defaultWord;
        public string DefaultWord
        {
            get { return _defaultWord; }
            set { _defaultWord = value; OnPropertyChanged(); }
        }

        private bool _hasSearched;
        public bool HasSearched
        {
            get { return _hasSearched; }
            set
            {
                _hasSearched = value;
                OnPropertyChanged();
                OnPropertyChanged("ShowPreSearch");
                OnPropertyChanged("ShowResults");
            }
        }

        public bool ShowPreSearch { get { return !_hasSearched; } }
        public bool ShowResults { get { return _hasSearched; } }

        private int _selectedTabIndex = 0; // 0:综合, 1:攻略, 2:同人, 3:Wiki, 4:话题, 5:用户
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsTabComposite");
                    OnPropertyChanged("IsTabStrategy");
                    OnPropertyChanged("IsTabFanart");
                    OnPropertyChanged("IsTabWiki");
                    OnPropertyChanged("IsTabTopic");
                    OnPropertyChanged("IsTabUser");

                    if (HasSearched && !string.IsNullOrEmpty(Keyword))
                    {
                        var ignore = EnsureTabLoadedAsync(_selectedTabIndex);
                    }
                }
            }
        }

        public bool IsTabComposite { get { return _selectedTabIndex == 0; } }
        public bool IsTabStrategy { get { return _selectedTabIndex == 1; } }
        public bool IsTabFanart { get { return _selectedTabIndex == 2; } }
        public bool IsTabWiki { get { return _selectedTabIndex == 3; } }
        public bool IsTabTopic { get { return _selectedTabIndex == 4; } }
        public bool IsTabUser { get { return _selectedTabIndex == 5; } }

        // Filter Drawer fields
        private bool _isFilterDrawerOpen;
        public bool IsFilterDrawerOpen
        {
            get { return _isFilterDrawerOpen; }
            set { _isFilterDrawerOpen = value; OnPropertyChanged(); }
        }

        private int _filterSort = 1; // 1: 综合, 2: 最多点赞, 3: 最多收藏, 4: 最新发布
        public int FilterSort
        {
            get { return _filterSort; }
            set
            {
                _filterSort = value;
                OnPropertyChanged();
                OnPropertyChanged("IsSortGeneral");
                OnPropertyChanged("IsSortLikes");
                OnPropertyChanged("IsSortCollects");
                OnPropertyChanged("IsSortNewest");
            }
        }

        public bool IsSortGeneral { get { return _filterSort == 1; } }
        public bool IsSortLikes { get { return _filterSort == 2; } }
        public bool IsSortCollects { get { return _filterSort == 3; } }
        public bool IsSortNewest { get { return _filterSort == 4; } }

        private int _filterPostType = 0; // 0: 全部, 1: 长图文, 2: 视频
        public int FilterPostType
        {
            get { return _filterPostType; }
            set
            {
                _filterPostType = value;
                OnPropertyChanged();
                OnPropertyChanged("IsPostTypeAll");
                OnPropertyChanged("IsPostTypeArticle");
                OnPropertyChanged("IsPostTypeVideo");
            }
        }

        public bool IsPostTypeAll { get { return _filterPostType == 0; } }
        public bool IsPostTypeArticle { get { return _filterPostType == 1; } }
        public bool IsPostTypeVideo { get { return _filterPostType == 2; } }

        private int _filterTime = 0; // 0: 全部, 1: 1天内, 3: 3天内, 7: 7天内, 30: 30天内
        public int FilterTime
        {
            get { return _filterTime; }
            set
            {
                _filterTime = value;
                OnPropertyChanged();
                OnPropertyChanged("IsTimeAll");
                OnPropertyChanged("IsTime1Day");
                OnPropertyChanged("IsTime3Days");
                OnPropertyChanged("IsTime7Days");
                OnPropertyChanged("IsTime30Days");
            }
        }

        public bool IsTimeAll { get { return _filterTime == 0; } }
        public bool IsTime1Day { get { return _filterTime == 1; } }
        public bool IsTime3Days { get { return _filterTime == 3; } }
        public bool IsTime7Days { get { return _filterTime == 7; } }
        public bool IsTime30Days { get { return _filterTime == 30; } }

        // Collections
        public ObservableCollection<string> RecentSearches { get; private set; }
        public ObservableCollection<HotSearchItem> HotSearchList { get; private set; }
        public ObservableCollection<TopicItem> TopicHotList { get; private set; }

        public ObservableCollection<PostItem> CompositePosts { get; private set; }
        public ObservableCollection<WikiSearchItem> CompositeWikis { get; private set; }
        public ObservableCollection<PostItem> StrategyPosts { get; private set; }
        public ObservableCollection<PostItem> FanartPosts { get; private set; }
        public ObservableCollection<WikiSearchItem> WikiResults { get; private set; }
        public ObservableCollection<TopicItem> TopicResults { get; private set; }
        public ObservableCollection<UserSearchItem> UserResults { get; private set; }

        public bool HasRecentSearches { get { return RecentSearches != null && RecentSearches.Count > 0; } }
        public bool HasCompositeWikis { get { return CompositeWikis != null && CompositeWikis.Count > 0; } }

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

        private readonly int[] _pagePerTab = new int[] { 1, 1, 1, 1, 1, 1 };
        private readonly bool[] _hasMorePerTab = new bool[] { true, true, true, true, true, true };

        public ICommand SearchCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public SearchViewModel()
        {
            RecentSearches = new ObservableCollection<string>();
            HotSearchList = new ObservableCollection<HotSearchItem>();
            TopicHotList = new ObservableCollection<TopicItem>();

            CompositePosts = new ObservableCollection<PostItem>();
            CompositeWikis = new ObservableCollection<WikiSearchItem>();
            StrategyPosts = new ObservableCollection<PostItem>();
            FanartPosts = new ObservableCollection<PostItem>();
            WikiResults = new ObservableCollection<WikiSearchItem>();
            TopicResults = new ObservableCollection<TopicItem>();
            UserResults = new ObservableCollection<UserSearchItem>();

            SearchCommand = new RelayCommand(async () => await ExecuteSearchAsync(null));
            RefreshCommand = new RelayCommand(async () => await ExecuteSearchAsync(Keyword));

            LoadRecentSearches();
        }

        public async Task InitializeAsync(int initialGameId)
        {
            GameId = initialGameId > 0 ? initialGameId : 2;
            LoadRecentSearches();
            await LoadPreSearchDataAsync();
        }

        public async Task SwitchGameAsync(int newGameId)
        {
            if (GameId == newGameId) return;
            GameId = newGameId;
            await LoadPreSearchDataAsync();
            if (HasSearched && !string.IsNullOrEmpty(Keyword))
            {
                await ExecuteSearchAsync(Keyword);
            }
        }

        public async Task LoadPreSearchDataAsync()
        {
            try
            {
                var configTask = KuroForumService.Instance.GetSearchConfigAsync(GameId);
                var topicTask = KuroForumService.Instance.GetTopicHotListAsync(GameId, 1, 20);

                await Task.WhenAll(configTask, topicTask);

                var config = configTask.Result;
                DefaultWord = config != null ? config.DefaultWord : "搜索帖子/用户/话题";

                HotSearchList.Clear();
                if (config != null && config.SearchList != null)
                {
                    foreach (var item in config.SearchList)
                    {
                        HotSearchList.Add(item);
                    }
                }

                TopicHotList.Clear();
                var topics = topicTask.Result;
                if (topics != null)
                {
                    foreach (var topic in topics)
                    {
                        TopicHotList.Add(topic);
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("SEARCH_PRE_ERROR", "Failed to load pre-search data: " + ex.Message);
            }
        }

        public async Task ExecuteSearchAsync(string query = null)
        {
            if (query != null)
            {
                Keyword = query;
            }

            string actualQuery = Keyword;
            if (string.IsNullOrWhiteSpace(actualQuery))
            {
                actualQuery = DefaultWord;
                Keyword = actualQuery;
            }

            if (string.IsNullOrWhiteSpace(actualQuery)) return;

            AddRecentSearch(actualQuery);
            HasSearched = true;

            // Invalidate all tabs so they refresh on demand
            CompositePosts.Clear();
            CompositeWikis.Clear();
            StrategyPosts.Clear();
            FanartPosts.Clear();
            WikiResults.Clear();
            TopicResults.Clear();
            UserResults.Clear();

            for (int i = 0; i < 6; i++)
            {
                _pagePerTab[i] = 1;
                _hasMorePerTab[i] = true;
            }

            await LoadTabResultsAsync(true);
        }

        public async Task EnsureTabLoadedAsync(int tabIndex)
        {
            if (!HasSearched || string.IsNullOrEmpty(Keyword)) return;

            bool needLoad = false;
            switch (tabIndex)
            {
                case 0:
                    needLoad = (CompositePosts.Count == 0 && CompositeWikis.Count == 0);
                    break;
                case 1:
                    needLoad = (StrategyPosts.Count == 0);
                    break;
                case 2:
                    needLoad = (FanartPosts.Count == 0);
                    break;
                case 3:
                    needLoad = (WikiResults.Count == 0);
                    break;
                case 4:
                    needLoad = (TopicResults.Count == 0);
                    break;
                case 5:
                    needLoad = (UserResults.Count == 0);
                    break;
            }

            if (needLoad)
            {
                _pagePerTab[tabIndex] = 1;
                _hasMorePerTab[tabIndex] = true;
                await LoadTabResultsAsync(true);
            }
            else
            {
                HasMore = _hasMorePerTab[tabIndex];
            }
        }

        public async Task SwitchTabAsync(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex > 5) return;
            SelectedTabIndex = tabIndex;
            await EnsureTabLoadedAsync(tabIndex);
        }

        public async Task LoadTabResultsAsync(bool isRefresh = false)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = null;

            int tab = SelectedTabIndex;
            if (isRefresh)
            {
                _pagePerTab[tab] = 1;
                _hasMorePerTab[tab] = true;
            }

            int page = _pagePerTab[tab];
            string query = Keyword ?? "";

            try
            {
                switch (tab)
                {
                    case 0: // 综合
                        {
                            var result = await KuroForumService.Instance.SearchCompositeAsync(GameId, query, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (isRefresh || page == 1)
                            {
                                CompositePosts.Clear();
                                CompositeWikis.Clear();
                                if (result.Wikis != null)
                                {
                                    foreach (var w in result.Wikis) CompositeWikis.Add(w);
                                }
                                OnPropertyChanged("HasCompositeWikis");
                            }

                            if (result.Posts != null)
                            {
                                foreach (var p in result.Posts) CompositePosts.Add(p);
                                _hasMorePerTab[0] = result.HasNext && result.Posts.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[0] = false;
                            }

                            HasMore = _hasMorePerTab[0];

                            if (CompositePosts.Count == 0 && CompositeWikis.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的综合内容";
                            break;
                        }
                    case 1: // 攻略 (forumType=2)
                        {
                            var list = await KuroForumService.Instance.SearchPostsAsync(GameId, query, 2, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (isRefresh || page == 1) StrategyPosts.Clear();

                            if (list != null && list.Count > 0)
                            {
                                foreach (var p in list) StrategyPosts.Add(p);
                                _hasMorePerTab[1] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[1] = false;
                            }

                            HasMore = _hasMorePerTab[1];

                            if (StrategyPosts.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的攻略";
                            break;
                        }
                    case 2: // 同人 (forumType=1)
                        {
                            var list = await KuroForumService.Instance.SearchPostsAsync(GameId, query, 1, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (isRefresh || page == 1) FanartPosts.Clear();

                            if (list != null && list.Count > 0)
                            {
                                foreach (var p in list) FanartPosts.Add(p);
                                _hasMorePerTab[2] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[2] = false;
                            }

                            HasMore = _hasMorePerTab[2];

                            if (FanartPosts.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的同人内容";
                            break;
                        }
                    case 3: // Wiki
                        {
                            var list = await KuroForumService.Instance.SearchWikiAsync(GameId, query, page, 20);
                            if (isRefresh || page == 1) WikiResults.Clear();

                            if (list != null && list.Count > 0)
                            {
                                foreach (var w in list) WikiResults.Add(w);
                                _hasMorePerTab[3] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[3] = false;
                            }

                            HasMore = _hasMorePerTab[3];

                            if (WikiResults.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的Wiki词条";
                            break;
                        }
                    case 4: // 话题
                        {
                            var list = await KuroForumService.Instance.SearchTopicsAsync(GameId, query, page, 20);
                            if (isRefresh || page == 1) TopicResults.Clear();

                            if (list != null && list.Count > 0)
                            {
                                foreach (var t in list) TopicResults.Add(t);
                                _hasMorePerTab[4] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[4] = false;
                            }

                            HasMore = _hasMorePerTab[4];

                            if (TopicResults.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的话题";
                            break;
                        }
                    case 5: // 用户
                        {
                            var list = await KuroForumService.Instance.SearchUsersAsync(GameId, query, page, 20);
                            if (isRefresh || page == 1) UserResults.Clear();

                            if (list != null && list.Count > 0)
                            {
                                foreach (var u in list) UserResults.Add(u);
                                _hasMorePerTab[5] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[5] = false;
                            }

                            HasMore = _hasMorePerTab[5];

                            if (UserResults.Count == 0)
                                StatusMessage = "未找到与「" + query + "」相关的用户";
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("SEARCH_TAB_ERROR", "Failed to load tab " + tab + ": " + ex.Message);
                StatusMessage = "搜索失败: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadMoreAsync()
        {
            int tab = SelectedTabIndex;
            if (IsBusy || IsLoadingMore || !_hasMorePerTab[tab] || !HasSearched) return;
            IsLoadingMore = true;

            try
            {
                _pagePerTab[tab]++;
                int page = _pagePerTab[tab];
                string query = Keyword ?? "";

                switch (tab)
                {
                    case 0: // 综合
                        {
                            var result = await KuroForumService.Instance.SearchCompositeAsync(GameId, query, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (result.Posts != null && result.Posts.Count > 0)
                            {
                                foreach (var p in result.Posts) CompositePosts.Add(p);
                                _hasMorePerTab[0] = result.HasNext && result.Posts.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[0] = false;
                            }
                            HasMore = _hasMorePerTab[0];
                            break;
                        }
                    case 1: // 攻略
                        {
                            var list = await KuroForumService.Instance.SearchPostsAsync(GameId, query, 2, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var p in list) StrategyPosts.Add(p);
                                _hasMorePerTab[1] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[1] = false;
                            }
                            HasMore = _hasMorePerTab[1];
                            break;
                        }
                    case 2: // 同人
                        {
                            var list = await KuroForumService.Instance.SearchPostsAsync(GameId, query, 1, FilterSort, FilterPostType, FilterTime, page, 20);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var p in list) FanartPosts.Add(p);
                                _hasMorePerTab[2] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[2] = false;
                            }
                            HasMore = _hasMorePerTab[2];
                            break;
                        }
                    case 3: // Wiki
                        {
                            var list = await KuroForumService.Instance.SearchWikiAsync(GameId, query, page, 20);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var w in list) WikiResults.Add(w);
                                _hasMorePerTab[3] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[3] = false;
                            }
                            HasMore = _hasMorePerTab[3];
                            break;
                        }
                    case 4: // 话题
                        {
                            var list = await KuroForumService.Instance.SearchTopicsAsync(GameId, query, page, 20);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var t in list) TopicResults.Add(t);
                                _hasMorePerTab[4] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[4] = false;
                            }
                            HasMore = _hasMorePerTab[4];
                            break;
                        }
                    case 5: // 用户
                        {
                            var list = await KuroForumService.Instance.SearchUsersAsync(GameId, query, page, 20);
                            if (list != null && list.Count > 0)
                            {
                                foreach (var u in list) UserResults.Add(u);
                                _hasMorePerTab[5] = list.Count >= 20;
                            }
                            else
                            {
                                _hasMorePerTab[5] = false;
                            }
                            HasMore = _hasMorePerTab[5];
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("SEARCH_LOADMORE_ERROR", "Load more failed: " + ex.Message);
                _pagePerTab[tab]--;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        public void ResetFilters()
        {
            FilterSort = 1;
            FilterPostType = 0;
            FilterTime = 0;
        }

        public async Task ApplyFiltersAsync()
        {
            IsFilterDrawerOpen = false;
            if (HasSearched && !string.IsNullOrEmpty(Keyword))
            {
                // Invalidate all tabs
                CompositePosts.Clear();
                CompositeWikis.Clear();
                StrategyPosts.Clear();
                FanartPosts.Clear();
                WikiResults.Clear();
                TopicResults.Clear();
                UserResults.Clear();

                for (int i = 0; i < 6; i++)
                {
                    _pagePerTab[i] = 1;
                    _hasMorePerTab[i] = true;
                }

                await LoadTabResultsAsync(true);
            }
        }

        public async Task ResetFiltersAndReloadAsync()
        {
            ResetFilters();
            await ApplyFiltersAsync();
        }

        private void LoadRecentSearches()
        {
            RecentSearches.Clear();
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(KeyRecentSearches))
            {
                string raw = localSettings.Values[KeyRecentSearches] as string;
                if (!string.IsNullOrEmpty(raw))
                {
                    string[] items = raw.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var it in items)
                    {
                        if (!string.IsNullOrWhiteSpace(it))
                        {
                            RecentSearches.Add(it.Trim());
                        }
                    }
                }
            }
            OnPropertyChanged("HasRecentSearches");
        }

        public void AddRecentSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            query = query.Trim();

            RecentSearches.Remove(query);
            RecentSearches.Insert(0, query);

            while (RecentSearches.Count > 15)
            {
                RecentSearches.RemoveAt(RecentSearches.Count - 1);
            }

            SaveRecentSearches();
            OnPropertyChanged("HasRecentSearches");
        }

        public void ClearRecentSearches()
        {
            RecentSearches.Clear();
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values.Remove(KeyRecentSearches);
            OnPropertyChanged("HasRecentSearches");
        }

        private void SaveRecentSearches()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            string raw = string.Join("||", RecentSearches);
            localSettings.Values[KeyRecentSearches] = raw;
        }
    }
}
