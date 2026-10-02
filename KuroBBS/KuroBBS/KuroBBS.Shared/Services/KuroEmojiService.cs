using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroEmojiService
    {
        private static KuroEmojiService _instance;
        public static KuroEmojiService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroEmojiService();
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, string> _emojiIdMap = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _emojiNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _emojiNameIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<EmojiPackageGroup> _packages = new List<EmojiPackageGroup>();
        private bool _isInitialized = false;
        private bool _isLoading = false;

        private static readonly Regex EmojiRegex = new Regex(@"_\[/([^\]]+)\]");

        public async Task InitializeAsync()
        {
            if (_isInitialized || _isLoading) return;
            _isLoading = true;

            try
            {
                // Try load from local storage cache first
                await LoadFromLocalCacheAsync();

                // Fetch fresh emoji catalog from KuroBBS API
                var json = await KuroApiClient.Instance.PostFormAsync("/user/emoji/queryAll", new Dictionary<string, string>());
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
                {
                    _packages.Clear();
                    _emojiIdMap.Clear();
                    _emojiNameMap.Clear();
                    _emojiNameIdMap.Clear();

                    ParseEmojiPackages(json.GetNamedArray("data"));
                    await SaveToLocalCacheAsync(json.Stringify());
                    _isInitialized = true;
                    KuroLogger.Loading("EMOJI_INIT_OK", string.Format("Loaded {0} emojis across {1} packages", _emojiIdMap.Count, _packages.Count));
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("EMOJI_INIT_FAIL", "Failed to load emoji packages: " + ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async Task<List<EmojiPackageGroup>> GetEmojiPackagesAsync()
        {
            if (!_isInitialized && !_isLoading)
            {
                await InitializeAsync();
            }
            return _packages;
        }

        public string GetEmojiId(string emojiNameOrTag)
        {
            if (string.IsNullOrEmpty(emojiNameOrTag)) return null;
            string clean = emojiNameOrTag.Trim();
            if (clean.StartsWith("_[/") && clean.EndsWith("]"))
            {
                clean = clean.Substring(3, clean.Length - 4);
            }
            if (_emojiNameIdMap.ContainsKey(clean)) return _emojiNameIdMap[clean];
            if (_emojiNameIdMap.ContainsKey(emojiNameOrTag)) return _emojiNameIdMap[emojiNameOrTag];

            int dashIdx = clean.IndexOf('-');
            if (dashIdx >= 0 && dashIdx < clean.Length - 1)
            {
                string shortName = clean.Substring(dashIdx + 1);
                if (_emojiNameIdMap.ContainsKey(shortName)) return _emojiNameIdMap[shortName];
            }
            return null;
        }

        private void ParseEmojiPackages(JsonArray packages)
        {
            foreach (var pVal in packages)
            {
                if (pVal.ValueType != JsonValueType.Object) continue;
                var pObj = pVal.GetObject();

                string pkgId = pObj.ContainsKey("id") ? pObj.GetNamedString("id", "") : "";
                string pkgName = pObj.ContainsKey("name") ? pObj.GetNamedString("name", "") : "";

                var pkgGroup = new EmojiPackageGroup
                {
                    PackageId = pkgId,
                    PackageName = !string.IsNullOrEmpty(pkgName) ? pkgName : "表情包"
                };

                if (pObj.ContainsKey("emojiList") && pObj.GetNamedValue("emojiList").ValueType == JsonValueType.Array)
                {
                    var emojiList = pObj.GetNamedArray("emojiList");
                    foreach (var eVal in emojiList)
                    {
                        if (eVal.ValueType != JsonValueType.Object) continue;
                        var eObj = eVal.GetObject();

                        string id = eObj.ContainsKey("id") ? eObj.GetNamedString("id", "") : "";
                        string name = eObj.ContainsKey("name") ? eObj.GetNamedString("name", "") : "";
                        string imgUrl = eObj.ContainsKey("imgUrl") ? eObj.GetNamedString("imgUrl", "") : "";

                        if (!string.IsNullOrEmpty(imgUrl))
                        {
                            pkgGroup.Emojis.Add(new EmojiEntryItem
                            {
                                Id = id,
                                Name = name,
                                ImgUrl = imgUrl
                            });

                            if (!string.IsNullOrEmpty(id))
                            {
                                _emojiIdMap[id] = imgUrl;
                            }

                            if (!string.IsNullOrEmpty(name))
                            {
                                _emojiNameMap[name] = imgUrl;
                                _emojiNameMap["_[/" + name + "]"] = imgUrl;
                                if (!string.IsNullOrEmpty(id))
                                {
                                    _emojiNameIdMap[name] = id;
                                    _emojiNameIdMap["_[/" + name + "]"] = id;
                                }

                                // Also handle sub-name if format is "Package-Name" e.g. "第一弹-哇哦" -> "哇哦"
                                int dashIdx = name.IndexOf('-');
                                if (dashIdx >= 0 && dashIdx < name.Length - 1)
                                {
                                    string shortName = name.Substring(dashIdx + 1);
                                    if (!_emojiNameMap.ContainsKey(shortName))
                                    {
                                        _emojiNameMap[shortName] = imgUrl;
                                    }
                                    if (!string.IsNullOrEmpty(id) && !_emojiNameIdMap.ContainsKey(shortName))
                                    {
                                        _emojiNameIdMap[shortName] = id;
                                    }
                                }
                            }
                        }
                    }
                }

                if (pkgGroup.Emojis.Count > 0)
                {
                    _packages.Add(pkgGroup);
                }
            }
        }

        public string ResolveEmojiUrl(string targetId, string emojiText)
        {
            // 1. Direct ID lookup
            if (!string.IsNullOrEmpty(targetId) && _emojiIdMap.ContainsKey(targetId))
            {
                return _emojiIdMap[targetId];
            }

            // 2. Parse ID from text if formatted like "_[/Name-Id]"
            if (!string.IsNullOrEmpty(emojiText))
            {
                string clean = emojiText.Trim();
                if (clean.StartsWith("_[/") && clean.EndsWith("]"))
                {
                    clean = clean.Substring(3, clean.Length - 4);
                }

                // Check direct name match
                if (_emojiNameMap.ContainsKey(clean))
                {
                    return _emojiNameMap[clean];
                }

                // Check if clean has an embedded ID like "第一弹-哇哦-1698052617052524"
                int lastDash = clean.LastIndexOf('-');
                if (lastDash > 0 && lastDash < clean.Length - 1)
                {
                    string candidateId = clean.Substring(lastDash + 1);
                    if (_emojiIdMap.ContainsKey(candidateId))
                    {
                        return _emojiIdMap[candidateId];
                    }

                    string namePart = clean.Substring(0, lastDash);
                    if (_emojiNameMap.ContainsKey(namePart))
                    {
                        return _emojiNameMap[namePart];
                    }
                }
            }

            // Fallback trigger init if empty
            if (!_isInitialized && !_isLoading)
            {
                var task = InitializeAsync();
            }

            return null;
        }

        public List<PostTextRun> ParseChildrenToRuns(JsonArray childrenArray)
        {
            var result = new List<PostTextRun>();
            if (childrenArray == null) return result;

            foreach (var childVal in childrenArray)
            {
                if (childVal.ValueType != JsonValueType.Object) continue;
                var childObj = childVal.GetObject();

                int type = (int)GetNumber(childObj, "type", 1);
                string content = GetString(childObj, "content", "");
                string target = GetString(childObj, "target", "");

                bool isBold = (childObj.ContainsKey("bold") && childObj.GetNamedValue("bold").ValueType == JsonValueType.Boolean) ? childObj.GetNamedBoolean("bold", false) : false;
                string color = GetString(childObj, "color", "");
                if (string.IsNullOrEmpty(color) && childObj.ContainsKey("style"))
                {
                    string st = GetString(childObj, "style", "");
                    var sm = Regex.Match(st, @"color\s*:\s*([^;""']+)");
                    if (sm.Success) color = sm.Groups[1].Value.Trim();
                }

                if (type == 2 || (!string.IsNullOrEmpty(content) && content.StartsWith("_[/") && content.EndsWith("]")))
                {
                    // Emoji run
                    string emojiUrl = ResolveEmojiUrl(target, content);
                    result.Add(new PostTextRun
                    {
                        IsEmoji = true,
                        Text = content,
                        EmojiUrl = emojiUrl,
                        EmojiName = content,
                        TargetId = target
                    });
                }
                else
                {
                    // If regular text has inline _[/...] patterns, split them
                    if (!string.IsNullOrEmpty(content) && content.Contains("_[/"))
                    {
                        var runs = ParseTextToRuns(content);
                        foreach (var r in runs)
                        {
                            if (!r.IsEmoji)
                            {
                                r.IsBold = isBold;
                                r.ColorHex = color;
                            }
                        }
                        result.AddRange(runs);
                    }
                    else if (!string.IsNullOrEmpty(content))
                    {
                        result.Add(new PostTextRun
                        {
                            IsEmoji = false,
                            Text = content,
                            IsBold = isBold,
                            ColorHex = color
                        });
                    }
                }
            }

            return result;
        }

        public List<PostTextRun> ParseBlocksJsonToRuns(string jsonString)
        {
            var result = new List<PostTextRun>();
            if (string.IsNullOrWhiteSpace(jsonString)) return result;

            try
            {
                JsonArray blocks;
                if (JsonArray.TryParse(jsonString, out blocks))
                {
                    foreach (var bVal in blocks)
                    {
                        if (bVal.ValueType != JsonValueType.Object) continue;
                        var bObj = bVal.GetObject();

                        // 图片块：{"contentType":2,"imgWidth":1080,"imgHeight":2046,"url":"…"}。
                        // 它既没有 children 也没有 content，只走下面两个分支会被**整块丢掉**
                        // → 评论/回复里的图片永远不显示。这里单独识别成图片 run。
                        int contentType = (int)GetNumber(bObj, "contentType", 1);
                        string imgUrl = GetString(bObj, "url", "");
                        if (contentType == 2 && !string.IsNullOrEmpty(imgUrl))
                        {
                            // 平台判定为异常的图片不渲染（与 KuroForumService.AppendContentImages 口径一致）
                            bool abnormal = bObj.ContainsKey("isAbnormal")
                                && bObj.GetNamedValue("isAbnormal").ValueType == JsonValueType.Boolean
                                && bObj.GetNamedBoolean("isAbnormal", false);
                            if (!abnormal)
                            {
                                result.Add(new PostTextRun
                                {
                                    IsImage = true,
                                    ImageUrl = imgUrl,
                                    ImageWidth = (int)GetNumber(bObj, "imgWidth", 0),
                                    ImageHeight = (int)GetNumber(bObj, "imgHeight", 0)
                                });
                            }
                            continue;
                        }

                        if (bObj.ContainsKey("children") && bObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                        {
                            var runs = ParseChildrenToRuns(bObj.GetNamedArray("children"));
                            result.AddRange(runs);
                        }
                        else if (bObj.ContainsKey("content"))
                        {
                            var cStr = GetString(bObj, "content", "");
                            if (!string.IsNullOrEmpty(cStr))
                            {
                                result.AddRange(ParseTextToRuns(cStr));
                            }
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        public List<PostTextRun> ParseTextToRuns(string rawText)
        {
            var result = new List<PostTextRun>();
            if (string.IsNullOrEmpty(rawText)) return result;

            int lastIndex = 0;
            var matches = EmojiRegex.Matches(rawText);

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    result.Add(new PostTextRun
                    {
                        IsEmoji = false,
                        Text = rawText.Substring(lastIndex, match.Index - lastIndex)
                    });
                }

                string emojiRaw = match.Value; // e.g. "_[/第一弹-哇哦]" or "_[/盯-1721378114419464]"
                string emojiUrl = ResolveEmojiUrl(null, emojiRaw);

                result.Add(new PostTextRun
                {
                    IsEmoji = true,
                    Text = emojiRaw,
                    EmojiUrl = emojiUrl,
                    EmojiName = emojiRaw
                });

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < rawText.Length)
            {
                result.Add(new PostTextRun
                {
                    IsEmoji = false,
                    Text = rawText.Substring(lastIndex)
                });
            }

            return result;
        }

        private async Task LoadFromLocalCacheAsync()
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.GetFileAsync("kuro_emojis_cache.json");
                if (file != null)
                {
                    string content = await FileIO.ReadTextAsync(file);
                    if (!string.IsNullOrEmpty(content))
                    {
                        JsonObject json;
                        if (JsonObject.TryParse(content, out json) && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
                        {
                            ParseEmojiPackages(json.GetNamedArray("data"));
                        }
                    }
                }
            }
            catch { }
        }

        private async Task SaveToLocalCacheAsync(string jsonStr)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonStr)) return;
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync("kuro_emojis_cache.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, jsonStr);
            }
            catch { }
        }

        private string GetString(JsonObject obj, string key, string fallback = "")
        {
            if (obj == null || !obj.ContainsKey(key)) return fallback;
            var val = obj.GetNamedValue(key);
            if (val.ValueType == JsonValueType.String) return val.GetString();
            if (val.ValueType == JsonValueType.Number) return val.GetNumber().ToString();
            return fallback;
        }

        private double GetNumber(JsonObject obj, string key, double fallback = 0)
        {
            if (obj == null || !obj.ContainsKey(key)) return fallback;
            var val = obj.GetNamedValue(key);
            if (val.ValueType == JsonValueType.Number) return val.GetNumber();
            if (val.ValueType == JsonValueType.String)
            {
                double d;
                if (double.TryParse(val.GetString(), out d)) return d;
            }
            return fallback;
        }
    }
}
