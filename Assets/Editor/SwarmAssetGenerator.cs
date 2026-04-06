using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Generate Swarm Assets
/// สร้าง WeaponData + StatData + WeaponFusionRecipe ครบทุกตัวโดยอัตโนมัติ
/// รัน 1 ครั้งเดียว — จะ Skip asset ที่มีอยู่แล้ว
/// </summary>
public static class SwarmAssetGenerator
{
    const string WEAPON_PATH = "Assets/Script/Data/WeaponData";
    const string STAT_PATH   = "Assets/Script/Data/StatData";
    const string RECIPE_PATH = "Assets/Script/Data/FusionRecipes";
    const string CHAR_PATH   = "Assets/Script/Data/Characters";

    // ── Menu Entry ──────────────────────────────────────────────────────────
    [MenuItem("Tools/Generate Swarm Assets")]
    public static void Generate()
    {
        EnsureFolders();

        // ── 1. Stats ───────────────────────────────────────────────────────
        var sd_Damage   = CreateStat("SD_Damage",          StatType.Damage,          "เพิ่มความเสียหายทุก weapon +10%/lv",     new[]{0.10f,0.10f,0.10f,0.10f,0.10f}, 90f);
        var sd_Haste    = CreateStat("SD_AbilityHaste",    StatType.AbilityHaste,    "ลด cooldown (Haste Formula) +10/lv",     new[]{10f,10f,10f,10f,10f},            85f);
        var sd_Crit     = CreateStat("SD_CriticalChance",  StatType.CriticalChance,  "โอกาส Crit ×2 damage +8%/lv",           new[]{0.08f,0.08f,0.08f,0.08f,0.08f}, 75f);
        var sd_Area     = CreateStat("SD_AreaSize",        StatType.AreaSize,        "เพิ่มรัศมี/ระยะทุก weapon +11%/lv",     new[]{0.11f,0.11f,0.11f,0.11f,0.11f}, 70f);
        var sd_ProjCnt  = CreateStat("SD_ProjectileCount", StatType.ProjectileCount, "กระสุนสะสม ทุก weapon",                  new[]{1f,1f,2f,2f,3f},                 65f);
        var sd_Duration = CreateStat("SD_Duration",        StatType.Duration,        "เพิ่ม projectile range +12%/lv",         new[]{0.12f,0.12f,0.12f,0.12f,0.12f}, 60f);
        var sd_MaxHP    = CreateStat("SD_MaxHealth",       StatType.MaxHealth,       "+150 HP สูงสุด/lv",                      new[]{150f,150f,150f,150f,150f},       70f);
        var sd_Armor    = CreateStat("SD_Armor",           StatType.Armor,           "+8 Armor ลด damage ที่รับ/lv",           new[]{8f,8f,8f,8f,8f},                 65f);
        var sd_Regen    = CreateStat("SD_HealthRegen",     StatType.HealthRegen,     "ฟื้น +4 HP/วินาที ต่อ lv",              new[]{4f,4f,4f,4f,4f},                 60f);
        var sd_Move     = CreateStat("SD_MoveSpeed",       StatType.MoveSpeed,       "+9% ความเร็ว/lv",                        new[]{0.09f,0.09f,0.09f,0.09f,0.09f}, 55f);
        var sd_Pickup   = CreateStat("SD_PickupRadius",    StatType.PickupRadius,    "+35% รัศมีดูด EXP/lv",                   new[]{0.35f,0.35f,0.35f,0.35f,0.35f}, 50f);
        var sd_Exp      = CreateStat("SD_ExpBonus",        StatType.ExpBonus,        "+10% EXP/lv",                            new[]{0.10f,0.10f,0.10f,0.10f,0.10f}, 45f);

        // ── 2. SUPER Weapons ──────────────────────────────────────────────
        var wd_Magnum   = CreateWeapon("WD_Magnum",   "Magnum",        WeaponTier.Super,  "PistolWeapon",
            new[]{L(80,0.50f,2,14,20,true)});

        var wd_Blunder  = CreateWeapon("WD_Blunderbuss","Blunderbuss", WeaponTier.Super,  "ShotgunWeapon",
            new[]{L(45,0.80f,8,9,12)});

        var wd_StarRing = CreateWeapon("WD_StarRing",  "Star Ring",    WeaponTier.Super,  "OrbiterWeapon",
            new[]{L(60,0.80f,6,5,0)});

        var wd_Railgun  = CreateWeapon("WD_Railgun",   "Railgun",      WeaponTier.Super,  "RailgunWeapon",
            new[]{L(200,4.0f,1,50,0)});

        var wd_Minefield= CreateWeapon("WD_Minefield","Minefield",     WeaponTier.Super,  "MinefieldWeapon",
            new[]{L(120,2.5f,3,6,0)});

        var wd_Chainsaw = CreateWeapon("WD_Chainsaw",  "Chainsaw",     WeaponTier.Super,  "WhipWeapon",
            new[]{L(40,0.25f,1,5,0)});

        // ── Riven Signature Weapons ───────────────────────────────────────
        var wd_BunnyHop = CreateWeapon("WD_BunnyHop", "Bunny Hop", WeaponTier.Normal, "BunnyHopWeapon",
            new[]{
                L(55,0f,1,3.5f,0),   // cooldown 0 = charge-based, range = AoE radius
                L(70,0f,1,3.8f,0),
                L(88,0f,1,4.2f,0),
                L(110,0f,1,4.6f,0),
                L(135,0f,1,5.0f,0)
            });
        wd_BunnyHop.weight = 0f;   // ไม่ปรากฏใน level-up cards (signature weapon เท่านั้น)
        EditorUtility.SetDirty(wd_BunnyHop);

        var wd_Valor = CreateWeapon("WD_Valor", "Valor", WeaponTier.Normal, "ValorWeapon",
            new[]{
                L(80,8f,1,4f,0),
                L(100,7.5f,1,4.3f,0),
                L(125,7f,1,4.6f,0),
                L(150,6.5f,1,5.0f,0),
                L(180,6f,1,5.5f,0)
            });
        wd_Valor.weight = 0f;
        EditorUtility.SetDirty(wd_Valor);

        var wd_BladeExile = CreateWeapon("WD_BladeOfExile", "Blade of Exile", WeaponTier.Normal, "BladeOfExileWeapon",
            new[]{
                L(0,80f,1,0,0),   // damage 0 (เป็น buff), cooldown 80s
                L(0,75f,1,0,0),
                L(0,70f,1,0,0),
                L(0,65f,1,0,0),
                L(0,60f,1,0,0)
            });
        wd_BladeExile.weight = 0f;
        EditorUtility.SetDirty(wd_BladeExile);

        // ── 3. FUSION Weapons ─────────────────────────────────────────────
        var wd_OrbCannon  = CreateWeapon("WD_OrbitalCannon", "Orbital Cannon", WeaponTier.Fusion, "OrbitalCannonWeapon",
            new[]{L(80,0.80f,4,5,18)});

        var wd_Cluster    = CreateWeapon("WD_ClusterBomb",   "Cluster Bomb",   WeaponTier.Fusion, "ClusterBombWeapon",
            new[]{L(100,2.5f,2,14,18)});

        var wd_PlasmaWhip = CreateWeapon("WD_PlasmaWhip",    "Plasma Whip",    WeaponTier.Fusion, "PlasmaWhipWeapon",
            new[]{L(90,1.0f,4,6,0)});

        // ── 4. NORMAL Weapons (link superVersion + conditions) ────────────
        var wd_Pistol  = CreateWeapon("WD_Pistol",  "Pistol",  WeaponTier.Normal, "PistolWeapon",
            new[]{
                L(20,1.00f,1, 8,14),
                L(26,0.90f,1, 9,14),
                L(33,0.80f,2,10,15),
                L(42,0.72f,2,10,15),
                L(52,0.62f,3,11,16)
            }, wd_Magnum,
            Cond(SuperConditionType.StatAtLevel, StatType.CriticalChance, 3));

        var wd_Shotgun = CreateWeapon("WD_Shotgun", "Shotgun", WeaponTier.Normal, "ShotgunWeapon",
            new[]{
                L(12,1.20f,4,7,12),
                L(14,1.10f,4,7,12),
                L(16,1.00f,5,8,13),
                L(18,0.95f,5,8,13),
                L(22,0.85f,6,9,13)
            }, wd_Blunder,
            Cond(SuperConditionType.StatAtLevel, StatType.AreaSize, 5));

        var wd_Orbiter = CreateWeapon("WD_Orbiter", "Orbiter", WeaponTier.Normal, "OrbiterWeapon",
            new[]{
                L(18,1.50f,2,3.0f,0),
                L(22,1.40f,2,3.0f,0),
                L(28,1.30f,3,3.5f,0),
                L(34,1.20f,3,4.0f,0),
                L(42,1.00f,4,4.5f,0)
            }, wd_StarRing,
            Cond(SuperConditionType.StatAtLevel, StatType.ProjectileCount, 3));

        var wd_Laser   = CreateWeapon("WD_Laser",   "Laser",   WeaponTier.Normal, "LaserWeapon",
            new[]{
                L(30,2.00f,1,12,25,true),
                L(38,1.80f,1,14,25,true),
                L(48,1.60f,1,15,26,true),
                L(60,1.40f,2,16,26,true),
                L(75,1.20f,2,18,28,true)
            }, wd_Railgun,
            Cond(SuperConditionType.StatAtLevel, StatType.AbilityHaste, 4));

        var wd_Grenade = CreateWeapon("WD_Grenade", "Grenade", WeaponTier.Normal, "GrenadeWeapon",
            new[]{
                L(50,3.00f,1,10,0),
                L(65,2.70f,1,11,0),
                L(82,2.50f,2,12,0),
                L(100,2.30f,2,13,0),
                L(125,2.00f,3,14,0)
            }, wd_Minefield,
            Cond(SuperConditionType.StatAtLevel, StatType.Duration, 3));

        var wd_Whip    = CreateWeapon("WD_Whip",    "Whip",    WeaponTier.Normal, "WhipWeapon",
            new[]{
                L(35,1.80f,1,2.5f,0),
                L(44,1.60f,1,2.8f,0),
                L(55,1.40f,1,3.2f,0),
                L(68,1.20f,1,3.6f,0),
                L(85,1.00f,1,4.0f,0)
            }, wd_Chainsaw,
            Cond(SuperConditionType.StatAtLevel, StatType.AreaSize, 3));

        // ── 5. Fusion Recipes ─────────────────────────────────────────────
        var fr_OrbCannon  = CreateFusionRecipe("FR_OrbitalCannon", "Orbital Cannon", wd_Magnum,   wd_StarRing,  wd_OrbCannon);
        var fr_Cluster    = CreateFusionRecipe("FR_ClusterBomb",   "Cluster Bomb",   wd_Blunder,  wd_Minefield, wd_Cluster);
        var fr_PlasmaWhip = CreateFusionRecipe("FR_PlasmaWhip",    "Plasma Whip",    wd_Railgun,  wd_Chainsaw,  wd_PlasmaWhip);

        // ── 6. Characters ─────────────────────────────────────────────────
        CreateCharacter("Char_Gunner",  "Gunner",  "นักยิงปืน เชี่ยวชาญอาวุธระยะไกล",     wd_Pistol,  100f, 5.0f, "พลิกกระสุนออกแบบพิเศษ — Crit ครั้งแรกในทุก 3 วิ ไม่มีระยะ cool down");
        CreateCharacter("Char_Hunter",  "Hunter",  "นักล่าที่รวดเร็ว ใช้ shotgun เป็นหลัก", wd_Shotgun, 120f, 5.5f, "ยิงในระยะใกล้ — damage เพิ่ม 25% ถ้าศัตรูอยู่ในระยะ 3");
        CreateCharacter("Char_Scholar", "Scholar", "นักวิทยาศาสตร์ ควบคุมพลังงาน",         wd_Laser,   90f,  4.8f, "ชาร์จพลังงาน — ยิง Laser นานกว่าปกติ 20% โดยไม่มี cost");
        CreateCharacter("Char_Brawler", "Brawler", "นักสู้ระยะประชิด แข็งแกร่งมาก",        wd_Whip,    150f, 4.5f, "เกราะหนา — ลดดาเมจที่รับ 10% ตลอดเวลา");
        CreateCharacter("Char_Riven",   "Riven",   "นักรบใช้ดาบ — สะสม CHARGE ขณะเดิน แล้ว Dash+Slash",
            wd_BunnyHop, 120f, 5.5f,
            "Runic Blade — Dash ทุก 20 units เดิน ดาเมจ +0–15% ตามระยะ รับ Shield 25% ดาเมจที่ทำ",
            passiveWeapons: new[]{ wd_Valor, wd_BladeExile });

        // ── 7. Update UpgradeManager on Player Prefab ─────────────────────
        PatchUpgradeManager(
            new[]{ wd_Pistol, wd_Shotgun, wd_Orbiter, wd_Laser, wd_Grenade, wd_Whip },
            new StatData[]{ sd_Damage, sd_Haste, sd_Crit, sd_Area, sd_ProjCnt, sd_Duration,
                            sd_MaxHP, sd_Armor, sd_Regen, sd_Move, sd_Pickup, sd_Exp },
            new[]{ fr_OrbCannon, fr_Cluster, fr_PlasmaWhip });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Swarm Asset Generator",
            "✅ สร้าง Assets สำเร็จ!\n\n" +
            "• WeaponData    : 15 assets (6 Normal, 6 Super, 3 Fusion)\n" +
            "• StatData      : 12 assets\n" +
            "• FusionRecipe  : 3 assets\n" +
            "• CharacterData : 5 assets (incl. Riven)\n\n" +
            "⚠️ Super/Fusion Weapon Prefabs ยังต้องสร้างเอง\n" +
            "   (สร้าง Empty GO + WeaponScript แล้ว assign ใน WD_xxx.prefab)\n\n" +
            "📋 Next steps:\n" +
            "1. สร้าง Weapon Prefabs แล้ว assign ใน WeaponData\n" +
            "2. เพิ่ม WeaponStatHUD component ใน SampleScene Canvas\n" +
            "3. เพิ่ม CharacterSelectUI ใน MainMenu Scene", "OK");
    }

    // ── Weapon Builder ──────────────────────────────────────────────────────
    static WeaponData CreateWeapon(
        string assetName, string weaponName, WeaponTier tier, string scriptName,
        WeaponLevelData[] levels,
        WeaponData superVersion = null,
        SuperCondition superCond = null)
    {
        string path = $"{WEAPON_PATH}/{assetName}.asset";

        // Load existing — ไม่ทับของเดิม แต่ update ข้อมูล
        var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (existing != null)
        {
            ApplyWeaponData(existing, weaponName, tier, scriptName, levels, superVersion, superCond);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var wd = ScriptableObject.CreateInstance<WeaponData>();
        ApplyWeaponData(wd, weaponName, tier, scriptName, levels, superVersion, superCond);
        AssetDatabase.CreateAsset(wd, path);
        Debug.Log($"[Generator] Created {path}");
        return wd;
    }

    static void ApplyWeaponData(WeaponData wd, string weaponName, WeaponTier tier,
        string scriptName, WeaponLevelData[] levels,
        WeaponData superVersion, SuperCondition superCond)
    {
        wd.weaponName = weaponName;
        wd.tier       = tier;
        wd.levels     = levels;
        wd.weight     = tier == WeaponTier.Normal ? 100f : 0f;  // Super/Fusion ไม่ถูก random

        if (superVersion != null) wd.superVersion = superVersion;

        if (superCond != null)
            wd.superConditions = new[] { superCond };

        // ถ้ายังไม่มี prefab → หาจาก Project
        if (wd.prefab == null)
        {
            var guids = AssetDatabase.FindAssets($"t:Prefab {weaponName.Replace(" ","")}");
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets($"t:Prefab {scriptName.Replace("Weapon","")}");
            if (guids.Length > 0)
                wd.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    // ── Stat Builder ───────────────────────────────────────────────────────
    static StatData CreateStat(string assetName, StatType type, string desc,
        float[] values, float weight)
    {
        string path     = $"{STAT_PATH}/{assetName}.asset";
        var    existing = AssetDatabase.LoadAssetAtPath<StatData>(path);
        if (existing != null)
        {
            existing.statName      = type.ToString();
            existing.description   = desc;
            existing.statType      = type;
            existing.valuePerLevel = values;
            existing.weight        = weight;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var sd = ScriptableObject.CreateInstance<StatData>();
        sd.statName      = type.ToString();
        sd.description   = desc;
        sd.statType      = type;
        sd.valuePerLevel = values;
        sd.weight        = weight;
        AssetDatabase.CreateAsset(sd, path);
        Debug.Log($"[Generator] Created {path}");
        return sd;
    }

    // ── Fusion Recipe Builder ──────────────────────────────────────────────
    static WeaponFusionRecipe CreateFusionRecipe(string assetName, string recipeName,
        WeaponData a, WeaponData b, WeaponData result)
    {
        string path     = $"{RECIPE_PATH}/{assetName}.asset";
        var    existing = AssetDatabase.LoadAssetAtPath<WeaponFusionRecipe>(path);
        if (existing != null)
        {
            existing.recipeName   = recipeName;
            existing.superWeaponA = a;
            existing.superWeaponB = b;
            existing.fusionResult = result;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var r = ScriptableObject.CreateInstance<WeaponFusionRecipe>();
        r.recipeName   = recipeName;
        r.superWeaponA = a;
        r.superWeaponB = b;
        r.fusionResult = result;
        AssetDatabase.CreateAsset(r, path);
        Debug.Log($"[Generator] Created {path}");
        return r;
    }

    // ── Character Builder ─────────────────────────────────────────────────
    static CharacterData CreateCharacter(string assetName, string charName, string desc,
        WeaponData startWep, float hp, float speed, string passive,
        WeaponData[] passiveWeapons = null)
    {
        string path     = $"{CHAR_PATH}/{assetName}.asset";
        var    existing = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (existing != null)
        {
            existing.characterName      = charName;
            existing.description        = desc;
            existing.startingWeapon     = startWep;
            existing.passiveWeapons  = passiveWeapons;
            existing.baseHealth         = hp;
            existing.baseMoveSpeed      = speed;
            existing.passiveDescription = passive;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var cd = ScriptableObject.CreateInstance<CharacterData>();
        cd.characterName      = charName;
        cd.description        = desc;
        cd.startingWeapon     = startWep;
        cd.passiveWeapons  = passiveWeapons;
        cd.baseHealth         = hp;
        cd.baseMoveSpeed      = speed;
        cd.passiveDescription = passive;
        AssetDatabase.CreateAsset(cd, path);
        Debug.Log($"[Generator] Created {path}");
        return cd;
    }

    // ── Patch UpgradeManager on Player Prefab ─────────────────────────────
    static void PatchUpgradeManager(WeaponData[] weapons, StatData[] stats,
        WeaponFusionRecipe[] recipes = null)
    {
        // หา Player Prefab
        var guids = AssetDatabase.FindAssets("t:Prefab Player");
        foreach (var guid in guids)
        {
            var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefab     = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            var um = prefab.GetComponent<UpgradeManager>();
            if (um == null) continue;

            using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
            var root   = scope.prefabContentsRoot;
            var umEdit = root.GetComponent<UpgradeManager>();
            if (umEdit == null) continue;

            // Ensure ChargeManager exists on player
            if (root.GetComponent<ChargeManager>() == null)
                root.AddComponent<ChargeManager>();

            umEdit.allWeapons = new System.Collections.Generic.List<WeaponData>(weapons);
            umEdit.allStats   = new System.Collections.Generic.List<StatData>(stats);
            if (recipes != null)
                umEdit.allRecipes = new System.Collections.Generic.List<WeaponFusionRecipe>(recipes);
            EditorUtility.SetDirty(umEdit);

            Debug.Log($"[Generator] Patched UpgradeManager on {prefabPath}");
            break;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    static WeaponLevelData L(float dmg, float cd, int count, float range,
        float speed, bool pierce = false) => new WeaponLevelData
    {
        damage          = dmg,
        cooldown        = cd,
        projectileCount = count,
        range           = range,
        projectileSpeed = speed,
        piercing        = pierce
    };

    static SuperCondition Cond(SuperConditionType type, StatType stat, int level) =>
        new SuperCondition
        {
            conditionType    = type,
            requiredStatType = stat,
            requiredLevel    = level
        };

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Script/Data");
        EnsureFolder(WEAPON_PATH);
        EnsureFolder(STAT_PATH);
        EnsureFolder(RECIPE_PATH);
        EnsureFolder(CHAR_PATH);
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts  = path.Split('/');
            var parent = string.Join("/", parts[..^1]);
            AssetDatabase.CreateFolder(parent, parts[^1]);
        }
    }
}
