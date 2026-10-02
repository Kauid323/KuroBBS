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
                            if (i > 0)
                            {
                                paragraph.Inlines.Add(new LineBreak());
                            }
                            if (!string.IsNullOrEmpty(lines[i]))
                            {
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
