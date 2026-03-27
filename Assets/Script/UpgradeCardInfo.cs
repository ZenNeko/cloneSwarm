using UnityEngine;

public enum UpgradeCardType
{
    WeaponNew,      // เพิ่ม weapon ใหม่ใน slot ว่าง
    WeaponLevelUp,  // weapon ที่มีอยู่แล้ว Lv+1
    WeaponSuper,    // Normal Lv5 → Super  (จาก Objective Orb)
    WeaponFusion,   // Super A + Super B → Fusion  (จาก Objective Orb)
    Stat            // stat upgrade
}

/// <summary>ข้อมูลการ์ดที่จะแสดงใน LevelUpUI / ObjectiveRewardUI</summary>
public class UpgradeCardInfo
{
    public UpgradeCardType type;

    // ── Weapon fields ─────────────────────────────────────────────────────
    public WeaponData weapon;
    /// <summary>Level ที่ weapon จะไปถึงหลัง apply (1-based display)</summary>
    public int        targetLevel;

    // ── Fusion extra ──────────────────────────────────────────────────────
    public WeaponFusionRecipe fusionRecipe;  // ใช้ตอน apply Fusion

    // ── Stat fields ───────────────────────────────────────────────────────
    public StatData stat;
    /// <summary>Level ปัจจุบันของ stat ก่อน apply (0-based)</summary>
    public int      currentStatLevel;

    // ── Display helpers ───────────────────────────────────────────────────
    public string DisplayName => type == UpgradeCardType.Stat
        ? (stat?.statName ?? "???")
        : (weapon?.weaponName ?? "???");

    public Sprite DisplayIcon => type == UpgradeCardType.Stat
        ? stat?.icon
        : weapon?.icon;

    public string DisplayDescription
    {
        get
        {
            if (type == UpgradeCardType.Stat)
                return stat?.description ?? "";

            if (weapon == null) return "";

            if (type is UpgradeCardType.WeaponNew or UpgradeCardType.WeaponLevelUp)
            {
                var ld = weapon.GetLevelData(targetLevel - 1);
                return string.IsNullOrEmpty(ld.levelUpText)
                    ? weapon.description
                    : ld.levelUpText;
            }
            return weapon.description;
        }
    }

    public string DisplayLevelText => type switch
    {
        UpgradeCardType.WeaponNew     => "NEW",
        UpgradeCardType.WeaponLevelUp => $"Lv {targetLevel} / {weapon?.MaxLevel}",
        UpgradeCardType.WeaponSuper   => "SUPER ★",
        UpgradeCardType.WeaponFusion  => "FUSION ★★",
        UpgradeCardType.Stat          => $"Lv {currentStatLevel + 1} / {stat?.MaxLevel}",
        _                             => ""
    };

    public float Weight => type == UpgradeCardType.Stat
        ? (stat?.weight ?? 50f)
        : (weapon?.weight ?? 50f);
}
