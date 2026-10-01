using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
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

        public static async Task TryPlayGifAsync(Image targetImage, string url, CancellationToken cancelToken)
        {
            if (targetImage == null || string.IsNullOrEmpty(url)) return;

            // Fast exit if not a GIF URL to avoid unnecessary I/O and RAM allocations
            if (!url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) && !url.Contains(".gif?") && !url.Contains(".gif&"))
            {
                return;
            }

            try
            {
                byte[] data = await KuroImageCache.Instance.GetImageBytesAsync(url);
                if (data == null || data.Length < 10) return;
                if (cancelToken.IsCancellationRequested) return;

                // Check GIF magic bytes ('GIF87a' or 'GIF89a')
                if (data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F')
                {
                    using (var ms = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(ms))
                        {
                            writer.WriteBytes(data);
                            await writer.StoreAsync();
                        }
                        ms.Seek(0);

                        var decoder = await BitmapDecoder.CreateAsync(ms);
                        uint frameCount = decoder.FrameCount;
                        if (frameCount <= 1) return; // Single frame, default BitmapImage handles it

                        uint width = decoder.OrientedPixelWidth;
                        uint height = decoder.OrientedPixelHeight;
                        if (width == 0 || height == 0) return;

                        var frames = new List<GifFrameInfo>();

                        for (uint i = 0; i < frameCount; i++)
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

                            var pixelData = await frame.GetPixelDataAsync(
                                BitmapPixelFormat.Bgra8,
                                BitmapAlphaMode.Premultiplied,
                                new BitmapTransform(),
                                ExifOrientationMode.RespectExifOrientation,
                                ColorManagementMode.ColorManageToSRgb);

                            frames.Add(new GifFrameInfo
                            {
                                Pixels = pixelData.DetachPixelData(),
                                DelayMs = delayMs
                            });
                        }

                        if (frames.Count <= 1 || cancelToken.IsCancellationRequested) return;

                        var wb = new WriteableBitmap((int)width, (int)height);
                        targetImage.Source = wb;

                        // Animation loop
                        int currentFrame = 0;
                        while (!cancelToken.IsCancellationRequested)
                        {
                            var f = frames[currentFrame];
                            using (var pixelStream = wb.PixelBuffer.AsStream())
                            {
                                pixelStream.Seek(0, SeekOrigin.Begin);
                                pixelStream.Write(f.Pixels, 0, f.Pixels.Length);
                            }
                            wb.Invalidate();

                            await Task.Delay(f.DelayMs);
                            currentFrame = (currentFrame + 1) % frames.Count;
                        }
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
