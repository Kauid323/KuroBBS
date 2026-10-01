using System.Collections.ObjectModel;

namespace KuroBBS.Models
{
    public class HaruRoleSummary
    {
        public bool Show { get; set; }
        public int CharacterCount { get; set; }
        public string RoleAllScore { get; set; }
        public int Achievement { get; set; }
        public int ScoreTitleCount { get; set; }
        public string FashionProcess { get; set; }
        public string StoryProcess { get; set; }
        public int GrandTotalLoginNum { get; set; }
        public int SgTreasureBoxCount { get; set; }
        public int SgTreasureBoxTotalCount { get; set; }
    }

    public class HaruAccountInfo
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public string ServerName { get; set; }
        public string HeadIconUrl { get; set; }
        public int Level { get; set; }
        public int Rank { get; set; }
        public string LevelText { get { return "Lv." + Level; } }
        public string RankText { get { return Rank > 0 ? "全服排名 " + Rank : ""; } }
    }

    public class HaruDailyItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
        public string ProgressText { get; set; }
    }

    public class HaruCharacterInfo
    {
        public int BodyId { get; set; }
        public string BodyName { get; set; }
        public string IconUrl { get; set; }
        public string Element { get; set; }
        public string Effect { get; set; }
        public int Quality { get; set; }
        public string Grade { get; set; }
        public string RoleRank { get; set; }
        public int FightAbility { get; set; }
        public int Level { get; set; }
        public int Priority { get; set; }
        public int WeaponType { get; set; }

        public string LevelText { get { return "Lv." + Level; } }
        public string FightAbilityText { get { return FightAbility > 0 ? ("战力 " + FightAbility) : ""; } }
        public string RankText { get { return !string.IsNullOrEmpty(Grade) ? Grade : (!string.IsNullOrEmpty(RoleRank) ? RoleRank : ""); } }
        public string ElementEffectText
        {
            get
            {
                if (!string.IsNullOrEmpty(Element) && !string.IsNullOrEmpty(Effect))
                    return Element + " · " + Effect;
                return Element ?? Effect ?? "";
            }
        }
    }

    public class HaruCharacterBody
    {
        public int BodyId { get; set; }
        public string RoleName { get; set; }
        public string BodyName { get; set; }
        public int CareerId { get; set; }
        public string Career { get; set; }
        public int IsNewRole { get; set; }
        public string IconUrl { get; set; }
        public string ImgUrl { get; set; }
        public string Element { get; set; }
        public string ElementDetail { get; set; }
        public string Effect { get; set; }
        public string WikiLink { get; set; }
        public string RoleRank { get; set; }
        public int Priority { get; set; }
        public int WeaponType { get; set; }

        public string FullTitle
        {
            get
            {
                if (string.IsNullOrEmpty(RoleName) || RoleName == BodyName) return BodyName;
                return RoleName + " · " + BodyName;
            }
        }
    }

    public class HaruWeaponSuit
    {
        public int SuitId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public string SkillDescriptionTwo { get; set; }
        public string SkillDescriptionFour { get; set; }
        public string SkillDescriptionSix { get; set; }
    }

    public class HaruWeaponItem
    {
        public int WeaponId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public string SkillName { get; set; }
        public string SkillDescription { get; set; }
    }

    public class HaruWeaponDetail
    {
        public HaruWeaponItem Weapon { get; set; }
        public int Quality { get; set; }
        public int OverRunLevel { get; set; }
        public HaruWeaponSuit Suit { get; set; }

        public int WeaponId { get { return Weapon != null ? Weapon.WeaponId : 0; } set { if (Weapon == null) Weapon = new HaruWeaponItem(); Weapon.WeaponId = value; } }
        public string Name { get { return Weapon != null ? Weapon.Name : ""; } set { if (Weapon == null) Weapon = new HaruWeaponItem(); Weapon.Name = value; } }
        public string IconUrl { get { return Weapon != null ? Weapon.IconUrl : ""; } set { if (Weapon == null) Weapon = new HaruWeaponItem(); Weapon.IconUrl = value; } }
        public string SkillName { get { return Weapon != null ? Weapon.SkillName : ""; } set { if (Weapon == null) Weapon = new HaruWeaponItem(); Weapon.SkillName = value; } }
        public string SkillDescription { get { return Weapon != null ? Weapon.SkillDescription : ""; } set { if (Weapon == null) Weapon = new HaruWeaponItem(); Weapon.SkillDescription = value; } }

        public HaruWeaponDetail()
        {
            Weapon = new HaruWeaponItem();
        }
    }

    public class HaruPartnerSkill
    {
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public int Level { get; set; }
        public string Description { get; set; }
        public string LevelText { get { return Level > 0 ? ("Lv." + Level) : ""; } }
    }

    public class HaruPartnerItem
    {
        public int PartnerId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public int Grade { get; set; }
        public string GradeStr { get; set; }
    }

    public class HaruPartnerDetail
    {
        public HaruPartnerItem Partner { get; set; }
        public int Level { get; set; }
        public int BreakThrough { get; set; }
        public string Grade { get; set; }
        public int Quality { get; set; }
        public ObservableCollection<HaruPartnerSkill> SkillList { get; set; }

        public int PartnerId { get { return Partner != null ? Partner.PartnerId : 0; } set { if (Partner == null) Partner = new HaruPartnerItem(); Partner.PartnerId = value; } }
        public string Name { get { return Partner != null ? Partner.Name : ""; } set { if (Partner == null) Partner = new HaruPartnerItem(); Partner.Name = value; } }
        public string IconUrl { get { return Partner != null ? Partner.IconUrl : ""; } set { if (Partner == null) Partner = new HaruPartnerItem(); Partner.IconUrl = value; } }
        public string LevelText { get { return "Lv." + Level; } }

        public HaruPartnerDetail()
        {
            Partner = new HaruPartnerItem();
            SkillList = new ObservableCollection<HaruPartnerSkill>();
        }
    }

    public class HaruChipSuit
    {
        public int SuitId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
        public int Num { get; set; }
        public string DescriptionTwo { get; set; }
        public string DescriptionFour { get; set; }
        public string DescriptionSix { get; set; }

        public string NumText { get { return Num + " 件套"; } }
    }

    public class HaruChipResonance
    {
        public int Site { get; set; }
        public string ChipName { get; set; }
        public string ChipIconUrl { get; set; }
        public bool Defend { get; set; }
        public string SuperSlotIconUrl { get; set; }
        public bool SuperAwake { get; set; }
        public string SuperDescription { get; set; }
        public string SubSlotIconUrl { get; set; }
        public bool SubAwake { get; set; }
        public string SubDescription { get; set; }

        public string SiteText { get { return Site + " 号位"; } }
    }

    public class HaruCharacterDetail
    {
        public HaruCharacterBody Body { get; set; }
        public int Quality { get; set; }
        public string Grade { get; set; }
        public int FightAbility { get; set; }
        public HaruWeaponDetail WeaponInfo { get; set; }
        public HaruPartnerDetail Partner { get; set; }
        public ObservableCollection<HaruChipSuit> ChipSuitList { get; set; }
        public ObservableCollection<HaruChipResonance> ChipResonanceList { get; set; }
        public string ChipExDamage { get; set; }
        public bool Show { get; set; }

        public string FightAbilityText { get { return FightAbility > 0 ? FightAbility.ToString() : "-"; } }

        public HaruCharacterDetail()
        {
            Body = new HaruCharacterBody();
            WeaponInfo = new HaruWeaponDetail();
            Partner = new HaruPartnerDetail();
            ChipSuitList = new ObservableCollection<HaruChipSuit>();
            ChipResonanceList = new ObservableCollection<HaruChipResonance>();
        }
    }

    public class CharacterDetailNavParams
    {
        public string ServerId { get; set; }
        public string RoleId { get; set; }
        public int CharacterId { get; set; }
        public string BodyName { get; set; }
        public string IconUrl { get; set; }
        public string RoleName { get; set; }
    }

    public class HaruAllRoleInfo
    {
        public int BodyId { get; set; }
        public string RoleName { get; set; }
        public string BodyName { get; set; }
        public string IconUrl { get; set; }
        public string Element { get; set; }
        public int Priority { get; set; }
        public int WeaponType { get; set; }
        public string RoleRank { get; set; }
        public string Effect { get; set; }
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(BodyName) || BodyName == RoleName) return RoleName;
                return RoleName + " · " + BodyName;
            }
        }
        public string PriorityText { get { return Priority > 0 ? "优先级 " + Priority : ""; } }
        public string WeaponTypeText { get { return WeaponType > 0 ? "武器 " + WeaponType : ""; } }
    }

    public class HaruFashionInfo
    {
        public string CharacterRate { get; set; }
        public string WeaponRate { get; set; }
        public int CharacterFashionCount { get; set; }
        public int WeaponFashionCount { get; set; }
        public string CharacterRateText { get { return (CharacterRate ?? "0") + "%"; } }
        public string WeaponRateText { get { return (WeaponRate ?? "0") + "%"; } }
        public string CharacterFashionCountText { get { return "已收集 " + CharacterFashionCount + " 件"; } }
        public string WeaponFashionCountText { get { return "已收集 " + WeaponFashionCount + " 件"; } }
    }

    public class HaruDetailData
    {
        public HaruRoleSummary Summary { get; set; }
        public HaruAccountInfo Account { get; set; }
        public ObservableCollection<HaruDailyItem> DailyItems { get; set; }
        public ObservableCollection<HaruCharacterInfo> Characters { get; set; }
        public HaruFashionInfo Fashion { get; set; }
    }

    public class HaruTeamMember
    {
        public int CharacterId { get; set; }
        public string CharacterName { get; set; }
        public string VersionName { get; set; }
        public string HeadUrl { get; set; }
        public int Grade { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(CharacterName) || CharacterName == VersionName) return VersionName ?? "";
                return CharacterName + " · " + VersionName;
            }
        }

        public string GradeText
        {
            get
            {
                switch (Grade)
                {
                    case 1: return "B";
                    case 2: return "A";
                    case 3: return "S";
                    case 4: return "SS";
                    case 5: return "SSS";
                    case 6: return "SSS+";
                    default: return Grade > 0 ? "阶位 " + Grade : "";
                }
            }
        }

        public bool HasGrade { get { return !string.IsNullOrEmpty(GradeText); } }
    }

    public class HaruTeamTag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public int Weights { get; set; }
    }

    public class HaruTeamItem
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string RoleName { get; set; }
        public string RoleHead { get; set; }
        public int? LinkNum { get; set; }
        public int? CollectNum { get; set; }
        public int? ViewNum { get; set; }
        public string CreateDay { get; set; }
        public int IsHot { get; set; }
        public int Recommend { get; set; }
        public ObservableCollection<HaruTeamTag> Tags { get; set; }
        public ObservableCollection<HaruTeamMember> CharacterInfoList { get; set; }

        public string AuthorDisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(RoleName)) return RoleName;
                return "推荐方案";
            }
        }

        public string ViewNumText { get { return ViewNum.HasValue && ViewNum.Value > 0 ? ViewNum.Value.ToString() : "-"; } }
        public string LinkNumText { get { return LinkNum.HasValue && LinkNum.Value > 0 ? LinkNum.Value.ToString() : "0"; } }
        public string CollectNumText { get { return CollectNum.HasValue && CollectNum.Value > 0 ? CollectNum.Value.ToString() : "0"; } }
        public bool HasTags { get { return Tags != null && Tags.Count > 0; } }

        public HaruTeamMember Member1 { get { return CharacterInfoList != null && CharacterInfoList.Count > 0 ? CharacterInfoList[0] : null; } }
        public HaruTeamMember Member2 { get { return CharacterInfoList != null && CharacterInfoList.Count > 1 ? CharacterInfoList[1] : null; } }
        public HaruTeamMember Member3 { get { return CharacterInfoList != null && CharacterInfoList.Count > 2 ? CharacterInfoList[2] : null; } }

        public HaruTeamItem()
        {
            Tags = new ObservableCollection<HaruTeamTag>();
            CharacterInfoList = new ObservableCollection<HaruTeamMember>();
        }
    }

    public class HaruTeamFilterCharacter
    {
        public int CharacterId { get; set; }
        public string CharacterName { get; set; }
        public string VersionName { get; set; }
        public string HeadUrl { get; set; }
        public string CharacterUrl { get; set; }
        public string AttributeCode { get; set; }
        public string AttributeName { get; set; }
        public string CharacterCareerId { get; set; }
        public int WeaponType { get; set; }
        public string Effect { get; set; }
        public int Priority { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(CharacterName) || CharacterName == VersionName) return VersionName ?? "";
                return CharacterName + " · " + VersionName;
            }
        }
    }

    public class HaruTeamNavParams
    {
        public string ServerId { get; set; }
        public string RoleId { get; set; }
        public string UserId { get; set; }
        public int CharacterId { get; set; }
        public string CharacterName { get; set; }
        public string BodyName { get; set; }
    }

    public class HaruTeamListResult
    {
        public ObservableCollection<HaruTeamItem> List { get; set; }
        public int Total { get; set; }
        public bool NewList { get; set; }

        public HaruTeamListResult()
        {
            List = new ObservableCollection<HaruTeamItem>();
        }
    }
}
