using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI ของ card แต่ละใบใน Level Up panel
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image            iconImage;
    public TextMeshProUGUI  nameText;
    public TextMeshProUGUI  descriptionText;
    public TextMeshProUGUI  stackText;      // "2 / 5"
    public TextMeshProUGUI  valueText;      // "+10 Damage"
    public Button           selectButton;

    private WeaponUpgradeData currentData;

    // ── Setup ─────────────────────────────────────────────────────────────
    public void Populate(WeaponUpgradeData data)
    {
        currentData = data;

        if (iconImage)        iconImage.sprite   = data.icon;
        if (nameText)         nameText.text       = data.upgradeName;
        if (descriptionText)  descriptionText.text = data.description;

        // Stack display
        if (stackText)
        {
            int cur = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetStacks(data) : 0;
            if (data.maxStacks > 0)
            {
                stackText.text = $"Lv {cur + 1} / {data.maxStacks}";
                stackText.gameObject.SetActive(true);
            }
            else
            {
                stackText.gameObject.SetActive(false);
            }
        }

        // Value label  e.g. "+10.0 Damage"  or  "+20% AttackSpeed"
        if (valueText)
        {
            string sign   = data.value >= 0 ? "+" : "";
            string suffix = data.mode == UpgradeApplicationMode.Multiplicative
                ? $"{sign}{data.value * 100f:F0}% {data.upgradeType}"
                : $"{sign}{data.value} {data.upgradeType}";
            valueText.text = suffix;
        }

        // Button listener — clear ก่อนเพื่อป้องกัน duplicate
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelect);
    }

    // ── On Click ──────────────────────────────────────────────────────────
    void OnSelect()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.ApplyUpgrade(currentData);
    }
}
