using UnityEngine;
using UnityEngine.Localization;

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
    [Header("Identity — ห้ามแปล ห้ามเปลี่ยนหลังปล่อยเกม")]
    [Tooltip("รหัสประจำอาวุธ ไม่ใช่ชื่อที่โชว์บนจอ — ใช้เป็นคีย์จริงใน:\n" +
             "  1. ServerRpc (AddWeaponServerRpc / UpgradeWeaponServerRpc / SpawnPassiveWeaponServerRpc)\n" +
             "  2. dictionary ของ damage ฝั่ง server (_serverWeaponDamages)\n" +
             "  3. FindWeaponDataByName ที่เทียบด้วย ==\n" +
             "แปลเมื่อไหร่ = host กับ client ส่งคนละสตริง อาวุธหาไม่เจอและ sync ไม่ตรง\n" +
             "ชื่อที่โชว์ให้ใส่ displayName ข้างล่าง (แพตเทิร์นเดียวกับ CharacterData)")]
    public string     weaponName;

    [Header("Display — แปลได้")]
    [Tooltip("ว่าง = ใช้ weaponName แทน · ตั้งอัตโนมัติด้วย Tools > Clone Swarm > Localization > 2. Relink")]
    public LocalizedString displayName;

    [Tooltip("คำอธิบายที่โชว์บนการ์ด/แผงรายละเอียด — ชี้ไป entry ใน String Table 'Content' · " +
             "ตั้งอัตโนมัติได้ด้วย Tools > Clone Swarm > Localization > 2. Relink")]
    public LocalizedString description;
    public Sprite     icon;
    public WeaponTier tier = WeaponTier.Normal;

    /// <summary>คำอธิบายที่แปลแล้วตาม locale ปัจจุบัน — คืนค่าว่างเมื่อยังไม่ได้ผูก entry
    ///
    /// ทุกที่ที่เอาคำอธิบายไปแสดงต้องอ่านตัวนี้ ไม่ใช่ field description ตรงๆ
    /// (GetLocalizedString เป็น sync — ใช้ได้เพราะ table ถูก preload ตอนเริ่มเกม)</summary>
    public string Description => description.IsEmpty ? "" : description.GetLocalizedString();

    /// <summary>ชื่อที่เอาไปโชว์บนจอ — ตกกลับไปใช้ weaponName เมื่อยังไม่ได้ตั้ง displayName
    ///
    /// ทุกที่ที่เอาชื่อไปแสดงต้องอ่านตัวนี้ ห้ามอ่าน weaponName ตรงๆ
    /// (จุดที่ส่งขึ้น ServerRpc / ใช้เป็น dictionary key / เทียบค่า ยังต้องใช้ weaponName เหมือนเดิม)</summary>
    public string DisplayName => displayName.IsEmpty ? weaponName : displayName.GetLocalizedString();

    [Header("Prefab")]
    [Tooltip("GameObject ที่มี WeaponBase component — จะ Instantiate เป็น child ของ player")]
    public GameObject prefab;

    [Header("Targeting")]
    [Tooltip("AutoNearest    = ล็อกศัตรูที่ใกล้ที่สุดอัตโนมัติ\n" +
             "MouseAim       = เล็งตามตำแหน่งเมาส์ของผู้เล่น\n" +
             "PlayerMovement = ยิงไปทางที่กำลังเดิน\n" +
             "Random         = สุ่มจากศัตรูทุกตัวในระยะ (ไกลแค่ไหนก็มีสิทธิ์)\n" +
             "RandomNear     = สุ่มจากศัตรูที่ใกล้ที่สุด N ตัว — ดู randomNearCandidates")]
    public AimMode aimMode = AimMode.AutoNearest;

    [Tooltip("ใช้เมื่อ aimMode = RandomNear — สุ่มจากศัตรูที่ใกล้ที่สุดกี่ตัว\n" +
             "1 = เท่ากับ AutoNearest · ค่าสูงๆ = เข้าใกล้ Random\n" +
             "กำหนดเป็นจำนวนตัว ไม่ใช่ระยะ เพื่อให้พฤติกรรมคงที่ทั้งตอนศัตรูบางและตอนศัตรูล้นจอ")]
    [Min(1)] public int randomNearCandidates = 3;

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
