using System.Collections.Generic;
using CloneSwarm.EditorTools;
using NUnit.Framework;
using UnityEngine;
using Shape = CloneSwarm.EditorTools.BossPatternGeometry.Shape;

/// <summary>
/// EditMode test ของ BossPatternGeometry — เรขาคณิตที่แผนผังสนาม · แถบความปลอดภัย · การลากแก้ใช้ร่วมกัน
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (แบบเดียวกับ ShieldStackTests)
///
/// ทิศ: yaw 0 = +Z (เหนือ) · Line เริ่มที่จุดเกิดแล้วพาดไปข้างหน้า · Cross อยู่กลางจุดเกิด
/// ต้องตรงกับที่ ArenaPreview วาด ไม่งั้น % ปลอดภัยจะไม่ตรงกับภาพ
/// </summary>
public class BossPatternGeometryTests
{
    static Shape Circle(float r) => new Shape { type = AoEType.Circle, radius = r };

    [Test]
    public void Circle_ContainsInsideNotOutside()
    {
        var s = Circle(3f);
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(2.9f, 0f, 0f)));
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(3.1f, 0f, 0f)));
    }

    [Test]
    public void Circle_IgnoresHeight()
    {
        Assert.IsTrue(BossPatternGeometry.Contains(Circle(3f), new Vector3(1f, 50f, 1f)));
    }

    [Test]
    public void Donut_HoleIsSafe()
    {
        var s = new Shape { type = AoEType.Donut, radius = 10f, innerRadius = 4f };
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(2f, 0f, 0f)),  "กลางโดนัทต้องปลอดภัย");
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(6f, 0f, 0f)));
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(11f, 0f, 0f)));
    }

    [Test]
    public void Line_StartsAtOriginAndGoesForward()
    {
        var s = new Shape { type = AoEType.Line, lineLength = 10f, lineWidth = 2f, yawDeg = 0f };
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(0f, 0f, 5f)),  "ข้างหน้า (เหนือ)");
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(0f, 0f, -1f)), "ข้างหลังจุดเกิด");
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(1.5f, 0f, 5f)), "นอกความกว้าง");
    }

    [Test]
    public void Line_Yaw90PointsEast()
    {
        var s = new Shape { type = AoEType.Line, lineLength = 10f, lineWidth = 2f, yawDeg = 90f };
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(5f, 0f, 0f)));
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(0f, 0f, 5f)));
    }

    [Test]
    public void Cross_BothArmsCentered()
    {
        var s = new Shape { type = AoEType.Cross, lineLength = 20f, lineWidth = 2f };
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(0f, 0f, -9f)));
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(9f, 0f, 0f)));
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(5f, 0f, 5f)), "มุมระหว่างแขนต้องปลอดภัย");
    }

    [Test]
    public void Cone_OnlyWithinAngle()
    {
        var s = new Shape { type = AoEType.Cone, radius = 10f, coneAngle = 90f, yawDeg = 0f };
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(0f, 0f, 5f)));
        Assert.IsTrue (BossPatternGeometry.Contains(s, new Vector3(3f, 0f, 5f)),  "ในครึ่งมุม 45°");
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(5f, 0f, 1f)),  "นอกมุม");
        Assert.IsFalse(BossPatternGeometry.Contains(s, new Vector3(0f, 0f, -5f)), "ข้างหลัง");
    }

    static BossPatternGeometry.Arena Arena(float r) => new BossPatternGeometry.Arena { center = Vector3.zero, radius = r };

    [Test]
    public void SafeFraction_NoShapes_IsFullySafe()
    {
        Assert.AreEqual(1f, BossPatternGeometry.SafeFraction(Arena(20f), new List<Shape>()));
    }

    [Test]
    public void SafeFraction_CircleCoveringArena_IsZero()
    {
        var shapes = new List<Shape> { Circle(25f) };
        Assert.AreEqual(0f, BossPatternGeometry.SafeFraction(Arena(20f), shapes));
    }

    [Test]
    public void SafeFraction_HalfRadiusCircle_AboutThreeQuartersSafe()
    {
        // วงรัศมีครึ่งหนึ่งของสนาม = พื้นที่ 1/4 → ปลอดภัยราว 75% (ตารางหยาบ คลาดได้นิดหน่อย)
        var shapes = new List<Shape> { Circle(10f) };
        float safe = BossPatternGeometry.SafeFraction(Arena(20f), shapes, 40);
        Assert.That(safe, Is.InRange(0.70f, 0.80f));
    }
}
