using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    /// <summary>
    /// 指挥官贡献榜页面 ViewModel。
    ///
    /// 接口：POST /wiki/core/score/record/getTop10List
    ///   type=2 周榜 / type=3 月榜 / type=4 总榜
    /// 该映射逐字取自官方前端 contributor-238a4428.js：
    ///   [{title:"周榜",value:2},{title:"月榜",value:3},{title:"总榜",value:4}]
    /// </summary>
    public class WikiRankViewModel : ViewModelBase
    {
        public int WikiType { get; private set; }

        public string GameTitle
        {
            get { return WikiType == 9 ? "鸣潮 WIKI" : "战双帕弥什 WIKI"; }
        }

        public ObservableCollection<WikiContributorItem> Weekly { get; private set; }
        public ObservableCollection<WikiContributorItem> Monthly { get; private set; }
        public ObservableCollection<WikiContributorItem> Total { get; private set; }

        private bool _hasLoaded;
        public bool HasLoaded
        {
            get { return _hasLoaded; }
            set { _hasLoaded = value; OnPropertyChanged(); }
        }

        public WikiRankViewModel()
        {
            Weekly = new ObservableCollection<WikiContributorItem>();
            Monthly = new ObservableCollection<WikiContributorItem>();
            Total = new ObservableCollection<WikiContributorItem>();
        }

        public async Task LoadAsync(int wikiType, bool forceRefresh = false)
        {
            // 页面被缓存（NavigationCacheMode.Required）后，OnNavigatedTo 每次返回都会触发。
            // 只有「换了游戏」或用户主动刷新时才重新拉数据，否则回到本页不刷新
            // （用户反馈：从别的页面回来总是自动刷新数据）。
            bool typeChanged = WikiType != wikiType;
            WikiType = wikiType;
            OnPropertyChanged("GameTitle");

            if (HasLoaded && !forceRefresh && !typeChanged) return;

            IsBusy = true;
            try
            {
                var weekly = await KuroWikiService.Instance.GetTopContributorsAsync(wikiType, 2);
                var monthly = await KuroWikiService.Instance.GetTopContributorsAsync(wikiType, 3);
                var total = await KuroWikiService.Instance.GetTopContributorsAsync(wikiType, 4);

                Fill(Weekly, weekly);
                Fill(Monthly, monthly);
                Fill(Total, total);

                HasLoaded = true;
            }
            catch (Exception ex)
            {
                StatusMessage = "加载贡献榜失败: " + ex.Message;
                KuroLogger.Error("WIKI_RANK_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static void Fill(ObservableCollection<WikiContributorItem> target, List<WikiContributorItem> source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }
    }
}
