using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// วงหมุนเลือกตัวละครฝั่งซ้าย — ตัวที่เลือกอยู่กลางแผงเสมอ เลื่อนแล้ววนไม่รู้จบ
///
/// กลไกการวาง/ลาก/ไถลเข้าช่องอยู่ใน CarouselBase ทั้งหมด ตัวนี้บอกแค่
/// "มีตัวละครกี่ตัว" กับ "ผูกข้อมูลลงการ์ดยังไง" บวกชั้นตัวเด่นที่เป็นของเฉพาะหน้าเลือกตัวละคร
///
/// ต้องวางบน LeftPanel ไม่ใช่บน Canvas ที่ CharacterSelectUI อยู่ — ดูเหตุผลใน CarouselBase
/// </summary>
public class CharacterCarousel : CarouselBase
{
    // ── Hero layer ────────────────────────────────────────────────────────
    [Header("Hero Layer (นอก mask — ล้นออกนอกแถบได้)")]
    [Tooltip("CanvasGroup ของ object ตัวเด่น ที่วางใต้ Panel_Character ต่อจาก LeftPanel - ปล่อยว่างได้")]
    public CanvasGroup heroGroup;
    [Tooltip("Image ที่จะใส่ portrait ของตัวที่อยู่กลาง")]
    public Image heroImage;
    [Tooltip("ความเร็วจาง/โผล่ของตัวเด่น (หน่วยอัลฟาต่อวินาที)")]
    public float heroFadeSpeed = 6f;

    // ── Data ──────────────────────────────────────────────────────────────
    private readonly List<CharacterData>   roster = new();
    private readonly List<CharacterCardUI> cards  = new();

    private Color selectedColor, normalColor;

    public int Count => roster.Count;

    protected override int ItemCount => roster.Count;

    // ══════════════════════════════════════════════════════════════════════
    // Setup
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>เรียกจาก CharacterSelectUI ครั้งเดียวตอน Start</summary>
    public void Setup(List<CharacterData> characters, GameObject cardTemplate,
                      RectTransform container, Color selColor, Color normColor)
    {
        roster.Clear();
        cards.Clear();
        if (characters != null)
            foreach (var cd in characters)
                if (cd != null) roster.Add(cd);

        selectedColor = selColor;
        normalColor   = normColor;

        // ตัวเด่นวางทับช่องกลางและใหญ่กว่าการ์ด ถ้ามันรับ raycast จะดูดคลิกกับการลากไปหมด
        // การ์ดใต้มันจะกดไม่ได้เลย — มันเป็นของประดับล้วน จึงปิดการรับ input ทิ้งตั้งแต่ต้น
        if (heroGroup != null)
        {
            heroGroup.blocksRaycasts = false;
            heroGroup.interactable   = false;
        }
        if (heroImage != null) heroImage.raycastTarget = false;

        SetupViews(cardTemplate, container);
    }

    // ── Hooks จาก CarouselBase ────────────────────────────────────────────
    protected override void OnViewBuilt(int slotIndex, GameObject go)
    {
        var card = go.GetComponent<CharacterCardUI>();
        if (card == null) card = go.AddComponent<CharacterCardUI>();
        cards.Add(card);
    }

    protected override void BindView(int slotIndex, int itemIndex)
    {
        if (slotIndex >= cards.Count) return;
        cards[slotIndex].SetData(roster[itemIndex], selectedColor, normalColor);
    }

    protected override void SetViewSelected(int slotIndex, bool selected)
    {
        if (slotIndex >= cards.Count) return;
        cards[slotIndex].SetSelected(selected);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Public API (CharacterSelectUI เรียก)
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>อัปเดตหน้าตาทุก view รวมถึง overlay ล็อก — เรียกหลังปลดล็อกสำเร็จ</summary>
    public void RefreshLocks() => RefreshAll();

    /// <summary>
    /// index ใน roster ของ carousel — ไม่ใช่ index ใน list ที่ส่งเข้ามา
    /// เพราะ Setup ข้าม entry ที่เป็น null ทิ้ง ช่องว่างช่องเดียวก็ทำให้เลขเคลื่อนแล้ว
    /// </summary>
    public int IndexOf(CharacterData cd)
    {
        if (cd == null) return -1;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] == cd) return i;
        return -1;
    }

    /// <summary>อ่านตัวละครตาม index แบบ bounds-safe</summary>
    public CharacterData GetCharacter(int index)
    {
        if (roster.Count == 0) return null;
        return roster[Wrap(index)];
    }

    // ══════════════════════════════════════════════════════════════════════
    // Hero layer
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// ตัวเด่นจางหายระหว่างที่ยังไม่นิ่ง แล้วโผล่กลับพร้อม portrait ของตัวใหม่ตอนเข้าช่องแล้ว
    /// ตั้งใจไม่ให้มันวิ่งตามการ์ด เพราะมันอยู่นอก mask การวิ่งจะเห็นภาพใหญ่กวาดข้ามจอ
    /// </summary>
    protected override void OnCenterSettledVisual(bool instant)
    {
        if (heroGroup == null) return;

        bool  show = IsIdle;
        float goal = show ? 1f : 0f;

        if (instant) heroGroup.alpha = goal;
        else heroGroup.alpha = Mathf.MoveTowards(heroGroup.alpha, goal,
                                                 heroFadeSpeed * Time.unscaledDeltaTime);

        if (show && heroImage != null && roster.Count > 0)
        {
            var cd  = roster[CenterIndex];
            var spr = cd != null ? cd.portrait : null;
            if (heroImage.sprite != spr)
            {
                heroImage.sprite  = spr;
                heroImage.enabled = spr != null;
            }
        }
    }
}
