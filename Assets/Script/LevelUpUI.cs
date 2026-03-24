using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ควบคุม Panel แสดง upgrade cards เมื่อ Level Up
/// - แสดง countdown timer (รับ event จาก SharedExperienceManager.OnTimerTick)
/// - แสดงจำนวนคนที่เลือกแล้ว "X / Y ผู้เล่น"
/// - ไม่ยุ่งกับ Time.timeScale — SharedExperienceManager จัดการแทน
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("Panel")]
    [Tooltip("Root GameObject ของ Panel ทั้งหมด — ตั้งค่า Inactive ไว้เริ่มต้น")]
    public GameObject panelRoot;

    [Header("Cards Section")]
    [Tooltip("GameObject ที่ครอบ levelLabel + cardSlots ทั้งหมด — ซ่อนหลังเลือกแล้ว แต่ panelRoot ยังเปิดอยู่")]
    public GameObject cardsSection;
    public TextMeshProUGUI levelLabel;      // "LEVEL UP!  →  Level 5"
    [Tooltip("ลาก UpgradeCardUI ทั้ง 3 ใบมาใส่ที่นี่")]
    public List<UpgradeCardUI> cardSlots;

    [Header("Timer")]
    [Tooltip("แสดง countdown  เช่น '28'  — ซ่อนได้ถ้าไม่ต้องการ")]
    public TextMeshProUGUI timerLabel;
    [Tooltip("สีปกติของตัวเลข timer")]
    public Color timerNormalColor  = Color.white;
    [Tooltip("สีเมื่อเหลือเวลาน้อย (< urgentThreshold วินาที)")]
    public Color timerUrgentColor  = new Color(1f, 0.3f, 0.2f);   // แดง
    [Tooltip("วินาทีที่เปลี่ยนเป็นสีด่วน")]
    public float urgentThreshold   = 10f;

    [Header("Waiting Status")]
    [Tooltip("แสดงจำนวนคนที่เลือกแล้ว  เช่น 'รอผู้เล่น: 1 / 2'")]
    public TextMeshProUGUI waitingLabel;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (panelRoot) panelRoot.SetActive(false);
    }

    void OnEnable()
    {
        SharedExperienceManager.OnTimerTick         += UpdateTimer;
        SharedExperienceManager.OnPickedCountChanged += UpdateWaiting;
        SharedExperienceManager.OnUpgradePhaseEnd   += Hide;
    }

    void OnDisable()
    {
        SharedExperienceManager.OnTimerTick         -= UpdateTimer;
        SharedExperienceManager.OnPickedCountChanged -= UpdateWaiting;
        SharedExperienceManager.OnUpgradePhaseEnd   -= Hide;
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <param name="upgrades">รายการ upgrade ที่สุ่มได้</param>
    /// <param name="stackLookup">fn(upgrade) → stack ปัจจุบัน</param>
    /// <param name="onPicked">callback เมื่อ player เลือก card</param>
    /// <param name="level">Level ใหม่ที่ขึ้น (0 = ไม่แสดงตัวเลข)</param>
    public void Show(
        List<WeaponUpgradeData>              upgrades,
        System.Func<WeaponUpgradeData, int>  stackLookup,
        System.Action<WeaponUpgradeData>     onPicked,
        int level = 0)
    {
        // Header
        if (levelLabel)
            levelLabel.text = level > 0 ? $"LEVEL UP!   Level {level}" : "LEVEL UP!";

        // Cards
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

        // เปิด cards section ก่อน
        if (cardsSection) cardsSection.SetActive(true);

        // Timer reset
        if (timerLabel)
        {
            timerLabel.color = timerNormalColor;
            timerLabel.text  = "";
        }

        // Waiting reset
        if (waitingLabel) waitingLabel.text = "";

        if (panelRoot) panelRoot.SetActive(true);
    }

    /// <summary>
    /// เรียกหลัง player เลือก card แล้ว —
    /// ซ่อน cards + level label แต่คง panelRoot ไว้เพื่อแสดง timer / waiting count
    /// </summary>
    public void HideCards()
    {
        if (cardsSection) cardsSection.SetActive(false);
    }

    /// <summary>ปิด Panel ทั้งหมด — เรียกเมื่อทุกคนเลือกเสร็จ (OnUpgradePhaseEnd)</summary>
    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    // ── Timer ─────────────────────────────────────────────────────────────
    void UpdateTimer(float remaining)
    {
        if (timerLabel == null) return;

        int secs = Mathf.CeilToInt(remaining);
        timerLabel.text  = secs > 0 ? secs.ToString() : "0";
        timerLabel.color = remaining <= urgentThreshold ? timerUrgentColor : timerNormalColor;
    }

    // ── Waiting Count ─────────────────────────────────────────────────────
    void UpdateWaiting(int picked, int total)
    {
        if (waitingLabel == null) return;
        waitingLabel.text = total > 1
            ? $"รอผู้เล่น: {picked} / {total}"
            : "";    // Solo ไม่ต้องแสดง
    }
}
