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
    [Tooltip("ชื่อ roll ที่ action นี้อ่าน — ว่าง = ไม่ใช้ roll")]
    public string rollName = "";
    [Tooltip("วินาทีที่เป็น Cooldown ล็อคการกระทำถัดไปหลังทำท่านี้เสร็จ")]
    public float cooldownAfter = 0f;

    /// <summary>
    /// ทำการรัน Action แบบ Coroutine บน Server
    /// </summary>
    /// <param name="runner">ตัวเรียกใช้งาน (BossController บน prefab บอส)</param>
    /// <param name="telegraphPrefab">TelegraphZone prefab</param>
    public abstract IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab);

    // ── Editor Support (Boss Designer) ────────────────────────────────────
    /// <summary>กันการเรียกซ้อนไม่รู้จบ กรณี action อ้างอิงกันเป็นวงกลม (Combo/Timeline ซ้อนตัวเอง)</summary>
    protected static int s_editorDurationDepth;

    /// <summary>
    /// ประมาณความยาวรวมของท่านี้เป็นวินาที (รวม actionDelay และ castTime) — ใช้วาดความยาวคลิปใน Boss Designer
    /// ไม่มีผลต่อ gameplay
    /// </summary>
    public virtual float GetEditorDuration() => actionDelay + castTime + 1f;

    // Helper: ค้นหาผู้เล่นที่อยู่ใกล้ที่สุด
    protected Transform FindNearestPlayer(Vector3 origin)
    {
        if (NetworkManager.Singleton == null) return null;
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            var pm = obj.GetComponent<playermove>();
            if (pm != null && pm.isDead.Value) continue; // ข้ามคนตาย

            float d = Vector3.Distance(origin, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj.transform; }
        }
        return nearest;
    }

    /// <summary>
    /// อ่านค่า roll ที่ resolve แล้วของ fight นี้ — คืน -1 ถ้าไม่มี roll หรือหาไม่เจอ
    /// </summary>
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
    protected Vector3 ResolveArenaPivot(NetworkBehaviour runner)
    {
        ArenaDefinition arena = (runner as BossController)?.config?.arena;
        if (arena != null) return arena.center;

        Debug.LogWarning($"[{GetType().Name}] {name}: ใช้ roll เชิงพื้นที่แต่ BossEncounterConfig.arena ว่าง — ใช้ตำแหน่งบอสเป็นจุดหมุนแทน");
        return runner != null ? runner.transform.position : Vector3.zero;
    }

    /// <summary>
    /// แปลงค่า roll ที่ resolve แล้วเป็นการพลิก/หมุนพิกัด · คืน Identity ถ้าไม่ใช่ roll เชิงพื้นที่
    /// `Anchor` ไม่อยู่ในนี้เพราะมันคือการ **เลือกจุดเกิด** ไม่ใช่การแปลง — จัดการที่ targeting
    /// </summary>
    protected RollTransform GetRollTransform(NetworkBehaviour runner)
    {
        var boss = runner as BossController;
        if (boss?.Rolls == null || string.IsNullOrEmpty(rollName)) return RollTransform.Identity;

        int value = boss.Rolls.Peek(rollName);
        if (value < 0) return RollTransform.Identity;

        var t = RollTransform.Identity;
        switch (boss.Rolls.GetKind(rollName))
        {
            case RollKind.SnapAngle:
                t.angleDeg = 360f / Mathf.Max(1, boss.Rolls.GetOptionCount(rollName)) * value;
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

        t.pivot = ResolveArenaPivot(runner);
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
