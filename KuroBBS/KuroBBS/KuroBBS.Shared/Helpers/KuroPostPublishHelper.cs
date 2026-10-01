using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Data.Json;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    public class PostEditorImage
    {
        public string Url { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int IsCover { get; set; }
        public string LocalFileName { get; set; }

        public string DimensionSummary
        {
            get
            {
                if (Width > 0 && Height > 0)
                {
                    return string.Format("{0} × {1}", (int)Width, (int)Height);
                }
                return "已上传";
            }
        }
    }

    public static class KuroPostPublishHelper
    {
        private static readonly Regex EmojiRegex = new Regex(@"_\[/([^\]]+)\]");
        private static readonly Regex TagRegex = new Regex(@"<[^>]+>");

        public static string BuildContentJson(string rawText, List<PostEditorImage> images)
        {
            var contentArray = new JsonArray();

            string cleanText = rawText != null ? StripHtml(rawText) : "";
            if (!string.IsNullOrWhiteSpace(cleanText))
            {
                var textBlockObj = new JsonObject();
                textBlockObj.SetNamedValue("contentType", JsonValue.CreateNumberValue(1));
                textBlockObj.SetNamedValue("content", JsonValue.CreateStringValue(cleanText));

                var childrenArray = new JsonArray();
                int lastIdx = 0;
                var matches = EmojiRegex.Matches(cleanText);

                foreach (Match m in matches)
                {
                    if (m.Index > lastIdx)
                    {
                        string plainSeg = cleanText.Substring(lastIdx, m.Index - lastIdx);
                        if (!string.IsNullOrEmpty(plainSeg))
                        {
                            var child = new JsonObject();
                            child.SetNamedValue("content", JsonValue.CreateStringValue(plainSeg));
                            child.SetNamedValue("target", JsonValue.Parse("null"));
                            child.SetNamedValue("type", JsonValue.CreateNumberValue(1));
                            childrenArray.Add(child);
                        }
                    }

                    string emojiTag = m.Value;
                    string emojiId = KuroEmojiService.Instance.GetEmojiId(emojiTag);

                    var emojiChild = new JsonObject();
                    emojiChild.SetNamedValue("content", JsonValue.CreateStringValue(emojiTag));
                    if (!string.IsNullOrEmpty(emojiId))
                    {
                        emojiChild.SetNamedValue("target", JsonValue.CreateStringValue(emojiId));
                    }
                    else
                    {
                        emojiChild.SetNamedValue("target", JsonValue.Parse("null"));
                    }
                    emojiChild.SetNamedValue("type", JsonValue.CreateNumberValue(2));
                    childrenArray.Add(emojiChild);

                    lastIdx = m.Index + m.Length;
                }

                if (lastIdx < cleanText.Length)
                {
                    string remaining = cleanText.Substring(lastIdx);
                    if (!string.IsNullOrEmpty(remaining))
                    {
                        var child = new JsonObject();
                        child.SetNamedValue("content", JsonValue.CreateStringValue(remaining));
                        child.SetNamedValue("target", JsonValue.Parse("null"));
                        child.SetNamedValue("type", JsonValue.CreateNumberValue(1));
                        childrenArray.Add(child);
                    }
                }

                if (childrenArray.Count == 0)
                {
                    var child = new JsonObject();
                    child.SetNamedValue("content", JsonValue.CreateStringValue(cleanText));
                    child.SetNamedValue("target", JsonValue.Parse("null"));
                    child.SetNamedValue("type", JsonValue.CreateNumberValue(1));
                    childrenArray.Add(child);
                }

                textBlockObj.SetNamedValue("children", childrenArray);
                textBlockObj.SetNamedValue("imgHeight", JsonValue.CreateNumberValue(0.0));
                textBlockObj.SetNamedValue("imgWidth", JsonValue.CreateNumberValue(0.0));
                textBlockObj.SetNamedValue("isCover", JsonValue.CreateNumberValue(0));
                textBlockObj.SetNamedValue("topicFlag", JsonValue.CreateNumberValue(0));
                textBlockObj.SetNamedValue("aiInfo", JsonValue.Parse("null"));
                textBlockObj.SetNamedValue("aiRuleId", JsonValue.Parse("null"));
                textBlockObj.SetNamedValue("contentLink", JsonValue.Parse("null"));
                textBlockObj.SetNamedValue("uri", JsonValue.Parse("null"));
                textBlockObj.SetNamedValue("url", JsonValue.Parse("null"));

                contentArray.Add(textBlockObj);
            }

            if (images != null)
            {
                foreach (var img in images)
                {
                    if (string.IsNullOrEmpty(img.Url)) continue;

                    var imgBlockObj = new JsonObject();
                    imgBlockObj.SetNamedValue("contentType", JsonValue.CreateNumberValue(2));
                    imgBlockObj.SetNamedValue("url", JsonValue.CreateStringValue(img.Url));
                    imgBlockObj.SetNamedValue("imgWidth", JsonValue.CreateNumberValue(img.Width > 0 ? img.Width : 750.0));
                    imgBlockObj.SetNamedValue("imgHeight", JsonValue.CreateNumberValue(img.Height > 0 ? img.Height : 750.0));
                    imgBlockObj.SetNamedValue("isCover", JsonValue.CreateNumberValue(img.IsCover));
                    imgBlockObj.SetNamedValue("topicFlag", JsonValue.CreateNumberValue(0));
                    imgBlockObj.SetNamedValue("aiInfo", JsonValue.Parse("null"));
                    imgBlockObj.SetNamedValue("aiRuleId", JsonValue.Parse("null"));
                    imgBlockObj.SetNamedValue("children", JsonValue.Parse("null"));
                    imgBlockObj.SetNamedValue("content", JsonValue.Parse("null"));
                    imgBlockObj.SetNamedValue("contentLink", JsonValue.Parse("null"));
                    imgBlockObj.SetNamedValue("uri", JsonValue.Parse("null"));

                    contentArray.Add(imgBlockObj);
                }
            }

            return contentArray.Stringify();
        }

        public static string BuildH5Content(string rawText, List<PostEditorImage> images)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(rawText))
            {
                string formatted = rawText.Trim();
                if (!formatted.StartsWith("<p>", StringComparison.OrdinalIgnoreCase) && !formatted.StartsWith("<div", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = formatted.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            sb.Append("<p><br></p>\n");
                        }
                        else
                        {
                            sb.Append("<p><font color=\"#222222\">").Append(line).Append("</font></p>\n");
                        }
                    }
                }
                else
                {
                    sb.Append(formatted).Append("\n");
                }
            }

            if (images != null)
            {
                foreach (var img in images)
                {
                    if (string.IsNullOrEmpty(img.Url)) continue;
                    double w = img.Width > 0 ? img.Width : 750.0;
                    double h = img.Height > 0 ? img.Height : 750.0;
                    sb.Append(string.Format("<div class=\"w-e_img-card\" data-module-name=\"img-card\" has-delete=\"true\" contenteditable=\"false\">\n <img class=\"w_e_network_image\" img-des=\"{0},{1},{2}\" listen-img-load=\"true\" src=\"{2}\">\n</div>\n",
                        (int)w, (int)h, img.Url));
                }
            }

            return sb.ToString();
        }

        public static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string unescaped = html.Replace("&nbsp;", " ").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
            return TagRegex.Replace(unescaped, "");
        }
    }
}
