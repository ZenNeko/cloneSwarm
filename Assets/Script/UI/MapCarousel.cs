using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// วงหมุนเลือกแมพ — โครงเดียวกับ CharacterCarousel ทุกอย่าง ต่างแค่ข้อมูลเป็น MapData
/// กลไกวาง/ลาก/ไถลเข้าช่องอยู่ใน CarouselBase
///
/// ต้องวางบน object ของแผงที่จะลาก (เช่น MapList ใต้ Panel_Map) ไม่ใช่บน Canvas
/// </summary>
public class MapCarousel : CarouselBase
{
    // ── Preview layer ─────────────────────────────────────────────────────
    [Header("Preview Layer (ภาพใหญ่นอก mask)")]
    [Tooltip("CanvasGroup ของภาพพรีวิวใหญ่ — ปล่อยว่างได้ถ้าไม่ต้องการ")]
    public CanvasGroup previewGroup;
    [Tooltip("Image ที่จะใส่ previewImage ของแมพที่อยู่กลาง")]
    public Image previewImage;
    [Tooltip("ความเร็วจาง/โผล่ของภาพใหญ่ (หน่วยอัลฟาต่อวินาที)")]
    public float previewFadeSpeed = 6f;

    [Range(0f, 1f)]
    [Tooltip("ความทึบของภาพใหญ่ตอนเข้าช่องนิ่งแล้ว")]
    public float previewIdleAlpha = 1f;

    [Range(0f, 1f)]
    [Tooltip("ความทึบระหว่างที่ยังลาก/ไถลอยู่ - ตั้ง 1 เท่ากับ previewIdleAlpha = ไม่จางเลย")]
    public float previewMovingAlpha = 0f;

    // ── Data ──────────────────────────────────────────────────────────────
    private readonly List<MapData>   maps  = new();
    private readonly List<MapCardUI> cards = new();

    private Color selectedColor, normalColor;

    public int Count => maps.Count;

    protected override int ItemCount => maps.Count;

    // ══════════════════════════════════════════════════════════════════════
    public void Setup(List<MapData> mapList, GameObject cardTemplate,
                      RectTransform container, Color selColor, Color normColor)
    {
        maps.Clear();
        cards.Clear();
        if (mapList != null)
            foreach (var m in mapList)
                if (m != null) maps.Add(m);

        selectedColor = selColor;
        normalColor   = normColor;

        // ตรวจก่อนว่าไม่ได้ชี้มาที่ container ของการ์ดเอง — ดู ValidateOverlayGroup
        previewGroup = ValidateOverlayGroup(previewGroup, container, "previewGroup");
        previewImage = ValidateOverlayImage(previewImage, "previewImage");

        // ภาพใหญ่ทับช่องกลาง ถ้ารับ raycast จะดูดคลิกกับการลากไปหมด — เป็นของประดับล้วน
        if (previewGroup != null)
        {
            previewGroup.blocksRaycasts = false;
            previewGroup.interactable   = false;
        }
        if (previewImage != null) previewImage.raycastTarget = false;

        SetupViews(cardTemplate, container);
    }

    // ── Hooks จาก CarouselBase ────────────────────────────────────────────
    protected override void OnViewBuilt(int slotIndex, GameObject go)
    {
        var card = go.GetComponent<MapCardUI>();
        if (card == null) card = go.AddComponent<MapCardUI>();
        cards.Add(card);
    }

    protected override void BindView(int slotIndex, int itemIndex)
    {
        if (slotIndex >= cards.Count) return;
        cards[slotIndex].SetData(maps[itemIndex], selectedColor, normalColor);
    }

    protected override void SetViewSelected(int slotIndex, bool selected)
    {
        if (slotIndex >= cards.Count) return;
        cards[slotIndex].SetSelected(selected);
    }

    // ══════════════════════════════════════════════════════════════════════
    /// <summary>index ใน list ของ carousel — ข้าม entry ที่เป็น null ไปแล้ว เลขจึงไม่ตรงกับ list ต้นทาง</summary>
    public int IndexOf(MapData map)
    {
        if (map == null) return -1;
        for (int i = 0; i < maps.Count; i++)
            if (maps[i] == map) return i;
        return -1;
    }

    public MapData GetMap(int index)
    {
        if (maps.Count == 0) return null;
        return maps[Wrap(index)];
    }

    // ══════════════════════════════════════════════════════════════════════
    /// <summary>ภาพใหญ่จางหายระหว่างเลื่อน โผล่กลับพร้อมภาพของแมพใหม่ตอนเข้าช่องแล้ว</summary>
    protected override void OnCenterSettledVisual(bool instant)
    {
        if (previewGroup == null) return;

        bool  show = IsIdle;
        float goal = show ? previewIdleAlpha : previewMovingAlpha;

        if (instant) previewGroup.alpha = goal;
        else previewGroup.alpha = Mathf.MoveTowards(previewGroup.alpha, goal,
                                                    previewFadeSpeed * Time.unscaledDeltaTime);

        if (show && previewImage != null && maps.Count > 0)
        {
            var m   = maps[CenterIndex];
            var spr = m != null ? m.previewImage : null;
            if (previewImage.sprite != spr)
            {
                previewImage.sprite  = spr;
                previewImage.enabled = spr != null;
            }
        }
    }
}
