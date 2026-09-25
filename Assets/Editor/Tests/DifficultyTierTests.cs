using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ระบบความยาก: ช่วงระดับบนคลิป · HP ตามจำนวนคน · ค่ากลางเมื่อไม่มีไฟล์
/// </summary>
public class DifficultyTierTests
{
    [Test]
    public void Clip_WithoutLimit_ActiveInEveryTier()
    {
        // คลิปเก่าในไฟล์ไม่มีช่องช่วงระดับ — ต้องออกทุกระดับ ไม่ใช่แค่ Easy
        var clip = new BossTimelineAction.TimelineClip();
        foreach (DifficultyTier t in System.Enum.GetValues(typeof(DifficultyTier)))
            Assert.IsTrue(clip.ActiveIn(t), t.ToString());
    }

    [Test]
    public void Clip_HardPlus_OnlyFromHard()
    {
        var clip = new BossTimelineAction.TimelineClip
            { limitTiers = true, minTier = DifficultyTier.Hard, maxTier = DifficultyTier.Epic };
        Assert.IsFalse(clip.ActiveIn(DifficultyTier.Normal));
        Assert.IsTrue (clip.ActiveIn(DifficultyTier.Hard));
        Assert.IsTrue (clip.ActiveIn(DifficultyTier.Epic));
    }

    [Test]
    public void HpForPlayers_UsesTableAndClampsBeyondFour()
    {
        var p = ScriptableObject.CreateInstance<DifficultyProfile>();
        try
        {
            p.hpByPlayers = new[] { 0.9f, 1.7f, 2.4f, 3.0f };
            Assert.AreEqual(0.9f, p.HpForPlayers(1), 1e-4);
            Assert.AreEqual(3.0f, p.HpForPlayers(4), 1e-4);
            Assert.AreEqual(3.0f, p.HpForPlayers(6), 1e-4, "เกินตารางใช้ช่องสุดท้าย");
            Assert.AreEqual(0.9f, p.HpForPlayers(0), 1e-4, "0 คน (ยังไม่มีใครต่อ) = ช่อง 1 คน");
        }
        finally { Object.DestroyImmediate(p); }
    }

    [Test]
    public void EveryTierResolves_AndNormalIsNeutral()
    {
        DifficultyProfile.ClearCache();
        foreach (DifficultyTier t in System.Enum.GetValues(typeof(DifficultyTier)))
            Assert.IsNotNull(DifficultyProfile.For(t), t.ToString());

        var n = DifficultyProfile.For(DifficultyTier.Normal);
        Assert.AreEqual(1f, n.bossHpMult);
        Assert.AreEqual(1f, n.bossDamageMult);
        Assert.AreEqual(1f, n.bossWarningMult);
    }
}
