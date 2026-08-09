using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Row entry ตัวเดียวที่แสดง HP ของ MiniBoss หนึ่งตัวใน Canvas
/// — สร้างเป็น Prefab แล้ว assign ให้ MiniBossHUDUI.miniBossBarPrefab
///
/// **Prefab hierarchy แนะนำ:**
///   MiniBossBarEntry  (มี MiniBossBarEntry.cs)
///   ├── NameText       TextMeshProUGUI  — ชื่อ MiniBoss
///   ├── Background     Image            — พื้นหลัง bar
///   ├── HPFill         Image            — Filled / Horizontal
///   └── HPNumText      TextMeshProUGUI  — "800/1200" (optional)
///
/// ชื่อ: ใช้ BossController.bossDisplayName → ถ้าว่างใช้ gameObject.name ของ boss
/// </summary>
public class MiniBossBarEntry : MonoBehaviour
{
    [Header("UI References")]
    public Image            hpFill;
    public TextMeshProUGUI  nameText;
    public TextMeshProUGUI  hpNumText;   // optional

    [Header("Cast Bar")]
    public GameObject      castBarRoot;
    public Image           castFill;
    public TextMeshProUGUI castNameText;

    [Header("HP Colors")]
    public Color colorFull     = new Color(0.9f, 0.6f, 0.1f);  // gold (mini boss)
    public Color colorMid      = new Color(0.9f, 0.3f, 0f);    // dark orange
    public Color colorCritical = new Color(0.8f, 0.1f, 0.1f);  // red
    [Range(0f,1f)] public float midThreshold      = 0.50f;
    [Range(0f,1f)] public float criticalThreshold = 0.25f;

    // ── Internal ──────────────────────────────────────────────────────────
    Enemy          _enemy;
    BossController _targetBoss;
    Coroutine      _castCoroutine;

    void Awake()
    {
        if (castBarRoot) castBarRoot.SetActive(false);
    }

    void OnEnable()
    {
        BossController.OnAnyCastStarted += OnCastStarted;
        BossController.OnAnyCastEnded   += OnCastEnded;
    }

    // ── Init ──────────────────────────────────────────────────────────────
    /// <summary>เรียกจาก BossHUDUI หลัง Instantiate</summary>
    public void Initialize(BossController boss)
    {
        if (boss == null) return;
        _targetBoss = boss;
        _enemy = boss.GetComponent<Enemy>();

        if (castBarRoot) castBarRoot.SetActive(false);

        // ชื่อ: ใช้ boss.bossDisplayName ก่อน → fallback gameObject.name
        if (nameText != null)
        {
            string n = string.IsNullOrEmpty(boss.bossDisplayName)
                ? boss.gameObject.name.Replace("(Clone)", "").Trim()
                : boss.bossDisplayName;
            nameText.text = n;
        }

        if (_enemy != null)
            _enemy.netHealth.OnValueChanged += OnHealthChanged;

        RefreshHP();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────
    void OnDisable()
    {
        BossController.OnAnyCastStarted -= OnCastStarted;
        BossController.OnAnyCastEnded   -= OnCastEnded;

        if (_enemy != null)
            _enemy.netHealth.OnValueChanged -= OnHealthChanged;

        OnCastEnded(_targetBoss);
    }

    void OnCastStarted(BossController boss, string castName, float castTime)
    {
        if (boss == null || boss != _targetBoss) return;
        if (castBarRoot == null || string.IsNullOrEmpty(castName) || castTime <= 0f) return;

        if (_castCoroutine != null) StopCoroutine(_castCoroutine);
        _castCoroutine = StartCoroutine(AnimateCastBar(castName, castTime));
    }

    void OnCastEnded(BossController boss)
    {
        if (boss != null && boss != _targetBoss) return;

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

    // ── HP Updates ────────────────────────────────────────────────────────
    void OnHealthChanged(float _, float hp) => RefreshHP();

    void RefreshHP()
    {
        if (_enemy == null) return;

        float pct = _enemy.netMaxHealth.Value > 0f
            ? Mathf.Clamp01(_enemy.netHealth.Value / _enemy.netMaxHealth.Value)
            : 0f;

        if (hpFill != null)
        {
            hpFill.fillAmount = pct;
            hpFill.color = pct > midThreshold      ? colorFull
                         : pct > criticalThreshold ? colorMid
                         :                           colorCritical;
        }

        if (hpNumText != null)
            hpNumText.text =
                $"{Mathf.CeilToInt(_enemy.netHealth.Value)}/{Mathf.CeilToInt(_enemy.netMaxHealth.Value)}";
    }
}
