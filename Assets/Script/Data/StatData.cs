using UnityEngine;
using UnityEngine.Localization;

public enum StatType
{
    // ── Combat ────────────────────────────────────────────────────────────
    Damage,           // +10%  damage ทุก weapon
    AbilityHaste,     // +10   ลด cooldown เป็น flat (haste formula)
    CriticalChance,   // +8%   โอกาส crit (×2 damage)
    AreaSize,         // +11%  range / AoE radius ทุก weapon
    ProjectileCount,  // 1/1/2/2/3  extra projectile ทุก weapon
    Duration,         // +12%  projectile lifetime / range

    // ── Survival ──────────────────────────────────────────────────────────
    MaxHealth,        // +150  HP สูงสุด
    Armor,            // +8    ลด damage ที่รับ (flat)
    HealthRegen,      // +4    HP ฟื้นต่อวินาที

    // ── Utility ───────────────────────────────────────────────────────────
    MoveSpeed,        // +9%   ความเร็ว
    PickupRadius,     // +35%  รัศมีดูด EXP orb
    ExpBonus,         // +10%  EXP ที่ได้รับ

    // ── Full Build Bonuses ─────────────────────────────────────────────────
    GainGold,         // +25 gold  (at max level)
    HealOnFullBuild   // heal 25% HP  (at max level)
}

[CreateAssetMenu(fileName = "Stat_New", menuName = "LoL Swarm/Stat Data")]
public class StatData : ScriptableObject
{
    [Header("Identity — ใช้ใน log ไม่ต้องแปล")]
    public string   statName;

    [Header("Display — แปลได้ ชี้ไป String Table 'Content'")]
    [Tooltip("ว่าง = ใช้ statName แทน · ตั้งอัตโนมัติด้วย Tools > Clone Swarm > Localization > 2. Relink")]
    public LocalizedString displayName;
    public LocalizedString description;
    public Sprite   icon;

    /// <summary>ข้อความที่แปลแล้วตาม locale ปัจจุบัน — ว่างเมื่อยังไม่ได้ผูก entry
    /// ทุกที่ที่เอาไปแสดงต้องอ่าน property พวกนี้ ไม่ใช่ field ตรงๆ</summary>
    public string DisplayName => displayName.IsEmpty ? statName : displayName.GetLocalizedString();
    public string Description  => description.IsEmpty ? "" : description.GetLocalizedString();
    public StatType statType;

    [Header("Value Per Level  (index 0 = Lv1)")]
    [Tooltip(
        "Damage/MoveSpeed/AreaSize/etc (%) → ใส่ decimal  0.10 = 10%\n" +
        "AbilityHaste/MaxHealth/Armor/HPRegen → ใส่ flat value\n" +
        "ProjectileCount → จำนวน projectile สะสม ณ level นั้น (เช่น 1,1,2,2,3)\n" +
        "GainGold/HealOnFullBuild → ใส่ค่าแค่ index 4 (max level) เท่านั้น")]
    public float[] valuePerLevel = new float[5];

    [Header("Card Pool")]
    [Tooltip("Common≈100  Uncommon≈70  Rare≈40  Epic≈15")]
    public float weight = 80f;

    // ── Helpers ───────────────────────────────────────────────────────────
    public int   MaxLevel               => valuePerLevel != null ? valuePerLevel.Length : 5;
    public float GetValueAtLevel(int z) => valuePerLevel != null
        ? valuePerLevel[Mathf.Clamp(z, 0, valuePerLevel.Length - 1)]
        : 0f;
}
