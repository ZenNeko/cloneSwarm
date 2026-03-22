using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI ของ card แต่ละใบใน Level Up panel
/// ไม่รู้จัก UpgradeManager โดยตรง — ใช้ callback แทน
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image            iconImage;
    public TextMeshProUGUI  nameText;
    public TextMeshProUGUI  descriptionText;
    public TextMeshProUGUI  stackText;   // "Lv 2 / 5"
    public TextMeshProUGUI  valueText;   // "+10 Damage"
    public Button           selectButton;

    private WeaponUpgradeData          currentData;
    private Action<WeaponUpgradeData>  onPicked;

    // ── Setup ─────────────────────────────────────────────────────────────
    /// <param name="data">ข้อมูล upgrade</param>
    /// <param name="currentStack">จำนวน stack ที่ apply แล้ว (ส่งมาจาก UpgradeManager)</param>
    /// <param name="pickedCallback">เรียกเมื่อ player กด</param>
    public void Populate(WeaponUpgradeData data, int currentStack, Action<WeaponUpgradeData> pickedCallback)
    {
        currentData = data;
        onPicked    = pickedCallback;

        if (iconImage)       iconImage.sprite    = data.icon;
        if (nameText)        nameText.text        = data.upgradeName;
        if (descriptionText) descriptionText.text = data.description;

        // Stack display
        if (stackText)
        {
            if (data.maxStacks > 0)
            {
                stackText.text = $"Lv {currentStack + 1} / {data.maxStacks}";
                stackText.gameObject.SetActive(true);
            }
            else
            {
                stackText.gameObject.SetActive(false);
            }
        }

        // Value label  "+10 Damage"  /  "+20% AttackSpeed"
        if (valueText)
        {
            string sign = data.value >= 0 ? "+" : "";
            valueText.text = data.mode == UpgradeApplicationMode.Multiplicative
                ? $"{sign}{data.value * 100f:F0}% {data.upgradeType}"
                : $"{sign}{data.value} {data.upgradeType}";
        }

        // Button — clear listener ก่อนเพื่อป้องกัน duplicate
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelect);
    }

    // ── On Click ──────────────────────────────────────────────────────────
    void OnSelect() => onPicked?.Invoke(currentData);
}
