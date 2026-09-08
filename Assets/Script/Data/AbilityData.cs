using UnityEngine;
using UnityEngine.Localization;

/// <summary>Stats ของ ability ในแต่ละ level</summary>
[System.Serializable]
public class AbilityLevelData
{
    public float damage   = 20f;
    public float cooldown = 8f;
    public float duration = 15f;    // ระยะเวลา active (เช่น exile, shield, summon)
    public float range    = 5f;
    [TextArea(1, 2)]
    [Tooltip("ข้อความแสดงบน upgrade card")]
    public string levelUpText;
}

/// <summary>กำหนด slot ที่ ability จะแสดงใน HUD</summary>
public enum AbilitySlotType { Q, E }

/// <summary>
/// ข้อมูล Ability — แยกจาก WeaponData โดยสมบูรณ์
/// ใช้กับ AbilityBase component (ValorWeapon, BladeOfExileWeapon ฯลฯ)
///
/// สร้างได้จาก  Assets > Create > LoL Swarm > Ability Data
/// </summary>
[CreateAssetMenu(fileName = "Ability_New", menuName = "LoL Swarm/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("Identity — ใช้ใน log/ค้นหา ไม่ต้องแปล")]
    public string          abilityName;

    [Header("Display — แปลได้ ชี้ไป String Table 'Content'")]
    [Tooltip("ว่าง = ใช้ abilityName แทน · ตั้งอัตโนมัติด้วย Tools > Clone Swarm > Localization > 2. Relink")]
    public LocalizedString displayName;
    public LocalizedString description;
    public Sprite          icon;

    /// <summary>ข้อความที่แปลแล้วตาม locale ปัจจุบัน — ว่างเมื่อยังไม่ได้ผูก entry
    /// ทุกที่ที่เอาไปแสดงต้องอ่าน property พวกนี้ ไม่ใช่ field ตรงๆ</summary>
    public string DisplayName => displayName.IsEmpty ? abilityName : displayName.GetLocalizedString();
    public string Description  => description.IsEmpty ? "" : description.GetLocalizedString();

    [Header("HUD Slot")]
    [Tooltip("กำหนดว่า ability นี้แสดงใน slot ไหนของ HUD (Q หรือ E)")]
    public AbilitySlotType slotType = AbilitySlotType.Q;

    [Header("Prefab")]
    [Tooltip("GameObject ที่มี AbilityBase component — Instantiate เป็น child ของ player")]
    public GameObject prefab;

    [Header("Levels")]
    [Tooltip("ส่วนมาก 1 level — เพิ่มได้ถ้า ability มีการ upgrade")]
    public AbilityLevelData[] levels = new AbilityLevelData[1];

    // NOTE: SFX fields (castSfx, hitSfx, castVolume, hitVolume, pitchVariance) ย้ายไป
    //       อยู่บน ability prefab (AbilityBase) แล้ว — เหตุผลเดียวกับ WeaponBase:
    //       presentation (audio) ควรอยู่กับ prefab ของแต่ละ ability ไม่ใช่ shared data

    // ── Helpers ───────────────────────────────────────────────────────────
    public int MaxLevel => (levels != null && levels.Length > 0) ? levels.Length : 1;

    public AbilityLevelData GetLevelData(int zeroBasedLevel)
    {
        if (levels == null || levels.Length == 0) return new AbilityLevelData();
        return levels[Mathf.Clamp(zeroBasedLevel, 0, levels.Length - 1)];
    }
}
