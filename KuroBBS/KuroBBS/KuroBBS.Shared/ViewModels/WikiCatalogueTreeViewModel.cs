using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class WikiCatalogueTreeViewModel : ViewModelBase
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
            }
        }

        public string GameTitle
        {
            get { return _wikiType == 9 ? "鸣潮 WIKI 目录" : "战双帕弥什 WIKI 目录"; }
        }

        public ObservableCollection<WikiCatalogueNode> RootCategories { get; private set; }
        public ObservableCollection<WikiCatalogueNode> DisplayNodes { get; private set; }

        private WikiCatalogueNode _selectedCategory;
        public WikiCatalogueNode SelectedCategory
        {
            get { return _selectedCategory; }
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                UpdateDisplayNodes();
            }
        }

        public WikiCatalogueTreeViewModel()
        {
            RootCategories = new ObservableCollection<WikiCatalogueNode>();
            DisplayNodes = new ObservableCollection<WikiCatalogueNode>();
        }

        public async Task LoadTreeAsync(int wikiType, bool forceRefresh = false)
        {
            WikiType = wikiType;
            IsBusy = true;
            try
            {
                var root = await KuroWikiService.Instance.GetWikiTreeAsync(wikiType, forceRefresh);
                RootCategories.Clear();
                if (root != null && root.Children != null)
                {
                    foreach (var child in root.Children)
                    {
                        RootCategories.Add(child);
                    }

                    if (RootCategories.Count > 0)
                    {
                        SelectedCategory = RootCategories[0];
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "加载目录树失败: " + ex.Message;
                KuroLogger.Error("WIKI_TREE_VM_ERR", ex.Message, ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateDisplayNodes()
        {
            DisplayNodes.Clear();
            if (_selectedCategory == null) return;

            // If selectedCategory has children, add them or flatten them
            if (_selectedCategory.Children != null && _selectedCategory.Children.Count > 0)
            {
                foreach (var child in _selectedCategory.Children)
                {
                    DisplayNodes.Add(child);
                }
            }
            else
            {
                // Is a leaf itself
                DisplayNodes.Add(_selectedCategory);
            }
        }
    }
}
