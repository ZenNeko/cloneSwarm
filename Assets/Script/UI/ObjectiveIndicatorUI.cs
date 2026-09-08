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
    [Tooltip("สีของวัตถุที่ต้องทำลาย (quest DestroyObjects)")]
    public Color destructibleColor = new(1.00f, 0.45f, 0.25f);

    [Header("Fetch Item Display")]
    [Tooltip("scale พิเศษของ FetchItem indicator (เล็กกว่า zone นิดๆ)")]
    public float fetchItemScale = 0.55f;
    [Tooltip("Prefix แสดงข้างหน้าระยะ — ใช้ได้เฉพาะตัวอักษรที่ฟอนต์มีจริง · " +
             "ห้ามใส่สัญลักษณ์อย่าง STAR / WARN / BOLT ทั้ง LiberationSans และ Sarabun ไม่มี · " +
             "ป้ายนี้อัปเดตทุกเฟรม จะได้ TMP warning รัวตลอดเกม")]
    public string fetchItemPrefix = "*";
    [Tooltip("Prefix ของวัตถุที่ต้องทำลาย — ห้ามใช้ glyph ที่ LiberationSans SDF ไม่มี")]
    public string destructiblePrefix = "!";

    [Header("Player-Relative Indicator (Off-Screen)")]
    [Tooltip("แสดงตัวนำทางรอบตัวผู้เล่นแทนขอบจอ")]
    public bool displayNearPlayer = true;
    [Tooltip("ระยะห่างจากตัวผู้เล่นบนหน้าจอ (พิกเซล)")]
    public float indicatorRadius = 120f;
    [Tooltip("เพดานรัศมี คิดเป็นสัดส่วนของด้านสั้นของจอ — indicatorRadius เป็นพิกเซลตายตัว " +
             "พอจอเล็กลงมันกินสัดส่วนจอมากขึ้นเรื่อยๆ (120px = 22% ของครึ่งจอที่ 1080p " +
             "แต่เป็น 67% ที่ 640x360) ตัวนี้กันไม่ให้วงบวมจนพ้นจอ")]
    [Range(0.05f, 0.5f)] public float maxRadiusScreenFraction = 0.25f;

    // ── Internal ──────────────────────────────────────────────────────────
    private enum Kind { Zone, FetchItem, Destructible }

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

        /// <summary>ข้อความที่เขียนลง distText ไปแล้ว — เขียนซ้ำเฉพาะตอนเปลี่ยนจริง
        /// ตอนอยู่ LateUpdate ป้ายนี้ถูกคิดใหม่ทุกเฟรม (120 ครั้ง/วินาทีที่ 120fps)
        /// ถ้าไม่กันไว้จะ alloc สตริงทิ้งและสั่ง TMP สร้าง mesh ใหม่ทุกเฟรมต่อ entry
        /// แพตเทิร์นเดียวกับ QuestCarryHUD ที่กันด้วย lastShown</summary>
        public string          lastText;
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

        DestructibleObjective.OnDestructibleSpawned   += OnDestructibleSpawned;
        DestructibleObjective.OnDestructibleDespawned += OnDestructibleRemoved;

        WinLoseUI.OnAnyResultTriggered += OnGameResult;
    }

    void OnDisable()
    {
        ZoneObjective.OnObjectiveSpawned   -= OnZoneSpawned;
        ZoneObjective.OnObjectiveCompleted -= OnZoneRemoved;
        ZoneObjective.OnObjectiveExpired   -= OnZoneRemoved;
        ZoneObjective.OnDeliveryProgress   -= OnDeliveryProgress;

        FetchItem.OnFetchItemSpawned   -= OnFetchItemSpawned;
        FetchItem.OnFetchItemDespawned -= OnFetchItemRemoved;

        DestructibleObjective.OnDestructibleSpawned   -= OnDestructibleSpawned;
        DestructibleObjective.OnDestructibleDespawned -= OnDestructibleRemoved;

        WinLoseUI.OnAnyResultTriggered -= OnGameResult;
    }

    /// <summary>เรียกจาก WinLoseUI.OnAnyResultTriggered — เกมจบแล้ว เก็บตัวชี้ทั้งหมด
    /// ไม่ให้ลูกศรยังชี้เป้าอยู่บนจอ VICTORY/DEFEAT (แพตเทิร์นเดียวกับ GameHUD.HideRespawnOverlay)</summary>
    void OnGameResult()
    {
        foreach (var e in _entries)
            if (e.rect != null) Destroy(e.rect.gameObject);
        _entries.Clear();
    }

    void Start()
    {
        _cam = Camera.main;
        ApplySortingOrder();
    }

    /// <summary>ตั้ง sortingOrder ให้ Canvas ที่มีอยู่ — ไม่แปะ Canvas ให้เองแล้ว
    ///
    /// ของเดิม AddComponent&lt;Canvas&gt;() ถ้าไม่เจอ ซึ่งซ่อนปัญหา config ไว้เงียบๆ
    /// และเพิ่ม canvas/batch โดยไม่มีใครรู้ · เป็นแพตเทิร์นเดียวกับที่ CarouselBase
    /// เพิ่งเลิกทำ (เลิก AddComponent&lt;RectMask2D&gt;() เปลี่ยนเป็นเช็คแล้วเตือน)</summary>
    void ApplySortingOrder()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning($"[ObjectiveIndicator] '{name}' ไม่มี Canvas — " +
                             "ตัวชี้จะใช้ลำดับการวาดของ Canvas แม่แทน canvasSortingOrder ที่ตั้งไว้ " +
                             "ถ้าต้องการคุมลำดับเอง ให้เพิ่ม Canvas + overrideSorting ใน Editor");
            return;
        }
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

    // ── Destructible events (quest DestroyObjects) ────────────────────────
    void OnDestructibleSpawned(DestructibleObjective d)
    {
        if (indicatorPrefab == null || d == null) return;
        CreateEntry(Kind.Destructible, d.transform, destructibleColor);
    }

    void OnDestructibleRemoved(DestructibleObjective d) => RemoveByTarget(d != null ? d.transform : null);

    // ── Entry helpers ─────────────────────────────────────────────────────
    Entry CreateEntry(Kind kind, Transform target, Color tint)
    {
        // กัน event ยิงซ้ำแล้วได้ลูกศรซ้อนกันสองอันบนเป้าเดียว
        // (BossHUDUI.OnBossSpawned กันด้วย _miniBars.ContainsKey มาตั้งแต่แรก)
        if (target == null || _entries.Exists(e => e.target == target)) return null;

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
    // LateUpdate ไม่ใช่ FixedUpdate — ตัวชี้คำนวณจาก _cam.WorldToScreenPoint
    // FixedUpdate รันที่ 50Hz คงที่ (Fixed Timestep 0.02) ไม่ผูกกับเฟรมเรต และรัน
    // **ก่อน** กล้องขยับ จึงได้ตำแหน่งจาก transform กล้องของเฟรมที่แล้ว
    // ผลคือเล่นที่เฟรมเรตสูงตัวชี้กระตุกและตามกล้องไม่ทัน ส่วนตอนเฟรมตกก็รันซ้ำหลายรอบเปล่าๆ
    // FollowCamera ขยับกล้องใน LateUpdate เหมือนกัน แต่ต่างคนละ component
    // ถ้าลำดับสลับกันจะช้าไปหนึ่งเฟรม (ยอมรับได้ ดีกว่าช้าตามเฟรมเรตแบบเดิม)
    void LateUpdate()
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

        // base scale ต่างกันตาม kind (ของกระจายรอบแมพเล็กกว่าตัวโซน)
        float baseScale = e.kind == Kind.Zone ? 1f : fetchItemScale;

        if (onScreen)
        {
            // ต้องบีบ — onScreen ปล่อยให้ y ขึ้นไปถึง sh - edgeMargin แล้ว onScreenOffsetY
            // บวกทับอีก เป้าที่อยู่แถบบนของจอจึงถูกดันพ้นขอบบนทุกความละเอียด
            e.rect.position   = ClampToScreen(
                new Vector3(screenPos.x, screenPos.y + onScreenOffsetY, 0f), e.rect);
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
                // แสดงใกล้ตัวผู้เล่น — จำกัดรัศมีตามด้านสั้นของจอก่อน ไม่งั้นจอเล็กจะได้วง
                // ที่ใหญ่เกินครึ่งจอ แล้วค่อยบีบตำแหน่งสุดท้ายเข้ากรอบอีกชั้น
                // (ผู้เล่นไม่ได้อยู่กึ่งกลางจอเสมอ — FollowCamera ใช้ offset คงที่)
                float maxR = Mathf.Min(sw, sh) * maxRadiusScreenFraction;
                float r    = Mathf.Min(indicatorRadius, maxR);
                e.rect.position = ClampToScreen(center + dir * r, e.rect);
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
                    // ห้ามใส่สัญลักษณ์นอก ASCII กลับมาที่นี่ — ไล่ cmap ของไฟล์ฟอนต์แล้ว
                    // ทั้ง LiberationSans.ttf และ Sarabun-*.ttf **ไม่มี** U+2605 (STAR),
                    // U+26A0 (WARN), U+26A1 (BOLT), U+23F1 (TIMER) เลยสักตัว
                    // ป้ายนี้ถูกคิดใหม่ทุกเฟรมใน LateUpdate = TMP warning รัวตลอดเกม
                    //
                    // (คอมเมนต์เดิมตรงนี้เขียนว่า "★ ใช้ได้ เพราะฟอนต์มี" ซึ่งไม่จริง
                    //  และเป็นเหตุให้ ★ ถูกใส่ไว้ 4 จุดทั่วระบบ objective)
                    // ถ้าอยากได้ไอคอนจริง ต้อง import ฟอนต์สัญลักษณ์เป็น fallback
                    // หรือทำ TMP sprite asset ก่อน
                    countPart = e.zone.ActiveQuestType switch
                    {
                        ZoneObjective.QuestType.FetchAndDeliver => $"{e.delivered}/{e.required}",
                        ZoneObjective.QuestType.Survive         => $"{e.delivered}/{e.required}s",
                        ZoneObjective.QuestType.DestroyObjects  => $"{e.delivered}/{e.required}",
                        ZoneObjective.QuestType.KillInZone      => $"{e.delivered}/{e.required}",
                        ZoneObjective.QuestType.SealTheRift     => $"{e.delivered}/{e.required}s",
                        _                                        => "",
                    };
                }

                if (string.IsNullOrEmpty(countPart))      text = distPart;
                else if (string.IsNullOrEmpty(distPart))  text = countPart;
                else                                      text = $"{distPart}  {countPart}";
            }
            else
            {
                string prefix = e.kind == Kind.Destructible ? destructiblePrefix : fetchItemPrefix;
                text = string.IsNullOrEmpty(distPart) ? prefix : $"{prefix} {distPart}";
            }

            // เขียนเฉพาะตอนเปลี่ยนจริง — ดู Entry.lastText
            if (e.lastText != text)
            {
                e.lastText      = text;
                e.distText.text = text;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    /// <summary>บีบตำแหน่งให้อยู่ในจอเสมอ — เว้นขอบ edgeMargin และเผื่อครึ่งขนาดของ icon เอง
    /// ไม่งั้นตัวชี้จะโผล่ครึ่งเดียวคาขอบจอ
    ///
    /// โหมดขอบจอ (else ด้านบน) ไม่ต้องใช้ เพราะสูตรมันฉายลงกรอบขอบจออยู่แล้วโดยธรรมชาติ
    /// อีกสองโหมดวางตำแหน่งเองดื้อๆ จึงหลุดจอได้</summary>
    Vector3 ClampToScreen(Vector3 pos, RectTransform rect)
    {
        float halfW = 0f, halfH = 0f;
        if (rect != null)
        {
            halfW = rect.rect.width  * 0.5f * Mathf.Abs(rect.lossyScale.x);
            halfH = rect.rect.height * 0.5f * Mathf.Abs(rect.lossyScale.y);
        }

        float minX = edgeMargin + halfW, maxX = Screen.width  - edgeMargin - halfW;
        float minY = edgeMargin + halfH, maxY = Screen.height - edgeMargin - halfH;

        // จอเล็กมากจนขอบสองข้างชนกัน — ยึดกึ่งกลางจอไว้ ดีกว่าส่ง Clamp ที่ min > max
        pos.x = maxX >= minX ? Mathf.Clamp(pos.x, minX, maxX) : Screen.width  * 0.5f;
        pos.y = maxY >= minY ? Mathf.Clamp(pos.y, minY, maxY) : Screen.height * 0.5f;
        return pos;
    }

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
