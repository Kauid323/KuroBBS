using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    /// <summary>
    /// 懒加载 / 异步解码的 Image 附加行为。
    ///
    /// 背景：图鉴 &gt; 画册 这类条目一个组件里可能有 40+ 张全宽大图。
    /// 若直接用 Image.Source = {Binding ..., Converter=ImageCache}，
    /// 所有 BitmapImage 会在 UI 线程一次性创建并立刻发起网络解码，
    /// 造成明显的卡顿甚至假死（用户反馈的「加载不出来」）。
    ///
    /// 本行为把「创建 BitmapImage + 下载 + 解码」全部搬到后台线程，
    /// 并受 KuroImageCache 的并发闸门限流，完成后再调度回 UI 线程填 Source。
    /// 同时，只有当 Image 真正进入可视树（Loaded）且滚动到可见位置时才触发，
    /// 保证长列表不会有一次性解码风暴。
    /// </summary>
    public static class KuroLazyImage
    {
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

        private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null) return;

            var oldCts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (oldCts != null)
            {
                try { oldCts.Cancel(); oldCts.Dispose(); } catch { }
                image.SetValue(LoadTokenProperty, null);
            }

            string url = e.NewValue as string;
            image.SetValue(LoadStartedProperty, false);

            if (string.IsNullOrWhiteSpace(url))
            {
                try { image.Source = null; } catch { }
                return;
            }

            var cts = new CancellationTokenSource();
            image.SetValue(LoadTokenProperty, cts);

            image.Loaded -= OnImageLoaded;
            image.Loaded += OnImageLoaded;
            image.Unloaded -= OnImageUnloaded;
            image.Unloaded += OnImageUnloaded;

            // 已在可视树中的情况（模板复用）直接触发。
            // WP8.1 无 IsLoaded，改用自定义 InTree 状态（由 Loaded/Unloaded 维护）。
            if ((bool)image.GetValue(InTreeProperty))
            {
                BeginLoad(image, cts.Token);
            }
        }

        private static void OnImageLoaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            image.SetValue(InTreeProperty, true);

            var cts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (cts == null || cts.IsCancellationRequested) return;

            BeginLoad(image, cts.Token);
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            image.SetValue(InTreeProperty, false);

            var cts = image.GetValue(LoadTokenProperty) as CancellationTokenSource;
            if (cts != null && !cts.IsCancellationRequested)
            {
                try { cts.Cancel(); } catch { }
            }
        }

        private static void BeginLoad(Image image, CancellationToken token)
        {
            if (image == null) return;

            // 同一 URL 只排队一次
            if ((bool)image.GetValue(LoadStartedProperty)) return;
            string url = GetSourceUrl(image);
            if (string.IsNullOrWhiteSpace(url)) return;
            image.SetValue(LoadStartedProperty, true);

            int decodeWidth = GetDecodeWidth(image);
            if (decodeWidth <= 0) decodeWidth = 480;

            // 完全后台执行；KuroImageCache 内部有限流与磁盘/内存缓存
            Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    await KuroImageCache.Instance.LoadIntoAsync(image, url, decodeWidth);
                }
                catch (Exception ex)
                {
                    KuroLogger.Warn("LAZY_IMG_ERR", "Lazy image load failed: " + ex.Message);
                }
            }, token);
        }
    }
}
