using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ LayeredMusicPlayer — คณิตของ volume ที่ AudioSource แต่ละชั้นได้จริง
///   volume = ระดับชั้น × deck gain (crossfade) × duck (จอเลือกการ์ด) × output scale (slider)
///
/// ไม่ได้ฟังเสียง (EditMode ไม่มีอะไรให้ฟัง) — ตรวจตัวเลขที่ส่งให้ AudioSource ตรงๆ
/// เวลาเดินด้วย Tick(dt) ที่เทสต์ส่งเอง ไม่พึ่ง Update/Time
/// การตรงจังหวะของ stem (PlayScheduled) และความเนียนของ fade ต้องฟังจริง — ดูเช็กลิสต์ใน PR
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp ได้โดยไม่ต้องประกาศ reference (ดูเหตุผลเต็มที่ ShieldStackTests.cs)
/// </summary>
public class LayeredMusicPlayerTests
{
    readonly List<Object> created = new List<Object>();
    LayeredMusicPlayer player;

    [SetUp]
    public void SetUp()
    {
        var go = new GameObject("MusicPlayerTest");
        created.Add(go);
        player = go.AddComponent<LayeredMusicPlayer>();
        player.Init(null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created)
            if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    LayeredTrack MakeTrack(int stems)
    {
        var t = ScriptableObject.CreateInstance<LayeredTrack>();
        created.Add(t);
        t.stems = new LayeredTrack.Stem[stems];
        for (int i = 0; i < stems; i++)
        {
            // ยาวเท่ากันทุกชั้น — ไม่งั้น WarnIfInvalid เตือน
            var clip = AudioClip.Create($"stem{i}", 4410, 1, 44100, false);
            created.Add(clip);
            t.stems[i] = new LayeredTrack.Stem { name = $"s{i}", clip = clip };
        }
        return t;
    }

    AudioSource[] Sources() => player.GetComponentsInChildren<AudioSource>(true);

    // ── ระดับชั้น ────────────────────────────────────────────────────────

    [Test]
    public void Play_NoFade_AppliesLevelsImmediately()
    {
        var t = MakeTrack(3);
        player.Play(t, 0f, new[] { 1f, 0.5f });   // ชั้นที่ขาด = 0

        Assert.AreSame(t, player.CurrentTrack);
        Assert.AreEqual(3, player.StemCount);
        Assert.AreEqual(1f,   player.StemVolume(0), 1e-5f);
        Assert.AreEqual(0.5f, player.StemVolume(1), 1e-5f);
        Assert.AreEqual(0f,   player.StemVolume(2), 1e-5f);
    }

    [Test]
    public void Play_EveryStemGetsItsOwnSourceAndClip()
    {
        var t = MakeTrack(4);
        player.Play(t, 0f, null);

        var clips = new HashSet<AudioClip>();
        foreach (var s in Sources()) if (s.clip != null) clips.Add(s.clip);
        Assert.AreEqual(4, clips.Count);
        foreach (var s in Sources()) Assert.IsTrue(s.loop);
    }

    [Test]
    public void SetLevels_FadesLinearlyOverFadeTime()
    {
        player.Play(MakeTrack(2), 0f, new[] { 0f, 1f });
        player.SetLevels(new[] { 1f, 0f }, 2f);

        player.Tick(1f);
        Assert.AreEqual(0.5f, player.StemLevel(0), 1e-4f, "ครึ่งทางขาขึ้น");
        Assert.AreEqual(0.5f, player.StemLevel(1), 1e-4f, "ครึ่งทางขาลง");

        player.Tick(1.5f);
        Assert.AreEqual(1f, player.StemLevel(0), 1e-5f, "ไม่เกินเป้า");
        Assert.AreEqual(0f, player.StemLevel(1), 1e-5f);
    }

    [Test]
    public void SilentLayer_KeepsPlayingSource()
    {
        // ชั้นที่ปิดต้องยังมีคลิปค้างอยู่ — ถ้าคืน source แล้วเปิดใหม่ทีหลังจะหลุดจังหวะ
        player.Play(MakeTrack(2), 0f, new[] { 1f, 1f });
        player.SetLevels(new[] { 1f, 0f }, 0f);
        player.Tick(5f);

        int withClip = 0;
        foreach (var s in Sources()) if (s.clip != null) withClip++;
        Assert.AreEqual(2, withClip);
    }

    // ── duck + slider ────────────────────────────────────────────────────

    [Test]
    public void Duck_ScalesVolumeButNotLayerLevel()
    {
        player.Play(MakeTrack(1), 0f, new[] { 0.8f });
        player.SetDuck(0.5f, 0f);

        Assert.AreEqual(0.8f, player.StemLevel(0),  1e-5f);
        Assert.AreEqual(0.4f, player.StemVolume(0), 1e-5f);

        player.SetDuck(1f, 0.4f);
        player.Tick(0.2f);
        Assert.AreEqual(0.75f, player.Duck, 1e-4f, "duck ค่อยๆ คืน");
        player.Tick(1f);
        Assert.AreEqual(0.8f, player.StemVolume(0), 1e-5f);
    }

    [Test]
    public void OutputScale_DoesNotWakeSilentLayers()
    {
        // บั๊กเดิมของ RescalePlaying: ขยับ slider แล้วชั้นที่ปิดอยู่ดังเต็ม
        player.Play(MakeTrack(2), 0f, new[] { 1f, 0f });
        player.SetOutputScale(0.6f);

        Assert.AreEqual(0.6f, player.StemVolume(0), 1e-5f);
        Assert.AreEqual(0f,   player.StemVolume(1), 1e-5f);
    }

    // ── สลับ track ───────────────────────────────────────────────────────

    [Test]
    public void Crossfade_NewTrackRisesWhileOldFadesThenReleases()
    {
        var a = MakeTrack(2);
        var b = MakeTrack(3);
        player.Play(a, 0f, new[] { 1f, 1f });
        var aSources = new List<AudioSource>(Sources());

        player.Play(b, 2f, new[] { 1f, 1f, 1f });
        Assert.AreSame(b, player.CurrentTrack);
        Assert.AreEqual(0f, player.StemVolume(0), 1e-5f, "เพลงใหม่เริ่มจากเงียบ");

        player.Tick(1f);
        Assert.AreEqual(0.5f, player.StemVolume(0), 1e-4f, "เพลงใหม่ครึ่งทาง");
        foreach (var s in aSources)
            Assert.AreEqual(0.5f, s.volume, 1e-4f, "เพลงเก่าครึ่งทาง");

        player.Tick(1.1f);
        Assert.AreEqual(1f, player.StemVolume(0), 1e-5f);
        foreach (var s in aSources)
            Assert.IsNull(s.clip, "เพลงเก่าเงียบแล้วต้องคืน source");
    }

    [Test]
    public void Crossfade_ReusesReleasedSources()
    {
        player.Play(MakeTrack(3), 0f, null);
        player.Play(MakeTrack(3), 1f, null);
        player.Tick(2f);                      // ชุดแรกคืนแล้ว
        player.Play(MakeTrack(2), 1f, null);  // ต้องหยิบของที่คืนมาใช้

        Assert.AreEqual(6, Sources().Length, "สองชุดที่ซ้อนกันตอน crossfade = มากสุดที่ต้องมี");
    }

    [Test]
    public void Stop_WithFade_FadesOutThenReleases()
    {
        player.Play(MakeTrack(2), 0f, new[] { 1f, 1f });
        var src = Sources();

        player.Stop(1f);
        Assert.IsNull(player.CurrentTrack);
        player.Tick(0.5f);
        Assert.AreEqual(0.5f, src[0].volume, 1e-4f);

        player.Tick(0.6f);
        foreach (var s in src) Assert.IsNull(s.clip);
    }

    [Test]
    public void Stop_NoFade_ReleasesImmediately()
    {
        player.Play(MakeTrack(2), 0f, new[] { 1f, 1f });
        player.Stop(0f);
        foreach (var s in Sources()) Assert.IsNull(s.clip);
    }
}
