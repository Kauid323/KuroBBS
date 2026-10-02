using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.ViewModels
{
    public class CreatePostViewModel : ViewModelBase
    {
        private string _title = "";
        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                    OnPropertyChanged("TitleCountText");
                    OnPropertyChanged("CanPublish");
                }
            }
        }

        public string TitleCountText
        {
            get { return string.Format("{0}/50", _title != null ? _title.Length : 0); }
        }

        private string _content = "";
        public string Content
        {
            get { return _content; }
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                    OnPropertyChanged("ContentCountText");
                    OnPropertyChanged("CanPublish");
                }
            }
        }

        public string ContentCountText
        {
            get { return string.Format("{0} 字", _content != null ? _content.Length : 0); }
        }

        private int _selectedGameId = 2; // Default: 战双帕弥什
        public int SelectedGameId
        {
            get { return _selectedGameId; }
            set
            {
                if (_selectedGameId != value)
                {
                    _selectedGameId = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsZhanShuangSelected");
                    OnPropertyChanged("IsMingChaoSelected");
                    OnPropertyChanged("SelectedGameName");
                    UpdateForumCategories();
                }
            }
        }

        public string SelectedGameName
        {
            get { return _selectedGameId == 2 ? "战双帕弥什" : "鸣潮"; }
        }

        public bool IsZhanShuangSelected
        {
            get { return _selectedGameId == 2; }
        }

        public bool IsMingChaoSelected
        {
            get { return _selectedGameId == 3; }
        }

        private readonly ObservableCollection<ForumCategoryItem> _forumCategories = new ObservableCollection<ForumCategoryItem>();
        public ObservableCollection<ForumCategoryItem> ForumCategories
        {
            get { return _forumCategories; }
        }

        private ForumCategoryItem _selectedForum;
        public ForumCategoryItem SelectedForum
        {
            get { return _selectedForum; }
            set
            {
                if (_selectedForum != value)
                {
                    _selectedForum = value;
                    OnPropertyChanged();
                    OnPropertyChanged("CanPublish");
                }
            }
        }

        private readonly ObservableCollection<PostEditorImage> _attachedImages = new ObservableCollection<PostEditorImage>();
        public ObservableCollection<PostEditorImage> AttachedImages
        {
            get { return _attachedImages; }
        }

        public bool HasAttachedImages
        {
            get { return _attachedImages.Count > 0; }
        }

        private readonly ObservableCollection<EmojiPackageGroup> _emojiPackages = new ObservableCollection<EmojiPackageGroup>();
        public ObservableCollection<EmojiPackageGroup> EmojiPackages
        {
            get { return _emojiPackages; }
        }

        private EmojiPackageGroup _selectedEmojiPackage;
        public EmojiPackageGroup SelectedEmojiPackage
        {
            get { return _selectedEmojiPackage; }
            set
            {
                if (_selectedEmojiPackage != value)
                {
                    _selectedEmojiPackage = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isUploadingImage = false;
        public bool IsUploadingImage
        {
            get { return _isUploadingImage; }
            set
            {
                if (_isUploadingImage != value)
                {
                    _isUploadingImage = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsBusy");
                }
            }
        }

        private bool _isPublishing = false;
        public bool IsPublishing
        {
            get { return _isPublishing; }
            set
            {
                if (_isPublishing != value)
                {
                    _isPublishing = value;
                    OnPropertyChanged();
                    OnPropertyChanged("IsBusy");
                }
            }
        }

        public new bool IsBusy
        {
            get { return _isUploadingImage || _isPublishing || base.IsBusy; }
        }

        private bool _isPreviewMode = false;
        public bool IsPreviewMode
        {
            get { return _isPreviewMode; }
            set
            {
                if (_isPreviewMode != value)
                {
                    _isPreviewMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<PostContentBlock> _previewBlocks = new List<PostContentBlock>();
        public List<PostContentBlock> PreviewBlocks
        {
            get { return _previewBlocks; }
            set
            {
                if (_previewBlocks != value)
                {
                    _previewBlocks = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanPublish
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_title) && 
                       (!string.IsNullOrWhiteSpace(_content) || _attachedImages.Count > 0) &&
                       _selectedForum != null &&
                       !IsBusy;
            }
        }

        public CreatePostViewModel()
        {
            UpdateForumCategories();
        }

        private readonly ObservableCollection<TopicItem> _selectedTopics = new ObservableCollection<TopicItem>();
        public ObservableCollection<TopicItem> SelectedTopics
        {
            get { return _selectedTopics; }
        }

        public bool HasSelectedTopics
        {
            get { return _selectedTopics.Count > 0; }
        }

        public void AddSelectedTopic(TopicItem topic)
        {
            if (topic == null) return;
            if (_selectedTopics.Any(t => t.TopicId == topic.TopicId))
            {
                NotificationHelper.ShowNotification("已添加该话题");
                return;
            }
            if (_selectedTopics.Count >= 5)
            {
                NotificationHelper.ShowNotification("最多只能添加5个话题");
                return;
            }
            _selectedTopics.Add(topic);
            OnPropertyChanged("HasSelectedTopics");
        }

        public void RemoveSelectedTopic(TopicItem topic)
        {
            if (topic != null && _selectedTopics.Contains(topic))
            {
                _selectedTopics.Remove(topic);
                OnPropertyChanged("HasSelectedTopics");
            }
        }

        private readonly ObservableCollection<TopicItem> _availableTopics = new ObservableCollection<TopicItem>();
        public ObservableCollection<TopicItem> AvailableTopics
        {
            get { return _availableTopics; }
        }

        public bool HasAvailableTopics
        {
            get { return _availableTopics.Count > 0; }
        }

        private bool _isLoadingTopics = false;
        public bool IsLoadingTopics
        {
            get { return _isLoadingTopics; }
            set
            {
                if (_isLoadingTopics != value)
                {
                    _isLoadingTopics = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _topicSearchKeyword = "";
        public string TopicSearchKeyword
        {
            get { return _topicSearchKeyword; }
            set
            {
                if (_topicSearchKeyword != value)
                {
                    _topicSearchKeyword = value;
                    OnPropertyChanged();
                }
            }
        }

        public async Task LoadHotTopicsAsync(int gameId, bool force = false)
        {
            if (!force && _availableTopics.Count > 0 && string.IsNullOrEmpty(_topicSearchKeyword)) return;

            IsLoadingTopics = true;
            try
            {
                var topics = await KuroForumService.Instance.GetTopicHotListAsync(gameId, type: 2, pageIndex: 1, pageSize: 20);
                _availableTopics.Clear();
                if (topics != null)
                {
                    foreach (var t in topics)
                    {
                        _availableTopics.Add(t);
                    }
                }
                OnPropertyChanged("HasAvailableTopics");
            }
            catch (Exception ex)
            {
                KuroLogger.Error("LOAD_HOT_TOPICS_ERR", "Failed to load hot topics: " + ex.Message, ex);
            }
            finally
            {
                IsLoadingTopics = false;
            }
        }

        public async Task SearchTopicsAsync(string keyword)
        {
            TopicSearchKeyword = keyword;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                await LoadHotTopicsAsync(_selectedGameId, force: true);
                return;
            }

            IsLoadingTopics = true;
            try
            {
                var topics = await KuroForumService.Instance.SearchTopicsAsync(_selectedGameId, keyword, pageIndex: 1, pageSize: 20);
                _availableTopics.Clear();
                if (topics != null)
                {
                    foreach (var t in topics)
                    {
                        _availableTopics.Add(t);
                    }
                }
                OnPropertyChanged("HasAvailableTopics");
            }
            catch (Exception ex)
            {
                KuroLogger.Error("SEARCH_TOPICS_ERR", "Failed to search topics: " + ex.Message, ex);
            }
            finally
            {
                IsLoadingTopics = false;
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                var packages = await KuroEmojiService.Instance.GetEmojiPackagesAsync();
                _emojiPackages.Clear();
                foreach (var pkg in packages)
                {
                    _emojiPackages.Add(pkg);
                }
                if (_emojiPackages.Count > 0 && _selectedEmojiPackage == null)
                {
                    SelectedEmojiPackage = _emojiPackages[0];
                }

                await LoadHotTopicsAsync(_selectedGameId);
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("CREATE_POST_EMOJI_INIT", "Failed to load emoji packages: " + ex.Message);
            }
        }

        public void SelectGame(int gameId)
        {
            SelectedGameId = gameId;
        }

        public void UpdateForumCategories()
        {
            _forumCategories.Clear();
            if (_selectedGameId == 2)
            {
                // 战双帕弥什
                _forumCategories.Add(new ForumCategoryItem { ForumId = 3, GameId = 2, Name = "伊甸闲庭", Description = "闲聊与日常交流" });
                _forumCategories.Add(new ForumCategoryItem { ForumId = 4, GameId = 2, Name = "战术终端", Description = "攻略心得与阵容" });
                _forumCategories.Add(new ForumCategoryItem { ForumId = 5, GameId = 2, Name = "艺术画廊", Description = "同人绘画与二创" });
            }
            else
            {
                // 鸣潮
                _forumCategories.Add(new ForumCategoryItem { ForumId = 9, GameId = 3, Name = "鸣友闲聊", Description = "今州日常与水帖" });
                _forumCategories.Add(new ForumCategoryItem { ForumId = 10, GameId = 3, Name = "声骸攻略", Description = "战斗攻略与养成" });
                _forumCategories.Add(new ForumCategoryItem { ForumId = 11, GameId = 3, Name = "同人画作", Description = "二创同人与鉴赏" });
            }

            if (_forumCategories.Count > 0)
            {
                SelectedForum = _forumCategories[0];
            }
        }

        public void InsertEmoji(EmojiEntryItem emoji)
        {
            if (emoji == null) return;
            string tag = emoji.TagText;
            InsertTextAtCurrentCursor(tag);
        }

        public void InsertTopicTag(string topicName)
        {
            if (string.IsNullOrWhiteSpace(topicName)) return;
            string tag = string.Format("#{0}# ", topicName.Trim('#', ' '));
            InsertTextAtCurrentCursor(tag);
        }

        public void ApplyFormatting(string openTag, string closeTag, string sampleText = "文字")
        {
            InsertTextAtCurrentCursor(string.Format("{0}{1}{2}", openTag, sampleText, closeTag));
        }

        public void ApplyColor(string hexColor, string sampleText = "彩色文字")
        {
            InsertTextAtCurrentCursor(string.Format("<font color=\"{0}\">{1}</font>", hexColor, sampleText));
        }

        public void InsertHeading(string sampleText = "标题文字")
        {
            InsertTextAtCurrentCursor(string.Format("\n<font size=\"5\"><b>{0}</b></font>\n", sampleText));
        }

        private void InsertTextAtCurrentCursor(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (string.IsNullOrEmpty(_content))
            {
                Content = text;
            }
            else
            {
                Content = _content + text;
            }
        }

        public async Task<bool> UploadAndAddImageFileAsync(StorageFile file)
        {
            if (file == null) return false;
            if (_attachedImages.Count >= 9)
            {
                StatusMessage = "最多只能添加9张图片";
                NotificationHelper.ShowNotification("最多只能添加9张图片");
                return false;
            }

            IsUploadingImage = true;
            StatusMessage = "正在上传图片: " + file.Name;

            try
            {
                byte[] bytes;
                using (var stream = await file.OpenReadAsync())
                {
                    bytes = new byte[stream.Size];
                    using (var reader = new DataReader(stream))
                    {
                        await reader.LoadAsync((uint)stream.Size);
                        reader.ReadBytes(bytes);
                    }
                }

                // Decode dimensions
                double width = 750;
                double height = 750;
                try
                {
                    using (var memStream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(memStream))
                        {
                            writer.WriteBytes(bytes);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                            memStream.Seek(0);

                            var bmp = new BitmapImage();
                            await bmp.SetSourceAsync(memStream);
                            if (bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                            {
                                width = bmp.PixelWidth;
                                height = bmp.PixelHeight;
                            }
                        }
                    }
                }
                catch { }

                string contentType = file.ContentType;
                if (string.IsNullOrEmpty(contentType))
                {
                    string ext = file.FileType.ToLower();
                    if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
                    else if (ext == ".gif") contentType = "image/gif";
                    else contentType = "image/png";
                }

                string uploadedUrl = await KuroApiClient.Instance.UploadImageAsync(bytes, file.Name, contentType);
                if (!string.IsNullOrEmpty(uploadedUrl))
                {
                    _attachedImages.Add(new PostEditorImage
                    {
                        Url = uploadedUrl,
                        Width = width,
                        Height = height,
                        LocalFileName = file.Name,
                        IsCover = _attachedImages.Count == 0 ? 1 : 0
                    });

                    OnPropertyChanged("HasAttachedImages");
                    OnPropertyChanged("CanPublish");
                    StatusMessage = "图片上传成功";
                    NotificationHelper.ShowNotification("图片上传成功");
                    return true;
                }
                else
                {
                    StatusMessage = "图片上传失败，请稍后重试";
                    NotificationHelper.ShowNotification("图片上传失败");
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("IMAGE_UPLOAD_EXCEPTION", "Failed to upload image file: " + ex.Message, ex);
                StatusMessage = "图片上传异常: " + ex.Message;
                NotificationHelper.ShowNotification("图片上传出错");
            }
            finally
            {
                IsUploadingImage = false;
            }

            return false;
        }

        public void RemoveImage(PostEditorImage img)
        {
            if (img != null && _attachedImages.Contains(img))
            {
                _attachedImages.Remove(img);
                OnPropertyChanged("HasAttachedImages");
                OnPropertyChanged("CanPublish");
            }
        }

        public void GeneratePreview()
        {
            var blocks = new List<PostContentBlock>();

            if (!string.IsNullOrWhiteSpace(_content))
            {
                var parsedBlocks = KuroHtmlPostParser.ParseHtml(_content);
                if (parsedBlocks != null)
                {
                    blocks.AddRange(parsedBlocks);
                }
            }

            foreach (var img in _attachedImages)
            {
                blocks.Add(new PostContentBlock
                {
                    BlockType = ContentBlockType.Image,
                    ImageUrl = img.Url,
                    ImageWidth = (int)img.Width,
                    ImageHeight = (int)img.Height
                });
            }

            PreviewBlocks = blocks;
            IsPreviewMode = true;
        }

        public async Task<PublishPostResult> PublishAsync()
        {
            if (string.IsNullOrWhiteSpace(_title))
            {
                NotificationHelper.ShowNotification("请输入帖子标题");
                return new PublishPostResult { Success = false, Message = "请输入帖子标题" };
            }

            if (string.IsNullOrWhiteSpace(_content) && _attachedImages.Count == 0)
            {
                NotificationHelper.ShowNotification("请输入帖子内容或添加图片");
                return new PublishPostResult { Success = false, Message = "请输入帖子内容或添加图片" };
            }

            if (_selectedForum == null)
            {
                NotificationHelper.ShowNotification("请选择发帖板块");
                return new PublishPostResult { Success = false, Message = "请选择发帖板块" };
            }

            IsPublishing = true;
            StatusMessage = "正在发布帖子...";

            try
            {
                var res = await KuroForumService.Instance.PublishPostAsync(
                    _title,
                    _selectedGameId,
                    _selectedForum.ForumId,
                    _content,
                    _attachedImages.ToList(),
                    _selectedTopics.ToList()
                );

                if (res != null && res.Success)
                {
                    StatusMessage = "发布成功！";
                    NotificationHelper.ShowNotification("帖子发布成功！");
                    return res;
                }
                else
                {
                    string err = (res != null && !string.IsNullOrEmpty(res.Message)) ? res.Message : "发布失败";
                    StatusMessage = err;
                    NotificationHelper.ShowNotification(err);
                    return res;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("PUBLISH_EXCEPTION", "Publish error: " + ex.Message, ex);
                StatusMessage = "发布异常: " + ex.Message;
                NotificationHelper.ShowNotification("发布异常: " + ex.Message);
                return new PublishPostResult { Success = false, Message = ex.Message };
            }
            finally
            {
                IsPublishing = false;
            }
        }
    }
}
