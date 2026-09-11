using UnityEngine;

/// <summary>
/// ลิสต์เลือกตัวละครฝั่งซ้าย — กลไกทั้งหมดอยู่ใน <see cref="CarouselBase{TData,TCard}"/>
/// ตัวนี้มีอยู่เพราะ Unity แปะ component ที่เป็น generic แบบเปิดลง GameObject ไม่ได้
/// ต้องมีคลาสที่ปิดชนิดแล้ว · เหลือไว้แค่การผูกข้อมูลกับชื่อที่อ่านออกจากฝั่งผู้เรียก
///
/// ต้องวางบนแผงที่จะลาก (P3R_Character/CardList) ไม่ใช่บน Canvas — ดูเหตุผลใน CarouselBase
/// </summary>
public class CharacterCarousel : CarouselBase<CharacterData, CharacterCardUI>
{
    protected override void Bind(CharacterCardUI card, CharacterData data, Color sel, Color norm)
        => card.SetData(data, sel, norm);

    protected override void SetCardSelected(CharacterCardUI card, bool selected)
        => card.SetSelected(selected);

    /// <summary>ภาพใหญ่ของตัวละครคือ portrait — icon เป็นของการ์ดใบเล็ก</summary>
    protected override Sprite FeaturedSpriteOf(CharacterData data)
        => data != null ? data.portrait : null;

    // ── ชื่อที่อ่านออกจากฝั่ง CharacterSelectUI ───────────────────────────
    /// <summary>อ่านตัวละครตาม index แบบ bounds-safe</summary>
    public CharacterData GetCharacter(int index) => GetItem(index);

    /// <summary>อัปเดตหน้าตาทุก view รวมถึง overlay ล็อก — เรียกหลังปลดล็อกสำเร็จ</summary>
    public void RefreshLocks() => RefreshAll();
}
