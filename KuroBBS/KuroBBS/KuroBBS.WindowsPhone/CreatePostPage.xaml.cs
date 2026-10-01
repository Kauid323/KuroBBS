using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
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
    public sealed partial class CreatePostPage : Page, IFileOpenPickerContinuable
    {
        private readonly CreatePostViewModel _viewModel;

        public CreatePostPage()
        {
            this.InitializeComponent();
            _viewModel = new CreatePostViewModel();
            this.DataContext = _viewModel;

#if WINDOWS_PHONE_APP
            var inputPane = InputPane.GetForCurrentView();
            inputPane.Showing += InputPane_Showing;
            inputPane.Hiding += InputPane_Hiding;
#endif
        }

#if WINDOWS_PHONE_APP
        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            FormatToolbarContainer.Margin = new Thickness(0, 0, 0, args.OccludedRect.Height);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            FormatToolbarContainer.Margin = new Thickness(0, 0, 0, 0);
        }
#endif

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is int)
            {
                int gameId = (int)e.Parameter;
                if (gameId == 2 || gameId == 3)
                {
                    _viewModel.SelectGame(gameId);
                }
            }

            RebuildForumFlyout();
            await _viewModel.InitializeAsync();
        }

        public async void ContinueWithFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            if (args != null && args.Files != null && args.Files.Count > 0)
            {
                foreach (var file in args.Files)
                {
                    await _viewModel.UploadAndAddImageFileAsync(file);
                }
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnPublishClick(object sender, RoutedEventArgs e)
        {
            var res = await _viewModel.PublishAsync();
            if (res != null && res.Success)
            {
                if (!string.IsNullOrEmpty(res.PostId))
                {
                    Frame.Navigate(typeof(PostDetailPage), res.PostId);
                }
                else if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
        }

        private void OnGameFlyoutClick(object sender, RoutedEventArgs e)
        {
        }

        private void OnSelectZhanShuangFlyout(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectGame(2);
            RebuildForumFlyout();
        }

        private void OnSelectMingChaoFlyout(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectGame(3);
            RebuildForumFlyout();
        }

        private void OnForumFlyoutClick(object sender, RoutedEventArgs e)
        {
            RebuildForumFlyout();
        }

        private void RebuildForumFlyout()
        {
            if (ForumMenuFlyout == null) return;
            ForumMenuFlyout.Items.Clear();

            foreach (var forum in _viewModel.ForumCategories)
            {
                var item = new MenuFlyoutItem
                {
                    Text = forum.Name,
                    Tag = forum
                };
                item.Click += (s, args) =>
                {
                    var clickedItem = s as MenuFlyoutItem;
                    if (clickedItem != null && clickedItem.Tag is ForumCategoryItem)
                    {
                        _viewModel.SelectedForum = clickedItem.Tag as ForumCategoryItem;
                    }
                };
                ForumMenuFlyout.Items.Add(item);
            }
        }

        private void OnContentTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
        }

        private void OnContentTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
        }

        private void OnAddImageClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".gif");
            picker.PickMultipleFilesAndContinue();
        }

        private void OnDeleteImageClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null && btn.Tag is PostEditorImage)
            {
                _viewModel.RemoveImage(btn.Tag as PostEditorImage);
            }
        }

        private void OnColorPickerToggleClick(object sender, RoutedEventArgs e)
        {
            ColorStripPanel.Visibility = (ColorStripPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
            EmojiPickerPanel.Visibility = Visibility.Collapsed;
        }

        private void OnCloseColorPanelClick(object sender, RoutedEventArgs e)
        {
            ColorStripPanel.Visibility = Visibility.Collapsed;
        }

        private void OnApplyPresetColorClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                string hex = btn.Tag.ToString();
                WrapOrInsertText(string.Format("<font color=\"{0}\">", hex), "</font>", "彩色文字");
                ColorStripPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnEmojiPickerToggleClick(object sender, RoutedEventArgs e)
        {
            EmojiPickerPanel.Visibility = (EmojiPickerPanel.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
            ColorStripPanel.Visibility = Visibility.Collapsed;
        }

        private void OnCloseEmojiPanelClick(object sender, RoutedEventArgs e)
        {
            EmojiPickerPanel.Visibility = Visibility.Collapsed;
        }

        private void OnSelectEmojiPackageClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null && btn.Tag is EmojiPackageGroup)
            {
                _viewModel.SelectedEmojiPackage = btn.Tag as EmojiPackageGroup;
            }
        }

        private void OnEmojiItemClick(object sender, ItemClickEventArgs e)
        {
            var emoji = e.ClickedItem as EmojiEntryItem;
            if (emoji != null)
            {
                InsertTextAtSelection(emoji.TagText);
            }
        }

        private void OnFormatBoldClick(object sender, RoutedEventArgs e)
        {
            WrapOrInsertText("<b>", "</b>", "粗体");
        }

        private void OnFormatItalicClick(object sender, RoutedEventArgs e)
        {
            WrapOrInsertText("<i>", "</i>", "斜体");
        }

        private void OnFormatUnderlineClick(object sender, RoutedEventArgs e)
        {
            WrapOrInsertText("<u>", "</u>", "下划线");
        }

        private void OnFormatHeadingClick(object sender, RoutedEventArgs e)
        {
            _viewModel.InsertHeading("大标题");
        }

        private void OnInsertTopicClick(object sender, RoutedEventArgs e)
        {
            string topicName = _viewModel.SelectedGameId == 2 ? "战双帕弥什" : "鸣潮";
            _viewModel.InsertTopicTag(topicName);
        }

        private void OnTogglePreviewClick(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsPreviewMode)
            {
                RenderPreview();
                _viewModel.IsPreviewMode = true;
            }
            else
            {
                _viewModel.IsPreviewMode = false;
            }
        }

        private void RenderPreview()
        {
            PreviewRichTextBlock.Blocks.Clear();
            var parsedBlocks = KuroHtmlPostParser.ParseHtml(_viewModel.Content);
            if (parsedBlocks != null && parsedBlocks.Count > 0)
            {
                foreach (var block in parsedBlocks)
                {
                    if (block.Runs != null && block.Runs.Count > 0)
                    {
                        var paragraph = new Paragraph();
                        foreach (var run in block.Runs)
                        {
                            if (run.IsEmoji && !string.IsNullOrEmpty(run.EmojiUrl))
                            {
                                var inlineUi = new InlineUIContainer();
                                var img = new Image
                                {
                                    Source = new BitmapImage(new Uri(run.EmojiUrl)),
                                    Width = 24,
                                    Height = 24,
                                    Margin = new Thickness(2, 0, 2, -4)
                                };
                                inlineUi.Child = img;
                                paragraph.Inlines.Add(inlineUi);
                            }
                            else
                            {
                                var tr = new Run { Text = run.Text ?? "" };
                                if (run.IsBold) tr.FontWeight = FontWeights.Bold;
                                if (run.IsItalic) tr.FontStyle = FontStyle.Italic;
                                if (!string.IsNullOrEmpty(run.ColorHex))
                                {
                                    try
                                    {
                                        string hex = run.ColorHex.Trim('#');
                                        if (hex.Length == 6)
                                        {
                                            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                                            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                                            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                                            tr.Foreground = new SolidColorBrush(Color.FromArgb(255, r, g, b));
                                        }
                                    }
                                    catch { }
                                }
                                else
                                {
                                    tr.Foreground = new SolidColorBrush(Colors.White);
                                }
                                paragraph.Inlines.Add(tr);
                            }
                        }
                        PreviewRichTextBlock.Blocks.Add(paragraph);
                    }
                }
            }
        }

        private void WrapOrInsertText(string openTag, string closeTag, string defaultText)
        {
            int selStart = ContentTextBox.SelectionStart;
            int selLen = ContentTextBox.SelectionLength;
            string current = ContentTextBox.Text ?? "";

            if (selLen > 0 && selStart >= 0 && selStart + selLen <= current.Length)
            {
                string selectedText = current.Substring(selStart, selLen);
                string replacement = string.Format("{0}{1}{2}", openTag, selectedText, closeTag);
                ContentTextBox.Text = current.Substring(0, selStart) + replacement + current.Substring(selStart + selLen);
                ContentTextBox.SelectionStart = selStart + replacement.Length;
            }
            else
            {
                string toInsert = string.Format("{0}{1}{2}", openTag, defaultText, closeTag);
                if (selStart >= 0 && selStart <= current.Length)
                {
                    ContentTextBox.Text = current.Substring(0, selStart) + toInsert + current.Substring(selStart);
                    ContentTextBox.SelectionStart = selStart + toInsert.Length;
                }
                else
                {
                    ContentTextBox.Text = current + toInsert;
                    ContentTextBox.SelectionStart = ContentTextBox.Text.Length;
                }
            }
        }

        private void InsertTextAtSelection(string toInsert)
        {
            int selStart = ContentTextBox.SelectionStart;
            string current = ContentTextBox.Text ?? "";
            if (selStart >= 0 && selStart <= current.Length)
            {
                ContentTextBox.Text = current.Substring(0, selStart) + toInsert + current.Substring(selStart);
                ContentTextBox.SelectionStart = selStart + toInsert.Length;
            }
            else
            {
                ContentTextBox.Text = current + toInsert;
                ContentTextBox.SelectionStart = ContentTextBox.Text.Length;
            }
        }
    }
}
