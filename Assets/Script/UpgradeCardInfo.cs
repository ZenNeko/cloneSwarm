using UnityEngine;

public enum UpgradeCardType
{
    WeaponNew,      // เพิ่ม weapon ใหม่ใน slot ว่าง
    WeaponLevelUp,  // weapon ที่มีอยู่แล้ว Lv+1
    WeaponSuper,    // Normal Lv5 → Super  (จาก Objective Orb)
    WeaponFusion,   // Super A + Super B → Fusion  (จาก Objective Orb)
    Stat,           // stat upgrade
    Augment         // Augment — ได้เฉพาะ level ที่กำหนดใน SharedExperienceManager
}

/// <summary>ข้อมูลการ์ดที่จะแสดงใน LevelUpUI / ObjectiveRewardUI</summary>
public class UpgradeCardInfo
{
    public UpgradeCardType type;
    public bool            isRecommended;
    public System.Collections.Generic.List<Sprite> synergyIcons = new();
    public bool            showSynergy;

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

    // ── Augment fields ────────────────────────────────────────────────────
    public AugmentData augment;

    // ── Display helpers ───────────────────────────────────────────────────
    public string DisplayName => type switch
    {
        UpgradeCardType.Augment => augment?.augmentName ?? "???",
        UpgradeCardType.Stat    => stat?.statName       ?? "???",
        _                       => weapon?.weaponName   ?? "???",
    };

    public Sprite DisplayIcon => type switch
    {
        UpgradeCardType.Augment => augment?.icon,
        UpgradeCardType.Stat    => stat?.icon,
        _                       => weapon?.icon,
    };

    public string DisplayDescription
    {
        get
        {
            if (type == UpgradeCardType.Augment)
                return augment?.description ?? "";

            if (type == UpgradeCardType.Stat)
                return stat?.description ?? "";

            if (weapon == null) return "";

            return weapon.description;
        }
    }

    public string DisplayLevelText => type switch
    {
        UpgradeCardType.WeaponNew     => "NEW",
        UpgradeCardType.WeaponLevelUp => $"Lv {targetLevel} / {weapon?.MaxLevel}",
        UpgradeCardType.WeaponSuper   => "SUPER ★",
        UpgradeCardType.WeaponFusion  => "FUSION ★★",
        UpgradeCardType.Stat          => currentStatLevel == 0 ? "NEW" : $"Lv {currentStatLevel + 1} / {stat?.MaxLevel}",
        UpgradeCardType.Augment       => augment?.RarityLabel ?? "AUGMENT",
        _                             => ""
    };

    public float Weight => type switch
    {
        UpgradeCardType.Augment => augment?.weight ?? 50f,
        UpgradeCardType.Stat    => stat?.weight    ?? 50f,
        _                       => weapon?.weight  ?? 50f,
    };
}
