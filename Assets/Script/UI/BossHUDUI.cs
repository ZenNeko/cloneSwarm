using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Main Boss Cast Bar")]
    public GameObject      castBarRoot;
    public Image           castFill;
    public TextMeshProUGUI castNameText;

    [Header("Enrage Warning")]
    [Tooltip("วินาทีก่อน Boss enrage ที่จะแสดง warning")]
    public float enrageWarningTime = 45f;

    // ── Boss State ────────────────────────────────────────────────────────
    readonly Dictionary<BossController, MiniBossBarEntry> _miniBars = new();
    bool enrageWarned;
    Coroutine _castCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (miniBossPanel) miniBossPanel.SetActive(false);
        if (castBarRoot)   castBarRoot.SetActive(false);
    }

    void OnEnable()
    {
        BossController.OnAnyBossSpawned   += OnBossSpawned;
        BossController.OnAnyBossDespawned += OnBossDespawned;
        BossController.OnAnyCastStarted   += OnCastStarted;
        BossController.OnAnyCastEnded     += OnCastEnded;
    }

    void OnDisable()
    {
        BossController.OnAnyBossSpawned   -= OnBossSpawned;
        BossController.OnAnyBossDespawned -= OnBossDespawned;
        BossController.OnAnyCastStarted   -= OnCastStarted;
        BossController.OnAnyCastEnded     -= OnCastEnded;

        ClearAllBars();
        OnCastEnded(null);
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

    void OnCastStarted(BossController boss, string castName, float castTime)
    {
        if (castBarRoot == null || string.IsNullOrEmpty(castName) || castTime <= 0f) return;

        if (_castCoroutine != null) StopCoroutine(_castCoroutine);
        _castCoroutine = StartCoroutine(AnimateCastBar(castName, castTime));
    }

    void OnCastEnded(BossController boss)
    {
        if (_castCoroutine != null)
        {
            StopCoroutine(_castCoroutine);
            _castCoroutine = null;
        }
        if (castBarRoot) castBarRoot.SetActive(false);
    }

    System.Collections.IEnumerator AnimateCastBar(string castName, float castTime)
    {
        castBarRoot.SetActive(true);
        if (castNameText != null) castNameText.text = castName;
        if (castFill != null) castFill.fillAmount = 0f;

        float elapsed = 0f;
        while (elapsed < castTime)
        {
            elapsed += Time.deltaTime;
            if (castFill != null) castFill.fillAmount = Mathf.Clamp01(elapsed / castTime);
            yield return null;
        }

        if (castFill != null) castFill.fillAmount = 1f;
        castBarRoot.SetActive(false);
        _castCoroutine = null;
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
