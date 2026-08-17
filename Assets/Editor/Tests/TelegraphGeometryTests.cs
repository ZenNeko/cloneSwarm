using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ TelegraphGeometry — คณิตล้วน ไม่ต้องมี scene/GameObject/NetworkBehaviour
/// (ADR-007 §2 หนี้ข้อ 7 "ไม่มีเทสต์")
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp (predefined เช่นกัน) ได้เพราะเป็น assembly ที่ตายตัวสองอันมองกันได้
/// อยู่แล้วโดยไม่ต้องประกาศ reference — ถ้าสร้าง .asmdef ให้ไฟล์นี้จะพังทันทีเพราะ .asmdef
/// อ้างอิง predefined assembly (Assembly-CSharp) ไม่ได้
/// </summary>
public class TelegraphGeometryTests
{
    static readonly Vector3 Center = new Vector3(3f, 1.5f, -2f);   // จงใจไม่ใช่ origin กัน bug ที่ลืมลบ center ออก

    // ── Circle ────────────────────────────────────────────────────────────
    [Test]
    public void Circle_Inside_ReturnsTrue()
    {
        Vector3 pos = Center + new Vector3(3f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInCircle(pos, Center, 5f, 1f));
    }

    [Test]
    public void Circle_Outside_ReturnsFalse()
    {
        Vector3 pos = Center + new Vector3(6f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInCircle(pos, Center, 5f, 1f));
    }

    [Test]
    public void Circle_ExactlyOnBoundary_ReturnsTrue()
    {
        // radius=5 ตามแกน X ล้วน — magnitude ได้ 5.0 พอดีไม่มีปัดเศษ (ไม่ผ่าน sqrt ของเลขไม่ลงตัว)
        Vector3 pos = Center + new Vector3(5f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInCircle(pos, Center, 5f, 1f));
    }

    [Test]
    public void Circle_ScaleApplied_ExpandsRadius()
    {
        Vector3 pos = Center + new Vector3(8f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInCircle(pos, Center, 5f, 1f));     // นอกรัศมีเดิม
        Assert.IsTrue(TelegraphGeometry.IsInCircle(pos, Center, 5f, 2f));      // scale 2x ครอบถึง
    }

    [Test]
    public void Circle_YAxisIgnored()
    {
        // hit test คิดบนระนาบ XZ ล้วน — Y ต่างกันแค่ไหนก็ไม่มีผล (ตรงกับคอมเมนต์ visualYOffset ใน TelegraphZone)
        Vector3 pos = Center + new Vector3(2f, 999f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInCircle(pos, Center, 5f, 1f));
    }

    // ── Donut ─────────────────────────────────────────────────────────────
    [Test]
    public void Donut_InsideAnnulus_ReturnsTrue()
    {
        Vector3 pos = Center + new Vector3(3f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInDonut(pos, Center, 1.5f, 5f, 1f));
    }

    [Test]
    public void Donut_InHole_ReturnsFalse()
    {
        // อยู่ใน innerRadius = โซนปลอดภัยกลางโดนัท ต้องไม่โดน
        Vector3 pos = Center + new Vector3(1f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInDonut(pos, Center, 1.5f, 5f, 1f));
    }

    [Test]
    public void Donut_OutsideOuterRadius_ReturnsFalse()
    {
        Vector3 pos = Center + new Vector3(6f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInDonut(pos, Center, 1.5f, 5f, 1f));
    }

    [Test]
    public void Donut_ExactlyOnInnerBoundary_ReturnsTrue()
    {
        // >= ที่ innerRadius ต้องนับเป็นโดน (ไม่ใช่ปลอดภัย) ตามสูตร dist >= innerRadius*scale
        Vector3 pos = Center + new Vector3(2f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInDonut(pos, Center, 2f, 5f, 1f));
    }

    [Test]
    public void Donut_ExactlyOnOuterBoundary_ReturnsTrue()
    {
        Vector3 pos = Center + new Vector3(5f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInDonut(pos, Center, 2f, 5f, 1f));
    }

    [Test]
    public void Donut_ScaleApplied_ExpandsBothRadii()
    {
        // ที่ scale 1: dist=3 อยู่ในรู (innerRadius 2) แต่ scale 2x ทำให้ innerRadius กลายเป็น 4 → ยังอยู่ในรู
        Vector3 pos = Center + new Vector3(3f, 0f, 0f);
        Assert.IsTrue(TelegraphGeometry.IsInDonut(pos, Center, 2f, 5f, 1f));
        Assert.IsFalse(TelegraphGeometry.IsInDonut(pos, Center, 2f, 5f, 2f));
    }

    // ── Line ──────────────────────────────────────────────────────────────
    [Test]
    public void Line_WidthBoundary_IndependentOfLength()
    {
        // ยาวมากพอไม่ให้ length เป็นตัวจำกัดผลลัพธ์ — เทส width ล้วนๆ
        float width = 4f, length = 200f;
        Assert.IsTrue(TelegraphGeometry.IsInLine(Center + new Vector3(2f, 0f, 0f), Center, Quaternion.identity, width, length, 1f));   // ขอบพอดี (width/2)
        Assert.IsFalse(TelegraphGeometry.IsInLine(Center + new Vector3(2.5f, 0f, 0f), Center, Quaternion.identity, width, length, 1f)); // เกิน width
    }

    [Test]
    public void Line_LengthBoundary_IndependentOfWidth()
    {
        float width = 200f, length = 10f;
        Assert.IsTrue(TelegraphGeometry.IsInLine(Center + new Vector3(0f, 0f, 5f), Center, Quaternion.identity, width, length, 1f));   // ขอบพอดี (length/2)
        Assert.IsFalse(TelegraphGeometry.IsInLine(Center + new Vector3(0f, 0f, 5.5f), Center, Quaternion.identity, width, length, 1f)); // เกิน length
    }

    [Test]
    public void Line_ScaleApplied()
    {
        float width = 2f, length = 10f;
        Vector3 pos = Center + new Vector3(1.8f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInLine(pos, Center, Quaternion.identity, width, length, 1f));
        Assert.IsTrue(TelegraphGeometry.IsInLine(pos, Center, Quaternion.identity, width, length, 2f));
    }

    /// <summary>
    /// Cross ใช้ IsInLine ตัวเดียวกันแค่หมุน rotation เพิ่ม 90° (ดู TelegraphZone.IsInLineCross)
    /// กล่องสี่เหลี่ยมสมมาตรรอบศูนย์กลาง — หมุน 90° แล้วผลลัพธ์ต้องเทียบเท่ากับกล่องเดิมที่สลับ
    /// width กับ length โดยไม่หมุน (เอกลักษณ์ทางเรขาคณิต ไม่ขึ้นกับทิศทางการหมุนของ Unity)
    /// เทสนี้จึงตรวจสอบตัวเองได้โดยไม่ต้องมโนว่า local.x/local.z ออกมาเป็นเท่าไหร่หลังหมุน
    /// </summary>
    [Test]
    public void Line_Rotated90ForCross_MatchesWidthLengthSwap()
    {
        float width = 2f, length = 10f, scale = 1f;
        Quaternion rot90 = Quaternion.identity * Quaternion.Euler(0f, 90f, 0f);

        Vector3[] localOffsets =
        {
            new Vector3(0.9f, 0f, 4f),
            new Vector3(4f, 0f, 0.9f),
            new Vector3(6f, 0f, 6f),
            new Vector3(1.5f, 0f, -1.5f),
            new Vector3(-4.9f, 0f, 0.5f),
        };

        foreach (var offset in localOffsets)
        {
            Vector3 pos = Center + offset;
            bool rotated          = TelegraphGeometry.IsInLine(pos, Center, rot90, width, length, scale);
            bool unrotatedSwapped = TelegraphGeometry.IsInLine(pos, Center, Quaternion.identity, length, width, scale);
            Assert.AreEqual(unrotatedSwapped, rotated,
                $"offset {offset}: หมุน 90° ต้องให้ผลตรงกับกล่องที่สลับ width/length โดยไม่หมุน");
        }
    }

    // ── Cone ──────────────────────────────────────────────────────────────
    [Test]
    public void Cone_WithinAngleAndRadius_ReturnsTrue()
    {
        Vector3 pos = Center + new Vector3(0f, 0f, 3f);   // ตรงหน้า forward เป๊ะ
        Assert.IsTrue(TelegraphGeometry.IsInCone(pos, Center, Vector3.forward, 5f, 90f, 1f));
    }

    [Test]
    public void Cone_OutsideRadius_ReturnsFalse()
    {
        Vector3 pos = Center + new Vector3(0f, 0f, 10f);
        Assert.IsFalse(TelegraphGeometry.IsInCone(pos, Center, Vector3.forward, 5f, 90f, 1f));
    }

    [Test]
    public void Cone_OutsideAngle_ReturnsFalse()
    {
        // coneAngle 90 → half 45° — จุดที่ 90° จาก forward (ด้านข้างเป๊ะ) ต้องอยู่นอกกรวยชัดเจน
        Vector3 pos = Center + new Vector3(3f, 0f, 0f);
        Assert.IsFalse(TelegraphGeometry.IsInCone(pos, Center, Vector3.forward, 5f, 90f, 1f));
    }

    [Test]
    public void Cone_AngleBoundary_JustInsideVsJustOutside()
    {
        float coneAngle = 90f;   // half = 45°
        float half = coneAngle * 0.5f;

        // สร้างทิศทางที่เอียงจาก forward น้อยกว่า/มากกว่า half องศาเล็กน้อย ด้วย Quaternion จริง
        // (ไม่มโนพิกัด XZ เอง เพื่อกัน error สะสมจาก sin/cos ที่พิมพ์เอง)
        Vector3 justInsideDir  = Quaternion.Euler(0f, half - 2f, 0f) * Vector3.forward;
        Vector3 justOutsideDir = Quaternion.Euler(0f, half + 2f, 0f) * Vector3.forward;

        Vector3 justInsidePos  = Center + justInsideDir  * 3f;
        Vector3 justOutsidePos = Center + justOutsideDir * 3f;

        Assert.IsTrue(TelegraphGeometry.IsInCone(justInsidePos, Center, Vector3.forward, 5f, coneAngle, 1f));
        Assert.IsFalse(TelegraphGeometry.IsInCone(justOutsidePos, Center, Vector3.forward, 5f, coneAngle, 1f));
    }

    [Test]
    public void Cone_AngleGTE360_ReturnsTrueRegardlessOfDirection()
    {
        // coneAngle >= 360 = วงกลมเต็ม — จุดด้านหลัง (ปกติต้องนอกกรวยทุกมุมที่ < 360) ต้องโดนด้วย
        Vector3 behindPos = Center + new Vector3(0f, 0f, -3f);
        Assert.IsTrue(TelegraphGeometry.IsInCone(behindPos, Center, Vector3.forward, 5f, 360f, 1f));
        Assert.IsTrue(TelegraphGeometry.IsInCone(behindPos, Center, Vector3.forward, 5f, 400f, 1f));
    }

    [Test]
    public void Cone_Apex_ReturnsTrueEvenWithNarrowAngle()
    {
        // ยืนทับจุดยอดพอดี (d ~ 0) — มุมของเวกเตอร์ศูนย์ไม่มีความหมาย ต้องนับว่าโดนเสมอ
        Assert.IsTrue(TelegraphGeometry.IsInCone(Center, Center, Vector3.forward, 5f, 1f, 1f));
    }

    [Test]
    public void Cone_ScaleApplied_ExpandsRadius()
    {
        Vector3 pos = Center + new Vector3(0f, 0f, 8f);
        Assert.IsFalse(TelegraphGeometry.IsInCone(pos, Center, Vector3.forward, 5f, 90f, 1f));
        Assert.IsTrue(TelegraphGeometry.IsInCone(pos, Center, Vector3.forward, 5f, 90f, 2f));
    }
}
