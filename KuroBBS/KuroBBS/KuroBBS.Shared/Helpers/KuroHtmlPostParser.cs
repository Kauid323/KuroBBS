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
        private static readonly Regex StyleFontSizeRegex = new Regex(@"font-size\s*:\s*([0-9\.]+)(px|pt|em|rem|%)?", RegexOptions.IgnoreCase);
        private static readonly Regex StyleWeightRegex = new Regex(@"font-weight\s*:\s*([^;""']+)");
        private static readonly Regex StyleItalicRegex = new Regex(@"font-style\s*:\s*([^;""']+)");
        private static readonly Regex StyleDecorationRegex = new Regex(@"text-decoration\s*:\s*([^;""']+)");
        private static readonly Regex EmojiRegex = new Regex(@"_\[/([^\]]+)\]");

        /// <summary>单个 &lt;img&gt; 标签（用于逐个解析属性，避免跨标签串味）。</summary>
        private static readonly Regex ImgTagRegex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>块级子元素的起始标签（用于判断容器块是否需要递归拆分）。</summary>
        private static readonly Regex BlockChildRegex = new Regex(@"<(p|div|h[1-6]|blockquote)(\s|>)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>
        /// 「标签之间的空白」：`&gt;` 与 `&lt;` 之间的换行 + 缩进。
        ///
        /// HTML 语义上这段空白是**无意义**的（浏览器会折叠掉），但旧代码把它原样交给
        /// `AddTextRuns` → 每个换行都变成一次 `LineBreak`。
        /// 真实案例（【锈夜逐光】疾雷手册，content 是一整张 &lt;table&gt;）：
        /// 正文只有 ~727 个可见字符，却产出 `runs=274`，其中约 240 个是「标签间缩进换行」
        /// 生成的**空行** —— 页面被撑到 ~4800px 几乎全是空白，而这些空行每一个都是
        /// 原生 text layout 对象（WP8.1 上白烧内存）。
        /// 去掉它等价于浏览器的空白折叠，只保留 `&lt;br&gt;` 与闭合块级标签产生的真实换行。
        /// </summary>
        private static readonly Regex InterTagWhitespaceRegex = new Regex(@">\s+<", RegexOptions.Singleline);

        /// <summary>
        /// Word / 旧版编辑器粘贴产生的「条件注释」。
        /// 形态：
        ///   &lt;!--[if gte vml 1]&gt; ... &lt;![endif]--&gt;   （带内容的整段）
        ///   &lt;!--[if !vml]--&gt;&lt;!--[endif]--&gt;            （空壳）
        ///   &lt;!--[if !supportLists]--&gt;...&lt;!--[endif]--&gt;
        /// 里面塞的是 &lt;v:imagedata src="file:///C:/Users/.../clip_image001.png"&gt;
        /// 这类本地路径，留在正文里会：①污染属性解析（后出现的 src 覆盖真正的图片 URL）
        /// ②让用户看到一堆乱字符。必须整段剔除。
        /// 只认 **`]>` 结尾** 的开启标签（`&lt;!-- [if gte vml 1]&gt;`），配到 `&lt;![endif]--&gt;`。
        ///
        /// 千万别把 `-->` 也当成开启标签的结尾：真实数据里存在以 `-->` 结尾的空壳
        /// `&lt;!-- [if !vml]--&gt;`，一旦允许它配对，惰性 `.*?` 就会一路跨到**下一个**
        /// `&lt;![endif]--&gt;`，把中间整段正文和图片一起吞掉
        /// （实测会吃掉「与红光相反」那段和它的配图）。空壳交给下面的通用规则处理。
        /// Singleline 允许跨行。
        /// </summary>
        private static readonly Regex ConditionalCommentRegex =
            new Regex(@"<!--\s*\[if[^\[]*?\]>.*?<!\s*\[endif\]\s*-->", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>
        /// 兜底：任何形式的普通 HTML 注释 &lt;!-- ... --&gt;，**包括** `[if` 开头的。
        /// 真实数据里存在没有配对 `&lt;![endif]&gt;` 的孤儿：
        /// `&lt;!-- [if !vml]--&gt;` 与写成 `&lt;!--[endif]--&gt;`（dash 位置不标准）的闭合，
        /// 只靠上面那条配对规则漏不掉。因为这条是惰性且不跨注释，不会误吞正文。
        /// </summary>
        private static readonly Regex HtmlCommentRegex =
            new Regex(@"<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>
        /// 去掉条件注释与 HTML 注释。所有解析入口都应先过一遍这个。
        /// </summary>
        public static string StripComments(string html)
        {
            if (string.IsNullOrEmpty(html)) return html;
            try
            {
                // 先精确配对整段，再通用兜底，两步缺一不可。
                html = ConditionalCommentRegex.Replace(html, "");
                html = HtmlCommentRegex.Replace(html, "");
            }
            catch { }
            return html;
        }

        private static readonly Dictionary<string, Windows.UI.Color> NamedColors = new Dictionary<string, Windows.UI.Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "red", Windows.UI.Color.FromArgb(255, 255, 69, 58) },
            { "green", Windows.UI.Color.FromArgb(255, 52, 199, 89) },
            { "blue", Windows.UI.Color.FromArgb(255, 10, 132, 255) },
            { "yellow", Windows.UI.Color.FromArgb(255, 255, 214, 10) },
            { "orange", Windows.UI.Color.FromArgb(255, 255, 159, 10) },
            { "purple", Windows.UI.Color.FromArgb(255, 191, 90, 242) },
            { "pink", Windows.UI.Color.FromArgb(255, 255, 55, 95) },
            { "cyan", Windows.UI.Color.FromArgb(255, 100, 210, 255) },
            { "teal", Windows.UI.Color.FromArgb(255, 100, 210, 255) },
            { "gold", Windows.UI.Color.FromArgb(255, 255, 215, 0) },
            { "white", Windows.UI.Color.FromArgb(255, 255, 255, 255) },
            { "gray", Windows.UI.Color.FromArgb(255, 160, 160, 170) },
            { "grey", Windows.UI.Color.FromArgb(255, 160, 160, 170) },
            { "silver", Windows.UI.Color.FromArgb(255, 192, 192, 192) },
            { "lime", Windows.UI.Color.FromArgb(255, 50, 205, 50) },
            { "magenta", Windows.UI.Color.FromArgb(255, 255, 0, 255) },
            { "violet", Windows.UI.Color.FromArgb(255, 238, 130, 238) }
        };

        public static List<PostContentBlock> ParseHtml(string html)
        {
            var blocks = new List<PostContentBlock>();
            if (string.IsNullOrWhiteSpace(html)) return blocks;

            // 先剔除条件注释（Word 残留的 <v:imagedata src="file:///..."> 就藏在这里）
            html = StripComments(html);
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

                // 容器块（div / p / blockquote）内部还有块级子元素时，必须先递归拆分。
                // 否则 BlockRegex 的惰性匹配会把整个
                //   <div class="kr-collapse-content"><p>文字</p><p><img></p>...</div>
                // 当成「一个含 img 的 div」，整段被压成单张图片 —— 正文全丢、
                // 图片位置错乱（常见问题FAQ / 战斗系统 就是这种情况）。
                if (tag != "img" && ContainsBlockChild(inner))
                {
                    var childBlocks = ParseHtml(inner);
                    if (childBlocks.Count > 0)
                    {
                        blocks.AddRange(childBlocks);
                        continue;
                    }
                }

                // Image Block: 块内每出现一个 <img> 就产出一个图片块，
                // 这样 <p><img A><img B></p> 的并排两张图都不会丢。
                bool hasImg = tag == "img" || (!string.IsNullOrEmpty(inner) && inner.Contains("<img"));
                if (hasImg)
                {
                    string imgScope = (tag == "img") ? match.Value : inner;
                    bool emitted = false;

                    foreach (Match it in ImgTagRegex.Matches(imgScope))
                    {
                        string src = null;
                        int w = 0, h = 0;

                        // 只解析「这一个 <img> 标签内部」的属性。
                        // 不能像以前那样对整个块做 ImgAttrRegex：Wiki 正文里常混入
                        // Word 粘贴残留的 <!-- [if gte vml 1]><v:imagedata src="file:///C:/...">
                        // 那段里的 file:// 会因为「后出现的 src 覆盖先出现的」而胜出，
                        // 最终渲染成一个永远加载不出来的本地路径。
                        var attrMatches = ImgAttrRegex.Matches(it.Value);
                        foreach (Match am in attrMatches)
                        {
                            string attrName = am.Groups[1].Value.ToLower();
                            string attrVal = am.Groups[2].Value;
                            if (attrName == "src") src = attrVal;
                            else if (attrName == "width") int.TryParse(attrVal, out w);
                            else if (attrName == "height") int.TryParse(attrVal, out h);
                        }

                        if (string.IsNullOrEmpty(src)) continue;
                        if (!src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            && !src.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;

                        blocks.Add(MakeImageBlock(src, w, h));
                        emitted = true;
                    }

                    if (emitted) continue;
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

        /// <summary>
        /// 容器块内部是否还嵌着块级子元素（&lt;p&gt; / &lt;div&gt; / &lt;h1-6&gt; / &lt;blockquote&gt;）。
        /// 有就必须递归拆分，否则整段会被 BlockRegex 的惰性匹配吞成一个块。
        /// </summary>
        private static bool ContainsBlockChild(string inner)
        {
            return !string.IsNullOrEmpty(inner) && BlockChildRegex.IsMatch(inner);
        }

        /// <summary>按宽高比判定「装饰性横幅 banner」并生成图片块。</summary>
        private static PostContentBlock MakeImageBlock(string src, int w, int h)
        {
            // 判定「装饰性横幅（banner）」的规则必须非常保守：
            // banner 会被渲染成 MaxHeight≈44px 的窄条，一旦把正文里
            // 的正常图片误判成 banner，就会被压扁、看起来「显示不全」。
            //
            // 真实案例：活动帖里的 1400×270 路线图/活动图（宽高比 ≈5.19）
            // 之前因 `w >= h*4` 被误判成 banner，结果只显示一条 44px 的窄缝。
            //
            // 因此：仅当 URL 明确带 /postBanner/ 路径，或「极其扁平」
            // （宽高比 ≥ 8 且绝对高度 ≤ 160）时才当作 banner。
            bool isBanner = src.Contains("/postBanner/") ||
                            (w > 0 && h > 0 && h <= 160 && w >= h * 8);

            return new PostContentBlock
            {
                BlockType = isBanner ? ContentBlockType.Banner : ContentBlockType.Image,
                ImageUrl = src,
                ImageWidth = w,
                ImageHeight = h
            };
        }

        public static List<PostTextRun> ParseInlineHtml(string html, bool isHeading, string defaultColor)
        {
            var runs = new List<PostTextRun>();
            if (string.IsNullOrEmpty(html)) return runs;

            html = StripComments(html);
            if (string.IsNullOrEmpty(html)) return runs;

            // 折叠「标签之间的空白/换行」（HTML 里本就无意义），否则每个缩进换行都会变成一次
            // LineBreak，把正文排成一堆空行（见 InterTagWhitespaceRegex 注释里的真实案例）。
            html = InterTagWhitespaceRegex.Replace(html, "><");

            bool bold = isHeading;
            bool italic = false;
            bool underline = false;
            bool strikethrough = false;
            string currentColor = defaultColor;
            double? currentFontSize = null;

            int lastIdx = 0;
            var tagMatches = InlineTagRegex.Matches(html);

            var colorStack = new Stack<string>();
            var fontSizeStack = new Stack<double?>();
            var boldStack = new Stack<bool>();
            var italicStack = new Stack<bool>();
            var underlineStack = new Stack<bool>();
            var strikeStack = new Stack<bool>();

            foreach (Match tm in tagMatches)
            {
                if (tm.Index > lastIdx)
                {
                    string text = html.Substring(lastIdx, tm.Index - lastIdx);
                    AddTextRuns(runs, text, bold, italic, underline, strikethrough, currentColor, currentFontSize);
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
                else if (tag == "u" || tag == "ins")
                {
                    if (isClosing)
                    {
                        underline = underlineStack.Count > 0 ? underlineStack.Pop() : false;
                    }
                    else
                    {
                        underlineStack.Push(underline);
                        underline = true;
                    }
                }
                else if (tag == "s" || tag == "strike" || tag == "del")
                {
                    if (isClosing)
                    {
                        strikethrough = strikeStack.Count > 0 ? strikeStack.Pop() : false;
                    }
                    else
                    {
                        strikeStack.Push(strikethrough);
                        strikethrough = true;
                    }
                }
                else if (tag == "span" || tag == "font")
                {
                    if (isClosing)
                    {
                        currentColor = colorStack.Count > 0 ? colorStack.Pop() : defaultColor;
                        currentFontSize = fontSizeStack.Count > 0 ? fontSizeStack.Pop() : null;
                    }
                    else
                    {
                        colorStack.Push(currentColor);
                        fontSizeStack.Push(currentFontSize);

                        // Color
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

                        // Font size
                        var fsm = StyleFontSizeRegex.Match(attrs);
                        if (fsm.Success)
                        {
                            double sizeVal;
                            if (double.TryParse(fsm.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out sizeVal))
                            {
                                string unit = fsm.Groups[2].Value.ToLower();
                                if (unit == "em" || unit == "rem") sizeVal = sizeVal * 14.5;
                                else if (unit == "%") sizeVal = (sizeVal / 100.0) * 14.5;
                                if (sizeVal >= 9 && sizeVal <= 36) currentFontSize = sizeVal;
                            }
                        }
                        else if (tag == "font")
                        {
                            var fmSize = Regex.Match(attrs, @"size\s*=\s*[""']?([1-7])");
                            if (fmSize.Success)
                            {
                                int sizeNum;
                                if (int.TryParse(fmSize.Groups[1].Value, out sizeNum))
                                {
                                    // Map font size 1..7 to reasonable pt
                                    double[] sizeMap = { 10.0, 11.5, 13.0, 14.5, 17.0, 20.0, 24.0 };
                                    if (sizeNum >= 1 && sizeNum <= 7) currentFontSize = sizeMap[sizeNum - 1];
                                }
                            }
                        }

                        // Font weight / style / text-decoration in style attribute
                        var wm = StyleWeightRegex.Match(attrs);
                        if (wm.Success && (wm.Groups[1].Value.Contains("bold") || wm.Groups[1].Value.Contains("700") || wm.Groups[1].Value.Contains("800") || wm.Groups[1].Value.Contains("900")))
                        {
                            bold = true;
                        }

                        var im = StyleItalicRegex.Match(attrs);
                        if (im.Success && im.Groups[1].Value.Contains("italic"))
                        {
                            italic = true;
                        }

                        var dm = StyleDecorationRegex.Match(attrs);
                        if (dm.Success)
                        {
                            string decor = dm.Groups[1].Value;
                            if (decor.Contains("underline")) underline = true;
                            if (decor.Contains("line-through")) strikethrough = true;
                        }
                    }
                }
                else if (tag == "br")
                {
                    AddTextRuns(runs, "\n", bold, italic, underline, strikethrough, currentColor, currentFontSize);
                }
                else if (tag == "p" || tag == "div" || tag == "tr" || tag == "li" || tag == "h1" || tag == "h2" || tag == "h3" || tag == "h4" || tag == "h5" || tag == "h6")
                {
                    if (isClosing)
                    {
                        AddTextRuns(runs, "\n", bold, italic, underline, strikethrough, currentColor, currentFontSize);
                    }
                }

                lastIdx = tm.Index + tm.Length;
            }

            if (lastIdx < html.Length)
            {
                string remaining = html.Substring(lastIdx);
                AddTextRuns(runs, remaining, bold, italic, underline, strikethrough, currentColor, currentFontSize);
            }

            return runs;
        }

        private static void AddTextRuns(List<PostTextRun> runs, string rawText, bool bold, bool italic, bool underline, bool strikethrough, string color, double? fontSize)
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
                            IsUnderline = underline,
                            IsStrikethrough = strikethrough,
                            ColorHex = color,
                            FontSize = fontSize
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
                        IsUnderline = underline,
                        IsStrikethrough = strikethrough,
                        ColorHex = color,
                        FontSize = fontSize
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

        /// <summary>
        /// 解析正文里显式指定的文字颜色，并做**深色主题适配**。
        ///
        /// 真实抓包里，正文 HTML 经常带
        ///   &lt;span style="color: rgb(0, 0, 0);"&gt;…&lt;/span&gt;
        /// 这类**黑色 / 近黑色**：既有网页/Word 复制粘贴带来的默认黑，
        /// 也有“为浅色表格单元格背景配的深色字”（例：#34495E / #5B544A / #666666）。
        /// 本 App 是深色主题，照搬这些颜色就会出现「正文黑字看都看不见」。
        ///
        /// 判据：只对「最亮通道 &lt; 110」的颜色返回 null —— 调用方
        /// （InlineRunsHelper / PostDetailPage）遇到 null 就不设 Foreground，
        /// 文字自动继承宿主 RichTextBlock 的浅色前景，等于**专门把黑字适配成主题文字色**。
        ///
        /// 为什么用「最亮通道」而不是逐通道 &lt;30：像 #333333 / #666666 / #34495E
        /// 这类近黑、深灰、暗板岩也属于“看不见”的范畴，必须一起兜住；
        /// 而红 rgb(224, 62, 45)、绿、蓝、金、橙等**彩色文字**至少有一个通道 ≥110，
        /// 一律原样返回，做到「不影响其他颜色的文字」。
        /// </summary>
        public static Windows.UI.Color? ParseColor(string colorStr)
        {
            var c = ParseColorRaw(colorStr);
            if (!c.HasValue) return null;

            // 太暗 → 在深色主题上不可读 → 退回主题文字色（不设 Foreground）
            int maxChannel = Math.Max(c.Value.R, Math.Max(c.Value.G, c.Value.B));
            if (maxChannel < 110) return null;

            return c;
        }

        /// <summary>纯粹的色值解析（不含主题适配），供 ParseColor 调用。</summary>
        private static Windows.UI.Color? ParseColorRaw(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr)) return null;
            string c = colorStr.Trim(' ', ';', '"', '\'');

            // Named colors
            if (NamedColors.ContainsKey(c))
            {
                return NamedColors[c];
            }

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

                        return Windows.UI.Color.FromArgb(a, r, g, b);
                    }
                }
            }

            return null;
        }
    }
}
