using System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using KuroBBS.Models;
using KuroBBS.ViewModels;

namespace KuroBBS
{
    public sealed partial class NoticeListPage : Page
    {
        private MessageHubViewModel _vm;
        private string _senderId;
        private string _senderType;
        private string _senderIcon;   // 会话头像：通知条目自身没有头像时用它兜底

        public NoticeListPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var sender = e.Parameter as MessageSenderItem;
            _senderId = sender != null ? sender.SenderId : (e.Parameter as string);
            _senderType = sender != null ? sender.SenderType : "";
            _senderIcon = sender != null ? sender.Icon : "";

            _vm = new MessageHubViewModel();
            this.DataContext = _vm;

            if (!string.IsNullOrEmpty(_senderId))
            {
                await _vm.LoadNoticeListAsync(_senderId, _senderType, true, _senderIcon);
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame != null && Frame.CanGoBack) Frame.GoBack();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_senderId)) await _vm.LoadNoticeListAsync(_senderId, _senderType, true, _senderIcon);
        }

        private async void OnLoadMoreClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_senderId)) await _vm.LoadNoticeListAsync(_senderId, _senderType, false, _senderIcon);
        }

        private async void OnNoticeClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as MessageNoticeItem;
            if (item == null || Frame == null) return;

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
            Frame.Navigate(typeof(PostDetailPage), post);
        }
    }
}
