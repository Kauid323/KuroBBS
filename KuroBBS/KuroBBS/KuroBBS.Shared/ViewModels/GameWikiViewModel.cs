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

                    Contributors.Clear();
                    foreach (var c in data.Contributors) Contributors.Add(c);

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
