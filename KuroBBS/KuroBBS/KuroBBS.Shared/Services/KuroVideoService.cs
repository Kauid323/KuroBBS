using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class VideoPlayInfoItem
    {
        public string Definition { get; set; }
        public string QualityName { get; set; }
        public string QualityDisplayName { get; set; }
        public string Format { get; set; }
        public string PlayUrl { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Bitrate { get; set; }
        public double Duration { get; set; }
        public long Size { get; set; }
    }

    public class VideoResolutionResult
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public string PlayAuth { get; set; }
        public double Duration { get; set; }
        public string BestPlayUrl { get; set; }
        public VideoPlayInfoItem BestQuality { get; set; }
        public List<VideoPlayInfoItem> PlayList { get; set; }
        public string PlayerHtml { get; set; }

        public VideoResolutionResult()
        {
            PlayList = new List<VideoPlayInfoItem>();
        }
    }

    public class KuroVideoService
    {
        private static KuroVideoService _instance;
        public static KuroVideoService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroVideoService();
                }
                return _instance;
            }
        }

        private static string PercentEncodeAliyun(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return Uri.EscapeDataString(value)
                .Replace("+", "%20")
                .Replace("*", "%2A")
                .Replace("%7E", "~");
        }

        private static string SafeGetString(JsonObject obj, string key, string defaultValue = "")
        {
            if (obj == null || !obj.ContainsKey(key)) return defaultValue;
            var val = obj.GetNamedValue(key);
            if (val.ValueType == JsonValueType.String)
            {
                return val.GetString();
            }
            if (val.ValueType == JsonValueType.Number)
            {
                return val.GetNumber().ToString(CultureInfo.InvariantCulture);
            }
            if (val.ValueType == JsonValueType.Boolean)
            {
                return val.GetBoolean().ToString();
            }
            return defaultValue;
        }

        private static double SafeGetNumber(JsonObject obj, string key, double defaultValue = 0)
        {
            if (obj == null || !obj.ContainsKey(key)) return defaultValue;
            var val = obj.GetNamedValue(key);
            if (val.ValueType == JsonValueType.Number)
            {
                return val.GetNumber();
            }
            if (val.ValueType == JsonValueType.String)
            {
                double d;
                if (double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                {
                    return d;
                }
            }
            return defaultValue;
        }

        private static void FillQualityNames(VideoPlayInfoItem item)
        {
            string defUpper = (item.Definition ?? "").ToUpperInvariant();
            switch (defUpper)
            {
                case "FD":
                    item.QualityName = "360P";
                    item.QualityDisplayName = "360P 流畅";
                    break;
                case "LD":
                    item.QualityName = "480P";
                    item.QualityDisplayName = "480P 标清";
                    break;
                case "SD":
                    item.QualityName = "720P";
                    item.QualityDisplayName = "720P 高清";
                    break;
                case "HD":
                    item.QualityName = "1080P";
                    item.QualityDisplayName = "1080P 超清";
                    break;
                case "OD":
                case "2K":
                    item.QualityName = "2K";
                    item.QualityDisplayName = "2K 原画";
                    break;
                case "4K":
                    item.QualityName = "4K";
                    item.QualityDisplayName = "4K 极清";
                    break;
                default:
                    if (item.Height > 0)
                    {
                        if (item.Height <= 360) { item.QualityName = "360P"; item.QualityDisplayName = "360P 流畅"; }
                        else if (item.Height <= 480) { item.QualityName = "480P"; item.QualityDisplayName = "480P 标清"; }
                        else if (item.Height <= 720) { item.QualityName = "720P"; item.QualityDisplayName = "720P 高清"; }
                        else { item.QualityName = "1080P"; item.QualityDisplayName = "1080P 超清"; }
                    }
                    else
                    {
                        item.QualityName = !string.IsNullOrEmpty(item.Definition) ? item.Definition : "自动";
                        item.QualityDisplayName = item.QualityName;
                    }
                    break;
            }
        }

        public async Task<VideoResolutionResult> ResolveVideoAsync(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;

            KuroLogger.Loading("VIDEO_RESOLVE", "Resolving videoId: " + videoId);

            try
            {
                // Step 1: Refresh Play Code from KuroBBS API
                var reqParameters = new Dictionary<string, string>
                {
                    { "videoId", videoId }
                };

                var customHeaders = new Dictionary<string, string>
                {
                    { "devCode", "qQEqfNyouMULztWVJcTjXxmZZ6kp85yv" },
                    { "source", "h5" },
                    { "version", "2.10.5" }
                };

                var json = await KuroApiClient.Instance.PostFormAsync("/forum/video/refreshPlayCode", reqParameters, customHeaders);
                if (json == null || !json.ContainsKey("code") || json.GetNamedNumber("code") != 200 || !json.ContainsKey("data"))
                {
                    KuroLogger.Warn("VIDEO_PLAYCODE_FAIL", "Failed to get playAuth for video: " + videoId);
                    return null;
                }

                var dataObj = json.GetNamedObject("data");
                if (!dataObj.ContainsKey("playAuth")) return null;

                string playAuthBase64 = SafeGetString(dataObj, "playAuth");
                if (string.IsNullOrEmpty(playAuthBase64)) return null;

                // Step 2: Base64 decode PlayAuth JSON
                var playAuthBuffer = CryptographicBuffer.DecodeFromBase64String(playAuthBase64);
                string playAuthJson = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, playAuthBuffer);
                
                JsonObject authObj;
                if (!JsonObject.TryParse(playAuthJson, out authObj))
                {
                    KuroLogger.Warn("VIDEO_AUTH_DECODE_ERR", "Could not parse decoded playAuth JSON");
                    return null;
                }

                string accessKeyId = SafeGetString(authObj, "AccessKeyId");
                string accessKeySecret = SafeGetString(authObj, "AccessKeySecret");
                string securityToken = SafeGetString(authObj, "SecurityToken");
                string region = SafeGetString(authObj, "Region", "cn-shanghai");

                string authInfo = "";
                if (authObj.ContainsKey("AuthInfo"))
                {
                    var aVal = authObj.GetNamedValue("AuthInfo");
                    if (aVal.ValueType == JsonValueType.String)
                    {
                        authInfo = aVal.GetString();
                    }
                    else if (aVal.ValueType == JsonValueType.Object)
                    {
                        authInfo = aVal.GetObject().Stringify();
                    }
                }

                var result = new VideoResolutionResult { VideoId = videoId };

                if (authObj.ContainsKey("VideoMeta") && authObj.GetNamedValue("VideoMeta").ValueType == JsonValueType.Object)
                {
                    var meta = authObj.GetNamedObject("VideoMeta");
                    result.Title = SafeGetString(meta, "Title");
                    result.CoverUrl = SafeGetString(meta, "CoverURL");
                    result.Duration = SafeGetNumber(meta, "Duration");
                }

                // Step 3: Build Aliyun GetPlayInfo query parameters
                var queryParams = new SortedDictionary<string, string>
                {
                    { "AccessKeyId", accessKeyId },
                    { "Action", "GetPlayInfo" },
                    { "AuthInfo", authInfo },
                    { "AuthTimeout", "7200" },
                    { "Channel", "HTML5" },
                    { "Definition", "FD,LD,SD,HD" },
                    { "Format", "JSON" },
                    { "Formats", "mp4,m3u8" },
                    { "PlayConfig", "{}" },
                    { "PlayerVersion", "2.29.2" },
                    { "Rand", Guid.NewGuid().ToString() },
                    { "ReAuthInfo", "{}" },
                    { "SecurityToken", securityToken },
                    { "SignatureMethod", "HMAC-SHA1" },
                    { "SignatureNonce", Guid.NewGuid().ToString() },
                    { "SignatureVersion", "1.0" },
                    { "StreamType", "video" },
                    { "Version", "2017-03-21" },
                    { "VideoId", videoId }
                };

                var canonicalSb = new StringBuilder();
                bool first = true;
                foreach (var kvp in queryParams)
                {
                    if (!first) canonicalSb.Append("&");
                    canonicalSb.Append(PercentEncodeAliyun(kvp.Key));
                    canonicalSb.Append("=");
                    canonicalSb.Append(PercentEncodeAliyun(kvp.Value));
                    first = false;
                }
                string canonicalQuery = canonicalSb.ToString();

                // Step 4: Calculate HMAC-SHA1 signature
                string stringToSign = "GET&" + PercentEncodeAliyun("/") + "&" + PercentEncodeAliyun(canonicalQuery);
                var macProvider = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha1);
                var keyMaterial = CryptographicBuffer.ConvertStringToBinary(accessKeySecret + "&", BinaryStringEncoding.Utf8);
                var cryptoKey = macProvider.CreateKey(keyMaterial);
                var dataBuffer = CryptographicBuffer.ConvertStringToBinary(stringToSign, BinaryStringEncoding.Utf8);
                var signatureBuffer = CryptographicEngine.Sign(cryptoKey, dataBuffer);
                string signature = CryptographicBuffer.EncodeToBase64String(signatureBuffer);

                string fullRequestUrl = string.Format("https://vod.{0}.aliyuncs.com?{1}&Signature={2}",
                    region, canonicalQuery, PercentEncodeAliyun(signature));

                // Step 5: Send request to Aliyun VOD
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                    httpClient.DefaultRequestHeaders.Add("Origin", "https://www.kurobbs.com");
                    httpClient.DefaultRequestHeaders.Add("Referer", "https://www.kurobbs.com/");

                    var response = await httpClient.GetAsync(fullRequestUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        KuroLogger.Warn("ALIYUN_VOD_HTTP_ERR", "Aliyun VOD HTTP error: " + response.StatusCode);
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    JsonObject vodObj;
                    if (!JsonObject.TryParse(responseBody, out vodObj))
                    {
                        KuroLogger.Warn("ALIYUN_VOD_JSON_ERR", "Aliyun VOD response JSON parse failed");
                        return null;
                    }

                    if (vodObj.ContainsKey("VideoBase") && vodObj.GetNamedValue("VideoBase").ValueType == JsonValueType.Object)
                    {
                        var vb = vodObj.GetNamedObject("VideoBase");
                        if (string.IsNullOrEmpty(result.Title)) result.Title = SafeGetString(vb, "Title");
                        if (string.IsNullOrEmpty(result.CoverUrl)) result.CoverUrl = SafeGetString(vb, "CoverURL");
                        if (result.Duration <= 0) result.Duration = SafeGetNumber(vb, "Duration");
                    }

                    if (vodObj.ContainsKey("PlayInfoList") && vodObj.GetNamedValue("PlayInfoList").ValueType == JsonValueType.Object)
                    {
                        var pil = vodObj.GetNamedObject("PlayInfoList");
                        if (pil.ContainsKey("PlayInfo") && pil.GetNamedValue("PlayInfo").ValueType == JsonValueType.Array)
                        {
                            var list = pil.GetNamedArray("PlayInfo");
                            foreach (var itemVal in list)
                            {
                                if (itemVal.ValueType != JsonValueType.Object) continue;
                                var pObj = itemVal.GetObject();

                                var playInfo = new VideoPlayInfoItem
                                {
                                    Format = SafeGetString(pObj, "Format"),
                                    Definition = SafeGetString(pObj, "Definition"),
                                    PlayUrl = SafeGetString(pObj, "PlayURL"),
                                    Width = (int)SafeGetNumber(pObj, "Width"),
                                    Height = (int)SafeGetNumber(pObj, "Height"),
                                    Bitrate = SafeGetNumber(pObj, "Bitrate"),
                                    Duration = SafeGetNumber(pObj, "Duration"),
                                    Size = (long)SafeGetNumber(pObj, "Size")
                                };

                                FillQualityNames(playInfo);

                                if (!string.IsNullOrEmpty(playInfo.PlayUrl))
                                {
                                    result.PlayList.Add(playInfo);
                                }
                            }
                        }
                    }

                    // Prefer MP4 streams for Windows Phone 8.1 native MediaElement support
                    var mp4Streams = result.PlayList.Where(x => string.Equals(x.Format, "mp4", StringComparison.OrdinalIgnoreCase)).ToList();
                    var candidateList = mp4Streams.Count > 0 ? mp4Streams : result.PlayList;

                    // Deduplicate and sort resolutions from 1080P -> 720P -> 480P -> 360P
                    result.PlayList = candidateList
                        .GroupBy(x => x.QualityName)
                        .Select(g => g.First())
                        .OrderByDescending(x => x.Height > 0 ? x.Height : (int)x.Bitrate)
                        .ToList();

                    // Pick default best play URL: favor 720P (SD) or 480P (LD) or best available
                    if (result.PlayList.Count > 0)
                    {
                        var preferred = result.PlayList.FirstOrDefault(x => x.Definition == "SD" || x.QualityName == "720P")
                            ?? result.PlayList.FirstOrDefault(x => x.Definition == "HD" || x.QualityName == "1080P")
                            ?? result.PlayList.FirstOrDefault(x => x.Definition == "LD" || x.QualityName == "480P")
                            ?? result.PlayList.FirstOrDefault(x => x.Definition == "FD" || x.QualityName == "360P")
                            ?? result.PlayList.First();

                        result.BestQuality = preferred;
                        result.BestPlayUrl = preferred.PlayUrl;
                        KuroLogger.Loading("VIDEO_RESOLVE_OK", string.Format("Resolved {0} video streams, selected [{1} ({2})]: {3}", 
                            result.PlayList.Count, preferred.QualityDisplayName, preferred.Format, result.BestPlayUrl));
                    }

                    result.PlayAuth = playAuthBase64;
                    result.PlayerHtml = GeneratePlayerHtml(videoId, playAuthBase64, result.CoverUrl);

                    return result;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("VIDEO_RESOLVE_EX", "Exception resolving video " + videoId + ": " + ex.Message);
                return null;
            }
        }

        public static string GeneratePlayerHtml(string videoId, string playAuth, string coverUrl = "")
        {
            string cleanCover = !string.IsNullOrEmpty(coverUrl) ? coverUrl : "";
            string html = @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
<link rel=""stylesheet"" href=""https://g.alicdn.com/de/prismplayer/2.9.23/skins/default/aliplayer-min.css"" />
<style>
* { margin:0; padding:0; box-sizing:border-box; background:transparent; }
html, body { width:100%; height:100%; overflow:hidden; background:#000; }
#player-con { width:100%; height:100%; }
.prism-player { width:100% !important; height:100% !important; background:#000 !important; }
.prism-player .prism-big-play-btn { left:50% !important; top:50% !important; margin-left:-32px !important; margin-top:-32px !important; }
</style>
<script src=""https://www.kurobbs.com/ali-video/aliplayer-min-2.29.2.js""></script>
<script src=""https://g.alicdn.com/apsara-media-box/imp-web-player/2.29.2/hls/aliplayer-hls2-min.js""></script>
</head>
<body>
<div id=""player-con""></div>
<script>
window.onload = function() {
  try {
    var player = new Aliplayer({
      id: 'player-con',
      vid: '" + videoId + @"',
      playauth: '" + playAuth + @"',
      cover: '" + cleanCover + @"',
      width: '100%',
      height: '100%',
      autoplay: false,
      isLive: false,
      rePlay: false,
      playsinline: true,
      preload: true,
      controlBarVisibility: 'hover',
      useH5Prism: true,
      qualitySort: 'asc',
      format: 'm3u8',
      mediaType: 'video'
    }, function(p) {
      if (window.external && window.external.notify) {
        window.external.notify('READY');
      }
    });
  } catch(e) {
    if (window.external && window.external.notify) {
      window.external.notify('ERROR:' + e.message);
    }
  }
};
</script>
</body>
</html>";
            return html;
        }
    }
}
