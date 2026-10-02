using System;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using KuroBBS.Helpers;
using KuroBBS.Models;
using KuroBBS.Services;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class UserProfilePage : Page
    {
        public UserProfileViewModel ViewModel { get; private set; }
        private DispatcherTimer _toastTimer;

        public UserProfilePage()
        {
            this.InitializeComponent();
            ViewModel = new UserProfileViewModel();
            this.DataContext = ViewModel;

            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            HookInfiniteScroll();
        }

        private void HookInfiniteScroll()
        {
            AttachListViewScroll(UserPostsListView, () =>
            {
                if (!ViewModel.IsLoadingPosts)
                {
                    var ignore = ViewModel.LoadMorePostsAsync();
                }
            });

            AttachListViewScroll(UserCollectionsListView, () =>
            {
                if (!ViewModel.IsLoadingCollections)
                {
                    var ignore = ViewModel.LoadMoreCollectionsAsync();
                }
            });

            AttachListViewScroll(UserCommentsListView, () =>
            {
                if (!ViewModel.IsLoadingComments)
                {
                    var ignore = ViewModel.LoadMoreCommentsAsync();
                }
            });
        }

        private void AttachListViewScroll(ListView listView, Action loadMoreAction)
        {
            if (listView == null) return;

            Action tryHook = () =>
            {
                var sv = FindVisualChild<ScrollViewer>(listView);
                if (sv != null)
                {
                    sv.ViewChanged += (s, e) =>
                    {
                        DismissBottomBar();
                        var scroller = s as ScrollViewer;
                        if (scroller == null) return;
                        if (scroller.ScrollableHeight > 0 && (scroller.VerticalOffset >= scroller.ScrollableHeight - 350 || (scroller.VerticalOffset / scroller.ScrollableHeight >= 0.75)))
                        {
                            loadMoreAction();
                        }
                    };
                }
            };

            tryHook();
            listView.Loaded += (s, e) => tryHook();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T) return (T)child;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            string userId = null;
            PostAuthor author = null;

            if (e.Parameter is string)
            {
                userId = (string)e.Parameter;
            }
            else if (e.Parameter is PostAuthor)
            {
                author = (PostAuthor)e.Parameter;
                userId = author.UserId;
            }
            else if (e.Parameter is UserProfile)
            {
                var up = (UserProfile)e.Parameter;
                userId = up.UserId;
                ViewModel.Profile = up;
            }
            else if (e.Parameter is PostItem)
            {
                var post = (PostItem)e.Parameter;
                if (post.Author != null)
                {
                    author = post.Author;
                    userId = author.UserId;
                }
            }

            HardwareButtons.BackPressed += OnHardwareBackPressed;

            var coreWin = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            if (coreWin != null)
            {
                coreWin.PointerPressed += OnCoreWindowPointer;
                coreWin.PointerMoved += OnCoreWindowPointer;
                coreWin.PointerReleased += OnCoreWindowPointer;
            }

            await ViewModel.InitializeAsync(userId, author);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            HardwareButtons.BackPressed -= OnHardwareBackPressed;

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

            if (!BackPressHelper.CanHandleBackPress())
            {
                e.Handled = true;
                return;
            }

            if (this.Frame != null && this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
                e.Handled = true;
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private void OnFollowingTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Profile != null && !string.IsNullOrEmpty(ViewModel.Profile.UserId))
            {
                this.Frame.Navigate(typeof(FollowListPage), ViewModel.Profile.UserId);
            }
        }

        private void OnAvatarTapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel != null && ViewModel.Profile != null && !string.IsNullOrEmpty(ViewModel.Profile.AvatarUrl))
            {
                ViewModel.OpenImageCommand.Execute(ViewModel.Profile.AvatarUrl);
            }
        }

        private void UpdateImageViewerContainerSize()
        {
            if (ImageViewerContainer != null)
            {
                double w = Window.Current.Bounds.Width;
                double h = Window.Current.Bounds.Height;
                ImageViewerContainer.Width = w;
                ImageViewerContainer.Height = h;
            }
        }

        private void ResetImageViewerScale()
        {
            UpdateImageViewerContainerSize();
            if (ImageViewerScrollViewer != null)
            {
                ImageViewerScrollViewer.ChangeView(0, 0, 1.0f, true);
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

                string fileName = "KuroBBS_Avatar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Math.Abs(url.GetHashCode()) + ext;

                StorageFolder picturesFolder = KnownFolders.SavedPictures;
                var file = await picturesFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteBytesAsync(file, bytes);

                KuroLogger.Info("IMG_SAVE_OK", "Saved avatar image to SavedPictures: " + fileName);
                ShowSaveToast("已保存到相册");
            }
            catch (Exception ex)
            {
                KuroLogger.Error("IMG_SAVE_ERR", "Failed to save avatar image: " + ex.Message);
                ShowSaveToast("保存失败: " + ex.Message);
            }
        }

        private void ShowSaveToast(string msg)
        {
            if (SaveToastBorder == null || SaveToastText == null) return;

            SaveToastText.Text = msg;
            SaveToastBorder.Visibility = Visibility.Visible;

            if (_toastTimer != null)
            {
                _toastTimer.Stop();
            }

            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(2.5);
            _toastTimer.Tick += (s, args) =>
            {
                _toastTimer.Stop();
                SaveToastBorder.Visibility = Visibility.Collapsed;
            };
            _toastTimer.Start();
        }

        private async void OnCommentLikeButtonClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            if (btn == null || ViewModel == null) return;
            var comment = btn.DataContext as UserCommentNoticeItem;
            if (comment != null)
            {
                await ViewModel.ToggleCommentLikeAsync(comment);
            }
        }

        private void OnPostItemClick(object sender, ItemClickEventArgs e)
        {
            var post = e.ClickedItem as PostItem;
            if (post != null)
            {
                this.Frame.Navigate(typeof(PostDetailPage), post);
            }
        }

        private void OnCommentPostTapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null && element.Tag != null)
            {
                string postId = element.Tag.ToString();
                if (!string.IsNullOrEmpty(postId))
                {
                    var dummyPost = new PostItem { PostId = postId };
                    this.Frame.Navigate(typeof(PostDetailPage), dummyPost);
                }
            }
        }

        private void OnUserPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DismissBottomBar();
        }

        private void OnCoreWindowPointer(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
        {
            DismissBottomBar();
        }

        private void DismissBottomBar()
        {
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
    }
}
