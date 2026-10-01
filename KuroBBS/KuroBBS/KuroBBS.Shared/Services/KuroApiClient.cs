using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroApiClient
    {
        private static readonly Uri BaseUri = new Uri("https://api.kurobbs.com");
        private readonly HttpClient _httpClient;
        private readonly HttpBaseProtocolFilter _filter;

        private bool _isGuestSessionInitialized = false;

        private static KuroApiClient _instance;
        public static KuroApiClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroApiClient();
                }
                return _instance;
            }
        }

        public KuroApiClient()
        {
            _filter = new HttpBaseProtocolFilter();
            _filter.AllowUI = false;

            _httpClient = new HttpClient(_filter);
            _httpClient.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("text/plain"));
            _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");
            
            KuroLogger.Loading("HTTP_INIT", "KuroApiClient WinRT HttpClient initialized with BaseUri: " + BaseUri);
        }

        private void ApplyHeaders(HttpRequestMessage request, Dictionary<string, string> customHeaders = null)
        {
            request.Headers.Add("source", "h5");
            request.Headers.Add("version", "3.3.2");
            request.Headers.Add("channelId", "10");
            request.Headers.Add("lang", "zh-CN");
            request.Headers.Add("countryCode", "CN");
            request.Headers.Add("devCode", SettingsHelper.DevCode);
            request.Headers.Add("distinct_id", SettingsHelper.DevCode);

            if (!string.IsNullOrEmpty(SettingsHelper.Token))
            {
                request.Headers.Add("token", SettingsHelper.Token);
            }

            if (customHeaders != null)
            {
                foreach (var kvp in customHeaders)
                {
                    if (request.Headers.ContainsKey(kvp.Key))
                    {
                        request.Headers.Remove(kvp.Key);
                    }
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }
        }

        public async Task EnsureGuestSessionAsync()
        {
            if (_isGuestSessionInitialized) return;

            KuroLogger.Loading("COOKIE_INIT", "Establishing visitor/guest session via /config/getGameConfig...");
            try
            {
                var uri = new Uri(BaseUri, "config/getGameConfig");
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                ApplyHeaders(request);
                request.Content = new HttpFormUrlEncodedContent(new Dictionary<string, string>());

                var response = await _httpClient.SendRequestAsync(request);
                response.EnsureSuccessStatusCode();

                var cookieCollection = _filter.CookieManager.GetCookies(BaseUri);
                var cookieLog = new StringBuilder();
                foreach (var cookie in cookieCollection)
                {
                    cookieLog.Append(cookie.Name).Append("=").Append(cookie.Value).Append("; ");
                }

                _isGuestSessionInitialized = true;
                KuroLogger.Info("COOKIE_READY", "Visitor guest session active. Cookies: " + (cookieLog.Length > 0 ? cookieLog.ToString() : "None"));
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("COOKIE_WARN", "Failed to pre-fetch game config cookies: " + ex.Message);
            }
        }

        public async Task<JsonObject> PostFormAsync(string endpoint, Dictionary<string, string> parameters, Dictionary<string, string> customHeaders = null)
        {
            using (NetworkActivity.Begin())
            {
                return await PostFormCoreAsync(endpoint, parameters, customHeaders);
            }
        }

        public async Task<string> UploadImageAsync(byte[] imageBytes, string fileName, string contentType = "image/png")
        {
            using (NetworkActivity.Begin())
            {
                await EnsureGuestSessionAsync();

                var sw = Stopwatch.StartNew();
                var uri = new Uri(BaseUri, "forum/uploadForumImg");

                KuroLogger.ThreadInfo("Network Dispatch", string.Format("Preparing POST /forum/uploadForumImg on Thread #{0} (Size: {1} bytes)", Environment.CurrentManagedThreadId, imageBytes != null ? imageBytes.Length : 0));

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, uri);
                    ApplyHeaders(request);

                    var multipart = new HttpMultipartFormDataContent();
                    var buffer = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(imageBytes);
                    var bufferContent = new HttpBufferContent(buffer);
                    bufferContent.Headers.ContentType = new HttpMediaTypeHeaderValue(contentType);
                    multipart.Add(bufferContent, "files", fileName);
                    request.Content = multipart;

                    var response = await _httpClient.SendRequestAsync(request);
                    sw.Stop();

                    IBuffer responseBuffer = await response.Content.ReadAsBufferAsync();
                    var jsonStr = await DecodeUtf8Async(responseBuffer);

                    KuroLogger.Network("UPLOAD_RESP", string.Format("/forum/uploadForumImg -> Status: {0} ({1}ms)", (int)response.StatusCode, sw.ElapsedMilliseconds),
                        string.Format("ContentLength: {0} bytes\nResponse Body:\n{1}", jsonStr != null ? jsonStr.Length : 0, jsonStr ?? "(null)"));

                    response.EnsureSuccessStatusCode();

                    JsonObject jsonObject = await ParseJsonAsync(jsonStr);
                    if (jsonObject != null && jsonObject.ContainsKey("code") && (int)jsonObject.GetNamedNumber("code") == 200)
                    {
                        if (jsonObject.ContainsKey("data") && jsonObject.GetNamedValue("data").ValueType == JsonValueType.Array)
                        {
                            var dataArr = jsonObject.GetNamedArray("data");
                            if (dataArr.Count > 0)
                            {
                                return dataArr.GetStringAt(0);
                            }
                        }
                    }
                    else if (jsonObject != null && jsonObject.ContainsKey("msg"))
                    {
                        var msg = jsonObject.GetNamedString("msg");
                        KuroLogger.Warn("UPLOAD_FAIL", "Image upload failed: " + msg);
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    KuroLogger.Error("UPLOAD_ERROR", string.Format("Image upload failed after {0}ms: {1}", sw.ElapsedMilliseconds, ex.Message), ex);
                }

                return null;
            }
        }

        private async Task<JsonObject> PostFormCoreAsync(string endpoint, Dictionary<string, string> parameters, Dictionary<string, string> customHeaders = null)
        {
            await EnsureGuestSessionAsync();

            var sw = Stopwatch.StartNew();
            var uri = new Uri(BaseUri, endpoint);

            var sbParams = new StringBuilder();
            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    if (sbParams.Length > 0) sbParams.Append("&");
                    sbParams.Append(kv.Key).Append("=").Append(kv.Value);
                }
            }

            var reqHeaders = new StringBuilder();
            reqHeaders.Append("source=h5, version=3.3.2, channelId=10, lang=zh-CN, countryCode=CN");
            reqHeaders.Append(", devCode=").Append(SettingsHelper.DevCode);
            if (!string.IsNullOrEmpty(SettingsHelper.Token))
            {
                reqHeaders.Append(", token=").Append(SettingsHelper.Token);
            }

            KuroLogger.ThreadInfo("Network Dispatch", string.Format("Preparing POST {0} on Thread #{1}", endpoint, Environment.CurrentManagedThreadId));
            KuroLogger.Network("POST", uri.ToString(), string.Format("Headers: [{0}]\nRequest Body: {1}", 
                reqHeaders.ToString(),
                sbParams.ToString()));

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                ApplyHeaders(request, customHeaders);

                if (parameters != null && parameters.Count > 0)
                {
                    request.Content = new HttpFormUrlEncodedContent(parameters);
                }
                else
                {
                    request.Content = new HttpFormUrlEncodedContent(new Dictionary<string, string>());
                }

                var response = await _httpClient.SendRequestAsync(request);
                sw.Stop();

                IBuffer responseBuffer = await response.Content.ReadAsBufferAsync();
                var jsonStr = await DecodeUtf8Async(responseBuffer);
                
                KuroLogger.Network("RESP", string.Format("{0} -> Status: {1} ({2}ms)", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds),
                    string.Format("ContentLength: {0} bytes\nResponse Body:\n{1}", 
                        jsonStr != null ? jsonStr.Length : 0, 
                        jsonStr != null ? jsonStr : "(null)"));

                response.EnsureSuccessStatusCode();

                JsonObject jsonObject = await ParseJsonAsync(jsonStr);
                if (jsonObject != null)
                {
                    if (jsonObject.ContainsKey("code"))
                    {
                        var code = (int)jsonObject.GetNamedNumber("code");
                        var msg = jsonObject.ContainsKey("msg") ? jsonObject.GetNamedString("msg") : "";
                        if (code == 1301 || code == 220)
                        {
                            KuroLogger.Warn("AUTH_EXPIRED", string.Format("Token required/expired (code {0}): {1}", code, msg));
                        }
                        else if (code != 200)
                        {
                            KuroLogger.Warn("API_CODE", string.Format("API returned code {0}: {1}", code, msg));
                        }
                    }
                    return jsonObject;
                }
                else
                {
                    KuroLogger.Error("JSON_PARSE", "Failed to parse response JSON for " + endpoint);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                KuroLogger.Error("NET_ERROR", string.Format("POST {0} failed after {1}ms: {2}", endpoint, sw.ElapsedMilliseconds, ex.Message), ex);
            }

            return null;
        }

        public async Task<JsonObject> GetAsync(string endpoint)
        {
            using (NetworkActivity.Begin())
            {
                return await GetCoreAsync(endpoint);
            }
        }

        private async Task<JsonObject> GetCoreAsync(string endpoint)
        {
            await EnsureGuestSessionAsync();

            var sw = Stopwatch.StartNew();
            var uri = new Uri(BaseUri, endpoint);

            KuroLogger.ThreadInfo("Network Dispatch", string.Format("Preparing GET {0} on Thread #{1}", endpoint, Environment.CurrentManagedThreadId));
            KuroLogger.Network("GET", uri.ToString(), "Headers: [source=h5, version=3.3.2]");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                ApplyHeaders(request);

                var response = await _httpClient.SendRequestAsync(request);
                sw.Stop();

                var jsonStr = await response.Content.ReadAsStringAsync();
                KuroLogger.Network("RESP", string.Format("{0} -> Status: {1} ({2}ms)", endpoint, (int)response.StatusCode, sw.ElapsedMilliseconds),
                    string.Format("ContentLength: {0} bytes", jsonStr != null ? jsonStr.Length : 0));

                response.EnsureSuccessStatusCode();

                JsonObject jsonObject = await ParseJsonAsync(jsonStr);
                if (jsonObject != null)
                {
                    return jsonObject;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                KuroLogger.Error("NET_ERROR", string.Format("GET {0} failed after {1}ms: {2}", endpoint, sw.ElapsedMilliseconds, ex.Message), ex);
            }

            return null;
        }

        private static async Task<string> DecodeUtf8Async(IBuffer responseBuffer)
        {
            return await Task.Run(() =>
            {
                byte[] responseBytes = new byte[(int)responseBuffer.Length];
                using (DataReader responseReader = DataReader.FromBuffer(responseBuffer))
                {
                    responseReader.ReadBytes(responseBytes);
                }
                return Encoding.UTF8.GetString(responseBytes, 0, responseBytes.Length);
            });
        }

        private static async Task<JsonObject> ParseJsonAsync(string json)
        {
            return await Task.Run(() =>
            {
                JsonObject jsonObject;
                return JsonObject.TryParse(json, out jsonObject) ? jsonObject : null;
            });
        }
    }
}
