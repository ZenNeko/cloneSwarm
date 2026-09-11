using UnityEngine;

/// <summary>
/// แถบเลือกแมพ — กลไกทั้งหมดอยู่ใน <see cref="CarouselBase{TData,TCard}"/> เหมือน
/// <see cref="CharacterCarousel"/> ทุกประการ ต่างกันแค่ชนิดข้อมูลกับแกนที่วาง
/// (แมพวางขวาง ตัวละครวางลง — ตั้งที่ช่อง axis ใน Inspector ไม่ใช่ในโค้ด)
///
/// ต้องวางบนแผงที่จะลาก (P3R_MapSelect/CardsRow) ไม่ใช่บน Canvas
/// </summary>
public class MapCarousel : CarouselBase<MapData, MapCardUI>
{
    protected override void Bind(MapCardUI card, MapData data, Color sel, Color norm)
        => card.SetData(data, sel, norm);

    protected override void SetCardSelected(MapCardUI card, bool selected)
        => card.SetSelected(selected);

    protected override Sprite FeaturedSpriteOf(MapData data)
        => data != null ? data.previewImage : null;

    // ── ชื่อที่อ่านออกจากฝั่ง MapSelectUI ─────────────────────────────────
    public MapData GetMap(int index) => GetItem(index);
}
