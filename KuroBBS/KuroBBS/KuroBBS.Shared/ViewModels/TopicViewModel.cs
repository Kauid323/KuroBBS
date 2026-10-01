using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class TopicViewModel : ViewModelBase
    {
        private string _topicId;
        public string TopicId
        {
            get { return _topicId; }
            set { _topicId = value; OnPropertyChanged(); }
        }

        private TopicDetailResult _topic;
        public TopicDetailResult Topic
        {
            get { return _topic; }
            set { _topic = value; OnPropertyChanged(); }
        }

        private int _selectedTabIndex = 0; // 0:最新发布 (type=1), 1:最新回复 (type=2), 2:精选 (type=4)
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsTabNewest");
                    OnPropertyChanged("IsTabLatestReply");
                    OnPropertyChanged("IsTabFeatured");

                    if (!string.IsNullOrEmpty(TopicId))
                    {
                        var ignore = EnsureTabLoadedAsync(_selectedTabIndex);
                    }
                }
            }
        }

        public bool IsTabNewest { get { return _selectedTabIndex == 0; } }
        public bool IsTabLatestReply { get { return _selectedTabIndex == 1; } }
        public bool IsTabFeatured { get { return _selectedTabIndex == 2; } }

        public ObservableCollection<PostItem> NewestPosts { get; private set; }
        public ObservableCollection<PostItem> LatestReplyPosts { get; private set; }
        public ObservableCollection<PostItem> FeaturedPosts { get; private set; }

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

        private readonly int[] _pagePerTab = new int[] { 1, 1, 1 };
        private readonly bool[] _hasMorePerTab = new bool[] { true, true, true };

        public ICommand RefreshCommand { get; private set; }

        public TopicViewModel()
        {
            NewestPosts = new ObservableCollection<PostItem>();
            LatestReplyPosts = new ObservableCollection<PostItem>();
            FeaturedPosts = new ObservableCollection<PostItem>();

            RefreshCommand = new RelayCommand(async () => await LoadTabPostsAsync(SelectedTabIndex, true));
        }

        public async Task InitializeAsync(string topicId)
        {
            TopicId = topicId;
            _selectedTabIndex = 0;
            NewestPosts.Clear();
            LatestReplyPosts.Clear();
            FeaturedPosts.Clear();

            for (int i = 0; i < 3; i++)
            {
                _pagePerTab[i] = 1;
                _hasMorePerTab[i] = true;
            }

            await LoadTabPostsAsync(0, true);
        }

        private int GetApiTypeForTab(int tabIndex)
        {
            switch (tabIndex)
            {
                case 1: return 2; // 最新回复
                case 2: return 4; // 精选
                default: return 1; // 最新发布
            }
        }

        private ObservableCollection<PostItem> GetCollectionForTab(int tabIndex)
        {
            switch (tabIndex)
            {
                case 1: return LatestReplyPosts;
                case 2: return FeaturedPosts;
                default: return NewestPosts;
            }
        }

        public async Task EnsureTabLoadedAsync(int tabIndex)
        {
            if (string.IsNullOrEmpty(TopicId) || tabIndex < 0 || tabIndex > 2) return;

            var collection = GetCollectionForTab(tabIndex);
            if (collection.Count == 0)
            {
                _pagePerTab[tabIndex] = 1;
                _hasMorePerTab[tabIndex] = true;
                await LoadTabPostsAsync(tabIndex, true);
            }
            else
            {
                HasMore = _hasMorePerTab[tabIndex];
            }
        }

        public async Task SwitchTabAsync(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex > 2) return;
            SelectedTabIndex = tabIndex;
            await EnsureTabLoadedAsync(tabIndex);
        }

        public async Task LoadTabPostsAsync(int tabIndex, bool isRefresh = false)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = null;

            if (isRefresh)
            {
                _pagePerTab[tabIndex] = 1;
                _hasMorePerTab[tabIndex] = true;
            }

            int page = _pagePerTab[tabIndex];
            int apiType = GetApiTypeForTab(tabIndex);
            var collection = GetCollectionForTab(tabIndex);

            try
            {
                var result = await KuroForumService.Instance.GetTopicDetailAsync(TopicId, apiType, page, 20);
                if (result != null)
                {
                    if (Topic == null || isRefresh)
                    {
                        Topic = result;
                    }

                    if (isRefresh || page == 1)
                    {
                        collection.Clear();
                    }

                    if (result.Posts != null && result.Posts.Count > 0)
                    {
                        foreach (var post in result.Posts)
                        {
                            collection.Add(post);
                        }
                        _hasMorePerTab[tabIndex] = result.HasNext && result.Posts.Count >= 20;
                    }
                    else
                    {
                        _hasMorePerTab[tabIndex] = false;
                        if (collection.Count == 0)
                        {
                            StatusMessage = "暂无帖子内容";
                        }
                    }

                    HasMore = _hasMorePerTab[tabIndex];
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("TOPIC_LOAD_ERROR", "Failed to load topic tab " + tabIndex + ": " + ex.Message);
                StatusMessage = "加载话题失败: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadMoreAsync()
        {
            int tabIndex = SelectedTabIndex;
            if (IsBusy || IsLoadingMore || !_hasMorePerTab[tabIndex]) return;
            IsLoadingMore = true;

            try
            {
                _pagePerTab[tabIndex]++;
                int page = _pagePerTab[tabIndex];
                int apiType = GetApiTypeForTab(tabIndex);
                var collection = GetCollectionForTab(tabIndex);

                var result = await KuroForumService.Instance.GetTopicDetailAsync(TopicId, apiType, page, 20);

                if (result != null && result.Posts != null && result.Posts.Count > 0)
                {
                    foreach (var post in result.Posts)
                    {
                        collection.Add(post);
                    }
                    _hasMorePerTab[tabIndex] = result.HasNext && result.Posts.Count >= 20;
                }
                else
                {
                    _hasMorePerTab[tabIndex] = false;
                }

                HasMore = _hasMorePerTab[tabIndex];
            }
            catch (Exception ex)
            {
                KuroLogger.Error("TOPIC_LOADMORE_ERROR", "Failed to load more topic posts: " + ex.Message);
                _pagePerTab[tabIndex]--;
            }
            finally
            {
                IsLoadingMore = false;
            }
        }
    }
}
