using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas HP bars สำหรับ Boss ทุกประเภท — MainBoss (static) + MiniBoss (dynamic list)
///
/// ─── Main Boss ───────────────────────────────────────────────────────────
///   • แสดง HP bar เดี่ยวพร้อม phase markers และ enrage warning
///   • Subscribe MainBoss.OnAnyBossSpawned / OnAnyBossDespawned
///
/// ─── Mini Boss ───────────────────────────────────────────────────────────
///   • Instantiate / Destroy bar ตาม MiniBoss spawn/despawn
///   • Subscribe MiniBossAI.OnAnyMiniBossSpawned / OnAnyMiniBossDefeated
///   • ซ่อน miniBossPanel เมื่อไม่มี MiniBoss active
///
/// Setup ใน Canvas hierarchy แนะนำ:
///   BossHUDUI  (component อยู่บน GameObject นี้)
///   ├── MainBossPanel
///   │   ├── BossNameText
///   │   ├── HPFill        (Filled Image)
///   │   ├── HPNumText
///   │   ├── Phase2Marker  (RectTransform)
///   │   └── Phase3Marker  (RectTransform)
///   └── MiniBossPanel
///       └── MiniBossContainer  (VerticalLayoutGroup)
///             ← MiniBossBarEntry prefabs จะ Instantiate มาตรงนี้
/// </summary>
public class BossHUDUI : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════════════
    //  MAIN BOSS
    // ══════════════════════════════════════════════════════════════════════
    [Header("── Main Boss ──────────────────────────────────────────────")]
    [Tooltip("Panel ของ Main Boss bar — ซ่อน/แสดงอัตโนมัติ")]
    public GameObject      bossHPRoot;
    [Tooltip("Filled Image, fillAmount = HP%")]
    public Image           hpFill;
    public TextMeshProUGUI bossNameText;
    [Tooltip("'1500 / 2000' (optional)")]
    public TextMeshProUGUI hpNumberText;

    [Header("Phase Markers (อยู่ใน HP bar ของ Main Boss)")]
    [Tooltip("Marker 60% — Phase 2 threshold")]
    public RectTransform   phase2Marker;
    [Tooltip("Marker 30% — Phase 3 threshold")]
    public RectTransform   phase3Marker;

    [Header("Enrage Warning")]
    [Tooltip("วินาทีก่อน Boss enrage ที่จะแสดง warning")]
    public float enrageWarningTime = 45f;

    // ══════════════════════════════════════════════════════════════════════
    //  MINI BOSS
    // ══════════════════════════════════════════════════════════════════════
    [Header("── Mini Boss ───────────────────────────────────────────────")]
    [Tooltip("Root panel ของ Mini Boss bars — ซ่อนเมื่อไม่มี MiniBoss active")]
    public GameObject      miniBossPanel;
    [Tooltip("Container ที่ bars จะ Instantiate เข้าไป — ควรมี VerticalLayoutGroup")]
    public Transform       miniBossContainer;
    [Tooltip("Prefab ที่มี MiniBossBarEntry.cs")]
    public MiniBossBarEntry miniBossBarPrefab;

    // ── Main Boss State ───────────────────────────────────────────────────
    MainBoss trackedBoss;
    Enemy    trackedEnemy;
    bool     enrageWarned;

    // ── Mini Boss State ───────────────────────────────────────────────────
    readonly Dictionary<MiniBossAI, MiniBossBarEntry> _miniBars = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (bossHPRoot)    bossHPRoot.SetActive(false);
        if (miniBossPanel) miniBossPanel.SetActive(false);
    }

    void OnEnable()
    {
        MainBoss.OnAnyBossSpawned    += OnMainBossSpawned;
        MainBoss.OnAnyBossDespawned  += OnMainBossDespawned;
        MiniBossAI.OnAnyMiniBossSpawned  += OnMiniBossSpawned;
        MiniBossAI.OnAnyMiniBossDefeated += OnMiniBossDefeated;
    }

    void OnDisable()
    {
        MainBoss.OnAnyBossSpawned    -= OnMainBossSpawned;
        MainBoss.OnAnyBossDespawned  -= OnMainBossDespawned;
        MiniBossAI.OnAnyMiniBossSpawned  -= OnMiniBossSpawned;
        MiniBossAI.OnAnyMiniBossDefeated -= OnMiniBossDefeated;

        UnsubscribeMainEnemy();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MAIN BOSS — handlers
    // ══════════════════════════════════════════════════════════════════════
    void OnMainBossSpawned(MainBoss boss)
    {
        trackedBoss  = boss;
        trackedEnemy = boss.GetComponent<Enemy>();

        if (trackedEnemy != null)
            trackedEnemy.netHealth.OnValueChanged += OnMainHPChanged;

        if (bossNameText)
            bossNameText.text = string.IsNullOrEmpty(boss.bossDisplayName)
                ? "BOSS"
                : boss.bossDisplayName;

        if (bossHPRoot) bossHPRoot.SetActive(true);

        enrageWarned = false;
        SetupPhaseMarkers(boss.phase2Threshold, boss.phase3Threshold);
        RefreshMainHP();
    }

    void OnMainBossDespawned()
    {
        if (bossHPRoot) bossHPRoot.SetActive(false);
        UnsubscribeMainEnemy();
        trackedBoss  = null;
        trackedEnemy = null;
    }

    void UnsubscribeMainEnemy()
    {
        if (trackedEnemy != null)
            trackedEnemy.netHealth.OnValueChanged -= OnMainHPChanged;
    }

    void OnMainHPChanged(float _, float newHP) => RefreshMainHP();

    void RefreshMainHP()
    {
        if (trackedEnemy == null || trackedBoss == null) return;

        float pct = trackedEnemy.GetHealthPercent();

        if (hpFill)
        {
            hpFill.fillAmount = Mathf.Clamp01(pct);
            hpFill.color = pct > trackedBoss.phase2Threshold ? Color.red
                         : pct > trackedBoss.phase3Threshold ? new Color(1f, 0.5f, 0f)
                         :                                     new Color(0.8f, 0f, 0f);
        }

        if (hpNumberText)
            hpNumberText.text =
                $"{Mathf.CeilToInt(trackedEnemy.netHealth.Value)} / {Mathf.CeilToInt(trackedEnemy.maxHealth)}";
    }

    // ── Enrage Warning ────────────────────────────────────────────────────
    void Update()
    {
        if (trackedBoss == null || enrageWarned) return;
        if (GameTimeline.Instance == null) return;

        float mainBossAt = GameTimeline.Instance.mainBossTimeMin * 60f;
        float remaining  = mainBossAt - GameTimeline.Instance.GetGameTime();

        if (remaining <= enrageWarningTime && remaining > 0f)
        {
            enrageWarned = true;
            Object.FindAnyObjectByType<GameHUD>()
                ?.ShowAnnouncement($"⚠ ENRAGE IN {Mathf.CeilToInt(remaining)}s!", new Color(1f, 0.4f, 0f));
        }
    }

    // ── Phase Markers ─────────────────────────────────────────────────────
    void SetupPhaseMarkers(float p2, float p3)
    {
        PlaceMarker(phase2Marker, p2);
        PlaceMarker(phase3Marker, p3);
    }

    void PlaceMarker(RectTransform marker, float pct)
    {
        if (marker == null || hpFill == null) return;
        float w = hpFill.rectTransform.rect.width;
        marker.anchoredPosition = new Vector2(w * pct, marker.anchoredPosition.y);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  MINI BOSS — handlers
    // ══════════════════════════════════════════════════════════════════════
    void OnMiniBossSpawned(MiniBossAI boss)
    {
        if (boss == null || _miniBars.ContainsKey(boss)) return;
        if (miniBossBarPrefab == null)
        {
            Debug.LogWarning("[BossHUDUI] miniBossBarPrefab not assigned!");
            return;
        }

        var container = miniBossContainer != null ? miniBossContainer : transform;
        var entry     = Instantiate(miniBossBarPrefab, container);
        entry.Initialize(boss);
        _miniBars[boss] = entry;

        if (miniBossPanel) miniBossPanel.SetActive(true);
    }

    void OnMiniBossDefeated(MiniBossAI boss)
    {
        if (!_miniBars.TryGetValue(boss, out var entry)) return;

        _miniBars.Remove(boss);
        if (entry != null) Destroy(entry.gameObject);

        if (_miniBars.Count == 0 && miniBossPanel)
            miniBossPanel.SetActive(false);
    }
}
