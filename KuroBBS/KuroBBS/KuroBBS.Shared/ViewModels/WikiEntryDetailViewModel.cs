using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class WikiEntryDetailViewModel : ViewModelBase
    {
        private int _wikiType;
        public int WikiType
        {
            get { return _wikiType; }
            set { _wikiType = value; OnPropertyChanged(); }
        }

        private string _entryId;
        public string EntryId
        {
            get { return _entryId; }
            set { _entryId = value; OnPropertyChanged(); }
        }

        private WikiEntryDetail _detail;
        public WikiEntryDetail Detail
        {
            get { return _detail; }
            set
            {
                _detail = value;
                OnPropertyChanged();
                OnPropertyChanged("Title");
                OnPropertyChanged("OrgFullName");
                OnPropertyChanged("LastUpdateTime");
                OnPropertyChanged("LastEditUserName");
                OnPropertyChanged("BrowseCountText");
                OnPropertyChanged("CoverImageUrl");
                OnPropertyChanged("HasCoverImage");
            }
        }

        public string Title
        {
            get { return _detail != null ? _detail.Title : "条目详情"; }
        }

        public string OrgFullName
        {
            get { return _detail != null ? _detail.OrgFullName : ""; }
        }

        public string LastUpdateTime
        {
            get { return _detail != null ? _detail.LastUpdateTime : ""; }
        }

        public string LastEditUserName
        {
            get { return _detail != null ? _detail.LastEditUserName : ""; }
        }

        public string BrowseCountText
        {
            get { return _detail != null ? string.Format("{0} 次浏览", _detail.BrowseCount) : ""; }
        }

        public string CoverImageUrl
        {
            get { return _detail != null ? _detail.CoverImageUrl : ""; }
        }

        public bool HasCoverImage
        {
            get { return !string.IsNullOrEmpty(CoverImageUrl); }
        }

        public ObservableCollection<WikiDetailModule> Modules { get; private set; }
        public ObservableCollection<WikiContributorItem> Contributors { get; private set; }

        public WikiEntryDetailViewModel()
        {
            Modules = new ObservableCollection<WikiDetailModule>();
            Contributors = new ObservableCollection<WikiContributorItem>();
        }

        public async Task LoadDetailAsync(int wikiType, string entryId, bool forceRefresh = false)
        {
            WikiType = wikiType;
            EntryId = entryId;
            IsBusy = true;
            try
            {
                var detail = await KuroWikiService.Instance.GetEntryDetailAsync(wikiType, entryId, forceRefresh);
                Detail = detail;
                Modules.Clear();
                if (detail != null && detail.Modules != null)
                {
                    foreach (var m in detail.Modules)
                    {
                        Modules.Add(m);
                    }
                }

                Contributors.Clear();
                if (detail != null && detail.Contributors != null)
                {
                    foreach (var c in detail.Contributors)
                    {
                        Contributors.Add(c);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载条目详情失败: " + ex.Message;
                KuroLogger.Error("WIKI_DETAIL_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
