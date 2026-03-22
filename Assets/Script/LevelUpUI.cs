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
    public void Show(List<WeaponUpgradeData> upgrades)
    {
        // อัป header
        if (levelLabel && ExperienceManager.Instance != null)
            levelLabel.text = $"LEVEL UP!   Level {ExperienceManager.Instance.GetCurrentLevel()}";

        // ตั้ง card slots
        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (i < upgrades.Count)
            {
                cardSlots[i].gameObject.SetActive(true);
                cardSlots[i].Populate(upgrades[i]);
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
