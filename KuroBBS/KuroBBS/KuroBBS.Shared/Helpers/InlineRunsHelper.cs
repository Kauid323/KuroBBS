using System;
using System.Collections;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    public static class InlineRunsHelper
    {
        public static readonly DependencyProperty RunsSourceProperty =
            DependencyProperty.RegisterAttached(
                "RunsSource",
                typeof(object),
                typeof(InlineRunsHelper),
                new PropertyMetadata(null, OnRunsSourceChanged));

        public static object GetRunsSource(DependencyObject obj)
        {
            return obj.GetValue(RunsSourceProperty);
        }

        public static void SetRunsSource(DependencyObject obj, object value)
        {
            obj.SetValue(RunsSourceProperty, value);
        }

        private static void OnRunsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rtb = d as RichTextBlock;
            if (rtb == null) return;

            try
            {
                // 显式声明换行方式（**注意：这不是崩溃的修复**）。
                // 官方文档：`RichTextBlock.TextWrapping` 默认就是 `Wrap`
                // （和 `TextBlock` 默认 `NoWrap` 不一样），所以这里只是把隐含默认写明，
                // 宿主 XAML 漏写也不会出错。
                // 之前怀疑「无限宽度 ScrollViewer + 超长单行导致原生文本布局崩溃」——
                // 该假设已被官方文档否定；真实死因见 KuroLazyImage / KuroWikiService 里的
                // WP8.1 内存上限说明（解析跑完后在 XAML 实现阶段 OOM，无托管异常、exit 1）。
                rtb.TextWrapping = TextWrapping.Wrap;

                rtb.Blocks.Clear();
                if (e.NewValue == null) return;

                IEnumerable<PostTextRun> runs = e.NewValue as IEnumerable<PostTextRun>;
                string textStr = e.NewValue as string;
                if (runs == null && !string.IsNullOrEmpty(textStr))
                {
                    runs = KuroEmojiService.Instance.ParseTextToRuns(textStr);
                }

                if (runs == null) return;

                var paragraph = new Paragraph();
                int emitted = 0;
                // 「当前是否位于一行开头」：用于 ①折叠连续换行（避免一长串空行把页面撑高）
                // ②丢掉行首缩进空白（HTML 里无意义）。初值 true 保证正文不以空行开头。
                bool atLineStart = true;

                // 【防御】单个 RichTextBlock 的 Inline 数量上限。
                // 某些 Wiki 条目的富文本会产出上千个 Run/LineBreak/InlineUIContainer，
                // WP8.1 的文本布局在极端 Inline 数下会异常耗时（表现为卡死/闪退）。
                // 超过上限后停止追加，保证页面可用且不崩。
                const int MaxInlines = 4000;

                foreach (var run in runs)
                {
                    if (run == null) continue;
                    if (emitted >= MaxInlines) break;

                    // 正文插图（评论/回复的 commentContent / replyContent 里 contentType=2 的图片块）。
                    // 这类 run 没有文字，必须单独渲染成 InlineUIContainer，否则整张图丢失。
                    if (run.IsImage)
                    {
                        if (!string.IsNullOrEmpty(run.ImageUrl))
                        {
                            try
                            {
                                // 按真实宽高比算一个确定的展示尺寸：
                                // 宽 240（正文列宽足够放下，不会把行撑爆），高上限 400。
                                double dispW = 240;
                                double dispH = dispW;
                                if (run.ImageWidth > 0 && run.ImageHeight > 0)
                                {
                                    dispH = dispW * run.ImageHeight / run.ImageWidth;
                                }
                                const double MaxImageHeight = 400;
                                if (dispH > MaxImageHeight)
                                {
                                    dispW = dispW * MaxImageHeight / dispH;
                                    dispH = MaxImageHeight;
                                }
                                if (dispH < 60) dispH = 60;

                                var imgContainer = new InlineUIContainer();
                                var img = new Image
                                {
                                    Width = dispW,
                                    Height = dispH,
                                    Stretch = Stretch.Uniform,
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Margin = new Thickness(0, 3, 0, 3)
                                };
                                // 走统一懒加载：后台下载 + 限流解码 + 离屏回收（只解到 360 宽）
                                KuroLazyImage.SetDecodeWidth(img, 360);
                                KuroLazyImage.SetSourceUrl(img, run.ImageUrl);
                                imgContainer.Child = img;
                                paragraph.Inlines.Add(imgContainer);
                                emitted++;
                                atLineStart = false;
                            }
                            catch
                            {
                                // 容器创建失败：忽略该图，不影响其余正文
                            }
                        }
                        continue;
                    }

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
                            try
                            {
                                var container = new InlineUIContainer();
                                var img = new Image
                                {
                                    Width = 20,
                                    Height = 20,
                                    Stretch = Stretch.Uniform,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    Margin = new Thickness(2, 0, 2, -3)
                                };
                                img.Source = KuroImageCache.Instance.GetImageSource(emojiUrl);
                                container.Child = img;
                                paragraph.Inlines.Add(container);
                                emitted++;
                                atLineStart = false;
                                continue;
                            }
                            catch
                            {
                                // Fallback to text if emoji container fails
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(run.Text))
                    {
                        string normalized = run.Text.Replace("\r\n", "\n").Replace("\r", "\n");
                        string[] lines = normalized.Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (emitted >= MaxInlines) break;
                            if (i > 0)
                            {
                                // 连续换行折叠为一个。真实案例（疾雷手册，content 是一整张 <table>）：
                                // HTML 里 `</p></div></div>` 这类相邻闭合标签会各产生一个 LineBreak，
                                // 叠加标签间缩进换行后正文出现 ~240 个空行，页面被撑到 ~4800px。
                                // 这些空行每一个都是原生 text layout 对象，在 WP8.1 上纯属白烧内存。
                                if (!atLineStart)
                                {
                                    paragraph.Inlines.Add(new LineBreak());
                                    emitted++;
                                }
                                atLineStart = true;
                            }
                            if (!string.IsNullOrEmpty(lines[i]))
                            {
                                if (emitted >= MaxInlines) break;
                                // 行首缩进空白（HTML 语义上无意义）直接丢弃，避免出现莫名其妙的缩进。
                                if (atLineStart && string.IsNullOrWhiteSpace(lines[i])) continue;
                                var r = new Run { Text = lines[i] };
                                if (run.IsBold)
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
                                    var color = KuroHtmlPostParser.ParseColor(run.ColorHex);
                                    if (color.HasValue)
                                    {
                                        r.Foreground = new SolidColorBrush(color.Value);
                                    }
                                }
                                paragraph.Inlines.Add(r);
                                emitted++;
                                atLineStart = false;
                            }
                        }
                    }
                }

                rtb.Blocks.Add(paragraph);
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("INLINE_RUNS_ERR", "Error rendering runs: " + ex.Message);
            }
        }
    }
}
