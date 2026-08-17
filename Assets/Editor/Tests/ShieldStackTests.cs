using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ ShieldStack — คณิตล้วนของระบบ shield แบบ "รายการชั้น" (ADR-008 D1/D2)
/// ไม่ต้องมี scene/GameObject/NetworkBehaviour ตามแบบเดียวกับ TelegraphGeometryTests.cs
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp (predefined เช่นกัน) ได้เพราะเป็น assembly ที่ตายตัวสองอันมองกันได้
/// อยู่แล้วโดยไม่ต้องประกาศ reference — ถ้าสร้าง .asmdef ให้ไฟล์นี้จะพังทันทีเพราะ .asmdef
/// อ้างอิง predefined assembly (Assembly-CSharp) ไม่ได้
/// </summary>
public class ShieldStackTests
{
    const int SourceA = 101;
    const int SourceB = 102;

    // ── บั๊กที่ ADR-008 แก้ (CRITICAL) ───────────────────────────────────────
    // playermove.cs เดิม: netShieldHP.Value = Mathf.Lerp(shieldAtLastAdd, 0f, elapsed/duration) ทุกเฟรม
    // คำนวณค่าสัมบูรณ์จาก snapshot ตอน AddShield — TakeDamage หักโล่แล้วไม่อัปเดต snapshot
    // เฟรมถัดไป decay เขียนทับกลับไปที่เส้นเดิมราวกับไม่มีอะไรถูกหัก
    [Test]
    public void AbsorbThenDecay_DoesNotRestoreAbsorbedAmount_RegressionForADR008CriticalBug()
    {
        var layers = new List<ShieldLayer>();

        // t=0: AddShield(100) — shieldDuration=3
        ShieldStack.Add(layers, 100f, duration: 3f, decays: true, sourceId: ShieldSourceId.Unassigned, now: 0f);

        // t=1.0: สลายไปตามเวลา → ~66.7 (100 * (1 - 1/3))
        ShieldStack.Tick(layers, deltaTime: 1.0f, now: 1.0f);
        Assert.AreEqual(66.667f, ShieldStack.Sum(layers), 0.01f, "decay ที่ t=1.0 ต้องเหลือ ~66.7");

        // t=1.1: โดนตี 50 → เหลือ ~16.7
        float leftover = ShieldStack.Absorb(layers, 50f, now: 1.1f);
        Assert.AreEqual(0f, leftover, "โล่ยังพอดูดดาเมจ 50 ทั้งหมด ไม่มี overkill");
        Assert.AreEqual(16.667f, ShieldStack.Sum(layers), 0.01f, "หลังโดนตี 50 ต้องเหลือ ~16.7");

        // t=1.2: decay ต่อไปอีก 0.1 วิ — ของเดิม (บั๊ก) จะ Lerp(100,0,0.4) กลับไปที่ 60.0
        // ของใหม่ต้องลดต่อจาก 16.7 เท่านั้น ไม่เด้งกลับไปเส้นเดิมที่ไม่รู้เรื่องการหักดาเมจ
        ShieldStack.Tick(layers, deltaTime: 0.1f, now: 1.2f);
        float afterFurtherDecay = ShieldStack.Sum(layers);

        Assert.Less(afterFurtherDecay, 16.667f,
            "decay ต้องลดต่อจากค่าหลังโดนดาเมจ ไม่ใช่หยุดนิ่งหรือเพิ่มขึ้น");
        Assert.Less(afterFurtherDecay, 60f,
            "ค่านี้คือใจกลางบั๊ก ADR-008: ของเดิมเด้งกลับไป 60.0 ที่นี่พอดี — ถ้าเทสนี้ล้มเหลวแปลว่าบั๊กกลับมา");
        Assert.AreEqual(13.333f, afterFurtherDecay, 0.01f,
            "ค่าที่ถูกต้อง: 16.667 - (100/3)*0.1 ≈ 13.333 (อัตรา decay อิง initialAmount ไม่ใช่ amount ปัจจุบัน)");
    }

    // ── Absorb: ดูดจากชั้นใกล้หมดอายุก่อน ────────────────────────────────────
    [Test]
    public void Absorb_ConsumesSoonestExpiringLayerFirst()
    {
        var layers = new List<ShieldLayer>();
        // A หมดอายุช้ากว่า (t=5) เพิ่มก่อน · B หมดอายุเร็วกว่า (t=2) เพิ่มทีหลัง
        // สลับลำดับการเพิ่มจงใจ กัน false-positive จาก "list order เดิม" บังเอิญตรงกับ "expireAt order"
        ShieldStack.Add(layers, amount: 30f, duration: 5f, decays: false, sourceId: SourceA, now: 0f);
        ShieldStack.Add(layers, amount: 10f, duration: 2f, decays: false, sourceId: SourceB, now: 0f);

        float leftover = ShieldStack.Absorb(layers, 15f, now: 0f);

        Assert.AreEqual(0f, leftover, "15 ดาเมจ ดูดได้หมดโดยไม่ overkill (10 จาก B + 5 จาก A)");
        Assert.AreEqual(1, layers.Count, "B (หมดอายุเร็วกว่า) ต้องถูกใช้จนหมดและถูกลบออกจาก list");
        Assert.AreEqual(SourceA, layers[0].sourceId, "ชั้นที่เหลือต้องเป็น A ไม่ใช่ B");
        Assert.AreEqual(25f, layers[0].amount, 0.001f, "A ต้องเหลือ 30 - 5 = 25 หลัง B ถูกใช้จนหมดก่อน");
    }

    // ── Non-decaying: อยู่เต็มค่าจนหมดอายุ แล้วหายทันที ────────────────────────
    [Test]
    public void NonDecayingLayer_HoldsFullValueUntilExpiry_ThenVanishes()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, amount: 50f, duration: 3f, decays: false, sourceId: SourceA, now: 0f);

        // ก่อนหมดอายุ — amount ต้องนิ่งเต็มค่า ไม่ค่อยๆ ลดแบบชั้น decaying
        ShieldStack.Tick(layers, deltaTime: 1f, now: 1f);
        Assert.AreEqual(50f, ShieldStack.Sum(layers), 0.001f, "ยังไม่หมดอายุ ต้องเต็มค่า 50 เป๊ะ");
        ShieldStack.Tick(layers, deltaTime: 1.9f, now: 2.9f);
        Assert.AreEqual(50f, ShieldStack.Sum(layers), 0.001f, "ใกล้หมดอายุแต่ยังไม่ถึง ต้องยังเต็มค่า");

        // ถึง expireAt พอดี — หายทันที ไม่ใช่ค่อยๆ ลด
        ShieldStack.Tick(layers, deltaTime: 0.1f, now: 3.0f);
        Assert.AreEqual(0, layers.Count, "หมดอายุแล้วต้องหายทันที (ถูกลบออกจาก list)");
    }

    // ── Decaying: ถึงศูนย์พอดีที่ duration ────────────────────────────────────
    [Test]
    public void DecayingLayer_ReachesZeroExactlyAtDuration()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, amount: 90f, duration: 3f, decays: true, sourceId: SourceA, now: 0f);

        ShieldStack.Tick(layers, deltaTime: 1f, now: 1f);
        Assert.AreEqual(60f, ShieldStack.Sum(layers), 0.001f);

        ShieldStack.Tick(layers, deltaTime: 1f, now: 2f);
        Assert.AreEqual(30f, ShieldStack.Sum(layers), 0.001f);

        ShieldStack.Tick(layers, deltaTime: 1f, now: 3f);
        Assert.AreEqual(0, layers.Count, "ถึง duration พอดี (t=3) ต้องเหลือ 0 และถูกลบออกจาก list");
    }

    // ── หลายชั้นหมดอายุพร้อมกัน ───────────────────────────────────────────────
    [Test]
    public void MultipleLayers_ExpiringOnSameTick_AllVanishTogether()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, amount: 20f, duration: 2f, decays: false, sourceId: SourceA, now: 0f);
        ShieldStack.Add(layers, amount: 40f, duration: 2f, decays: false, sourceId: SourceB, now: 0f);
        ShieldStack.Add(layers, amount: 60f, duration: 2f, decays: true,  sourceId: ShieldSourceId.Unassigned, now: 0f);

        Assert.AreEqual(3, layers.Count);

        // เดินเวลาข้ามไปถึง t=2 ในทีเดียว (deltaTime กว้างพอให้ decaying layer ลบเหลือ <= 0 ด้วย)
        ShieldStack.Tick(layers, deltaTime: 2f, now: 2f);

        Assert.AreEqual(0, layers.Count, "ทั้งสามชั้นต้องหมดอายุ/สลายจนหมดพร้อมกันที่ tick เดียว");
    }

    // ── Overkill: ดาเมจเกินกว่าที่ทุกชั้นรวมกันดูดไหว ─────────────────────────
    [Test]
    public void Absorb_OverkillDamage_ReturnsCorrectLeftover()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, amount: 40f, duration: 5f, decays: true, sourceId: SourceA, now: 0f);

        float leftover = ShieldStack.Absorb(layers, 100f, now: 0f);

        Assert.AreEqual(60f, leftover, 0.001f, "100 ดาเมจ - 40 ที่โล่ดูดได้ = 60 เหลือไปหักเลือด");
        Assert.AreEqual(0, layers.Count, "ชั้นที่ถูกดูดจนหมดต้องถูกลบออกจาก list");
    }

    // ── Refresh-by-sourceId: แทนที่ ไม่สแตก ──────────────────────────────────
    [Test]
    public void Refresh_BySourceId_ReplacesRatherThanStacks()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Refresh(layers, ShieldSourceId.SupportArena, amount: 50f, duration: 2f, decays: true, now: 0f);
        Assert.AreEqual(1, layers.Count);
        Assert.AreEqual(50f, ShieldStack.Sum(layers), 0.001f);

        // รีเฟรชซ้ำก่อนหมดอายุ ด้วยค่าใหม่ — ต้องแทนที่ชั้นเดิม ไม่ใช่บวกเข้าไปกลายเป็น 130
        ShieldStack.Refresh(layers, ShieldSourceId.SupportArena, amount: 80f, duration: 2f, decays: true, now: 1f);

        Assert.AreEqual(1, layers.Count, "sourceId เดียวกัน ต้องยังเป็นชั้นเดียว ไม่สแตก");
        Assert.AreEqual(80f, ShieldStack.Sum(layers), 0.001f, "ค่าต้องถูกแทนที่เป็น 80 ไม่ใช่ 50+80=130");
    }

    [Test]
    public void Refresh_UnknownSourceId_CreatesNewLayer_LikeAdd()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Refresh(layers, SourceA, amount: 25f, duration: 1f, decays: false, now: 0f);

        Assert.AreEqual(1, layers.Count);
        Assert.AreEqual(SourceA, layers[0].sourceId);
        Assert.AreEqual(25f, layers[0].amount, 0.001f);
    }

    // ── ค่าที่รับเข้ามาไม่ได้ต้องถูกปฏิเสธ ────────────────────────────────────
    [Test]
    public void IsValidAmount_RejectsNegativeZeroNaNAndInfinity()
    {
        Assert.IsFalse(ShieldStack.IsValidAmount(-5f));
        Assert.IsFalse(ShieldStack.IsValidAmount(0f));
        Assert.IsFalse(ShieldStack.IsValidAmount(float.NaN));
        Assert.IsFalse(ShieldStack.IsValidAmount(float.PositiveInfinity));
        Assert.IsTrue(ShieldStack.IsValidAmount(1f));
    }

    [Test]
    public void Add_NegativeZeroOrNaNAmount_IsNoOp()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, -10f, duration: 3f, decays: true, sourceId: SourceA, now: 0f);
        ShieldStack.Add(layers, 0f, duration: 3f, decays: true, sourceId: SourceA, now: 0f);
        ShieldStack.Add(layers, float.NaN, duration: 3f, decays: true, sourceId: SourceA, now: 0f);
        Assert.AreEqual(0, layers.Count, "ค่าที่ไม่ผ่าน IsValidAmount ต้องไม่ถูกเพิ่มเป็นชั้นเลย");
    }

    [Test]
    public void Add_NonPositiveDuration_IsNoOp()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, 10f, duration: 0f, decays: true, sourceId: SourceA, now: 0f);
        ShieldStack.Add(layers, 10f, duration: -1f, decays: true, sourceId: SourceA, now: 0f);
        Assert.AreEqual(0, layers.Count, "duration <= 0 ต้องไม่สร้างชั้นที่ไม่มีวันหมดอายุ/หารด้วยศูนย์");
    }

    [Test]
    public void Refresh_NegativeOrNaNAmount_IsNoOp_EvenWhenLayerExists()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Refresh(layers, SourceA, amount: 40f, duration: 2f, decays: true, now: 0f);

        ShieldStack.Refresh(layers, SourceA, amount: -1f, duration: 2f, decays: true, now: 1f);
        ShieldStack.Refresh(layers, SourceA, amount: float.NaN, duration: 2f, decays: true, now: 1f);

        Assert.AreEqual(1, layers.Count);
        Assert.AreEqual(40f, ShieldStack.Sum(layers), 0.001f, "ค่าที่ไม่ผ่านการตรวจต้องไม่แตะชั้นเดิมเลย");
    }

    [Test]
    public void Absorb_NaNDamage_ConsumesNothingAndReturnsZero()
    {
        var layers = new List<ShieldLayer>();
        ShieldStack.Add(layers, 50f, duration: 3f, decays: true, sourceId: SourceA, now: 0f);

        float leftover = ShieldStack.Absorb(layers, float.NaN, now: 0f);

        Assert.AreEqual(0f, leftover);
        Assert.AreEqual(50f, ShieldStack.Sum(layers), 0.001f, "ดาเมจเสีย (NaN) ต้องไม่แตะโล่เลย");
    }
}
