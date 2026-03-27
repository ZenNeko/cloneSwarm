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
    public TextMeshProUGUI valueText;   // "+10 damage" หรือ "+15% ATK SPD"
    public Button          selectButton;

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
        if (nameText)        nameText.text        = card.DisplayName;
        if (descriptionText) descriptionText.text = card.DisplayDescription;
        if (levelText)       levelText.text       = card.DisplayLevelText;

        // Value label
        if (valueText) valueText.text = BuildValueText(card);

        // Card color
        if (cardBackground)
        {
            cardBackground.color = card.type switch
            {
                UpgradeCardType.WeaponSuper   => superColor,
                UpgradeCardType.WeaponFusion  => fusionColor,
                UpgradeCardType.Stat          => statColor,
                _                             => weaponColor
            };
        }

        if (selectButton)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelect);
        }
    }

    // ── Value Text ────────────────────────────────────────────────────────
    string BuildValueText(UpgradeCardInfo card)
    {
        if (card.type == UpgradeCardType.Stat && card.stat != null)
        {
            float val = card.stat.GetValueAtLevel(card.currentStatLevel);
            return card.stat.statType switch
            {
                StatType.Damage          => $"+{val * 100f:F0}% Damage",
                StatType.AbilityHaste    => $"+{val:F0} Ability Haste",
                StatType.CriticalChance  => $"+{val * 100f:F0}% Crit Chance",
                StatType.AreaSize        => $"+{val * 100f:F0}% Area Size",
                StatType.ProjectileCount => $"Proj Count → {val:F0}",
                StatType.Duration        => $"+{val * 100f:F0}% Duration",
                StatType.MaxHealth       => $"+{val:F0} Max HP",
                StatType.Armor           => $"+{val:F0} Armor",
                StatType.HealthRegen     => $"+{val:F1} HP/s",
                StatType.MoveSpeed       => $"+{val * 100f:F0}% Move Speed",
                StatType.PickupRadius    => $"+{val * 100f:F0}% Pickup Radius",
                StatType.ExpBonus        => $"+{val * 100f:F0}% EXP",
                StatType.GainGold        => $"+25 Gold (Full Build)",
                StatType.HealOnFullBuild => $"Heal 25% HP (Full Build)",
                _                        => $"+{val}"
            };
        }

        if (card.weapon != null)
        {
            var ld = card.weapon.GetLevelData(
                card.type == UpgradeCardType.WeaponNew ? 0 : card.targetLevel - 1);
            return $"DMG {ld.damage:F0}  CD {ld.cooldown:F2}s  ×{ld.projectileCount}";
        }

        return "";
    }

    // ── Click ─────────────────────────────────────────────────────────────
    void OnSelect() => onPicked?.Invoke(currentCard);
}
