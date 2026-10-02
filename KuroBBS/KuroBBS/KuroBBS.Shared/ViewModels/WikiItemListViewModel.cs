using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class WikiTagItemViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class WikiTagGroupViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ObservableCollection<WikiTagItemViewModel> Tags { get; private set; }

        public WikiTagGroupViewModel()
        {
            Tags = new ObservableCollection<WikiTagItemViewModel>();
        }
    }

    public class WikiItemListViewModel : ViewModelBase
    {
        private int _wikiType;
        public int WikiType
        {
            get { return _wikiType; }
            set { _wikiType = value; OnPropertyChanged(); }
        }

        private int _catalogueId;
        public int CatalogueId
        {
            get { return _catalogueId; }
            set { _catalogueId = value; OnPropertyChanged(); }
        }

        private string _catalogueTitle = "条目列表";
        public string CatalogueTitle
        {
            get { return _catalogueTitle; }
            set { _catalogueTitle = value; OnPropertyChanged(); }
        }

        public List<WikiItemRecord> AllItems { get; private set; }
        public ObservableCollection<WikiItemRecord> FilteredItems { get; private set; }
        public ObservableCollection<WikiTagGroupViewModel> TagGroups { get; private set; }

        private string _keyword = "";
        public string Keyword
        {
            get { return _keyword; }
            set
            {
                _keyword = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private bool _hasTags;
        public bool HasTags
        {
            get { return _hasTags; }
            set { _hasTags = value; OnPropertyChanged(); }
        }

        /// <summary>每行列数，来自设置（图鉴 / 意识手册列表也走它）。</summary>
        public int GridColumns { get; private set; }

        private double _itemWidth;
        public double ItemWidth
        {
            get { return _itemWidth; }
            set { _itemWidth = value; OnPropertyChanged(); }
        }

        private double _itemHeight;
        public double ItemHeight
        {
            get { return _itemHeight; }
            set { _itemHeight = value; OnPropertyChanged(); }
        }

        public WikiItemListViewModel()
        {
            AllItems = new List<WikiItemRecord>();
            FilteredItems = new ObservableCollection<WikiItemRecord>();
            TagGroups = new ObservableCollection<WikiTagGroupViewModel>();

            GridColumns = SettingsHelper.GridColumns;
            // 图鉴卡片宽高比 ≈ 0.75（图 100 + 底栏 ~46），与旧 ItemWidth=116/ItemHeight=152 接近
            const double aspect = 152.0 / 116.0;
            try
            {
                var bounds = Windows.UI.Xaml.Window.Current.Bounds;
                double w = bounds.Width > 0 ? bounds.Width - 24 : 0;
                UpdateLayoutWidth(w, aspect);
            }
            catch
            {
                UpdateLayoutWidth(432, aspect); // 兜底：480 宽减 Padding
            }
        }

        /// <summary>页面 SizeChanged 时重算单元格宽高，保证按设置列数自适应。</summary>
        public void UpdateLayoutWidth(double availableWidth, double aspect = 152.0 / 116.0)
        {
            if (availableWidth <= 0) return;
            GridColumns = SettingsHelper.GridColumns;
            OnPropertyChanged("GridColumns");

            double w = GridLayoutHelper.CalcItemWidth(availableWidth, GridColumns, 90);
            ItemWidth = w;
            ItemHeight = GridLayoutHelper.CalcItemHeight(w, aspect);
        }

        public async Task LoadItemsAsync(int wikiType, int catalogueId, string title = null, bool forceRefresh = false)
        {
            WikiType = wikiType;
            CatalogueId = catalogueId;
            if (!string.IsNullOrEmpty(title))
            {
                CatalogueTitle = title;
            }

            IsBusy = true;
            try
            {
                var result = await KuroWikiService.Instance.GetCatalogueItemPageAsync(wikiType, catalogueId, 1, 1000, forceRefresh);
                if (result != null)
                {
                    if (!string.IsNullOrEmpty(result.Item3) && string.IsNullOrEmpty(title))
                    {
                        CatalogueTitle = result.Item3;
                    }

                    AllItems.Clear();
                    if (result.Item1 != null)
                    {
                        AllItems.AddRange(result.Item1);
                    }

                    TagGroups.Clear();
                    if (result.Item2 != null && result.Item2.Count > 0)
                    {
                        foreach (var parentTag in result.Item2)
                        {
                            var group = new WikiTagGroupViewModel
                            {
                                Id = parentTag.Id,
                                Name = parentTag.Name
                            };

                            if (parentTag.Children != null)
                            {
                                foreach (var childTag in parentTag.Children)
                                {
                                    group.Tags.Add(new WikiTagItemViewModel
                                    {
                                        Id = childTag.Id,
                                        Name = childTag.Name,
                                        ParentId = parentTag.Id,
                                        IsSelected = false
                                    });
                                }
                            }

                            if (group.Tags.Count > 0)
                            {
                                TagGroups.Add(group);
                            }
                        }
                    }

                    HasTags = TagGroups.Count > 0;
                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载条目失败: " + ex.Message;
                KuroLogger.Error("WIKI_ITEM_LIST_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ToggleTag(WikiTagItemViewModel tag)
        {
            if (tag == null) return;
            tag.IsSelected = !tag.IsSelected;
            ApplyFilter();
        }

        public void ResetFilter()
        {
            foreach (var group in TagGroups)
            {
                foreach (var tag in group.Tags)
                {
                    tag.IsSelected = false;
                }
            }
            Keyword = "";
            ApplyFilter();
        }

        public void ApplyFilter()
        {
            var selectedTagIds = new HashSet<string>();
            foreach (var group in TagGroups)
            {
                foreach (var tag in group.Tags)
                {
                    if (tag.IsSelected)
                    {
                        selectedTagIds.Add(tag.Id.ToString());
                    }
                }
            }

            var query = AllItems.AsEnumerable();

            // Filter by selected tags: An item matches if it has all selected tags or at least one tag per group (standard multi-tag logic)
            if (selectedTagIds.Count > 0)
            {
                query = query.Where(item =>
                {
                    if (item.RelateTagIds == null || item.RelateTagIds.Count == 0) return false;
                    foreach (var tagId in selectedTagIds)
                    {
                        if (item.RelateTagIds.Contains(tagId)) return true;
                    }
                    return false;
                });
            }

            // Filter by keyword
            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var kw = _keyword.Trim().ToLower();
                query = query.Where(item =>
                    (!string.IsNullOrEmpty(item.Name) && item.Name.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(item.Title) && item.Title.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(item.SubTitle) && item.SubTitle.ToLower().Contains(kw)));
            }

            FilteredItems.Clear();
            foreach (var item in query)
            {
                FilteredItems.Add(item);
            }
        }
    }
}
