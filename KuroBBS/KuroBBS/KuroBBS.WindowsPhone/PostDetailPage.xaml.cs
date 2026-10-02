using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class PostDetailPage : Page
    {
        public PostDetailViewModel ViewModel { get; private set; }
        private KuroHlsMediaStreamSource _hlsSource;
        private DispatcherTimer _playbackTimer;
        private DispatcherTimer _seekDebounceTimer;
        private DispatcherTimer _bottomBarAutoCloseTimer;
        private readonly List<System.Threading.CancellationTokenSource> _gifCtsList = new List<System.Threading.CancellationTokenSource>();
        private System.Threading.CancellationTokenSource _viewerGifCts;
        private bool _isUpdatingPositionFromTimer = false;
        private bool _isUserDraggingSlider = false;
        private bool _playWhenLoaded = false;

        public PostDetailPage()
        {
            this.InitializeComponent();

            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(500);
            _playbackTimer.Tick += PlaybackTimer_Tick;

            _seekDebounceTimer = new DispatcherTimer();
            _seekDebounceTimer.Interval = TimeSpan.FromMilliseconds(250);
            _seekDebounceTimer.Tick += SeekDebounceTimer_Tick;

            _bottomBarAutoCloseTimer = new DispatcherTimer();
            _bottomBarAutoCloseTimer.Interval = TimeSpan.FromSeconds(4);
            _bottomBarAutoCloseTimer.Tick += (s, ev) =>
            {
                _bottomBarAutoCloseTimer.Stop();
                DismissBottomBar();
            };

            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (BottomBar != null)
            {
                BottomBar.Opened += (s, ev) =>
                {
                    _bottomBarAutoCloseTimer.Stop();
                    _bottomBarAutoCloseTimer.Start();
                };
                BottomBar.Closed += (s, ev) =>
                {
                    _bottomBarAutoCloseTimer.Stop();
                };
            }

            if (ContentScrollViewer != null)
            {
                ContentScrollViewer.ViewChanged += OnScrollViewerViewChanged;
                ContentScrollViewer.AddHandler(UIElement.ManipulationDeltaEvent, new ManipulationDeltaEventHandler((s, ev) => DismissBottomBar()), true);
                ContentScrollViewer.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((s, ev) => DismissBottomBar()), true);
            }

            if (CommentsListView != null)
            {
                AttachScrollViewerToListView(CommentsListView);
            }

            if (MainPivot != null)
            {
                MainPivot.SelectionChanged += (s, ev) => DismissBottomBar();
                MainPivot.PivotItemLoaded += (s, ev) => DismissBottomBar();
            }

            if (RootLayoutGrid != null)
            {
                RootLayoutGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((s, ev) => DismissBottomBar()), true);
            }

            if (MainPivot != null)
            {
                HookPivotScrollViewer(MainPivot);
            }

            Window.Current.SizeChanged += (s, ev) => UpdateImageViewerContainerSize();
            UpdateImageViewerContainerSize();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var coreWin = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            if (coreWin != null)
            {
                coreWin.PointerPressed += OnCoreWindowPointer;
                coreWin.PointerMoved += OnCoreWindowPointer;
                coreWin.PointerReleased += OnCoreWindowPointer;
            }

            PostItem post = e.Parameter as PostItem;
            if (post == null)
            {
                string postIdStr = e.Parameter as string;
                if (!string.IsNullOrEmpty(postIdStr))
                {
                    post = new PostItem { PostId = postIdStr };
                }
            }
            ViewModel = new PostDetailViewModel(post);
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == "Post")
                {
                    RenderPostContentBlocks(ViewModel.Post);
                }
                else if (args.PropertyName == "IsImageViewerOpen" && ViewModel.IsImageViewerOpen)
                {
                    ResetImageViewerScale();
                }
                else if (args.PropertyName == "SelectedImageUrl" && ViewModel.IsImageViewerOpen)
                {
                    // 预览条切换主图：重新归位缩放并重新播放 GIF
                    ResetImageViewerScale();
                }
            };

            if (ViewModel.Post != null && ((ViewModel.Post.ContentBlocks != null && ViewModel.Post.ContentBlocks.Count > 0) || !string.IsNullOrEmpty(ViewModel.Post.FullContent)))
            {
                RenderPostContentBlocks(ViewModel.Post);
            }

            ViewModel.RequestFullScreenToggle += (s, ev) => SetFullScreenMode(!ViewModel.IsFullScreen);
            ViewModel.RequestQualityChange += async (quality) => await SwitchQualityAsync(quality);

            HardwareButtons.BackPressed += OnHardwareBackPressed;

            await ViewModel.LoadPostDetailAndCommentsAsync();

            // 从消息中心（评论/回复/通知）跳转时，定位并高亮到目标评论
            await LocateTargetCommentAsync(post);

            UpdateQualityFlyout();

            // Pre-load HLS video stream in background (AutoPlay=False) so it's armed instantly on first tap
            if (!string.IsNullOrEmpty(ViewModel.VideoPlayUrl))
            {
                await InitOrSwitchHlsStreamAsync(ViewModel.VideoPlayUrl, TimeSpan.Zero, autoPlay: false);
            }
        }

        private async Task InitOrSwitchHlsStreamAsync(string playUrl, TimeSpan startPos, bool autoPlay)
        {
            if (string.IsNullOrEmpty(playUrl) || PostMediaElement == null) return;

            try
            {
                ViewModel.IsVideoResolving = true;
                KuroLogger.Loading("PLAYER_MOUNT", "Mounting HLS stream: " + playUrl);

                if (_hlsSource != null)
                {
                    _hlsSource.Dispose();
                    _hlsSource = null;
                }

                uint w = (ViewModel.SelectedQuality != null && ViewModel.SelectedQuality.Width > 0) ? (uint)ViewModel.SelectedQuality.Width : 1280;
                uint h = (ViewModel.SelectedQuality != null && ViewModel.SelectedQuality.Height > 0) ? (uint)ViewModel.SelectedQuality.Height : 720;

                _hlsSource = await KuroHlsMediaStreamSource.CreateAsync(playUrl, w, h);
                if (startPos > TimeSpan.Zero)
                {
                    _hlsSource.SeekTo(startPos);
                }

                PostMediaElement.SetMediaStreamSource(_hlsSource.MediaStreamSource);
                KuroLogger.Loading("PLAYER_READY", "SetMediaStreamSource attached successfully");

                if (autoPlay || _playWhenLoaded)
                {
                    _playWhenLoaded = false;
                    ViewModel.HasStartedPlayback = true;
                    PostMediaElement.Play();
                    KuroLogger.Loading("PLAYER_PLAY", "Play requested");
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("HLS_INIT_ERR", "Failed to initialize HLS stream: " + ex.Message, ex);
            }
            finally
            {
                ViewModel.IsVideoResolving = false;
            }
        }

        private void PlaybackTimer_Tick(object sender, object e)
        {
            if (_isUserDraggingSlider || _seekDebounceTimer.IsEnabled) return;

            if (PostMediaElement != null && PostMediaElement.CurrentState == MediaElementState.Playing)
            {
                _isUpdatingPositionFromTimer = true;
                ViewModel.CurrentPositionSeconds = PostMediaElement.Position.TotalSeconds;
                _isUpdatingPositionFromTimer = false;
            }
        }

        private void SeekDebounceTimer_Tick(object sender, object e)
        {
            _seekDebounceTimer.Stop();
            _isUserDraggingSlider = false;

            if (PostMediaElement != null && ViewModel != null)
            {
                double targetSec = Math.Max(0, Math.Min(ViewModel.CurrentPositionSeconds, ViewModel.TotalDurationSeconds));
                KuroLogger.Loading("SEEK_APPLY", string.Format("Applying Seek to {0:F1}s", targetSec));
                PostMediaElement.Position = TimeSpan.FromSeconds(targetSec);

                // Auto resume/continue playing if video was already active
                if (ViewModel.IsVideoPlaying || ViewModel.HasStartedPlayback)
                {
                    PostMediaElement.Play();
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnHardwareBackPressed;

            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
            }

            if (_seekDebounceTimer != null)
            {
                _seekDebounceTimer.Stop();
            }

            if (PostMediaElement != null)
            {
                PostMediaElement.Stop();
                PostMediaElement.Source = null;
            }

            if (_hlsSource != null)
            {
                _hlsSource.Dispose();
                _hlsSource = null;
            }

            if (_bottomBarAutoCloseTimer != null)
            {
                _bottomBarAutoCloseTimer.Stop();
            }

            CancelAllGifs();

            if (_viewerGifCts != null)
            {
                try { _viewerGifCts.Cancel(); _viewerGifCts.Dispose(); } catch { }
                _viewerGifCts = null;
            }

            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;

            var coreWin = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            if (coreWin != null)
            {
                coreWin.PointerPressed -= OnCoreWindowPointer;
                coreWin.PointerMoved -= OnCoreWindowPointer;
                coreWin.PointerReleased -= OnCoreWindowPointer;
            }

            base.OnNavigatedFrom(e);
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            if (e.Handled) return;

            if (ViewModel != null && ViewModel.IsImageViewerOpen)
            {
                ViewModel.CloseImageCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (ViewModel != null && ViewModel.IsFullScreen)
            {
                SetFullScreenMode(false);
                e.Handled = true;
                return;
            }

            if (!KuroBBS.Helpers.BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }

            if (this.Frame != null && this.Frame.CanGoBack)
            {
                e.Handled = true;
                this.Frame.GoBack();
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.IsImageViewerOpen)
            {
                ViewModel.CloseImageCommand.Execute(null);
                return;
            }

            if (ViewModel != null && ViewModel.IsFullScreen)
            {
                SetFullScreenMode(false);
                return;
            }

            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private async void OnPlayPauseClick(object sender, RoutedEventArgs e)
        {
            if (PostMediaElement == null || ViewModel == null) return;

            ViewModel.HasStartedPlayback = true;

            // Apply pending seek immediately before toggling playback
            if (_seekDebounceTimer.IsEnabled)
            {
                _seekDebounceTimer.Stop();
                _isUserDraggingSlider = false;
                double targetSec = Math.Max(0, Math.Min(ViewModel.CurrentPositionSeconds, ViewModel.TotalDurationSeconds));
                PostMediaElement.Position = TimeSpan.FromSeconds(targetSec);
            }

            if (_hlsSource == null)
            {
                if (!string.IsNullOrEmpty(ViewModel.VideoPlayUrl))
                {
                    _playWhenLoaded = true;
                    double startSec = Math.Max(0, ViewModel.CurrentPositionSeconds);
                    await InitOrSwitchHlsStreamAsync(ViewModel.VideoPlayUrl, TimeSpan.FromSeconds(startSec), true);
                }
                return;
            }

            if (ViewModel.IsVideoResolving)
            {
                _playWhenLoaded = true;
                return;
            }

            if (PostMediaElement.CurrentState == MediaElementState.Playing)
            {
                KuroLogger.Loading("PLAYER_PAUSE", "Pausing video");
                PostMediaElement.Pause();
            }
            else
            {
                KuroLogger.Loading("PLAYER_PLAY", "Playing video");
                PostMediaElement.Play();
            }
        }

        private void OnSeekSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingPositionFromTimer || PostMediaElement == null) return;

            _isUserDraggingSlider = true;
            _seekDebounceTimer.Stop();
            _seekDebounceTimer.Start();
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            KuroLogger.Loading("MEDIA_OPENED", "MediaOpened event fired");
            if (PostMediaElement != null && PostMediaElement.NaturalDuration.HasTimeSpan && PostMediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                ViewModel.TotalDurationSeconds = PostMediaElement.NaturalDuration.TimeSpan.TotalSeconds;
            }
            UpdateQualityFlyout();
        }

        private void OnMediaCurrentStateChanged(object sender, RoutedEventArgs e)
        {
            if (PostMediaElement == null || ViewModel == null) return;

            KuroLogger.Loading("MEDIA_STATE", "MediaElement state changed to: " + PostMediaElement.CurrentState);

            if (PostMediaElement.CurrentState == MediaElementState.Playing)
            {
                ViewModel.IsVideoPlaying = true;
                if (!_playbackTimer.IsEnabled) _playbackTimer.Start();
            }
            else if (PostMediaElement.CurrentState == MediaElementState.Paused)
            {
                ViewModel.IsVideoPlaying = false;
                if (_playbackTimer.IsEnabled) _playbackTimer.Stop();
            }
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            KuroLogger.Loading("MEDIA_ENDED", "MediaEnded event fired");
            if (ViewModel != null)
            {
                ViewModel.IsVideoPlaying = false;
                ViewModel.CurrentPositionSeconds = 0;
            }
            if (_playbackTimer != null) _playbackTimer.Stop();
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            KuroLogger.Error("MEDIA_FAIL", "MediaElement playback failed: " + e.ErrorMessage);
        }

        private void UpdateQualityFlyout()
        {
            if (QualityFlyout == null || ViewModel == null) return;

            QualityFlyout.Items.Clear();
            foreach (var q in ViewModel.AvailableQualities)
            {
                var item = new MenuFlyoutItem
                {
                    Text = q.QualityName + (q == ViewModel.SelectedQuality ? " ✔" : "")
                };
                var targetQuality = q;
                item.Click += async (s, ev) =>
                {
                    await SwitchQualityAsync(targetQuality);
                };
                QualityFlyout.Items.Add(item);
            }
        }

        private async Task SwitchQualityAsync(VideoPlayInfoItem quality)
        {
            if (quality == null || ViewModel == null) return;

            KuroLogger.Loading("QUALITY_SWITCH", "Switching quality to: " + quality.QualityName);

            ViewModel.SelectedQuality = quality;
            ViewModel.VideoPlayUrl = quality.PlayUrl;
            UpdateQualityFlyout();

            var currentPos = (PostMediaElement != null) ? PostMediaElement.Position : TimeSpan.Zero;
            bool wasPlaying = (PostMediaElement != null && PostMediaElement.CurrentState == MediaElementState.Playing);
            await InitOrSwitchHlsStreamAsync(quality.PlayUrl, currentPos, wasPlaying);
        }

        private void OnVideoAreaTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.IsControlsVisible = !ViewModel.IsControlsVisible;
            }
        }

        private void OnFullScreenToggleClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                SetFullScreenMode(!ViewModel.IsFullScreen);
            }
        }

        private void SetFullScreenMode(bool isFull)
        {
            if (ViewModel == null || InlineVideoContainer == null) return;
            ViewModel.IsFullScreen = isFull;
            KuroLogger.Loading("FULLSCREEN", "Toggle fullscreen: " + isFull);

            if (isFull)
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape | DisplayOrientations.LandscapeFlipped;
                InlineVideoContainer.Height = double.NaN;
                InlineVideoContainer.SetValue(Grid.RowSpanProperty, 2);
            }
            else
            {
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Portrait;
                InlineVideoContainer.Height = 220;
                InlineVideoContainer.SetValue(Grid.RowSpanProperty, 1);
            }
        }

        private void OnImageItemTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            string url = element != null ? element.Tag as string : null;
            if (!string.IsNullOrEmpty(url) && ViewModel != null)
            {
                // 正文图片：把整篇正文的图片作为预览条数据源
                ViewModel.OpenImageWithSiblings(url, GetBodyImages());
            }
        }

        /// <summary>收集当前正文里所有图片块，供查看器底部预览条使用。</summary>
        private System.Collections.Generic.List<PostImage> GetBodyImages()
        {
            var list = new System.Collections.Generic.List<PostImage>();
            var post = ViewModel != null ? ViewModel.Post : null;
            if (post == null || post.ContentBlocks == null) return list;

            foreach (var block in post.ContentBlocks)
            {
                if (block != null && (block.IsImage || block.IsBanner) && !string.IsNullOrEmpty(block.ImageUrl))
                {
                    list.Add(new PostImage
                    {
                        Url = block.ImageUrl,
                        Width = block.ImageWidth,
                        Height = block.ImageHeight
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// 评论区图片点击：Tag 绑定的是 PostImage（含 Url/Width/Height）。
        /// 复用与正文图片相同的全屏图片查看器，并把同一楼层的图片作为预览条数据源。
        /// </summary>
        private void OnCommentImageTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null || ViewModel == null) return;

            string url = null;
            PostImage tapped = null;

            tapped = element.Tag as PostImage;
            if (tapped != null)
            {
                url = tapped.Url;
            }
            else if (element.Tag is string)
            {
                url = element.Tag as string;
            }

            if (string.IsNullOrEmpty(url)) return;

            // 同一楼层的图片集合 → 底部预览条
            var siblings = FindSiblingImages(element);
            ViewModel.OpenImageWithSiblings(url, siblings);
            e.Handled = true;
        }

        /// <summary>
        /// 从被点的元素往上找 DataContext，取出其所在评论/回复的 ImageList。
        /// 找不到就返回 null（查看器退化为单张模式）。
        /// </summary>
        private System.Collections.Generic.IEnumerable<PostImage> FindSiblingImages(FrameworkElement element)
        {
            var node = element as DependencyObject;
            while (node != null)
            {
                var comment = (node as FrameworkElement) != null ? (node as FrameworkElement).DataContext as PostCommentItem : null;
                if (comment != null && comment.ImageList != null && comment.ImageList.Count > 0)
                {
                    return comment.ImageList;
                }

                var reply = (node as FrameworkElement) != null ? (node as FrameworkElement).DataContext as PostReplyItem : null;
                if (reply != null && reply.ImageList != null && reply.ImageList.Count > 0)
                {
                    return reply.ImageList;
                }

                node = VisualTreeHelper.GetParent(node);
            }
            return null;
        }

        private void OnAuthorHeaderTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Post != null && ViewModel.Post.Author != null && !string.IsNullOrEmpty(ViewModel.Post.Author.UserId))
            {
                this.Frame.Navigate(typeof(UserProfilePage), ViewModel.Post.Author);
            }
        }

        private void OnTopicTagTapped(object sender, TappedRoutedEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border == null) return;
            var topic = border.Tag as TopicItem ?? border.DataContext as TopicItem;
            if (topic != null)
            {
                this.Frame.Navigate(typeof(TopicDetailPage), topic);
                e.Handled = true;
            }
        }

        private async void OnCommentLikeButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null || ViewModel == null) return;
            var comment = btn.DataContext as PostCommentItem;
            if (comment != null)
            {
                await ViewModel.ToggleCommentLikeAsync(comment);
            }
        }

        private async void OnReplyLikeButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null || ViewModel == null) return;
            var reply = btn.DataContext as PostReplyItem;
            if (reply != null)
            {
                await ViewModel.ToggleReplyLikeAsync(reply);
            }
        }

        private void OnCommentAvatarTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                string userId = element.Tag as string;
                if (!string.IsNullOrEmpty(userId))
                {
                    this.Frame.Navigate(typeof(UserProfilePage), userId);
                    e.Handled = true;
                }
            }
        }

        private void CancelAllGifs()
        {
            foreach (var cts in _gifCtsList)
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch { }
            }
            _gifCtsList.Clear();
        }

        private string _lastRenderedKey = null;

        private void RenderPostContentBlocks(PostItem post)
        {
            if (DynamicPostContentContainer == null || post == null) return;

            int blockCount = post.ContentBlocks != null ? post.ContentBlocks.Count : 0;
            int contentLength = post.FullContent != null ? post.FullContent.Length : 0;
            string key = string.Format("{0}_{1}_{2}", post.PostId, blockCount, contentLength);
            if (key == _lastRenderedKey && DynamicPostContentContainer.Children.Count > 0)
            {
                return;
            }
            _lastRenderedKey = key;

            CancelAllGifs();
            DynamicPostContentContainer.Children.Clear();

            if (post.ContentBlocks == null || post.ContentBlocks.Count == 0)
            {
                if (!string.IsNullOrEmpty(post.FullContent))
                {
                    var rtb = CreateRichTextBlock(null, KuroEmojiService.Instance.ParseTextToRuns(post.FullContent));
                    DynamicPostContentContainer.Children.Add(rtb);
                }
                return;
            }

            foreach (var block in post.ContentBlocks)
            {
                if (block.IsText)
                {
                    if (block.Runs != null && block.Runs.Count > 0)
                    {
                        var rtb = CreateRichTextBlock(block, block.Runs);
                        DynamicPostContentContainer.Children.Add(rtb);
                    }
                    else if (!string.IsNullOrWhiteSpace(block.RawContent))
                    {
                        var rtb = CreateRichTextBlock(block, KuroEmojiService.Instance.ParseTextToRuns(block.RawContent));
                        DynamicPostContentContainer.Children.Add(rtb);
                    }
                }
                else if (block.IsImage || block.IsBanner)
                {
                    if (!string.IsNullOrEmpty(block.ImageUrl))
                    {
                        var imgCard = CreateImageCard(block.ImageUrl, block.ImageWidth, block.ImageHeight, block.IsBanner);
                        DynamicPostContentContainer.Children.Add(imgCard);
                    }
                }
            }
        }

        private RichTextBlock CreateRichTextBlock(PostContentBlock block, IEnumerable<PostTextRun> runs)
        {
            bool isHeading = (block != null && block.IsHeading);
            int headingLevel = (block != null) ? block.HeadingLevel : 1;

            double fontSize = isHeading ? (headingLevel == 1 ? 20.0 : headingLevel == 2 ? 18.0 : headingLevel == 3 ? 16.5 : 15.5) : 14.5;
            double lineHeight = isHeading ? (headingLevel == 1 ? 28.0 : headingLevel == 2 ? 25.0 : 23.0) : 22.0;
            var margin = isHeading ? (headingLevel == 1 ? new Thickness(0, 16, 0, 8) : headingLevel == 2 ? new Thickness(0, 14, 0, 6) : new Thickness(0, 12, 0, 4)) : new Thickness(0, 3, 0, 7);
            var defaultColor = isHeading ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : Windows.UI.Color.FromArgb(255, 224, 224, 230);

            var rtb = new RichTextBlock
            {
                FontSize = fontSize,
                LineHeight = lineHeight,
                Foreground = new SolidColorBrush(defaultColor),
                TextWrapping = TextWrapping.Wrap,
                Margin = margin,
                IsTextSelectionEnabled = false
            };

            if (isHeading)
            {
                rtb.FontWeight = Windows.UI.Text.FontWeights.Bold;
            }

            var p = new Paragraph();
            foreach (var run in runs)
            {
                if (run == null) continue;

                if (run.IsEmoji)
                {
                    string emojiUrl = run.EmojiUrl;
                    if (string.IsNullOrEmpty(emojiUrl))
                    {
                        emojiUrl = KuroEmojiService.Instance.ResolveEmojiUrl(run.TargetId, run.Text);
                        run.EmojiUrl = emojiUrl;
                    }

                    if (!string.IsNullOrEmpty(emojiUrl))
                    {
                        var container = new InlineUIContainer();
                        var img = new Image
                        {
                            Width = isHeading ? 24 : 22,
                            Height = isHeading ? 24 : 22,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(2, 0, 2, -4)
                        };
                        img.Source = KuroImageCache.Instance.GetImageSource(emojiUrl);
                        container.Child = img;
                        p.Inlines.Add(container);
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(run.Text))
                {
                    var r = new Run { Text = run.Text };
                    if (run.IsBold || isHeading)
                    {
                        r.FontWeight = Windows.UI.Text.FontWeights.Bold;
                    }
                    if (run.IsItalic)
                    {
                        r.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    }
                    if (run.FontSize.HasValue && run.FontSize.Value > 0)
                    {
                        r.FontSize = run.FontSize.Value;
                    }
                    if (!string.IsNullOrEmpty(run.ColorHex))
                    {
                        var c = KuroHtmlPostParser.ParseColor(run.ColorHex);
                        if (c.HasValue)
                        {
                            r.Foreground = new SolidColorBrush(c.Value);
                        }
                    }

                    if (run.IsUnderline)
                    {
                        var u = new Underline();
                        u.Inlines.Add(r);
                        p.Inlines.Add(u);
                    }
                    else
                    {
                        p.Inlines.Add(r);
                    }
                }
            }

            rtb.Blocks.Add(p);
            return rtb;
        }

        private FrameworkElement CreateImageCard(string url, int imgWidth, int imgHeight, bool isBanner)
        {
            var border = new Border
            {
                Margin = isBanner ? new Thickness(0, 8, 0, 8) : new Thickness(0, 6, 0, 10),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 22, 28)),
                Tag = url
            };

            int decodeWidth = 720;
            int decodeHeight = 0;
            double estimatedH = 160;

            if (isBanner)
            {
                // 装饰横幅：保持扁平但不至于只剩一条缝。
                // 旧的 44px 太狠，一旦正文图被误判成 banner 就会「显示不全」。
                border.MaxHeight = 90;
                decodeWidth = 640;
            }
            else if (imgWidth > 0 && imgHeight > 0)
            {
                // Safe texture decoding: ensure long images don't exceed GPU texture limit (2048) and get cropped
                double ratio = (double)imgHeight / imgWidth;
                if (ratio > 1.8 || imgHeight > 2048)
                {
                    decodeHeight = 2048;
                    decodeWidth = 0;
                }

                estimatedH = Math.Max(120, (double)imgHeight * 330.0 / Math.Max(1, imgWidth));
                border.MinHeight = estimatedH;
            }
            else
            {
                border.MinHeight = 160;
            }

            var grid = new Grid();

            Border skeleton = null;
            if (!isBanner)
            {
                skeleton = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 34, 44)),
                    MinHeight = border.MinHeight
                };
                var sp = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 30)
                };
                var pb = new ProgressBar
                {
                    IsIndeterminate = true,
                    Width = 120,
                    Foreground = (Brush)Application.Current.Resources["MetroAccentBrush"],
                    Opacity = 0.75,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var tb = new TextBlock
                {
                    Text = "正在载入图片...",
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["MetroSubTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.6
                };
                sp.Children.Add(pb);
                sp.Children.Add(tb);
                skeleton.Child = sp;
                grid.Children.Add(skeleton);
            }

            // 【关键】图片就绪后必须把"正在载入图片..."骨架层从布局中移除。
            // 之前骨架层只加不删，即使图片已经加载出来，ProgressBar 仍叠在上面
            // （用户反馈："图加载出来了但是进度条不消失"）。
            var skeletonToRemove = skeleton;

            var bitmapSource = KuroImageCache.Instance.GetImageSource(url, decodeWidth, decodeHeight);

            // If the rendered height exceeds safe composition surface limit (1400 DIPs), slice seamlessly into multiple stacked clipping containers
            if (!isBanner && estimatedH > 1400)
            {
                int slices = (int)Math.Ceiling(estimatedH / 1200.0);
                var sliceStack = new StackPanel();
                for (int i = 0; i < slices; i++)
                {
                    double sliceH = estimatedH / slices;
                    double sliceOffset = i * sliceH;

                    var sliceContainer = new Grid
                    {
                        Height = sliceH,
                        Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 4000, sliceH) },
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var sliceImg = new Image
                    {
                        Source = bitmapSource,
                        Stretch = Stretch.Fill,
                        Height = estimatedH,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, -sliceOffset, 0, 0)
                    };
                    sliceContainer.Children.Add(sliceImg);
                    sliceStack.Children.Add(sliceContainer);
                }
                grid.Children.Add(sliceStack);

                // 分片长图：首片打开即移除骨架层。
                if (sliceStack.Children.Count > 0)
                {
                    var firstSlice = sliceStack.Children[0] as Grid;
                    var firstImg = firstSlice != null && firstSlice.Children.Count > 0 ? firstSlice.Children[0] as Image : null;
                    RemoveSkeletonWhenImageReady(firstImg, skeletonToRemove, grid);
                }
            }
            else
            {
                var img = new Image
                {
                    Stretch = isBanner ? Stretch.UniformToFill : Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                img.Source = bitmapSource;
                grid.Children.Add(img);

                // 图片真正打开后移除骨架层；失败时也移除，避免进度条永远转下去。
                RemoveSkeletonWhenImageReady(img, skeletonToRemove, grid);

                // Automatically play animated GIFs with safe decoding and lifecycle management
                KuroGifHelper.SetGifSource(img, url);
            }

            border.Child = grid;
            border.Tapped += OnImageItemTapped;

            return border;
        }

        /// <summary>
        /// 当 Image 成功打开（或失败）后，把"正在载入图片..."骨架层从容器里移除。
        /// <para>
        /// 旧逻辑：骨架层只 <c>grid.Children.Add(skeleton)</c> 从不移除，
        /// 于是图片明明已加载完成，ProgressBar + 文字仍叠在上层不消失。
        /// </para>
        /// <para>
        /// 注意：BitmapImage 是异步解码的，<c>ImageOpened</c> 可能在本方法返回之后才触发，
        /// 所以这里用事件回调而不是"立即移除"。
        /// </para>
        /// </summary>
        private static void RemoveSkeletonWhenImageReady(Image img, Border skeleton, Panel container)
        {
            if (img == null || skeleton == null || container == null) return;

            // 双保险：可能只有一个会触发，用 one-shot 标记避免重复移除。
            bool removed = false;
            RoutedEventHandler remove = null;
            ExceptionRoutedEventHandler removeOnFail = null;

            remove = (s, e) =>
            {
                if (removed) return;
                removed = true;
                try { container.Children.Remove(skeleton); } catch { }
                DetachImageLoadHandlers(img, remove, removeOnFail);
            };
            removeOnFail = (s, e) =>
            {
                if (removed) return;
                removed = true;
                try { container.Children.Remove(skeleton); } catch { }
                DetachImageLoadHandlers(img, remove, removeOnFail);
            };

            img.ImageOpened += remove;
            img.ImageFailed += removeOnFail;

            // 若图片同步命中内存缓存、且已在可视树中，ImageOpened 可能已经错过 —— 兜底检查一次。
            try
            {
                var bmp = img.Source as BitmapSource;
                if (bmp != null && bmp.PixelWidth > 0)
                {
                    if (!removed)
                    {
                        removed = true;
                        container.Children.Remove(skeleton);
                        DetachImageLoadHandlers(img, remove, removeOnFail);
                    }
                }
            }
            catch { }
        }

        private static void DetachImageLoadHandlers(Image img, RoutedEventHandler onOpened, ExceptionRoutedEventHandler onFailed)
        {
            if (img == null) return;
            try { if (onOpened != null) img.ImageOpened -= onOpened; } catch { }
            try { if (onFailed != null) img.ImageFailed -= onFailed; } catch { }
        }

        private DispatcherTimer _toastTimer;

        private void UpdateImageViewerContainerSize()
        {
            if (ImageViewerContainer != null && ViewerImage != null)
            {
                double screenW = Window.Current.Bounds.Width;
                double screenH = Window.Current.Bounds.Height;

                // 底部 9:1 预览条会占掉一条 86px 高的窄带，主图区高度必须减掉它，
                // 否则长图会被遮住一截、缩放中心也会算错。
                double stripH = 0;
                if (ViewerPreviewStrip != null && ViewerPreviewStrip.Visibility == Visibility.Visible)
                {
                    stripH = ViewerPreviewStrip.ActualHeight > 0 ? ViewerPreviewStrip.ActualHeight : 86;
                }
                double contentH = Math.Max(120, screenH - stripH);

                var bmp = ViewerImage.Source as BitmapSource;
                if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    double aspect = (double)bmp.PixelHeight / bmp.PixelWidth;
                    if (aspect > (contentH / screenW))
                    {
                        // Long image: expand height to natural aspect ratio and align to top so entire image scrolls smoothly
                        ImageViewerContainer.Width = screenW;
                        ImageViewerContainer.Height = screenW * aspect;
                        ImageViewerContainer.HorizontalAlignment = HorizontalAlignment.Center;
                        ImageViewerContainer.VerticalAlignment = VerticalAlignment.Top;
                        ViewerImage.Stretch = Stretch.UniformToFill;
                        ViewerImage.VerticalAlignment = VerticalAlignment.Top;
                        return;
                    }
                }

                ImageViewerContainer.Width = screenW;
                ImageViewerContainer.Height = contentH;
                ImageViewerContainer.HorizontalAlignment = HorizontalAlignment.Center;
                ImageViewerContainer.VerticalAlignment = VerticalAlignment.Center;
                ViewerImage.Stretch = Stretch.Uniform;
                ViewerImage.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void ResetImageViewerScale()
        {
            if (ViewerLoadingBar != null) ViewerLoadingBar.Visibility = Visibility.Visible;
            UpdateImageViewerContainerSize();
            if (ImageViewerScrollViewer != null)
            {
                ImageViewerScrollViewer.ChangeView(0, 0, 1.0f, true);
            }

            // 预览条把当前图滚动到可见位置，切换主图时不会「跑丢」
            if (ViewerThumbList != null && ViewModel != null && ViewModel.SelectedViewerIndex >= 0)
            {
                try { ViewerThumbList.ScrollIntoView(ViewModel.ViewerImages[ViewModel.SelectedViewerIndex]); }
                catch { }
            }

            if (ViewerImage != null && ViewModel != null && !string.IsNullOrEmpty(ViewModel.SelectedImageUrl))
            {
                if (_viewerGifCts != null)
                {
                    try { _viewerGifCts.Cancel(); _viewerGifCts.Dispose(); } catch { }
                }
                _viewerGifCts = new System.Threading.CancellationTokenSource();
                var ignore = KuroGifHelper.TryPlayGifAsync(ViewerImage, ViewModel.SelectedImageUrl, _viewerGifCts.Token);
            }
        }

        private void OnViewerImageOpened(object sender, RoutedEventArgs e)
        {
            if (ViewerLoadingBar != null) ViewerLoadingBar.Visibility = Visibility.Collapsed;
            ResetImageViewerScale();
        }

        private void OnImageViewerDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ImageViewerScrollViewer == null) return;

            if (ImageViewerScrollViewer.ZoomFactor > 1.2f)
            {
                ImageViewerScrollViewer.ChangeView(0, 0, 1.0f);
            }
            else
            {
                var pos = e.GetPosition(ImageViewerContainer);
                double targetZoom = 2.5;
                double hOffset = Math.Max(0, (pos.X * targetZoom) - (Window.Current.Bounds.Width / 2.0));
                double vOffset = Math.Max(0, (pos.Y * targetZoom) - (Window.Current.Bounds.Height / 2.0));
                ImageViewerScrollViewer.ChangeView(hOffset, vOffset, (float)targetZoom);
            }
            e.Handled = true;
        }

        private void OnImageViewerBackgroundTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ImageViewerScrollViewer != null && ImageViewerScrollViewer.ZoomFactor <= 1.1f)
            {
                OnImageViewerCloseClick(sender, e);
            }
        }

        private void OnImageViewerCloseClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.CloseImageCommand.Execute(null);
            }
        }

        private async void OnSaveImageClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null || string.IsNullOrWhiteSpace(ViewModel.SelectedImageUrl)) return;

            string url = ViewModel.SelectedImageUrl;
            try
            {
                ShowSaveToast("正在保存图片...");

                byte[] bytes = await KuroImageCache.Instance.GetImageBytesAsync(url);
                if (bytes == null || bytes.Length == 0)
                {
                    ShowSaveToast("保存失败: 无法下载图片");
                    return;
                }

                string ext = ".jpg";
                int dotIdx = url.LastIndexOf('.');
                if (dotIdx > 0)
                {
                    int queryIdx = url.IndexOf('?', dotIdx);
                    if (queryIdx > 0)
                        ext = url.Substring(dotIdx, queryIdx - dotIdx).ToLower();
                    else
                        ext = url.Substring(dotIdx).ToLower();

                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp")
                        ext = ".jpg";
                }

                string fileName = "KuroBBS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Math.Abs(url.GetHashCode()) + ext;

                StorageFolder picturesFolder = KnownFolders.SavedPictures;
                var file = await picturesFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteBytesAsync(file, bytes);

                KuroLogger.Info("IMG_SAVE_OK", "Saved image to SavedPictures: " + fileName);
                ShowSaveToast("已保存到相册");
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("IMG_SAVE_ERR", "Failed saving image: " + ex.Message);
                ShowSaveToast("保存失败: " + ex.Message);
            }
        }

        private void ShowSaveToast(string message)
        {
            if (SaveToastBorder == null || SaveToastText == null) return;

            SaveToastText.Text = message;
            SaveToastBorder.Visibility = Visibility.Visible;

            if (_toastTimer == null)
            {
                _toastTimer = new DispatcherTimer();
                _toastTimer.Interval = TimeSpan.FromSeconds(2.5);
                _toastTimer.Tick += (s, e) =>
                {
                    _toastTimer.Stop();
                    SaveToastBorder.Visibility = Visibility.Collapsed;
                };
            }
            else
            {
                _toastTimer.Stop();
            }
            _toastTimer.Start();
        }

        private ScrollViewer _commentsScrollViewer;
        private void AttachScrollViewerToListView(ListView listView)
        {
            if (listView == null) return;

            Action tryHook = () =>
            {
                if (_commentsScrollViewer != null) return;
                var sv = FindVisualChild<ScrollViewer>(listView);
                if (sv != null)
                {
                    _commentsScrollViewer = sv;
                    sv.ViewChanged -= CommentsScrollViewer_ViewChanged;
                    sv.ViewChanged += CommentsScrollViewer_ViewChanged;
                }
            };

            tryHook();
            listView.Loaded += (s, e) => tryHook();
        }

        private void CommentsScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            DismissBottomBar();
            var sv = sender as ScrollViewer;
            if (sv == null || ViewModel == null) return;

            if (!e.IsIntermediate && sv.ScrollableHeight > 0 && (sv.VerticalOffset >= sv.ScrollableHeight - 350 || (sv.VerticalOffset / sv.ScrollableHeight >= 0.75)))
            {
                if (!ViewModel.IsLoadingComments && ViewModel.HasMoreComments)
                {
                    var ignore = ViewModel.LoadMoreCommentsAsync();
                }
            }
        }

        private ScrollViewer _pivotScrollViewer;
        private void HookPivotScrollViewer(Pivot pivot)
        {
            if (pivot == null) return;

            Action tryHook = () =>
            {
                if (_pivotScrollViewer != null) return;
                var sv = FindVisualChild<ScrollViewer>(pivot);
                if (sv != null)
                {
                    _pivotScrollViewer = sv;
                    sv.ViewChanged -= OnScrollViewerViewChanged;
                    sv.ViewChanged += OnScrollViewerViewChanged;
                }
            };

            tryHook();
            pivot.Loaded += (s, e) => tryHook();
        }

        private void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            DismissBottomBar();
        }

        private void OnCoreWindowPointer(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            DismissBottomBar();
        }

        private void DismissBottomBar()
        {
            if (_bottomBarAutoCloseTimer != null)
            {
                _bottomBarAutoCloseTimer.Stop();
            }

            if (BottomBar != null && BottomBar.IsOpen)
            {
                if (Dispatcher.HasThreadAccess)
                {
                    BottomBar.IsOpen = false;
                }
                else
                {
                    var ignore = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (BottomBar != null && BottomBar.IsOpen)
                        {
                            BottomBar.IsOpen = false;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 从消息中心跳转时，定位并高亮到 TargetCommentId 对应的评论楼层。
        /// 若目标评论不在已加载列表里，会先翻几页评论再尝试；未找到则静默放弃。
        /// </summary>
        private async Task LocateTargetCommentAsync(PostItem post)
        {
            if (post == null || string.IsNullOrEmpty(post.TargetCommentId)) return;
            if (ViewModel == null || ViewModel.Comments == null || CommentsListView == null) return;

            PostCommentItem target = FindCommentById(post.TargetCommentId);
            int attempts = 0;
            while (target == null && attempts < 4 && ViewModel.HasMoreComments && !ViewModel.IsLoadingComments)
            {
                await ViewModel.LoadMoreCommentsAsync();
                target = FindCommentById(post.TargetCommentId);
                attempts++;
            }
            if (target == null) return;

            // 先切到「评论」分页，确保 CommentsListView 已加载到可视树
            if (MainPivot != null)
            {
                foreach (var it in MainPivot.Items)
                {
                    var pi = it as PivotItem;
                    if (pi != null && object.Equals(pi.Header, "评论"))
                    {
                        MainPivot.SelectedItem = pi;
                        break;
                    }
                }
            }

            // 高亮 + 滚入视野
            target.IsHighlighted = true;
            CommentsListView.ScrollIntoView(target);

            // 数秒后自动取消高亮
            var tgt = target;
            var ignore = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000);
                tgt.IsHighlighted = false;
            });
        }

        private PostCommentItem FindCommentById(string id)
        {
            if (ViewModel == null || ViewModel.Comments == null) return null;
            foreach (var c in ViewModel.Comments)
            {
                if (c != null && string.Equals(c.CommentId, id))
                {
                    return c;
                }
            }
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T) return (T)child;
                var sub = FindVisualChild<T>(child);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
