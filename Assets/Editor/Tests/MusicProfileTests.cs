using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ MusicProfile — คณิตล้วนที่ MusicDirector ใช้เลือก mix:
/// BandIndexAt (ไม่พึ่งลำดับใน Inspector) · PhaseMix (clamp เฟส) · FadeOf (ค่าติดลบ = ค่าตั้งต้น)
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp ได้โดยไม่ต้องประกาศ reference (ดูเหตุผลเต็มที่ ShieldStackTests.cs)
/// </summary>
public class MusicProfileTests
{
    MusicProfile profile;

    [SetUp]
    public void SetUp() => profile = ScriptableObject.CreateInstance<MusicProfile>();

    [TearDown]
    public void TearDown()
    {
        if (profile != null) Object.DestroyImmediate(profile);
    }

    static MusicProfile.TimeBand Band(float atMinutes) =>
        new MusicProfile.TimeBand { atMinutes = atMinutes, mix = new StemMix() };

    // ── BandIndexAt ──────────────────────────────────────────────────────

    [Test]
    public void BandIndexAt_NoBands_ReturnsMinusOne()
    {
        profile.timeBands = new MusicProfile.TimeBand[0];
        Assert.AreEqual(-1, profile.BandIndexAt(5f));

        profile.timeBands = null;
        Assert.AreEqual(-1, profile.BandIndexAt(5f));
    }

    [Test]
    public void BandIndexAt_UnsortedBands_PicksLatestStartedBand()
    {
        // ลำดับใน Inspector สลับ: index 0 = นาที 5, index 1 = นาที 0, index 2 = นาที 10
        profile.timeBands = new[] { Band(5f), Band(0f), Band(10f) };

        Assert.AreEqual(1, profile.BandIndexAt(0f));
        Assert.AreEqual(1, profile.BandIndexAt(4.9f));
        Assert.AreEqual(0, profile.BandIndexAt(5f),  "ขอบซ้ายนับรวม (atMinutes <= minutes)");
        Assert.AreEqual(0, profile.BandIndexAt(7f));
        Assert.AreEqual(2, profile.BandIndexAt(10f));
        Assert.AreEqual(2, profile.BandIndexAt(99f));
    }

    [Test]
    public void BandIndexAt_BeforeFirstBand_ReturnsEarliestBand()
    {
        // ไม่มี band เริ่มที่ 0 — band ที่เริ่มเร็วสุดต้องครอบตั้งแต่ต้นเกม
        // และต้องหา "เร็วสุด" จากค่า ไม่ใช่จาก index 0
        profile.timeBands = new[] { Band(6f), Band(2f), Band(4f) };
        Assert.AreEqual(1, profile.BandIndexAt(0f));
        Assert.AreEqual(1, profile.BandIndexAt(1.5f));
    }

    // ── PhaseMix ─────────────────────────────────────────────────────────

    [Test]
    public void PhaseMix_NoPhases_ReturnsNull()
    {
        profile.mainBossPhases = new StemMix[0];
        Assert.IsNull(profile.PhaseMix(0));

        profile.mainBossPhases = null;
        Assert.IsNull(profile.PhaseMix(0));
    }

    [Test]
    public void PhaseMix_ClampsPhaseIntoRange()
    {
        var p0 = new StemMix();
        var p1 = new StemMix();
        var p2 = new StemMix();
        profile.mainBossPhases = new[] { p0, p1, p2 };

        Assert.AreSame(p0, profile.PhaseMix(-1), "เฟสติดลบ → ช่องแรก");
        Assert.AreSame(p0, profile.PhaseMix(0));
        Assert.AreSame(p1, profile.PhaseMix(1));
        Assert.AreSame(p2, profile.PhaseMix(2));
        Assert.AreSame(p2, profile.PhaseMix(5), "เฟสเกินจำนวนช่อง → ใช้ช่องสุดท้าย");
    }

    // ── FadeOf ───────────────────────────────────────────────────────────

    [Test]
    public void FadeOf_UsesMixFadeWhenNonNegative()
    {
        profile.defaultFade = 2f;
        Assert.AreEqual(1.5f, profile.FadeOf(new StemMix { fadeSeconds = 1.5f }));
        Assert.AreEqual(0f,   profile.FadeOf(new StemMix { fadeSeconds = 0f }), "0 = ตัดทันที ไม่ใช่ค่าตั้งต้น");
    }

    [Test]
    public void FadeOf_NegativeOrNullMix_UsesDefaultFade()
    {
        profile.defaultFade = 2f;
        Assert.AreEqual(2f, profile.FadeOf(new StemMix { fadeSeconds = -1f }));
        Assert.AreEqual(2f, profile.FadeOf(null));
    }
}
