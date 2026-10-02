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
using Windows.UI.Xaml.Controls;
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
        private readonly LinkedList<string> _lruKeys;

        /// <summary>
        /// 内存缓存**条目数**上限（次要闸门，兜底防「几万张缩略图对象」）。
        /// 主闸门是 <see cref="MaxMemoryCacheBytes"/>。
        /// </summary>
        private const int MaxMemoryCacheItems = 256;

        /// <summary>
        /// 内存缓存**解码像素字节数**上限（主闸门）。
        ///
        /// 【为什么按字节而不是按条数】每条都是「已解码的位图」，尺寸差异极大：
        /// 列表缩略图 150px ≈ 150×112×4 ≈ 66KB；图鉴长图 480×3000 ≈ 5.5MB —— 相差 80 倍。
        /// 旧实现按「条数 = 8」限：8 张缩略图才 0.5MB，却已经把缓存挤爆 →
        /// 滚动时反复 miss、反复重新解码（用户实测：滑动图片总是重新加载）；
        /// 而 8 张长图又要 44MB，照样可能 OOM。**按字节限才能「小图多留、大图早走」**。
        ///
        /// 【为什么不再「滚出视口就删缓存」】旧实现滚出视口就 EvictMemoryCache，
        /// 导致滚回来必然 miss → 重新读盘 + 重新解码。现在离屏只**摘下 Image.Source 引用**，
        /// 解码结果留在本缓存里，滚回来直接命中、瞬时显示；内存安全完全由这里的字节预算保证。
        ///
        /// 20MB 的选取：旧实现最坏 8×5.5MB ≈ 44MB，新实现**更小**，所以不会更危险。
        /// </summary>
        private const long MaxMemoryCacheBytes = 20L * 1024 * 1024;

        /// <summary>cacheKey -> 该条目解码后占用的估算字节数。</summary>
        private readonly Dictionary<string, long> _entryBytes;
        /// <summary>当前内存缓存占用的估算总字节数（= _entryBytes 各项之和）。</summary>
        private long _memoryCacheBytes;

        /// <summary>
        /// cacheKey -> **实测**解码字节数。一经测得就长期记住，**不随缓存淘汰而遗忘**。
        ///
        /// 作用：防止「抖动」—— 某图被淘汰后，估算又变回乐观值 → 被判定为便宜 → 重新解码
        /// → 实测很大 → 再淘汰 …… 循环。记住实测值后，该图的成本是稳定的，
        /// 判定结果不会再反复横跳。
        /// </summary>
        private readonly Dictionary<string, long> _knownBytes;
        /// <summary>_knownBytes 条数上限，超出时清理「已不在缓存中」的项。</summary>
        private const int MaxKnownSizeEntries = 2048;

        private readonly HashSet<string> _diskFileCache;
        private readonly HashSet<string> _inFlightUrls;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _downloadThrottle = new SemaphoreSlim(4, 4);
        private readonly TaskCompletionSource<bool> _initTcs = new TaskCompletionSource<bool>();
        private int _initStarted = 0;
        private StorageFolder _cacheFolder;
        private HttpClient _httpClient;

        /// <summary>
        /// 同时进行「下载 + 解码」的并发上限。画册类条目一屏可能有 40+ 张全宽大图，
        /// 若全部并发下载/解码会瞬间占满 UI 线程与内存，故串行化到小窗口逐步回填。
        /// </summary>
        private readonly SemaphoreSlim _decodeThrottle = new SemaphoreSlim(MaxConcurrentDecodes, MaxConcurrentDecodes);

        /// <summary>单次解码并发上限常量。</summary>
        private const int MaxConcurrentDecodes = 3;

        public KuroImageCache()
        {
            _memoryCache = new Dictionary<string, BitmapImage>();
            _lruKeys = new LinkedList<string>();
            _entryBytes = new Dictionary<string, long>(StringComparer.Ordinal);
            _knownBytes = new Dictionary<string, long>(StringComparer.Ordinal);
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

        public BitmapImage GetImageSource(string url, int decodeWidth = 360, int decodeHeight = 0)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string cacheKey = string.Format("{0}_w{1}_h{2}", url, decodeWidth, decodeHeight);
            lock (_lock)
            {
                if (_memoryCache.ContainsKey(cacheKey))
                {
                    // Move to front of LRU
                    _lruKeys.Remove(cacheKey);
                    _lruKeys.AddFirst(cacheKey);
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
                bitmap.DecodePixelHeight = Math.Min(1080, decodeHeight);
            }
            else if (decodeWidth > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Physical;
                bitmap.DecodePixelWidth = Math.Min(1080, decodeWidth);
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

                PutInMemoryCache(cacheKey, bitmap, decodeWidth, decodeHeight);
                return bitmap;
            }

            // Not yet on disk: point directly to online URL so XAML renders smoothly immediately
            try
            {
                bitmap.UriSource = new Uri(url);
            }
            catch { }

            PutInMemoryCache(cacheKey, bitmap, decodeWidth, decodeHeight);

            QueueBackgroundDownload(url, fileName);
            return bitmap;
        }

        /// <summary>
        /// 在后台线程下载并解码图片，完成后在 UI 线程把 Source 回填到 image.Source。
        /// 这是画册 / 图集类页面的推荐用法：调用方只负责 create 一个空 Image，
        /// 解码与网络全部脱离 UI 线程，且受 _decodeThrottle 限流。
        /// </summary>
        /// <param name="image">目标 Image（可为 null，此时只做预热缓存）</param>
        /// <param name="url">图片地址</param>
        /// <param name="decodeWidth">解码宽度上限</param>
        public async Task LoadIntoAsync(Image image, string url, int decodeWidth = 360, int decodeHeight = 0)
        {
            await LoadIntoAsync(image, image != null ? image.Dispatcher : null, url, decodeWidth, decodeHeight);
        }

        /// <summary>
        /// 重载：由调用方**在 UI 线程上预先捕获** dispatcher 后传入。
        /// 当调用点本身就在后台线程（如 Task.Run 里）时，必须用这个重载，
        /// 否则内部读取 image.Dispatcher 仍会 RPC_E_WRONG_THREAD。
        /// </summary>
        public async Task LoadIntoAsync(Image image, CoreDispatcher dispatcher, string url, int decodeWidth = 360, int decodeHeight = 0)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // 【关键】dispatcher 由调用方在 UI 线程上取出后传入。
            // 之前是在后台线程里访问 image.Dispatcher，属于跨线程触碰 UI 对象，
            // 会抛 RPC_E_WRONG_THREAD (0x8001010E) —— 用户日志里的 LAZY_IMG_ERR 就是它。
            // 拿到之后，后续所有 await 都只用这个局部变量，绝不再碰 image 的 UI 成员。

            string cacheKey = string.Format("{0}_w{1}_h{2}", url, decodeWidth, decodeHeight);

            // 1. 命中内存缓存：直接回填（仍在 UI 线程，但无 IO）
            BitmapImage cached;
            lock (_lock)
            {
                if (_memoryCache.TryGetValue(cacheKey, out cached))
                {
                    _lruKeys.Remove(cacheKey);
                    _lruKeys.AddFirst(cacheKey);
                }
            }
            if (cached != null)
            {
                await SetSourceOnUiThreadAsync(image, dispatcher, cached);
                return;
            }

            // 2. 磁盘缓存命中：从本地文件解码，快且不耗流量
            string fileName = ComputeHash(url) + GetExtension(url);
            bool isCachedOnDisk;
            lock (_lock) { isCachedOnDisk = _diskFileCache.Contains(fileName); }

            if (isCachedOnDisk)
            {
                // 【关键】BitmapImage 是 DependencyObject，只能在 UI 线程创建/配置。
                // 之前在这里直接 new BitmapImage() 会因为当前在后台线程而抛 RPC_E_WRONG_THREAD。
                await SetUriSourceOnUiThreadAsync(
                    image, dispatcher, "ms-appdata:///local/ImageCache/" + fileName,
                    decodeWidth, decodeHeight, cacheKey, url);
                return;
            }

            // 3. 网络：进入限流队列，后台下载，完成后回填
            await _initTcs.Task;
            if (_cacheFolder == null)
            {
                // 缓存目录不可用时降级：直接挂远程 URI，保证图片仍可见
                await SetUriSourceOnUiThreadAsync(
                    image, dispatcher, url, decodeWidth, decodeHeight, cacheKey, url);
                return;
            }

            await _decodeThrottle.WaitAsync().ConfigureAwait(false);
            try
            {
                // 限流等待期间可能已有同图完成，二次检查避免重复下载
                lock (_lock)
                {
                    if (_memoryCache.TryGetValue(cacheKey, out cached)) { }
                }
                if (cached != null)
                {
                    await SetSourceOnUiThreadAsync(image, dispatcher, cached);
                    return;
                }

                var response = await _httpClient.GetAsync(new Uri(url));
                if (!response.IsSuccessStatusCode) return;

                var buffer = await response.Content.ReadAsBufferAsync();

                string localUri = null;
                try
                {
                    var file = await _cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBufferAsync(file, buffer);
                    lock (_lock) { _diskFileCache.Add(fileName); }
                    localUri = "ms-appdata:///local/ImageCache/" + fileName;
                }
                catch { }

                // 后台只做下载/写盘；BitmapImage 的创建与绑定一律回 UI 线程完成
                await SetUriSourceOnUiThreadAsync(
                    image, dispatcher, localUri != null ? localUri : url,
                    decodeWidth, decodeHeight, cacheKey, url);
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("IMG_LAZY_ERR", "Lazy image load failed: " + url + " -> " + ex.Message);
            }
            finally
            {
                _decodeThrottle.Release();
            }
        }

        /// <summary>
        /// 在 UI 线程创建 BitmapImage、设置解码尺寸与 UriSource，并回填到 image.Source。
        /// </summary>
        /// <remarks>
        /// **BitmapImage 的创建与配置必须在 UI 线程执行**（它是 DependencyObject）：
        /// 在后台线程 new / 设 DecodePixel* / 设 UriSource 都会抛
        /// RPC_E_WRONG_THREAD (0x8001010E)——这正是之前 LAZY_IMG_ERR 的根因。
        /// WinRT 的 BitmapImage 在 UriSource 赋值后本身异步解码，
        /// 因此不需要（也不能）把「创建 BitmapImage」挪到后台线程。
        /// 本方法内部把「创建 bitmap」整个放进 UI 线程回调里完成。
        /// </remarks>
        private async Task SetUriSourceOnUiThreadAsync(
            Image image, CoreDispatcher dispatcher, string uriSource,
            int decodeWidth, int decodeHeight, string cacheKey, string originalUrl)
        {
            if (string.IsNullOrWhiteSpace(uriSource)) return;

            // 创建 bitmap 的动作整体封成闭包，只在 UI 线程执行。
            // 注意：RunAsync 需要 DispatchedHandler，所以这里用方法组/局部函数形式而非 Action。
            DispatchedHandler apply = () =>
            {
                BitmapImage bitmap;
                try
                {
                    bitmap = CreateEmptyBitmap(decodeWidth, decodeHeight);
                    try { bitmap.UriSource = new Uri(uriSource); }
                    catch
                    {
                        try { bitmap.UriSource = new Uri(originalUrl); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    KuroLogger.Warn("IMG_URI_ERR", "Create bitmap failed: " + ex.Message);
                    return;
                }

                PutInMemoryCache(cacheKey, bitmap, decodeWidth, decodeHeight);

                if (image == null) return;
                try { image.Source = bitmap; } catch { }
            };

            if (dispatcher == null)
            {
                // 没有 dispatcher：只预热缓存，不碰 UI 对象
                apply();
                return;
            }

            if (dispatcher.HasThreadAccess)
            {
                apply();
                return;
            }
            await dispatcher.RunAsync(CoreDispatcherPriority.Low, apply);
        }

        private static BitmapImage CreateEmptyBitmap(int decodeWidth, int decodeHeight)
        {
            var bitmap = new BitmapImage();
            if (decodeHeight > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Physical;
                bitmap.DecodePixelHeight = Math.Min(1080, decodeHeight);
            }
            else if (decodeWidth > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Physical;
                bitmap.DecodePixelWidth = Math.Min(1080, decodeWidth);
            }
            return bitmap;
        }

        /// <summary>
        /// 在 UI 线程把 Source 回填到 image。
        /// **dispatcher 必须由调用方在 UI 线程上预先取出**（见 LoadIntoAsync 开头）——
        /// 绝不能在后台线程里访问 image.Dispatcher，否则 RPC_E_WRONG_THREAD。
        /// </summary>
        private static async Task SetSourceOnUiThreadAsync(Image image, CoreDispatcher dispatcher, BitmapImage bitmap)
        {
            if (image == null || bitmap == null) return;
            if (dispatcher == null) return;

            if (dispatcher.HasThreadAccess)
            {
                try { image.Source = bitmap; } catch { }
                return;
            }
            await dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try { image.Source = bitmap; } catch { }
            });
        }

        private void PutInMemoryCache(string key, BitmapImage bitmap, int decodeWidth, int decodeHeight)
        {
            if (bitmap == null) return;

            bool isNew;
            lock (_lock)
            {
                isNew = !_memoryCache.ContainsKey(key);
                if (!isNew) _lruKeys.Remove(key);

                _memoryCache[key] = bitmap;
                _lruKeys.AddFirst(key);
                SetEntryBytesLocked(key, EstimateBytes(decodeWidth, decodeHeight));
                TrimMemoryCacheLocked();
            }

            // 解码完成后能拿到**真实**像素尺寸，用它替换粗略估算（长图/宽图差别极大）。
            if (isNew) HookBitmapSize(key, bitmap);
        }

        /// <summary>
        /// 粗略估算一张图解码后占用的字节数。
        /// 只知道宽度时（decodeHeight == 0，懒加载恒为 0）按 1:1.5 竖幅估一个高度 ——
        /// 宁可高估：高估只会让**远端**的图更早被回收，视口内的图永远优先保留。
        /// 真实值会在 ImageOpened 后修正。
        /// </summary>
        private static long EstimateBytes(int decodeWidth, int decodeHeight)
        {
            int w = decodeWidth > 0 ? Math.Min(1080, decodeWidth) : 480;
            int h = decodeHeight > 0 ? Math.Min(1080, decodeHeight) : (int)(w * 1.5);
            if (h < 1) h = 1;
            return (long)w * h * 4L;
        }

        /// <summary>
        /// 供 KuroLazyImage 做「同时持有位图」的字节预算判断。
        /// 命中缓存返回**真实**估算值；否则返回按解码尺寸的粗略值。
        /// </summary>
        public long GetEstimatedBytes(string url, int decodeWidth, int decodeHeight = 0)
        {
            if (string.IsNullOrWhiteSpace(url)) return 0;

            string cacheKey = string.Format("{0}_w{1}_h{2}", url, decodeWidth, decodeHeight);
            lock (_lock)
            {
                long bytes;
                if (_entryBytes.TryGetValue(cacheKey, out bytes) && bytes > 0) return bytes;
                // 缓存里没有，但曾经实测过 → 用实测值（避免估算来回横跳导致抖动）
                if (_knownBytes.TryGetValue(cacheKey, out bytes) && bytes > 0) return bytes;
            }
            return EstimateBytes(decodeWidth, decodeHeight);
        }

        /// <summary>
        /// 位图解码完成 / 失败时修正条目的真实字节占用。
        /// 没有这一步，长图（480×3000 ≈ 5.5MB）会被按 480×720 ≈ 1.4MB 低估 4 倍，
        /// 字节预算就形同虚设。
        /// </summary>
        private void HookBitmapSize(string key, BitmapImage bitmap)
        {
            try
            {
                bitmap.ImageOpened += (sender, args) => RefineEntryBytes(key, bitmap);
                bitmap.ImageFailed += (sender, args) =>
                {
                    lock (_lock) { RemoveEntryLocked(key); }
                };

                // 极端情况（本地磁盘缓存、瞬时解码）：事件可能在订阅前就已触发，补一次。
                RefineEntryBytes(key, bitmap);
            }
            catch { }
        }

        private void RefineEntryBytes(string key, BitmapImage bitmap)
        {
            try
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;
                if (w <= 0 || h <= 0) return;

                long bytes = (long)w * h * 4L;
                lock (_lock)
                {
                    RememberRealBytesLocked(key, bytes);
                    if (_memoryCache.ContainsKey(key))
                    {
                        SetEntryBytesLocked(key, bytes);
                        TrimMemoryCacheLocked();
                    }
                }
            }
            catch { }
        }

        /// <summary>记住实测字节数（即使该图已被缓存淘汰也不遗忘），并做条数上限清理。</summary>
        private void RememberRealBytesLocked(string key, long bytes)
        {
            _knownBytes[key] = bytes;
            if (_knownBytes.Count <= MaxKnownSizeEntries) return;

            // 超限：优先丢掉「已不在缓存里」的记录（它们只用于估算，丢了顶多退回粗略值）
            var drop = new List<string>();
            foreach (var kv in _knownBytes)
            {
                if (!_memoryCache.ContainsKey(kv.Key)) drop.Add(kv.Key);
            }
            for (int i = 0; i < drop.Count; i++) _knownBytes.Remove(drop[i]);
        }

        /// <summary>更新某条目的字节占用（增量维护 _memoryCacheBytes，避免每次全量求和）。</summary>
        private void SetEntryBytesLocked(string key, long bytes)
        {
            long old;
            if (_entryBytes.TryGetValue(key, out old)) _memoryCacheBytes -= old;
            _entryBytes[key] = bytes;
            _memoryCacheBytes += bytes;
        }

        /// <summary>摘除一个缓存条目（同时维护 LRU 链与字节计数）。</summary>
        private void RemoveEntryLocked(string key)
        {
            if (_memoryCache.Remove(key))
            {
                _lruKeys.Remove(key);
                long b;
                if (_entryBytes.TryGetValue(key, out b)) _memoryCacheBytes -= b;
                _entryBytes.Remove(key);
            }
        }

        /// <summary>
        /// 按字节预算从 LRU 尾部淘汰。
        /// 保留至少 1 条（单张超大图也不能把缓存清空到「刚插入就没了」）。
        /// </summary>
        private void TrimMemoryCacheLocked()
        {
            while ((_memoryCacheBytes > MaxMemoryCacheBytes || _memoryCache.Count > MaxMemoryCacheItems)
                   && _memoryCache.Count > 1 && _lruKeys.Count > 0)
            {
                RemoveEntryLocked(_lruKeys.Last.Value);
            }
        }

        /// <summary>
        /// 从内存缓存里**彻底摘除**一张已解码位图，让 GC 能回收它占用的像素内存。
        ///
        /// 注意：**常规滚动/导航离屏不再调用本方法**（那会造成「滚回来就重新加载」）。
        /// 现在只有需要主动放弃某张图时才用；日常回收由 <see cref="TrimMemoryCacheLocked"/>
        /// 的字节预算自动完成。
        /// </summary>
        public void EvictMemoryCache(string url, int decodeWidth, int decodeHeight = 0)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            string cacheKey = string.Format("{0}_w{1}_h{2}", url, decodeWidth, decodeHeight);
            lock (_lock) { RemoveEntryLocked(cacheKey); }
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
                    _lruKeys.Clear();
                    _entryBytes.Clear();
                    _knownBytes.Clear();
                    _memoryCacheBytes = 0;
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
