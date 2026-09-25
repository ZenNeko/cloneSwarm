using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base class for all modular boss attacks (FFXIV / Rabbit & Steel style)
/// </summary>
public abstract class BossAction : ScriptableObject
{
    [Header("Modular Action Settings")]
    [Tooltip("วินาทีที่หน่วงก่อนจะเริ่มทำท่านี้")]
    public float actionDelay = 0f;
    [Header("Cast bar")]
    [Tooltip("ชื่อท่าที่โชว์บน cast bar — ว่าง = ไม่โชว์ cast bar")]
    public string castName = "";
    [Tooltip("วินาทีที่ cast bar วิ่ง — 0 = ไม่โชว์")]
    public float castTime = 0f;
    [Header("Roll")]
    [Tooltip("ชื่อ roll ที่ action นี้อ่าน — ว่าง = ไม่ใช้ roll\n" +
             "ชื่อมาจาก Roll Definitions ใน BossEncounterConfig")]
    [RollId] public string rollName = "";
    [Tooltip("วินาทีที่เป็น Cooldown ล็อคการกระทำถัดไปหลังทำท่านี้เสร็จ")]
    public float cooldownAfter = 0f;

    /// <summary>
    /// ทำการรัน Action แบบ Coroutine บน Server
    ///
    /// ═══ สัญญา: คืนค่าเมื่อ "กลไกจบ" ไม่ใช่เมื่อ "ยิงออกไปแล้ว" ═══
    ///
    /// AoE ต้องคืนหลังวงระเบิด · tether ต้องคืนหลังสายหมดอายุ — ไม่ใช่คืนทันทีที่ spawn
    ///
    /// เพราะ <see cref="BossTimelineAction"/> ใช้ "คลิปทุกตัวจบแล้วหรือยัง" เป็นตัวตัดสินว่า
    /// timeline จบเมื่อไหร่ ซึ่งเป็นตัวกำหนดจังหวะวนรอบของทั้งเฟส · ท่าที่คืนเร็วกว่ากลไกจริง
    /// จะทำให้บอสขึ้นรอบใหม่ทับท่าที่ยังค้างอยู่
    ///
    /// ของเดิมใช้ <see cref="GetEditorDuration"/> มารอแทน ซึ่งเป็นค่าประมาณสำหรับวาดภาพ
    /// (ค่าฐานคือ +1f ที่เดาเอา) — จังหวะจริงของบอสจึงขึ้นกับตัวเลขที่ไม่มีใครตั้งใจให้แม่น
    /// </summary>
    /// <param name="runner">ตัวเรียกใช้งาน (BossController บน prefab บอส)</param>
    /// <param name="telegraphPrefab">TelegraphZone prefab</param>
    public abstract IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab);

    // ── รอบการรัน ─────────────────────────────────────────────────────────
    /// <summary>เลขรอบปัจจุบันของบอส — จดไว้ตอนท่าเริ่ม แล้วส่งให้ <see cref="RunStillValid"/></summary>
    protected static int RunGenerationOf(NetworkBehaviour runner)
        => (runner as BossController)?.RunGeneration ?? 0;

    /// <summary>
    /// รอบที่จดไว้ยังเป็นรอบปัจจุบันอยู่ไหม — false เมื่อบอสเปลี่ยนเฟส ตาย หรือ despawn
    ///
    /// ท่าที่ปล่อย coroutine ลูกแบบ fire-and-forget ต้องเช็คตรงนี้ก่อนลงมือทุกครั้ง
    /// ไม่งั้นท่าของเฟสเก่าจะไปโผล่กลางเฟสใหม่ (ดูคำอธิบายเต็มใน BossController._runGen)
    /// </summary>
    protected static bool RunStillValid(NetworkBehaviour runner, int gen)
    {
        if (runner == null || !runner.NetworkObject.IsSpawned) return false;
        var boss = runner as BossController;
        return boss == null || boss.IsRunCurrent(gen);
    }

    // ── Editor Support (Boss Designer) ────────────────────────────────────
    /// <summary>กันการเรียกซ้อนไม่รู้จบ กรณี action อ้างอิงกันเป็นวงกลม (Combo/Timeline ซ้อนตัวเอง)</summary>
    protected static int s_editorDurationDepth;

    /// <summary>
    /// ประมาณความยาวรวมของท่านี้เป็นวินาที (รวม actionDelay และ castTime) — ใช้วาดความยาวคลิปใน Boss Designer
    ///
    /// **ไม่มีผลต่อ gameplay** — ประโยคนี้เคยไม่จริง: BossTimelineAction เอาค่านี้ไปรอเป็นจังหวะ
    /// ของเฟส ทำให้ตัวเลขที่ตั้งใจให้ "พอเห็นภาพ" กลายเป็นตัวกำหนดความเร็วบอสจริงๆ
    /// ตอนนี้ timeline รอ coroutine ของคลิปจบเอง ค่านี้จึงกลับมาเป็นค่าวาดภาพล้วนตามที่เขียนไว้
    ///
    /// หน้าที่ที่เหลืออยู่จึงคือ **วาดให้ตรงกับของจริง** — คลิปที่วาดสั้นกว่ากลไกจริง
    /// ทำให้คนออกแบบวางท่าถัดไปทับของเดิมโดยไม่รู้ตัว
    /// </summary>
    public virtual float GetEditorDuration() => actionDelay + castTime + 1f;

    // หาผู้เล่นใกล้สุดย้ายไปอยู่ที่ AoEWorld.TryNearestPlayer แล้ว — เก็บไว้ที่นี่ด้วย
    // จะกลายเป็นนิยามที่สองที่ editor เรียกไม่ได้ และเพี้ยนจากกันได้เมื่อมีคนแก้ข้างเดียว

    /// <summary>
    /// อ่านค่า roll ที่ resolve แล้วของ fight นี้ — คืน -1 ถ้าไม่มี roll หรือหาไม่เจอ
    /// </summary>
    /// <summary>ตัวคูณของระดับความยากที่บอสตัวนี้ใช้ · ไม่ได้รันผ่าน BossController = ค่ากลาง</summary>
    protected static DifficultyProfile TuningOf(NetworkBehaviour runner)
        => (runner as BossController)?.Tuning ?? DifficultyProfile.For(DifficultyTier.Normal);

    /// <summary>ระดับความยากของบอสตัวนี้ — คลิปที่ตั้งช่วงระดับไว้ดูค่านี้</summary>
    protected static DifficultyTier TierOf(NetworkBehaviour runner)
        => (runner as BossController)?.Tier ?? RunSetup.Difficulty;

    protected int GetRoll(NetworkBehaviour runner)
    {
        if (string.IsNullOrEmpty(rollName) || runner == null) return -1;
        var boss = runner as BossController;
        if (boss != null && boss.Rolls != null)
        {
            return boss.Rolls.Peek(rollName);
        }
        return -1;
    }

    /// <summary>
    /// จุดหมุน/จุดพลิกของ roll เชิงพื้นที่ = จุดศูนย์กลางสนาม
    /// ไม่มี arena ให้ถอยไปใช้ตำแหน่งบอสพร้อม warning — ปล่อยเงียบไม่ได้ เพราะแพตเทิร์นจะเพี้ยน
    /// แบบหาสาเหตุยาก (เหตุผลเดียวกับ ColorMatchAoEAction.ResolveSharedCenter)
    /// </summary>
    protected Vector3 ResolveArenaPivot(in AoEWorld world)
    {
        if (world.arena != null) return world.arena.center;

        Debug.LogWarning($"[{GetType().Name}] {name}: ใช้ roll เชิงพื้นที่แต่ BossEncounterConfig.arena ว่าง — ใช้ตำแหน่งบอสเป็นจุดหมุนแทน");
        return world.bossPos;
    }

    /// <summary>
    /// แปลงค่า roll ที่ resolve แล้วเป็นการพลิก/หมุนพิกัด · คืน Identity ถ้าไม่ใช่ roll เชิงพื้นที่
    /// `Anchor` ไม่อยู่ในนี้เพราะมันคือการ **เลือกจุดเกิด** ไม่ใช่การแปลง — จัดการที่ targeting
    /// </summary>
    protected RollTransform GetRollTransform(in AoEWorld world)
    {
        var rolls = world.rolls;
        if (rolls == null || string.IsNullOrEmpty(rollName)) return RollTransform.Identity;

        int value = rolls.Peek(rollName);
        if (value < 0) return RollTransform.Identity;

        var t = RollTransform.Identity;
        switch (rolls.GetKind(rollName))
        {
            case RollKind.SnapAngle:
                t.angleDeg = 360f / Mathf.Max(1, rolls.GetOptionCount(rollName)) * value;
                break;
            case RollKind.MirrorX:
                t.mirrorX = value != 0;
                break;
            case RollKind.MirrorZ:
                t.mirrorZ = value != 0;
                break;
            default:
                return RollTransform.Identity;   // Variant / Target / Anchor / Order — ไม่ใช่การแปลงพิกัด
        }

        t.pivot = ResolveArenaPivot(world);
        return t;
    }

    /// <summary>
    /// roll ให้ action ลูก **ครั้งเดียวต่อ rollName** ตอนที่ชุดท่าเริ่ม
    ///
    /// `BossController.AttackLoop` roll ให้เฉพาะ action ระดับบนสุด · คลิปใน Timeline/Combo
    /// รันผ่าน StartCoroutine ตรงๆ จึงไม่เคยถูก roll เลย · และต้อง roll **ครั้งเดียว** ไม่ใช่ต่อคลิป
    /// ไม่งั้นคลิปที่ใช้ rollName เดียวกันจะได้คนละค่า แพตเทิร์นแตกเป็นคนละทิศ
    /// </summary>
    protected void RollForSubActions(NetworkBehaviour runner, IEnumerable<BossAction> subActions)
    {
        var boss = runner as BossController;
        if (boss?.Rolls == null || subActions == null) return;

        var rolled = new HashSet<string>();
        foreach (var a in subActions)
        {
            if (a == null || a == this || string.IsNullOrEmpty(a.rollName)) continue;
            if (rolled.Add(a.rollName)) boss.Rolls.Roll(a.rollName);
        }
    }
}
