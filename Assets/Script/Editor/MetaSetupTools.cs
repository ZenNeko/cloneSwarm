#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CloneSwarm.Meta;
using UnityEditor;
using UnityEngine;

/// <summary>
/// สร้าง asset ตั้งต้นของระบบ Meta ให้ครบในคลิกเดียว
/// Tools &gt; Clone Swarm &gt; Meta &gt; ...
///
/// รันซ้ำได้ปลอดภัย — ถ้า asset มีอยู่แล้วจะ **ไม่ทับ** ค่าที่ปรับไว้
/// (ยกเว้นการเติม reference ที่ยังว่างใน MetaDatabase)
/// </summary>
public static class MetaSetupTools
{
    const string TalentDir     = "Assets/Script/Data/Talent";
    const string AugmentDir    = "Assets/Script/Data/Augment/Definitions";
    const string ResourcesDir  = "Assets/Resources";
    const string DatabasePath  = ResourcesDir + "/MetaDatabase.asset";

    [MenuItem("Tools/Clone Swarm/Meta/Create Default Talents + Database")]
    public static void CreateDefaults()
    {
        EnsureDir(TalentDir);
        EnsureDir(ResourcesDir);

        // มี talent ครบทุก StatType ที่ PlayerStatManager อ่านค่าจริง (12 ตัว)
        // + 2 ตัวที่ไม่ใช่ stat (Gold Finder / Second Chance)
        //
        // ไม่ทำ GainGold / HealOnFullBuild — สองตัวนั้นเป็นโบนัส "ยิงครั้งเดียวตอนบิลด์ตัน"
        // ที่ทำงานใน ApplyStatLocal เท่านั้น ไม่ใช่ stat สะสม → เป็น talent แล้วจะไม่มีผลอะไรเลย
        // สีไอคอนบอกกลุ่มแบบไม่ต้องมีหัวข้อคั่นใน grid
        const string Attack   = "#F0864B";   // ส้ม — สายทำดาเมจ
        const string Reach    = "#5AA9F0";   // ฟ้า — สายระยะ/ความเร็ว/EXP
        const string Vital    = "#4ADE80";   // เขียว — สายเลือด
        const string Treasure = "#F0B429";   // ทอง — สายเก็บของ/ทอง/เกราะ
        const string Rare     = "#C084FC";   // ม่วง — ของหายากที่ซื้อได้ครั้งเดียว

        var talents = new List<TalentData>
        {
            // ── Combat ────────────────────────────────────────────────────
            MakeStatTalent("talent_attack", "Attack Power", StatType.Damage, 0.03f,
                new[] { 100, 200, 400, 800, 1600 }, TalentCategory.Combat, 0, Attack,
                "เพิ่มพลังโจมตีถาวรของอาวุธทุกชิ้น +3% ต่อเลเวล"),
            MakeStatTalent("talent_haste", "Haste Speed", StatType.AbilityHaste, 5f,
                new[] { 150, 300, 600, 1200, 2400 }, TalentCategory.Combat, 1, Attack,
                "ลดคูลดาวน์อาวุธและสกิล +5 Haste ต่อเลเวล"),
            MakeStatTalent("talent_crit", "Critical Edge", StatType.CriticalChance, 0.04f,
                new[] { 150, 300, 600, 1200, 2400 }, TalentCategory.Combat, 2, Attack,
                "โอกาสติดคริติคอลถาวร +4% ต่อเลเวล"),
            MakeStatTalent("talent_area", "Wide Impact", StatType.AreaSize, 0.04f,
                new[] { 120, 240, 480, 960, 1920 }, TalentCategory.Combat, 3, Reach,
                "ขยายรัศมี AoE และระยะโจมตี +4% ต่อเลเวล"),
            MakeStatTalent("talent_duration", "Lingering Force", StatType.Duration, 0.05f,
                new[] { 100, 200, 400, 800, 1600 }, TalentCategory.Combat, 4, Reach,
                "ยืดอายุและระยะบินของกระสุน +5% ต่อเลเวล"),
            MakeStatTalent("talent_projectile", "Extra Shot", StatType.ProjectileCount, 1f,
                new[] { 8000 }, TalentCategory.Combat, 5, Rare,
                "เพิ่มกระสุนอีก 1 นัดให้ทุกอาวุธ (ซื้อได้ครั้งเดียว)"),

            // ── Survival ──────────────────────────────────────────────────
            MakeStatTalent("talent_health", "Max Health", StatType.MaxHealth, 50f,
                new[] { 100, 200, 400, 800, 1600 }, TalentCategory.Survival, 10, Vital,
                "เพิ่มพลังชีวิตสูงสุดถาวร +50 ต่อเลเวล"),
            MakeStatTalent("talent_armor", "Iron Skin", StatType.Armor, 3f,
                new[] { 120, 240, 480, 960, 1920 }, TalentCategory.Survival, 11, Treasure,
                "ลดความเสียหายที่ได้รับแบบคงที่ +3 ต่อเลเวล"),
            MakeStatTalent("talent_regen", "Regeneration", StatType.HealthRegen, 0.5f,
                new[] { 120, 240, 480, 960, 1920 }, TalentCategory.Survival, 12, Vital,
                "ฟื้นฟูพลังชีวิต +0.5 ต่อวินาที ต่อเลเวล"),

            // ── Utility ───────────────────────────────────────────────────
            MakeStatTalent("talent_movespeed", "Swift Boots", StatType.MoveSpeed, 0.02f,
                new[] { 150, 300, 600, 1200, 2400 }, TalentCategory.Utility, 20, Reach,
                "เพิ่มความเร็วในการเคลื่อนที่ +2% ต่อเลเวล"),
            MakeStatTalent("talent_magnet", "Magnet Boost", StatType.PickupRadius, 0.10f,
                new[] { 80, 160, 320, 640, 1280 }, TalentCategory.Utility, 21, Treasure,
                "ขยายรัศมีดูด EXP และไอเทม +10% ต่อเลเวล"),
            MakeStatTalent("talent_exp", "Fast Learner", StatType.ExpBonus, 0.05f,
                new[] { 200, 400, 800, 1600, 3200 }, TalentCategory.Utility, 22, Reach,
                "ได้รับ EXP เพิ่มขึ้นทุกแหล่ง +5% ต่อเลเวล"),

            // ── Meta (ไม่ใช่ stat) ────────────────────────────────────────
            MakeSpecialTalent("talent_gold", "Gold Finder", TalentEffectMode.GoldFind, 0.10f,
                new[] { 200, 400, 800, 1600, 3200 }, 30, Treasure,
                "ได้ทองท้ายเกมเพิ่มขึ้น +10% ต่อเลเวล"),
            MakeSpecialTalent("talent_revive", "Second Chance", TalentEffectMode.SecondChance, 0f,
                new[] { 5000 }, 31, Rare,
                "ชุบชีวิตอัตโนมัติ 1 ครั้งต่อเกม ที่ HP 30%"),
        };

        // ── Database ──────────────────────────────────────────────────────
        var db = AssetDatabase.LoadAssetAtPath<MetaDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<MetaDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            Debug.Log($"[MetaSetup] สร้าง {DatabasePath}");
        }

        foreach (var t in talents)
            if (!db.talents.Contains(t)) db.talents.Add(t);

        // เติม CharacterData ทุกตัวที่มีในโปรเจกต์ (ถ้ายังไม่มีในลิสต์)
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (cd != null && !db.characters.Contains(cd)) db.characters.Add(cd);
        }

        // เติม Augment ทุกตัวที่มีในโปรเจกต์
        foreach (var guid in AssetDatabase.FindAssets("t:AugmentData"))
        {
            var a = AssetDatabase.LoadAssetAtPath<AugmentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && !db.augments.Contains(a)) db.augments.Add(a);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = db;
        Debug.Log($"[MetaSetup] ✅ เสร็จ — talents {db.talents.Count} · characters {db.characters.Count} · augments {db.augments.Count}");
    }

    [MenuItem("Tools/Clone Swarm/Meta/Create Sample Augments")]
    public static void CreateSampleAugments()
    {
        EnsureDir(AugmentDir);

        // ── Stat augments ────────────────────────────────────────────────
        MakeStatAugment("aug_glass_cannon", "Glass Cannon", AugmentRarity.Gold, 40f,
            "ดาเมจ +45% แต่ HP สูงสุด -60",
            new (StatType, float)[] { (StatType.Damage, 0.45f), (StatType.MaxHealth, -60f) });

        MakeStatAugment("aug_juggernaut", "Juggernaut", AugmentRarity.Silver, 100f,
            "HP สูงสุด +150 · เกราะ +10 · ความเร็ว -5%",
            new (StatType, float)[] { (StatType.MaxHealth, 150f), (StatType.Armor, 10f), (StatType.MoveSpeed, -0.05f) });

        MakeStatAugment("aug_overdrive", "Overdrive", AugmentRarity.Gold, 40f,
            "Ability Haste +30 · ระยะ/ขนาด AoE +15%",
            new (StatType, float)[] { (StatType.AbilityHaste, 30f), (StatType.AreaSize, 0.15f) });

        MakeStatAugment("aug_sharpshooter", "Sharpshooter", AugmentRarity.Silver, 100f,
            "โอกาสคริติคอล +20% · ดาเมจ +10%",
            new (StatType, float)[] { (StatType.CriticalChance, 0.20f), (StatType.Damage, 0.10f) });

        MakeStatAugment("aug_swarm_lord", "Swarm Lord", AugmentRarity.Prismatic, 12f,
            "กระสุนทุกอาวุธ +2 · ดาเมจ -10%",
            new (StatType, float)[] { (StatType.ProjectileCount, 2f), (StatType.Damage, -0.10f) });

        // ── Trigger augments ─────────────────────────────────────────────
        MakeTriggerAugment("aug_bloodthirst", "Bloodthirst", AugmentRarity.Silver, 100f,
            "ทุกๆ 20 ศัตรูที่ทีมกำจัด — ฟื้น HP 5%",
            AugmentTrigger.EveryNKills, 20f, AugmentEffect.HealPercent, 0.05f, 0f);

        MakeTriggerAugment("aug_adrenaline", "Adrenaline", AugmentRarity.Silver, 100f,
            "เมื่อโดนโจมตี — ความเร็ว +35% นาน 2 วิ (คูลดาวน์ 5 วิ)",
            AugmentTrigger.OnTakeDamage, 1f, AugmentEffect.TempMoveSpeed, 0.35f, 2f, cooldown: 5f);

        MakeTriggerAugment("aug_second_wind", "Second Wind", AugmentRarity.Gold, 40f,
            "ทุก 15 วินาที — ได้โล่ 60",
            AugmentTrigger.Periodic, 15f, AugmentEffect.Shield, 60f, 0f);

        MakeTriggerAugment("aug_momentum", "Momentum", AugmentRarity.Prismatic, 12f,
            "ทุกครั้งที่เลเวลอัป — ดาเมจ +60% นาน 12 วิ",
            AugmentTrigger.OnLevelUp, 1f, AugmentEffect.TempDamage, 0.60f, 12f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MetaSetup] ✅ สร้าง sample augments แล้ว — รัน 'Create Default Talents + Database' อีกครั้งเพื่อลงทะเบียนเข้า MetaDatabase");
    }

    [MenuItem("Tools/Clone Swarm/Meta/Open Save File Folder")]
    public static void OpenSaveFolder() => EditorUtility.RevealInFinder(SaveManager.SavePath);

    [MenuItem("Tools/Clone Swarm/Meta/Delete Save File")]
    public static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("ลบไฟล์เซฟ",
            $"ลบ {SaveManager.SavePath} และไฟล์ backup?", "ลบเลย", "ยกเลิก")) return;

        if (File.Exists(SaveManager.SavePath))   File.Delete(SaveManager.SavePath);
        if (File.Exists(SaveManager.BackupPath)) File.Delete(SaveManager.BackupPath);
        Debug.Log("[MetaSetup] ลบไฟล์เซฟแล้ว");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════
    static void EnsureDir(string dir)
    {
        if (Directory.Exists(dir)) return;
        Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
    }

    static TalentData MakeStatTalent(string id, string name, StatType stat, float valuePerLevel,
                                     int[] costs, TalentCategory category, int sortOrder,
                                     string tintHex, string desc)
        => MakeTalent(id, name, TalentEffectMode.Stat, stat, valuePerLevel, costs, category,
                      sortOrder, tintHex, desc);

    static TalentData MakeSpecialTalent(string id, string name, TalentEffectMode mode, float valuePerLevel,
                                        int[] costs, int sortOrder, string tintHex, string desc)
        => MakeTalent(id, name, mode, StatType.Damage, valuePerLevel, costs, TalentCategory.Meta,
                      sortOrder, tintHex, desc);

    /// <summary>
    /// สร้างถ้ายังไม่มี · ถ้ามีแล้วจะ **ซ่อมเฉพาะฟิลด์ identity** (mode / statType / category)
    /// แต่ไม่แตะ valuePerLevel / costPerLevel ที่ designer อาจปรับไว้
    ///
    /// การซ่อมนี้จำเป็นเพราะ asset ที่สร้างก่อนเปลี่ยนมาใช้ StatType จะมี mode/statType
    /// เป็นค่า default ทั้งหมด (= Damage) ซึ่งผิดสำหรับเกือบทุกตัว
    ///
    /// tintColor ซ่อมเฉพาะตอนที่ยังเป็นสีขาว (= ไม่เคยตั้ง) — ถ้า designer เลือกสีเองแล้วจะไม่แตะ
    /// </summary>
    static TalentData MakeTalent(string id, string name, TalentEffectMode mode, StatType stat,
                                 float valuePerLevel, int[] costs, TalentCategory category,
                                 int sortOrder, string tintHex, string desc)
    {
        Color tint = ParseHex(tintHex);
        string path = $"{TalentDir}/{id}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TalentData>(path);

        if (existing != null)
        {
            bool changed = existing.mode != mode
                        || existing.statType != stat
                        || existing.category != category;

            if (changed)
            {
                existing.mode     = mode;
                existing.statType = stat;
                existing.category = category;
                Debug.Log($"[MetaSetup] ซ่อม identity ของ {id} → mode={mode} stat={stat}");
            }

            if (existing.tintColor == Color.white)   // ยังไม่เคยตั้งสี → เติมให้
            {
                existing.tintColor = tint;
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(existing);
            return existing;
        }

        var t = ScriptableObject.CreateInstance<TalentData>();
        t.talentId      = id;
        t.talentName    = name;
        t.description   = desc;
        t.mode          = mode;
        t.statType      = stat;
        t.valuePerLevel = valuePerLevel;
        t.costPerLevel  = costs;
        t.category      = category;
        t.sortOrder     = sortOrder;
        t.tintColor     = tint;

        AssetDatabase.CreateAsset(t, path);
        return t;
    }

    static Color ParseHex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    static void MakeStatAugment(string id, string name, AugmentRarity rarity, float weight,
                                string desc, (StatType type, float value)[] bonuses)
    {
        string path = $"{AugmentDir}/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<StatAugment>(path) != null) return;

        var a = ScriptableObject.CreateInstance<StatAugment>();
        a.augmentId   = id;
        a.augmentName = name;
        a.description = desc;
        a.rarity      = rarity;
        a.weight      = weight;

        a.bonuses = new StatAugment.StatBonus[bonuses.Length];
        for (int i = 0; i < bonuses.Length; i++)
            a.bonuses[i] = new StatAugment.StatBonus { type = bonuses[i].type, value = bonuses[i].value };

        AssetDatabase.CreateAsset(a, path);
    }

    static void MakeTriggerAugment(string id, string name, AugmentRarity rarity, float weight,
                                   string desc, AugmentTrigger trigger, float amount,
                                   AugmentEffect effect, float magnitude, float duration,
                                   float cooldown = 0f)
    {
        string path = $"{AugmentDir}/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<TriggerAugment>(path) != null) return;

        var a = ScriptableObject.CreateInstance<TriggerAugment>();
        a.augmentId        = id;
        a.augmentName      = name;
        a.description      = desc;
        a.rarity           = rarity;
        a.weight           = weight;
        a.trigger          = trigger;
        a.triggerAmount    = amount;
        a.effect           = effect;
        a.magnitude        = magnitude;
        a.duration         = duration;
        a.internalCooldown = cooldown;

        AssetDatabase.CreateAsset(a, path);
    }
}
#endif
