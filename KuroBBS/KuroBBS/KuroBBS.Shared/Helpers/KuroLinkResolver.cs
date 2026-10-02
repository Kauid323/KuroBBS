using System;
using System.Reflection;

namespace KuroBBS.Helpers
{
    /// <summary>内链解析结果的类型。</summary>
    public enum KuroLinkKind
    {
        /// <summary>无法识别。</summary>
        Unknown = 0,
        /// <summary>Wiki 条目：/item/&lt;entryId&gt;，跳 WikiEntryDetailPage。</summary>
        WikiEntry,
        /// <summary>Wiki 图鉴列表：?fid=&amp;sid=，跳 WikiItemListPage。</summary>
        WikiCatalogue,
        /// <summary>社区帖子：/post/&lt;postId&gt;，跳 PostDetailPage。</summary>
        Post,
        /// <summary>社区话题：/topic/&lt;topicId&gt;，跳 TopicDetailPage。</summary>
        Topic,
        /// <summary>用户主页：/user/&lt;uid&gt;、/userCenter?uid=、/person-center?id=&lt;uid&gt;，跳 UserProfilePage。</summary>
        User,
        /// <summary>站外链接：交给系统浏览器。</summary>
        External
    }

    /// <summary>
    /// 内链解析结果。调用方按 Kind 决定 Frame.Navigate 的目标与参数。
    /// </summary>
    public class KuroLinkTarget
    {
        public KuroLinkKind Kind { get; set; }
        /// <summary>目标 ID（entryId / postId / topicId / uid / catalogueId）。</summary>
        public string Id { get; set; }
        /// <summary>附带标题（列表页有时需要）。</summary>
        public string Title { get; set; }
        /// <summary>原始链接。</summary>
        public string RawUrl { get; set; }

        public bool IsResolved { get { return Kind != KuroLinkKind.Unknown; } }
    }

    /// <summary>
    /// 全软件统一的内链解析器。
    ///
    /// 背景：原先「/item/、sid=、fid=、/post/、外部 http」这套判断被复制粘贴在
    /// WikiItemListPage / GameWikiPnsPage / GameWikiMcPage / WikiEntryDetailPage
    /// 等多处，新增一种链接类型就要改 N 个文件，极易漏改
    /// （曾因此出现「活动卡片点了不跳转」）。
    ///
    /// 现在所有解析集中在这里，调用方只需：
    ///   var target = KuroLinkResolver.Resolve(url);
    ///   if (KuroLinkResolver.TryNavigate(Frame, target, wikiType)) return;
    /// </summary>
    public static class KuroLinkResolver
    {
        // ---------- 通用入口 ----------

        /// <summary>解析任意链接，返回统一的分类结果。</summary>
        public static KuroLinkTarget Resolve(string url, string title = null)
        {
            var target = new KuroLinkTarget { RawUrl = url, Title = title, Kind = KuroLinkKind.Unknown };
            if (string.IsNullOrWhiteSpace(url)) return target;

            string lower = url.ToLowerInvariant();

            // 1. Wiki 条目：/item/<digits>
            string entryId = ExtractPathSegmentId(url, lower, "/item/");
            if (entryId != null)
            {
                target.Kind = KuroLinkKind.WikiEntry;
                target.Id = entryId;
                return target;
            }

            // 2. 社区帖子：/post/<digits> 或 ?postId=
            string postId = TryParsePostIdCore(url, lower);
            if (postId != null)
            {
                target.Kind = KuroLinkKind.Post;
                target.Id = postId;
                return target;
            }

            // 3. 社区话题：/topic/<digits> 或 ?topicId=
            string topicId = ExtractPathSegmentId(url, lower, "/topic/");
            if (topicId == null) topicId = QueryDigits(url, "topicId");
            if (topicId != null)
            {
                target.Kind = KuroLinkKind.Topic;
                target.Id = topicId;
                return target;
            }

            // 4. 用户主页：/user/<digits>、/userCenter/<digits>、/person-center?id=<digits> 或 ?uid=
            //    person-center 是 B 站式个人主页链接，真实 Wiki 正文里大量出现：
            //      https://www.kurobbs.com/person-center?id=10046430
            //    注意：id 这个 key 过于通用（postId/catalogueId 都不用裸 id），
            //    因此只在路径里出现 person-center 时才认它，避免误伤。
            string uid = ExtractPathSegmentId(url, lower, "/user/");
            if (uid == null) uid = ExtractPathSegmentId(url, lower, "/usercenter/");
            if (uid == null) uid = ExtractPathSegmentId(url, lower, "/person-center/");
            if (uid == null && lower.IndexOf("person-center", StringComparison.Ordinal) >= 0)
            {
                uid = QueryDigits(url, "id");
            }
            if (uid == null) uid = QueryDigits(url, "uid");
            if (uid != null)
            {
                target.Kind = KuroLinkKind.User;
                target.Id = uid;
                return target;
            }

            // 5. Wiki 图鉴列表：?fid= / ?sid= / ?catalogueId=
            string catId = null;
            foreach (var key in new[] { "catalogueId", "sid", "fid" })
            {
                catId = QueryDigits(url, key);
                if (catId != null) break;
            }
            if (catId != null)
            {
                target.Kind = KuroLinkKind.WikiCatalogue;
                target.Id = catId;
                return target;
            }

            // 6. 站外链接
            if (lower.StartsWith("http://") || lower.StartsWith("https://"))
            {
                target.Kind = KuroLinkKind.External;
                target.Id = url;
                return target;
            }

            return target;
        }

        /// <summary>
        /// 便捷判断：该链接是否是需要「应用内跳转」的内链（非站外、且能识别）。
        /// </summary>
        public static bool IsInternalLink(string url)
        {
            var t = Resolve(url);
            return t.IsResolved && t.Kind != KuroLinkKind.External;
        }

        // ---------- 兼容旧 API（KuroWikiLinkHelper 的调用点保持可用）----------

        /// <summary>尝试从 URL 中解析社区帖子 ID。</summary>
        public static bool TryParsePostId(string url, out string postId)
        {
            postId = string.IsNullOrEmpty(url) ? null : TryParsePostIdCore(url, url.ToLowerInvariant());
            return postId != null;
        }

        /// <summary>判断链接是否指向社区帖子。</summary>
        public static bool IsPostUrl(string url)
        {
            string id;
            return TryParsePostId(url, out id);
        }

        // ---------- 内部实现 ----------

        private static string TryParsePostIdCore(string url, string lower)
        {
            string id = ExtractPathSegmentId(url, lower, "/post/");
            if (id != null) return id;

            id = QueryDigits(url, "postId");
            if (id != null) return id;

            return QueryDigits(url, "post_id");
        }

        /// <summary>从 “/seg/123” 形态中取出纯数字 123；非纯数字返回 null。</summary>
        private static string ExtractPathSegmentId(string url, string lower, string segment)
        {
            int idx = lower.IndexOf(segment, StringComparison.Ordinal);
            if (idx < 0) return null;

            string sub = url.Substring(idx + segment.Length);
            int cut = sub.IndexOfAny(new[] { '?', '#', '/' });
            if (cut >= 0) sub = sub.Substring(0, cut);
            return IsAllDigits(sub) ? sub : null;
        }

        /// <summary>取出查询串中某个 key 的纯数字值。</summary>
        private static string QueryDigits(string url, string key)
        {
            string value = ExtractQueryValue(url, key);
            return IsAllDigits(value) ? value : null;
        }

        private static string ExtractQueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key)) return null;

            string marker = key + "=";
            int idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            string sub = url.Substring(idx + marker.Length);
            int cut = sub.Length;
            int amp = sub.IndexOf('&');
            int hash = sub.IndexOf('#');
            if (amp >= 0 && amp < cut) cut = amp;
            if (hash >= 0 && hash < cut) cut = hash;
            return sub.Substring(0, cut);
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 内链跳转适配器：把解析结果落到具体的 Frame.Navigate。
    ///
    /// 页面只需一行：
    ///   if (await KuroLinkNavigator.NavigateAsync(Frame, url, wikiType, title)) return;
    /// 即可获得全软件一致的跳转行为，无需各自 switch。
    ///
    /// 放在 Shared 里以便所有页面复用；通过反射定位页面类型，
    /// 避免 Shared → WindowsPhone 的反向引用（分层不允许）。
    /// </summary>
    public static class KuroLinkNavigator
    {
        /// <summary>
        /// 解析并跳转。返回 true 表示已处理（内链已跳转或已交给浏览器）。
        /// </summary>
        /// <param name="frame">当前 Frame</param>
        /// <param name="url">待解析的链接</param>
        /// <param name="wikiType">Wiki 类型（2=战双 / 9=鸣潮），用于 Wiki 内链</param>
        /// <param name="title">可选标题（列表页展示用）</param>
        public static bool Navigate(object frame, string url, int wikiType, string title = null)
        {
            if (frame == null || string.IsNullOrWhiteSpace(url)) return false;

            var target = KuroLinkResolver.Resolve(url, title);
            return Navigate(frame, target, wikiType);
        }

        /// <summary>按已解析的 KuroLinkTarget 跳转。</summary>
        public static bool Navigate(object frame, KuroLinkTarget target, int wikiType)
        {
            if (frame == null || target == null || !target.IsResolved) return false;

            switch (target.Kind)
            {
                case KuroLinkKind.WikiEntry:
                    return TryGo(frame, "WikiEntryDetailPage", wikiType + "|" + target.Id);

                case KuroLinkKind.Post:
                    return TryGo(frame, "PostDetailPage", target.Id);

                case KuroLinkKind.Topic:
                    return TryGo(frame, "TopicDetailPage", target.Id);

                case KuroLinkKind.User:
                    return TryGo(frame, "UserProfilePage", target.Id);

                case KuroLinkKind.WikiCatalogue:
                    return TryGo(frame, "WikiItemListPage",
                        wikiType + "|" + target.Id + "|" + (string.IsNullOrEmpty(target.Title) ? "图鉴列表" : target.Title));

                case KuroLinkKind.External:
                    LaunchExternal(target.RawUrl);
                    return true;
            }

            return false;
        }

        private static bool TryGo(object frame, string pageTypeName, object parameter)
        {
            var pageType = FindPageType(pageTypeName);
            if (pageType == null) return false;

            // WP8.1 / WinRT 下 Type 上的 GetMethod / Assembly 不是直接可用成员，
            // 必须经由 GetTypeInfo()（需要 using System.Reflection）。
            // 注意：Frame 有两个 Navigate 重载（Navigate(Type) / Navigate(Type, object)），
            // 直接用 GetDeclaredMethod("Navigate") 会抛 AmbiguousMatchException，
            // 因此按参数个数精确挑出「(Type, object)」那个。
            System.Reflection.MethodInfo navigate = null;
            try
            {
                var methods = frame.GetType().GetTypeInfo().GetDeclaredMethods("Navigate");
                foreach (var m in methods)
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 2
                        && ps[0].ParameterType == typeof(Type)
                        && ps[1].ParameterType == typeof(object))
                    {
                        navigate = m;
                        break;
                    }
                }
            }
            catch
            {
                return false;
            }

            if (navigate == null) return false;

            try
            {
                navigate.Invoke(frame, new[] { pageType, parameter });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type FindPageType(string simpleName)
        {
            try
            {
                // WP8.1 说明（已核对 v8.1 参考程序集 mscorlib.dll 元数据）：
                //   - Type 上取 GetTypeInfo() 是扩展方法（IntrospectionExtensions），Type 可用；
                //   - Assembly 上 **没有** GetTypeInfo()，也**没有** DeclaredTypes；
                //   - Assembly 直接暴露 DefinedTypes（IEnumerable<TypeInfo>）与 GetTypes()。
                // 因此这里用 Assembly.DefinedTypes，避免再踩 GetTypeInfo/GetTypes 的坑。
                var asm = typeof(KuroLinkNavigator).GetTypeInfo().Assembly;
                foreach (var t in asm.DefinedTypes)
                {
                    if (t.Name == simpleName) return t.AsType();
                }
            }
            catch { }
            return null;
        }

        private static async void LaunchExternal(string url)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            catch
            {
                // 无法打开则忽略
            }
        }
    }
}
