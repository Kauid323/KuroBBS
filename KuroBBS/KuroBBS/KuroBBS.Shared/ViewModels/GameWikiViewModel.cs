using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class GameWikiViewModel : ViewModelBase
    {
        private int _wikiType;
        public int WikiType
        {
            get { return _wikiType; }
            set
            {
                _wikiType = value;
                OnPropertyChanged();
                OnPropertyChanged("GameTitle");
                OnPropertyChanged("IsMingChao");
                OnPropertyChanged("IsZhanShuang");
            }
        }

        public string GameTitle
        {
            get { return _wikiType == 9 ? "鸣潮 WIKI" : "战双帕弥什 WIKI"; }
        }

        public bool IsMingChao { get { return _wikiType == 9; } }
        public bool IsZhanShuang { get { return _wikiType == 2; } }

        public ObservableCollection<WikiBannerItem> Banners { get; private set; }
        public ObservableCollection<WikiAnnouncementItem> Announcements { get; private set; }
        public ObservableCollection<WikiShortcutItem> Shortcuts { get; private set; }
        public ObservableCollection<WikiShortcutItem> MainModules { get; private set; }
        public ObservableCollection<WikiShortcutItem> SideModules { get; private set; }
        public ObservableCollection<WikiContributorItem> Contributors { get; private set; }
        public ObservableCollection<WikiSearchItem> SearchResults { get; private set; }

        /// <summary>特色玩法 / 研发卡池 / 常驻玩法（带剩余时间 + 进度条）。</summary>
        public ObservableCollection<WikiEventCard> FeaturedCards { get; private set; }
        /// <summary>热门活动（带剩余时间 + 进度条）。</summary>
        public ObservableCollection<WikiEventCard> HotActivities { get; private set; }
        /// <summary>热门话题（话题标签）。</summary>
        public ObservableCollection<WikiShortcutItem> HotTopics { get; private set; }
        /// <summary>未归类的 sideModules 兜底入口。</summary>
        public ObservableCollection<WikiShortcutItem> OtherModules { get; private set; }
        /// <summary>纷争战区（带剩余时间 + 进度条）。</summary>
        public ObservableCollection<WikiEventCard> ZoneCards { get; private set; }
        /// <summary>热门资讯（分组 + 词条）。</summary>
        public ObservableCollection<WikiNewsGroup> NewsGroups { get; private set; }

        /// <summary>指挥官贡献榜入口标题（接口未给则用默认文案）。</summary>
        public string ContributorTitle
        {
            get { return _contributorTitle; }
        }
        private string _contributorTitle = "指挥官贡献榜";

        /// <summary>指挥官贡献榜入口图标（接口未给则空）。</summary>
        public string ContributorIconUrl
        {
            get { return _contributorIconUrl; }
        }
        private string _contributorIconUrl = "";

        // 倒计时刷新定时器（首页卡片上的「剩余时间 / 进度条」需要随时间推进）
        private Windows.UI.Xaml.DispatcherTimer _countdownTimer;
        private readonly System.Collections.Generic.List<WikiCountdownInfo> _activeCountdowns = new System.Collections.Generic.List<WikiCountdownInfo>();

        private string _searchQuery = "";
        public string SearchQuery
        {
            get { return _searchQuery; }
            set { _searchQuery = value; OnPropertyChanged(); }
        }

        private bool _isSearching;
        public bool IsSearching
        {
            get { return _isSearching; }
            set { _isSearching = value; OnPropertyChanged(); }
        }

        private bool _hasLoaded;
        public bool HasLoaded
        {
            get { return _hasLoaded; }
            set { _hasLoaded = value; OnPropertyChanged(); }
        }

        public GameWikiViewModel()
        {
            Banners = new ObservableCollection<WikiBannerItem>();
            Announcements = new ObservableCollection<WikiAnnouncementItem>();
            Shortcuts = new ObservableCollection<WikiShortcutItem>();
            MainModules = new ObservableCollection<WikiShortcutItem>();
            SideModules = new ObservableCollection<WikiShortcutItem>();
            Contributors = new ObservableCollection<WikiContributorItem>();
            SearchResults = new ObservableCollection<WikiSearchItem>();
            FeaturedCards = new ObservableCollection<WikiEventCard>();
            HotActivities = new ObservableCollection<WikiEventCard>();
            HotTopics = new ObservableCollection<WikiShortcutItem>();
            OtherModules = new ObservableCollection<WikiShortcutItem>();
            ZoneCards = new ObservableCollection<WikiEventCard>();
            NewsGroups = new ObservableCollection<WikiNewsGroup>();
        }

        /// <summary>启动倒计时刷新（30 秒一次），让首页卡片的剩余时间/进度条随时间推进。</summary>
        public void StartCountdownTimer()
        {
            if (_countdownTimer == null)
            {
                _countdownTimer = new Windows.UI.Xaml.DispatcherTimer();
                _countdownTimer.Interval = TimeSpan.FromSeconds(30);
                _countdownTimer.Tick += CountdownTimer_Tick;
            }
            _countdownTimer.Start();
        }

        /// <summary>停止倒计时刷新（离开页面时调用，避免无谓的后台唤醒）。</summary>
        public void StopCountdownTimer()
        {
            if (_countdownTimer != null) _countdownTimer.Stop();
        }

        private void CountdownTimer_Tick(object sender, object e)
        {
            for (int i = 0; i < _activeCountdowns.Count; i++)
            {
                try { _activeCountdowns[i].Refresh(); }
                catch { }
            }
        }

        public async Task LoadHomepageAsync(int wikiType, bool forceRefresh = false)
        {
            WikiType = wikiType;
            IsBusy = true;
            try
            {
                var data = await KuroWikiService.Instance.GetWikiHomepageAsync(wikiType, forceRefresh);
                if (data != null)
                {
                    Banners.Clear();
                    foreach (var b in data.Banners) Banners.Add(b);

                    Announcements.Clear();
                    foreach (var a in data.Announcements) Announcements.Add(a);

                    Shortcuts.Clear();
                    foreach (var s in data.Shortcuts) Shortcuts.Add(s);

                    MainModules.Clear();
                    foreach (var m in data.MainModules) MainModules.Add(m);

                    SideModules.Clear();
                    foreach (var sm in data.SideModules) SideModules.Add(sm);

                    FeaturedCards.Clear();
                    foreach (var fc in data.FeaturedCards) FeaturedCards.Add(fc);

                    HotActivities.Clear();
                    foreach (var ha in data.HotActivities) HotActivities.Add(ha);

                    HotTopics.Clear();
                    foreach (var ht in data.HotTopics) HotTopics.Add(ht);

                    OtherModules.Clear();
                    foreach (var om in data.OtherModules) OtherModules.Add(om);

                    ZoneCards.Clear();
                    foreach (var zc in data.ZoneCards) ZoneCards.Add(zc);

                    NewsGroups.Clear();
                    foreach (var ng in data.NewsGroups) NewsGroups.Add(ng);

                    Contributors.Clear();
                    foreach (var c in data.Contributors) Contributors.Add(c);

                    // 贡献榜入口：接口给了就用接口标题/图标，否则用默认文案。
                    if (data.ContributorModule != null && !string.IsNullOrWhiteSpace(data.ContributorModule.Title))
                    {
                        _contributorTitle = data.ContributorModule.Title;
                        _contributorIconUrl = data.ContributorModule.IconUrl;
                    }
                    OnPropertyChanged("ContributorTitle");
                    OnPropertyChanged("ContributorIconUrl");

                    CollectActiveCountdowns();
                    StartCountdownTimer();

                    HasLoaded = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载WIKI首页失败: " + ex.Message;
                KuroLogger.Error("WIKI_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>收集所有带有效区间的倒计时，供定时器统一刷新。</summary>
        private void CollectActiveCountdowns()
        {
            _activeCountdowns.Clear();
            foreach (var c in FeaturedCards)
            {
                if (c != null && c.Countdown != null && c.Countdown.HasRange) _activeCountdowns.Add(c.Countdown);
            }
            foreach (var c in HotActivities)
            {
                if (c != null && c.Countdown != null && c.Countdown.HasRange) _activeCountdowns.Add(c.Countdown);
            }
            foreach (var c in ZoneCards)
            {
                if (c != null && c.Countdown != null && c.Countdown.HasRange) _activeCountdowns.Add(c.Countdown);
            }
        }

        public async Task SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchResults.Clear();
                IsSearching = false;
                return;
            }

            IsSearching = true;
            IsBusy = true;
            try
            {
                var results = await KuroWikiService.Instance.SearchWikiAsync(WikiType, query.Trim());
                SearchResults.Clear();
                if (results != null)
                {
                    foreach (var item in results)
                    {
                        SearchResults.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "搜索WIKI失败: " + ex.Message;
                KuroLogger.Error("WIKI_SEARCH_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
