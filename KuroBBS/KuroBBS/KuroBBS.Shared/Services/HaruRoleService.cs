using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Data.Json;
using KuroBBS.Helpers;
using KuroBBS.Models;

namespace KuroBBS.Services
{
    public class HaruRoleService
    {
        private static HaruRoleService _instance;
        public static HaruRoleService Instance
        {
            get
            {
                if (_instance == null) _instance = new HaruRoleService();
                return _instance;
            }
        }

        public async Task<HaruDetailData> GetDetailAsync(string serverId, string roleId)
        {
            var result = new HaruDetailData
            {
                Summary = new HaruRoleSummary(),
                Account = new HaruAccountInfo { RoleId = roleId },
                DailyItems = new ObservableCollection<HaruDailyItem>(),
                Characters = new ObservableCollection<HaruCharacterInfo>(),
                Fashion = new HaruFashionInfo()
            };

            var key = new Dictionary<string, string> { { "serverId", serverId ?? "" }, { "roleId", roleId ?? "" } };
            var dailyKey = new Dictionary<string, string> { { "type", "2" }, { "serverId", serverId ?? "" }, { "roleId", roleId ?? "" } };

            Task<JsonObject> baseTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/baseData", key);
            Task<JsonObject> accountTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/accountData", key);
            Task<JsonObject> dailyTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/dailyData", dailyKey);
            Task<JsonObject> roleTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/roleIndex", key);
            Task<JsonObject> characterFashionTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/characterFashion", key);
            Task<JsonObject> weaponFashionTask = KuroApiClient.Instance.PostFormAsync("/haru/roleBox/weaponFashion", key);

            await Task.WhenAll(baseTask, accountTask, dailyTask, roleTask, characterFashionTask, weaponFashionTask);

            ParseBase(result, baseTask.Result);
            ParseAccount(result, accountTask.Result);
            ParseDaily(result, dailyTask.Result);
            ParseCharacters(result, roleTask.Result);
            ParseFashion(result, characterFashionTask.Result, weaponFashionTask.Result);
            return result;
        }

        public async Task<ObservableCollection<HaruCharacterInfo>> GetRoleIndexAsync(string serverId, string roleId)
        {
            var roles = new ObservableCollection<HaruCharacterInfo>();
            var key = new Dictionary<string, string>
            {
                { "serverId", serverId ?? "" },
                { "roleId", roleId ?? "" }
            };

            JsonObject json = await KuroApiClient.Instance.PostFormAsync("/haru/roleBox/roleIndex", key);
            JsonObject data = GetObject(json, "data");
            JsonArray list = GetArray(data, "characterList");
            if (list == null) return roles;

            for (uint i = 0; i < list.Count; i++)
            {
                try
                {
                    JsonObject item = list.GetObjectAt(i);
                    roles.Add(new HaruCharacterInfo
                    {
                        BodyId = GetInt(item, "bodyId"),
                        BodyName = GetString(item, "bodyName", "未知构造体"),
                        IconUrl = GetString(item, "iconUrl"),
                        Element = GetString(item, "element"),
                        Effect = GetString(item, "effect"),
                        Quality = GetInt(item, "quality"),
                        Grade = GetString(item, "grade", GetString(item, "roleRank")),
                        RoleRank = GetString(item, "roleRank"),
                        FightAbility = GetInt(item, "fightAbility"),
                        Level = GetInt(item, "level"),
                        Priority = GetInt(item, "priority"),
                        WeaponType = GetInt(item, "weaponType")
                    });
                }
                catch { }
            }
            return roles;
        }

        public async Task<List<HaruTeamFilterCharacter>> GetTeamFilterCharactersAsync(string serverId, string roleId, string userId)
        {
            var list = new List<HaruTeamFilterCharacter>();
            var key = new Dictionary<string, string>
            {
                { "gameId", "2" },
                { "roleId", roleId ?? "" },
                { "serverId", serverId ?? "" },
                { "userId", !string.IsNullOrEmpty(userId) ? userId : SettingsHelper.UserId },
                { "source", "1" }
            };

            JsonObject json = await KuroApiClient.Instance.PostFormAsync("/haru/team/initCharacter", key);
            JsonArray dataArray = GetArray(json, "data");
            if (dataArray == null) return list;

            for (uint i = 0; i < dataArray.Count; i++)
            {
                try
                {
                    JsonObject item = dataArray.GetObjectAt(i);
                    list.Add(new HaruTeamFilterCharacter
                    {
                        CharacterId = GetInt(item, "characterId"),
                        CharacterName = GetString(item, "characterName"),
                        VersionName = GetString(item, "versionName"),
                        HeadUrl = GetString(item, "headUrl"),
                        CharacterUrl = GetString(item, "characterUrl"),
                        AttributeCode = GetString(item, "attributeCode"),
                        AttributeName = GetString(item, "attributeName"),
                        CharacterCareerId = GetString(item, "characterCareerId"),
                        WeaponType = GetInt(item, "weaponType"),
                        Effect = GetString(item, "effect"),
                        Priority = GetInt(item, "priority")
                    });
                }
                catch { }
            }
            return list;
        }

        public async Task<HaruTeamListResult> GetTeamListAsync(string serverId, string roleId, string userId, int characterId, int page, int sortType = 0, int type = 0, int dataRange = 1, string tagsJson = null)
        {
            var result = new HaruTeamListResult();
            var key = new Dictionary<string, string>
            {
                { "sortType", sortType.ToString() },
                { "type", type.ToString() },
                { "dataRange", dataRange.ToString() },
                { "characterId", characterId.ToString() },
                { "page", page.ToString() },
                { "gameId", "2" },
                { "roleId", roleId ?? "" },
                { "serverId", serverId ?? "" },
                { "userId", !string.IsNullOrEmpty(userId) ? userId : SettingsHelper.UserId },
                { "source", "1" }
            };

            if (!string.IsNullOrEmpty(tagsJson))
            {
                key["tags"] = tagsJson;
            }

            JsonObject json = await KuroApiClient.Instance.PostFormAsync("/haru/team/list", key);
            JsonObject dataObj = GetObject(json, "data");
            if (dataObj == null) return result;

            result.Total = GetInt(dataObj, "total");
            result.NewList = GetBool(dataObj, "newList");

            JsonArray listArray = GetArray(dataObj, "list");
            if (listArray == null) return result;

            for (uint i = 0; i < listArray.Count; i++)
            {
                try
                {
                    JsonObject item = listArray.GetObjectAt(i);
                    var team = new HaruTeamItem
                    {
                        Id = (long)GetInt(item, "id"),
                        Title = GetString(item, "title"),
                        RoleName = GetString(item, "roleName"),
                        RoleHead = GetString(item, "roleHead"),
                        LinkNum = item.ContainsKey("linkNum") && item.GetNamedValue("linkNum").ValueType == JsonValueType.Number ? (int?)GetInt(item, "linkNum") : null,
                        CollectNum = item.ContainsKey("collectNum") && item.GetNamedValue("collectNum").ValueType == JsonValueType.Number ? (int?)GetInt(item, "collectNum") : null,
                        ViewNum = item.ContainsKey("viewNum") && item.GetNamedValue("viewNum").ValueType == JsonValueType.Number ? (int?)GetInt(item, "viewNum") : null,
                        CreateDay = GetString(item, "createDay"),
                        IsHot = GetInt(item, "isHot"),
                        Recommend = GetInt(item, "recommend")
                    };

                    JsonArray tagsArray = GetArray(item, "tags");
                    if (tagsArray != null)
                    {
                        for (uint t = 0; t < tagsArray.Count; t++)
                        {
                            try
                            {
                                JsonObject tagObj = tagsArray.GetObjectAt(t);
                                team.Tags.Add(new HaruTeamTag
                                {
                                    Id = GetInt(tagObj, "id"),
                                    Name = GetString(tagObj, "name"),
                                    Type = GetInt(tagObj, "type"),
                                    Weights = GetInt(tagObj, "weights")
                                });
                            }
                            catch { }
                        }
                    }

                    JsonArray membersArray = GetArray(item, "characterInfoList");
                    if (membersArray != null)
                    {
                        for (uint m = 0; m < membersArray.Count; m++)
                        {
                            try
                            {
                                JsonObject memberObj = membersArray.GetObjectAt(m);
                                team.CharacterInfoList.Add(new HaruTeamMember
                                {
                                    CharacterId = GetInt(memberObj, "characterId"),
                                    CharacterName = GetString(memberObj, "characterName"),
                                    VersionName = GetString(memberObj, "versionName"),
                                    Grade = GetInt(memberObj, "grade"),
                                    HeadUrl = GetString(memberObj, "headUrl")
                                });
                            }
                            catch { }
                        }
                    }

                    result.List.Add(team);
                }
                catch { }
            }

            return result;
        }

        public async Task<HaruCharacterDetail> GetCharacterDetailAsync(string serverId, string roleId, int characterId)
        {
            var detail = new HaruCharacterDetail();
            var key = new Dictionary<string, string>
            {
                { "serverId", serverId ?? "" },
                { "roleId", roleId ?? "" },
                { "characterId", characterId.ToString() }
            };

            JsonObject json = await KuroApiClient.Instance.PostFormAsync("/haru/roleBox/roleDetail", key);
            JsonObject data = GetObject(json, "data");
            if (data == null) return detail;

            detail.Show = GetBool(data, "show");
            JsonObject charObj = GetObject(data, "character");
            if (charObj == null) return detail;

            detail.Quality = GetInt(charObj, "quality");
            detail.Grade = GetString(charObj, "grade");
            detail.FightAbility = GetInt(charObj, "fightAbility");
            detail.ChipExDamage = GetString(charObj, "chipExDamage", "0%");

            // Body
            JsonObject bodyObj = GetObject(charObj, "body");
            if (bodyObj != null)
            {
                detail.Body.BodyId = GetInt(bodyObj, "bodyId");
                detail.Body.RoleName = GetString(bodyObj, "roleName");
                detail.Body.BodyName = GetString(bodyObj, "bodyName");
                detail.Body.CareerId = GetInt(bodyObj, "careerId");
                detail.Body.Career = GetString(bodyObj, "career");
                detail.Body.IsNewRole = GetInt(bodyObj, "isNewRole");
                detail.Body.IconUrl = GetString(bodyObj, "iconUrl");
                detail.Body.ImgUrl = GetString(bodyObj, "imgUrl");
                detail.Body.Element = GetString(bodyObj, "element");
                detail.Body.ElementDetail = GetString(bodyObj, "elementDetail");
                detail.Body.Effect = GetString(bodyObj, "effect");
                detail.Body.WikiLink = GetString(bodyObj, "wikiLink");
                detail.Body.RoleRank = GetString(bodyObj, "roleRank");
                detail.Body.Priority = GetInt(bodyObj, "priority");
                detail.Body.WeaponType = GetInt(bodyObj, "weaponType");
            }

            // WeaponInfo
            JsonObject weaponInfoObj = GetObject(charObj, "weaponInfo");
            if (weaponInfoObj != null)
            {
                detail.WeaponInfo.Quality = GetInt(weaponInfoObj, "quality");
                detail.WeaponInfo.OverRunLevel = GetInt(weaponInfoObj, "overRunLevel");
                JsonObject weaponObj = GetObject(weaponInfoObj, "weapon");
                if (weaponObj != null)
                {
                    detail.WeaponInfo.WeaponId = GetInt(weaponObj, "weaponId");
                    detail.WeaponInfo.Name = GetString(weaponObj, "name");
                    detail.WeaponInfo.IconUrl = GetString(weaponObj, "iconUrl");
                    detail.WeaponInfo.SkillName = GetString(weaponObj, "skillName");
                    detail.WeaponInfo.SkillDescription = GetString(weaponObj, "skillDescription");
                }
                JsonObject suitObj = GetObject(weaponInfoObj, "suit");
                if (suitObj != null)
                {
                    detail.WeaponInfo.Suit = new HaruWeaponSuit
                    {
                        SuitId = GetInt(suitObj, "suitId"),
                        Name = GetString(suitObj, "name"),
                        IconUrl = GetString(suitObj, "iconUrl"),
                        SkillDescriptionTwo = GetString(suitObj, "skillDescriptionTwo"),
                        SkillDescriptionFour = GetString(suitObj, "skillDescriptionFour"),
                        SkillDescriptionSix = GetString(suitObj, "skillDescriptionSix")
                    };
                }
            }

            // Partner
            JsonObject partnerObj = GetObject(charObj, "partner");
            if (partnerObj != null)
            {
                detail.Partner.Level = GetInt(partnerObj, "level");
                detail.Partner.BreakThrough = GetInt(partnerObj, "breakThrough");
                detail.Partner.Grade = GetString(partnerObj, "grade");
                detail.Partner.Quality = GetInt(partnerObj, "quality");
                JsonObject pInfo = GetObject(partnerObj, "partner");
                if (pInfo != null)
                {
                    detail.Partner.PartnerId = GetInt(pInfo, "partnerId");
                    detail.Partner.Name = GetString(pInfo, "name");
                    detail.Partner.IconUrl = GetString(pInfo, "iconUrl");
                }
                JsonArray skills = GetArray(partnerObj, "skillList");
                if (skills != null)
                {
                    for (uint s = 0; s < skills.Count; s++)
                    {
                        try
                        {
                            JsonObject sk = skills.GetObjectAt(s);
                            detail.Partner.SkillList.Add(new HaruPartnerSkill
                            {
                                Name = GetString(sk, "name"),
                                IconUrl = GetString(sk, "iconUrl"),
                                Level = GetInt(sk, "level"),
                                Description = GetString(sk, "description")
                            });
                        }
                        catch { }
                    }
                }
            }

            // ChipSuitList
            JsonArray chipSuits = GetArray(charObj, "chipSuitList");
            if (chipSuits != null)
            {
                for (uint c = 0; c < chipSuits.Count; c++)
                {
                    try
                    {
                        JsonObject cs = chipSuits.GetObjectAt(c);
                        detail.ChipSuitList.Add(new HaruChipSuit
                        {
                            SuitId = GetInt(cs, "suitId"),
                            Name = GetString(cs, "name"),
                            IconUrl = GetString(cs, "iconUrl"),
                            Num = GetInt(cs, "num"),
                            DescriptionTwo = GetString(cs, "descriptionTwo"),
                            DescriptionFour = GetString(cs, "descriptionFour"),
                            DescriptionSix = GetString(cs, "descriptionSix")
                        });
                    }
                    catch { }
                }
            }

            // ChipResonanceList
            JsonArray chipRes = GetArray(charObj, "chipResonanceList");
            if (chipRes != null)
            {
                for (uint r = 0; r < chipRes.Count; r++)
                {
                    try
                    {
                        JsonObject cr = chipRes.GetObjectAt(r);
                        detail.ChipResonanceList.Add(new HaruChipResonance
                        {
                            Site = GetInt(cr, "site"),
                            ChipName = GetString(cr, "chipName"),
                            ChipIconUrl = GetString(cr, "chipIconUrl"),
                            Defend = GetBool(cr, "defend"),
                            SuperSlotIconUrl = GetString(cr, "superSlotIconUrl"),
                            SuperAwake = GetBool(cr, "superAwake"),
                            SuperDescription = GetString(cr, "superDescription"),
                            SubSlotIconUrl = GetString(cr, "subSlotIconUrl"),
                            SubAwake = GetBool(cr, "subAwake"),
                            SubDescription = GetString(cr, "subDescription")
                        });
                    }
                    catch { }
                }
            }

            return detail;
        }

        public async Task<ObservableCollection<HaruAllRoleInfo>> GetAllRolesAsync(string serverId, string roleId)
        {
            var roles = new ObservableCollection<HaruAllRoleInfo>();
            var key = new Dictionary<string, string>
            {
                { "serverId", serverId ?? "" },
                { "roleId", roleId ?? "" }
            };

            JsonObject json = await KuroApiClient.Instance.PostFormAsync("/haru/roleBox/allRoleList", key);
            JsonArray list = GetArray(json, "data");
            if (list == null) return roles;

            for (uint i = 0; i < list.Count; i++)
            {
                try
                {
                    JsonObject item = list.GetObjectAt(i);
                    roles.Add(new HaruAllRoleInfo
                    {
                        BodyId = GetInt(item, "bodyId"),
                        RoleName = GetString(item, "roleName"),
                        BodyName = GetString(item, "bodyName"),
                        IconUrl = GetString(item, "iconUrl"),
                        Element = GetString(item, "element"),
                        Priority = GetInt(item, "priority"),
                        WeaponType = GetInt(item, "weaponType"),
                        RoleRank = GetString(item, "roleRank"),
                        Effect = GetString(item, "effect")
                    });
                }
                catch
                {
                }
            }
            return roles;
        }

        private static void ParseBase(HaruDetailData result, JsonObject json)
        {
            JsonObject data = GetObject(json, "data");
            if (data == null) return;
            result.Summary.Show = GetBool(data, "show");
            result.Summary.CharacterCount = GetInt(data, "characterCount");
            result.Summary.RoleAllScore = GetString(data, "roleAllScore");
            result.Summary.Achievement = GetInt(data, "achievement");
            result.Summary.ScoreTitleCount = GetInt(data, "scoreTitleCount");
            result.Summary.FashionProcess = GetString(data, "fashionProcess");
            result.Summary.StoryProcess = GetString(data, "storyProcess");
            result.Summary.GrandTotalLoginNum = GetInt(data, "grandTotalLoginNum");
            result.Summary.SgTreasureBoxCount = GetInt(data, "sgTreasureBoxCount");
            result.Summary.SgTreasureBoxTotalCount = GetInt(data, "sgTreasureBoxTotalCount");
        }

        private static void ParseAccount(HaruDetailData result, JsonObject json)
        {
            JsonObject data = GetObject(json, "data");
            if (data == null) return;
            result.Account.RoleId = GetString(data, "roleId", result.Account.RoleId);
            result.Account.RoleName = GetString(data, "roleName");
            result.Account.ServerName = GetString(data, "serverName");
            result.Account.HeadIconUrl = GetString(data, "headIconUrl");
            result.Account.Level = GetInt(data, "level");
            result.Account.Rank = GetInt(data, "rank");
        }

        private static void ParseDaily(HaruDetailData result, JsonObject json)
        {
            JsonObject data = GetObject(json, "data");
            if (data == null) return;
            AddDailyItem(result, GetObject(data, "actionData"), "体力");
            AddDailyItem(result, GetObject(data, "dormData"), "宿舍");
            AddDailyItem(result, GetObject(data, "activeData"), "每日活跃");

            JsonArray bosses = GetArray(data, "bossData");
            if (bosses == null) return;
            for (uint i = 0; i < bosses.Count; i++)
            {
                try
                {
                    JsonObject boss = bosses.GetObjectAt(i);
                    AddDailyItem(result, boss, GetString(boss, "name", "周期活动"));
                }
                catch { }
            }
        }

        private static void AddDailyItem(HaruDetailData result, JsonObject data, string fallbackName)
        {
            if (data == null) return;
            string value = GetString(data, "value");
            int current = GetInt(data, "cur");
            int total = GetInt(data, "total");
            result.DailyItems.Add(new HaruDailyItem
            {
                Name = GetString(data, "name", fallbackName),
                Value = value,
                Current = current,
                Total = total,
                ProgressText = !string.IsNullOrEmpty(value) ? value : (current + "/" + total)
            });
        }

        private static void ParseCharacters(HaruDetailData result, JsonObject json)
        {
            JsonObject data = GetObject(json, "data");
            JsonArray list = GetArray(data, "characterList");
            if (list == null) return;
            for (uint i = 0; i < list.Count; i++)
            {
                try
                {
                    JsonObject item = list.GetObjectAt(i);
                    result.Characters.Add(new HaruCharacterInfo
                    {
                        BodyId = GetInt(item, "bodyId"),
                        BodyName = GetString(item, "bodyName", "未知构造体"),
                        IconUrl = GetString(item, "iconUrl"),
                        Element = GetString(item, "element"),
                        Effect = GetString(item, "effect"),
                        Quality = GetInt(item, "quality"),
                        Grade = GetString(item, "grade", GetString(item, "roleRank")),
                        RoleRank = GetString(item, "roleRank"),
                        FightAbility = GetInt(item, "fightAbility"),
                        Level = GetInt(item, "level"),
                        Priority = GetInt(item, "priority"),
                        WeaponType = GetInt(item, "weaponType")
                    });
                }
                catch { }
            }
        }

        private static void ParseFashion(HaruDetailData result, JsonObject characterJson, JsonObject weaponJson)
        {
            JsonObject character = GetObject(characterJson, "data");
            JsonObject weapon = GetObject(weaponJson, "data");
            result.Fashion.CharacterRate = GetString(character, "rate", "0");
            result.Fashion.WeaponRate = GetString(weapon, "rate", "0");
            result.Fashion.CharacterFashionCount = GetArray(character, "fashionList") != null ? (int)GetArray(character, "fashionList").Count : 0;
            result.Fashion.WeaponFashionCount = GetArray(weapon, "fashionList") != null ? (int)GetArray(weapon, "fashionList").Count : 0;
        }

        private static JsonObject GetObject(JsonObject parent, string name)
        {
            if (parent == null || !parent.ContainsKey(name) || parent.GetNamedValue(name).ValueType != JsonValueType.Object) return null;
            return parent.GetNamedObject(name);
        }

        private static JsonArray GetArray(JsonObject parent, string name)
        {
            if (parent == null || !parent.ContainsKey(name) || parent.GetNamedValue(name).ValueType != JsonValueType.Array) return null;
            return parent.GetNamedArray(name);
        }

        private static string GetString(JsonObject obj, string name, string fallback = "")
        {
            if (obj == null || !obj.ContainsKey(name)) return fallback;
            JsonValue value = obj.GetNamedValue(name);
            if (value.ValueType == JsonValueType.String) return value.GetString();
            if (value.ValueType == JsonValueType.Number) return value.GetNumber().ToString();
            if (value.ValueType == JsonValueType.Boolean) return value.GetBoolean().ToString();
            return fallback;
        }

        private static int GetInt(JsonObject obj, string name)
        {
            double number = 0;
            if (obj != null && obj.ContainsKey(name) && obj.GetNamedValue(name).ValueType == JsonValueType.Number)
            {
                number = obj.GetNamedNumber(name);
            }
            else
            {
                int parsed;
                if (int.TryParse(GetString(obj, name), out parsed)) number = parsed;
            }
            return (int)number;
        }

        private static bool GetBool(JsonObject obj, string name)
        {
            return obj != null && obj.ContainsKey(name) && obj.GetNamedValue(name).ValueType == JsonValueType.Boolean && obj.GetNamedBoolean(name);
        }
    }
}
