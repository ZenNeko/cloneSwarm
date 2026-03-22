using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ควบคุม Panel แสดง upgrade cards เมื่อ Level Up
/// เป็น pure UI — ไม่ยุ่งกับ Time.timeScale หรือ game logic
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("Root GameObject ของ Panel ทั้งหมด — ตั้งค่า Inactive ไว้เริ่มต้น")]
    public GameObject panelRoot;

    [Header("Header")]
    public TextMeshProUGUI levelLabel;     // "LEVEL UP! → Level 5"

    [Header("Cards")]
    [Tooltip("ลาก UpgradeCardUI ทั้ง 3 ใบมาใส่ที่นี่")]
    public List<UpgradeCardUI> cardSlots;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot) panelRoot.SetActive(false);
    }

    // ── Public API (เรียกจาก UpgradeManager) ──────────────────────────────
    /// <param name="upgrades">รายการ upgrade ที่สุ่มได้</param>
    /// <param name="stackLookup">fn(upgrade) → stack ปัจจุบัน — ส่งมาจาก UpgradeManager</param>
    /// <param name="onPicked">callback เมื่อ player เลือก card</param>
    /// <param name="level">Level ใหม่ที่ขึ้น (0 = ไม่แสดง)</param>
    public void Show(
        List<WeaponUpgradeData>                   upgrades,
        System.Func<WeaponUpgradeData, int>       stackLookup,
        System.Action<WeaponUpgradeData>          onPicked,
        int level = 0)
    {
        if (levelLabel)
            levelLabel.text = level > 0 ? $"LEVEL UP!   Level {level}" : "LEVEL UP!";

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i < upgrades.Count)
            {
                cardSlots[i].gameObject.SetActive(true);
                cardSlots[i].Populate(upgrades[i], stackLookup(upgrades[i]), onPicked);
            }
            else
            {
                cardSlots[i].gameObject.SetActive(false);
            }
        }

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }
}
