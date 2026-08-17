using UnityEngine;

/// <summary>
/// คณิตล้วนของ hit test แต่ละทรง telegraph — แยกออกจาก TelegraphZone (MonoBehaviour) เพื่อให้
/// เทสได้โดยไม่ต้องมี scene/GameObject/transform (ดู Assets/Editor/Tests/TelegraphGeometryTests.cs)
///
/// ทุกเมธอดรับพารามิเตอร์ตรงๆ (position, center, rotation/forward, ขนาด, scale) ไม่มี field
/// ไม่มี transform — ฝั่ง TelegraphZone.IsInXxx() เป็นคนส่ง transform.position / transform.rotation /
/// HitScale เข้ามา แล้วยังคงพฤติกรรมเดิมทุกจุด **ห้ามเปลี่ยน**: coneAngle >= 360 → true เสมอ,
/// ยืนทับจุดยอดกรวยพอดี → true, ดาเมจใช้ scaleEnd (ผ่านพารามิเตอร์ scale) ไม่ใช่ CurrentScale
/// (เหตุผลอยู่ที่คอมเมนต์ของ TelegraphZone.HitScale — elapsed เป็นตัวนับฝั่ง client)
///
/// **debt #8 (ADR-007 §2)** — ภาพ (shader graph) กับ hit test (ที่นี่) เป็นคนละ implementation
/// ของทรงเดียวกัน ไม่มีอะไรจับความเพี้ยนได้อัตโนมัติ คอมเมนต์ในแต่ละเมธอดด้านล่างชี้ไปยัง
/// property/ตำแหน่งในกราฟที่คำนวณเรื่องเดียวกัน — ราคาศูนย์ ไม่กันเพี้ยนแต่ทำให้หาเจอ
/// ตารางเต็มอยู่ที่ docs/telegraph-shader-crossref.md (paste ลง sticky note บนกราฟได้เลย)
/// </summary>
public static class TelegraphGeometry
{
    /// <summary>
    /// วงกลม — ระยะจากศูนย์กลางบนระนาบ XZ ไม่เกิน radius * scale
    ///
    /// ไม่มีคู่ในกราฟ — ขอบเขตที่แท้จริงมาจาก mesh (circlePrefab สเกล radius*2) ไม่ใช่สูตรใน shader
    /// กราฟมีแค่ _OutlineWidth/_EdgeGlow/_EdgeStrength วาดเส้นขอบตกแต่งรอบ mesh เท่านั้น
    /// ไม่ได้ตัดสินว่าอยู่ในหรือนอกวง — จึงไม่เข้าข่ายหนี้ข้อ 8
    /// </summary>
    public static bool IsInCircle(Vector3 pos, Vector3 center, float radius, float scale)
    {
        Vector2 d = new Vector2(pos.x - center.x, pos.z - center.z);
        return d.magnitude <= radius * scale;
    }

    /// <summary>
    /// กรวย — อยู่ในรัศมี **และ** อยู่ในมุมกางจากทิศ forward
    /// coneAngle >= 360 = วงกลมเต็ม (คืน true ทันทีถ้าอยู่ในรัศมี ไม่เช็คมุม)
    /// ยืนทับจุดยอด (d ~ 0) ถือว่าโดนเสมอ — มุมของเวกเตอร์ศูนย์ไม่มีความหมาย
    ///
    /// ไม่มีคู่ในกราฟ — รูปทรงมาจาก mesh ที่ C# ปั้นเอง (TelegraphZone.CreateConePrimitive)
    /// ใช้ coneAngle ตัวเดียวกัน แต่คำนวณอยู่ใน C# ทั้งสองจุด (mesh + hit test นี้) shader ไม่มีสูตรกรวย
    /// </summary>
    public static bool IsInCone(Vector3 pos, Vector3 center, Vector3 forward, float radius, float coneAngle, float scale)
    {
        Vector3 d = pos - center;
        d.y = 0f;

        float r = radius * scale;
        if (d.sqrMagnitude > r * r) return false;
        if (coneAngle >= 360f) return true;
        if (d.sqrMagnitude < 0.0001f) return true;   // ยืนทับจุดยอดกรวย

        return Vector3.Angle(forward, d) <= coneAngle * 0.5f;
    }

    /// <summary>
    /// เส้นตรง (กล่องสี่เหลี่ยม) แนวแกน rotation — เทียบ local X กับ lineWidth ครึ่งหนึ่ง,
    /// local Z กับ lineLength ครึ่งหนึ่ง (ใช้ทั้ง Line ปกติ และ Cross ที่หมุน rotation เพิ่ม 90°
    /// ก่อนเรียก — ดู TelegraphZone.IsInLineCross)
    ///
    /// ไม่มีคู่ในกราฟ — ขอบเขตมาจาก mesh (linePrefab/Cube สเกล lineWidth×lineLength)
    /// กราฟมีแค่ _OutlineShape = 0 บอกให้วาดขอบเหลี่ยมแทนขอบโค้ง ไม่ใช่สูตรระยะซ้ำ
    /// </summary>
    public static bool IsInLine(Vector3 pos, Vector3 center, Quaternion rotation, float lineWidth, float lineLength, float scale)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (pos - center);
        return Mathf.Abs(local.x) <= lineWidth * scale * 0.5f
            && Mathf.Abs(local.z) <= lineLength * scale * 0.5f;
    }

    /// <summary>
    /// โดนัท (annulus) — ระยะจากศูนย์กลางอยู่ระหว่าง innerRadius*scale ถึง radius*scale
    ///
    /// **มีคู่ในกราฟจริง — นี่คือกรณีที่ debt #8 พูดถึง** TelegraphUniversal ใช้ _InnerRadius
    /// เทียบระยะจากศูนย์กลาง (object space) แล้วตัด alpha ออกเป็นรูตรงกลาง ค่าที่ส่งเข้ากราฟมาจาก
    /// Mathf.Clamp01(innerRadius / radius) ใน TelegraphZone.PushShaderColors() — annulus เดียวกัน
    /// คิดสองที่ (ที่นี่ vs ในกราฟ) ไม่มีอะไรจับได้ถ้าสูตรฝั่งใดฝั่งหนึ่งเพี้ยน
    /// ดูตาราง cross-reference เต็มที่ docs/telegraph-shader-crossref.md
    /// </summary>
    public static bool IsInDonut(Vector3 pos, Vector3 center, float innerRadius, float radius, float scale)
    {
        float dist = new Vector2(pos.x - center.x, pos.z - center.z).magnitude;
        return dist >= innerRadius * scale && dist <= radius * scale;
    }
}
