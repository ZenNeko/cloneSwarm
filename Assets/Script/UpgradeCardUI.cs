using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI ของ card แต่ละใบใน Level Up / Objective Reward panel
/// รับ UpgradeCardInfo ที่อาจเป็น Weapon หรือ Stat
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image           iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI levelText;   // "Lv 3 / 5" / "NEW" / "SUPER"
    [Tooltip("ป้ายซ้ายบน — ระบบที่การ์ดมาจาก (WEAPON / STAT / AUGMENT)\n\n" +
             "เคยถูกอบค่าไว้ใน prefab แล้วไม่มีใครเขียนตอนรัน — ผลคือทุกใบว่างเปล่า\n" +
             "ปล่อยว่างได้ การ์ดยังบอกชนิดด้วยสีกับป้ายขวาอยู่")]
    public TextMeshProUGUI typeLabel;
    public Button          selectButton;

    [Header("Evolution Synergy")]
    public GameObject      evolutionBadge;
    [Tooltip("แม่แบบหนึ่งบรรทัด — ปิดไว้เสมอ · ถูก clone ตามจำนวนบรรทัดที่ได้รับจริง\n" +
             "ว่าง = ตกกลับไปใช้ synergyIconImages แบบเดิม (โชว์ได้แค่รูป)")]
    public SynergyLineUI   synergyLineTemplate;
    [Tooltip("สีข้อความของเงื่อนไขที่ครบแล้ว")]
    public Color synergyMetColor     = new Color(0.30f, 0.78f, 0.45f, 1f);
    [Tooltip("สีข้อความของเงื่อนไขที่ยังขาด")]
    public Color synergyPendingColor = new Color(1f, 1f, 1f, 0.65f);

    [Tooltip("ช่องไอคอนแบบเก่า — ใช้เฉพาะตอนยังไม่มี synergyLineTemplate")]
    public System.Collections.Generic.List<Image> synergyIconImages = new();

    private readonly System.Collections.Generic.List<SynergyLineUI> spawnedLines = new();

    [Header("UI Elements - Premium Highlights")]
    public GameObject      recommendedGlowOutline;
    public GameObject      recommendedRibbon;
    public Animator        cardAnimator;

    [Header("Comparative Stat Rows (Prefab Mode)")]
    [Tooltip("Prefab ของแถวแสดงสเตตัสเปรียบเทียบ")]
    public UpgradeStatRowUI statRowPrefab;
    [Tooltip("Container สำหรับใส่แถวสเตตัสเปรียบเทียบ")]
    public Transform        statRowsContainer;

    [Header("Visual — Weapon vs Stat")]
    public Image    cardBackground;

    /// <summary>
    /// ชิ้นส่วนอื่นที่ต้องเปลี่ยนสีตามชนิดการ์ดด้วย — กรอบ · พื้นไอคอน · วงเรืองแสง
    ///
    /// **ทำไมต้องมี** — ของเดิมย้อมแค่ <see cref="cardBackground"/> (หัวการ์ด) ส่วนกรอบกับ
    /// พื้นไอคอนถูกอบสีไว้ตอนสร้างซีนตามช่องที่มันอยู่ · การ์ด Stat ตกช่องแรกจึงได้
    /// หัวเขียวแต่กรอบน้ำเงิน · สีเน้นต้องมาจาก **ชนิดของการ์ด** ไม่ใช่จากช่องที่มันไปลง
    ///
    /// <c>alpha</c> แยกต่อชิ้นเพราะพื้นไอคอนใช้สีเดียวกันแต่จางกว่า (0.13) และวงเรืองแสง
    /// จางกว่าอีก (0.18) — เก็บไว้ที่นี่ทำให้ builder ไม่ต้องรู้เรื่องสีเลย
    /// </summary>
    [System.Serializable]
    public class AccentTarget
    {
        public Graphic graphic;
        [Range(0f, 1f)] public float alpha = 1f;
    }

    [Tooltip("ชิ้นที่ต้องย้อมตามชนิดการ์ด นอกเหนือจาก cardBackground")]
    public System.Collections.Generic.List<AccentTarget> accentTargets = new();

    [Header("Recommended")]
    [Tooltip("ยกการ์ดใบที่แนะนำขึ้นกี่ px — 0 = ไม่ยก\n\n" +
             "ของเดิมอบการยกไว้กับช่องกลางตายตัว ส่วนป้ายแนะนำวิ่งตาม isRecommended จริง\n" +
             "สองอย่างจึงหลุดจากกันได้ ผู้เล่นเห็นใบหนึ่งเด่นแต่อีกใบติดป้าย")]
    public float recommendedLift = 22f;
    public Color    weaponColor  = new Color(0.25f, 0.45f, 0.85f);   // น้ำเงิน
    public Color    statColor    = new Color(0.30f, 0.65f, 0.35f);   // เขียว
    public Color    superColor   = new Color(0.85f, 0.60f, 0.10f);   // ทอง
    public Color    fusionColor  = new Color(0.65f, 0.20f, 0.85f);   // ม่วง

    private UpgradeCardInfo            currentCard;
    private Action<UpgradeCardInfo>    onPicked;

    // ── Setup ─────────────────────────────────────────────────────────────
    public void Populate(UpgradeCardInfo card, Action<UpgradeCardInfo> pickedCallback)
    {
        currentCard = card;
        onPicked    = pickedCallback;

        // **ต้องปิด Image เมื่อไม่มีรูป** — Image ที่ไม่มี sprite วาดเป็นสี่เหลี่ยมทึบ
        // ไม่ใช่ว่าง · augment ทุกใบตอนนี้ยังไม่มีไอคอน ถ้าไม่ปิดจะได้กล่องขาวเต็มใบ
        // (บั๊กเดียวกับที่ WeaponStatHUD เคยเป็น) · พื้นไอคอนกับกรอบเป็นคนละ object
        // จึงยังเห็นช่องไอคอนเป็นกรอบเปล่าตามแบบ ไม่ใช่หายไปทั้งก้อน
        if (iconImage)
        {
            iconImage.sprite  = card.DisplayIcon;
            iconImage.enabled = card.DisplayIcon != null;
        }
        if (nameText)        nameText.text = card.DisplayName;
        if (levelText)       levelText.text       = card.DisplayLevelText;
        if (typeLabel)       typeLabel.text       = card.TypeLabel;

        // ได้ครั้งแรก -> คำอธิบายอย่างเดียว · level up -> สเตตัสที่เพิ่มอย่างเดียว
        // สองอย่างนี้ไม่เคยโชว์พร้อมกัน กฎหลักอ่านจาก card.IsFirstAcquisition
        //
        // ข้อยกเว้นเดียว: level up ที่ไม่มีฟิลด์ไหนใน WeaponLevelData เปลี่ยนเลย
        // (เช่น WD_GunnerPassiveWeapon ที่ตั้งค่าเท่ากันทุกเลเวล) จะได้การ์ดว่างทั้งใบ
        // เพราะคำอธิบายก็ถูกซ่อน แถวสเตตัสก็ไม่มี — เคสนี้ตกกลับไปโชว์คำอธิบายแทน
        // จึงต้องคิด changes ก่อน แล้วค่อยตัดสินใจว่าจะโชว์คำอธิบายไหม
        var changes = GetStatChanges(card);
        bool showDescription = card.IsFirstAcquisition || changes.Count == 0;
        if (descriptionText)
        {
            descriptionText.gameObject.SetActive(showDescription);
            if (showDescription) descriptionText.text = card.DisplayDescription;
        }

        // --- ระบบ "แนะนำ" ถูกถอดออกตาม ADR-009 ---
        //
        // วงเรืองแสงเป็นของ **hover เท่านั้น** แล้ว (UpgradeCardGlowOnHover เป็นคนเปิด)
        // ที่นี่ปิดไว้เสมอเพื่อให้สถานะตั้งต้นถูก ไม่ใช่ค้างจากการ์ดใบก่อนที่ถูกใช้ซ้ำ
        if (recommendedGlowOutline)
            recommendedGlowOutline.SetActive(false);

        // ป้าย "แนะนำ" ปิดตายจนกว่าจะถูกถอดออกจาก prefab จริง — ปิดที่นี่ก่อน
        // เพราะ object ยังอยู่ใน LevelUpCard.prefab และถ้าไม่มีใครสั่งปิด
        // มันจะโผล่บนการ์ดทุกใบตามสถานะที่ถูกเซฟไว้
        if (recommendedRibbon)
            recommendedRibbon.SetActive(false);
        if (cardAnimator)
            cardAnimator.SetBool("IsRecommended", false);

        // --- Comparative Stats ---
        // 1. ลบแถวเดิมออกก่อน
        if (statRowsContainer != null)
        {
            foreach (Transform child in statRowsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 2. สร้างแถวใหม่ตามจำนวนสเตตัสที่อัปเกรด
        if (statRowPrefab != null && statRowsContainer != null)
        {
            foreach (var change in changes)
            {
                var row = Instantiate(statRowPrefab, statRowsContainer);
                row.SetData(change.statName, change.beforeValue, change.afterValue);
            }
            statRowsContainer.gameObject.SetActive(changes.Count > 0);
        }

        // Card color — Augment ใช้สีตาม rarity ของตัวเอง
        Color accent = AccentFor(card);
        if (cardBackground) cardBackground.color = accent;

        // กรอบ · พื้นไอคอน · วงเรืองแสง ต้องตามชนิดการ์ดด้วย ไม่ใช่ตามช่องที่มันไปลง
        foreach (var t in accentTargets)
        {
            if (t?.graphic == null) continue;
            t.graphic.color = new Color(accent.r, accent.g, accent.b, t.alpha);
        }

        // การยกใบที่แนะนำหายไปพร้อมระบบแนะนำ — การ์ดเรียบระดับเดียวกันเสมอ
        ApplyRecommendedLift(false);

        PopulateSynergy(card);

        if (selectButton)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelect);
        }
    }

    // ── Get Stat Changes ──────────────────────────────────────────────────
    private struct StatChangeInfo
    {
        public string statName;
        public string beforeValue;
        public string afterValue;
    }

    System.Collections.Generic.List<StatChangeInfo> GetStatChanges(UpgradeCardInfo card)
    {
        var list = new System.Collections.Generic.List<StatChangeInfo>();

        // ได้ครั้งแรก (WeaponNew / Super / Fusion / Augment / Stat Lv0) ยังไม่มีค่าเดิม
        // ให้เทียบ การ์ดโชว์คำอธิบายแทน
        if (card.IsFirstAcquisition) return list;

        if (card.type == UpgradeCardType.Stat && card.stat != null)
        {
            float prevVal = GetAccumulatedStatValue(card.stat, card.currentStatLevel);
            float newVal  = GetAccumulatedStatValue(card.stat, card.currentStatLevel + 1);

            string name = card.stat.statType switch
            {
                StatType.Damage          => "Damage",
                StatType.AbilityHaste    => "Haste",
                StatType.CriticalChance  => "Crit Chance",
                StatType.AreaSize        => "Area Size",
                StatType.ProjectileCount => "Projectiles",
                StatType.Duration        => "Duration",
                StatType.MaxHealth       => "Max HP",
                StatType.Armor           => "Armor",
                StatType.HealthRegen     => "HP Regen",
                StatType.MoveSpeed       => "Move Speed",
                StatType.PickupRadius    => "Pickup Radius",
                StatType.ExpBonus        => "EXP Bonus",
                StatType.GainGold        => "Full Build",
                StatType.HealOnFullBuild => "Full Build",
                _                        => card.stat.statName
            };

            string before = FormatStatVal(card.stat.statType, prevVal, false);
            string after  = FormatStatVal(card.stat.statType, newVal,  false);

            if (card.stat.statType == StatType.GainGold)
            {
                before = "0";
                after = "+25 Gold";
            }
            else if (card.stat.statType == StatType.HealOnFullBuild)
            {
                before = "0";
                after = "Heal 25%";
            }

            list.Add(new StatChangeInfo { statName = name, beforeValue = before, afterValue = after });
        }
        else if (card.weapon != null)
        {
            if (card.type == UpgradeCardType.WeaponLevelUp)
            {
                var curLd  = card.weapon.GetLevelData(card.targetLevel - 2);
                var nextLd = card.weapon.GetLevelData(card.targetLevel - 1);

                if (curLd.damage != nextLd.damage)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Damage",
                        beforeValue = curLd.damage.ToString("F0"),
                        afterValue = nextLd.damage.ToString("F0")
                    });
                }
                if (curLd.cooldown != nextLd.cooldown)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Cooldown",
                        beforeValue = curLd.cooldown.ToString("F1") + "s",
                        afterValue = nextLd.cooldown.ToString("F1") + "s"
                    });
                }
                if (curLd.projectileCount != nextLd.projectileCount)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Projectiles",
                        beforeValue = "x" + curLd.projectileCount,
                        afterValue = "x" + nextLd.projectileCount
                    });
                }
                if (curLd.range != nextLd.range)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Range",
                        beforeValue = curLd.range.ToString("F1") + "m",
                        afterValue = nextLd.range.ToString("F1") + "m"
                    });
                }
                if (curLd.projectileSpeed != nextLd.projectileSpeed)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Speed",
                        beforeValue = curLd.projectileSpeed.ToString("F0"),
                        afterValue = nextLd.projectileSpeed.ToString("F0")
                    });
                }

                // radius / duration ที่เป็น 0 แปลว่า "ใช้ค่าเริ่มต้นใน Script" ไม่ใช่ศูนย์จริง
                // (ดู tooltip ใน WeaponLevelData) โชว์เป็น "-" แทนเลข 0 จะได้ไม่โกหกตัวเลข
                if (curLd.radius != nextLd.radius)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Area",
                        beforeValue = curLd.radius  > 0f ? curLd.radius.ToString("F1")  + "m" : "-",
                        afterValue  = nextLd.radius > 0f ? nextLd.radius.ToString("F1") + "m" : "-"
                    });
                }
                if (curLd.duration != nextLd.duration)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Duration",
                        beforeValue = curLd.duration  > 0f ? curLd.duration.ToString("F1")  + "s" : "-",
                        afterValue  = nextLd.duration > 0f ? nextLd.duration.ToString("F1") + "s" : "-"
                    });
                }
                if (curLd.piercing != nextLd.piercing)
                {
                    list.Add(new StatChangeInfo {
                        statName = "Piercing",
                        beforeValue = curLd.piercing  ? "Yes" : "No",
                        afterValue  = nextLd.piercing ? "Yes" : "No"
                    });
                }
            }
        }

        return list;
    }

    float GetAccumulatedStatValue(StatData stat, int level)
    {
        if (stat == null || level <= 0) return 0f;

        if (stat.statType == StatType.ProjectileCount)
        {
            return stat.GetValueAtLevel(level - 1);
        }

        float sum = 0f;
        for (int i = 0; i < level; i++)
        {
            sum += stat.GetValueAtLevel(i);
        }
        return sum;
    }

    string FormatStatVal(StatType type, float val, bool isNew)
    {
        if (val == 0f && isNew) return "0";
        string prefix = val > 0f ? "+" : "";
        return type switch
        {
            StatType.Damage          => $"{prefix}{val * 100f:F0}%",
            StatType.AbilityHaste    => $"{prefix}{val:F0}",
            StatType.CriticalChance  => $"{prefix}{val * 100f:F0}%",
            StatType.AreaSize        => $"{prefix}{val * 100f:F0}%",
            StatType.ProjectileCount => $"{val:F0}",
            StatType.Duration        => $"{prefix}{val * 100f:F0}%",
            StatType.MaxHealth       => $"{prefix}{val:F0}",
            StatType.Armor           => $"{prefix}{val:F0}",
            StatType.HealthRegen     => $"{prefix}{val:F1}",
            StatType.MoveSpeed       => $"{prefix}{val * 100f:F0}%",
            StatType.PickupRadius    => $"{prefix}{val * 100f:F0}%",
            StatType.ExpBonus        => $"{prefix}{val * 100f:F0}%",
            _                        => $"{val:F1}"
        };
    }

    // ── Accent / Lift ─────────────────────────────────────────────────────
    /// <summary>สีเน้นของการ์ด — แหล่งความจริงเดียวของทั้งหัวการ์ด กรอบ และพื้นไอคอน</summary>
    Color AccentFor(UpgradeCardInfo card) => card.type switch
    {
        UpgradeCardType.Augment      => card.augment != null ? card.augment.RarityColor : weaponColor,
        UpgradeCardType.WeaponSuper  => superColor,
        UpgradeCardType.WeaponFusion => fusionColor,
        UpgradeCardType.Stat         => statColor,
        _                            => weaponColor
    };

    // ══════════════════════════════════════════════════════════════════════
    // EVOLUTION SYNERGY
    //
    // ระบบ "แนะนำ" ถูกถอดออก (ADR-009) แถบนี้จึงเป็น **ช่องทางเดียว** ที่เกม
    // ใช้บอกทางผู้เล่น · มันต้องแบกข้อมูลได้มากกว่าไอคอนเปล่าๆ แบบเดิม
    //
    // สร้างบรรทัดตามจำนวนที่ได้รับจริงจาก template แทนการมีช่องตายตัว —
    // ของเดิมมีช่องฝังไว้ 2 ช่องขณะที่ฝั่งข้อมูลส่งมาได้ถึง 3 บรรทัดนี้จึง
    // **หายเงียบ** ทุกครั้งที่มีบรรทัดที่สาม (บั๊กเดียวกับที่ EnsureCardSlots แก้ไป)
    // ══════════════════════════════════════════════════════════════════════
    void PopulateSynergy(UpgradeCardInfo card)
    {
        bool has = card.showSynergy && card.synergyLines != null && card.synergyLines.Count > 0;
        if (evolutionBadge) evolutionBadge.SetActive(has);
        if (!has) { HideSpawnedLines(0); return; }

        // ยังไม่มี template ในซีน → ตกกลับไปใช้ช่องไอคอนตายตัวแบบเดิม
        // (การ์ดรุ่นเก่าที่ยังไม่ถูกสร้างใหม่จาก builder ต้องไม่พังไปด้วย)
        if (synergyLineTemplate == null)
        {
            FallbackIconsOnly(card);
            return;
        }

        EnsureLines(card.synergyLines.Count);

        for (int i = 0; i < spawnedLines.Count; i++)
        {
            bool used = i < card.synergyLines.Count;
            spawnedLines[i].gameObject.SetActive(used);
            if (used) spawnedLines[i].SetData(card.synergyLines[i], synergyMetColor, synergyPendingColor);
        }
    }

    void EnsureLines(int count)
    {
        while (spawnedLines.Count < count)
        {
            var line = Instantiate(synergyLineTemplate, synergyLineTemplate.transform.parent);
            line.name = $"SynergyLine_{spawnedLines.Count}";
            spawnedLines.Add(line);
        }
    }

    void HideSpawnedLines(int from)
    {
        for (int i = from; i < spawnedLines.Count; i++)
            if (spawnedLines[i] != null) spawnedLines[i].gameObject.SetActive(false);
    }

    /// <summary>ทางหนีสำหรับการ์ดที่ยังไม่มี template — โชว์ได้แค่รูป ไม่มีข้อความ</summary>
    void FallbackIconsOnly(UpgradeCardInfo card)
    {
        if (synergyIconImages == null) return;
        var icons = card.SynergyIcons;
        for (int i = 0; i < synergyIconImages.Count; i++)
        {
            if (synergyIconImages[i] == null) continue;
            bool used = i < icons.Count && icons[i] != null;
            synergyIconImages[i].gameObject.SetActive(used);
            if (used) synergyIconImages[i].sprite = icons[i];
        }

        if (icons.Count > synergyIconImages.Count)
            Debug.LogWarning($"[UpgradeCard] synergy {icons.Count} บรรทัด แต่การ์ดมีช่อง " +
                             $"{synergyIconImages.Count} — ที่เกินหายไป · " +
                             "สร้างการ์ดใหม่จาก builder เพื่อให้ได้ SynergyLine template");
    }

    /// <summary>
    /// ยกใบที่แนะนำขึ้นตาม <see cref="recommendedLift"/>
    ///
    /// จำ y ตั้งต้นไว้ครั้งแรกครั้งเดียว — ถ้าอ่านค่าปัจจุบันทุกครั้งแล้วบวกเพิ่ม
    /// การ์ดจะไต่ขึ้นเรื่อยๆ ทุกรอบที่ถูก Populate ซ้ำ
    /// </summary>
    void ApplyRecommendedLift(bool recommended)
    {
        if (Mathf.Approximately(recommendedLift, 0f)) return;
        if (transform is not RectTransform rt) return;

        if (!baseYCaptured) { baseY = rt.anchoredPosition.y; baseYCaptured = true; }
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                          baseY + (recommended ? recommendedLift : 0f));
    }

    private float baseY;
    private bool  baseYCaptured;

    // ── Click ─────────────────────────────────────────────────────────────
    void OnSelect() => onPicked?.Invoke(currentCard);
}
