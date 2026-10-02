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
            };

            if (ViewModel.Post != null && ((ViewModel.Post.ContentBlocks != null && ViewModel.Post.ContentBlocks.Count > 0) || !string.IsNullOrEmpty(ViewModel.Post.FullContent)))
            {
                RenderPostContentBlocks(ViewModel.Post);
            }

            ViewModel.RequestFullScreenToggle += (s, ev) => SetFullScreenMode(!ViewModel.IsFullScreen);
            ViewModel.RequestQualityChange += async (quality) => await SwitchQualityAsync(quality);

            HardwareButtons.BackPressed += OnHardwareBackPressed;

            await ViewModel.LoadPostDetailAndCommentsAsync();

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
                ViewModel.OpenImageCommand.Execute(url);
            }
        }

        /// <summary>
        /// 评论区图片点击：Tag 绑定的是 PostImage（含 Url/Width/Height）。
        /// 复用与正文图片相同的全屏图片查看器。
        /// </summary>
        private void OnCommentImageTapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null || ViewModel == null) return;

            string url = null;

            var img = element.Tag as KuroBBS.Models.PostImage;
            if (img != null)
            {
                url = img.Url;
            }
            else if (element.Tag is string)
            {
                url = element.Tag as string;
            }

            if (!string.IsNullOrEmpty(url))
            {
                ViewModel.OpenImageCommand.Execute(url);
            }
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
                border.MaxHeight = 44;
                decodeWidth = 480;
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

            if (!isBanner)
            {
                var skeleton = new Border
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

                // Automatically play animated GIFs with safe decoding and lifecycle management
                KuroGifHelper.SetGifSource(img, url);
            }

            border.Child = grid;
            border.Tapped += OnImageItemTapped;

            return border;
        }

        private DispatcherTimer _toastTimer;

        private void UpdateImageViewerContainerSize()
        {
            if (ImageViewerContainer != null && ViewerImage != null)
            {
                double screenW = Window.Current.Bounds.Width;
                double screenH = Window.Current.Bounds.Height;

                var bmp = ViewerImage.Source as BitmapSource;
                if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    double aspect = (double)bmp.PixelHeight / bmp.PixelWidth;
                    if (aspect > (screenH / screenW))
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
                ImageViewerContainer.Height = screenH;
                ImageViewerContainer.HorizontalAlignment = HorizontalAlignment.Center;
                ImageViewerContainer.VerticalAlignment = VerticalAlignment.Center;
                ViewerImage.Stretch = Stretch.Uniform;
                ViewerImage.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void ResetImageViewerScale()
        {
            UpdateImageViewerContainerSize();
            if (ImageViewerScrollViewer != null)
            {
                ImageViewerScrollViewer.ChangeView(0, 0, 1.0f, true);
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
