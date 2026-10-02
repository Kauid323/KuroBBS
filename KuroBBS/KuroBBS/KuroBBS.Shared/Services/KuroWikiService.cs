using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class KuroWikiService
    {
        private static KuroWikiService _instance;
        public static KuroWikiService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroWikiService();
                }
                return _instance;
            }
        }

        private readonly Dictionary<int, WikiHomepageData> _homepageCache = new Dictionary<int, WikiHomepageData>();
        private readonly Dictionary<int, WikiCatalogueNode> _treeCache = new Dictionary<int, WikiCatalogueNode>();
        private readonly Dictionary<string, Tuple<List<WikiItemRecord>, List<WikiTagNode>, string>> _cataloguePageCache = new Dictionary<string, Tuple<List<WikiItemRecord>, List<WikiTagNode>, string>>();
        private readonly Dictionary<string, WikiEntryDetail> _entryDetailCache = new Dictionary<string, WikiEntryDetail>();

        public void ClearCache()
        {
            _homepageCache.Clear();
            _treeCache.Clear();
            _cataloguePageCache.Clear();
            _entryDetailCache.Clear();
        }

        private Dictionary<string, string> BuildWikiHeaders(int wikiType)
        {
            return new Dictionary<string, string>
            {
                { "wiki_type", wikiType.ToString() },
                { "Referer", "https://wiki.kurobbs.com/" }
            };
        }

        public async Task<WikiHomepageData> GetWikiHomepageAsync(int wikiType, bool forceRefresh = false)
        {
            if (!forceRefresh && _homepageCache.ContainsKey(wikiType))
            {
                return _homepageCache[wikiType];
            }

            var data = new WikiHomepageData
            {
                WikiType = wikiType,
                GameName = wikiType == 9 ? "鸣潮" : "战双帕弥什"
            };

            try
            {
                var headers = BuildWikiHeaders(wikiType);
                var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/homepage/getPage", new Dictionary<string, string>(), headers);
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var dataObj = json.GetNamedObject("data");
                    JsonObject contentObj = null;

                    if (dataObj.ContainsKey("contentJson"))
                    {
                        var cVal = dataObj.GetNamedValue("contentJson");
                        if (cVal.ValueType == JsonValueType.Object)
                        {
                            contentObj = cVal.GetObject();
                        }
                        else if (cVal.ValueType == JsonValueType.String)
                        {
                            JsonObject parsed;
                            if (JsonObject.TryParse(cVal.GetString(), out parsed))
                            {
                                contentObj = parsed;
                            }
                        }
                    }

                    if (contentObj != null)
                    {
                        // 1. Banner
                        if (contentObj.ContainsKey("banner") && contentObj.GetNamedValue("banner").ValueType == JsonValueType.Array)
                        {
                            var bannerArr = contentObj.GetNamedArray("banner");
                            foreach (var bVal in bannerArr)
                            {
                                if (bVal.ValueType != JsonValueType.Object) continue;
                                var bObj = bVal.GetObject();
                                var bItem = new WikiBannerItem
                                {
                                    Title = GetString(bObj, "title", ""),
                                    Describe = GetString(bObj, "describe", ""),
                                    Url = GetString(bObj, "url", ""),
                                    Active = GetBoolean(bObj, "active", true)
                                };

                                if (bObj.ContainsKey("linkConfig") && bObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                {
                                    var lc = bObj.GetNamedObject("linkConfig");
                                    bItem.LinkType = (int)GetNumber(lc, "linkType", 0);
                                    bItem.CatalogueId = (int)GetNumber(lc, "catalogueId", 0);
                                    bItem.EntryId = GetString(lc, "entryId", "");
                                    bItem.LinkUrl = GetString(lc, "linkUrl", "");
                                }

                                if (string.IsNullOrEmpty(bItem.Title) && !string.IsNullOrEmpty(bItem.Describe))
                                {
                                    bItem.Title = bItem.Describe;
                                }

                                data.Banners.Add(bItem);
                            }
                        }

                        // 2. Announcements
                        if (contentObj.ContainsKey("announcement") && contentObj.GetNamedValue("announcement").ValueType == JsonValueType.Array)
                        {
                            var annArr = contentObj.GetNamedArray("announcement");
                            foreach (var aVal in annArr)
                            {
                                if (aVal.ValueType != JsonValueType.Object) continue;
                                var aObj = aVal.GetObject();
                                var aItem = new WikiAnnouncementItem
                                {
                                    Name = GetString(aObj, "name", "公告"),
                                    Active = GetBoolean(aObj, "active", true),
                                    LinkCardVisible = GetBoolean(aObj, "linkCardVisible", false),
                                    // 公告正文（announcement[].content）是 HTML，
                                    // 这才是「公告 / 更新日志」的实际内容，必须解析。
                                    Body = CleanHtmlToText(GetString(aObj, "content", ""))
                                };

                                if (aObj.ContainsKey("linkCard") && aObj.GetNamedValue("linkCard").ValueType == JsonValueType.Object)
                                {
                                    var lc = aObj.GetNamedObject("linkCard");
                                    aItem.Title = GetString(lc, "title", "");
                                    aItem.Content = GetString(lc, "content", "");
                                    aItem.ImgUrl = GetString(lc, "imgUrl", "");
                                    if (lc.ContainsKey("linkConfig") && lc.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                    {
                                        var lcc = lc.GetNamedObject("linkConfig");
                                        aItem.LinkUrl = GetString(lcc, "linkUrl", "");
                                        aItem.LinkType = (int)GetNumber(lcc, "linkType", 0);
                                    }
                                }

                                data.Announcements.Add(aItem);
                            }
                        }

                        // 3. Shortcuts (快捷导航)
                        if (contentObj.ContainsKey("shortcuts") && contentObj.GetNamedValue("shortcuts").ValueType == JsonValueType.Object)
                        {
                            var scObj = contentObj.GetNamedObject("shortcuts");
                            if (scObj.ContainsKey("content") && scObj.GetNamedValue("content").ValueType == JsonValueType.Array)
                            {
                                var scArr = scObj.GetNamedArray("content");
                                foreach (var sVal in scArr)
                                {
                                    if (sVal.ValueType != JsonValueType.Object) continue;
                                    var sObj = sVal.GetObject();
                                    string icon = GetString(sObj, "iconUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(sObj, "contentUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(sObj, "mobileImgUrl", "");

                                    var sItem = new WikiShortcutItem
                                    {
                                        Title = GetString(sObj, "title", ""),
                                        IconUrl = icon,
                                        IconGlyph = GetShortcutGlyph(GetString(sObj, "title", ""))
                                    };

                                    if (sObj.ContainsKey("linkConfig") && sObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                    {
                                        var lc = sObj.GetNamedObject("linkConfig");
                                        sItem.CatalogueId = (int)GetNumber(lc, "catalogueId", 0);
                                        sItem.EntryId = GetString(lc, "entryId", "");
                                        sItem.LinkUrl = GetString(lc, "linkUrl", "");
                                        sItem.LinkType = (int)GetNumber(lc, "linkType", 0);
                                    }

                                    data.Shortcuts.Add(sItem);
                                }
                            }
                        }

                        // 4. Main Modules (主模块: 图鉴, 攻略, 剧情等)
                        if (contentObj.ContainsKey("mainModules") && contentObj.GetNamedValue("mainModules").ValueType == JsonValueType.Array)
                        {
                            var mmArr = contentObj.GetNamedArray("mainModules");
                            foreach (var mVal in mmArr)
                            {
                                if (mVal.ValueType != JsonValueType.Object) continue;
                                var mObj = mVal.GetObject();
                                string icon = GetString(mObj, "iconUrl", "");
                                if (string.IsNullOrEmpty(icon)) icon = GetString(mObj, "contentUrl", "");
                                if (string.IsNullOrEmpty(icon)) icon = GetString(mObj, "mobileImgUrl", "");

                                var mItem = new WikiShortcutItem
                                {
                                    Title = GetString(mObj, "title", ""),
                                    IconUrl = icon,
                                    IconGlyph = GetShortcutGlyph(GetString(mObj, "title", ""))
                                };

                                if (mObj.ContainsKey("more") && mObj.GetNamedValue("more").ValueType == JsonValueType.Object)
                                {
                                    var moreObj = mObj.GetNamedObject("more");
                                    if (moreObj.ContainsKey("linkConfig") && moreObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                    {
                                        var lc = moreObj.GetNamedObject("linkConfig");
                                        mItem.CatalogueId = (int)GetNumber(lc, "catalogueId", 0);
                                        mItem.EntryId = GetString(lc, "entryId", "");
                                        mItem.LinkUrl = GetString(lc, "linkUrl", "");
                                        mItem.LinkType = (int)GetNumber(lc, "linkType", 0);
                                    }
                                }

                                // 主页模块可能指向「分组节点」（如图鉴=1024），其 getPage 恒为空。
                                // 这里把挂载的子目录也解析出来，供导航层引导用户逐级进入。
                                if (mObj.ContainsKey("content") && mObj.GetNamedValue("content").ValueType == JsonValueType.Object)
                                {
                                    var cObj = mObj.GetNamedObject("content");

                                    // some modules（如「主题影音」）的 more.linkConfig 里没有 catalogueId，
                                    // 真正的目标目录挂在 content.id 上（已经验证：主题影音 content.id=1029）。
                                    // 回退取用，避免这类模块「点了没反应」。
                                    if (mItem.CatalogueId <= 0)
                                    {
                                        int contentId = (int)GetNumber(cObj, "id", 0);
                                        if (contentId > 0) mItem.CatalogueId = contentId;
                                    }

                                    if (cObj.ContainsKey("children") && cObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                                    {
                                        ParseCatalogueChildren(cObj.GetNamedArray("children"), mItem.Children, mItem.CatalogueId);
                                    }
                                }

                                // 计算导航目标：优先落到 content.children 里 active:true 的子目录
                                // （与官方前端一致：图鉴→机体图鉴、攻略→版本攻略、剧情→主线剧情、主题影音→节日贺图）。
                                // 若没有 active 标记，则回退到 CatalogueId 本身。
                                mItem.TargetCatalogueId = ResolveTargetCatalogueId(mItem);

                                data.MainModules.Add(mItem);
                            }
                        }

                        // 5. Side Modules (侧边模块: 研发池/卡池, 战区/副本, 贡献榜等)
                        if (contentObj.ContainsKey("sideModules") && contentObj.GetNamedValue("sideModules").ValueType == JsonValueType.Array)
                        {
                            var smArr = contentObj.GetNamedArray("sideModules");
                            foreach (var sVal in smArr)
                            {
                                if (sVal.ValueType != JsonValueType.Object) continue;
                                var sObj = sVal.GetObject();
                                string icon = GetString(sObj, "iconUrl", "");
                                if (string.IsNullOrEmpty(icon)) icon = GetString(sObj, "contentUrl", "");
                                if (string.IsNullOrEmpty(icon)) icon = GetString(sObj, "mobileImgUrl", "");

                                var sItem = new WikiShortcutItem
                                {
                                    Title = GetString(sObj, "title", ""),
                                    IconUrl = icon,
                                    IconGlyph = GetShortcutGlyph(GetString(sObj, "title", ""))
                                };

                                if (sObj.ContainsKey("more") && sObj.GetNamedValue("more").ValueType == JsonValueType.Object)
                                {
                                    var moreObj = sObj.GetNamedObject("more");
                                    if (moreObj.ContainsKey("linkConfig") && moreObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                    {
                                        var lc = moreObj.GetNamedObject("linkConfig");
                                        sItem.CatalogueId = (int)GetNumber(lc, "catalogueId", 0);
                                        sItem.EntryId = GetString(lc, "entryId", "");
                                        sItem.LinkUrl = GetString(lc, "linkUrl", "");
                                        sItem.LinkType = (int)GetNumber(lc, "linkType", 0);
                                    }
                                }

                                data.SideModules.Add(sItem);
                            }
                        }
                    }
                }

                // Also load top contributors
                var contribs = await GetTopContributorsAsync(wikiType, 2);
                if (contribs != null)
                {
                    data.Contributors.AddRange(contribs);
                }

                _homepageCache[wikiType] = data;
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_HOMEPAGE_ERR", string.Format("Error loading wiki homepage (type={0}): {1}", wikiType, ex.Message), ex);
            }

            return data;
        }

        public async Task<WikiCatalogueNode> GetWikiTreeAsync(int wikiType, bool forceRefresh = false)
        {
            if (!forceRefresh && _treeCache.ContainsKey(wikiType))
            {
                return _treeCache[wikiType];
            }

            try
            {
                var headers = BuildWikiHeaders(wikiType);
                var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/catalogue/config/getTree", new Dictionary<string, string>(), headers);
                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var rootNode = ParseCatalogueNode(json.GetNamedObject("data"));
                    _treeCache[wikiType] = rootNode;
                    return rootNode;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_TREE_ERR", string.Format("Error loading wiki tree (type={0}): {1}", wikiType, ex.Message), ex);
            }

            return new WikiCatalogueNode { Name = "目录" };
        }

        private WikiCatalogueNode ParseCatalogueNode(JsonObject obj)
        {
            var node = new WikiCatalogueNode
            {
                Id = (int)GetNumber(obj, "id", 0),
                Key = (int)GetNumber(obj, "key", (int)GetNumber(obj, "id", 0)),
                Name = GetString(obj, "name", ""),
                ParentId = (int)GetNumber(obj, "parentId", 0),
                Level = (int)GetNumber(obj, "level", 0),
                Sort = (int)GetNumber(obj, "sort", 0)
            };

            if (obj.ContainsKey("children") && obj.GetNamedValue("children").ValueType == JsonValueType.Array)
            {
                var arr = obj.GetNamedArray("children");
                foreach (var itemVal in arr)
                {
                    if (itemVal.ValueType != JsonValueType.Object) continue;
                    node.Children.Add(ParseCatalogueNode(itemVal.GetObject()));
                }
            }

            return node;
        }

        public async Task<Tuple<List<WikiItemRecord>, List<WikiTagNode>, string>> GetCatalogueItemPageAsync(int wikiType, int catalogueId, int page = 1, int limit = 1000, bool forceRefresh = false)
        {
            string cacheKey = string.Format("{0}_{1}_{2}_{3}", wikiType, catalogueId, page, limit);
            if (!forceRefresh && _cataloguePageCache.ContainsKey(cacheKey))
            {
                return _cataloguePageCache[cacheKey];
            }

            var items = new List<WikiItemRecord>();
            var tags = new List<WikiTagNode>();
            string title = "";

            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "catalogueId", catalogueId.ToString() },
                    { "page", page.ToString() },
                    { "limit", limit.ToString() }
                };

                var headers = BuildWikiHeaders(wikiType);
                var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/catalogue/item/getPage", parameters, headers);

                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                {
                    var dataObj = json.GetNamedObject("data");

                    // 1. Tag Tree
                    if (dataObj.ContainsKey("tagTree") && dataObj.GetNamedValue("tagTree").ValueType == JsonValueType.Object)
                    {
                        var tagTreeObj = dataObj.GetNamedObject("tagTree");
                        title = GetString(tagTreeObj, "name", "");
                        if (tagTreeObj.ContainsKey("children") && tagTreeObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                        {
                            var tagArr = tagTreeObj.GetNamedArray("children");
                            foreach (var tVal in tagArr)
                            {
                                if (tVal.ValueType != JsonValueType.Object) continue;
                                tags.Add(ParseTagNode(tVal.GetObject()));
                            }
                        }
                    }

                    // 2. Records
                    if (dataObj.ContainsKey("results") && dataObj.GetNamedValue("results").ValueType == JsonValueType.Object)
                    {
                        var resObj = dataObj.GetNamedObject("results");
                        if (resObj.ContainsKey("records") && resObj.GetNamedValue("records").ValueType == JsonValueType.Array)
                        {
                            var recArr = resObj.GetNamedArray("records");
                            foreach (var rVal in recArr)
                            {
                                if (rVal.ValueType != JsonValueType.Object) continue;
                                var rObj = rVal.GetObject();
                                var record = new WikiItemRecord
                                {
                                    Id = (int)GetNumber(rObj, "id", 0),
                                    Name = GetString(rObj, "name", ""),
                                    EntryId = GetString(rObj, "entryId", ((long)GetNumber(rObj, "entryId", 0)).ToString()),
                                    CatalogueId = catalogueId
                                };

                                if (rObj.ContainsKey("content") && rObj.GetNamedValue("content").ValueType == JsonValueType.Object)
                                {
                                    var cObj = rObj.GetNamedObject("content");
                                    record.Title = GetString(cObj, "title", record.Name);
                                    
                                    string icon = GetString(cObj, "contentUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(cObj, "iconUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(cObj, "figureUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(cObj, "mobileImgUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(cObj, "imgUrl", "");
                                    if (string.IsNullOrEmpty(icon)) icon = GetString(cObj, "customBgUrl", "");
                                    record.IconUrl = icon;

                                    record.CornerMarkUrl = GetString(cObj, "cornerMarkUrl", "");
                                    record.Level = GetString(cObj, "level", "");
                                    record.LinkUrl = GetString(cObj, "linkUrl", "");

                                    if (cObj.ContainsKey("linkConfig") && cObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                    {
                                        var lc = cObj.GetNamedObject("linkConfig");
                                        record.LinkType = (int)GetNumber(lc, "linkType", 1);
                                        string eid = GetString(lc, "entryId", "");
                                        if (!string.IsNullOrEmpty(eid) && eid != "0")
                                        {
                                             record.EntryId = eid;
                                        }
                                    }
                                    else if (cObj.ContainsKey("linkGather") && cObj.GetNamedValue("linkGather").ValueType == JsonValueType.Array)
                                    {
                                        var lgArr = cObj.GetNamedArray("linkGather");
                                        foreach (var lgVal in lgArr)
                                        {
                                            if (lgVal.ValueType == JsonValueType.Object)
                                            {
                                                var lgObj = lgVal.GetObject();
                                                if (lgObj.ContainsKey("linkConfig") && lgObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                                {
                                                    var lc = lgObj.GetNamedObject("linkConfig");
                                                    string eid = GetString(lc, "entryId", "");
                                                    if (!string.IsNullOrEmpty(eid) && eid != "0")
                                                    {
                                                        record.EntryId = eid;
                                                        record.LinkType = (int)GetNumber(lc, "linkType", 1);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (cObj.ContainsKey("relateTagIds") && cObj.GetNamedValue("relateTagIds").ValueType == JsonValueType.Array)
                                    {
                                        var tagIds = cObj.GetNamedArray("relateTagIds");
                                        foreach (var tid in tagIds)
                                        {
                                            if (tid.ValueType == JsonValueType.String) record.RelateTagIds.Add(tid.GetString());
                                            else if (tid.ValueType == JsonValueType.Number) record.RelateTagIds.Add(((int)tid.GetNumber()).ToString());
                                        }
                                    }
                                }

                                if (string.IsNullOrEmpty(record.Title)) record.Title = record.Name;
                                if (string.IsNullOrEmpty(record.Name)) record.Name = record.Title;

                                items.Add(record);
                            }
                        }
                    }
                }

                var resTuple = Tuple.Create(items, tags, title);
                if (items.Count > 0 || tags.Count > 0)
                {
                    _cataloguePageCache[cacheKey] = resTuple;
                }
                return resTuple;
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_PAGE_ERR", string.Format("Error loading catalogue item page (catId={0}): {1}", catalogueId, ex.Message), ex);
            }

            return Tuple.Create(items, tags, title);
        }

        private WikiTagNode ParseTagNode(JsonObject obj)
        {
            var node = new WikiTagNode
            {
                Id = (int)GetNumber(obj, "id", 0),
                Name = GetString(obj, "name", ""),
                Level = (int)GetNumber(obj, "level", 1)
            };

            if (obj.ContainsKey("children") && obj.GetNamedValue("children").ValueType == JsonValueType.Array)
            {
                var arr = obj.GetNamedArray("children");
                foreach (var itemVal in arr)
                {
                    if (itemVal.ValueType != JsonValueType.Object) continue;
                    node.Children.Add(ParseTagNode(itemVal.GetObject()));
                }
            }

            return node;
        }

        public async Task<WikiEntryDetail> GetEntryDetailAsync(int wikiType, string entryId, bool forceRefresh = false)
        {
            string cacheKey = string.Format("{0}_{1}", wikiType, entryId);
            if (!forceRefresh && _entryDetailCache.ContainsKey(cacheKey))
            {
                return _entryDetailCache[cacheKey];
            }

                var detail = new WikiEntryDetail { Id = entryId };

                try
                {
                    var parameters = new Dictionary<string, string>
                    {
                        { "id", entryId }
                    };

                    var headers = BuildWikiHeaders(wikiType);
                    var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/catalogue/item/getEntryDetail", parameters, headers);

                    if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Object)
                    {
                        var dObj = json.GetNamedObject("data");
                        detail.Id = GetString(dObj, "id", entryId);
                        detail.Name = GetString(dObj, "name", "");
                        detail.OrgFullName = GetString(dObj, "orgFullName", "");
                        detail.LastUpdateTime = GetString(dObj, "lastUpdateTime", "");
                        detail.LastEditUserName = GetString(dObj, "lastEditUserName", "");
                        detail.BrowseCount = (int)GetNumber(dObj, "browseCount", 0);

                        // 意识手册条目使用专属版式（图鉴 > 意识手册）
                        bool isConsciousnessEntry = !string.IsNullOrEmpty(detail.OrgFullName)
                                                    && detail.OrgFullName.Contains("意识手册");

                    // Content & Modules
                    if (dObj.ContainsKey("content") && dObj.GetNamedValue("content").ValueType == JsonValueType.Object)
                    {
                        var cObj = dObj.GetNamedObject("content");
                        detail.Title = GetString(cObj, "title", detail.Name);

                        if (cObj.ContainsKey("modules") && cObj.GetNamedValue("modules").ValueType == JsonValueType.Array)
                        {
                            var modArr = cObj.GetNamedArray("modules");
                            foreach (var mVal in modArr)
                            {
                                if (mVal.ValueType != JsonValueType.Object) continue;
                                var mObj = mVal.GetObject();
                                var mod = new WikiDetailModule
                                {
                                    Title = GetString(mObj, "title", "")
                                };

                                if (mObj.ContainsKey("components") && mObj.GetNamedValue("components").ValueType == JsonValueType.Array)
                                {
                                    var compArr = mObj.GetNamedArray("components");
                                    foreach (var cpVal in compArr)
                                    {
                                        if (cpVal.ValueType != JsonValueType.Object) continue;
                                        var cpObj = cpVal.GetObject();
                                         var comp = new WikiDetailComponent
                                        {
                                            Type = GetString(cpObj, "type", ""),
                                            Title = GetString(cpObj, "title", ""),
                                            Size = GetString(cpObj, "size", ""),
                                            Content = GetString(cpObj, "content", ""),
                                            ImageUrl = GetString(cpObj, "url", GetString(cpObj, "imgUrl", "")),
                                            IsCollapsed = GetBoolean(cpObj, "collapse", false)
                                        };

                                        if (cpObj.ContainsKey("imageList") && cpObj.GetNamedValue("imageList").ValueType == JsonValueType.Array)
                                        {
                                            var imgArr = cpObj.GetNamedArray("imageList");
                                            foreach (var imgVal in imgArr)
                                            {
                                                if (imgVal.ValueType == JsonValueType.String)
                                                {
                                                    comp.ImageList.Add(imgVal.GetString());
                                                }
                                            }
                                        }

                                        // 1. Role Component (PNS & MC)
                                        if (comp.Type == "role-component")
                                        {
                                            comp.RoleInfo = ParseRoleComponent(cpObj);
                                        }

                                        // 2. Tabs Component
                                        if (comp.Type == "tabs-component" && cpObj.ContainsKey("tabs") && cpObj.GetNamedValue("tabs").ValueType == JsonValueType.Array)
                                        {
                                            var tabsArr = cpObj.GetNamedArray("tabs");
                                            foreach (var tVal in tabsArr)
                                            {
                                                if (tVal.ValueType != JsonValueType.Object) continue;
                                                var tObj = tVal.GetObject();
                                                var tabItem = new WikiTabItem
                                                {
                                                    Title = GetString(tObj, "title", "标签"),
                                                    RawContent = GetString(tObj, "content", ""),
                                                    IsSelected = GetBoolean(tObj, "active", false)
                                                };

                                                // Extract structured rich subcomponents
                                                tabItem.BigImageUrl = ExtractBigIllustration(tabItem.RawContent);

                                                var sections = ParseDetailsTree(tabItem.RawContent);
                                                if (sections != null && sections.Count > 0)
                                                {
                                                    foreach (var s in sections) tabItem.Sections.Add(s);
                                                }

                                                var voiceRows = ParseVoiceRows(tabItem.RawContent);
                                                if (voiceRows != null && voiceRows.Count > 0)
                                                {
                                                    tabItem.VoiceRows.AddRange(voiceRows);
                                                }

                                                var skillRows = ParseSkillRows(tabItem.RawContent);
                                                if (skillRows != null && skillRows.Count > 0)
                                                {
                                                    tabItem.SkillRows.AddRange(skillRows);
                                                }

                                                string compTitle = comp.Title ?? "";
                                                string tabTitle = tabItem.Title ?? "";

                                                if (compTitle.Contains("流程") || compTitle.Contains("手法") || compTitle.Contains("连招") || tabTitle.Contains("流程") || tabTitle.Contains("连招"))
                                                {
                                                    var rotationLines = ParseRotationFlow(tabItem.RawContent, tabItem.Title);
                                                    if (rotationLines != null && rotationLines.Count > 0)
                                                    {
                                                        tabItem.RotationLines.AddRange(rotationLines);
                                                    }
                                                }
                                                else if (compTitle.Contains("队") || compTitle.Contains("阵容") || compTitle.Contains("配队"))
                                                {
                                                    var teamGroups = ParseTeamTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                    if (teamGroups != null && teamGroups.Count > 0)
                                                    {
                                                        tabItem.EquipGroups.AddRange(teamGroups);
                                                    }
                                                }
                                                else if (compTitle.Contains("意识"))
                                                {
                                                    var consciousnessGroups = ParseConsciousnessTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                    if (consciousnessGroups != null && consciousnessGroups.Count > 0)
                                                    {
                                                        tabItem.EquipGroups.AddRange(consciousnessGroups);
                                                    }
                                                }
                                                else if (compTitle.Contains("装备") || compTitle.Contains("武器") || compTitle.Contains("辅助机"))
                                                {
                                                    var equipGroups = ParseEquipOrPetTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                    if (equipGroups != null && equipGroups.Count > 0)
                                                    {
                                                        tabItem.EquipGroups.AddRange(equipGroups);
                                                    }
                                                }
                                                else
                                                {
                                                    if (tabTitle.Contains("队") || tabTitle.Contains("阵容") || tabTitle.Contains("配队"))
                                                    {
                                                        var teamGroups = ParseTeamTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                        if (teamGroups != null && teamGroups.Count > 0) tabItem.EquipGroups.AddRange(teamGroups);
                                                    }
                                                    else if (tabTitle.Contains("意识"))
                                                    {
                                                        var consciousnessGroups = ParseConsciousnessTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                        if (consciousnessGroups != null && consciousnessGroups.Count > 0) tabItem.EquipGroups.AddRange(consciousnessGroups);
                                                    }
                                                    else if (tabTitle.Contains("武器") || tabTitle.Contains("辅助机") || tabTitle.Contains("装备"))
                                                    {
                                                        var equipGroups = ParseEquipOrPetTab(tabItem.RawContent, tabItem.Title, compTitle);
                                                        if (equipGroups != null && equipGroups.Count > 0) tabItem.EquipGroups.AddRange(equipGroups);
                                                    }
                                                }

                                                // Extract link if any (e.g. 好感剧情)
                                                var aMatch = Regex.Match(tabItem.RawContent, @"<a[^>]+href=[""'](?<url>[^""']+)[""'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                                if (aMatch.Success)
                                                {
                                                    tabItem.LinkUrl = WebUtility.HtmlDecode(aMatch.Groups["url"].Value);
                                                    tabItem.LinkTitle = CleanHtmlToText(aMatch.Groups["text"].Value);
                                                    if (string.IsNullOrWhiteSpace(tabItem.LinkTitle))
                                                    {
                                                        tabItem.LinkTitle = "点击此处前往查看相关条目详情";
                                                    }

                                                    var entryMatch = Regex.Match(tabItem.LinkUrl, @"/item/(\d+)");
                                                    if (entryMatch.Success)
                                                    {
                                                        tabItem.LinkEntryId = entryMatch.Groups[1].Value;
                                                    }
                                                }

                                                tabItem.CleanText = CleanHtmlToText(tabItem.RawContent);
                                                tabItem.Runs = KuroHtmlPostParser.ParseInlineHtml(tabItem.RawContent, false, null);
                                                tabItem.ImageList = ExtractImageUrlsFromHtml(tabItem.RawContent);
                                                comp.Tabs.Add(tabItem);
                                            }

                                            if (comp.Tabs.Count > 0)
                                            {
                                                WikiTabItem activeTab = null;
                                                foreach (var t in comp.Tabs)
                                                {
                                                    if (t.IsSelected) { activeTab = t; break; }
                                                }
                                                comp.SelectedTab = activeTab ?? comp.Tabs[0];
                                            }
                                        }

                                        // 2.5 Consciousness (意识手册) dedicated components
                                        //    These live under 图鉴 > 意识手册 and use a very uniform layout:
                                        //    简介 / 意识立绘 / 突破素材 / 意识故事 / 意识使用心得
                                        var consciousnessComp = ParseConsciousnessComponent(comp, cpObj, isConsciousnessEntry);
                                        if (consciousnessComp != null)
                                        {
                                            comp.Consciousness = consciousnessComp;
                                        }

                                        // 3. Strategy Component
                                        if (comp.Type == "strategy-component" && cpObj.ContainsKey("strategy") && cpObj.GetNamedValue("strategy").ValueType == JsonValueType.Array)
                                        {
                                            var stratArr = cpObj.GetNamedArray("strategy");
                                            foreach (var sVal in stratArr)
                                            {
                                                if (sVal.ValueType != JsonValueType.Object) continue;
                                                var sObj = sVal.GetObject();
                                                var strat = new WikiStrategyItem
                                                {
                                                    Title = GetString(sObj, "title", "相关攻略"),
                                                    BgUrl = GetString(sObj, "bgUrl", "")
                                                };
                                                if (sObj.ContainsKey("linkConfig") && sObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                                {
                                                    var lc = sObj.GetNamedObject("linkConfig");
                                                    strat.EntryId = GetString(lc, "entryId", "");
                                                    strat.LinkType = (int)GetNumber(lc, "linkType", 1);
                                                }
                                                comp.Strategies.Add(strat);
                                            }
                                        }

                                        // 4. Audio Component
                                        if (comp.Type == "audio-component" && cpObj.ContainsKey("mediaTabs") && cpObj.GetNamedValue("mediaTabs").ValueType == JsonValueType.Array)
                                        {
                                            var mTabsArr = cpObj.GetNamedArray("mediaTabs");
                                            foreach (var mtVal in mTabsArr)
                                            {
                                                if (mtVal.ValueType != JsonValueType.Object) continue;
                                                var mtObj = mtVal.GetObject();
                                                var audioTab = new WikiAudioTab
                                                {
                                                    Title = GetString(mtObj, "title", "语音")
                                                };
                                                if (mtObj.ContainsKey("mediaList") && mtObj.GetNamedValue("mediaList").ValueType == JsonValueType.Array)
                                                {
                                                    var mList = mtObj.GetNamedArray("mediaList");
                                                    foreach (var mediaVal in mList)
                                                    {
                                                        if (mediaVal.ValueType != JsonValueType.Object) continue;
                                                        var mediaObj = mediaVal.GetObject();
                                                        audioTab.Audios.Add(new WikiAudioItem
                                                        {
                                                            Script = GetString(mediaObj, "content", ""),
                                                            PlayUrl = GetString(mediaObj, "playUrl", ""),
                                                            FileSize = (long)GetNumber(mediaObj, "fileSize", 0)
                                                        });
                                                    }
                                                }
                                                comp.AudioTabs.Add(audioTab);
                                            }
                                        }

                                        // Clean text, Runs, and extract images for text/basic components
                                        if (!string.IsNullOrEmpty(comp.Content))
                                        {
                                            comp.CleanText = CleanHtmlToText(comp.Content);
                                            comp.Runs = KuroHtmlPostParser.ParseInlineHtml(comp.Content, false, null);
                                            var extractedImgs = ExtractImageUrlsFromHtml(comp.Content);
                                            foreach (var img in extractedImgs)
                                            {
                                                if (!comp.ImageList.Contains(img))
                                                {
                                                    comp.ImageList.Add(img);
                                                }
                                            }
                                        }

                                        mod.Components.Add(comp);
                                    }
                                }

                                detail.Modules.Add(mod);
                            }
                        }
                    }

                    // User Score list / Contributors
                    if (dObj.ContainsKey("userScoreList") && dObj.GetNamedValue("userScoreList").ValueType == JsonValueType.Array)
                    {
                        var uArr = dObj.GetNamedArray("userScoreList");
                        int rank = 0;
                        foreach (var uVal in uArr)
                        {
                            if (uVal.ValueType != JsonValueType.Object) continue;
                            var uObj = uVal.GetObject();
                            detail.Contributors.Add(new WikiContributorItem
                            {
                                Uid = GetString(uObj, "userId", ((int)GetNumber(uObj, "userId", 0)).ToString()),
                                UserName = GetString(uObj, "userName", ""),
                                UserHeadUrl = GetString(uObj, "userHeadUrl", ""),
                                UserCenterUrl = GetString(uObj, "userCenterUrl", ""),
                                Score = GetNumber(uObj, "score", 0).ToString(),
                                Sort = rank++
                            });
                        }
                    }
                }

                if (detail.Modules.Count > 0 || !string.IsNullOrEmpty(detail.Name))
                {
                    _entryDetailCache[cacheKey] = detail;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_ENTRY_ERR", string.Format("Error loading entry detail (id={0}): {1}", entryId, ex.Message), ex);
            }

            return detail;
        }

        public async Task<List<WikiContributorItem>> GetTopContributorsAsync(int wikiType, int type = 2)
        {
            var list = new List<WikiContributorItem>();
            try
            {
                var parameters = new Dictionary<string, string>
                {
                    { "type", type.ToString() }
                };

                var headers = BuildWikiHeaders(wikiType);
                var json = await KuroApiClient.Instance.PostFormAsync("/wiki/core/score/record/getTop10List", parameters, headers);

                if (json != null && json.ContainsKey("data") && json.GetNamedValue("data").ValueType == JsonValueType.Array)
                {
                    var arr = json.GetNamedArray("data");
                    int rank = 0;
                    foreach (var val in arr)
                    {
                        if (val.ValueType != JsonValueType.Object) continue;
                        var o = val.GetObject();
                        list.Add(new WikiContributorItem
                        {
                            Uid = GetString(o, "uid", ""),
                            UserName = GetString(o, "userName", ""),
                            UserHeadUrl = GetString(o, "userHeadUrl", ""),
                            UserCenterUrl = GetString(o, "userCenterUrl", ""),
                            Score = GetString(o, "score", GetNumber(o, "scoreNumber", 0).ToString("F0")),
                            Sort = rank++
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_CONTRIB_ERR", string.Format("Error loading contributors (type={0}): {1}", wikiType, ex.Message), ex);
            }

            return list;
        }

        public async Task<List<WikiSearchItem>> SearchWikiAsync(int wikiType, string keyword)
        {
            var list = new List<WikiSearchItem>();
            try
            {
                int gameId = wikiType == 9 ? 3 : 2;
                var comp = await KuroForumService.Instance.SearchCompositeAsync(gameId, keyword);
                if (comp != null && comp.Wikis != null)
                {
                    list.AddRange(comp.Wikis);
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("WIKI_SEARCH_ERR", string.Format("Error searching wiki (keyword={0}): {1}", keyword, ex.Message), ex);
            }
            return list;
        }

        private string GetShortcutGlyph(string title)
        {
            if (string.IsNullOrEmpty(title)) return "📖";
            if (title.Contains("机体") || title.Contains("角色")) return "👤";
            if (title.Contains("武器")) return "⚔";
            if (title.Contains("声骸") || title.Contains("意识")) return "💠";
            if (title.Contains("怪物") || title.Contains("敌人")) return "👾";
            if (title.Contains("道具") || title.Contains("物品")) return "🎒";
            if (title.Contains("剧情") || title.Contains("故事")) return "📜";
            if (title.Contains("成就") || title.Contains("任务")) return "🏆";
            if (title.Contains("攻略")) return "📝";
            if (title.Contains("百科")) return "📚";
            if (title.Contains("壁纸") || title.Contains("影音") || title.Contains("影像")) return "🖼";
            if (title.Contains("活动") || title.Contains("资讯")) return "📢";
            if (title.Contains("官网") || title.Contains("网站")) return "🌐";
            return "📌";
        }

        private string GetString(JsonObject obj, string key, string defVal = "")
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return ((long)val.GetNumber()).ToString();
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean().ToString();
            }
            catch { }
            return defVal;
        }

        private double GetNumber(JsonObject obj, string key, double defVal = 0)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Number) return val.GetNumber();
                if (val.ValueType == JsonValueType.String)
                {
                    double d;
                    if (double.TryParse(val.GetString(), out d)) return d;
                }
            }
            catch { }
            return defVal;
        }

        private bool GetBoolean(JsonObject obj, string key, bool defVal = false)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean();
                if (val.ValueType == JsonValueType.String)
                {
                    bool b;
                    if (bool.TryParse(val.GetString(), out b)) return b;
                }
            }
            catch { }
            return defVal;
        }

        private WikiRoleCardInfo ParseRoleComponent(JsonObject cpObj)
        {
            var role = new WikiRoleCardInfo();

            // PNS Role parsing
            if (cpObj.ContainsKey("pnsRole") && cpObj.GetNamedValue("pnsRole").ValueType == JsonValueType.Object)
            {
                var pns = cpObj.GetNamedObject("pnsRole");
                role.Title = GetString(pns, "title", "");
                role.Subtitle = GetString(pns, "subtitle", "");
                role.RobotType = GetString(pns, "robotType", "");
                role.RobotTypeImage = GetString(pns, "robotTypeImage", "");
                role.RoleAvatar = GetString(pns, "roleAvatar", "");
                role.RoleFigure = GetString(pns, "roleFigure", "");
                role.RoleQuotes = GetString(pns, "roleQuotes", GetString(pns, "roleRuotes", ""));
                role.RoleIntroduce = GetString(pns, "roleIntroduce", "");
                role.RoleInitQuality = GetString(pns, "roleInitQuality", "");

                if (pns.ContainsKey("card") && pns.GetNamedValue("card").ValueType == JsonValueType.Array)
                {
                    var cardArr = pns.GetNamedArray("card");

                    // Card 0: Energy, Features, Effect
                    if (cardArr.Count > 0 && cardArr[0].ValueType == JsonValueType.Object)
                    {
                        var c0 = cardArr[0].GetObject();
                        if (c0.ContainsKey("energy") && c0.GetNamedValue("energy").ValueType == JsonValueType.Array)
                        {
                            var eArr = c0.GetNamedArray("energy");
                            foreach (var eVal in eArr)
                            {
                                if (eVal.ValueType != JsonValueType.Object) continue;
                                var eObj = eVal.GetObject();
                                role.EnergyList.Add(new WikiKeyValueItem(GetString(eObj, "key", "能量"), GetString(eObj, "value", "")));
                            }
                        }
                        if (c0.ContainsKey("features") && c0.GetNamedValue("features").ValueType == JsonValueType.Array)
                        {
                            var fArr = c0.GetNamedArray("features");
                            foreach (var fVal in fArr)
                            {
                                if (fVal.ValueType != JsonValueType.Object) continue;
                                var fObj = fVal.GetObject();
                                role.FeatureList.Add(new WikiKeyValueItem(GetString(fObj, "key", "特征"), GetString(fObj, "value", "")));
                            }
                        }
                        if (c0.ContainsKey("effect") && c0.GetNamedValue("effect").ValueType == JsonValueType.Object)
                        {
                            var eff = c0.GetNamedObject("effect");
                            role.EffectIcon = GetString(eff, "icon", "");
                            role.EffectDescription = GetString(eff, "description", "");
                        }
                    }

                    // Card 1: Stats (HP, ATK, DEF, CRIT)
                    if (cardArr.Count > 1 && cardArr[1].ValueType == JsonValueType.Object)
                    {
                        var c1 = cardArr[1].GetObject();
                        string[] statKeys = { "hp", "atk", "def", "crit" };
                        string[] statNames = { "生命", "攻击", "防御", "会心" };
                        for (int i = 0; i < statKeys.Length; i++)
                        {
                            string sk = statKeys[i];
                            if (c1.ContainsKey(sk) && c1.GetNamedValue(sk).ValueType == JsonValueType.Array)
                            {
                                var sArr = c1.GetNamedArray(sk);
                                string minVal = sArr.Count > 0 ? GetStringFromArrayItem(sArr[0]) : "";
                                string maxVal = sArr.Count > 1 ? GetStringFromArrayItem(sArr[1]) : minVal;
                                role.Stats.Add(new WikiRoleStatItem
                                {
                                    Name = statNames[i],
                                    MinValue = minVal,
                                    MaxValue = maxVal
                                });
                            }
                        }
                    }

                    // Card 2: Profile (Name, RobotName, Height, Weight, HeartAge, LoopType, LaunchDay)
                    if (cardArr.Count > 2 && cardArr[2].ValueType == JsonValueType.Object)
                    {
                        var c2 = cardArr[2].GetObject();
                        AddProfileIfNotEmpty(role.ProfileList, "姓名", GetString(c2, "name", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "机体名", GetString(c2, "robotName", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "身高", GetString(c2, "height", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "体重", GetString(c2, "weight", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "心理年龄", GetString(c2, "heartAge", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "循环类型", GetString(c2, "loopType", ""));
                        AddProfileIfNotEmpty(role.ProfileList, "启动日", GetString(c2, "launchDay", ""));
                    }

                    // Card 3: Gears, Weapons, Gifts
                    if (cardArr.Count > 3)
                    {
                        JsonArray gearArr = null;
                        if (cardArr[3].ValueType == JsonValueType.Array) gearArr = cardArr[3].GetArray();
                        else if (cardArr[3].ValueType == JsonValueType.Object && cardArr[3].GetObject().ContainsKey("card") && cardArr[3].GetObject().GetNamedValue("card").ValueType == JsonValueType.Array)
                        {
                            gearArr = cardArr[3].GetObject().GetNamedArray("card");
                        }

                        if (gearArr != null)
                        {
                            foreach (var gVal in gearArr)
                            {
                                if (gVal.ValueType != JsonValueType.Object) continue;
                                var gObj = gVal.GetObject();
                                var gear = new WikiRoleGearItem
                                {
                                    Title = GetString(gObj, "title", ""),
                                    Name = GetString(gObj, "name", ""),
                                    ImgUrl = GetString(gObj, "imgUrl", "")
                                };
                                if (gObj.ContainsKey("linkConfig") && gObj.GetNamedValue("linkConfig").ValueType == JsonValueType.Object)
                                {
                                    var lc = gObj.GetNamedObject("linkConfig");
                                    gear.EntryId = GetString(lc, "entryId", "");
                                    gear.LinkType = (int)GetNumber(lc, "linkType", 1);
                                }
                                role.GearList.Add(gear);
                            }
                        }
                    }
                }
            }

            // MC Role parsing
            if (cpObj.ContainsKey("role") && cpObj.GetNamedValue("role").ValueType == JsonValueType.Object)
            {
                var mcRole = cpObj.GetNamedObject("role");
                role.Title = GetString(mcRole, "title", "");
                role.Subtitle = GetString(mcRole, "subtitle", "");
                role.CampIcon = GetString(mcRole, "campIcon", "");
                role.RoleIntroduce = GetString(mcRole, "roleDescription", GetString(mcRole, "roleIntroduce", ""));

                if (mcRole.ContainsKey("figures") && mcRole.GetNamedValue("figures").ValueType == JsonValueType.Array)
                {
                    var figArr = mcRole.GetNamedArray("figures");
                    if (figArr.Count > 0 && figArr[0].ValueType == JsonValueType.Object)
                    {
                        role.RoleFigure = GetString(figArr[0].GetObject(), "url", "");
                    }
                }

                if (mcRole.ContainsKey("info") && mcRole.GetNamedValue("info").ValueType == JsonValueType.Array)
                {
                    var infoArr = mcRole.GetNamedArray("info");
                    foreach (var iVal in infoArr)
                    {
                        if (iVal.ValueType != JsonValueType.Object) continue;
                        var iObj = iVal.GetObject();
                        string txt = GetString(iObj, "text", "");
                        if (!string.IsNullOrEmpty(txt))
                        {
                            var parts = txt.Split(new char[] { '：', ':' }, 2);
                            if (parts.Length == 2)
                            {
                                role.ProfileList.Add(new WikiKeyValueItem(parts[0].Trim(), parts[1].Trim()));
                            }
                            else
                            {
                                role.ProfileList.Add(new WikiKeyValueItem("信息", txt));
                            }
                        }
                    }
                }
            }

            return role;
        }

        private static void AddProfileIfNotEmpty(List<WikiKeyValueItem> list, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != "0")
            {
                list.Add(new WikiKeyValueItem(key, value));
            }
        }

        #region 意识手册 (Consciousness Manual) 专用解析

        /// <summary>
        /// 解析意识手册条目中的单个组件。
        /// 意识手册条目的组件布局固定为：简介 / 意识立绘 / 突破素材 / 意识故事 / 意识使用心得。
        /// 返回 null 表示该组件不属于意识版式，走通用渲染。
        /// </summary>
        private static ConsciousnessInfo ParseConsciousnessComponent(WikiDetailComponent comp, JsonObject cpObj, bool isConsciousnessEntry)
        {
            if (!isConsciousnessEntry) return null;

            string title = comp.Title ?? "";
            string html = comp.Content ?? "";

            try
            {
                // 简介：基础资料 + 属性（初始/最大）+ 套装技能效果
                if (title.Contains("简介"))
                {
                    var info = new ConsciousnessInfo();
                    ParseConsciousnessIntro(html, info);
                    ParseConsciousnessStats(html, info);
                    ParseConsciousnessSetEffects(html, info);
                    if (info.HasRarity || info.HasStats || info.HasSetEffects || info.HasCoverImage)
                    {
                        return info;
                    }
                    return null;
                }

                // 意识立绘：img 类型 tabs（1/4号位、2/5号位、3/6号位）
                if (title.Contains("立绘"))
                {
                    var illus = ParseConsciousnessIllustrations(cpObj);
                    if (illus != null && illus.Count > 0)
                    {
                        var info = new ConsciousnessInfo();
                        foreach (var i in illus) info.Illustrations.Add(i);
                        info.SelectedIllustration = info.Illustrations[0];
                        return info;
                    }
                    return null;
                }

                // 突破素材：突破1~4 + 材料图标与数量
                if (title.Contains("突破素材") || title.Contains("突破"))
                {
                    var info = new ConsciousnessInfo();
                    ParseConsciousnessBreakMaterials(html, info);
                    if (info.HasBreakStages) return info;
                    return null;
                }

                // 意识故事：故事1 / 故事2 ...
                if (title.Contains("意识故事") || title == "故事")
                {
                    var info = new ConsciousnessInfo();
                    ParseConsciousnessStories(html, info);
                    if (info.HasStories) return info;
                    return null;
                }

                // 意识使用心得：推荐攻略缩略图卡片
                if (title.Contains("意识使用心得") || title.Contains("使用心得"))
                {
                    var info = new ConsciousnessInfo();
                    info.TipsTitle = title;
                    ParseConsciousnessTips(html, info);
                    if (info.HasTips) return info;
                    return null;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("WIKI_CONSCIOUSNESS_PARSE_ERR", string.Format("Error parsing consciousness component '{0}': {1}", title, ex.Message));
            }

            return null;
        }

        /// <summary>
        /// 解析简介表格：稀有度 / 等级上限 / 适用范围 / 意识特性 / 获得方式 + 封面图
        /// </summary>
        private static void ParseConsciousnessIntro(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            string rarityHtml = GrabConsciousnessCell(html, "稀有度");
            if (!string.IsNullOrEmpty(rarityHtml))
            {
                // 统计实心星数量（★），兼容被 span/strong 包裹的情况
                int stars = 0;
                foreach (var ch in WebUtility.HtmlDecode(rarityHtml))
                {
                    if (ch == '★') stars++;
                }
                info.RarityStars = stars;
                if (stars > 0)
                {
                    info.Rarity = new string('★', stars);
                }
                else
                {
                    info.Rarity = CleanHtmlToText(rarityHtml);
                }
            }

            info.LevelCap = CleanHtmlToText(GrabConsciousnessCell(html, "等级上限"));
            info.ApplicableTo = CleanHtmlToText(GrabConsciousnessCell(html, "适用范围"));
            info.Traits = CleanHtmlToText(GrabConsciousnessCell(html, "意识特性"));
            info.Acquire = CleanHtmlToText(GrabConsciousnessCell(html, "获得方式"));

            // 封面：首张独立图片表格中的图片
            var firstTable = Regex.Match(html, @"<table[^>]*>(?<tab>.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (firstTable.Success)
            {
                var imgs = ExtractImageUrlsFromHtml(firstTable.Groups["tab"].Value);
                if (imgs.Count > 0) info.CoverImageUrl = imgs[0];
            }
        }

        /// <summary>
        /// 提取形如 &lt;td&gt;标签&lt;/td&gt;&lt;td&gt;值&lt;/td&gt; 的相邻单元格内容
        /// </summary>
        private static string GrabConsciousnessCell(string html, string label)
        {
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrEmpty(label)) return "";
            try
            {
                string pattern = @"<t[dh][^>]*>\s*(?:<[^>]+>\s*)*" + Regex.Escape(label) +
                                 @"\s*(?:</[^>]+>\s*)*</t[dh]>\s*<td[^>]*>(?<val>.*?)</td>";
                var m = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups["val"].Value;
            }
            catch { }
            return "";
        }

        /// <summary>
        /// 解析「属性（初始/最大）」表格。
        /// 表格布局：第1列为 1/2/3位，第3列为 4/5/6位；行内为 名称/数值 交替。
        /// </summary>
        private static void ParseConsciousnessStats(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            foreach (Match tm in Regex.Matches(html, @"<table[^>]*>(?<tab>.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string tableHtml = tm.Groups["tab"].Value;
                if (!tableHtml.Contains("初始") || !tableHtml.Contains("最大")) continue;

                foreach (Match rm in Regex.Matches(tableHtml, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    var cellMatches = Regex.Matches(rm.Groups["row"].Value, @"<t[dh][^>]*>(?<cell>.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (cellMatches.Count < 4) continue;

                    // 收集 (文本, 图标)
                    var cells = new List<KeyValuePair<string, string>>();
                    foreach (Match cm in cellMatches)
                    {
                        string cellHtml = cm.Groups["cell"].Value;
                        var imgs = ExtractImageUrlsFromHtml(cellHtml);
                        cells.Add(new KeyValuePair<string, string>(
                            CleanHtmlToText(cellHtml),
                            imgs.Count > 0 ? imgs[0] : ""));
                    }

                    // 每两个单元格为一组：名称 + 数值（左列 → 1/2/3位，右列 → 4/5/6位）
                    for (int i = 0; i + 1 < cells.Count; i += 2)
                    {
                        string name = cells[i].Key.Trim();
                        string value = cells[i + 1].Key.Trim();
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value)) continue;
                        if (name == "1/2/3位" || name == "4/5/6位") continue;
                        if (name.Contains("初始") || name.Contains("最大")) continue;

                        var stat = new ConsciousnessStatItem
                        {
                            Name = name,
                            Value = value,
                            IconUrl = cells[i].Value
                        };

                        info.Stats.Add(stat);
                        if (i == 0) info.PrimaryStats.Add(stat);
                        else info.SecondaryStats.Add(stat);
                    }
                }
                break;
            }
        }

        /// <summary>
        /// 解析「套装技能效果」表格：2件套 / 4件套（标题 + 效果正文）
        /// </summary>
        private static void ParseConsciousnessSetEffects(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            foreach (Match tm in Regex.Matches(html, @"<table[^>]*>(?<tab>.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string tableHtml = tm.Groups["tab"].Value;
                if (!tableHtml.Contains("套装")) continue;

                foreach (Match rm in Regex.Matches(tableHtml, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    string rowHtml = rm.Groups["row"].Value;
                    if (rowHtml.Contains("套装技能效果")) continue;

                    foreach (Match cm in Regex.Matches(rowHtml, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                    {
                        string raw = cm.Groups["cell"].Value;
                        // <hr> 把「件套」标题与效果正文分隔开
                        string normalized = Regex.Replace(raw, @"<hr\s*/?>", "\n", RegexOptions.IgnoreCase);
                        string ctext = CleanHtmlToText(normalized);
                        if (string.IsNullOrWhiteSpace(ctext)) continue;

                        var lines = new List<string>();
                        foreach (var l in ctext.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string t = l.Trim();
                            if (!string.IsNullOrEmpty(t)) lines.Add(t);
                        }
                        if (lines.Count == 0) continue;

                        var effect = new ConsciousnessSetEffect();
                        if (lines.Count >= 2)
                        {
                            effect.Title = lines[0];
                            effect.Body = string.Join("\n", lines.GetRange(1, lines.Count - 1).ToArray());
                        }
                        else
                        {
                            effect.Body = lines[0];
                        }
                        info.SetEffects.Add(effect);
                    }
                }
                break;
            }
        }

        /// <summary>
        /// 解析「突破素材」表格：突破1~4 及每阶段材料（图标 + 名称 + 数量）
        /// </summary>
        private static void ParseConsciousnessBreakMaterials(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            foreach (Match rm in Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                var cellMatches = Regex.Matches(rm.Groups["row"].Value, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (cellMatches.Count == 0) continue;

                string stage = CleanHtmlToText(cellMatches[0].Groups["cell"].Value);
                if (string.IsNullOrEmpty(stage) || !stage.StartsWith("突破")) continue;

                var breakStage = new ConsciousnessBreakStage { Stage = stage };

                for (int ci = 1; ci < cellMatches.Count; ci++)
                {
                    string cellHtml = cellMatches[ci].Groups["cell"].Value;

                    // 每个材料形如： <a title="材料名" ...><img src="图标"></a>&times;4
                    var anchorMatches = Regex.Matches(cellHtml,
                        @"<a[^>]*>(?<inner>.*?)</a>(?<tail>(?:\s*(?:&times;|×|\*)\s*[0-9]+)?)",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    int matched = 0;
                    foreach (Match am in anchorMatches)
                    {
                        string inner = am.Groups["inner"].Value;
                        string tail = am.Groups["tail"].Value;
                        var imgs = ExtractImageUrlsFromHtml(inner);

                        string name = "";
                        var titleMatch = Regex.Match(am.Value, @"title=[""'](?<n>[^""']*)[""']", RegexOptions.IgnoreCase);
                        if (titleMatch.Success) name = WebUtility.HtmlDecode(titleMatch.Groups["n"].Value);
                        if (string.IsNullOrEmpty(name))
                        {
                            var altMatch = Regex.Match(inner, @"alt=[""'](?<n>[^""']*)[""']", RegexOptions.IgnoreCase);
                            if (altMatch.Success) name = WebUtility.HtmlDecode(altMatch.Groups["n"].Value);
                        }

                        var cntMatch = Regex.Match(tail, @"(?:&times;|×|\*)\s*(?<c>[0-9]+)", RegexOptions.IgnoreCase);
                        string count = cntMatch.Success ? cntMatch.Groups["c"].Value : "";

                        if (imgs.Count == 0 && string.IsNullOrEmpty(name)) continue;

                        breakStage.Materials.Add(new ConsciousnessMaterialItem
                        {
                            Name = name,
                            IconUrl = imgs.Count > 0 ? imgs[0] : "",
                            Count = count
                        });
                        matched++;
                    }

                    // 无 <a> 结构（例如「无法突破」）兜底为纯文本
                    if (matched == 0)
                    {
                        string plain = CleanHtmlToText(cellHtml);
                        if (!string.IsNullOrWhiteSpace(plain))
                        {
                            breakStage.Materials.Add(new ConsciousnessMaterialItem { Name = plain });
                        }
                    }
                }

                info.BreakStages.Add(breakStage);
            }
        }

        /// <summary>
        /// 解析「意识故事」：每个 wikitable 为一个故事（故事1 / 故事2 ...）
        /// </summary>
        private static void ParseConsciousnessStories(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            foreach (Match tm in Regex.Matches(html, @"<table[^>]*>(?<tab>.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                string tableHtml = tm.Groups["tab"].Value;

                var titleMatch = Regex.Match(tableHtml,
                    @"<t[dh][^>]*>\s*(?:<[^>]+>\s*)*?(?<t>故事[0-9０-９]+)\s*(?:</[^>]+>\s*)*</t[dh]>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!titleMatch.Success) continue;

                string storyTitle = CleanHtmlToText(titleMatch.Groups["t"].Value);
                if (string.IsNullOrEmpty(storyTitle)) storyTitle = "故事";

                var cellMatches = Regex.Matches(tableHtml, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                string body = cellMatches.Count > 0 ? CleanHtmlToText(cellMatches[cellMatches.Count - 1].Groups["cell"].Value) : "";

                info.Stories.Add(new ConsciousnessStory
                {
                    Title = storyTitle,
                    Body = body,
                    IsExpanded = true
                });
            }
        }

        /// <summary>
        /// 解析「意识使用心得」：两行表格（上行为缩略图，下行为攻略标题）
        /// </summary>
        private static void ParseConsciousnessTips(string html, ConsciousnessInfo info)
        {
            if (string.IsNullOrWhiteSpace(html)) return;

            var rowMatches = Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (rowMatches.Count < 2) return;

            var imgRow = Regex.Matches(rowMatches[0].Groups["row"].Value, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var txtRow = Regex.Matches(rowMatches[1].Groups["row"].Value, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int count = Math.Max(imgRow.Count, txtRow.Count);
            for (int i = 0; i < count; i++)
            {
                string icon = "";
                if (i < imgRow.Count)
                {
                    var imgs = ExtractImageUrlsFromHtml(imgRow[i].Groups["cell"].Value);
                    if (imgs.Count > 0) icon = imgs[0];
                }

                string text = i < txtRow.Count ? CleanHtmlToText(txtRow[i].Groups["cell"].Value) : "";

                if (string.IsNullOrEmpty(icon) && string.IsNullOrEmpty(text)) continue;

                info.Tips.Add(new ConsciousnessTipCard
                {
                    IconUrl = icon,
                    Title = text
                });
            }
        }

        /// <summary>
        /// 解析「意识立绘」img 类型 tabs：1/4号位、2/5号位、3/6号位
        /// </summary>
        private static List<ConsciousnessIllustration> ParseConsciousnessIllustrations(JsonObject cpObj)
        {
            var list = new List<ConsciousnessIllustration>();
            if (cpObj == null || !cpObj.ContainsKey("tabs")) return list;
            if (cpObj.GetNamedValue("tabs").ValueType != JsonValueType.Array) return list;

            var tabsArr = cpObj.GetNamedArray("tabs");
            foreach (var tVal in tabsArr)
            {
                if (tVal.ValueType != JsonValueType.Object) continue;
                var tObj = tVal.GetObject();

                var illus = new ConsciousnessIllustration
                {
                    Title = GetStringStatic(tObj, "title", "立绘"),
                    TabIconUrl = GetStringStatic(tObj, "img", "")
                };

                string bodyHtml = GetStringStatic(tObj, "content", "");
                var imgs = ExtractImageUrlsFromHtml(bodyHtml);
                if (imgs.Count > 0) illus.ImageUrl = imgs[0];

                // 画师：先剥标签再取「画师」之后的文字，兼容 <strong>画师</strong>作者 与 <strong>画师 …</strong>作者
                string bodyText = CleanHtmlToText(bodyHtml);
                int painterIdx = bodyText.IndexOf("画师", StringComparison.Ordinal);
                if (painterIdx >= 0)
                {
                    string painter = bodyText.Substring(painterIdx + 2).Trim();
                    painter = painter.TrimStart(':', '：', ' ', '\t', '\n');
                    if (!string.IsNullOrWhiteSpace(painter)) illus.Painter = painter;
                }

                list.Add(illus);
            }

            return list;
        }

        #endregion

        /// <summary>静态版的 GetString：供 static 解析方法（意识手册等）使用。</summary>
        private static string GetStringStatic(JsonObject obj, string key, string defVal = "")
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return ((long)val.GetNumber()).ToString();
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean().ToString();
            }
            catch { }
            return defVal;
        }

        private static string GetStringFromArrayItem(IJsonValue val)
        {
            if (val == null) return "";
            if (val.ValueType == JsonValueType.String) return val.GetString();
            if (val.ValueType == JsonValueType.Number) return ((long)val.GetNumber()).ToString();
            return "";
        }

        /// <summary>
        /// 解析主页模块挂载的子目录节点（递归）。
        /// 主页模块（mainModules）的 more.linkConfig.catalogueId 常指向「分组节点」，
        /// 该节点 getPage 为空；真正的条目在 content.children 里。
        /// </summary>
        /// <summary>
        /// 计算主页核心模块的最终导航目录：
        /// 若 content.children 里存在 active:true 的子目录，则用它（官方前端默认选中项）；
        /// 否则回退到模块自身的 CatalogueId。
        /// </summary>
        private static int ResolveTargetCatalogueId(WikiShortcutItem item)
        {
            if (item == null) return 0;

            if (item.Children != null)
            {
                // 优先 active 标记
                foreach (var c in item.Children)
                {
                    if (c != null && c.Active && c.Id > 0) return c.Id;
                }
            }

            return item.CatalogueId;
        }

        private static void ParseCatalogueChildren(JsonArray arr, List<WikiCatalogueNode> outList, int parentId)
        {
            if (arr == null || outList == null) return;
            foreach (var cVal in arr)
            {
                if (cVal.ValueType != JsonValueType.Object) continue;
                var cObj = cVal.GetObject();

                var node = new WikiCatalogueNode
                {
                    Id = (int)GetNumberStatic(cObj, "id", 0),
                    Key = (int)GetNumberStatic(cObj, "key", 0),
                    Name = GetStringStatic(cObj, "name", ""),
                    ParentId = (int)GetNumberStatic(cObj, "parentId", parentId),
                    Sort = (int)GetNumberStatic(cObj, "sort", 0),
                    // 官方前端用 active:true 标记「默认选中的子目录」。
                    Active = GetBoolStatic(cObj, "active", false)
                };

                if (cObj.ContainsKey("children") && cObj.GetNamedValue("children").ValueType == JsonValueType.Array)
                {
                    ParseCatalogueChildren(cObj.GetNamedArray("children"), node.Children, node.Id);
                }

                outList.Add(node);
            }
        }

        /// <summary>静态版的 GetNumber：供 static 解析方法使用。</summary>
        private static double GetNumberStatic(JsonObject obj, string key, double defVal = 0)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Number) return val.GetNumber();
                if (val.ValueType == JsonValueType.String)
                {
                    double d;
                    if (double.TryParse(val.GetString(), out d)) return d;
                }
            }
            catch { }
            return defVal;
        }

        /// <summary>静态版的 GetBool：供 static 解析方法使用。兼容 true/false 布尔值及 "true"/"1" 字符串。</summary>
        private static bool GetBoolStatic(JsonObject obj, string key, bool defVal = false)
        {
            if (obj == null || !obj.ContainsKey(key)) return defVal;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber() != 0;
                if (val.ValueType == JsonValueType.String)
                {
                    var s = val.GetString();
                    return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
                }
            }
            catch { }
            return defVal;
        }

        private static string CleanHtmlToText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            try
            {
                string text = html;

                // Remove <summary> formatting
                text = Regex.Replace(text, @"<summary[^>]*>(.*?)</summary>", "\n【$1】\n", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                // Replace line-breaks and structural tags
                text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"</p>", "\n\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"</div>", "\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"</t[dh]>", " ", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"<tr[^>]*>", "\n", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"<hr\s*/?>", "\n────────────────────────\n", RegexOptions.IgnoreCase);

                // Strip remaining HTML tags
                text = Regex.Replace(text, @"<[^>]+>", string.Empty);
                text = WebUtility.HtmlDecode(text);

                // Normalize whitespace
                text = Regex.Replace(text, @"[ \t]+", " ");
                text = Regex.Replace(text, @"\n{3,}", "\n\n");
                return text.Trim();
            }
            catch
            {
                return html;
            }
        }

        private static List<string> ExtractImageUrlsFromHtml(string html)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(html)) return list;
            try
            {
                var matches = Regex.Matches(html, @"src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    string url = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
                    {
                        if (!list.Contains(url))
                        {
                            list.Add(url);
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static string ExtractBigIllustration(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            try
            {
                var m = Regex.Match(html, @"<div[^>]*class=[""'][^""']*floatnone[^""']*[""'][^>]*>\s*<img[^>]*src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
            catch { }
            return "";
        }

                        private static List<WikiVoiceRowItem> ParseVoiceRows(string html)
        {
            var list = new List<WikiVoiceRowItem>();
            if (string.IsNullOrWhiteSpace(html)) return list;
            try
            {
                if (!html.Contains("td1") && !html.Contains("构造体加入") && !html.Contains("日常问候") && !html.Contains("信赖提升"))
                {
                    return list;
                }

                var rowMatches = Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match rm in rowMatches)
                {
                    string rowHtml = rm.Groups["row"].Value;
                    var cellMatches = Regex.Matches(rowHtml, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (cellMatches.Count >= 2)
                    {
                        string tag = CleanHtmlToText(cellMatches[0].Groups["cell"].Value);
                        string text = CleanHtmlToText(cellMatches[1].Groups["cell"].Value);
                        if (!string.IsNullOrEmpty(tag) || !string.IsNullOrEmpty(text))
                        {
                            list.Add(new WikiVoiceRowItem
                            {
                                Tag = tag,
                                Text = text,
                                Runs = KuroHtmlPostParser.ParseInlineHtml(cellMatches[1].Groups["cell"].Value, false, null)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static List<WikiSkillRowItem> ParseSkillRows(string html)
        {
            var list = new List<WikiSkillRowItem>();
            if (string.IsNullOrWhiteSpace(html)) return list;
            try
            {
                if (!html.Contains("wikitable") && !html.Contains("残渊哀鸣") && !html.Contains("暮途归斩") && !html.Contains("追迹") && !html.Contains("刀华流转") && !html.Contains("光耀余晖") && !html.Contains("帕夫利琴科") && !html.Contains("时序遍历") && !html.Contains("繁时"))
                {
                    return list;
                }

                var rowMatches = Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match rm in rowMatches)
                {
                    string rowContent = rm.Groups["row"].Value;
                    var cellMatches = Regex.Matches(rowContent, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (cellMatches.Count >= 2)
                    {
                        string cell0 = cellMatches[0].Groups["cell"].Value;
                        string cell1 = cellMatches[1].Groups["cell"].Value;

                        var imgMatch = Regex.Match(cell0, @"src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                        string imgUrl = imgMatch.Success ? imgMatch.Groups[1].Value : "";

                        string name = CleanHtmlToText(cell0);
                        string desc = CleanHtmlToText(cell1);

                        if (!string.IsNullOrEmpty(desc) && (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(imgUrl)))
                        {
                            var colMatch = Regex.Match(cell0, @"color:\s*(#[0-9a-fA-F]{3,8}|rgb\([^)]+\))", RegexOptions.IgnoreCase);
                            string colorHex = colMatch.Success ? colMatch.Groups[1].Value : "";

                            list.Add(new WikiSkillRowItem
                            {
                                Name = name,
                                IconUrl = imgUrl,
                                Description = desc,
                                ColorHex = colorHex,
                                Runs = KuroHtmlPostParser.ParseInlineHtml(cell1, false, null)
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static List<WikiSectionItem> ParseDetailsTree(string html)
        {
            var list = new List<WikiSectionItem>();
            if (string.IsNullOrWhiteSpace(html) || !html.Contains("<details")) return list;

            try
            {
                int searchIdx = 0;
                while (searchIdx < html.Length)
                {
                    int startIdx = html.IndexOf("<details", searchIdx, StringComparison.OrdinalIgnoreCase);
                    if (startIdx < 0) break;

                    int tagEnd = html.IndexOf('>', startIdx);
                    if (tagEnd < 0) break;

                    string openTag = html.Substring(startIdx, tagEnd - startIdx + 1);
                    bool isOpen = true; // Default to open so archive text & demo GIFs are immediately visible

                    // Find matching </details> balancing nested <details>
                    int depth = 1;
                    int cur = tagEnd + 1;
                    int closeIdx = -1;
                    while (cur < html.Length)
                    {
                        int nextOpen = html.IndexOf("<details", cur, StringComparison.OrdinalIgnoreCase);
                        int nextClose = html.IndexOf("</details>", cur, StringComparison.OrdinalIgnoreCase);

                        if (nextClose < 0) break;

                        if (nextOpen >= 0 && nextOpen < nextClose)
                        {
                            depth++;
                            cur = nextOpen + 8;
                        }
                        else
                        {
                            depth--;
                            if (depth == 0)
                            {
                                closeIdx = nextClose;
                                break;
                            }
                            cur = nextClose + 10;
                        }
                    }

                    if (closeIdx < 0)
                    {
                        closeIdx = html.Length;
                    }

                    string blockInner = html.Substring(tagEnd + 1, closeIdx - (tagEnd + 1));
                    searchIdx = closeIdx + 10;

                    // Extract summary
                    string title = "";
                    string subtitle = "";
                    string bodyHtml = blockInner;

                    var sumMatch = Regex.Match(blockInner, @"<summary[^>]*>(?<sum>.*?)</summary>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (sumMatch.Success)
                    {
                        string rawSum = sumMatch.Groups["sum"].Value;
                        title = CleanHtmlToText(rawSum);

                        var subMatch = Regex.Match(rawSum, @"<span[^>]*>(?<sublvl>.*?信赖度.*?解锁.*?)</span>|<small>(?<sublvl>.*?)</small>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (subMatch.Success)
                        {
                            subtitle = CleanHtmlToText(subMatch.Groups["sublvl"].Value);
                            if (!string.IsNullOrEmpty(subtitle) && title.Contains(subtitle))
                            {
                                title = title.Replace(subtitle, "").Trim();
                            }
                        }

                        bodyHtml = blockInner.Substring(sumMatch.Index + sumMatch.Length);
                    }

                    // Check for nested children
                    var children = new List<WikiSectionItem>();
                    string directHtml = bodyHtml;
                    if (bodyHtml.Contains("<details"))
                    {
                        children = ParseDetailsTree(bodyHtml);
                        directHtml = Regex.Replace(bodyHtml, @"<details[^>]*>.*?</details>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    }

                    // Extract image
                    var imgMatch = Regex.Match(directHtml, @"src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    string img = imgMatch.Success ? imgMatch.Groups[1].Value : "";

                    var item = new WikiSectionItem
                    {
                        Title = title,
                        Subtitle = subtitle,
                        Content = CleanHtmlToText(directHtml),
                        Runs = KuroHtmlPostParser.ParseInlineHtml(directHtml, false, null),
                        ImageUrl = img,
                        IsExpanded = isOpen
                    };

                    if (children.Count > 0)
                    {
                        item.Children = children;
                    }

                    list.Add(item);
                }
            }
            catch { }

            return list;
        }

        private static List<WikiRotationLine> ParseRotationFlow(string html, string tabTitle)
        {
            var lines = new List<WikiRotationLine>();
            if (string.IsNullOrWhiteSpace(html)) return lines;

            try
            {
                string decoded = WebUtility.HtmlDecode(html)
                    .Replace("&rarr;", "→")
                    .Replace("&ldquo;", "“")
                    .Replace("&rdquo;", "”")
                    .Replace("&middot;", "·")
                    .Replace("&nbsp;", " ");

                // Extract all table rows, cells, or paragraphs
                var rowMatches = Regex.Matches(decoded, @"<(?:td|p|div|tr)[^>]*>(?<content>.*?)</(?:td|p|div|tr)>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var segments = new List<string>();

                if (rowMatches.Count > 0)
                {
                    foreach (Match rm in rowMatches)
                    {
                        string c = rm.Groups["content"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(c))
                        {
                            segments.Add(c);
                        }
                    }
                }
                else
                {
                    segments.Add(decoded);
                }

                var noteSegments = new List<string>();
                var seenNotes = new HashSet<string>();

                foreach (var seg in segments)
                {
                    string clean = CleanHtmlToText(seg);
                    if (string.IsNullOrWhiteSpace(clean)) continue;

                    // If it contains rotation step arrows
                    if (clean.Contains("→") || clean.Contains("->"))
                    {
                        var line = new WikiRotationLine();
                        line.RawText = clean;
                        line.Runs = KuroHtmlPostParser.ParseInlineHtml(seg, false, null);

                        string flowBody = clean;
                        if (clean.Contains("："))
                        {
                            int colonIdx = clean.IndexOf("：");
                            if (colonIdx > 0 && colonIdx <= 15)
                            {
                                line.Title = clean.Substring(0, colonIdx + 1).Trim();
                                flowBody = clean.Substring(colonIdx + 1).Trim();
                            }
                        }
                        else if (clean.Contains(":"))
                        {
                            int colonIdx = clean.IndexOf(":");
                            if (colonIdx > 0 && colonIdx <= 15)
                            {
                                line.Title = clean.Substring(0, colonIdx + 1).Trim();
                                flowBody = clean.Substring(colonIdx + 1).Trim();
                            }
                        }

                        if (string.IsNullOrEmpty(line.Title))
                        {
                            if (clean.StartsWith("①") || clean.StartsWith("②") || clean.StartsWith("③") || clean.StartsWith("1.") || clean.StartsWith("2."))
                            {
                                line.Title = "连招流程";
                            }
                            else
                            {
                                line.Title = string.IsNullOrEmpty(tabTitle) ? "输出流程" : tabTitle;
                            }
                        }

                        // Split flowBody into steps
                        string[] rawSteps = Regex.Split(flowBody, @"→|->");
                        int stepIdx = 1;
                        foreach (var st in rawSteps)
                        {
                            string stepTrim = st.Trim();
                            if (string.IsNullOrEmpty(stepTrim)) continue;

                            var stepObj = new WikiRotationStep
                            {
                                StepIndex = stepIdx++,
                                StepText = stepTrim,
                                IsKeyAction = stepTrim.Contains("长按") || stepTrim.Contains("必杀") || stepTrim.Contains("核心")
                            };
                            line.Steps.Add(stepObj);
                        }

                        if (line.Steps.Count > 0)
                        {
                            lines.Add(line);
                        }
                    }
                    else
                    {
                        if (clean.Length > 2 && clean != "输出流程" && clean != "操作说明" && clean != "技能流程")
                        {
                            if (!seenNotes.Contains(clean))
                            {
                                seenNotes.Add(clean);
                                noteSegments.Add(seg);
                            }
                        }
                    }
                }

                // Consolidate all explanation / note segments into ONE unified note block at the bottom
                if (noteSegments.Count > 0)
                {
                    string mergedNotesHtml = string.Join("<br/><br/>", noteSegments);
                    var combinedNoteLine = new WikiRotationLine
                    {
                        Title = "流程要点与说明",
                        RawText = CleanHtmlToText(mergedNotesHtml),
                        Runs = KuroHtmlPostParser.ParseInlineHtml(mergedNotesHtml, false, null)
                    };
                    lines.Add(combinedNoteLine);
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("WIKI_ROTATION_ERR", "Error parsing rotation flow: " + ex.Message);
            }

            return lines;
        }

        private static string ExtractEntryIdFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            try
            {
                var m = Regex.Match(html, @"/item/(?<id>[0-9]{10,25})", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups["id"].Value;
            }
            catch { }
            return "";
        }

        private static List<WikiEquipRecommendGroup> ParseTeamTab(string html, string tabTitle, string compTitle = "")
        {
            var groups = new List<WikiEquipRecommendGroup>();
            if (string.IsNullOrWhiteSpace(html)) return groups;

            try
            {
                var teamGroup = new WikiEquipRecommendGroup
                {
                    Title = string.IsNullOrEmpty(tabTitle) ? "推荐出战阵容" : ("推荐阵容 - " + tabTitle)
                };

                var rowMatches = Regex.Matches(html, @"<tr[^>]*>(?<row>.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tipSegments = new List<string>();
                WikiEquipRecommendItem currMember = null;

                foreach (Match rm in rowMatches)
                {
                    string rowHtml = rm.Groups["row"].Value;
                    var cellMatches = Regex.Matches(rowHtml, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (cellMatches.Count == 0) continue;

                    string rowClean = CleanHtmlToText(rowHtml);
                    if (rowClean.StartsWith("角色") && (rowClean.Contains("定位") || rowClean.Contains("养成")))
                    {
                        continue;
                    }

                    string c0 = cellMatches[0].Groups["cell"].Value;
                    string c0Clean = CleanHtmlToText(c0);
                    var c0Imgs = ExtractImageUrlsFromHtml(c0);
                    string c0Eid = ExtractEntryIdFromHtml(c0);

                    // Check if summary / tips row
                    if (cellMatches.Count == 1 || rowHtml.ToLower().Contains("colspan"))
                    {
                        if (c0Clean.Contains("配队简析") || c0Clean.Contains("配队思路") || c0Clean.Contains("免费角色") || 
                            c0Clean.Contains("获取途径") || c0Clean.Contains("仅供参考") || c0Clean.Contains("养成推荐") ||
                            c0Clean.Contains("注意") || c0Clean.Contains("点击"))
                        {
                            tipSegments.Add(c0);
                            continue;
                        }
                    }

                    // Character Member row (3 cells or has icon + numbered prefix)
                    if (cellMatches.Count >= 3 || (c0Imgs.Count > 0 && (Regex.IsMatch(c0Clean, @"^[①②③123]") || cellMatches.Count >= 2)))
                    {
                        string name = c0Clean;
                        string roleText = cellMatches.Count > 1 ? CleanHtmlToText(cellMatches[1].Groups["cell"].Value) : "";
                        string buildText = cellMatches.Count > 2 ? CleanHtmlToText(cellMatches[2].Groups["cell"].Value) : "";

                        string cat = "出战队员";
                        if (name.Contains("①") || name.StartsWith("1") || name.Contains("队长") || roleText.Contains("队长"))
                            cat = "队长位";
                        else if (roleText.Contains("增幅") || roleText.Contains("辅助") || roleText.Contains("治疗"))
                            cat = "增幅/辅助";
                        else if (roleText.Contains("装甲") || roleText.Contains("减抗") || roleText.Contains("破盾"))
                            cat = "装甲/减抗";
                        else if (roleText.Contains("输出") || roleText.Contains("进攻") || roleText.Contains("主C"))
                            cat = "主输出";

                        var noteParts = new List<string>();
                        if (!string.IsNullOrEmpty(roleText)) noteParts.Add("定位: " + roleText);
                        if (!string.IsNullOrEmpty(buildText)) noteParts.Add("推荐: " + buildText);

                        currMember = new WikiEquipRecommendItem
                        {
                            Name = name,
                            IconUrl = c0Imgs.Count > 0 ? c0Imgs[0] : "",
                            EntryId = c0Eid,
                            Category = cat,
                            Note = string.Join("  |  ", noteParts),
                            SubInfo = ""
                        };
                        teamGroup.Items.Add(currMember);
                        continue;
                    }

                    // Equipment/Weapons/Pets/Leap under current member
                    if (currMember != null && cellMatches.Count == 1)
                    {
                        string eqClean = CleanHtmlToText(c0);
                        if (!string.IsNullOrWhiteSpace(eqClean))
                        {
                            if (string.IsNullOrEmpty(currMember.SubInfo))
                            {
                                currMember.SubInfo = eqClean;
                            }
                            else
                            {
                                currMember.SubInfo += "  •  " + eqClean;
                            }
                        }
                        continue;
                    }
                }

                if (tipSegments.Count > 0)
                {
                    string mergedHtml = string.Join("<br/><br/>", tipSegments);
                    teamGroup.Tip = CleanHtmlToText(mergedHtml);
                    teamGroup.TipRuns = KuroHtmlPostParser.ParseInlineHtml(mergedHtml, false, null);
                }

                if (teamGroup.Items.Count > 0 || !string.IsNullOrEmpty(teamGroup.Tip))
                {
                    groups.Add(teamGroup);
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("WIKI_TEAM_PARSE_ERR", "Error parsing team tab: " + ex.Message);
            }

            return groups;
        }

        private static List<WikiEquipRecommendGroup> ParseConsciousnessTab(string html, string tabTitle, string compTitle = "")
        {
            var groups = new List<WikiEquipRecommendGroup>();
            if (string.IsNullOrWhiteSpace(html)) return groups;

            try
            {
                var mainGroup = new WikiEquipRecommendGroup { Title = "推荐意识套装" };
                var resGroup = new WikiEquipRecommendGroup { Title = "意识共鸣与谐振" };

                var cellMatches = Regex.Matches(html, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tipSegments = new List<string>();

                foreach (Match cm in cellMatches)
                {
                    string cell = cm.Groups["cell"].Value;
                    string clean = CleanHtmlToText(cell);
                    if (string.IsNullOrWhiteSpace(clean)) continue;

                    var imgs = ExtractImageUrlsFromHtml(cell);
                    string firstImg = imgs.Count > 0 ? imgs[0] : "";

                    // Table headers to skip
                    if (clean == "装备武器" || clean == "武器共鸣推荐" || clean == "装备辅助机" || 
                        clean == "装备意识" || clean == "意识搭配推荐" || clean == "无谐振")
                    {
                        continue;
                    }

                    // Tips, guidance, and analysis text
                    if (clean.Contains("简析") || clean.Contains("搭配只需") || clean.Contains("无需特意") || 
                        clean.Contains("暂替") || clean.Contains("任意15") || clean.Contains("摆放") || 
                        clean.Contains("参考") || clean.Length > 40)
                    {
                        tipSegments.Add(cell);
                        continue;
                    }

                    // Consciousness Set item (e.g. 帕夫利琴科*4 or 辰积原*2)
                    var setMatch = Regex.Match(clean, @"(?<name>[\u4e00-\u9fa5A-Za-z0-9·]+)\s*\*\s*(?<cnt>[0-9]+)");
                    if (setMatch.Success)
                    {
                        string name = setMatch.Groups["name"].Value;
                        string cnt = "*" + setMatch.Groups["cnt"].Value;
                        string eid = ExtractEntryIdFromHtml(cell);

                        mainGroup.Items.Add(new WikiEquipRecommendItem
                        {
                            Name = name,
                            Count = cnt,
                            IconUrl = firstImg,
                            Category = "意识套装",
                            EntryId = eid,
                            Runs = KuroHtmlPostParser.ParseInlineHtml(cell, false, null)
                        });
                        continue;
                    }

                    // Resonance and Harmonization items
                    if (clean.Contains("谐振") || clean.Contains("上位") || clean.Contains("下位") || 
                        clean.Contains("共鸣") || clean.Contains("攻击") || clean.Contains("必杀") || 
                        clean.Contains("核心") || clean.Contains("职业技") || clean.Contains("大招"))
                    {
                        var lines = clean.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        int imgIdx = 0;
                        foreach (var l in lines)
                        {
                            string lineTxt = l.Trim();
                            if (string.IsNullOrEmpty(lineTxt)) continue;
                            string icon = imgIdx < imgs.Count ? imgs[imgIdx] : firstImg;
                            imgIdx++;

                            resGroup.Items.Add(new WikiEquipRecommendItem
                            {
                                Name = lineTxt,
                                IconUrl = icon,
                                Category = "意识共鸣",
                                Runs = KuroHtmlPostParser.ParseInlineHtml(lineTxt, false, null)
                            });
                        }
                        continue;
                    }
                }

                if (tipSegments.Count > 0)
                {
                    string mergedHtml = string.Join("<br/>", tipSegments);
                    mainGroup.Tip = CleanHtmlToText(mergedHtml);
                    mainGroup.TipRuns = KuroHtmlPostParser.ParseInlineHtml(mergedHtml, false, null);
                }

                if (mainGroup.Items.Count > 0)
                {
                    groups.Add(mainGroup);
                }
                if (resGroup.Items.Count > 0)
                {
                    groups.Add(resGroup);
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("WIKI_CONSCIOUSNESS_PARSE_ERR", "Error parsing consciousness tab: " + ex.Message);
            }

            return groups;
        }

        private static List<WikiEquipRecommendGroup> ParseEquipOrPetTab(string html, string tabTitle, string compTitle = "")
        {
            var groups = new List<WikiEquipRecommendGroup>();
            if (string.IsNullOrWhiteSpace(html)) return groups;

            try
            {
                string combined = ((compTitle ?? "") + " " + (tabTitle ?? "")).ToLower();
                bool isPet = combined.Contains("辅助机") || combined.Contains("宠物") || combined.Contains("pet");

                var mainGroup = new WikiEquipRecommendGroup { Title = isPet ? "推荐装备辅助机" : "推荐装备武器" };
                var subGroup = new WikiEquipRecommendGroup { Title = isPet ? "技能升级顺序推荐" : "武器共鸣推荐" };

                var cellMatches = Regex.Matches(html, @"<td[^>]*>(?<cell>.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var tipSegments = new List<string>();

                foreach (Match cm in cellMatches)
                {
                    string cell = cm.Groups["cell"].Value;
                    string clean = CleanHtmlToText(cell);
                    if (string.IsNullOrWhiteSpace(clean)) continue;

                    var imgs = ExtractImageUrlsFromHtml(cell);
                    string firstImg = imgs.Count > 0 ? imgs[0] : "";

                    // 1. Table Headers to skip
                    if (clean == "装备武器" || clean == "武器共鸣推荐" || clean == "装备辅助机" || 
                        clean == "技能升级顺序推荐" || clean == "推荐辅助机" || clean == "武器装备" || 
                        clean == "推荐武器" || clean == "装备")
                    {
                        continue;
                    }

                    // 2. Analysis / Tips / Guidance notes (简析、参考、规划等)
                    if (clean.Contains("简析") || clean.Contains("参考") || clean.Contains("建议") || 
                        clean.Contains("替代") || clean.Contains("说明") || clean.Contains("规划抽取") || 
                        clean.Contains("仅供参考") || clean.Contains("总览") || clean.Contains("前往") || 
                        clean.Length > 35)
                    {
                        tipSegments.Add(cell);
                        continue;
                    }

                    // 3. Numbered Skill or Resonance items (e.g. ①光耀余晖, ②寒渊烬影, ③凝霜, 死线计时)
                    bool isSkillOrResonance = Regex.IsMatch(clean, @"^[①②③④⑤⑥⑦⑧⑨⑩\d\.]") || 
                                             clean.Contains("①") || clean.Contains("②") || clean.Contains("③") ||
                                             clean.Contains("④") || clean.Contains("⑤") || clean.Contains("⑥") ||
                                             clean.Contains("共鸣") || clean.Contains("死线") || clean.Contains("超算") || 
                                             clean.Contains("光耀") || clean.Contains("零落");

                    if (isSkillOrResonance)
                    {
                        var lines = clean.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        int imgIdx = 0;
                        foreach (var l in lines)
                        {
                            string lineTxt = l.Trim();
                            if (string.IsNullOrEmpty(lineTxt)) continue;

                            string icon = imgIdx < imgs.Count ? imgs[imgIdx] : firstImg;
                            imgIdx++;

                            subGroup.Items.Add(new WikiEquipRecommendItem
                            {
                                Name = lineTxt,
                                IconUrl = icon,
                                Category = isPet ? "技能升级" : "武器共鸣",
                                Runs = KuroHtmlPostParser.ParseInlineHtml(lineTxt, false, null)
                            });
                        }
                        continue;
                    }

                    // 4. Main Weapon / Pet Item (e.g. 无光暮途, 异身)
                    if (!string.IsNullOrEmpty(firstImg) || !string.IsNullOrEmpty(clean))
                    {
                        string eid = ExtractEntryIdFromHtml(cell);
                        string cat = isPet ? "推荐辅助机" : "推荐武器";
                        mainGroup.Items.Add(new WikiEquipRecommendItem
                        {
                            Name = clean,
                            IconUrl = firstImg,
                            Category = cat,
                            EntryId = eid,
                            Runs = KuroHtmlPostParser.ParseInlineHtml(cell, false, null)
                        });
                    }
                }

                if (tipSegments.Count > 0)
                {
                    string mergedHtml = string.Join("<br/>", tipSegments);
                    mainGroup.Tip = CleanHtmlToText(mergedHtml);
                    mainGroup.TipRuns = KuroHtmlPostParser.ParseInlineHtml(mergedHtml, false, null);
                }

                if (mainGroup.Items.Count > 0)
                {
                    groups.Add(mainGroup);
                }
                if (subGroup.Items.Count > 0)
                {
                    groups.Add(subGroup);
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("WIKI_EQUIP_PARSE_ERR", "Error parsing equip tab: " + ex.Message);
            }

            return groups;
        }
    }
}
