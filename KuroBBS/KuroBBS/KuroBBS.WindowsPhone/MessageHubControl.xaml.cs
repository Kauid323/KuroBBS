using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class MessageHubControl : UserControl
    {
        private MessageHubViewModel ViewModel
        {
            get { return DataContext as MessageHubViewModel; }
        }

        private readonly bool[] _tabLoaded = new bool[5];

        public MessageHubControl()
        {
            this.InitializeComponent();

            // WP8.1：内层 Pivot 首次被实现时，不保证会为初始选中项(索引 0)触发 SelectionChanged，
            // 而「通知」会话列表(GET /user/notice/senders)只由该事件驱动 → 首次进入 消息 tab
            // 列表会一直是空的。这里用 Loaded / DataContextChanged 兜底触发一次首屏加载。
            this.Loaded += OnMessageHubLoaded;
            this.DataContextChanged += OnMessageHubDataContextChanged;
        }

        private void OnMessageHubLoaded(object sender, RoutedEventArgs e)
        {
            EnsureFirstTabLoaded();
        }

        private void OnMessageHubDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            EnsureFirstTabLoaded();
        }

        /// <summary>
        /// 兜底加载「通知」会话列表（GET /user/notice/senders）。
        /// 已加载过或正在加载时直接返回，可安全重复调用。
        /// </summary>
        private async void EnsureFirstTabLoaded()
        {
            var vm = ViewModel;
            if (vm == null || _tabLoaded[0]) return;
            _tabLoaded[0] = true;
            await vm.LoadNoticeSendersAsync();
        }

        private static Frame RootFrame
        {
            get { return Window.Current.Content as Frame; }
        }

        private async void OnHubPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = ViewModel;
            if (vm == null) return;

            int idx = HubPivot.SelectedIndex;
            if (idx < 0 || idx >= _tabLoaded.Length) return;

            if (_tabLoaded[idx])
            {
                // 正在加载中就别重入（会和 Loaded 兜底的首屏加载撞车）
                if (vm.IsBusy) return;
                // 如果已加载但列表为空，允许重新加载（首次加载可能因快速切换被中断）
                if (IsTabEmpty(vm, idx))
                {
                    _tabLoaded[idx] = false;
                }
                else return;
            }
            _tabLoaded[idx] = true;

            switch (idx)
            {
                case 0: await vm.LoadNoticeSendersAsync(); break;
                case 1: await vm.LoadCommentNoticesAsync(); break;
                case 2: await vm.LoadLikeNoticesAsync(); break;
                case 3: await vm.LoadAtMeAsync(); break;
                case 4: await vm.LoadFansAsync(); break;
            }
        }

        private static bool IsTabEmpty(MessageHubViewModel vm, int idx)
        {
            if (vm == null) return true;
            switch (idx)
            {
                case 0: return vm.NoticeSenders == null || vm.NoticeSenders.Count == 0;
                case 1: return vm.CommentNotices == null || vm.CommentNotices.Count == 0;
                case 2: return vm.LikeNotices == null || vm.LikeNotices.Count == 0;
                case 4: return vm.Fans == null || vm.Fans.Count == 0;
                default: return false;
            }
        }

        #region 通知 → 通知列表页

        private void OnSenderClick(object sender, ItemClickEventArgs e)
        {
            var senderItem = e.ClickedItem as MessageSenderItem;
            if (senderItem == null) return;
            if (RootFrame != null) RootFrame.Navigate(typeof(NoticeListPage), senderItem);
        }

        #endregion

        #region 评论和回复

        private void OnCommentClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as MessageNoticeItem;
            if (item == null) return;
            if (item.IsReplyToMe)
            {
                if (RootFrame != null) RootFrame.Navigate(typeof(ReplyListPage), item);
            }
            else
            {
                NavigateToPost(item);
            }
        }

        private void OnViewReplyClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn != null ? btn.Tag as MessageNoticeItem : null;
            if (item != null && RootFrame != null) RootFrame.Navigate(typeof(ReplyListPage), item);
        }

        private async void OnLoadMoreCommentClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) await ViewModel.LoadCommentNoticesAsync(false);
        }

        #endregion

        #region 点赞

        private void OnLikeClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as MessageNoticeItem;
            if (item != null) NavigateToPost(item);
        }

        private async void OnLoadMoreLikeClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) await ViewModel.LoadLikeNoticesAsync(false);
        }

        #endregion

        #region 新增粉丝

        private void OnFanClick(object sender, ItemClickEventArgs e)
        {
            var fan = e.ClickedItem as MessageFanItem;
            if (fan != null && RootFrame != null) RootFrame.Navigate(typeof(UserProfilePage), fan.UserId);
        }

        private async void OnFollowBackClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var fan = btn != null ? btn.Tag as MessageFanItem : null;
            if (fan != null && ViewModel != null) await ViewModel.FollowBackAsync(fan);
        }

        private async void OnLoadMoreFanClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) await ViewModel.LoadFansAsync(false);
        }

        #endregion

        #region 公共：跳转到帖子详情并定位评论

        private async void NavigateToPost(MessageNoticeItem item)
        {
            if (RootFrame == null) return;

            string postId = !string.IsNullOrEmpty(item.TargetPostId) ? item.TargetPostId : item.PostId;
            if (string.IsNullOrEmpty(postId))
            {
                if (!string.IsNullOrEmpty(item.TargetUrl))
                {
                    try { await Windows.System.Launcher.LaunchUriAsync(new Uri(item.TargetUrl)); } catch { }
                }
                else
                {
                    await new MessageDialog("该通知没有可跳转的帖子").ShowAsync();
                }
                return;
            }

            var post = new PostItem
            {
                PostId = postId,
                GameId = item.GameId,
                Title = item.NoticeTitle,
                TargetCommentId = item.PostCommentId
            };
            RootFrame.Navigate(typeof(PostDetailPage), post);
        }

        #endregion
    }
}
