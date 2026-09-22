using System;
using UnityEngine;

public enum RollKind { Variant, Anchor, SnapAngle, Target, MirrorX, MirrorZ, Order }

/// <summary>
/// ช่องที่ต้องเป็น "ชื่อ roll ที่มีนิยามอยู่จริง" — ให้ drawer เปลี่ยนเป็นรายการให้เลือก
///
/// พิมพ์ผิดแล้วเงียบสนิท: RollContext หา definition ไม่เจอจะถือเป็น Variant optionCount 2
/// ซึ่งเป็นค่าที่ valid ระบบเลยไม่มีทางรู้ว่าผิด · ท่าที่ควรหมุนก็แค่หยุดหมุน
/// (โรคเดียวกับช่อง variant ของนัดหมาย — ดู VariantIdAttribute)
/// </summary>
public class RollIdAttribute : PropertyAttribute { }

[Serializable]
public class RollDefinition
{
    public string   rollName = "roll";   // คลิปอ้างด้วยชื่อนี้
    public RollKind kind = RollKind.Variant;
    public int      optionCount = 2;     // จำนวนตัวเลือก
    public bool     excludePrevious = false;
}
