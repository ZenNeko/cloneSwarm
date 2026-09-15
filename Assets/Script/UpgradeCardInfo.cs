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

/// <summary>
/// หนึ่งบรรทัดของ Evolution Synergy — **บอกข้อเท็จจริง ไม่ชี้นำ**
///
/// เส้นแบ่งนี้คือเหตุผลที่ระบบ "แนะนำ" ถูกถอดออก (ADR-009) — เกมบอกได้ว่า
/// *อะไรเชื่อมกับอะไร และห่างอีกเท่าไร* แต่ไม่บอกว่า *ควรกดใบไหน*
/// ข้อความในนี้จึงต้องเป็นสภาพปัจจุบันเสมอ ห้ามมีคำว่าควร/แนะนำ/คุ้ม
/// </summary>
public struct SynergyLine
{
    public Sprite icon;
    /// <summary>ชื่อของที่เกี่ยว — อาวุธ (บนการ์ดสเตตัส) หรือสเตตัส (บนการ์ดอาวุธ)</summary>
    public string label;
    /// <summary>สภาพตอนนี้ เช่น "Lv5 · ขาด Armor อีก 2" — ว่างได้ถ้าไม่มีอะไรจะบอก</summary>
    public string detail;
    /// <summary>เงื่อนไขฝั่งนี้ครบแล้ว — ใช้ย้อมให้ต่างจากที่ยังขาด</summary>
    public bool   met;
}

/// <summary>ข้อมูลการ์ดที่จะแสดงใน LevelUpUI / ObjectiveRewardUI</summary>
public class UpgradeCardInfo
{
    public UpgradeCardType type;

    // ── Evolution Synergy ─────────────────────────────────────────────────
    /// <summary>บรรทัดที่จะโชว์ในแถบ EVOLUTION — เรียงจากใกล้ครบที่สุดไปหาไกลสุด</summary>
    public System.Collections.Generic.List<SynergyLine> synergyLines = new();
    public bool showSynergy;

    /// <summary>รูปอย่างเดียว — ตัวช่วยสำหรับที่แสดงผลเก่าที่ยังรับแค่ sprite</summary>
    public System.Collections.Generic.List<Sprite> SynergyIcons
    {
        get
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var l in synergyLines) if (l.icon != null) list.Add(l.icon);
            return list;
        }
    }

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
        _                       => weapon?.DisplayName   ?? "???",   // ชื่อบนการ์ด ไม่ใช่ ID
    };

    public Sprite DisplayIcon => type switch
    {
        UpgradeCardType.Augment => augment?.icon,
        UpgradeCardType.Stat    => stat?.Icon,   // Icon = ตกไปใช้รูปกลางของ StatIconSet ให้
        _                       => weapon?.icon,
    };

    public string DisplayDescription
    {
        get
        {
            if (type == UpgradeCardType.Augment)
                return augment?.description ?? "";

            if (type == UpgradeCardType.Stat)
                return stat != null ? stat.Description : "";

            if (weapon == null) return "";

            return weapon.Description;   // แปลแล้วตาม locale
        }
    }

    /// <summary>true = ได้ครั้งแรก (WeaponNew / Super / Fusion / Augment / Stat Lv0)
    /// การ์ดจะโชว์ "คำอธิบาย" อย่างเดียว เพราะยังไม่มีค่าเดิมให้เทียบ
    ///
    /// false = level up ของที่มีอยู่แล้ว การ์ดจะโชว์ "สเตตัสที่เพิ่ม" อย่างเดียว
    /// ผู้เล่นอ่านคำอธิบายไปแล้วตอนได้ครั้งแรก
    ///
    /// เป็นแหล่งความจริงเดียวของกฎนี้ — ทั้งการซ่อน descriptionText และการสร้าง
    /// stat row ใน UpgradeCardUI อ่านจากตัวนี้ จะได้ไม่มีทางเพี้ยนไปคนละทาง</summary>
    public bool IsFirstAcquisition => type switch
    {
        UpgradeCardType.WeaponLevelUp => false,
        UpgradeCardType.Stat          => currentStatLevel == 0,
        _                             => true,
    };

    /// <summary>
    /// ป้ายซ้ายบนของการ์ด — **ระบบที่การ์ดใบนี้มาจาก** ไม่ใช่สิ่งที่จะได้
    ///
    /// คู่กับ <see cref="DisplayLevelText"/> ที่อยู่ขวาบนและบอก "ได้อะไร"
    /// (NEW · Lv 3 / 5 · SUPER · FUSION · SILVER) · แยกกันแบบนี้แล้วไม่มีคำซ้ำ:
    /// การ์ด Super อ่านว่า WEAPON | SUPER  ·  augment อ่านว่า AUGMENT | GOLD
    ///
    /// **Augment เป็นการ์ดเหมือนใบอื่นทุกอย่าง** ต่างแค่ทางที่ได้มา (เฉพาะเลเวลที่
    /// กำหนดไว้ใน SharedExperienceManager.augmentLevels) — ระบบที่ทำงานกับการ์ด
    /// ทั้งกอง (เช่น reroll ในอนาคต) จึงใช้กับมันได้โดยไม่ต้องเขียนทางแยก
    /// </summary>
    public string TypeLabel => type switch
    {
        UpgradeCardType.Stat    => "STAT",
        UpgradeCardType.Augment => "AUGMENT",
        _                       => "WEAPON",
    };

    public string DisplayLevelText => type switch
    {
        UpgradeCardType.WeaponNew     => "NEW",
        UpgradeCardType.WeaponLevelUp => $"Lv {targetLevel} / {weapon?.MaxLevel}",
        // **ห้ามใส่ ★** — ไม่มีฟอนต์ไหนในโปรเจกต์มีกลิฟนี้ TMP วาดเป็นกล่องสี่เหลี่ยม
        // คำว่า SUPER / FUSION กับสีของการ์ดบอกระดับอยู่แล้ว ดาวเป็นของประดับล้วน
        // ถ้าวันหนึ่งเพิ่ม Noto Sans Symbols 2 เป็น fallback ค่อยเอากลับมาได้
        UpgradeCardType.WeaponSuper   => "SUPER",
        UpgradeCardType.WeaponFusion  => "FUSION",
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
