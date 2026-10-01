using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using KuroBBS.Models;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    public static class KuroHtmlPostParser
    {
        private static readonly Regex BlockRegex = new Regex(@"<(h[1-6]|p|div|blockquote)[^>]*>(.*?)</\1>|<(img)\s+([^>]*)/?>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex ImgAttrRegex = new Regex(@"(src|width|height)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        private static readonly Regex InlineTagRegex = new Regex(@"<(/?[a-zA-Z0-9]+)([^>]*)>", RegexOptions.Singleline);
        private static readonly Regex StyleColorRegex = new Regex(@"color\s*:\s*([^;""']+)");
        private static readonly Regex EmojiRegex = new Regex(@"_\[/([^\]]+)\]");

        public static List<PostContentBlock> ParseHtml(string html)
        {
            var blocks = new List<PostContentBlock>();
            if (string.IsNullOrWhiteSpace(html)) return blocks;

            // Normalize newlines
            html = html.Replace("\r", "").Replace("\n", "");

            var matches = BlockRegex.Matches(html);
            if (matches.Count == 0)
            {
                // Fallback: single text block
                var runs = ParseInlineHtml(html, false, null);
                if (runs.Count > 0)
                {
                    blocks.Add(new PostContentBlock
                    {
                        BlockType = ContentBlockType.Text,
                        Runs = runs
                    });
                }
                return blocks;
            }

            foreach (Match match in matches)
            {
                string tag = match.Groups[1].Value.ToLower();
                string inner = match.Groups[2].Value;

                // Check for Image Block (standalone <img> or <div> containing <img>)
                if (tag == "img" || (!string.IsNullOrEmpty(inner) && inner.Contains("<img")))
                {
                    string imgTag = (tag == "img") ? match.Value : inner;
                    string src = null;
                    int w = 0, h = 0;

                    var attrMatches = ImgAttrRegex.Matches(imgTag);
                    foreach (Match am in attrMatches)
                    {
                        string attrName = am.Groups[1].Value.ToLower();
                        string attrVal = am.Groups[2].Value;
                        if (attrName == "src") src = attrVal;
                        else if (attrName == "width") int.TryParse(attrVal, out w);
                        else if (attrName == "height") int.TryParse(attrVal, out h);
                    }

                    if (!string.IsNullOrEmpty(src))
                    {
                        bool isBanner = src.Contains("/postBanner/") || (w > 0 && h > 0 && w >= h * 4);
                        blocks.Add(new PostContentBlock
                        {
                            BlockType = isBanner ? ContentBlockType.Banner : ContentBlockType.Image,
                            ImageUrl = src,
                            ImageWidth = w,
                            ImageHeight = h
                        });
                        continue;
                    }
                }

                if (tag.StartsWith("h"))
                {
                    int level = 1;
                    if (tag.Length > 1) int.TryParse(tag.Substring(1), out level);
                    var runs = ParseInlineHtml(inner, true, null);
                    if (runs.Count > 0 && HasVisibleContent(runs))
                    {
                        blocks.Add(new PostContentBlock
                        {
                            BlockType = ContentBlockType.Heading,
                            IsHeading = true,
                            HeadingLevel = level,
                            Runs = runs
                        });
                    }
                }
                else if (tag == "p" || tag == "div" || tag == "blockquote")
                {
                    var runs = ParseInlineHtml(inner, false, null);
                    if (runs.Count > 0 && HasVisibleContent(runs))
                    {
                        blocks.Add(new PostContentBlock
                        {
                            BlockType = ContentBlockType.Text,
                            Runs = runs
                        });
                    }
                }
            }

            return blocks;
        }

        public static List<PostTextRun> ParseInlineHtml(string html, bool isHeading, string defaultColor)
        {
            var runs = new List<PostTextRun>();
            if (string.IsNullOrEmpty(html)) return runs;

            bool bold = isHeading;
            bool italic = false;
            string currentColor = defaultColor;

            int lastIdx = 0;
            var tagMatches = InlineTagRegex.Matches(html);

            var colorStack = new Stack<string>();
            var boldStack = new Stack<bool>();
            var italicStack = new Stack<bool>();

            foreach (Match tm in tagMatches)
            {
                if (tm.Index > lastIdx)
                {
                    string text = html.Substring(lastIdx, tm.Index - lastIdx);
                    AddTextRuns(runs, text, bold, italic, currentColor);
                }

                string fullTagName = tm.Groups[1].Value.ToLower();
                string attrs = tm.Groups[2].Value;
                bool isClosing = fullTagName.StartsWith("/");
                string tag = isClosing ? fullTagName.Substring(1) : fullTagName;

                if (tag == "strong" || tag == "b")
                {
                    if (isClosing)
                    {
                        bold = boldStack.Count > 0 ? boldStack.Pop() : isHeading;
                    }
                    else
                    {
                        boldStack.Push(bold);
                        bold = true;
                    }
                }
                else if (tag == "em" || tag == "i")
                {
                    if (isClosing)
                    {
                        italic = italicStack.Count > 0 ? italicStack.Pop() : false;
                    }
                    else
                    {
                        italicStack.Push(italic);
                        italic = true;
                    }
                }
                else if (tag == "span" || tag == "font")
                {
                    if (isClosing)
                    {
                        currentColor = colorStack.Count > 0 ? colorStack.Pop() : defaultColor;
                    }
                    else
                    {
                        colorStack.Push(currentColor);
                        var sm = StyleColorRegex.Match(attrs);
                        if (sm.Success)
                        {
                            currentColor = sm.Groups[1].Value.Trim();
                        }
                        else if (tag == "font")
                        {
                            var fm = Regex.Match(attrs, @"color\s*=\s*[""']?([^""'\s>]+)");
                            if (fm.Success)
                            {
                                currentColor = fm.Groups[1].Value.Trim();
                            }
                        }
                    }
                }
                else if (tag == "br")
                {
                    AddTextRuns(runs, "\n", bold, italic, currentColor);
                }

                lastIdx = tm.Index + tm.Length;
            }

            if (lastIdx < html.Length)
            {
                string remaining = html.Substring(lastIdx);
                AddTextRuns(runs, remaining, bold, italic, currentColor);
            }

            return runs;
        }

        private static void AddTextRuns(List<PostTextRun> runs, string rawText, bool bold, bool italic, string color)
        {
            if (string.IsNullOrEmpty(rawText)) return;

            string decoded = WebUtility.HtmlDecode(rawText);
            decoded = decoded.Replace('\u00A0', ' ');

            int lastIdx = 0;
            var matches = EmojiRegex.Matches(decoded);
            foreach (Match m in matches)
            {
                if (m.Index > lastIdx)
                {
                    string t = decoded.Substring(lastIdx, m.Index - lastIdx);
                    if (!string.IsNullOrEmpty(t))
                    {
                        runs.Add(new PostTextRun
                        {
                            Text = t,
                            IsBold = bold,
                            IsItalic = italic,
                            ColorHex = color
                        });
                    }
                }

                string emojiRaw = m.Value;
                string emojiUrl = KuroEmojiService.Instance.ResolveEmojiUrl(null, emojiRaw);
                runs.Add(new PostTextRun
                {
                    IsEmoji = true,
                    Text = emojiRaw,
                    EmojiName = emojiRaw,
                    EmojiUrl = emojiUrl
                });

                lastIdx = m.Index + m.Length;
            }

            if (lastIdx < decoded.Length)
            {
                string rem = decoded.Substring(lastIdx);
                if (!string.IsNullOrEmpty(rem))
                {
                    runs.Add(new PostTextRun
                    {
                        Text = rem,
                        IsBold = bold,
                        IsItalic = italic,
                        ColorHex = color
                    });
                }
            }
        }

        private static bool HasVisibleContent(List<PostTextRun> runs)
        {
            if (runs == null || runs.Count == 0) return false;
            foreach (var r in runs)
            {
                if (r.IsEmoji || !string.IsNullOrWhiteSpace(r.Text))
                {
                    return true;
                }
            }
            return false;
        }

        public static Windows.UI.Color? ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr)) return null;
            string c = colorStr.Trim();

            // rgb(r, g, b) or rgba(r, g, b, a)
            if (c.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int start = c.IndexOf('(');
                int end = c.IndexOf(')');
                if (start >= 0 && end > start)
                {
                    string inside = c.Substring(start + 1, end - start - 1);
                    string[] parts = inside.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        byte r, g, b;
                        if (byte.TryParse(parts[0].Trim(), out r) &&
                            byte.TryParse(parts[1].Trim(), out g) &&
                            byte.TryParse(parts[2].Trim(), out b))
                        {
                            byte a = 255;
                            if (parts.Length >= 4)
                            {
                                double alphaDouble;
                                if (double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out alphaDouble))
                                {
                                    if (alphaDouble <= 1.0) a = (byte)(alphaDouble * 255);
                                    else byte.TryParse(parts[3].Trim(), out a);
                                }
                            }

                            // Protect against very dark text on dark background
                            int brightness = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                            if (brightness < 60) return null;

                            return Windows.UI.Color.FromArgb(a, r, g, b);
                        }
                    }
                }
            }

            // Hex (#RRGGBB or #RGB or #AARRGGBB)
            if (c.StartsWith("#"))
            {
                string hex = c.Substring(1).Trim();
                if (hex.Length == 3)
                {
                    hex = string.Format("{0}{0}{1}{1}{2}{2}", hex[0], hex[1], hex[2]);
                }
                if (hex.Length == 6)
                {
                    uint val;
                    if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out val))
                    {
                        byte r = (byte)((val >> 16) & 0xFF);
                        byte g = (byte)((val >> 8) & 0xFF);
                        byte b = (byte)(val & 0xFF);

                        int brightness = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                        if (brightness < 60) return null;

                        return Windows.UI.Color.FromArgb(255, r, g, b);
                    }
                }
                else if (hex.Length == 8)
                {
                    uint val;
                    if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out val))
                    {
                        byte a = (byte)((val >> 24) & 0xFF);
                        byte r = (byte)((val >> 16) & 0xFF);
                        byte g = (byte)((val >> 8) & 0xFF);
                        byte b = (byte)(val & 0xFF);

                        int brightness = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                        if (brightness < 60) return null;

                        return Windows.UI.Color.FromArgb(a, r, g, b);
                    }
                }
            }

            return null;
        }
    }
}
