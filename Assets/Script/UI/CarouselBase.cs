using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>แกนที่การ์ดเรียงตาม</summary>
public enum CarouselAxis
{
    /// <summary>เรียงลงล่าง — ลิสต์ตัวละครฝั่งซ้าย</summary>
    Vertical   = 0,
    /// <summary>เรียงไปขวา — แถบแมพที่วางขวางใต้ภาพพรีวิว</summary>
    Horizontal = 1,
}

/// <summary>
/// การ์ดเกาะตรงไหนของแผง — ตัวนี้เปลี่ยน "ความหมายของการเลื่อน" ไปทั้งชุด ไม่ใช่แค่ตำแหน่ง
/// </summary>
public enum CarouselAlign
{
    /// <summary>
    /// ตัวที่เลือกอยู่กลางแผงเสมอ เลื่อนแล้ววนไม่รู้จบ — การเลื่อน **คือ** การเลือก
    /// เหมาะกับของที่มีเยอะและไม่มีจุดเริ่ม/จุดจบ
    /// </summary>
    Center = 0,

    /// <summary>
    /// ชิ้นแรกเกาะขอบต้นของแผง ไล่ไปทางท้าย ไม่วน — การเลื่อนกับการเลือก **แยกกัน**
    /// เลื่อนคือพาสายตาไปดู เลือกคือคลิก · นี่คือลิสต์ธรรมดา และเป็นสิ่งที่แบบ P3R วางไว้
    /// ของน้อยกว่าที่แผงใส่ได้จะไม่เลื่อนเลย วางนิ่งชิดขอบต้นตามแบบ
    /// </summary>
    Start = 1,
}

/// <summary>
/// กลไกลิสต์/วงหมุนที่ใช้ร่วมกันทั้งเกม — **ทุกอย่าง** อยู่ในคลาสนี้คลาสเดียว
/// ทั้งการวาง การลาก การไถลเข้าช่อง การผูกข้อมูล และชั้นภาพใหญ่ของตัวที่เลือก
///
/// subclass เหลือหน้าที่แค่สามบรรทัด — "ผูกข้อมูลลงการ์ดยังไง" "ตั้งสถานะเลือกยังไง"
/// และ "ภาพใหญ่ของชิ้นนี้คือสไปรต์ไหน" · ดู <c>CharacterCarousel</c> / <c>MapCarousel</c>
///
/// ═══ ทำไมยังต้องมีสอง subclass ทั้งที่โค้ดเหมือนกันหมด ═══
///
/// Unity แปะ component ที่เป็น generic แบบเปิด (<c>CarouselBase&lt;,&gt;</c>) ลง GameObject ไม่ได้
/// ต้องมีคลาสปิดชนิดแล้วให้แปะ — <c>class CharacterCarousel : CarouselBase&lt;CharacterData, CharacterCardUI&gt;</c>
/// จึงเป็นตัวที่บางที่สุดที่ยังทำงานได้ · ความซ้ำที่เหลืออยู่คือชื่อคลาสกับ alias อ่านง่าย ไม่ใช่ตรรกะ
///
/// ═══ ต้องวางบนไหน ═══
///
/// ต้องอยู่บน **แผงที่จะลาก** ไม่ใช่บน Canvas — <c>IDragHandler</c>/<c>IScrollHandler</c> ของ uGUI
/// ส่ง event ให้เฉพาะ object ที่ pointer ชี้โดนกับ parent ของมัน panel ที่คั่นอยู่จะดักไปก่อน
/// ผู้เรียกค้นด้วย <c>GetComponentInParent</c> จาก container ขึ้นไป จะอยู่ชั้นไหนก็ได้ที่อยู่เหนือมัน
///
/// ═══ พิกัด ═══
///
/// นับเป็นหน่วย "ช่อง" ทั้งหมด · ชิ้นที่ตำแหน่ง p วางห่างจากจุดอ้างอิง <c>(p - scroll) * pitch</c>
/// จุดอ้างอิงคือกลางแผงเมื่อ <see cref="CarouselAlign.Center"/> และขอบต้นแผงเมื่อ
/// <see cref="CarouselAlign.Start"/> — สูตรเดียวกัน ต่างกันที่ anchor ของการ์ด
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class CarouselBase<TData, TCard> : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    where TData : UnityEngine.Object
    where TCard : Component
{
    // ── Layout ────────────────────────────────────────────────────────────
    [Header("Layout")]
    [Tooltip("แกนที่การ์ดเรียงตาม — ลิสต์ตัวละครเป็น Vertical แถบแมพเป็น Horizontal")]
    public CarouselAxis axis = CarouselAxis.Vertical;

    [Tooltip("การ์ดเกาะตรงไหน — Start = ลิสต์ชิดขอบต้น (ตามแบบ) · Center = วงหมุนล็อกกลาง")]
    public CarouselAlign align = CarouselAlign.Start;

    [Tooltip("ระยะจากกึ่งกลางการ์ดใบหนึ่งถึงใบถัดไป (px)\n" +
             "ต้องเท่าขนาดการ์ดตามแกน + ช่องไฟ ไม่งั้นจะห่างเกินหรือทับกัน")]
    public float pitch = 118f;

    [Tooltip("จำนวน view ที่สร้างจริง — เผื่อไว้มากกว่าที่เห็นหนึ่งใบ สำหรับใบที่โผล่ครึ่งตอนเลื่อน\n" +
             "ถ้าของมีน้อยกว่านี้ จะสร้างเท่าที่มี ไม่ปล่อยให้การ์ดซ้ำ")]
    public int viewCount = 8;

    // ── Feel ──────────────────────────────────────────────────────────────
    [Header("Feel")]
    [Tooltip("เวลาที่ใช้ไถลเข้าช่องหลังปล่อยนิ้ว (วินาที)")]
    public float snapSmoothTime = 0.12f;

    [Tooltip("ระยะที่ถือว่าเข้าช่องแล้ว (หน่วยช่อง) — ต่ำกว่านี้จะ snap ตรงเป๊ะ")]
    public float settleEpsilon = 0.002f;

    [Tooltip("หมุนกี่ช่องต่อการหมุนลูกกลิ้งหนึ่งครั้ง")]
    public int wheelStep = 1;

    // ── Appearance by distance ────────────────────────────────────────────
    [Header("Card Appearance")]
    [Tooltip("ขนาดการ์ดใบที่เลือกอยู่ — แบบ P3R ใช้ 1 ทุกใบ เน้นด้วยสีพื้นแทนขนาด")]
    public float centerScale = 1f;

    [Tooltip("ขนาดการ์ดที่ห่างจากใบที่เลือก 2 ช่อง — เท่ากับ centerScale = ทุกใบขนาดเดียวกัน")]
    public float edgeScale = 1f;

    [Tooltip("ไล่ความจางตามระยะห่าง - ปิดไว้ = script ไม่แตะ CanvasGroup.alpha เลย\n" +
             "และไม่แปะ CanvasGroup ให้การ์ดที่ยังไม่มี ความทึบจึงเป็นของที่ตั้งไว้ใน Editor ล้วนๆ")]
    public bool fadeByDistance = false;

    [Tooltip("ความทึบของการ์ดที่ห่าง 2 ช่อง - มีผลเมื่อ fadeByDistance เปิดเท่านั้น")]
    public float edgeAlpha = 0.45f;

    [FormerlySerializedAs("unmaskCenterCard")]
    [Tooltip("ปิด RectMask2D ของการ์ดใบที่เลือก เพื่อให้ภาพล้นกรอบการ์ดออกมาได้ - " +
             "หา RectMask2D ทั้งกิ่งของการ์ด จะอยู่บน root หรือบนลูกที่ครอบภาพก็ได้ - ไม่มีก็ข้ามไปเฉยๆ")]
    public bool unmaskSelectedCard = false;

    [Tooltip("บอกว่า 'ตั้งใจให้มีการตัดขอบ' เฉยๆ - ไม่ได้แปะ mask ให้เอง\n" +
             "เปิดไว้แล้วไม่มี Mask/RectMask2D เหนือ container จะเตือนใน Console\n" +
             "ปิดถ้าอยากให้การ์ดล้นพ้นขอบแผงออกไปได้")]
    public bool clipToPanel = true;

    // ── Arrow buttons ─────────────────────────────────────────────────────
    [Header("Arrow Buttons (optional)")]
    [FormerlySerializedAs("upButton")]
    [Tooltip("ถอยไปชิ้นก่อนหน้า — ขึ้นเมื่อเรียงแนวตั้ง ซ้ายเมื่อเรียงแนวนอน")]
    public Button prevButton;

    [FormerlySerializedAs("downButton")]
    [Tooltip("ไปชิ้นถัดไป")]
    public Button nextButton;

    // ── Featured layer ────────────────────────────────────────────────────
    [Header("Featured Layer (ภาพใหญ่นอก mask — ปล่อยว่างได้)")]
    [FormerlySerializedAs("heroGroup"), FormerlySerializedAs("previewGroup")]
    [Tooltip("CanvasGroup ของภาพใหญ่ที่อยู่ **นอก** แถบการ์ด — ห้ามชี้มาที่ container ของการ์ดเอง")]
    public CanvasGroup featuredGroup;

    [FormerlySerializedAs("heroImage"), FormerlySerializedAs("previewImage")]
    [Tooltip("Image ที่จะใส่ภาพใหญ่ของชิ้นที่เลือกอยู่")]
    public Image featuredImage;

    [FormerlySerializedAs("heroFadeSpeed"), FormerlySerializedAs("previewFadeSpeed")]
    [Tooltip("ความเร็วจาง/โผล่ของภาพใหญ่ (หน่วยอัลฟาต่อวินาที)")]
    public float featuredFadeSpeed = 6f;

    [FormerlySerializedAs("heroIdleAlpha"), FormerlySerializedAs("previewIdleAlpha")]
    [Range(0f, 1f)]
    [Tooltip("ความทึบของภาพใหญ่ตอนนิ่งแล้ว")]
    public float featuredIdleAlpha = 1f;

    [FormerlySerializedAs("heroMovingAlpha"), FormerlySerializedAs("previewMovingAlpha")]
    [Range(0f, 1f)]
    [Tooltip("ความทึบระหว่างที่ยังลาก/ไถลอยู่ - ตั้งเท่ากับ featuredIdleAlpha = ไม่จางเลย")]
    public float featuredMovingAlpha = 0f;

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>ยิงเมื่อชิ้นที่เลือก **เปลี่ยน** — จุดเดียวที่การเลือกออกไปข้างนอก</summary>
    public event System.Action<int> OnSettled;

    // ══════════════════════════════════════════════════════════════════════
    // View
    // ══════════════════════════════════════════════════════════════════════
    protected class SlotView
    {
        public GameObject    go;
        public RectTransform rect;
        public CanvasGroup   group;
        public Button        button;
        public int           position;         // index เสมือน อาจติดลบหรือเกินจำนวนชิ้น
        public int           boundIndex = -1;  // index ที่ผูกไว้ล่าสุด (-1 = ยังไม่ผูก)
        public RectMask2D    cardMask;         // null ได้ ถ้า prefab ไม่ได้ใส่มา
        public TCard         card;
    }

    protected readonly List<SlotView> views = new();
    protected RectTransform slotParent;

    private readonly List<TData> items = new();

    private Color   selectedColor, normalColor;
    private Vector2 templateSize;

    private float _scroll;            // ตำแหน่งเลื่อนต่อเนื่อง หน่วยช่อง
    private float _scrollTarget;
    private float _vel;
    private bool  _dragging;
    private bool  _settled = true;
    private int   _selected;          // index ที่เลือก — มีความหมายเฉพาะโหมด Start
    private int   _lastSettledIndex = -1;
    private float _lastAxisSize = -1f;

    // ── สถานะที่อ่านได้จากข้างนอก ─────────────────────────────────────────
    public int  Count     => items.Count;
    public bool IsSettled => _settled;

    /// <summary>index ของชิ้นที่เลือกอยู่ — โหมด Center คือชิ้นที่อยู่กลาง</summary>
    public int SelectedIndex => align == CarouselAlign.Center
        ? Wrap(Mathf.RoundToInt(_scroll))
        : Mathf.Clamp(_selected, 0, Mathf.Max(0, items.Count - 1));

    protected int ItemCount => items.Count;

    /// <summary>true เมื่อหยุดนิ่งและไม่ได้กำลังลาก</summary>
    protected bool IsIdle => _settled && !_dragging;

    // ══════════════════════════════════════════════════════════════════════
    // สิ่งที่ subclass ต้องบอก — สามอย่าง จบ
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ผูกข้อมูลหนึ่งชิ้นลงการ์ดหนึ่งใบ</summary>
    protected abstract void Bind(TCard card, TData data, Color selColor, Color normColor);

    /// <summary>ตั้งสถานะ "ถูกเลือกอยู่" ของการ์ด</summary>
    protected abstract void SetCardSelected(TCard card, bool selected);

    /// <summary>ภาพใหญ่ของชิ้นนี้ — null ได้ ถ้าชนิดนี้ไม่มีภาพใหญ่</summary>
    protected abstract Sprite FeaturedSpriteOf(TData data);

    // ══════════════════════════════════════════════════════════════════════
    // Setup
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// เรียกครั้งเดียวจากตัวคุมจอตอน Start — เตรียมข้อมูล สร้าง view แล้ววางให้จบในตัว
    /// </summary>
    public void Setup(List<TData> source, GameObject cardTemplate,
                      RectTransform container, Color selColor, Color normColor)
    {
        items.Clear();
        if (source != null)
            foreach (var d in source)
                if (d != null) items.Add(d);

        selectedColor = selColor;
        normalColor   = normColor;

        // ตรวจก่อนว่าไม่ได้ชี้ผิดมาที่ container ของการ์ดเอง — ดู ValidateFeaturedGroup
        featuredGroup = ValidateFeaturedGroup(featuredGroup, container);
        featuredImage = ValidateFeaturedImage(featuredImage);

        // ภาพใหญ่วางทับการ์ดและใหญ่กว่า ถ้ามันรับ raycast จะดูดคลิกกับการลากไปหมด
        // การ์ดใต้มันจะกดไม่ได้เลย — มันเป็นของประดับล้วน จึงปิดการรับ input ทิ้งตั้งแต่ต้น
        if (featuredGroup != null)
        {
            featuredGroup.blocksRaycasts = false;
            featuredGroup.interactable   = false;
        }
        if (featuredImage != null) featuredImage.raycastTarget = false;

        SetupViews(cardTemplate, container);
    }

    /// <summary>
    /// กัน featuredGroup ชี้ผิดมาที่ container ของการ์ดเอง (หรือพ่อของมัน)
    ///
    /// ต่อผิดแบบนี้จะพังสองทางพร้อมกัน — OnFeaturedVisual เขียน alpha ลงไป
    /// ทั้งแถบการ์ดเลยจางหายตอนลาก และการปิด blocksRaycasts ก็ไปปิดของการ์ดทั้งหมด
    /// อาการที่เห็นคือ "ลากแล้วการ์ดหาย" กับ "คลิกการ์ดไม่ติด" ซึ่งดูไม่ออกว่ามาจากช่องนี้
    /// </summary>
    private CanvasGroup ValidateFeaturedGroup(CanvasGroup group, RectTransform container)
    {
        if (group == null || container == null) return group;
        if (!container.IsChildOf(group.transform)) return group;

        Debug.LogWarning("[" + GetType().Name + "] featuredGroup ชี้มาที่ '" + group.name +
                         "' ซึ่งเป็น container ของการ์ดเอง (หรือพ่อของมัน) — ทั้งแถบจะจางหายตอนลาก " +
                         "และการ์ดจะกดไม่ติด · ปิด layer นี้ไปก่อน ปล่อยช่องว่างไว้ " +
                         "หรือชี้ไปที่ object ภาพใหญ่ที่อยู่นอกแถบการ์ด", this);
        return null;
    }

    /// <summary>
    /// กัน featuredImage ชี้ไปที่ Image ใน "prefab asset" แทนที่จะเป็น object ในซีน
    ///
    /// ต่อผิดแบบนี้พังหนักกว่าที่คิด เพราะโค้ดเขียนค่าลงไปจริง —
    ///   OnFeaturedVisual: featuredImage.sprite = ...  → เขียนทับไฟล์ prefab บนดิสก์
    ///   Setup: featuredImage.raycastTarget = false    → เขียนทับไฟล์ prefab บนดิสก์
    /// ผลคือ prefab ถูกแก้ใน git โดยไม่มีใครตั้งใจ และตัวคุมจอจะคิดว่า
    /// "carousel เป็นเจ้าของภาพใหญ่แล้ว" แล้วปิดภาพในแผงรายละเอียดทิ้ง แผงเลยว่าง
    /// </summary>
    private Image ValidateFeaturedImage(Image img)
    {
        if (img == null) return null;
        if (img.gameObject.scene.IsValid()) return img;

        Debug.LogWarning("[" + GetType().Name + "] featuredImage ชี้ไปที่ Image ใน prefab asset " +
                         "('" + img.gameObject.name + "') ไม่ใช่ object ในซีน — การเขียน sprite/raycastTarget " +
                         "จะไปแก้ไฟล์ prefab บนดิสก์ และทำให้แผงรายละเอียดถูกปิดทิ้ง · ปิด layer นี้ไปก่อน " +
                         "ปล่อยช่องว่างไว้ หรือลาก Image ที่อยู่ในซีนมาใส่", this);
        return null;
    }

    private void SetupViews(GameObject cardTemplate, RectTransform container)
    {
        slotParent = container;

        if (items.Count == 0)
        {
            Debug.LogWarning("[" + GetType().Name + "] ไม่มีข้อมูล — ไม่มีอะไรให้แสดง", this);
            return;
        }
        if (cardTemplate == null || slotParent == null)
        {
            Debug.LogError("[" + GetType().Name + "] cardTemplate หรือ container เป็น null — สร้าง view ไม่ได้", this);
            return;
        }
        if (pitch <= 1f)
        {
            Debug.LogError("[" + GetType().Name + "] pitch = " + pitch + " — การ์ดจะซ้อนทับกันหมด · " +
                           "ตั้งให้เท่าขนาดการ์ดตามแกน + ช่องไฟ", this);
            pitch = 1f;
        }

        // LayoutGroup ที่ตั้งไว้ใน scene จะแย่งจัดตำแหน่ง — ต้องปิดก่อน
        var layout = slotParent.GetComponent<LayoutGroup>();
        if (layout != null && layout.enabled)
        {
            layout.enabled = false;
            Debug.Log("[" + GetType().Name + "] ปิด " + layout.GetType().Name +
                      " บน container — carousel วางตำแหน่งเอง", this);
        }
        var fitter = slotParent.GetComponent<ContentSizeFitter>();
        if (fitter != null && fitter.enabled) fitter.enabled = false;

        // ไม่แปะ RectMask2D ให้เอง — การ AddComponent ตอนรันทำให้ภาพที่ได้ขึ้นกับว่า
        // script ไปอยู่บน object ไหน ซึ่งมองไม่ออกจากใน Editor และซ้อนทับ mask ที่ซีนมีอยู่แล้ว
        // การตัดขอบเป็นหน้าที่ของซีน (builder ของจอ P3R ใส่ RectMask2D ให้บนแผงแล้ว)
        if (clipToPanel
            && slotParent.GetComponentInParent<RectMask2D>(true) == null
            && slotParent.GetComponentInParent<Mask>(true) == null)
        {
            Debug.LogWarning("[" + GetType().Name + "] clipToPanel เปิดอยู่ แต่ไม่มี Mask หรือ RectMask2D " +
                             "เหนือ '" + slotParent.name + "' ขึ้นไปเลย — การ์ดจะล้นพ้นขอบแผงไปทับของอื่น · " +
                             "ใส่ RectMask2D ในซีน หรือปิด clipToPanel", this);
        }

        // ซ่อน template ได้เฉพาะตอนที่มันเป็น object ในซีน — ถ้าเป็น prefab asset
        // SetActive จะไปแก้ไฟล์ asset บนดิสก์ กลายเป็นไฟล์ถูกแก้ใน git โดยไม่มีใครตั้งใจ
        var templateRect = cardTemplate.transform as RectTransform;
        templateSize = templateRect != null ? templateRect.sizeDelta : new Vector2(100f, 100f);
        if (cardTemplate.scene.IsValid()) cardTemplate.SetActive(false);

        BuildViews(cardTemplate);

        if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(+1));

        if (unmaskSelectedCard && views.Count > 0 && views[0].cardMask == null)
        {
            Debug.LogWarning("[" + GetType().Name + "] unmaskSelectedCard เปิดอยู่ แต่หา RectMask2D " +
                             "ในกิ่งของการ์ดไม่เจอ — ภาพจะไม่ล้นกรอบ · ใส่ RectMask2D บน root ของการ์ด " +
                             "หรือบนลูกที่ครอบภาพ", this);
        }

        // จัดวางให้จบในตัว ไม่ฝากไว้กับ JumpTo ของผู้เรียกซึ่งถูกข้ามได้
        Apply();

        Debug.Log("[" + GetType().Name + "] พร้อมแล้ว — " + items.Count + " ชิ้น · view " + views.Count +
                  " ใบ · " + axis + " / " + align + " · pitch " + pitch +
                  " · ที่แผงใส่ได้ " + VisibleSlots + " ช่อง", this);
    }

    private void BuildViews(GameObject cardTemplate)
    {
        foreach (var v in views)
            if (v.go != null) Destroy(v.go);
        views.Clear();

        // ไม่สร้างเกินจำนวนของที่มี — ของ 3 ชิ้นกับ view 8 ใบ แปลว่าการ์ดซ้ำ 5 ใบ
        // ซึ่งคือบั๊กที่เห็นเป็น "GUNNER โผล่สองใบ" กับ "ARENA 01 สี่ใบ"
        int n = Mathf.Max(1, Mathf.Min(viewCount, items.Count));

        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(cardTemplate, slotParent);
            go.name = "Slot_" + i;
            go.SetActive(true);

            var rect = (RectTransform)go.transform;
            ApplyAnchors(rect);

            // แปะ CanvasGroup ให้เฉพาะตอนที่จะขับ alpha จริง — ไม่งั้นเป็นการยัด component
            // ลงการ์ดตอนรันโดยที่ไม่มีใครสั่ง (เหตุผลเดียวกับที่ไม่แปะ RectMask2D ให้แผง)
            var group = go.GetComponent<CanvasGroup>();
            if (group == null && fadeByDistance) group = go.AddComponent<CanvasGroup>();

            // ค้นทั้งกิ่ง ไม่ใช่แค่ root — วาง mask ไว้บน wrapper ที่ครอบเฉพาะภาพเป็นโครงที่ถูกกว่า
            // (พื้นหลังการ์ดกับพื้นที่รับคลิกของ Button จะได้ไม่โดนตัดไปด้วย)
            var cardMask = go.GetComponentInChildren<RectMask2D>(true);

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            // ปิด navigation กันคีย์ Submit ยิงการ์ดที่โฟกัสค้างอยู่
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

            // สีพื้นการ์ดมีเจ้าของแล้วคือ SetCardSelected() — ปล่อยให้ Selectable เขียนด้วย
            // จะได้สองคนเขียนสีเดียวกัน แล้วใบที่เลือกกะพริบกลับเป็นสีปกติตอนเมาส์ออก
            btn.transition = Selectable.Transition.None;

            EnsureClickable(go, btn);

            var card = go.GetComponent<TCard>();
            if (card == null) card = go.AddComponent<TCard>();

            var view = new SlotView { go = go, rect = rect, group = group,
                                      button = btn, cardMask = cardMask, card = card };
            btn.onClick.AddListener(() => OnSlotClicked(view));
            views.Add(view);
        }
    }

    /// <summary>
    /// การ์ดที่ไม่มี Graphic ตัวไหน raycastTarget เลย = **กดไม่ติด และไม่มีอะไรฟ้อง**
    ///
    /// มี Button ครบ · interactable ติ๊กอยู่ · listener ต่อแล้ว · การ์ดโชว์ถูกทุกอย่าง
    /// แต่ EventSystem หาไม่เจอว่าเมาส์ชี้โดนอะไร มันจึงไม่เคยส่ง event มาถึง Button เลย
    /// ใน Inspector เห็นเป็น `Target Graphic: None` กับ `On Click () List is Empty`
    /// ซึ่งทั้งสองอย่าง **ปกติ** สำหรับ Button ที่ต่อ listener ตอนรัน จึงไม่ได้ชี้อะไรเลย
    ///
    /// ต้นเหตุจริงอยู่ที่ <c>P3RBuilderKit.NewImage</c> ซึ่งตั้ง `raycastTarget = false`
    /// ให้ทุก Image เพื่อลดภาระ raycast — ถูกต้องสำหรับของประดับ แต่การ์ดต้องกดได้
    /// builder ที่เขียนทีหลังลืมเปิดกลับได้ง่ายมาก และอาการที่ได้ดูไม่ออกว่ามาจากตรงนี้
    ///
    /// เปิดให้แล้วเตือน — การ์ดที่กดไม่ได้ไม่เคยเป็นสิ่งที่ตั้งใจ
    /// </summary>
    private static void EnsureClickable(GameObject go, Button btn)
    {
        var graphics = go.GetComponentsInChildren<Graphic>(true);

        if (btn.targetGraphic == null)
            btn.targetGraphic = go.GetComponent<Graphic>()
                             ?? (graphics.Length > 0 ? graphics[0] : null);

        if (graphics.Any(g => g.raycastTarget)) return;

        if (btn.targetGraphic == null)
        {
            Debug.LogError($"[Carousel] การ์ด '{go.name}' ไม่มี Graphic เลยสักตัว — กดไม่ได้แน่นอน · " +
                           "ใส่ Image พื้นหลังให้แม่แบบการ์ด", go);
            return;
        }

        btn.targetGraphic.raycastTarget = true;
        Debug.LogWarning($"[Carousel] การ์ด '{go.name}' ไม่มี Graphic ตัวไหนรับ raycast เลย — " +
                         $"เปิดให้ '{btn.targetGraphic.name}' แล้วตอนรัน · " +
                         "แก้ที่ต้นทางด้วยการตั้ง raycastTarget = true ให้พื้นหลังการ์ดในแม่แบบ", go);
    }

    /// <summary>
    /// anchor ของการ์ดตัดสินว่า anchoredPosition ที่คำนวณไว้นับจากตรงไหน
    /// จึงต้องไปทางเดียวกับ align เสมอ — ชิดขอบต้นแล้วยังยึดกลางอยู่คือของลอยกลางแผง
    /// ซึ่งเป็นอาการ "ARENA 01 ลอยกลางจอ" ก่อนหน้านี้
    /// </summary>
    private void ApplyAnchors(RectTransform rect)
    {
        if (align == CarouselAlign.Center)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = templateSize;
            return;
        }

        if (axis == CarouselAxis.Vertical)
        {
            // กางเต็มความกว้างแผง สูงเท่าแม่แบบ — ตรงกับที่ builder วางการ์ดตัวอย่างไว้
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, templateSize.y);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot     = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(templateSize.x, 0f);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Public control
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ชิ้นที่ index แบบ bounds-safe — null เมื่อไม่มีของเลย</summary>
    public TData GetItem(int index)
    {
        if (items.Count == 0) return null;
        return items[Wrap(index)];
    }

    /// <summary>
    /// index ใน list ของ carousel — **ไม่ใช่** index ใน list ที่ส่งเข้ามา
    /// เพราะ Setup ข้าม entry ที่เป็น null ทิ้ง ช่องว่างช่องเดียวก็ทำให้เลขเคลื่อนแล้ว
    /// </summary>
    public int IndexOf(TData data)
    {
        if (data == null) return -1;
        for (int i = 0; i < items.Count; i++)
            if (items[i] == data) return i;
        return -1;
    }

    /// <summary>วางชิ้นที่ index ให้เห็นทันที ไม่มีอนิเมชัน ไม่ยิง OnSettled — ใช้ตอนเปิดหน้ามาครั้งแรก</summary>
    public void JumpTo(int index)
    {
        if (items.Count == 0) return;

        if (align == CarouselAlign.Center)
        {
            _scroll = _scrollTarget = Wrap(index);
        }
        else
        {
            _selected     = Mathf.Clamp(index, 0, items.Count - 1);
            _scroll       = _scrollTarget = ClampScroll(ScrollToShow(_selected, 0f));
        }

        _vel      = 0f;
        _dragging = false;
        _settled  = true;
        _lastSettledIndex = SelectedIndex;
        Apply();
        OnFeaturedVisual(true);
    }

    /// <summary>ขยับการเลือกไป steps ชิ้น (บวก = ไปทางท้ายลิสต์)</summary>
    public void Step(int steps)
    {
        if (items.Count == 0 || steps == 0) return;

        if (align == CarouselAlign.Center)
        {
            _scrollTarget = Mathf.Round(_scrollTarget) + steps;
            _settled = false;
            return;
        }

        Select(_selected + steps);
    }

    /// <summary>
    /// เลือกชิ้นที่ index แล้วพาสายตาไปหาถ้ามันอยู่นอกกรอบ — โหมด Start เท่านั้น
    /// ยิง OnSettled ทันที ไม่รอไถลจบ เพราะการเลือกกับการเลื่อนเป็นคนละเรื่องกันในโหมดนี้
    /// </summary>
    public void Select(int index)
    {
        if (items.Count == 0) return;

        if (align == CarouselAlign.Center)
        {
            _scrollTarget = index;
            _settled = false;
            return;
        }

        _selected     = Mathf.Clamp(index, 0, items.Count - 1);
        _scrollTarget = ClampScroll(ScrollToShow(_selected, _scrollTarget));
        _settled      = !Mathf.Approximately(_scrollTarget, _scroll) ? false : _settled;
        Apply();
        Commit(_selected);
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
        if (items.Count == 0 || !CanScroll) return;
        _dragging = true;
        _settled  = false;
        _vel      = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging) return;
        // เนื้อหาวิ่งตามนิ้วเสมอ — แนวตั้งลากขึ้นคือไปหา index มากขึ้น
        // แนวนอนลากไปทางขวาคือถอยกลับไป index น้อยลง จึงกลับเครื่องหมาย
        _scroll += axis == CarouselAxis.Vertical ? e.delta.y / pitch
                                                 : -e.delta.x / pitch;
        if (align == CarouselAlign.Start) _scroll = ClampScroll(_scroll);
        Apply();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;
        _dragging     = false;
        _scrollTarget = align == CarouselAlign.Center
            ? Mathf.Round(_scroll)                      // ปล่อยแล้วไถลเข้าช่องที่ใกล้สุด
            : ClampScroll(Mathf.Round(_scroll));
    }

    public void OnScroll(PointerEventData e)
    {
        if (items.Count == 0 || !CanScroll) return;
        if (Mathf.Abs(e.scrollDelta.y) < 0.01f) return;

        int step = e.scrollDelta.y > 0f ? -wheelStep : wheelStep;

        if (align == CarouselAlign.Center)
        {
            // ลูกกลิ้งขึ้น (บวก) = ย้อนไป index น้อยลง ให้ทิศเดียวกับลิสต์ทั่วไป
            Step(step);
            return;
        }

        // โหมดลิสต์ ลูกกลิ้งเลื่อนสายตา ไม่ย้ายการเลือก — เหมือนลิสต์ทุกตัวในระบบปฏิบัติการ
        _scrollTarget = ClampScroll(Mathf.Round(_scrollTarget) + step);
        _settled      = false;
    }

    private void OnSlotClicked(SlotView view)
    {
        if (items.Count == 0) return;

        if (align == CarouselAlign.Center)
        {
            if (view.position == Mathf.RoundToInt(_scroll)) return;   // ใบกลางอยู่แล้ว
            _scrollTarget = view.position;
            _settled      = false;
            return;
        }

        Select(view.position);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Tick
    // ══════════════════════════════════════════════════════════════════════
    protected virtual void Update()
    {
        if (items.Count == 0) return;

        // เงื่อนไขคือ "ยังไม่นิ่ง" ไม่ใช่ "ยังห่างจากเป้า" — ปล่อยนิ้วตอนที่ scroll
        // บังเอิญตรงช่องพอดีจะไม่เหลือระยะให้ไถล ถ้าเช็กระยะ Settle จะไม่ถูกเรียกเลย
        if (!_dragging && !_settled)
        {
            if (Mathf.Abs(_scrollTarget - _scroll) < settleEpsilon)
            {
                _scroll = _scrollTarget;
                _vel    = 0f;
                Settle();
            }
            else
            {
                _scroll = Mathf.SmoothDamp(_scroll, _scrollTarget, ref _vel, snapSmoothTime);
            }
            Apply();
        }
        else if (!Mathf.Approximately(AxisSize, _lastAxisSize))
        {
            // ขนาดแผงเพิ่งถูกคำนวณเสร็จ (หรือจอเปลี่ยนขนาด) — จำนวนช่องที่ใส่ได้เปลี่ยนตาม
            // ถ้าไม่วางใหม่ตรงนี้ ภาพจะค้างอยู่กับขนาดตอนเฟรมแรกซึ่งมักเป็น 0
            Apply();
        }

        OnFeaturedVisual(false);
    }

    /// <summary>ไถลเข้าที่แล้ว</summary>
    private void Settle()
    {
        if (_settled) return;
        _settled = true;

        if (align == CarouselAlign.Center)
        {
            // กัน scroll ไหลไกลเรื่อยๆ จนเสียความละเอียด float ในเซสชันยาว
            // ตำแหน่งการ์ดคำนวณแบบสัมพัทธ์ ย่อค่าลงมาแล้วภาพไม่ขยับ
            int idx = Wrap(Mathf.RoundToInt(_scroll));
            _scroll = _scrollTarget = idx;
            Commit(idx);
            return;
        }

        // โหมดลิสต์ การไถลจบไม่ได้แปลว่าการเลือกเปลี่ยน — Select ยิงไปแล้วตอนคลิก
    }

    /// <summary>จุดเดียวที่ OnSettled ออกไปข้างนอก — กันยิงซ้ำด้วย index ที่ไม่เปลี่ยน</summary>
    private void Commit(int index)
    {
        if (index == _lastSettledIndex) return;
        _lastSettledIndex = index;
        OnSettled?.Invoke(index);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Featured layer
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// ภาพใหญ่จางหายระหว่างที่ยังไม่นิ่ง แล้วโผล่กลับพร้อมภาพของชิ้นใหม่ตอนเข้าที่แล้ว
    /// ตั้งใจไม่ให้มันวิ่งตามการ์ด เพราะมันอยู่นอก mask การวิ่งจะเห็นภาพใหญ่กวาดข้ามจอ
    /// </summary>
    protected virtual void OnFeaturedVisual(bool instant)
    {
        if (featuredGroup == null) return;

        bool  show = IsIdle;
        float goal = show ? featuredIdleAlpha : featuredMovingAlpha;

        if (instant) featuredGroup.alpha = goal;
        else featuredGroup.alpha = Mathf.MoveTowards(featuredGroup.alpha, goal,
                                                     featuredFadeSpeed * Time.unscaledDeltaTime);

        if (show && featuredImage != null && items.Count > 0)
        {
            var spr = FeaturedSpriteOf(items[SelectedIndex]);
            if (featuredImage.sprite != spr)
            {
                featuredImage.sprite  = spr;
                featuredImage.enabled = spr != null;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Layout
    // ══════════════════════════════════════════════════════════════════════
    private float AxisSize
    {
        get
        {
            if (slotParent == null) return 0f;
            var r = slotParent.rect;
            return axis == CarouselAxis.Vertical ? r.height : r.width;
        }
    }

    /// <summary>กี่ช่องที่แผงใส่ได้พร้อมกัน — อย่างน้อย 1 เสมอ แม้แผงจะยังไม่ถูกคำนวณขนาด</summary>
    public int VisibleSlots => Mathf.Max(1, Mathf.FloorToInt((AxisSize + 0.5f) / Mathf.Max(1f, pitch)));

    /// <summary>เลื่อนได้ไหม — ของน้อยกว่าที่แผงใส่ได้ก็ไม่มีอะไรให้เลื่อน</summary>
    private bool CanScroll => align == CarouselAlign.Center || items.Count > VisibleSlots;

    private float ClampScroll(float s) => Mathf.Clamp(s, 0f, Mathf.Max(0f, items.Count - VisibleSlots));

    /// <summary>ค่า scroll ที่น้อยที่สุดที่ทำให้ index อยู่ในกรอบ — อยู่ในกรอบแล้วไม่ขยับ</summary>
    private float ScrollToShow(int index, float from)
    {
        if (index < from) return index;
        if (index > from + VisibleSlots - 1) return index - VisibleSlots + 1;
        return from;
    }

    protected void Apply()
    {
        if (views.Count == 0 || items.Count == 0) return;

        _lastAxisSize = AxisSize;

        int basePos = align == CarouselAlign.Center
            // หน้าต่าง view คร่อมตัวกลาง — (n-1)/2 ทำให้ n คี่ได้ตัวกลางอยู่กลางจริง
            // ของเดิมใช้ n/2 - 1 ซึ่ง n = 3 แล้วตัวที่เลือกไปโผล่บนสุด
            ? Mathf.FloorToInt(_scroll) - (views.Count - 1) / 2
            : Mathf.Clamp(Mathf.FloorToInt(_scroll), 0,
                          Mathf.Max(0, items.Count - views.Count));

        int selected = SelectedIndex;

        for (int i = 0; i < views.Count; i++)
        {
            var v = views[i];
            v.position = basePos + i;

            // โหมดลิสต์ไม่วน — ช่องที่เลยปลายลิสต์ต้องหายไป ไม่ใช่วนกลับมาโชว์ของซ้ำ
            bool live = align == CarouselAlign.Center
                     || (v.position >= 0 && v.position < items.Count);
            if (v.go.activeSelf != live) v.go.SetActive(live);
            if (!live) continue;

            float delta = v.position - _scroll;                   // ระยะจากจุดอ้างอิง หน่วยช่อง
            float d     = delta * pitch;
            v.rect.anchoredPosition = axis == CarouselAxis.Vertical
                ? new Vector2(0f, -d)
                : new Vector2(d, 0f);

            int itemIdx = Wrap(v.position);

            // ไล่ขนาด/ความจางตามระยะห่างจาก **ตัวที่เลือก** ไม่ใช่จากกลางแผง
            // โหมดลิสต์ตัวที่เลือกไม่ได้อยู่กลาง ถ้าวัดจากกลางจะได้ใบที่เด่นผิดใบ
            float away = align == CarouselAlign.Center
                ? Mathf.Abs(delta)
                : Mathf.Abs(v.position - selected);
            float t = Mathf.Clamp01(away / 2f);
            float s = Mathf.Lerp(centerScale, edgeScale, t);
            v.rect.localScale = new Vector3(s, s, 1f);
            if (fadeByDistance && v.group != null)
                v.group.alpha = Mathf.Lerp(1f, edgeAlpha, t);

            // ผูกข้อมูลเฉพาะตอนชิ้นที่ view นี้แสดงเปลี่ยนจริง — การผูกมักไล่ลิสต์/แตะ TMP
            // ถ้าทำทุกเฟรมระหว่างลากคือเสียเปล่าทั้งหมด ส่วนตำแหน่ง/ขนาดด้านบนเปลี่ยนจริงทุกเฟรม
            if (v.boundIndex != itemIdx)
            {
                v.boundIndex = itemIdx;
                if (v.card != null) Bind(v.card, items[itemIdx], selectedColor, normalColor);
            }

            bool isSelected = align == CarouselAlign.Center
                ? Mathf.Abs(delta) < 0.5f
                : v.position == selected;
            if (v.card != null) SetCardSelected(v.card, isSelected);

            // ใบที่เลือกปิด mask ตัวเองเพื่อให้ภาพทะลุกรอบการ์ดออกมา ใบอื่นเปิดไว้ให้อยู่ในกรอบ
            // toggle เฉพาะตอนค่าเปลี่ยน — การสลับ enabled สั่งคำนวณพื้นที่ clip ใหม่ทั้งซับทรี
            if (v.cardMask != null)
            {
                bool wantMask = !(unmaskSelectedCard && isSelected);
                if (v.cardMask.enabled != wantMask) v.cardMask.enabled = wantMask;
            }
        }
    }

    protected int Wrap(int i)
    {
        int n = items.Count;
        if (n <= 0) return 0;
        return ((i % n) + n) % n;
    }
}
