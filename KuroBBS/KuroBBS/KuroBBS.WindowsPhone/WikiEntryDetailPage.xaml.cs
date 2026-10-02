using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
    public sealed partial class WikiEntryDetailPage : Page
    {
        public WikiEntryDetailViewModel ViewModel { get; set; }
        private int _wikiType = 9;
        private string _entryId = "";
        private System.Threading.CancellationTokenSource _viewerGifCts;
        private DispatcherTimer _toastTimer;

        public WikiEntryDetailPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new WikiEntryDetailViewModel();
            this.DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            Window.Current.SizeChanged += (s, e) => UpdateImageViewerContainerSize();
            Windows.Phone.UI.Input.HardwareButtons.BackPressed += OnHardwareBackPressed;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Windows.Phone.UI.Input.HardwareButtons.BackPressed -= OnHardwareBackPressed;
            base.OnNavigatedFrom(e);
        }

        private void OnHardwareBackPressed(object sender, Windows.Phone.UI.Input.BackPressedEventArgs e)
        {
            if (e.Handled) return;

            // 图片查看器打开时，返回键先关查看器，而不是退出页面。
            if (ViewModel != null && ViewModel.IsImageViewerOpen)
            {
                ViewModel.CloseImageCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsImageViewerOpen" && ViewModel.IsImageViewerOpen)
            {
                ResetImageViewerScale();
            }
            else if (e.PropertyName == "SelectedImageUrl" && ViewModel.IsImageViewerOpen)
            {
                ResetImageViewerScale();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.Back) return;

            string newEntryId = "";
            int newWikiType = 9;

            if (e.Parameter != null)
            {
                // Format: "wikiType|entryId"
                string paramStr = e.Parameter.ToString();
                string[] parts = paramStr.Split('|');
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out newWikiType);
                    newEntryId = parts[1];
                }
                else
                {
                    newEntryId = paramStr;
                }
            }

            if (!string.IsNullOrEmpty(newEntryId))
            {
                _wikiType = newWikiType;
                _entryId = newEntryId;
                await ViewModel.LoadDetailAsync(_wikiType, _entryId);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_entryId))
            {
                await ViewModel.LoadDetailAsync(_wikiType, _entryId, forceRefresh: true);
            }
        }

        private void OnTabButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var tab = btn.DataContext as WikiTabItem;
            if (tab == null || ViewModel == null || ViewModel.Modules == null) return;

            foreach (var mod in ViewModel.Modules)
            {
                if (mod.Components == null) continue;
                foreach (var comp in mod.Components)
                {
                    if (comp.Tabs != null && comp.Tabs.Contains(tab))
                    {
                        comp.SelectedTab = tab;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 意识手册 - 立绘页签切换（1/4号位、2/5号位 ...）
        /// </summary>
        private void OnConsciousnessIllustrationClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null) return;
            var ill = btn.DataContext as ConsciousnessIllustration;
            if (ill == null || ViewModel == null || ViewModel.Modules == null) return;

            foreach (var mod in ViewModel.Modules)
            {
                if (mod.Components == null) continue;
                foreach (var comp in mod.Components)
                {
                    if (comp.Consciousness != null && comp.Consciousness.Illustrations != null
                        && comp.Consciousness.Illustrations.Contains(ill))
                    {
                        comp.Consciousness.SelectedIllustration = ill;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 意识手册 - 意识故事折叠展开
        /// </summary>
        private void OnConsciousnessStoryTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var story = elem.DataContext as ConsciousnessStory;
            if (story != null)
            {
                story.IsExpanded = !story.IsExpanded;
            }
        }

        private void OnComponentHeaderTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var comp = elem.DataContext as WikiDetailComponent;
            if (comp != null && comp.CanCollapse)
            {
                comp.IsCollapsed = !comp.IsCollapsed;
            }
        }

        private void OnSectionHeaderTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var sec = elem.DataContext as WikiSectionItem;
            if (sec != null)
            {
                sec.IsExpanded = !sec.IsExpanded;
            }
        }

        private void OnStrategyItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var strat = elem.DataContext as WikiStrategyItem;
            if (strat != null && !string.IsNullOrEmpty(strat.EntryId) && strat.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, strat.EntryId));
            }
        }

        private void OnGearItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var gear = elem.DataContext as WikiRoleGearItem;
            if (gear != null && !string.IsNullOrEmpty(gear.EntryId) && gear.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, gear.EntryId));
            }
        }

        private void OnEquipRecommendItemClick(object sender, RoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var item = elem.DataContext as WikiEquipRecommendItem;
            if (item != null && !string.IsNullOrEmpty(item.EntryId) && item.EntryId != "0")
            {
                Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, item.EntryId));
            }
        }

        private void OnActiveTabLinkTap(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;
            var comp = elem.DataContext as WikiDetailComponent;
            if (comp != null && comp.HasActiveTabLink)
            {
                if (!string.IsNullOrEmpty(comp.ActiveTabLinkEntryId) && comp.ActiveTabLinkEntryId != "0")
                {
                    Frame.Navigate(typeof(WikiEntryDetailPage), string.Format("{0}|{1}", _wikiType, comp.ActiveTabLinkEntryId));
                }
                else if (!string.IsNullOrEmpty(comp.ActiveTabLinkUrl))
                {
                    // 全软件统一的内链解析（/item/、/post/、/topic/、/user/、站外链接）。
                    KuroBBS.Helpers.KuroLinkNavigator.Navigate(Frame, comp.ActiveTabLinkUrl, _wikiType);
                }
            }
        }

        // ================= 图片查看器 =================

        private void OnWikiImageTapped(object sender, TappedRoutedEventArgs e)
        {
            var elem = sender as FrameworkElement;
            if (elem == null) return;

            string url = elem.Tag as string;
            if (string.IsNullOrEmpty(url)) return;

            if (ViewModel != null)
            {
                ViewModel.OpenImageCommand.Execute(url);
            }
            e.Handled = true;
        }

        private void UpdateImageViewerContainerSize()
        {
            if (ImageViewerContainer == null || ViewerImage == null) return;

            double screenW = Window.Current.Bounds.Width;
            double screenH = Window.Current.Bounds.Height;

            var bmp = ViewerImage.Source as BitmapSource;
            if (bmp != null && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
            {
                double aspect = (double)bmp.PixelHeight / bmp.PixelWidth;
                if (aspect > (screenH / screenW))
                {
                    // 长图：按原始比例撑开高度并顶对齐，可上下滚动。
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

        private void OnViewerPrevClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.PrevImageCommand.Execute(null);
        }

        private void OnViewerNextClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.NextImageCommand.Execute(null);
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
                    ext = queryIdx > 0 ? url.Substring(dotIdx, queryIdx - dotIdx).ToLower() : url.Substring(dotIdx).ToLower();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp") ext = ".jpg";
                }

                string fileName = "KuroBBS_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Math.Abs(url.GetHashCode()) + ext;

                var picturesFolder = Windows.Storage.KnownFolders.SavedPictures;
                var file = await picturesFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);

                KuroLogger.Info("IMG_SAVE_OK", "Saved wiki image to SavedPictures: " + fileName);
                ShowSaveToast("已保存到相册");
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("IMG_SAVE_ERR", "Failed saving wiki image: " + ex.Message);
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
    }
}

