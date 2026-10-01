using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace KuroBBS.Services
{
    public class KuroImageCache
    {
        private static KuroImageCache _instance;
        public static KuroImageCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroImageCache();
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, BitmapImage> _memoryCache;
        private readonly HashSet<string> _diskFileCache;
        private readonly HashSet<string> _inFlightUrls;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _downloadThrottle = new SemaphoreSlim(4, 4);
        private readonly TaskCompletionSource<bool> _initTcs = new TaskCompletionSource<bool>();
        private int _initStarted = 0;
        private StorageFolder _cacheFolder;
        private HttpClient _httpClient;

        public KuroImageCache()
        {
            _memoryCache = new Dictionary<string, BitmapImage>();
            _diskFileCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _inFlightUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var filter = new HttpBaseProtocolFilter { AllowUI = false };
            _httpClient = new HttpClient(filter);

            // Start background initialization once
            InitializeAsync();
        }

        public Task InitializeAsync()
        {
            if (Interlocked.CompareExchange(ref _initStarted, 1, 0) == 0)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var localFolder = ApplicationData.Current.LocalFolder;
                        _cacheFolder = await localFolder.CreateFolderAsync("ImageCache", CreationCollisionOption.OpenIfExists);
                        var existingFiles = await _cacheFolder.GetFilesAsync();
                        lock (_lock)
                        {
                            _diskFileCache.Clear();
                            foreach (var f in existingFiles)
                            {
                                _diskFileCache.Add(f.Name);
                            }
                        }
                        KuroLogger.Info("CACHE_INIT", string.Format("Image cache initialized: {0} files on disk", _diskFileCache.Count));
                    }
                    catch (Exception ex)
                    {
                        KuroLogger.Error("CACHE_INIT_ERR", "ImageCache init failed: " + ex.Message, ex);
                    }
                    finally
                    {
                        _initTcs.TrySetResult(true);
                    }
                });
            }
            return _initTcs.Task;
        }

        public BitmapImage GetImageSource(string url, int decodeWidth = 720, int decodeHeight = 0)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string cacheKey = string.Format("{0}_w{1}_h{2}", url, decodeWidth, decodeHeight);
            lock (_lock)
            {
                if (_memoryCache.ContainsKey(cacheKey))
                {
                    return _memoryCache[cacheKey];
                }
            }

            string fileName = ComputeHash(url) + GetExtension(url);

            bool isCachedOnDisk = false;
            lock (_lock)
            {
                isCachedOnDisk = _diskFileCache.Contains(fileName);
            }

            var bitmap = new BitmapImage();
            if (decodeHeight > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Physical;
                bitmap.DecodePixelHeight = Math.Min(2048, decodeHeight);
            }
            else if (decodeWidth > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Physical;
                bitmap.DecodePixelWidth = Math.Min(2048, decodeWidth);
            }

            if (isCachedOnDisk)
            {
                try
                {
                    bitmap.UriSource = new Uri("ms-appdata:///local/ImageCache/" + fileName);
                }
                catch 
                {
                    try { bitmap.UriSource = new Uri(url); } catch { }
                }

                lock (_lock)
                {
                    _memoryCache[cacheKey] = bitmap;
                }
                return bitmap;
            }

            // Not yet on disk: point directly to online URL so XAML renders smoothly immediately
            try
            {
                bitmap.UriSource = new Uri(url);
            }
            catch { }

            lock (_lock)
            {
                _memoryCache[cacheKey] = bitmap;
            }

            QueueBackgroundDownload(url, fileName);
            return bitmap;
        }

        public async Task<byte[]> GetImageBytesAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            await _initTcs.Task;

            string fileName = ComputeHash(url) + GetExtension(url);
            try
            {
                if (_cacheFolder != null)
                {
                    try
                    {
                        var file = await _cacheFolder.GetFileAsync(fileName);
                        if (file != null)
                        {
                            var buffer = await FileIO.ReadBufferAsync(file);
                            return buffer.ToArray();
                        }
                    }
                    catch { }
                }

                var response = await _httpClient.GetAsync(new Uri(url));
                if (response.IsSuccessStatusCode)
                {
                    var buffer = await response.Content.ReadAsBufferAsync();
                    byte[] bytes = buffer.ToArray();
                    if (_cacheFolder != null)
                    {
                        try
                        {
                            var file = await _cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                            await FileIO.WriteBufferAsync(file, buffer);
                            lock (_lock)
                            {
                                _diskFileCache.Add(fileName);
                            }
                        }
                        catch { }
                    }
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("IMG_BYTES_ERR", "Failed getting image bytes: " + ex.Message);
            }
            return null;
        }

        private void QueueBackgroundDownload(string url, string fileName)
        {
            lock (_lock)
            {
                if (_inFlightUrls.Contains(url)) return;
                _inFlightUrls.Add(url);
            }

            Task.Run(async () =>
            {
                await _initTcs.Task;
                if (_cacheFolder == null) return;

                await _downloadThrottle.WaitAsync();
                try
                {
                    var uri = new Uri(url);
                    var response = await _httpClient.GetAsync(uri);
                    if (response.IsSuccessStatusCode)
                    {
                        var buffer = await response.Content.ReadAsBufferAsync();
                        var file = await _cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteBufferAsync(file, buffer);

                        lock (_lock)
                        {
                            _diskFileCache.Add(fileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    KuroLogger.Warn("CACHE_DL_ERR", "Failed caching image " + url + ": " + ex.Message);
                }
                finally
                {
                    lock (_lock)
                    {
                        _inFlightUrls.Remove(url);
                    }
                    _downloadThrottle.Release();
                }
            });
        }

        private string ComputeHash(string str)
        {
            try
            {
                var alg = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                var buff = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);
                var hashed = alg.HashData(buff);
                return CryptographicBuffer.EncodeToHexString(hashed);
            }
            catch
            {
                return Math.Abs(str.GetHashCode()).ToString("X");
            }
        }

        private string GetExtension(string url)
        {
            try
            {
                int queryIdx = url.IndexOf('?');
                string cleanUrl = queryIdx >= 0 ? url.Substring(0, queryIdx) : url;
                int dotIdx = cleanUrl.LastIndexOf('.');
                if (dotIdx >= 0 && dotIdx < cleanUrl.Length - 1)
                {
                    string ext = cleanUrl.Substring(dotIdx).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".gif")
                    {
                        return ext;
                    }
                }
            }
            catch { }
            return ".jpg";
        }

        public void Preload(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string fileName = ComputeHash(url) + GetExtension(url);
            QueueBackgroundDownload(url, fileName);
        }

        public void PreloadBatch(IEnumerable<string> urls)
        {
            if (urls == null) return;
            foreach (var u in urls)
            {
                Preload(u);
            }
        }

        public async Task<long> GetCacheSizeBytesAsync()
        {
            await _initTcs.Task;
            if (_cacheFolder == null) return 0;

            try
            {
                var files = await _cacheFolder.GetFilesAsync();
                long total = 0;
                foreach (var f in files)
                {
                    var props = await f.GetBasicPropertiesAsync();
                    total += (long)props.Size;
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> ClearAllCacheAsync()
        {
            await _initTcs.Task;
            try
            {
                lock (_lock)
                {
                    _memoryCache.Clear();
                    _diskFileCache.Clear();
                    _inFlightUrls.Clear();
                }

                if (_cacheFolder != null)
                {
                    var files = await _cacheFolder.GetFilesAsync();
                    foreach (var f in files)
                    {
                        try
                        {
                            await f.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                        catch { }
                    }
                }
                KuroLogger.Info("CACHE_CLEAR", "All cache cleared successfully");
                return true;
            }
            catch (Exception ex)
            {
                KuroLogger.Error("CACHE_CLEAR_ERR", "Failed clearing cache: " + ex.Message, ex);
                return false;
            }
        }
    }
}
