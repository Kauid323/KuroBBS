using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class ReplyListPage : Page
    {
        private MessageHubViewModel _vm;
        private string _postCommentId;
        private string _postId;

        public ReplyListPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var item = e.Parameter as MessageNoticeItem;
            _postCommentId = item != null ? item.PostCommentId : "";
            // postId 优先；缺失时回退到 TargetPostId（由 contentId / link 解析而来），
            // 保证「原贴」按钮在任何入口下都可用。
            _postId = item != null
                ? (!string.IsNullOrEmpty(item.PostId) ? item.PostId : item.TargetPostId)
                : "";

            _vm = new MessageHubViewModel();
            this.DataContext = _vm;

            // 没有帖子 id 时「原贴」按钮无意义，直接置灰（正常情况下 postId 一定存在）
            if (GoToPostButton != null) GoToPostButton.IsEnabled = !string.IsNullOrEmpty(_postId);

            if (!string.IsNullOrEmpty(_postCommentId) && !string.IsNullOrEmpty(_postId))
            {
                await _vm.LoadReplyListAsync(_postCommentId, _postId);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_postCommentId) && !string.IsNullOrEmpty(_postId))
                await _vm.LoadReplyListAsync(_postCommentId, _postId);
        }

        /// <summary>CommandBar「原贴」：跳转到该评论所属的原帖详情，并定位到我的评论。</summary>
        private void OnGoToPostClick(object sender, RoutedEventArgs e)
        {
            NavigateToPost();
        }

        private void OnReplyClick(object sender, ItemClickEventArgs e)
        {
            NavigateToPost();
        }

        private void NavigateToPost()
        {
            if (Frame == null || string.IsNullOrEmpty(_postId)) return;
            var post = new PostItem
            {
                PostId = _postId,
                GameId = 0,
                Title = "原贴",
                TargetCommentId = _postCommentId
            };
            Frame.Navigate(typeof(PostDetailPage), post);
        }
    }
}
