using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Helpers;
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

        // ---- 图片查看器状态（与 PostDetailPage / UserProfilePage 保持一致）----

        private bool _isImageViewerOpen;
        public bool IsImageViewerOpen
        {
            get { return _isImageViewerOpen; }
            set { _isImageViewerOpen = value; OnPropertyChanged(); }
        }

        private string _selectedImageUrl;
        public string SelectedImageUrl
        {
            get { return _selectedImageUrl; }
            set { _selectedImageUrl = value; OnPropertyChanged(); }
        }

        /// <summary>当前查看的图片 URL 列表（用于左右滑动切换）。</summary>
        public ObservableCollection<string> ViewerImages { get; private set; }

        private int _viewerIndex;
        public int ViewerIndex
        {
            get { return _viewerIndex; }
            set
            {
                if (_viewerIndex == value) return;
                _viewerIndex = value;
                OnPropertyChanged();
                SelectedImageUrl = (value >= 0 && value < ViewerImages.Count) ? ViewerImages[value] : null;
                OnPropertyChanged("ViewerCounterText");
            }
        }

        /// <summary>「3 / 12」形式的计数，供查看器顶部显示。</summary>
        public string ViewerCounterText
        {
            get
            {
                if (ViewerImages == null || ViewerImages.Count <= 1) return "";
                return string.Format("{0} / {1}", _viewerIndex + 1, ViewerImages.Count);
            }
        }

        public bool HasMultipleViewerImages
        {
            get { return ViewerImages != null && ViewerImages.Count > 1; }
        }

        public RelayCommand<string> OpenImageCommand { get; private set; }
        public RelayCommand CloseImageCommand { get; private set; }
        public RelayCommand NextImageCommand { get; private set; }
        public RelayCommand PrevImageCommand { get; private set; }

        public WikiEntryDetailViewModel()
        {
            Modules = new ObservableCollection<WikiDetailModule>();
            Contributors = new ObservableCollection<WikiContributorItem>();
            ViewerImages = new ObservableCollection<string>();

            OpenImageCommand = new RelayCommand<string>(OpenImage);
            CloseImageCommand = new RelayCommand(CloseImage);
            NextImageCommand = new RelayCommand(() => ViewerIndex = Math.Min(ViewerIndex + 1, ViewerImages.Count - 1));
            PrevImageCommand = new RelayCommand(() => ViewerIndex = Math.Max(ViewerIndex - 1, 0));
        }

        /// <summary>
        /// 打开图片查看器。会基于当前条目的所有图片建立可滑动列表，
        /// 并把点击的那张设为当前项。
        /// </summary>
        private void OpenImage(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            ViewerImages.Clear();
            foreach (var u in CollectAllImageUrls())
            {
                if (!string.IsNullOrEmpty(u) && !ViewerImages.Contains(u))
                {
                    ViewerImages.Add(u);
                }
            }

            if (!ViewerImages.Contains(url)) ViewerImages.Add(url);

            int idx = ViewerImages.IndexOf(url);
            _viewerIndex = idx < 0 ? 0 : idx;
            OnPropertyChanged("ViewerIndex");
            SelectedImageUrl = (idx >= 0 && idx < ViewerImages.Count) ? ViewerImages[idx] : url;
            OnPropertyChanged("ViewerCounterText");
            OnPropertyChanged("HasMultipleViewerImages");
            IsImageViewerOpen = true;
        }

        private void CloseImage()
        {
            IsImageViewerOpen = false;
            SelectedImageUrl = null;
        }

        /// <summary>汇总当前条目里所有可查看的图片 URL（组件主图 + 图集）。</summary>
        private IEnumerable<string> CollectAllImageUrls()
        {
            if (_detail == null || _detail.Modules == null) yield break;

            foreach (var mod in _detail.Modules)
            {
                if (mod == null || mod.Components == null) continue;
                foreach (var comp in mod.Components)
                {
                    if (comp == null) continue;

                    if (!string.IsNullOrEmpty(comp.ImageUrl))
                    {
                        yield return comp.ImageUrl;
                    }

                    if (comp.ImageList != null)
                    {
                        foreach (var img in comp.ImageList)
                        {
                            if (!string.IsNullOrEmpty(img)) yield return img;
                        }
                    }

                    // 简单富文本 tab 里的图片同样可查看。
                    var tabImgs = comp.ActiveTabImages;
                    if (tabImgs != null)
                    {
                        foreach (var img in tabImgs)
                        {
                            if (!string.IsNullOrEmpty(img)) yield return img;
                        }
                    }
                }
            }
        }

        public async Task LoadDetailAsync(int wikiType, string entryId, bool forceRefresh = false)
        {
            WikiType = wikiType;
            EntryId = entryId;
            IsBusy = true;
            CloseImage();
            ViewerImages.Clear();
            KuroLogger.Trace("WIKI_DETAIL_BEGIN id=" + entryId);
            try
            {
                var detail = await KuroWikiService.Instance.GetEntryDetailAsync(wikiType, entryId, forceRefresh);
                KuroLogger.Trace("WIKI_DETAIL_FETCHED modules=" + (detail != null && detail.Modules != null ? detail.Modules.Count : -1)
                                 + " comps=" + CountComponents(detail));
                Detail = detail;
                Modules.Clear();
                if (detail != null && detail.Modules != null)
                {
                    // 逐块（module）分帧加入，而不是一次性 Add 全部。
                    // 画册 / 图集类条目单页可能有几十个 module、数百个组件，
                    // 一次性构建会在 UI 线程上产生长任务，表现为「点进去一直转圈 / 加载不出来」。
                    // 这里每加一块就主动让出一次 dispatcher，保证滚动与响应不被卡死。
                    for (int i = 0; i < detail.Modules.Count; i++)
                    {
                        Modules.Add(detail.Modules[i]);
                        KuroLogger.Trace("WIKI_DETAIL_ADD_MODULE " + i);
                        await YieldToUiAsync();
                    }
                }

                KuroLogger.Trace("WIKI_DETAIL_MODULES_ADDED");
                Contributors.Clear();
                if (detail != null && detail.Contributors != null)
                {
                    foreach (var c in detail.Contributors)
                    {
                        Contributors.Add(c);
                    }
                }
                KuroLogger.Trace("WIKI_DETAIL_DONE contributors=" + Contributors.Count);
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

        private static int CountComponents(WikiEntryDetail detail)
        {
            if (detail == null || detail.Modules == null) return -1;
            int n = 0;
            foreach (var m in detail.Modules)
            {
                if (m != null && m.Components != null) n += m.Components.Count;
            }
            return n;
        }

        /// <summary>
        /// 在当前 UI 线程让出一次调度机会（低优先级），使渲染/输入得以插队执行。
        /// 用于把「一次性构建大量 UI」拆成多帧，避免界面卡死。
        /// 注意：C# 5.0（VS2013）不允许在 catch 块里 await，
        /// 因此这里把「取 dispatcher」与「让出」分开，异常路径只记录标志，
        /// 真正的 await 放在 try/catch 之外。
        /// </summary>
        private static async Task YieldToUiAsync()
        {
            Windows.UI.Core.CoreDispatcher dispatcher = null;
            try
            {
                var coreWindow = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow;
                if (coreWindow != null) dispatcher = coreWindow.Dispatcher;
            }
            catch
            {
                dispatcher = null;
            }

            if (dispatcher != null)
            {
                try
                {
                    await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () => { });
                    return;
                }
                catch
                {
                    // 调度失败：退化为普通异步让出（在 catch 外执行）
                }
            }

            // 非 UI 线程 / 设计时 / 调度失败：普通异步让出
            await Task.Yield();
        }
    }
}
