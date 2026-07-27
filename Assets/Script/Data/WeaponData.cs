using UnityEngine;

public enum WeaponTier { Normal, Super, Fusion }

public enum SuperConditionType
{
    StatAtLevel,    // ต้องมี stat X ถึง Lv.Y
    WeaponAtLevel,  // ต้องมี weapon X ถึง Lv.Y ด้วย
    PlayerLevel     // shared level ถึง X
}

[System.Serializable]
public class SuperCondition
{
    public SuperConditionType conditionType;

    [Tooltip("StatAtLevel: ต้องการ stat ประเภทนี้")]
    public StatType requiredStatType;
    [Tooltip("StatAtLevel / WeaponAtLevel: ต้องถึง level นี้ขึ้นไป (1-based)")]
    public int      requiredLevel = 5;

    [Tooltip("WeaponAtLevel: weapon ที่ต้องมี")]
    public WeaponData requiredWeapon;

    public string GetDescription()
    {
        return conditionType switch
        {
            SuperConditionType.StatAtLevel
                => $"{requiredStatType} Lv.{requiredLevel}",
            SuperConditionType.WeaponAtLevel
                => $"{requiredWeapon?.weaponName ?? "?"} Lv.{requiredLevel}",
            SuperConditionType.PlayerLevel
                => $"Player Level {requiredLevel}",
            _ => "?"
        };
    }
}

/// <summary>Stats ของ weapon ในแต่ละ level</summary>
[System.Serializable]
public class WeaponLevelData
{
    public float  damage          = 20f;
    public float  cooldown        = 1f;
    public int    projectileCount = 1;
    public float  range           = 8f;
    public float  projectileSpeed = 12f;
    [Tooltip("รัศมีการทำลายล้าง/พื้นที่แสดงผลพิเศษ (เช่น วงระเบิด, ออร่า)")]
    public float  radius          = 0f;
    [Tooltip("ระยะเวลาการคงอยู่ของอาวุธประเภทติดตั้ง (วินาที) — ตั้งค่า 0 เพื่อใช้ค่าเริ่มต้นใน Script")]
    public float  duration        = 0f;
    public bool   piercing        = false;
}

[CreateAssetMenu(fileName = "Weapon_New", menuName = "LoL Swarm/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    public string     weaponName;
    [TextArea(1, 3)]
    public string     description;
    public Sprite     icon;
    public WeaponTier tier = WeaponTier.Normal;

    [Header("Prefab")]
    [Tooltip("GameObject ที่มี WeaponBase component — จะ Instantiate เป็น child ของ player")]
    public GameObject prefab;

    [Header("Targeting")]
    [Tooltip("AutoNearest = ล็อกศัตรูที่ใกล้ที่สุดอัตโนมัติ\n" +
             "MouseAim    = เล็งตามตำแหน่งเมาส์ของผู้เล่น")]
    public AimMode aimMode = AimMode.AutoNearest;

    // NOTE: VFX + SFX fields ย้ายไปอยู่บน weapon prefab (WeaponBase) แล้ว
    //   VFX → weaponVfxType, secondaryVfxType
    //   SFX → fireSfx[], hitSfx[], fireVolume, hitVolume, pitchVariance
    // เหตุผล: presentation (visual + audio) ควรอยู่กับ prefab ของแต่ละ weapon
    //         ส่วน WeaponData เก็บแต่ shared stat / pool config เท่านั้น
    //
    // หมายเหตุ: HitEffect / CritHitEffect (impact spark ตอนโดน) — Enemy.cs spawn เอง
    //          ผ่าน NotifyHitClientRpc ใน EnemyTakeDamage (ไม่ต้อง config ที่ weapon)

    [Header("Levels")]
    [Tooltip("Normal weapon: 5 levels | Super/Fusion: 1 level")]
    public WeaponLevelData[] levels = new WeaponLevelData[5];

    [Header("Super Upgrade (Normal only)")]
    [Tooltip("Super Weapon ที่ได้จาก Objective Orb")]
    public WeaponData superVersion;
    [Tooltip("เงื่อนไขเพิ่มเติม — ทุก condition ต้องครบจึง Super ได้\n" +
             "Weapon Lv5 ถูกตรวจ auto อยู่แล้ว ไม่ต้องใส่ที่นี่")]
    public SuperCondition[] superConditions;

    [Header("Card Pool")]
    [Tooltip("น้ำหนักสุ่มการ์ด — Common≈100, Uncommon≈60, Rare≈25, Epic≈8")]
    public float weight = 100f;
    [Tooltip("ถ้าใส่ไว้ — weapon นี้จะออกให้เฉพาะผู้เล่นที่ใช้ตัวละครนั้นเท่านั้น\n" +
             "ปล่อยว่าง = ทุกตัวละครสุ่มได้")]
    public CharacterData exclusiveCharacter;

    // ── Helpers ───────────────────────────────────────────────────────────
    public int MaxLevel => (levels != null && levels.Length > 0) ? levels.Length : 1;

    public WeaponLevelData GetLevelData(int zeroBasedLevel)
    {
        if (levels == null || levels.Length == 0) return new WeaponLevelData();
        return levels[Mathf.Clamp(zeroBasedLevel, 0, levels.Length - 1)];
    }
}
