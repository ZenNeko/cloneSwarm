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
    [Tooltip("Parent ของ card ทั้งหมด (Panel) — ถ้าไม่ assign จะหาจาก cardsSection อัตโนมัติ")]
    public GameObject cardsContainer;
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
    [Tooltip("แถบเล็กมุมจอตอนรอเพื่อน — ปล่อยว่างได้")]
    public GameObject waitingStrip;

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
    /// <param name="cards">รายการ card ที่สุ่มได้ (UpgradeCardInfo)</param>
    /// <param name="onPicked">callback เมื่อ player เลือก card</param>
    /// <param name="level">Level ใหม่ที่ขึ้น (0 = แสดงแค่ LEVEL UP!)</param>
    public void Show(
        List<UpgradeCardInfo>              cards,
        System.Action<UpgradeCardInfo>     onPicked,
        int level = 0)
    {
        // 1. เปิด hierarchy ก่อนเสมอ
        if (panelRoot)      panelRoot.SetActive(true);
        if (cardsSection)   cardsSection.SetActive(true);
        if (cardsContainer) cardsContainer.SetActive(true);
        if (waitingStrip)   waitingStrip.SetActive(false);

        // 2. Header
        if (levelLabel)
            levelLabel.text = level > 0 ? $"LEVEL UP!   Level {level}" : "LEVEL UP!";

        // 3. หา card slots จาก cardsContainer โดยตรง
        UpgradeCardUI[] slots = cardsContainer
            ? cardsContainer.GetComponentsInChildren<UpgradeCardUI>(true)
            : cardSlots.ToArray();

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < cards.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Populate(cards[i], onPicked);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        // 4. Reset timer / waiting
        if (timerLabel)  { timerLabel.color = timerNormalColor; timerLabel.text = ""; }
        if (waitingLabel)  waitingLabel.text = "";
    }

    /// <summary>
    /// เรียกหลัง player เลือก card แล้ว —
    /// ซ่อน cards + level label แต่คง panelRoot ไว้เพื่อแสดง timer / waiting count
    /// </summary>
    public void HideCards()
    {
        if (cardsSection) cardsSection.SetActive(false);
        if (waitingStrip) waitingStrip.SetActive(true);
    }

    /// <summary>ปิด Panel ทั้งหมด — เรียกเมื่อทุกคนเลือกเสร็จ (OnUpgradePhaseEnd)</summary>
    public void Hide()
    {
        if (waitingStrip) waitingStrip.SetActive(false);
        if (panelRoot)    panelRoot.SetActive(false);
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
