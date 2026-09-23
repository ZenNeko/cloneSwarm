using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ LayeredTrack — IndexOf + Validate() (กติกาไฟล์ stem ที่ LayeredMusicPlayer
/// กับ P3RSmokeTest ใช้ร่วมกัน) · AudioClip.Create ทำใน EditMode ได้ ไม่ต้องมีไฟล์เสียงจริง
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp ได้โดยไม่ต้องประกาศ reference (ดูเหตุผลเต็มที่ ShieldStackTests.cs)
///
/// ข้อความผิดพลาดเป็นภาษาไทย — assert แค่ท่อนที่บอกชนิดปัญหา ไม่ assert ทั้งประโยค
/// แก้ถ้อยคำได้โดย test ไม่พัง ตราบใดที่ยังบอกปัญหาชนิดเดิม
/// </summary>
public class LayeredTrackTests
{
    readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created)
            if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    LayeredTrack MakeTrack(params LayeredTrack.Stem[] stems)
    {
        var t = ScriptableObject.CreateInstance<LayeredTrack>();
        created.Add(t);
        t.stems = stems;
        return t;
    }

    AudioClip MakeClip(string name, int samples = 4410, int frequency = 44100)
    {
        var c = AudioClip.Create(name, samples, 1, frequency, false);
        created.Add(c);
        return c;
    }

    static LayeredTrack.Stem Stem(string name, AudioClip clip) =>
        new LayeredTrack.Stem { name = name, clip = clip };

    // ── IndexOf ──────────────────────────────────────────────────────────

    [Test]
    public void IndexOf_FindsStemByName()
    {
        var t = MakeTrack(Stem("pad", null), Stem("drums", null), Stem("bass", null));
        Assert.AreEqual(0, t.IndexOf("pad"));
        Assert.AreEqual(1, t.IndexOf("drums"));
        Assert.AreEqual(2, t.IndexOf("bass"));
    }

    [Test]
    public void IndexOf_MissingOrEmptyName_ReturnsMinusOne()
    {
        var t = MakeTrack(Stem("pad", null));
        Assert.AreEqual(-1, t.IndexOf("lead"));
        Assert.AreEqual(-1, t.IndexOf(""));
        Assert.AreEqual(-1, t.IndexOf(null));
    }

    [Test]
    public void IndexOf_NullStems_ReturnsMinusOne()
    {
        var t = MakeTrack();
        t.stems = null;
        Assert.AreEqual(-1, t.IndexOf("pad"));
    }

    // ── Validate ─────────────────────────────────────────────────────────

    [Test]
    public void Validate_NoStems_ReportsProblem()
    {
        var t = MakeTrack();
        StringAssert.Contains("ไม่มี stem", t.Validate());

        t.stems = null;
        StringAssert.Contains("ไม่มี stem", t.Validate());
    }

    [Test]
    public void Validate_EmptyName_ReportsProblem()
    {
        var t = MakeTrack(Stem("pad", MakeClip("a")), Stem("", MakeClip("b")));
        StringAssert.Contains("ไม่มีชื่อ", t.Validate());
    }

    [Test]
    public void Validate_DuplicateName_ReportsProblem()
    {
        var t = MakeTrack(Stem("pad", MakeClip("a")), Stem("pad", MakeClip("b")));
        StringAssert.Contains("ซ้ำ", t.Validate());
    }

    [Test]
    public void Validate_NullClip_ReportsProblem()
    {
        var t = MakeTrack(Stem("pad", MakeClip("a")), Stem("drums", null));
        string err = t.Validate();
        StringAssert.Contains("ไม่มีคลิป", err);
        StringAssert.Contains("drums", err);
    }

    [Test]
    public void Validate_FrequencyMismatch_ReportsSampleRate()
    {
        var t = MakeTrack(Stem("pad",   MakeClip("a", 4410, 44100)),
                          Stem("drums", MakeClip("b", 4410, 48000)));
        StringAssert.Contains("sample rate", t.Validate());
    }

    [Test]
    public void Validate_LengthMismatch_ReportsLoopDrift()
    {
        var t = MakeTrack(Stem("pad",   MakeClip("a", 4410, 44100)),
                          Stem("drums", MakeClip("b", 4411, 44100)));
        StringAssert.Contains("ลูปจะเหลื่อม", t.Validate());
    }

    [Test]
    public void Validate_MatchingStems_ReturnsNull()
    {
        var t = MakeTrack(Stem("pad",   MakeClip("a")),
                          Stem("drums", MakeClip("b")),
                          Stem("bass",  MakeClip("c")));
        Assert.IsNull(t.Validate());
    }

    [Test]
    public void Validate_SingleStem_ReturnsNull()
    {
        // เพลงเต็มเป็น stem เดียว — กรณี "ยังไม่มี stem" ที่ doc ของคลาสบอกว่าต้องใช้ได้
        var t = MakeTrack(Stem("full", MakeClip("a")));
        Assert.IsNull(t.Validate());
    }
}
