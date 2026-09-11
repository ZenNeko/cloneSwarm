using UnityEngine;
using UnityEngine.Localization;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// Talent ทำงานยังไง
    ///
    /// ส่วนใหญ่เป็น <see cref="Stat"/> คือบวกเข้า <see cref="StatType"/> ตรงๆ —
    /// ไม่ต้องมี enum ของตัวเองซ้ำกับ StatData
    /// เหลือแค่ 2 อย่างที่ไม่ใช่ stat จึงต้องมี mode แยก
    /// </summary>
    public enum TalentEffectMode
    {
        Stat,           // บวกเข้า StatType ที่ระบุใน statType
        GoldFind,       // ตัวคูณทองท้ายเกม (RunRewardTracker อ่าน)
        SecondChance,   // ชุบชีวิต 1 ครั้ง/เกม (playermove.Die อ่าน)
    }

    /// <summary>หมวดในร้าน — ใช้จัดกลุ่มตอนแสดงผลเท่านั้น</summary>
    public enum TalentCategory
    {
        Combat,
        Survival,
        Utility,
        Meta,
    }

    /// <summary>
    /// Talent ถาวรใน Talent Shop — Assets &gt; Create &gt; LoL Swarm/Meta/Talent Data
    ///
    /// ค่าเป็น **per level** และสะสมเชิงบวก (level 3 = valuePerLevel × 3)
    /// ยกเว้น SecondChance ที่เป็น flag ล้วน (costPerLevel ความยาว 1)
    ///
    /// mode = Stat → ค่าไปเข้า <see cref="PlayerStatManager.AddPermanentBonus"/>
    /// จึงใช้หน่วยเดียวกับ <c>StatData.valuePerLevel</c> เป๊ะ:
    ///   % → decimal (0.03 = +3%) · flat → ใส่ตรงๆ
    /// </summary>
    [CreateAssetMenu(fileName = "Talent_New", menuName = "LoL Swarm/Meta/Talent Data")]
    public class TalentData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("คีย์ถาวรที่เขียนลงไฟล์เซฟ — **ห้ามเปลี่ยนหลังปล่อยเกม** ไม่งั้นผู้เล่นเสียเลเวลที่ซื้อไว้")]
        public string talentId = "talent_new";
        [Tooltip("ใช้ใน log และเป็นค่าตั้งต้นเมื่อยังไม่ได้ผูก displayName — ไม่ต้องแปล")]
        public string talentName = "New Talent";

        [Header("Display — แปลได้ ชี้ไป String Table 'Content'")]
        public LocalizedString displayName;
        public LocalizedString description;

        /// <summary>ข้อความที่แปลแล้วตาม locale ปัจจุบัน — ว่างเมื่อยังไม่ได้ผูก entry</summary>
        public string DisplayName => displayName.IsEmpty ? talentName : displayName.GetLocalizedString();
        public string Description  => description.IsEmpty ? "" : description.GetLocalizedString();
        public Sprite icon;

        [Header("Effect")]
        [Tooltip("ปกติใช้ Stat — อีก 2 อันไว้สำหรับผลที่ไม่ใช่สเตตัส")]
        public TalentEffectMode mode = TalentEffectMode.Stat;
        [Tooltip("ใช้เมื่อ mode = Stat — ประเภทเดียวกับ StatData เป๊ะ")]
        public StatType statType = StatType.Damage;
        [Tooltip("ค่าที่ได้ต่อ 1 เลเวล (หน่วยเดียวกับ StatData.valuePerLevel)\n" +
                 "% → decimal (0.03 = +3%)  ·  flat → ใส่ตรงๆ\n" +
                 "SecondChance → ไม่ใช้ (เป็น flag)")]
        public float valuePerLevel = 0.03f;

        [Header("Cost")]
        [Tooltip("ราคาทองของแต่ละเลเวล — index 0 = ซื้อเลเวล 1\n" +
                 "ความยาว array = max level ของ talent นี้")]
        public int[] costPerLevel = { 100, 200, 400, 800, 1600 };

        [Header("Display")]
        public TalentCategory category = TalentCategory.Combat;
        [Tooltip("ลำดับที่แสดงในร้าน — น้อยขึ้นก่อน (เรียงเป็น grid ซ้าย→ขวา บน→ล่าง)")]
        public int sortOrder = 0;
        [Tooltip("สีของไอคอนในช่อง — ใช้บอกหมวดแบบไม่ต้องมีหัวข้อคั่น")]
        public Color tintColor = Color.white;

        // ── Helpers ───────────────────────────────────────────────────────
        public int MaxLevel => costPerLevel != null ? costPerLevel.Length : 0;

        /// <summary>ราคาของการอัปจาก currentLevel → currentLevel+1 (คืน -1 ถ้าตันแล้ว)</summary>
        public int GetCostToUpgrade(int currentLevel)
        {
            if (costPerLevel == null || currentLevel < 0 || currentLevel >= costPerLevel.Length)
                return -1;
            return costPerLevel[currentLevel];
        }

        /// <summary>
        /// บรรทัด "ได้เท่าไรต่อเลเวล" ใต้ช่อง NOW/NEXT — คืนค่าว่างเมื่อไม่มีความหมาย
        ///
        /// แผงรายละเอียดเคยฮาร์ดโค้ดบรรทัดนี้ไว้ว่า `DAMAGE +3% / LEVEL` แล้วไม่มีใคร
        /// อัปเดตเลย · talent ทุกตัวที่ไม่ใช่ Damage จึงโชว์ตัวเลขของคนอื่นมาตลอด
        /// </summary>
        public string FormatPerLevel()
        {
            if (mode == TalentEffectMode.SecondChance) return "";
            if (mode == TalentEffectMode.GoldFind)     return $"+{valuePerLevel * 100f:F0}% Gold / LEVEL";
            return $"{FormatStatValue(statType, valuePerLevel)} / LEVEL";
        }

        /// <summary>ค่าผลรวมที่ level นี้</summary>
        public float GetTotalValue(int level) => valuePerLevel * Mathf.Clamp(level, 0, MaxLevel);

        public string CategoryLabel => category switch
        {
            TalentCategory.Combat   => "COMBAT",
            TalentCategory.Survival => "SURVIVAL",
            TalentCategory.Utility  => "UTILITY",
            TalentCategory.Meta     => "META",
            _                       => "",
        };

        // ═══════════════════════════════════════════════════════════════════
        // Display formatting
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ข้อความของ "ค่าที่จะได้ถ้าซื้อเพิ่มอีก 1 เลเวล" — panel รายละเอียดใช้โชว์ ก่อน › หลัง
        /// คืนค่าว่างถ้าตันแล้ว
        /// </summary>
        public string FormatNextValue(int level)
            => level >= MaxLevel ? "" : FormatValue(level + 1);

        /// <summary>
        /// เฉพาะ **ตัวเลข** ไม่มีชื่อสเตตัส — `"+9%"` แทน `"+9% Damage"`
        ///
        /// แผงรายละเอียดวาง `NOW ‹ค่า› › NEXT ‹ค่า›` ไว้เรียงกันในแนวนอน กว้างรวม 556px
        /// ชื่อสเตตัสเต็มยาวเกินกว่าจะใส่ได้สองรอบ (`+0% PICKUP RADIUS` อย่างเดียวก็ล้นแล้ว)
        /// และมันซ้ำกับบรรทัด `DAMAGE +3% / LEVEL` ที่อยู่ใต้ลงไปอีกสองบรรทัด
        ///
        /// เทียบ `FormatValue` ที่ยังคืนชื่อเต็ม — การ์ดอัปเกรดในเกมโชว์ทีละใบ มีที่ให้ชื่อ
        /// </summary>
        public string FormatValueShort(int level)
        {
            // สั้นจริงๆ — ช่อง NOW/NEXT กว้าง 184px ที่ฟอนต์ 30 "1 ครั้ง/เกม" โดนตัดเป็น "1 คร้."
            if (mode == TalentEffectMode.SecondChance)
                return level > 0 ? "1 ครั้ง" : "—";

            float v = GetTotalValue(level);
            if (mode == TalentEffectMode.GoldFind) return $"+{v * 100f:F0}%";

            return FormatStatMagnitude(statType, v);
        }

        /// <summary>คู่กับ <see cref="FormatValueShort"/> — คืนค่าว่างเมื่อตันแล้ว</summary>
        public string FormatNextValueShort(int level)
            => level >= MaxLevel ? "" : FormatValueShort(level + 1);

        /// <summary>ข้อความสรุปผลที่ level นี้ เช่น "+9% Damage" — ร้านค้าใช้</summary>
        public string FormatValue(int level)
        {
            if (mode == TalentEffectMode.SecondChance)
                return level > 0 ? "ชุบชีวิต 1 ครั้ง/เกม" : "—";

            float v = GetTotalValue(level);

            if (mode == TalentEffectMode.GoldFind)
                return $"+{v * 100f:F0}% Gold";

            return FormatStatValue(statType, v);
        }

        /// <summary>
        /// จัดรูปแบบตามหน่วยของ StatType — ใช้ตารางเดียวกับที่การ์ดอัปเกรดในเกมใช้
        /// เพื่อให้ผู้เล่นเห็นตัวเลขหน่วยเดียวกันทั้งในร้านและในเกม
        /// </summary>
        public static string FormatStatValue(StatType type, float v)
        {
            string label = StatLabel(type);
            string mag   = FormatStatMagnitude(type, v);
            return string.IsNullOrEmpty(label) ? mag : $"{mag} {label}";
        }

        /// <summary>
        /// ตัวเลขกับหน่วยของมัน ไม่มีชื่อสเตตัส — `"+9%"` · `"+5"` · `"+1.5"`
        /// แยกจาก <see cref="StatLabel"/> เพื่อให้ที่แคบโชว์แค่ตัวเลขได้ โดยที่
        /// ตารางหน่วยยังอยู่ที่เดียว · ต่อกลับเป็นข้อความเต็มได้เสมอด้วย FormatStatValue
        /// </summary>
        public static string FormatStatMagnitude(StatType type, float v)
        {
            string sign = v >= 0f ? "+" : "";
            return type switch
            {
                StatType.Damage          => $"{sign}{v * 100f:F0}%",
                StatType.CriticalChance  => $"{sign}{v * 100f:F0}%",
                StatType.AreaSize        => $"{sign}{v * 100f:F0}%",
                StatType.Duration        => $"{sign}{v * 100f:F0}%",
                StatType.MoveSpeed       => $"{sign}{v * 100f:F0}%",
                StatType.PickupRadius    => $"{sign}{v * 100f:F0}%",
                StatType.ExpBonus        => $"{sign}{v * 100f:F0}%",
                StatType.AbilityHaste    => $"{sign}{v:F0}",
                StatType.ProjectileCount => $"{sign}{v:F0}",
                StatType.MaxHealth       => $"{sign}{v:F0}",
                StatType.Armor           => $"{sign}{v:F0}",
                StatType.HealthRegen     => $"{sign}{v:F1}",
                _                        => $"{sign}{v:F2}",
            };
        }

        /// <summary>ชื่อสเตตัสที่ต่อท้ายตัวเลข — ค่าว่างแปลว่าตัวเลขอ่านได้ด้วยตัวเอง</summary>
        public static string StatLabel(StatType type) => type switch
        {
            StatType.Damage          => "Damage",
            StatType.AbilityHaste    => "Ability Haste",
            StatType.CriticalChance  => "Crit Chance",
            StatType.AreaSize        => "Area Size",
            StatType.ProjectileCount => "Projectile",
            StatType.Duration        => "Duration",
            StatType.MaxHealth       => "Max HP",
            StatType.Armor           => "Armor",
            StatType.HealthRegen     => "HP/s",
            StatType.MoveSpeed       => "Move Speed",
            StatType.PickupRadius    => "Pickup Radius",
            StatType.ExpBonus        => "EXP",
            _                        => "",
        };
    }
}
