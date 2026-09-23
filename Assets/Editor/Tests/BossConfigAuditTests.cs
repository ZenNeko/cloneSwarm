using System.Collections.Generic;
using CloneSwarm.EditorTools;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode test ของ BossConfigAudit.CheckPhaseThresholds — เฟสที่ไปไม่ถึงเพราะตั้ง
/// transitionHealthPct ผิด (≤ 0 บนเฟสที่ไม่ใช่เฟสสุดท้าย · ไม่ลดลงจากเฟสก่อน)
///
/// อยู่ใต้ Assets/Editor/ โดยไม่มี .asmdef — คอมไพล์เข้า Assembly-CSharp-Editor เดียวกับ
/// BossConfigAudit จึงเรียกเมธอด internal ได้ตรงๆ (ดูเหตุผลเรื่อง .asmdef ที่ ShieldStackTests.cs)
/// </summary>
public class BossConfigAuditTests
{
    BossEncounterConfig cfg;

    [SetUp]
    public void SetUp() => cfg = ScriptableObject.CreateInstance<BossEncounterConfig>();

    [TearDown]
    public void TearDown()
    {
        if (cfg != null) Object.DestroyImmediate(cfg);
    }

    List<BossConfigAudit.Problem> Check(params float[] thresholds)
    {
        cfg.phases = new List<BossPhase>();
        foreach (var t in thresholds)
            cfg.phases.Add(new BossPhase { transitionHealthPct = t });

        var problems = new List<BossConfigAudit.Problem>();
        BossConfigAudit.CheckPhaseThresholds(cfg, problems);
        return problems;
    }

    [Test]
    public void ValidDescendingThresholds_NoProblems()
    {
        Assert.IsEmpty(Check(0.75f, 0.4f, 0f));
    }

    [Test]
    public void LastPhaseZero_IsFine()
    {
        // เฟสสุดท้ายไม่มีเฟสถัดไป — ค่านี้ไม่ถูกใช้ ตั้ง 0 ได้
        Assert.IsEmpty(Check(0.5f, 0f));
        Assert.IsEmpty(Check(0f), "บอสเฟสเดียว ค่า 0 ต้องผ่าน");
    }

    [Test]
    public void ZeroOnNonLastPhase_IsBlocking()
    {
        // เคสจริง: เฟสสุดท้ายตั้ง 0 แล้วกด "+ Phase" ต่อท้าย
        var problems = Check(0.6f, 0f, 0f);
        Assert.AreEqual(1, problems.Count);
        Assert.IsTrue(problems[0].blocking);
        StringAssert.Contains("Phase 2", problems[0].where);
    }

    [Test]
    public void NonDescendingThreshold_IsBlocking()
    {
        var problems = Check(0.5f, 0.6f, 0f);
        Assert.AreEqual(1, problems.Count);
        Assert.IsTrue(problems[0].blocking);
        StringAssert.Contains("Phase 2", problems[0].where);
    }

    [Test]
    public void EqualThreshold_CountsAsNonDescending()
    {
        // เท่ากับเฟสก่อน = เข้าเฟสแล้ว HP ต่ำกว่าเกณฑ์อยู่แล้ว ข้ามทันทีเหมือนกัน
        var problems = Check(0.5f, 0.5f, 0f);
        Assert.AreEqual(1, problems.Count);
        Assert.IsTrue(problems[0].blocking);
    }

    [Test]
    public void NullPhases_NoProblemsNoThrow()
    {
        cfg.phases = null;
        var problems = new List<BossConfigAudit.Problem>();
        Assert.DoesNotThrow(() => BossConfigAudit.CheckPhaseThresholds(cfg, problems));
        Assert.IsEmpty(problems);
    }
}
