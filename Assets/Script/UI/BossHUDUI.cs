using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss HP Bar แสดงบน HUD
///
/// Setup (Inspector):
///   - bossHPRoot  : root GameObject ของ boss bar panel (เริ่มซ่อนอยู่)
///   - hpFill      : Image แบบ Filled ของ HP bar
///   - bossNameText: ชื่อ Boss (optional)
///   - phase2Marker / phase3Marker : RectTransform ที่ mark จุด 60% และ 30%
///
/// Events:
///   SubscribesMainBoss.OnAnyBossSpawned / OnAnyBossDespawned (static)
/// </summary>
public class BossHUDUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject      bossHPRoot;        // ซ่อน/แสดงทั้ง panel
    public Image           hpFill;            // Filled Image, fillAmount = hp%
    public TextMeshProUGUI bossNameText;
    public TextMeshProUGUI hpNumberText;      // "1500 / 2000" (optional)

    [Header("Phase Markers (RectTransform ใน HP bar)")]
    [Tooltip("Marker ที่ตำแหน่ง 60% (Phase 2 threshold)")]
    public RectTransform   phase2Marker;
    [Tooltip("Marker ที่ตำแหน่ง 30% (Phase 3/Enrage threshold)")]
    public RectTransform   phase3Marker;

    [Header("Enrage Warning")]
    [Tooltip("วินาทีก่อน Boss enrage ที่จะแสดง warning")]
    public float enrageWarningTime = 45f;

    // ── State ─────────────────────────────────────────────────────────────
    private MainBoss trackedBoss;
    private Enemy    trackedEnemy;
    private bool     enrageWarned;
    private float    gameStartTime;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (bossHPRoot) bossHPRoot.SetActive(false);
    }

    void OnEnable()
    {
        MainBoss.OnAnyBossSpawned   += OnBossSpawned;
        MainBoss.OnAnyBossDespawned += OnBossDespawned;
    }

    void OnDisable()
    {
        MainBoss.OnAnyBossSpawned   -= OnBossSpawned;
        MainBoss.OnAnyBossDespawned -= OnBossDespawned;

        UnsubscribeEnemy();
    }

    // ── Boss Tracking ─────────────────────────────────────────────────────
    void OnBossSpawned(MainBoss boss)
    {
        trackedBoss  = boss;
        trackedEnemy = boss.GetComponent<Enemy>();

        if (trackedEnemy != null)
            trackedEnemy.netHealth.OnValueChanged += OnHealthChanged;

        if (bossNameText) bossNameText.text = "BOSS";
        if (bossHPRoot)   bossHPRoot.SetActive(true);

        enrageWarned  = false;
        gameStartTime = Time.time;

        SetupPhaseMarkers(boss.phase2Threshold, boss.phase3Threshold);
        RefreshHP();
    }

    void OnBossDespawned()
    {
        if (bossHPRoot) bossHPRoot.SetActive(false);
        UnsubscribeEnemy();
        trackedBoss  = null;
        trackedEnemy = null;
    }

    void UnsubscribeEnemy()
    {
        if (trackedEnemy != null)
            trackedEnemy.netHealth.OnValueChanged -= OnHealthChanged;
    }

    // ── HP Updates ────────────────────────────────────────────────────────
    void OnHealthChanged(float _, float newHP) => RefreshHP();

    void RefreshHP()
    {
        if (trackedEnemy == null) return;

        float pct = trackedEnemy.GetHealthPercent();
        if (hpFill)      hpFill.fillAmount = Mathf.Clamp01(pct);
        if (hpNumberText)
            hpNumberText.text = $"{Mathf.CeilToInt(trackedEnemy.netHealth.Value)} / {Mathf.CeilToInt(trackedEnemy.maxHealth)}";

        // Flash bar color based on phase
        if (hpFill)
        {
            hpFill.color = pct > trackedBoss.phase2Threshold ? Color.red
                         : pct > trackedBoss.phase3Threshold ? new Color(1f, 0.5f, 0f)
                         : new Color(0.8f, 0f, 0f);
        }
    }

    // ── Enrage Warning (timer-based) ──────────────────────────────────────
    void Update()
    {
        if (trackedBoss == null || enrageWarned) return;
        if (GameTimeline.Instance == null) return;

        // GameTimeline มี mainBossTimeMin — แจ้ง enrage 45 วิก่อน
        float mainBossAt  = GameTimeline.Instance.mainBossTimeMin * 60f;
        float remaining   = mainBossAt - GameTimeline.Instance.GetGameTime();

        if (remaining <= enrageWarningTime && remaining > 0f)
        {
            enrageWarned = true;
            FindAnyObjectOfType<GameHUD>()
                ?.ShowAnnouncement($"⚠ ENRAGE IN {Mathf.CeilToInt(remaining)}s!", new Color(1f, 0.4f, 0f));
        }
    }

    // ── Phase Markers Setup ───────────────────────────────────────────────
    void SetupPhaseMarkers(float phase2Pct, float phase3Pct)
    {
        // ตั้ง anchoredPosition ของ marker ให้ตรงกับ % บน HP bar
        // สมมติ HP bar กว้าง 300 px — ปรับตาม layout ของคุณ
        if (phase2Marker != null)
        {
            var rect = hpFill?.rectTransform;
            if (rect != null)
            {
                float w = rect.rect.width;
                phase2Marker.anchoredPosition = new Vector2(w * phase2Pct, phase2Marker.anchoredPosition.y);
            }
        }
        if (phase3Marker != null)
        {
            var rect = hpFill?.rectTransform;
            if (rect != null)
            {
                float w = rect.rect.width;
                phase3Marker.anchoredPosition = new Vector2(w * phase3Pct, phase3Marker.anchoredPosition.y);
            }
        }
    }
}
