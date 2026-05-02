using UnityEngine;

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
    [Header("Identity")]
    public string          abilityName;
    [TextArea(1, 3)]
    public string          description;
    public Sprite          icon;

    [Header("HUD Slot")]
    [Tooltip("กำหนดว่า ability นี้แสดงใน slot ไหนของ HUD (Q หรือ E)")]
    public AbilitySlotType slotType = AbilitySlotType.Q;

    [Header("Prefab")]
    [Tooltip("GameObject ที่มี AbilityBase component — Instantiate เป็น child ของ player")]
    public GameObject prefab;

    [Header("Levels")]
    [Tooltip("ส่วนมาก 1 level — เพิ่มได้ถ้า ability มีการ upgrade")]
    public AbilityLevelData[] levels = new AbilityLevelData[1];

    [Header("Audio (Optional)")]
    [Tooltip("เสียงตอนใช้ ability — ใส่ได้หลายเสียง สุ่มเล่น\n" +
             "เรียกผ่าน PlayCastSfx() ใน OnActivate() ของ ability script")]
    public AudioClip[] castSfx;
    [Tooltip("เสียงตอน ability ทำดาเมจ / hit — ใส่ได้หลายเสียง สุ่มเล่น\n" +
             "(เช่น exile activate, missile explode)")]
    public AudioClip[] hitSfx;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ castSfx")]
    public float castVolume = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ hitSfx")]
    public float hitVolume  = 0.7f;
    [Tooltip("Pitch variance สุ่มต่อครั้ง (0 = ไม่สุ่ม)\n" +
             "0.1 = ±10% (0.9 → 1.1)")]
    [Range(0f, 0.5f)]
    public float pitchVariance = 0.05f;

    /// <summary>คืน random clip จาก castSfx array</summary>
    public AudioClip GetRandomCastSfx()
        => (castSfx == null || castSfx.Length == 0) ? null : castSfx[Random.Range(0, castSfx.Length)];

    /// <summary>คืน random clip จาก hitSfx array</summary>
    public AudioClip GetRandomHitSfx()
        => (hitSfx == null || hitSfx.Length == 0) ? null : hitSfx[Random.Range(0, hitSfx.Length)];

    // ── Helpers ───────────────────────────────────────────────────────────
    public int MaxLevel => (levels != null && levels.Length > 0) ? levels.Length : 1;

    public AbilityLevelData GetLevelData(int zeroBasedLevel)
    {
        if (levels == null || levels.Length == 0) return new AbilityLevelData();
        return levels[Mathf.Clamp(zeroBasedLevel, 0, levels.Length - 1)];
    }
}
