using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// roll เชิงพื้นที่กับจุดเกิด + การหยิบของท่าสุ่ม
///
/// บั๊กที่กันไว้: กากบาทที่ยึดตัวบอส + roll หมุน 45° ไปโผล่ห่างจากตัวบอส เพราะเดิมหมุนทุกจุดเกิด
/// รอบกลางสนาม · พรีวิวใน editor วางบอสไว้กลางสนามพอดีจึงไม่เคยเห็น — test นี้วางบอสนอกกลาง
/// </summary>
public class BossRollPlacementTests
{
    readonly List<Object> _made = new();

    T Make<T>() where T : ScriptableObject
    {
        var o = ScriptableObject.CreateInstance<T>();
        _made.Add(o);
        return o;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
        _made.Clear();
    }

    /// <summary>roll จนได้ค่าที่ไม่ใช่ 0 (หมุนจริง) — seed ตายตัว ผลคงที่</summary>
    static RollContext RolledNonZero(string name, RollKind kind, int options)
    {
        var ctx = new RollContext(1234, new[] { new RollDefinition { rollName = name, kind = kind, optionCount = options } });
        for (int i = 0; i < 64 && ctx.Roll(name) == 0; i++) { }
        Assume.That(ctx.Peek(name), Is.GreaterThan(0));
        return ctx;
    }

    AoEWorld WorldAt(Vector3 boss, RollContext rolls) => new AoEWorld
    {
        bossPos      = boss,
        alivePlayers = new List<Vector3>(),
        arena        = Make<ArenaDefinition>(),   // กลางสนาม = (0,0,0) — บอสไม่ได้ยืนตรงนั้น
        rolls        = rolls,
    };

    [Test]
    public void BossAnchoredCross_WithSnapAngleRoll_StaysOnBoss()
    {
        var cross = Make<CrossAoEAction>();
        cross.targetingMode = SpawnAoEActionBase.TargetingMode.BossPosition;
        cross.rollName = "r";
        var boss = new Vector3(20f, 0f, 10f);

        var wave = cross.ResolveWave(WorldAt(boss, RolledNonZero("r", RollKind.SnapAngle, 8)));

        Assert.AreEqual(1, wave.Count);
        Assert.That(Vector3.Distance(wave[0].pos, boss), Is.LessThan(0.001f), "กากบาทต้องอยู่ที่ตัวบอส");
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(wave[0].rot.eulerAngles.y, 0f)), Is.GreaterThan(1f), "แต่ต้องหมุนตาม roll");
    }

    [Test]
    public void BossAnchoredOffset_RotatesAroundBoss()
    {
        var circle = Make<CircleAoEAction>();
        circle.targetingMode = SpawnAoEActionBase.TargetingMode.BossPosition;
        circle.targetOffset = new Vector3(0f, 0f, 5f);
        circle.rollName = "r";
        var boss = new Vector3(20f, 0f, 10f);

        var wave = circle.ResolveWave(WorldAt(boss, RolledNonZero("r", RollKind.SnapAngle, 4)));

        Assert.That(Vector3.Distance(wave[0].pos, boss), Is.EqualTo(5f).Within(0.001f), "ระยะจากบอสคงเดิม");
    }

    [Test]
    public void ArenaAnchored_StillRotatesAroundArenaCenter()
    {
        var circle = Make<CircleAoEAction>();
        circle.targetingMode = SpawnAoEActionBase.TargetingMode.StaticCoords;
        circle.targetOffset = new Vector3(10f, 0f, 0f);
        circle.rollName = "r";
        var world = WorldAt(new Vector3(20f, 0f, 10f), RolledNonZero("r", RollKind.MirrorX, 2));

        var wave = circle.ResolveWave(world);

        Assert.That(wave[0].pos.x, Is.EqualTo(-10f).Within(0.001f), "พิกัดสนามพลิกรอบกลางสนามเหมือนเดิม");
    }

    [Test]
    public void FixedAngleLine_UsesAngleNotNearestPlayer()
    {
        var line = Make<LineAoEAction>();
        line.aimMode = SpawnAoEActionBase.AimMode.FixedAngle;
        line.angleDegrees = 90f;
        var world = WorldAt(Vector3.zero, null);
        world.alivePlayers.Add(new Vector3(0f, 0f, 10f));   // ผู้เล่นอยู่ทางเหนือ

        var wave = line.ResolveWave(world);

        Assert.That(Mathf.DeltaAngle(wave[0].rot.eulerAngles.y, 90f), Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void RandomPool_Alternate_CyclesFromRolledSlot()
    {
        var a = Make<CircleAoEAction>();
        var b = Make<DonutAoEAction>();
        var pool = Make<RandomAttackAction>();
        pool.attackPool = new List<BossAction> { a, b };
        pool.repeatCount = 3;
        pool.repeatPick = RandomAttackAction.RepeatPick.Alternate;

        Assert.AreSame(b, pool.PickForPreview(1, 0));
        Assert.AreSame(a, pool.PickForPreview(1, 1));
        Assert.AreSame(b, pool.PickForPreview(1, 2));
    }

    [Test]
    public void RandomPool_SameEachTime_KeepsRolledSlot()
    {
        var a = Make<CircleAoEAction>();
        var b = Make<DonutAoEAction>();
        var pool = Make<RandomAttackAction>();
        pool.attackPool = new List<BossAction> { a, b };
        pool.repeatCount = 3;

        for (int r = 0; r < 3; r++) Assert.AreSame(b, pool.PickForPreview(1, r));
    }
}
