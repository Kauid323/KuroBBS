using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using KuroBBS.Services;

namespace KuroBBS.Helpers
{
    public static class KuroGifHelper
    {
        private class GifFrameInfo
        {
            public byte[] Pixels;
            public int DelayMs;
        }

        private class GifPlaybackState
        {
            public string Url;
            public int MaxDecodeWidth;
            public CancellationTokenSource Cts;
            public bool IsActive;
        }

        public static readonly DependencyProperty GifSourceProperty =
            DependencyProperty.RegisterAttached(
                "GifSource",
                typeof(string),
                typeof(KuroGifHelper),
                new PropertyMetadata(null, OnGifSourceChanged));

        public static string GetGifSource(DependencyObject obj)
        {
            return (string)obj.GetValue(GifSourceProperty);
        }

        public static void SetGifSource(DependencyObject obj, string value)
        {
            obj.SetValue(GifSourceProperty, value);
        }

        public static readonly DependencyProperty MaxDecodeWidthProperty =
            DependencyProperty.RegisterAttached(
                "MaxDecodeWidth",
                typeof(int),
                typeof(KuroGifHelper),
                new PropertyMetadata(480));

        public static int GetMaxDecodeWidth(DependencyObject obj)
        {
            return (int)obj.GetValue(MaxDecodeWidthProperty);
        }

        public static void SetMaxDecodeWidth(DependencyObject obj, int value)
        {
            obj.SetValue(MaxDecodeWidthProperty, value);
        }

        private static readonly DependencyProperty PlaybackStateProperty =
            DependencyProperty.RegisterAttached(
                "PlaybackState",
                typeof(GifPlaybackState),
                typeof(KuroGifHelper),
                new PropertyMetadata(null));

        private static void OnGifSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var image = d as Image;
            if (image == null) return;

            string url = e.NewValue as string;

            StopPlayback(image);

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            // 性能关键：非 GIF 图片完全不需要走「下载全部字节 → 解析帧」这条重路径。
            // 画册类条目（图鉴 > 画册）动辄 40+ 张全宽 PNG，若每张都后台下载整张图只为
            // 读一个魔数，会同时打爆网络与内存，并拖垮 UI 线程。这里按扩展名直接短路。
            if (!LooksLikeGifUrl(url))
            {
                return;
            }

            int maxWidth = GetMaxDecodeWidth(image);
            if (maxWidth <= 0) maxWidth = 480;

            var state = new GifPlaybackState
            {
                Url = url,
                MaxDecodeWidth = maxWidth,
                Cts = new CancellationTokenSource(),
                IsActive = true
            };
            image.SetValue(PlaybackStateProperty, state);

            image.Unloaded -= OnImageUnloaded;
            image.Unloaded += OnImageUnloaded;
            image.Loaded -= OnImageLoaded;
            image.Loaded += OnImageLoaded;

            StartPlayback(image, state);
        }

        /// <summary>
        /// 仅当 URL 明确指向 .gif 时才认为是动图。CDN 有时会把扩展名放在 query 里，
        /// 因此同时检查 path 与 query 段；其它一律返回 false（当作静态图处理）。
        /// </summary>
        private static bool LooksLikeGifUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            try
            {
                int q = url.IndexOf('?');
                string path = q >= 0 ? url.Substring(0, q) : url;
                if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) return true;

                // 少数 CDN 形如 .../xxx?format=gif 或 ?type=image/gif
                if (q >= 0)
                {
                    string query = url.Substring(q + 1);
                    if (query.IndexOf("image/gif", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if (Regex.IsMatch(query, @"(?:^|&)(?:format|type|ext)=gif(?:&|$)", RegexOptions.IgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            var state = image.GetValue(PlaybackStateProperty) as GifPlaybackState;
            if (state != null && state.Cts != null)
            {
                try
                {
                    state.Cts.Cancel();
                    state.Cts.Dispose();
                    state.Cts = null;
                }
                catch { }
                state.IsActive = false;
            }
        }

        private static void OnImageLoaded(object sender, RoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            var state = image.GetValue(PlaybackStateProperty) as GifPlaybackState;
            if (state != null && !state.IsActive && !string.IsNullOrEmpty(state.Url))
            {
                state.Cts = new CancellationTokenSource();
                state.IsActive = true;
                StartPlayback(image, state);
            }
        }

        private static void StopPlayback(Image image)
        {
            if (image == null) return;
            var state = image.GetValue(PlaybackStateProperty) as GifPlaybackState;
            if (state != null)
            {
                if (state.Cts != null)
                {
                    try
                    {
                        state.Cts.Cancel();
                        state.Cts.Dispose();
                    }
                    catch { }
                    state.Cts = null;
                }
                state.IsActive = false;
                image.SetValue(PlaybackStateProperty, null);
            }
        }

        private static void StartPlayback(Image image, GifPlaybackState state)
        {
            if (image == null || state == null || string.IsNullOrEmpty(state.Url)) return;

            // Execute in background
            var token = state.Cts.Token;
            string url = state.Url;
            int maxDecodeWidth = state.MaxDecodeWidth;

            // 【关键】StartPlayback 一定运行在 UI 线程（属性变更回调 / Loaded 事件），
            // 必须在进入后台任务之前把 Dispatcher 取出来带进去。
            // 否则后台线程里访问 image.Dispatcher 会抛 RPC_E_WRONG_THREAD (0x8001010E)。
            var dispatcher = image.Dispatcher;

            Task.Run(async () =>
            {
                try
                {
                    await PlayGifInternalAsync(dispatcher, image, url, maxDecodeWidth, token);
                }
                catch (Exception ex)
                {
                    KuroLogger.Warn("GIF_START_ERR", "Failed starting gif playback: " + ex.Message);
                }
            });
        }

        public static async Task TryPlayGifAsync(Image targetImage, string url, CancellationToken cancelToken, int maxDecodeWidth = 480)
        {
            if (targetImage == null) return;
            // 属性回调（很可能在 UI 线程）上先取 dispatcher，再传入后台逻辑
            var dispatcher = targetImage.Dispatcher;
            await PlayGifInternalAsync(dispatcher, targetImage, url, maxDecodeWidth, cancelToken);
        }

        private static async Task PlayGifInternalAsync(CoreDispatcher dispatcher, Image targetImage, string url, int maxDecodeWidth, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (dispatcher == null) return;

            try
            {
                byte[] data = await KuroImageCache.Instance.GetImageBytesAsync(url);
                if (data == null || data.Length < 10) return;
                if (cancelToken.IsCancellationRequested) return;

                // Validate GIF magic bytes ('GIF87a' or 'GIF89a')
                if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
                {
                    using (var ms = new InMemoryRandomAccessStream())
                    {
                        var writer = new DataWriter(ms);
                        writer.WriteBytes(data);
                        await writer.StoreAsync();
                        writer.DetachStream(); // DetachStream prevents DataWriter from closing the underlying stream on disposal
                        writer = null;
                        ms.Seek(0);

                        var decoder = await BitmapDecoder.CreateAsync(ms);
                        uint frameCount = decoder.FrameCount;
                        if (frameCount <= 1) return; // Single frame, static image is enough

                        uint srcW = decoder.OrientedPixelWidth;
                        uint srcH = decoder.OrientedPixelHeight;
                        if (srcW == 0 || srcH == 0) return;

                        // Calculate scale down factor to prevent GPU / RAM exhaustion on WP8.1
                        var transform = new BitmapTransform();
                        uint targetW = srcW;
                        uint targetH = srcH;
                        if (maxDecodeWidth > 0 && srcW > (uint)maxDecodeWidth)
                        {
                            double scale = (double)maxDecodeWidth / srcW;
                            targetW = (uint)Math.Max(1, (int)(srcW * scale));
                            targetH = (uint)Math.Max(1, (int)(srcH * scale));
                            transform.ScaledWidth = targetW;
                            transform.ScaledHeight = targetH;
                        }

                        // Sampling step if GIF is very long (cap max loaded frames to 45 to protect WP8.1 512MB/1GB RAM)
                        uint frameStep = 1;
                        if (frameCount > 45)
                        {
                            frameStep = (uint)Math.Ceiling((double)frameCount / 45.0);
                        }

                        var frames = new List<GifFrameInfo>();

                        for (uint i = 0; i < frameCount; i += frameStep)
                        {
                            if (cancelToken.IsCancellationRequested) return;
                            var frame = await decoder.GetFrameAsync(i);

                            int delayMs = 100;
                            try
                            {
                                var props = await frame.BitmapProperties.GetPropertiesAsync(new[] { "/grctlext/Delay" });
                                if (props.ContainsKey("/grctlext/Delay"))
                                {
                                    var val = props["/grctlext/Delay"].Value;
                                    if (val != null)
                                    {
                                        int d = Convert.ToInt32(val);
                                        if (d > 0) delayMs = d * 10;
                                    }
                                }
                            }
                            catch { }
                            if (delayMs < 20) delayMs = 100;
                            if (frameStep > 1) delayMs = (int)(delayMs * frameStep);

                            var pixelData = await frame.GetPixelDataAsync(
                                BitmapPixelFormat.Bgra8,
                                BitmapAlphaMode.Premultiplied,
                                transform,
                                ExifOrientationMode.RespectExifOrientation,
                                ColorManagementMode.ColorManageToSRgb);

                            frames.Add(new GifFrameInfo
                            {
                                Pixels = pixelData.DetachPixelData(),
                                DelayMs = delayMs
                            });
                        }

                        if (frames.Count <= 1 || cancelToken.IsCancellationRequested) return;

                        // Switch image source to WriteableBitmap on UI thread
                        WriteableBitmap wb = null;
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            if (cancelToken.IsCancellationRequested) return;
                            wb = new WriteableBitmap((int)targetW, (int)targetH);
                            if (targetImage != null) targetImage.Source = wb;
                        });

                        if (wb == null || cancelToken.IsCancellationRequested) return;

                        // Animation loop — pass cancelToken to Task.Delay for immediate exit
                        int currentFrame = 0;
                        while (!cancelToken.IsCancellationRequested)
                        {
                            var f = frames[currentFrame];
                            var fPixels = f.Pixels;

                            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                if (cancelToken.IsCancellationRequested) return;
                                try
                                {
                                    using (var pixelStream = wb.PixelBuffer.AsStream())
                                    {
                                        pixelStream.Seek(0, SeekOrigin.Begin);
                                        pixelStream.Write(fPixels, 0, fPixels.Length);
                                    }
                                    wb.Invalidate();
                                }
                                catch (ObjectDisposedException) { return; }
                                catch { }
                            });

                            if (cancelToken.IsCancellationRequested) break;

                            try
                            {
                                await Task.Delay(f.DelayMs, cancelToken);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            currentFrame = (currentFrame + 1) % frames.Count;
                        }

                        // Clean up: release WriteableBitmap from Image.Source after loop exits
                        try
                        {
                            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                try { if (targetImage != null) targetImage.Source = null; } catch { }
                            });
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("GIF_PLAY_ERR", "Failed decoding/playing GIF: " + ex.Message);
            }
        }
    }
}
