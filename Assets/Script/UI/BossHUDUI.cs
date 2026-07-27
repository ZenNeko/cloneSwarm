using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canvas HP bars สำหรับ Boss ทุกประเภท
/// — ระบบใหม่: แสดงหลอดเลือดบอสทุกคนแบบไดนามิกในรูปของหลอดเลือด Mini Boss ใน MiniBossPanel
/// </summary>
public class BossHUDUI : MonoBehaviour
{
    [Header("── Boss HUD Panel (Dynamic List) ──")]
    [Tooltip("Root panel สำหรับแสดงหลอดเลือดบอสทั้งหมด — ซ่อนเมื่อไม่มีบอส active")]
    public GameObject      miniBossPanel;
    [Tooltip("Container ที่ bars จะ Instantiate เข้าไป — ควรมี VerticalLayoutGroup")]
    public Transform       miniBossContainer;
    [Tooltip("Prefab ที่มี MiniBossBarEntry.cs")]
    public MiniBossBarEntry miniBossBarPrefab;

    [Header("Enrage Warning")]
    [Tooltip("วินาทีก่อน Boss enrage ที่จะแสดง warning")]
    public float enrageWarningTime = 45f;

    // ── Boss State ────────────────────────────────────────────────────────
    readonly Dictionary<BossController, MiniBossBarEntry> _miniBars = new();
    bool enrageWarned;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (miniBossPanel) miniBossPanel.SetActive(false);
    }

    void OnEnable()
    {
        BossController.OnAnyBossSpawned   += OnBossSpawned;
        BossController.OnAnyBossDespawned += OnBossDespawned;
    }

    void OnDisable()
    {
        BossController.OnAnyBossSpawned   -= OnBossSpawned;
        BossController.OnAnyBossDespawned -= OnBossDespawned;

        ClearAllBars();
    }

    // ── Handlers ──────────────────────────────────────────────────────────
    void OnBossSpawned(BossController boss)
    {
        if (boss == null || _miniBars.ContainsKey(boss)) return;
        if (miniBossBarPrefab == null)
        {
            Debug.LogWarning("[BossHUDUI] miniBossBarPrefab not assigned!");
            return;
        }

        if (_miniBars.Count == 0)
        {
            enrageWarned = false;
        }

        var container = miniBossContainer != null ? miniBossContainer : transform;
        var entry     = Instantiate(miniBossBarPrefab, container);
        entry.Initialize(boss);
        _miniBars[boss] = entry;

        if (miniBossPanel) miniBossPanel.SetActive(true);
    }

    void OnBossDespawned(BossController boss)
    {
        if (boss == null || !_miniBars.TryGetValue(boss, out var entry)) return;

        _miniBars.Remove(boss);
        if (entry != null) Destroy(entry.gameObject);

        if (_miniBars.Count == 0 && miniBossPanel)
            miniBossPanel.SetActive(false);
    }

    void ClearAllBars()
    {
        foreach (var entry in _miniBars.Values)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        _miniBars.Clear();
        if (miniBossPanel) miniBossPanel.SetActive(false);
    }

    // ── Enrage Warning ────────────────────────────────────────────────────
    void Update()
    {
        if (_miniBars.Count == 0 || enrageWarned) return;
        if (GameTimeline.Instance == null) return;

        float mainBossAt = GameTimeline.Instance.mainBossTimeMin * 60f;
        float remaining  = mainBossAt - GameTimeline.Instance.GetGameTime();

        if (remaining <= enrageWarningTime && remaining > 0f)
        {
            enrageWarned = true;
            GameHUD.Instance
                ?.ShowAnnouncement($"⚠ ENRAGE IN {Mathf.CeilToInt(remaining)}s!", new Color(1f, 0.4f, 0f));
        }
    }
}
