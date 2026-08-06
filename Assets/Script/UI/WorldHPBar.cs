using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-space HP bar ลอยเหนือ Boss / MiniBoss — Billboard (หันหากล้องเสมอ)
///
/// **Setup บน Boss/MiniBoss prefab:**
///   1. วาง WorldHPBar.cs บน root GameObject (ต้องมี Enemy.cs)
///   2. สร้าง child "WorldHPCanvas":
///      → Canvas component → Render Mode = World Space
///      → ปรับ Scale ≈ (0.01, 0.01, 0.01)  (ขึ้นอยู่กับขนาด Boss)
///   3. ใน WorldHPCanvas สร้าง child elements:
///      ├── NameText       TextMeshProUGUI  — ชื่อ Boss
///      ├── Background     Image            — แถบพื้นหลัง HP bar (ยืดเต็มแถบ)
///      ├── HPFill         Image            — Image Type: Filled / Fill Method: Horizontal
///      ├── HPNumText      TextMeshProUGUI  — "1500 / 2000" (optional)
///      └── Phase2Marker   RectTransform    — เส้น marker 60% (optional, MainBoss เท่านั้น)
///          Phase3Marker   RectTransform    — เส้น marker 30% (optional, MainBoss เท่านั้น)
///   4. Assign ทุก ref ใน Inspector ของ WorldHPBar
///
/// **Notes:**
///   • สามารถใช้กับบอสทุกตัวที่มี BossController
///   • displayName ปล่อยว่าง = ใช้ gameObject.name อัตโนมัติ
///   • Phase markers สำหรับ MainBoss (ถ้าไม่มีก็ไม่ต้อง assign)
/// </summary>
[RequireComponent(typeof(Enemy))]
public class WorldHPBar : MonoBehaviour
{
    [Header("World Canvas  (child Render Mode: World Space)")]
    [Tooltip("Canvas component ของ child object — ต้อง Render Mode = World Space")]
    public Canvas worldCanvas;

    [Header("UI References")]
    [Tooltip("Image type=Filled, FillMethod=Horizontal — Fill Amount = HP%")]
    public Image            hpFill;
    [Tooltip("ชื่อ Boss (TextMeshProUGUI)")]
    public TextMeshProUGUI  nameText;
    [Tooltip("'1500 / 2000' — optional")]
    public TextMeshProUGUI  hpNumberText;

    [Header("Phase Markers  (MainBoss only — optional)")]
    [Tooltip("RectTransform marker ที่ 60% ของ HP bar (Phase 2 threshold)")]
    public RectTransform phase2Marker;
    [Tooltip("RectTransform marker ที่ 30% ของ HP bar (Phase 3 threshold)")]
    public RectTransform phase3Marker;

    [Header("Position")]
    [Tooltip("ความสูงเหนือ pivot ของ Boss (world units) — ปรับตามขนาด model")]
    public float heightOffset = 2.8f;

    [Header("HP Colors")]
    public Color colorFull     = new Color(0.15f, 0.85f, 0.15f); // green
    public Color colorMid      = new Color(1f,    0.60f, 0f);    // orange
    public Color colorCritical = new Color(0.85f, 0.10f, 0.10f); // red
    [Range(0f, 1f)] public float midThreshold      = 0.50f;
    [Range(0f, 1f)] public float criticalThreshold = 0.25f;

    [Header("Display Name")]
    [Tooltip("ชื่อที่แสดงใน bar — ถ้าว่างจะใช้ gameObject.name")]
    public string displayName = "";

    // ── Internal ──────────────────────────────────────────────────────────
    Enemy  _enemy;
    Camera _cam;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        _enemy = GetComponent<Enemy>();

        // ตั้งชื่อ
        if (nameText != null)
            nameText.text = string.IsNullOrEmpty(displayName)
                ? gameObject.name.Replace("(Clone)", "").Trim()
                : displayName;
    }

    void OnEnable()
    {
        if (_enemy != null)
            _enemy.netHealth.OnValueChanged += OnHealthChanged;
    }

    void OnDisable()
    {
        if (_enemy != null)
            _enemy.netHealth.OnValueChanged -= OnHealthChanged;
    }

    void Start()
    {
        _cam = Camera.main;
        RefreshHP();
        SetupPhaseMarkers();
    }

    // ── Billboard + Position ──────────────────────────────────────────────
    void LateUpdate()
    {
        if (worldCanvas == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // ตำแหน่ง: เหนือ Boss pivot ตาม heightOffset
        worldCanvas.transform.position = transform.position + Vector3.up * heightOffset;

        // Billboard: หันหากล้องเสมอ (ใช้ camera rotation เพื่อ avoid gimbal lock)
        worldCanvas.transform.LookAt(
            worldCanvas.transform.position + _cam.transform.rotation * Vector3.forward,
            _cam.transform.rotation * Vector3.up
        );
    }

    // ── HP Updates ────────────────────────────────────────────────────────
    void OnHealthChanged(float _, float newHP) => RefreshHP();

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

        if (hpNumberText != null)
            hpNumberText.text =
                $"{Mathf.CeilToInt(_enemy.netHealth.Value)} / {Mathf.CeilToInt(_enemy.netMaxHealth.Value)}";
    }

    // ── Phase Markers ─────────────────────────────────────────────────────
    /// <summary>ตั้งตำแหน่ง marker ให้ตรงกับ HP% บน bar — เรียกใน Start()</summary>
    void SetupPhaseMarkers()
    {
        PlaceMarker(phase2Marker, 0.60f);
        PlaceMarker(phase3Marker, 0.30f);
    }

    void PlaceMarker(RectTransform marker, float pct)
    {
        if (marker == null || hpFill == null) return;
        float barWidth = hpFill.rectTransform.rect.width;
        marker.anchoredPosition = new Vector2(barWidth * pct, marker.anchoredPosition.y);
    }
}
