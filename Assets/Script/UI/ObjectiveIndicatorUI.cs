using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// แสดง UI indicator สำหรับ ZoneObjective + FetchItem ที่ active อยู่ทุกตัว
///
/// - ZoneObjective ที่ spawn → indicator 1 อัน (ระยะ + count "X/N" ตอน fetch quest)
/// - FetchItem ที่ spawn → indicator แยก (ระยะ — สีต่างกันเพื่อแยกออกจาก zone)
///
/// อยู่นอกจอ → arrow ที่ขอบจอชี้ไปหา target
/// อยู่ในจอ  → icon เล็กเหนือ target
///
/// Setup:
///   วาง component บน Canvas (Screen Space - Overlay)
///   indicatorPrefab → prefab ที่มี Image + TextMeshProUGUI + RectTransform
/// </summary>
public class ObjectiveIndicatorUI : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab indicator 1 อัน — ต้องมี Image + TextMeshProUGUI")]
    public GameObject indicatorPrefab;

    [Header("Render Order")]
    [Tooltip("Canvas sortingOrder ของ indicator")]
    public int canvasSortingOrder = 200;

    [Header("Edge Margin")]
    public float edgeMargin = 48f;

    [Header("On-Screen Icon")]
    public float onScreenOffsetY = 80f;
    public float onScreenScale   = 0.7f;

    [Header("Distance Text")]
    public bool showDistance = true;

    [Header("Colors")]
    [Tooltip("สีของ ZoneObjective indicator")]
    public Color zoneColor      = new(0.20f, 0.85f, 1.00f);
    [Tooltip("สีของ FetchItem indicator")]
    public Color fetchItemColor = new(1.00f, 0.85f, 0.20f);

    [Header("Fetch Item Display")]
    [Tooltip("scale พิเศษของ FetchItem indicator (เล็กกว่า zone นิดๆ)")]
    public float fetchItemScale = 0.55f;
    [Tooltip("Prefix แสดงข้างหน้าระยะ — '★' หรือ icon character")]
    public string fetchItemPrefix = "★";

    [Header("Player-Relative Indicator (Off-Screen)")]
    [Tooltip("แสดงตัวนำทางรอบตัวผู้เล่นแทนขอบจอ")]
    public bool displayNearPlayer = true;
    [Tooltip("ระยะห่างจากตัวผู้เล่นบนหน้าจอ (พิกเซล)")]
    public float indicatorRadius = 120f;

    // ── Internal ──────────────────────────────────────────────────────────
    private enum Kind { Zone, FetchItem }

    private class Entry
    {
        public Kind            kind;
        public Transform       target;        // zone or fetch item transform
        public ZoneObjective   zone;          // null for FetchItem
        public RectTransform   rect;
        public Image           arrowImg;
        public TextMeshProUGUI distText;

        // ZoneObjective (FetchAndDeliver) cache
        public int             delivered;
        public int             required;
    }

    private readonly List<Entry> _entries = new();
    private Camera _cam;
    private playermove _cachedLocalPlayer;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        ZoneObjective.OnObjectiveSpawned   += OnZoneSpawned;
        ZoneObjective.OnObjectiveCompleted += OnZoneRemoved;
        ZoneObjective.OnObjectiveExpired   += OnZoneRemoved;
        ZoneObjective.OnDeliveryProgress   += OnDeliveryProgress;

        FetchItem.OnFetchItemSpawned   += OnFetchItemSpawned;
        FetchItem.OnFetchItemDespawned += OnFetchItemRemoved;
    }

    void OnDisable()
    {
        ZoneObjective.OnObjectiveSpawned   -= OnZoneSpawned;
        ZoneObjective.OnObjectiveCompleted -= OnZoneRemoved;
        ZoneObjective.OnObjectiveExpired   -= OnZoneRemoved;
        ZoneObjective.OnDeliveryProgress   -= OnDeliveryProgress;

        FetchItem.OnFetchItemSpawned   -= OnFetchItemSpawned;
        FetchItem.OnFetchItemDespawned -= OnFetchItemRemoved;
    }

    void Start()
    {
        _cam = Camera.main;
        ApplySortingOrder();
    }

    void ApplySortingOrder()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = canvasSortingOrder;
    }

    // ── Zone events ───────────────────────────────────────────────────────
    void OnZoneSpawned(ZoneObjective zone)
    {
        if (indicatorPrefab == null || zone == null) return;
        var entry = CreateEntry(Kind.Zone, zone.transform, zoneColor);
        if (entry != null) entry.zone = zone;
    }

    void OnZoneRemoved(ZoneObjective zone) => RemoveByTarget(zone != null ? zone.transform : null);

    void OnDeliveryProgress(ZoneObjective zone, int delivered, int required)
    {
        var entry = _entries.Find(e => e.kind == Kind.Zone && e.zone == zone);
        if (entry == null) return;
        entry.delivered = delivered;
        entry.required  = required;
    }

    // ── FetchItem events ──────────────────────────────────────────────────
    void OnFetchItemSpawned(FetchItem item)
    {
        if (indicatorPrefab == null || item == null) return;
        CreateEntry(Kind.FetchItem, item.transform, fetchItemColor);
    }

    void OnFetchItemRemoved(FetchItem item) => RemoveByTarget(item != null ? item.transform : null);

    // ── Entry helpers ─────────────────────────────────────────────────────
    Entry CreateEntry(Kind kind, Transform target, Color tint)
    {
        var go   = Instantiate(indicatorPrefab, transform);
        var rect = go.GetComponent<RectTransform>();
        var img  = go.GetComponentInChildren<Image>();
        var txt  = go.GetComponentInChildren<TextMeshProUGUI>();

        if (img != null) img.color = tint;
        if (txt != null) txt.text  = "";

        var entry = new Entry
        {
            kind     = kind,
            target   = target,
            rect     = rect,
            arrowImg = img,
            distText = txt,
        };
        _entries.Add(entry);
        return entry;
    }

    void RemoveByTarget(Transform target)
    {
        if (target == null) return;
        var entry = _entries.Find(e => e.target == target);
        if (entry == null) return;
        if (entry.rect != null) Destroy(entry.rect.gameObject);
        _entries.Remove(entry);
    }

    // ── Update ────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Transform playerT = GetLocalPlayerTransform();

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.target == null || e.rect == null) { _entries.RemoveAt(i); continue; }

            Vector3 worldPos = e.target.position + Vector3.up * 2f;
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

        // base scale ต่างกันตาม kind (FetchItem เล็กกว่า)
        float baseScale = e.kind == Kind.FetchItem ? fetchItemScale : 1f;

        if (onScreen)
        {
            e.rect.position   = new Vector3(screenPos.x, screenPos.y + onScreenOffsetY, 0f);
            e.rect.localScale = Vector3.one * onScreenScale * baseScale;
            if (e.arrowImg != null) e.arrowImg.rectTransform.localRotation = Quaternion.identity;
        }
        else
        {
            e.rect.localScale = Vector3.one * baseScale;

            if (!inFront)
                screenPos = new Vector3(sw - screenPos.x, sh - screenPos.y, 0f);

            // หาจุดศูนย์กลาง (ใช้หน้าจอผู้เล่นจริง หรือกึ่งกลางจอ)
            Vector3 center = new Vector3(sw * 0.5f, sh * 0.5f, 0f);
            bool usePlayerRel = displayNearPlayer && playerT != null;

            if (usePlayerRel)
            {
                Vector3 pScreen = _cam.WorldToScreenPoint(playerT.position + Vector3.up * 1f);
                pScreen.z = 0f;
                center = pScreen;
            }

            Vector3 dir = (screenPos - center).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

            if (usePlayerRel)
            {
                // แสดงใกล้ตัวผู้เล่น
                e.rect.position = center + dir * indicatorRadius;
            }
            else
            {
                // แสดงที่ขอบจอตามเดิม
                float halfW   = sw * 0.5f - edgeMargin;
                float halfH   = sh * 0.5f - edgeMargin;
                float scaleX  = Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
                float scaleY  = Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
                float scale   = Mathf.Min(scaleX, scaleY);
                e.rect.position = center + dir * scale;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            if (e.arrowImg != null)
                e.arrowImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── Distance + count text ─────────────────────────────────────────
        if (e.distText != null)
        {
            string distPart = "";
            if (showDistance && playerT != null)
            {
                float dist = Vector3.Distance(playerT.position, e.target.position);
                distPart = $"{Mathf.RoundToInt(dist)}m";
            }

            string text;
            if (e.kind == Kind.Zone)
            {
                string countPart = "";
                if (e.zone != null && e.zone.HasActiveQuest && e.required > 0)
                {
                    countPart = e.zone.ActiveQuestType switch
                    {
                        ZoneObjective.QuestType.FetchAndDeliver => $"★ {e.delivered}/{e.required}",
                        ZoneObjective.QuestType.Survive          => $"⏱ {e.delivered}/{e.required}s",
                        _                                         => "",
                    };
                }

                if (string.IsNullOrEmpty(countPart))      text = distPart;
                else if (string.IsNullOrEmpty(distPart))  text = countPart;
                else                                      text = $"{distPart}  {countPart}";
            }
            else // FetchItem
            {
                text = string.IsNullOrEmpty(distPart) ? fetchItemPrefix : $"{fetchItemPrefix} {distPart}";
            }

            e.distText.text = text;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetLocalPlayerTransform()
    {
        if (_cachedLocalPlayer != null) return _cachedLocalPlayer.transform;

        foreach (var pm in Object.FindObjectsByType<playermove>(FindObjectsSortMode.None))
        {
            if (pm != null && pm.IsOwner)
            {
                _cachedLocalPlayer = pm;
                return pm.transform;
            }
        }
        return null;
    }
}
