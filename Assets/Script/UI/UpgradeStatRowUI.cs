using UnityEngine;
using TMPro;

/// <summary>
/// แถบแสดงค่าสเตตัสเปรียบเทียบในแต่ละคุณสมบัติ (เช่น Damage: 10 -> 15)
/// ใช้เป็น Prefab เพื่อนำไป Instantiate ลงในกล่อง Container ของ UpgradeCardUI
/// </summary>
public class UpgradeStatRowUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI beforeText;
    public GameObject      arrowIcon;
    public TextMeshProUGUI afterText;

    /// <summary>ตั้งค่าข้อความและสเตตัสเปรียบเทียบ</summary>
    public void SetData(string statName, string beforeValue, string afterValue)
    {
        if (nameText)   nameText.text   = statName;
        if (beforeText) beforeText.text = beforeValue;
        if (afterText)  afterText.text  = afterValue;
        if (arrowIcon)  arrowIcon.SetActive(true);
    }
}
