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
    public TextMeshProUGUI levelText;   // "Lv 3 / 5" / "NEW" / "SUPER ★"
    public Button          selectButton;

    [Header("Evolution Synergy")]
    public GameObject      evolutionBadge;
    public System.Collections.Generic.List<Image> synergyIconImages = new();

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

        if (iconImage)       iconImage.sprite    = card.DisplayIcon;
        if (nameText)        nameText.text = card.DisplayName;
        if (descriptionText) descriptionText.text = card.DisplayDescription;
        if (levelText)       levelText.text       = card.DisplayLevelText;

        // --- Premium Highlights ---
        if (recommendedGlowOutline)
            recommendedGlowOutline.SetActive(card.isRecommended);
        if (recommendedRibbon)
            recommendedRibbon.SetActive(card.isRecommended);
        if (cardAnimator)
            cardAnimator.SetBool("IsRecommended", card.isRecommended);

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
            var changes = GetStatChanges(card);
            foreach (var change in changes)
            {
                var row = Instantiate(statRowPrefab, statRowsContainer);
                row.SetData(change.statName, change.beforeValue, change.afterValue);
            }
        }

        // Card color — Augment ใช้สีตาม rarity ของตัวเอง
        if (cardBackground)
        {
            cardBackground.color = card.type switch
            {
                UpgradeCardType.Augment       => card.augment != null ? card.augment.RarityColor : weaponColor,
                UpgradeCardType.WeaponSuper   => superColor,
                UpgradeCardType.WeaponFusion  => fusionColor,
                UpgradeCardType.Stat          => statColor,
                _                             => weaponColor
            };
        }

        // --- Evolution Synergy Panel ---
        if (evolutionBadge)
        {
            bool hasSynergy = card.showSynergy && card.synergyIcons != null && card.synergyIcons.Count > 0;
            evolutionBadge.SetActive(hasSynergy);
            if (hasSynergy && synergyIconImages != null)
            {
                for (int i = 0; i < synergyIconImages.Count; i++)
                {
                    if (synergyIconImages[i] != null)
                    {
                        if (i < card.synergyIcons.Count && card.synergyIcons[i] != null)
                        {
                            synergyIconImages[i].gameObject.SetActive(true);
                            synergyIconImages[i].sprite = card.synergyIcons[i];
                        }
                        else
                        {
                            synergyIconImages[i].gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

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

        if (card.type == UpgradeCardType.Stat && card.stat != null)
        {
            // ได้ครั้งแรก (currentStatLevel == 0) -> ไม่ต้องแสดงแถบพลังเปรียบเทียบ
            if (card.currentStatLevel == 0) return list;

            float prevVal = GetAccumulatedStatValue(card.stat, card.currentStatLevel);
            float newVal  = GetAccumulatedStatValue(card.stat, card.currentStatLevel + 1);
            bool isNew = card.currentStatLevel == 0;

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

            string before = isNew ? "0" : FormatStatVal(card.stat.statType, prevVal, false);
            string after = FormatStatVal(card.stat.statType, newVal, isNew);

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
                        afterValue = curLd.projectileSpeed.ToString("F0")
                    });
                }
            }
            // WeaponNew, Super, Fusion (ได้ครั้งแรก) -> ไม่ต้องแสดงสเตตัสเปรียบเทียบ
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

    // ── Click ─────────────────────────────────────────────────────────────
    void OnSelect() => onPicked?.Invoke(currentCard);
}
