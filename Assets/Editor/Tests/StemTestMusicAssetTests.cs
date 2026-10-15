using NUnit.Framework;
using UnityEditor;

/// <summary>
/// เพลงทดสอบ Music_StemTest (scripts/gen_stem_test_audio.py) ยังใช้ได้หลัง Unity import
///
/// P3RSmokeTest ตรวจแค่เพลงที่แมพอ้าง · ตัวนี้ไม่มีแมพไหนอ้าง (DevTools สลับให้กลางรัน)
/// จึงต้องตรวจแยก — โดยเฉพาะว่าหลัง import เป็น Vorbis แล้วทุก stem ยังยาวเท่ากันระดับ sample
/// </summary>
public class StemTestMusicAssetTests
{
    const string ProfilePath = "Assets/ScriptableObjects/Audio/Music_StemTest.asset";

    [Test]
    public void StemTestProfile_TracksAreValidAndEveryMixNameExists()
    {
        var p = AssetDatabase.LoadAssetAtPath<MusicProfile>(ProfilePath);
        Assert.IsNotNull(p, ProfilePath);
        Assert.IsNotNull(p.track, "track หลัก");
        Assert.IsNotNull(p.mainBossTrack, "ธีมบอส");

        Assert.IsNull(p.track.Validate(),         $"track หลัก: {p.track.Validate()}");
        Assert.IsNull(p.mainBossTrack.Validate(), $"ธีมบอส: {p.mainBossTrack.Validate()}");

        foreach (var band in p.timeBands)
            foreach (var l in band.mix.levels)
                Assert.GreaterOrEqual(p.track.IndexOf(l.stem), 0, $"timeBands '{l.stem}'");
        foreach (var l in p.miniBossOverlay.levels)
            Assert.GreaterOrEqual(p.track.IndexOf(l.stem), 0, $"miniBossOverlay '{l.stem}'");
        foreach (var phase in p.mainBossPhases)
            foreach (var l in phase.levels)
                Assert.GreaterOrEqual(p.mainBossTrack.IndexOf(l.stem), 0, $"mainBossPhases '{l.stem}'");
        foreach (var l in p.cardPickOverrides)
            Assert.IsTrue(p.track.IndexOf(l.stem) >= 0 || p.mainBossTrack.IndexOf(l.stem) >= 0,
                          $"cardPickOverrides '{l.stem}'");

        Assert.IsNotNull(p.winStinger,  "stinger ชนะ");
        Assert.IsNotNull(p.loseStinger, "stinger แพ้");
    }
}
