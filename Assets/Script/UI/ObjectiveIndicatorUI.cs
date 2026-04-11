using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง UI indicator สำหรับ ZoneObjective ที่ active อยู่ทุกตัว
///
/// ทุก ZoneObjective ที่ spawn → สร้าง indicator 1 อัน
///   — ถ้า objective อยู่นอกจอ → แสดง arrow ที่ขอบจอชี้ไปหา objective
///   — ถ้า objective อยู่ในจอ  → แสดง icon เล็กๆ เหนือ objective (worldspace)
///
/// Setup:
///   วาง ObjectiveIndicatorUI บน Canvas (Screen Space - Overlay)
///   กำหนด indicatorPrefab → prefab ที่มี:
///     Image (arrow icon) + TextMeshProUGUI (ระยะ/หมายเลข) + RectTransform
/// </summary>
public class ObjectiveIndicatorUI : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab indicator 1 อัน — ต้องมี Image + TextMeshProUGUI")]
    public GameObject indicatorPrefab;

    [Header("Edge Margin")]
    [Tooltip("ระยะห่างจากขอบจอ (px)")]
    public float edgeMargin = 48f;

    [Header("On-Screen Icon")]
    [Tooltip("offset ขึ้นด้านบนเมื่ออยู่ในจอ (px)")]
    public float onScreenOffsetY = 80f;
    [Tooltip("scale เมื่ออยู่ในจอ")]
    public float onScreenScale = 0.7f;

    [Header("Distance Text")]
    public bool showDistance = true;

    // ── Internal ──────────────────────────────────────────────────────────
    private class Entry
    {
        public ZoneObjective  zone;
        public RectTransform  rect;
        public Image          arrowImg;
        public TextMeshProUGUI distText;
    }

    private readonly List<Entry> _entries = new();
    private Camera _cam;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        ZoneObjective.OnObjectiveSpawned   += OnSpawned;
        ZoneObjective.OnObjectiveCompleted += OnRemoved;
        ZoneObjective.OnObjectiveExpired   += OnRemoved;
    }

    void OnDisable()
    {
        ZoneObjective.OnObjectiveSpawned   -= OnSpawned;
        ZoneObjective.OnObjectiveCompleted -= OnRemoved;
        ZoneObjective.OnObjectiveExpired   -= OnRemoved;
    }

    void Start()
    {
        _cam = Camera.main;
    }

    // ── Events ─────────────────────────────────────────────────────────────
    void OnSpawned(ZoneObjective zone)
    {
        if (indicatorPrefab == null) return;

        var go   = Instantiate(indicatorPrefab, transform);
        var rect = go.GetComponent<RectTransform>();
        var img  = go.GetComponentInChildren<Image>();
        var txt  = go.GetComponentInChildren<TextMeshProUGUI>();

        // เลข index + สี ต่างกันตาม indicator
        int idx = _entries.Count;
        if (txt != null) txt.text = $"#{idx + 1}";

        // สีต่างกันตาม index
        if (img != null)
        {
            Color[] colors = {
                new Color(0.20f, 0.85f, 1.00f),
                new Color(1.00f, 0.80f, 0.20f),
                new Color(0.30f, 1.00f, 0.50f),
                new Color(1.00f, 0.40f, 0.20f),
                new Color(0.80f, 0.40f, 1.00f),
            };
            img.color = colors[idx % colors.Length];
        }

        _entries.Add(new Entry { zone = zone, rect = rect, arrowImg = img, distText = txt });
    }

    void OnRemoved(ZoneObjective zone)
    {
        var entry = _entries.Find(e => e.zone == zone);
        if (entry == null) return;
        if (entry.rect != null) Destroy(entry.rect.gameObject);
        _entries.Remove(entry);
        RenumberIndicators();
    }

    // ── Update ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // หา local player transform
        Transform playerT = GetLocalPlayerTransform();

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.zone == null || e.rect == null) { _entries.RemoveAt(i); continue; }

            Vector3 worldPos = e.zone.transform.position + Vector3.up * 2f;
            UpdateIndicator(e, worldPos, playerT);
        }
    }

    void UpdateIndicator(Entry e, Vector3 worldPos, Transform playerT)
    {
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
        bool    inFront   = screenPos.z > 0f;

        float sw = Screen.width;
        float sh = Screen.height;

        bool onScreen = inFront
            && screenPos.x > edgeMargin && screenPos.x < sw - edgeMargin
            && screenPos.y > edgeMargin && screenPos.y < sh - edgeMargin;

        if (onScreen)
        {
            // อยู่ในจอ → วางเหนือ objective
            e.rect.position    = new Vector3(screenPos.x, screenPos.y + onScreenOffsetY, 0f);
            e.rect.localScale  = Vector3.one * onScreenScale;

            // ไม่ rotate
            if (e.arrowImg != null)
                e.arrowImg.rectTransform.localRotation = Quaternion.identity;
        }
        else
        {
            // นอกจอ → clamp ที่ขอบและชี้ทิศทาง
            e.rect.localScale = Vector3.one;

            // ถ้า z < 0 (อยู่หลังกล้อง) ต้อง flip
            if (!inFront)
                screenPos = new Vector3(sw - screenPos.x, sh - screenPos.y, 0f);

            // หา direction จากกลางจอ → ตำแหน่ง objective
            Vector3 center    = new Vector3(sw * 0.5f, sh * 0.5f, 0f);
            Vector3 dir       = (screenPos - center).normalized;

            // clamp ไว้ที่ขอบ
            float   halfW = sw * 0.5f - edgeMargin;
            float   halfH = sh * 0.5f - edgeMargin;
            float   scaleX = Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float   scaleY = Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            float   scale  = Mathf.Min(scaleX, scaleY);
            Vector3 clampedPos = center + dir * scale;

            e.rect.position = clampedPos;

            // หมุน arrow ชี้ไปทาง objective
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            if (e.arrowImg != null)
                e.arrowImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // อัปเดตระยะ
        if (showDistance && e.distText != null && playerT != null)
        {
            float dist = Vector3.Distance(playerT.position, e.zone.transform.position);
            e.distText.text = $"{Mathf.RoundToInt(dist)}m";
        }
    }

    void RenumberIndicators()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            // ไม่ renumber text หลัง remove เพื่อไม่ให้สับสน
        }
    }

    Transform GetLocalPlayerTransform()
    {
        var pm = Object.FindAnyObjectByType<playermove>();
        if (pm == null) return null;
        return pm.IsOwner ? pm.transform : null;
    }
}
