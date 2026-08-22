using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// กลไกวงหมุนแบบตัวที่เลือกอยู่กลางเสมอ — ส่วนที่ไม่เกี่ยวกับว่าข้อมูลเป็นอะไร
/// ตัวละคร แมพ หรืออย่างอื่นใช้ตัวนี้ร่วมกันได้ subclass บอกแค่ "มีกี่ชิ้น" กับ "ผูกข้อมูลยังไง"
///
/// ต้องวางบน object ของแผงที่จะลาก ไม่ใช่บน Canvas —
/// IDragHandler/IScrollHandler ของ uGUI ส่ง event ให้เฉพาะ object ที่ pointer ชี้โดน
/// กับ parent ของมันเท่านั้น panel ที่คั่นอยู่จะดักไปก่อน
///
/// พิกัด: offset นับเป็นหน่วย "ช่อง" — offset 0 = ชิ้นที่ 0 อยู่กลาง
/// ชิ้นที่ index p วางที่ y = (offset - p) * pitch
/// offset เพิ่ม = การ์ดเลื่อนขึ้น = เดินไปหา index ที่มากขึ้น (ตรงกับการลากขึ้น)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class CarouselBase : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    // ── Layout ────────────────────────────────────────────────────────────
    [Header("Layout")]
    [Tooltip("ระยะห่างระหว่างช่อง (px) — ต้องเท่าความสูงการ์ดถึงจะเรียงชิดกันพอดี")]
    public float pitch = 250f;

    [Tooltip("จำนวน view ที่สร้างจริง — 6 คือ 5 ช่องที่เห็น บวกอีก 1 เผื่อตอนลากค้างกลางทาง")]
    public int viewCount = 6;

    // ── Feel ──────────────────────────────────────────────────────────────
    [Header("Feel")]
    [Tooltip("เวลาที่ใช้ไถลเข้าช่องหลังปล่อยนิ้ว (วินาที)")]
    public float snapSmoothTime = 0.12f;

    [Tooltip("ระยะที่ถือว่าเข้าช่องแล้ว (หน่วยช่อง) — ต่ำกว่านี้จะ snap ตรงเป๊ะแล้วยิง OnSettled")]
    public float settleEpsilon = 0.002f;

    [Tooltip("หมุนกี่ช่องต่อการหมุนลูกกลิ้งหนึ่งครั้ง")]
    public int wheelStep = 1;

    // ── Appearance by distance ────────────────────────────────────────────
    [Header("Card Appearance")]
    [Tooltip("ขนาดการ์ดใบกลาง")]
    public float centerScale = 1f;
    [Tooltip("ขนาดการ์ดที่ห่างจากกลาง 2 ช่อง — ระหว่างนั้นไล่ต่อเนื่อง")]
    public float edgeScale = 0.65f;
    [Tooltip("ความทึบของการ์ดที่ห่างจากกลาง 2 ช่อง")]
    public float edgeAlpha = 0.45f;

    [Tooltip("ปิด RectMask2D ของการ์ดใบกลาง เพื่อให้ภาพล้นกรอบการ์ดออกมาได้ - " +
             "หา RectMask2D ทั้งกิ่งของการ์ด จะอยู่บน root หรือบนลูกที่ครอบภาพก็ได้ - ไม่มีก็ข้ามไปเฉยๆ")]
    public bool unmaskCenterCard = true;

    [Tooltip("ใส่ RectMask2D ที่แผงเพื่อตัดใบที่โผล่ขอบ - " +
             "ปิดช่องนี้ถ้าอยากให้ภาพใบกลางล้นพ้นขอบแผงออกไปได้ แล้วใช้ edgeAlpha กลบใบที่โผล่แทน")]
    public bool clipToPanel = true;

    // ── Arrow buttons ─────────────────────────────────────────────────────
    [Header("Arrow Buttons (optional)")]
    public Button upButton;
    public Button downButton;

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>ยิงเมื่อการ์ดเข้าช่องนิ่งแล้วและ index เปลี่ยนจากครั้งก่อน</summary>
    public event System.Action<int> OnSettled;

    // ── View ──────────────────────────────────────────────────────────────
    protected class SlotView
    {
        public GameObject      go;
        public RectTransform   rect;
        public CanvasGroup     group;
        public Button          button;
        public int             position;         // index เสมือน อาจติดลบหรือเกินจำนวนชิ้น
        public int             boundIndex = -1;  // index ที่ผูกไว้ล่าสุด (-1 = ยังไม่ผูก)
        public RectMask2D      cardMask;         // null ได้ ถ้า prefab ไม่ได้ใส่มา
    }

    protected readonly List<SlotView> views = new();
    protected RectTransform slotParent;

    private float _offset;      // ตำแหน่งต่อเนื่อง หน่วยช่อง
    private float _target;      // ช่องปลายทางที่กำลังไถลเข้าหา
    private float _vel;
    private bool  _dragging;
    private bool  _settled = true;
    private int   _lastSettledIndex = -1;

    public bool IsSettled   => _settled;
    /// <summary>index ของชิ้นที่อยู่กลางตอนนี้ — ระหว่างลากคือชิ้นที่ใกล้กลางที่สุด</summary>
    public int  CenterIndex => Wrap(Mathf.RoundToInt(_offset));

    // ══════════════════════════════════════════════════════════════════════
    // สิ่งที่ subclass ต้องบอก
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>จำนวนชิ้นในวง — 0 = ไม่มีอะไรให้หมุน</summary>
    protected abstract int ItemCount { get; }

    /// <summary>เรียกครั้งเดียวตอนสร้าง view — subclass เก็บ component ที่ต้องใช้ไว้เองตาม slotIndex</summary>
    protected abstract void OnViewBuilt(int slotIndex, GameObject go);

    /// <summary>ผูกข้อมูลชิ้นที่ itemIndex ลง view ช่อง slotIndex — เรียกเฉพาะตอนข้อมูลเปลี่ยนจริง</summary>
    protected abstract void BindView(int slotIndex, int itemIndex);

    /// <summary>ตั้งสถานะ "อยู่กลาง" ของ view — เรียกทุกเฟรมที่ Apply ทำงาน</summary>
    protected abstract void SetViewSelected(int slotIndex, bool selected);

    // ══════════════════════════════════════════════════════════════════════
    // Setup
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// สร้าง view ตามจำนวนที่ตั้งไว้แล้วจัดวางให้จบในตัว
    /// subclass ต้องเตรียมข้อมูลให้ ItemCount ใช้ได้ก่อนเรียกตัวนี้
    /// </summary>
    protected void SetupViews(GameObject cardTemplate, RectTransform container)
    {
        slotParent = container;

        if (ItemCount == 0)
        {
            Debug.LogWarning("[" + GetType().Name + "] ไม่มีข้อมูล — ไม่มีอะไรให้หมุน");
            return;
        }
        if (cardTemplate == null || slotParent == null)
        {
            Debug.LogError("[" + GetType().Name + "] cardTemplate หรือ container เป็น null — สร้าง view ไม่ได้");
            return;
        }

        // LayoutGroup ที่ตั้งไว้ใน scene จะแย่งจัดตำแหน่ง — ต้องปิดก่อน
        var layout = slotParent.GetComponent<LayoutGroup>();
        if (layout != null && layout.enabled)
        {
            layout.enabled = false;
            Debug.Log("[" + GetType().Name + "] ปิด " + layout.GetType().Name + " บน container — carousel วางตำแหน่งเอง");
        }
        var fitter = slotParent.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled) fitter.enabled = false;

        // mask ซ้อนกันคิดแบบทับซ้อน — ตราบใดที่ตัวนี้ยังอยู่ ภาพจะล้นได้แค่กรอบการ์ด
        // ไม่พ้นขอบแผง ต่อให้ปิด mask ของการ์ดใบกลางแล้วก็ตาม
        var panelMask = GetComponent<RectMask2D>();
        if (clipToPanel && panelMask == null)
        {
            gameObject.AddComponent<RectMask2D>();
            Debug.Log("[" + GetType().Name + "] เพิ่ม RectMask2D ให้แผง — ตัดใบที่โผล่ขอบ");
        }
        else if (!clipToPanel && panelMask != null)
        {
            panelMask.enabled = false;
            Debug.Log("[" + GetType().Name + "] ปิด RectMask2D ของแผงตาม clipToPanel — " +
                      "ภาพล้นพ้นขอบได้ แต่ใบที่ ±2 จะไม่ถูกตัด ต้องพึ่ง edgeAlpha กลบแทน");
        }

        // ซ่อน template ได้เฉพาะตอนที่มันเป็น object ในซีน — ถ้าเป็น prefab asset
        // SetActive จะไปแก้ไฟล์ asset บนดิสก์ กลายเป็นไฟล์ถูกแก้ใน git โดยไม่มีใครตั้งใจ
        if (cardTemplate.scene.IsValid()) cardTemplate.SetActive(false);

        BuildViews(cardTemplate);

        if (upButton   != null) upButton.onClick.AddListener(()   => Step(-1));
        if (downButton != null) downButton.onClick.AddListener(() => Step(+1));

        if (unmaskCenterCard && views.Count > 0 && views[0].cardMask == null)
        {
            Debug.LogWarning("[" + GetType().Name + "] unmaskCenterCard เปิดอยู่ แต่หา RectMask2D ในกิ่งของการ์ดไม่เจอ — " +
                             "ภาพจะไม่ล้นกรอบ · ใส่ RectMask2D บน root ของการ์ด หรือบนลูกที่ครอบภาพ");
        }

        // จัดวางให้จบในตัว ไม่ฝากไว้กับ JumpTo ของผู้เรียกซึ่งถูกข้ามได้
        Apply();

        Debug.Log("[" + GetType().Name + "] พร้อมแล้ว — " + ItemCount + " ชิ้น · view " + views.Count + " ใบ" +
                  " · card mask " + (views.Count > 0 && views[0].cardMask != null ? "เจอ" : "ไม่เจอ"));
    }

    void BuildViews(GameObject cardTemplate)
    {
        foreach (var v in views)
            if (v.go != null) Destroy(v.go);
        views.Clear();

        int n = Mathf.Max(3, viewCount);
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(cardTemplate, slotParent);
            go.name = "Slot_" + i;
            go.SetActive(true);

            var rect = go.transform as RectTransform;
            // ยึดกลางแผงทุกใบ ตำแหน่งจริงมาจาก anchoredPosition ที่คำนวณรายเฟรม
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot     = new Vector2(0.5f, 0.5f);

            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();

            // ค้นทั้งกิ่ง ไม่ใช่แค่ root — วาง mask ไว้บน wrapper ที่ครอบเฉพาะภาพเป็นโครงที่ถูกกว่า
            // (พื้นหลังการ์ดกับพื้นที่รับคลิกของ Button จะได้ไม่โดนตัดไปด้วย)
            var cardMask = go.GetComponentInChildren<RectMask2D>(true);

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            // ปิด navigation กันคีย์ Submit ยิงการ์ดที่โฟกัสค้างอยู่
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

            var view = new SlotView { go = go, rect = rect, group = group,
                                      button = btn, cardMask = cardMask };
            btn.onClick.AddListener(() => OnSlotClicked(view));
            views.Add(view);

            OnViewBuilt(i, go);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Public control
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>วางชิ้นที่ index ไว้กลางทันที ไม่มีอนิเมชัน — ใช้ตอนเปิดหน้ามาครั้งแรก</summary>
    public void JumpTo(int index)
    {
        if (ItemCount == 0) return;
        _offset = _target = Wrap(index);
        _vel      = 0f;
        _dragging = false;
        _settled  = true;
        _lastSettledIndex = CenterIndex;
        Apply();
        OnCenterSettledVisual(true);
    }

    /// <summary>หมุนไป steps ช่อง (บวก = ลงล่าง ไปหา index มากขึ้น)</summary>
    public void Step(int steps)
    {
        if (ItemCount == 0 || steps == 0) return;
        _target  = Mathf.Round(_target) + steps;
        _settled = false;
    }

    /// <summary>บังคับผูกข้อมูลใหม่ทุก view — เรียกเมื่อข้อมูลเบื้องหลังเปลี่ยน (เช่นปลดล็อกแล้ว)</summary>
    public void RefreshAll()
    {
        foreach (var v in views) v.boundIndex = -1;
        Apply();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Input
    // ══════════════════════════════════════════════════════════════════════
    public void OnBeginDrag(PointerEventData e)
    {
        if (ItemCount == 0) return;
        _dragging = true;
        _settled  = false;
        _vel      = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging || pitch <= 0.01f) return;
        // ลากขึ้น (delta.y เป็นบวก) → offset เพิ่ม → การ์ดเลื่อนขึ้นตามนิ้ว
        _offset += e.delta.y / pitch;
        Apply();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;
        _dragging = false;
        _target   = Mathf.Round(_offset);   // ปล่อยแล้วไถลเข้าช่องที่ใกล้สุด
    }

    public void OnScroll(PointerEventData e)
    {
        if (ItemCount == 0) return;
        if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;
        // ลูกกลิ้งขึ้น (บวก) = ย้อนไป index น้อยลง ให้ทิศเดียวกับลิสต์ทั่วไป
        Step(e.scrollDelta.y > 0f ? -wheelStep : wheelStep);
    }

    void OnSlotClicked(SlotView view)
    {
        if (ItemCount == 0) return;
        int centerPos = Mathf.RoundToInt(_offset);
        if (view.position == centerPos) return;   // ใบกลางอยู่แล้ว ไม่ต้องหมุน
        _target  = view.position;
        _settled = false;
    }

    // ══════════════════════════════════════════════════════════════════════
    // Tick
    // ══════════════════════════════════════════════════════════════════════
    protected virtual void Update()
    {
        if (ItemCount == 0) return;

        // เงื่อนไขคือ "ยังไม่นิ่ง" ไม่ใช่ "ยังห่างจากเป้า" — ปล่อยนิ้วตอนที่ offset
        // บังเอิญตรงช่องพอดีจะไม่เหลือระยะให้ไถล ถ้าเช็กระยะ Settle จะไม่ถูกเรียกเลย
        if (!_dragging && !_settled)
        {
            if (Mathf.Abs(_target - _offset) < settleEpsilon)
            {
                _offset = _target;
                _vel    = 0f;
                Settle();
            }
            else
            {
                _offset = Mathf.SmoothDamp(_offset, _target, ref _vel, snapSmoothTime);
            }
            Apply();
        }

        OnCenterSettledVisual(false);
    }

    /// <summary>เข้าช่องนิ่งแล้ว — จุดเดียวที่ยิง OnSettled ออกไป</summary>
    void Settle()
    {
        if (_settled) return;
        _settled = true;

        // กัน offset ไหลไกลเรื่อยๆ จนเสียความละเอียด float ในเซสชันยาว
        // ตำแหน่งการ์ดคำนวณแบบสัมพัทธ์ ย่อค่าลงมาแล้วภาพไม่ขยับ
        int idx = CenterIndex;
        _offset = _target = idx;
        // ไม่เรียก Apply ที่นี่ — ผู้เรียกเดียวคือ Update ซึ่งเรียกต่อทันทีในเฟรมเดียวกัน
        if (idx == _lastSettledIndex) return;
        _lastSettledIndex = idx;
        OnSettled?.Invoke(idx);
    }

    /// <summary>ให้ subclass ทำเอฟเฟกต์ที่อิงสถานะนิ่ง/กำลังเลื่อน (เช่นภาพใหญ่ที่จางตอนลาก)</summary>
    protected virtual void OnCenterSettledVisual(bool instant) { }

    /// <summary>true เมื่อหยุดนิ่งและไม่ได้กำลังลาก — subclass ใช้ตัดสินว่าจะโชว์ของที่อิงตัวกลางไหม</summary>
    protected bool IsIdle => _settled && !_dragging;

    // ══════════════════════════════════════════════════════════════════════
    // Layout + visuals
    // ══════════════════════════════════════════════════════════════════════
    protected void Apply()
    {
        if (views.Count == 0 || ItemCount == 0) return;

        int half    = views.Count / 2;
        int basePos = Mathf.FloorToInt(_offset) - half + 1;

        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            v.position = basePos + i;

            float delta = v.position - _offset;              // ระยะจากกลาง หน่วยช่อง
            v.rect.anchoredPosition = new Vector2(0f, -delta * pitch);

            float t = Mathf.Clamp01(Mathf.Abs(delta) / 2f);  // 0 = กลาง, 1 = ห่าง 2 ช่องขึ้นไป
            float s = Mathf.Lerp(centerScale, edgeScale, t);
            v.rect.localScale = new Vector3(s, s, 1f);
            v.group.alpha     = Mathf.Lerp(1f, edgeAlpha, t);

            // ผูกข้อมูลเฉพาะตอนชิ้นที่ view นี้แสดงเปลี่ยนจริง — การผูกมักไล่ลิสต์/แตะ TMP
            // ถ้าทำทุกเฟรมระหว่างลากคือเสียเปล่าทั้งหมด ส่วนตำแหน่ง/ขนาด/ความจางด้านบนเปลี่ยนจริงทุกเฟรม
            int itemIdx = Wrap(v.position);
            if (v.boundIndex != itemIdx)
            {
                v.boundIndex = itemIdx;
                BindView(i, itemIdx);
            }

            bool isCenter = Mathf.Abs(delta) < 0.5f;
            SetViewSelected(i, isCenter);

            // ใบกลางปิด mask ตัวเองเพื่อให้ภาพทะลุกรอบการ์ดออกมา ใบอื่นเปิดไว้ให้อยู่ในกรอบ
            // toggle เฉพาะตอนค่าเปลี่ยน — การสลับ enabled สั่งคำนวณพื้นที่ clip ใหม่ทั้งซับทรี
            if (v.cardMask != null)
            {
                bool wantMask = !(unmaskCenterCard && isCenter);
                if (v.cardMask.enabled != wantMask) v.cardMask.enabled = wantMask;
            }
        }
    }

    protected int Wrap(int i)
    {
        int n = ItemCount;
        if (n <= 0) return 0;
        return ((i % n) + n) % n;
    }
}
