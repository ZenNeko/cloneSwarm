using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode test ของ MusicMix — สูตรที่ MusicDirector ใช้ตัดสินว่าแต่ละชั้นดังเท่าไร
/// ไล่ทุกสถานการณ์ในรันจริง (ช่วงเวลา · มินิบอส · จอเลือกการ์ด · บอสใหญ่มี/ไม่มีธีม) โดยไม่ต้องเปิดซีน
///
/// เพลงในเทสต์: เพลงหลัก pad / drums / bass / perc · ธีมบอส b_drums / b_lead
/// คลิปเป็น null ได้ — สูตรอ่านแค่ชื่อ stem
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor (predefined)
/// ซึ่งมองเห็น Assembly-CSharp ได้โดยไม่ต้องประกาศ reference (ดูเหตุผลเต็มที่ ShieldStackTests.cs)
/// </summary>
public class MusicMixTests
{
    const int Pad = 0, Drums = 1, Bass = 2, Perc = 3;
    const int BDrums = 0, BLead = 1;

    readonly List<Object> created = new List<Object>();
    MusicProfile profile;
    LayeredTrack main, boss;
    MusicMix mix;

    [SetUp]
    public void SetUp()
    {
        main = MakeTrack("Main", "pad", "drums", "bass", "perc");
        boss = MakeTrack("Boss", "b_drums", "b_lead");

        profile = ScriptableObject.CreateInstance<MusicProfile>();
        created.Add(profile);
        profile.name        = "TestProfile";
        profile.track       = main;
        profile.defaultFade = 2f;
        profile.timeBands = new[]
        {
            Band(0f,  L("pad", 1f)),
            Band(5f,  L("pad", 1f), L("drums", 1f)),
            Band(10f, L("pad", 1f), L("drums", 1f), L("bass", 0.8f)),
        };
        profile.miniBossOverlay   = new StemMix { levels = new[] { L("perc", 1f), L("bass", 0.3f) }, fadeSeconds = 1f };
        profile.cardPickOverrides = new[] { L("drums", 0f), L("b_drums", 0.2f) };
        profile.cardPickDuck = 0.5f;
        profile.cardPickFade = 0.4f;

        mix = new MusicMix();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created)
            if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    LayeredTrack MakeTrack(string name, params string[] stems)
    {
        var t = ScriptableObject.CreateInstance<LayeredTrack>();
        created.Add(t);
        t.name  = name;
        t.stems = new LayeredTrack.Stem[stems.Length];
        for (int i = 0; i < stems.Length; i++) t.stems[i] = new LayeredTrack.Stem { name = stems[i] };
        return t;
    }

    static StemLevel L(string stem, float level) => new StemLevel { stem = stem, level = level };

    static MusicProfile.TimeBand Band(float atMinutes, params StemLevel[] levels) =>
        new MusicProfile.TimeBand { atMinutes = atMinutes, mix = new StemMix { levels = levels } };

    void UseBossTheme()
    {
        profile.mainBossTrack  = boss;
        profile.mainBossPhases = new[]
        {
            new StemMix { levels = new[] { L("b_drums", 1f) } },
            new StemMix { levels = new[] { L("b_drums", 1f), L("b_lead", 1f) }, fadeSeconds = 0.5f },
        };
    }

    MusicMix.Result Eval(float minutes, bool miniBoss = false, bool cardPick = false,
                         bool mainBoss = false, int phase = 0,
                         bool sentOverlay = false, bool sentCardPick = false) =>
        mix.Evaluate(profile, new MusicMix.Facts
        {
            minutes = minutes, miniBoss = miniBoss, cardPick = cardPick,
            mainBossPhase = mainBoss, bossPhase = phase,
            sentOverlay = sentOverlay, sentCardPick = sentCardPick,
        });

    void AssertLevels(params float[] expected)
    {
        Assert.AreEqual(expected.Length, mix.Levels.Length, "จำนวนชั้น");
        for (int i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], mix.Levels[i], 1e-5f, $"ชั้น #{i}");
    }

    // ── ช่วงเวลา ─────────────────────────────────────────────────────────

    [Test]
    public void TimeBands_AddLayersAsMinutesPass()
    {
        var r = Eval(0f);
        Assert.AreSame(main, r.track);
        AssertLevels(1f, 0f, 0f, 0f);

        Eval(4.9f);  AssertLevels(1f, 0f, 0f,   0f);
        Eval(5f);    AssertLevels(1f, 1f, 0f,   0f);
        Eval(14.9f); AssertLevels(1f, 1f, 0.8f, 0f);
    }

    [Test]
    public void TimeBands_UseBandFadeAndFullVolume()
    {
        var r = Eval(6f);
        Assert.AreEqual(2f, r.fade, "band ไม่ได้ตั้ง fade → ค่าตั้งต้นของ profile");
        Assert.AreEqual(1f, r.duck);
        Assert.IsFalse(r.overlay);
    }

    // ── มินิบอส ──────────────────────────────────────────────────────────

    [Test]
    public void MiniBoss_AddsOverlayLayers()
    {
        var r = Eval(2f, miniBoss: true);
        Assert.IsTrue(r.overlay);
        AssertLevels(1f, 0f, 0.3f, 1f);
    }

    [Test]
    public void MiniBoss_OverlayNeverLowersALayer()
    {
        // นาที 12 bass = 0.8 อยู่แล้ว · overlay ตั้ง bass 0.3 → ต้องยังเป็น 0.8
        Eval(12f, miniBoss: true);
        AssertLevels(1f, 1f, 0.8f, 1f);
    }

    [Test]
    public void MiniBoss_StartAndEndUseOverlayFade()
    {
        Assert.AreEqual(1f, Eval(2f, miniBoss: true,  sentOverlay: false).fade, "มินิบอสออก");
        Assert.AreEqual(2f, Eval(2f, miniBoss: true,  sentOverlay: true).fade,  "ระหว่างสู้ ไม่มีอะไรเปลี่ยน");
        Assert.AreEqual(1f, Eval(2f, miniBoss: false, sentOverlay: true).fade,  "มินิบอสตายหมด");
    }

    // ── จอเลือกการ์ด ──────────────────────────────────────────────────────

    [Test]
    public void CardPick_OverridesOnlyListedLayers_AndDucks()
    {
        var r = Eval(12f, miniBoss: true, cardPick: true, sentOverlay: true);
        AssertLevels(1f, 0f, 0.8f, 1f);   // drums ถูกบังคับเป็น 0 · ที่เหลือตามสถานการณ์
        Assert.AreEqual(0.5f, r.duck);
    }

    [Test]
    public void CardPick_OpenAndCloseUseCardPickFade()
    {
        Assert.AreEqual(0.4f, Eval(6f, cardPick: true,  sentCardPick: false).fade, "เปิดจอ");
        Assert.AreEqual(0.4f, Eval(6f, cardPick: false, sentCardPick: true).fade,  "ปิดจอ");
    }

    [Test]
    public void CardPick_Closed_RestoresTheMix()
    {
        Eval(6f, cardPick: true);
        var r = Eval(6f, cardPick: false, sentCardPick: true);
        AssertLevels(1f, 1f, 0f, 0f);
        Assert.AreEqual(1f, r.duck);
    }

    [Test]
    public void CardPick_NameFromOtherTrack_IsSilentlySkipped()
    {
        // b_drums อยู่ในรายการเพื่อใช้ตอนธีมบอส — ตอนเพลงหลักต้องข้ามเงียบๆ ไม่เตือนทุกครั้งที่เปิดจอ
        Eval(6f, cardPick: true);
        LogAssert.NoUnexpectedReceived();
    }

    // ── บอสใหญ่ มีธีม ────────────────────────────────────────────────────

    [Test]
    public void MainBoss_WithTheme_SwitchesTrackAndFollowsPhase()
    {
        UseBossTheme();

        var r = Eval(15f, mainBoss: true, phase: 0);
        Assert.AreSame(boss, r.track);
        AssertLevels(1f, 0f);
        Assert.AreEqual(2f, r.fade);

        r = Eval(15.5f, mainBoss: true, phase: 1);
        AssertLevels(1f, 1f);
        Assert.AreEqual(0.5f, r.fade, "ใช้ fade ของเฟส");

        Eval(16f, mainBoss: true, phase: 4);
        AssertLevels(1f, 1f);   // เฟสเกินจำนวนช่อง → ช่องสุดท้าย
    }

    [Test]
    public void MainBoss_WithTheme_IgnoresMiniBossOverlay()
    {
        UseBossTheme();
        var r = Eval(15f, mainBoss: true, miniBoss: true);
        Assert.IsFalse(r.overlay);
        AssertLevels(1f, 0f);
    }

    [Test]
    public void MainBoss_WithTheme_CardPickUsesBossStemNames()
    {
        UseBossTheme();
        Eval(15f, mainBoss: true, phase: 1, cardPick: true);
        AssertLevels(0.2f, 1f);
    }

    [Test]
    public void MainBoss_EmptyThemeTrack_CountsAsNoTheme()
    {
        profile.mainBossTrack = MakeTrack("Empty");
        Assert.AreSame(main, Eval(15f, mainBoss: true).track);
    }

    // ── บอสใหญ่ ไม่มีธีม แต่ตั้งเฟสไว้ = ปรับชั้นของเพลงหลัก ────────────────

    void UseMainTrackPhases()
    {
        profile.mainBossPhases = new[]
        {
            new StemMix { levels = new[] { L("pad", 1f), L("drums", 1f), L("bass", 1f) } },
            new StemMix { levels = new[] { L("pad", 1f), L("drums", 1f), L("bass", 1f), L("perc", 1f) }, fadeSeconds = 0.5f },
        };
    }

    [Test]
    public void MainBoss_NoThemeWithPhases_LayersMainTrackByPhase()
    {
        UseMainTrackPhases();
        Eval(14f);

        var r = Eval(15f, mainBoss: true, phase: 0);
        Assert.AreSame(main, r.track, "ไม่สลับเพลง");
        AssertLevels(1f, 1f, 1f, 0f);

        r = Eval(16f, mainBoss: true, phase: 1);
        AssertLevels(1f, 1f, 1f, 1f);
        Assert.AreEqual(0.5f, r.fade, "ใช้ fade ของเฟส");
    }

    [Test]
    public void MainBoss_NoThemeWithPhases_IgnoresMiniBossAndAppliesCardPick()
    {
        UseMainTrackPhases();
        var r = Eval(15f, mainBoss: true, miniBoss: true, cardPick: true);
        Assert.IsFalse(r.overlay);
        AssertLevels(1f, 0f, 1f, 0f);   // drums ถูกปิดตอนเลือกการ์ด · perc ของมินิบอสไม่มา
    }

    // ── บอสใหญ่ ไม่มีธีม ─────────────────────────────────────────────────

    [Test]
    public void MainBoss_NoTheme_HoldsLastMixBeforeBoss()
    {
        Eval(14f, miniBoss: true);   // ก่อนบอสออก: pad drums bass(0.8) + perc จากมินิบอส

        var r = Eval(15f, mainBoss: true, sentOverlay: true);
        Assert.AreSame(main, r.track);
        AssertLevels(1f, 1f, 0.8f, 1f);

        Eval(40f, mainBoss: true);   // เวลาเดินต่อ mix ต้องไม่ขยับ
        AssertLevels(1f, 1f, 0.8f, 1f);
    }

    [Test]
    public void MainBoss_NoTheme_JoinedMidFight_UsesCurrentBand()
    {
        // ไม่เคยคำนวณก่อนบอส (เข้ากลางไฟต์) → ใช้ช่วงเวลาตอนนี้ ไม่ใช่เงียบ
        Eval(15f, mainBoss: true);
        AssertLevels(1f, 1f, 0.8f, 0f);
    }

    // ── ชื่อ stem ผิด ────────────────────────────────────────────────────

    [Test]
    public void MissingStemName_IsSkippedAndWarnsOnce()
    {
        profile.timeBands = new[] { Band(0f, L("pad", 1f), L("choir", 1f)) };

        LogAssert.Expect(LogType.Warning, new Regex("choir"));
        Eval(1f);
        AssertLevels(1f, 0f, 0f, 0f);

        Eval(2f);   // รอบสองต้องไม่เตือนซ้ำ
        LogAssert.NoUnexpectedReceived();
    }
}
