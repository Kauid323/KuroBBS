using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    /// <summary>
    /// 懒加载 / 异步解码的 Image 附加行为（**视口感知 + 离屏回收**）。
    ///
    /// 背景：图鉴 &gt; 画册 / 萌新入门指南页 这类条目一个组件里可能有 40+ 张全宽大图，
    /// 而且 WikiEntryDetailPage 是「一个 ScrollViewer + StackPanel + 多层 ItemsControl」的
    /// **非虚拟化**结构：所有 Image 会在页面加载时**一次性全部进入可视树**。
    ///
    /// 仅靠「Loaded 才加载」是不够的（它们会同时 Loaded）。本行为进一步做到：
    /// 1. 只有图片**真正进入 ScrollViewer 视口**（含预取余量）才发起下载 + 解码；
    /// 2. 图片**滚出视口**（超出「数量 + 字节」双重预算）时取消在途加载并**摘下 Image.Source**，
    ///    但**保留已解码位图在 KuroImageCache 里** —— 滚回来直接命中、瞬时显示，
    ///    不会重新下载/解码（用户曾反馈：滑动时图片总是重新加载）。
    ///    真正撑爆 WP8.1 内存的解码像素，由缓存自身的**字节预算 LRU** 负责淘汰；
    ///    淘汰时因为 UI 引用已被摘下，位图才真正可被 GC 回收。
    /// 3. 每次 Loaded / 视口变化都会重新判定，保证滚回视野的图片能立即重新挂上。
    ///
    /// 这一切都在 UI 线程上做判定（TransformToVisual / 事件），
    /// 真正的下载 + 解码仍在后台线程，受 KuroImageCache 的并发闸门限流。
    /// </summary>
    public static class KuroLazyImage
    {
        /// <summary>视口预取余量（像素）：在视口上下各多加载这么多，滚动时更顺滑。</summary>
        private const double PrefetchMargin = 400;

        /// <summary>
        /// 未加载图片的「假定高度」。
        /// 关键：这些 Image 未设 Width/Height（Stretch=Uniform + MaxHeight），
        /// 加载前 ActualHeight == 0，几十张会**重叠在同一 Y 上**，
        /// 导致视口判定认为「全都在视野内」→ 又变成一次性全部解码。
        /// 因此对 ActualHeight &lt;= 0 的图片按此高度估算，让纵向排布真实展开，
        /// 视口裁剪才能真正生效。滚到近处会立刻真实加载。
        /// </summary>
        private const double AssumedPlaceholderHeight = 220;

        public static readonly DependencyProperty SourceUrlProperty =
            DependencyProperty.RegisterAttached(
                "SourceUrl",
                typeof(string),
                typeof(KuroLazyImage),
                new PropertyMetadata(null, OnSourceUrlChanged));

        public static string GetSourceUrl(DependencyObject obj)
        {
            return (string)obj.GetValue(SourceUrlProperty);
        }

        public static void SetSourceUrl(DependencyObject obj, string value)
        {
            obj.SetValue(SourceUrlProperty, value);
        }

        public static readonly DependencyProperty DecodeWidthProperty =
            DependencyProperty.RegisterAttached(
                "DecodeWidth",
                typeof(int),
                typeof(KuroLazyImage),
                new PropertyMetadata(480));

        public static int GetDecodeWidth(DependencyObject obj)
        {
            return (int)obj.GetValue(DecodeWidthProperty);
        }

        public static void SetDecodeWidth(DependencyObject obj, int value)
        {
            obj.SetValue(DecodeWidthProperty, value);
        }

        private static readonly DependencyProperty LoadTokenProperty =
            DependencyProperty.RegisterAttached(
                "LoadToken",
                typeof(CancellationTokenSource),
                typeof(KuroLazyImage),
                new PropertyMetadata(null));

        /// <summary>标记「已排队加载」，避免 Loaded 多次触发导致重复下载。</summary>
        private static readonly DependencyProperty LoadStartedProperty =
            DependencyProperty.RegisterAttached(
                "LoadStarted",
                typeof(bool),
                typeof(KuroLazyImage),
                new PropertyMetadata(false));

        /// <summary>
        /// WP8.1 的 FrameworkElement 没有 IsLoaded（那是 WPF/UWP 的属性），
        /// 这里自己维护「是否已在可视树中」的状态，供模板复用时判断。
        /// </summary>
        private static readonly DependencyProperty InTreeProperty =
            DependencyProperty.RegisterAttached(
                "InTree",
                typeof(bool),
                typeof(KuroLazyImage),
                new PropertyMetadata(false));

        // ================= 视口登记表 =================
        // 每个 ScrollViewer 维护一个「待视口判定的 Image」集合，
        // 只在 ViewChanged 时批量判定，避免每个 Image 都挂一次事件。
        private static readonly DependencyProperty ViewportLinkedProperty =
            DependencyProperty.RegisterAttached(
                "ViewportLinked",
                typeof(bool),
                typeof(KuroLazyImage),
                new PropertyMetadata(false));

        private class ViewportRegistry
        {
            // 用非泛型 WeakReference（WP8.1 / Win8.1 都保证存在），避免泛型实现在两平台上的差异。
            public readonly List<WeakReference> Images = new List<WeakReference>();
            public bool Hooked;
            /// <summary>是否还需要在「布局完成后」再做一次初始视口判定。</summary>
            public bool NeedInitialEval;
            /// <summary>已登记图片总数（仅用于诊断日志）。</summary>
            public int RegisterCount;
            /// <summary>布局未就绪时的重试计数/标志。</summary>
            public int RetryCount;
            public bool RetryScheduled;
            public readonly object Sync = new object();
        }

        private static readonly Dictionary<ScrollViewer, ViewportRegistry> _registries =
            new Dictionary<ScrollViewer, ViewportRegistry>();

        /// <summary>
        /// 已经因为「挂错目标类型」告警过的元素类型名。
        /// 只在每种类型上告警一次，避免列表里成百上千个 item 刷爆日志。
        /// </summary>
        private static readonly HashSet<string> _warnedBadTargets = new HashSet<string>();

        private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null)
            {
                // 【重要】本附加属性**只支持 Image 元素**。
                // 挂到 ImageBrush 等其它元素上以前是「静默什么都不做」——曾导致
                // 「通知 / 定位回复」两个页面的圆形头像永远不加载，且日志里毫无线索。
                // 圆形头像请改用 ImageBrush + ImageCacheConverter（App.xaml 已注册）。
                if (e.NewValue != null)
                {
                    try
                    {
                        string typeName = d.GetType().Name;
                        bool firstTime;
                        lock (_warnedBadTargets) { firstTime = _warnedBadTargets.Add(typeName); }
                        if (firstTime)
                        {
                            KuroLogger.Warn("LAZY_IMG_TARGET",
                                "KuroLazyImage.SourceUrl only supports Image, but was attached to a " +
                                typeName + ". It will do nothing. Use ImageCacheConverter for ImageBrush.");
                        }
                    }
                    catch { }
                }
                return;
            }

            // URL 变化：先彻底重置（取消在途、清 Source、清状态）
            ResetImage(image);

            string url = e.NewValue as string;
            if (string.IsNullOrWhiteSpace(url))
            {
                try { image.Source = null; } catch { }
                return;
            }

            image.Loaded -= OnImageLoaded;
            image.Loaded += OnImageLoaded;
            image.Unloaded -= OnImageUnloaded;
            image.Unloaded += OnImageUnloaded;

            // 已在可视树中的情况（模板复用）直接触发判定。
            if ((bool)image.GetValue(InTreeProperty))
            {
                TryLoadIfVisible(image);
            }
        }

        private static void ResetImage(Image image)
        {
            var oldCts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (oldCts != null)
            {
                try { oldCts.Cancel(); oldCts.Dispose(); } catch { }
                image.SetValue(LoadTokenProperty, null);
            }
            image.SetValue(LoadStartedProperty, false);
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            image.SetValue(InTreeProperty, false);
            UnregisterFromViewport(image);
            image.ImageOpened -= OnImageOpened;

            // 离开可视树：取消在途加载、摘下 Source 引用、重置排队标记。
            // 必须重置 LoadStarted，否则再次进入视野时 BeginLoad 会因「已排队」直接 return，
            // 缩略图永久空白。
            //
            // 【关键】这里**不再**把图片从内存缓存里摘除。旧实现一离屏就 EvictMemoryCache，
            // 导致返回本页 / 滚回视野必然 miss → 重新读盘 + 重新解码
            // （用户实测：滑动时图片总是重新加载）。现在解码结果留在缓存里，
            // 滚回来直接命中、瞬时显示；内存安全交给 KuroImageCache 的字节预算 LRU。
            DetachSource(image);
        }

        private static void OnImageLoaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            image.SetValue(InTreeProperty, true);

            // 之前被 Unloaded 回收过时 CTS 已取消并置空，这里重建一个再续播。
            var cts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (cts == null || cts.IsCancellationRequested)
            {
                string curUrl = GetSourceUrl(image);
                if (string.IsNullOrWhiteSpace(curUrl)) return;
                cts = new CancellationTokenSource();
                image.SetValue(LoadTokenProperty, cts);
            }

            // 挂在最近的 ScrollViewer 上，做视口判定 + 离屏回收
            RegisterToViewport(image);
            TryLoadIfVisible(image);

            // 解码完成后重跑一次预算：此时 KuroImageCache 已按**真实像素尺寸**修正了
            // 该图的字节占用（长图可达估算值的数倍），据此把远端的图及时回收掉，
            // 否则「数量 + 字节」预算会因低估而失效。
            image.ImageOpened -= OnImageOpened;
            image.ImageOpened += OnImageOpened;
        }

        /// <summary>
        /// 一张图解码完成 → 重跑该 ScrollViewer 的预算评估。
        /// 注意：这里只是**重新评估**（可能摘掉远端图片的 Source），
        /// 并不会删除内存缓存条目，所以不会造成「滚回来重新加载」。
        /// </summary>
        private static void OnImageOpened(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;
            if (!(bool)image.GetValue(InTreeProperty)) return;

            var sv = FindAncestorScrollViewer(image);
            if (sv == null) return;
            EvaluateAllForViewport(sv);
        }

        // ================= 视口登记 =================

        private static void RegisterToViewport(Image image)
        {
            if ((bool)image.GetValue(ViewportLinkedProperty)) return;

            var sv = FindAncestorScrollViewer(image);
            if (sv == null) return;

            ViewportRegistry reg;
            lock (_registries)
            {
                if (!_registries.TryGetValue(sv, out reg))
                {
                    reg = new ViewportRegistry();
                    _registries[sv] = reg;
                }
            }

            lock (reg.Sync)
            {
                reg.Images.Add(new WeakReference(image));
                reg.RegisterCount++;
                if (reg.RegisterCount % 10 == 0)
                {
                    KuroLogger.Trace("LAZY_REG count=" + reg.RegisterCount);
                }
                if (!reg.Hooked)
                {
                    reg.Hooked = true;
                    sv.ViewChanged += OnScrollViewerViewChanged;
                    // 首次布局完成后需要再做一次视口判定：Loaded 触发时布局尚未完成，
                    // TransformToVisual 取到的位置不可靠（可能全为 0）。
                    reg.NeedInitialEval = true;
                    sv.LayoutUpdated += OnScrollViewerLayoutUpdated;
                    sv.Unloaded += OnScrollViewerUnloaded;
                }
            }
            image.SetValue(ViewportLinkedProperty, true);
        }

        private static void UnregisterFromViewport(Image image)
        {
            image.SetValue(ViewportLinkedProperty, false);
            // registry 里用 WeakReference，Image 被回收后自然会失效；
            // 这里不主动摘除（摘除需反查所有 ScrollViewer，成本高且无必要）。
        }

        private static void OnScrollViewerUnloaded(object sender, RoutedEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;
            lock (_registries)
            {
                _registries.Remove(sv);
            }
        }

        private static void OnScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;
            EvaluateAllForViewport(sv);
        }

        /// <summary>
        /// 首次布局完成后补一次视口判定（Loaded 时布局未就绪，位置不可靠）。
        /// 判定一次后即摘掉 LayoutUpdated，避免每次布局都全量扫描。
        /// </summary>
        private static void OnScrollViewerLayoutUpdated(object sender, object e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            ViewportRegistry reg;
            lock (_registries)
            {
                if (!_registries.TryGetValue(sv, out reg)) return;
            }
            if (reg == null) return;

            bool need;
            lock (reg.Sync) { need = reg.NeedInitialEval; }
            if (!need) return;

            // 布局完成：做一次全量判定，然后解除挂钩。
            lock (reg.Sync) { reg.NeedInitialEval = false; }
            try { sv.LayoutUpdated -= OnScrollViewerLayoutUpdated; } catch { }

            EvaluateAllForViewport(sv);
        }

        private static void EvaluateAllForViewport(ScrollViewer sv)
        {
            ViewportRegistry reg;
            lock (_registries)
            {
                if (!_registries.TryGetValue(sv, out reg)) return;
            }
            if (reg == null) return;

            List<Image> alive;
            lock (reg.Sync)
            {
                alive = new List<Image>(reg.Images.Count);
                for (int i = reg.Images.Count - 1; i >= 0; i--)
                {
                    var img = reg.Images[i].Target as Image;
                    if (img == null)
                    {
                        reg.Images.RemoveAt(i);
                        continue;
                    }
                    alive.Add(img);
                }
            }

            // 【硬上限】统一收口：先算出每张图相对视口的距离，只允许「最近的 N 张」持有位图，
            // 其余全部回收。即便几何判定在边界情况下失灵，这一条也能保证同时解码/常驻的
            // 图片数量有**绝对上限**，从根上杜绝 OOM。
            EvaluateWithBudget(sv, alive);
        }

        /// <summary>
        /// 同时「允许持有已解码位图」的图片**数量**上限（次要闸门）。
        /// 真正的内存闸门是 <see cref="MaxLiveBytes"/>：这里只是兜底，
        /// 避免一屏几十张小缩略图全部挂上引用。
        /// </summary>
        private const int MaxLiveImages = 12;

        /// <summary>
        /// 同时「允许持有已解码位图」的**字节**上限（主闸门）。
        /// 与 KuroImageCache.MaxMemoryCacheBytes 同量级 —— 视口内实际需要同时显示的像素
        /// 远小于此，这个上限只在「连续大长图」时才会触发，把远端的提前摘掉。
        /// </summary>
        private const long MaxLiveBytes = 20L * 1024 * 1024;

        private static void EvaluateWithBudget(ScrollViewer sv, List<Image> images)
        {
            if (images == null || images.Count == 0) return;

            // 计算每张图的可见性与「到视口的距离」
            var candidates = new List<ImageDistance>(images.Count);
            double viewTop = -PrefetchMargin;
            double viewBottom = sv.ViewportHeight + PrefetchMargin;
            bool viewportUsable = sv.ViewportHeight > 0;

            foreach (var img in images)
            {
                if (!(bool)img.GetValue(InTreeProperty)) continue;

                double top, bottom;
                if (!TryGetRelativeVerticalBounds(sv, img, out top, out bottom))
                {
                    continue; // 取不到位置：本轮跳过（下一轮会再算）
                }

                double dist;
                if (bottom >= viewTop && top <= viewBottom) dist = 0;              // 可见
                else if (top > viewBottom) dist = top - viewBottom;                // 在下方
                else dist = viewTop - bottom;                                      // 在上方

                candidates.Add(new ImageDistance { Image = img, Distance = dist });
            }

            if (candidates.Count == 0) return;

            // 视口不可用（布局未完成）时，不做任何判定 —— 直接返回，等下一次事件。
            // 这是避免「布局未完成 → 全部误判可见 → 一次性全量解码」的关键。
            if (!viewportUsable)
            {
                KuroLogger.Trace(string.Format(
                    "LAZY_BUDGET_DEFER regs={0} cands={1} viewportH=0 (layout not ready)",
                    images.Count, candidates.Count));
                // 布局仍未就绪：安排一次重试，否则可能再也没有事件触发判定 → 图片永久空白。
                ScheduleRetry(sv);
                return;
            }

            // 距离升序：可见的（0）排最前
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            // 【双重预算】数量 + 字节。按距离由近到远依次放行，
            // 一旦预算爆掉，其后的（更远的）全部回收。
            // 字节数优先取「已解码真实大小」，未解码的用保守估算 —— 否则连续长图会把内存顶爆。
            int kept = 0, recycled = 0, liveCount = 0;
            long liveBytes = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];

                long est = 0;
                if (liveCount < MaxLiveImages)
                {
                    try
                    {
                        est = KuroImageCache.Instance.GetEstimatedBytes(
                            GetSourceUrl(c.Image), GetDecodeWidth(c.Image));
                    }
                    catch { est = 0; }
                }

                // 最近的一张永远放行（保证视口内至少有一张能显示）；
                // 之后必须同时满足「数量未超」且「字节未超」。
                bool keep = liveCount < MaxLiveImages
                            && (liveCount == 0 || liveBytes + est <= MaxLiveBytes);

                if (keep)
                {
                    liveBytes += est;
                    liveCount++;
                    if (!(bool)c.Image.GetValue(LoadStartedProperty)) kept++;
                    BeginLoad(c.Image);
                }
                else if ((bool)c.Image.GetValue(LoadStartedProperty))
                {
                    RecycleImage(c.Image);
                    recycled++;
                }
            }

            KuroLogger.Trace(string.Format(
                "LAZY_BUDGET regs={0} cands={1} keep={2}(new={3}) recycle={4} vpH={5} liveKB={6} minDist={7}",
                images.Count, candidates.Count, liveCount, kept, recycled,
                (int)sv.ViewportHeight, (int)(liveBytes / 1024), (int)candidates[0].Distance));
        }

        private class ImageDistance
        {
            public Image Image;
            public double Distance;
        }

        /// <summary>
        /// 布局未就绪时安排一次重试（最多 <see cref="MaxEvalRetries"/> 次），
        /// 避免「所有 Loaded 回调都在布局完成前跑完 → 之后再无事件 → 图片永久空白」。
        /// </summary>
        private static void ScheduleRetry(ScrollViewer sv)
        {
            ViewportRegistry reg;
            lock (_registries)
            {
                if (!_registries.TryGetValue(sv, out reg)) return;
            }
            if (reg == null) return;

            bool schedule;
            lock (reg.Sync)
            {
                schedule = reg.RetryScheduled == false && reg.RetryCount < MaxEvalRetries;
                if (schedule)
                {
                    reg.RetryScheduled = true;
                    reg.RetryCount++;
                }
            }
            if (!schedule) return;

            var dispatcher = sv.Dispatcher;
            if (dispatcher == null)
            {
                lock (reg.Sync) { reg.RetryScheduled = false; }
                return;
            }

            var ignore = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                lock (reg.Sync) { reg.RetryScheduled = false; }
                EvaluateAllForViewport(sv);
            });
        }

        private const int MaxEvalRetries = 12;

        // ================= 核心：视口判定 =================

        /// <summary>
        /// Loaded / URL 变化后的判定入口。
        /// **不直接加载**，而是把该 ScrollViewer 下的所有已登记图片交给带预算的统一评估，
        /// 这样「最多 N 张同时持有位图」的硬上限永远生效。
        /// 另外延迟一拍（Low 优先级）执行，让布局先完成——否则位置全为 0，会全量误判为可见。
        /// </summary>
        private static void TryLoadIfVisible(Image image)
        {
            var dispatcher = image.Dispatcher;
            if (dispatcher == null)
            {
                // 无法调度：退化为直接加载，保证不空白
                BeginLoad(image);
                return;
            }

            // 故意的 fire-and-forget（不等待）：这里只是排一次「布局后判定」，
            // 结果无需等待；用局部变量吞掉 CS4014 警告。
            var ignore = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                if (!(bool)image.GetValue(InTreeProperty)) return;
                var sv = FindAncestorScrollViewer(image);
                if (sv == null)
                {
                    // 不在可滚动容器里：进入可视树即加载（无预算约束，但这类通常很少）
                    BeginLoad(image);
                    return;
                }
                EvaluateAllForViewport(sv);
            });
        }

        /// <summary>
        /// 计算 image 相对 ScrollViewer **视口**的上下边界（0 = 视口顶部，向上为负）。
        /// `TransformToVisual(sv)` 得到的就是相对 ScrollViewer 控件（视口）的坐标，
        /// 已隐含当前滚动位移，**不能**再叠加 VerticalOffset。
        /// </summary>
        private static bool TryGetRelativeVerticalBounds(ScrollViewer sv, FrameworkElement elem,
                                                         out double top, out double bottom)
        {
            top = 0; bottom = 0;
            try
            {
                var t = elem.TransformToVisual(sv);
                var p1 = t.TransformPoint(new Point(0, 0));
                double h = elem.ActualHeight;
                // 未加载时高度为 0，会让大量图片重叠在同一位置 → 视口判定失效。
                // 用假定高度估算，保证纵向真实展开、视口裁剪有效。
                if (h <= 0) h = AssumedPlaceholderHeight;
                var p2 = t.TransformPoint(new Point(0, h));
                top = p1.Y;
                bottom = p2.Y;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 找到最近的**可纵向滚动**的祖先 ScrollViewer。
        /// 页面里还有不少 `HorizontScrollBarVisibility="Auto"` 的内层横向 ScrollViewer，
        /// 视口判定必须用外层纵向容器，否则坐标系不对、判定失效。
        /// </summary>
        private static ScrollViewer FindAncestorScrollViewer(DependencyObject start)
        {
            try
            {
                ScrollViewer firstAny = null;
                var cur = VisualTreeHelper.GetParent(start);
                while (cur != null)
                {
                    var sv = cur as ScrollViewer;
                    if (sv != null)
                    {
                        if (firstAny == null) firstAny = sv;
                        // 纵向可滚动的才是我们要的视口容器
                        if (sv.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
                        {
                            return sv;
                        }
                    }
                    cur = VisualTreeHelper.GetParent(cur);
                }
                return firstAny;
            }
            catch { }
            return null;
        }

        // ================= 加载 / 回收 =================

        private static void BeginLoad(Image image)
        {
            if (image == null) return;
            if (!(bool)image.GetValue(InTreeProperty)) return;

            // 同一 URL 只排队一次（Unloaded / 回收时会被重置，保证复用可重载）
            if ((bool)image.GetValue(LoadStartedProperty)) return;
            string url = GetSourceUrl(image);
            if (string.IsNullOrWhiteSpace(url)) return;
            image.SetValue(LoadStartedProperty, true);

            int decodeWidth = GetDecodeWidth(image);
            if (decodeWidth <= 0) decodeWidth = 480;

            // 【关键】BeginLoad 运行在 UI 线程（Loaded 事件 / 属性变更回调 / ViewChanged），
            // 必须在这里就把 Dispatcher 取出来，再带进后台任务。
            var dispatcher = image.Dispatcher;

            var cts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (cts == null || cts.IsCancellationRequested)
            {
                cts = new CancellationTokenSource();
                image.SetValue(LoadTokenProperty, cts);
            }
            var token = cts.Token;

            // 完全后台执行；KuroImageCache 内部有限流与磁盘/内存缓存。
            // 注意：不要在后台线程写附加 DP（有跨线程校验风险），
            // 是否持有位图由 UI 线程上的 LoadStarted 标记推断即可。
            Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    await KuroImageCache.Instance.LoadIntoAsync(image, dispatcher, url, decodeWidth);
                }
                catch (Exception ex)
                {
                    KuroLogger.Warn("LAZY_IMG_ERR", "Lazy image load failed: " + ex.Message);
                }
            }, token);
        }

        /// <summary>
        /// 超出预算时的回收：取消在途加载 + 摘下 `Image.Source` 引用，**保留内存缓存条目**。
        /// 滚回视野时命中缓存 → 瞬时显示，不会重新下载/解码。
        ///
        /// 解码像素的真正回收由 `KuroImageCache` 的字节预算 LRU 负责：
        /// 一旦某条目被它淘汰，而这里又已摘掉了 UI 引用，位图就再无强引用 → 可被 GC 回收。
        /// 二者配合才是「既省内存、又不重复加载」。
        /// </summary>
        private static void RecycleImage(Image image)
        {
            DetachSource(image);
        }

        /// <summary>
        /// 取消在途加载 + 摘下 `Image.Source` 引用（**不动**内存缓存）。
        /// </summary>
        private static void DetachSource(Image image)
        {
            if (image == null) return;
            ResetImage(image);
            ReleaseSource(image);
        }

        private static void ReleaseSource(Image image)
        {
            try { image.Source = null; } catch { }
        }
    }
}
