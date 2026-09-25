using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// หยิบท่าจากกอง · **ผูก rollName (Variant) = หยิบตามค่า roll ของไฟต์** ค่าเดียวกันได้ท่าเดียวกันเสมอ
/// ใช้ roll ร่วมกันได้ (สองกองชื่อ roll เดียวกัน = ออกช่องเดียวกัน เช่น วงไล่↔วง · โดนัทไล่↔โดนัท)
/// และพรีวิวใน Boss Designer เห็นท่าที่ถูกหยิบ · ไม่ผูก roll = สุ่มใหม่ทุกครั้งแบบเดิม
/// ช่อง = ค่า roll mod จำนวนในกอง (หยิบหลายท่า = ช่องถัดๆ ไป)
///
/// ═══ ซ้ำ ═══
/// กองยิงซ้ำเองได้ (repeatCount) และเลือกได้ว่าแต่ละนัดหยิบยังไง (<see cref="RepeatPick"/>)
/// ท่าในกองควรตั้งซ้ำ = 1 — ไม่งั้นซ้อนกันเป็น (ซ้ำของกอง × ซ้ำของท่า) นัด
/// Boss Designer มีปุ่ม "ย้ายการซ้ำมาไว้ที่ท่าสุ่ม" ทำให้
/// </summary>
[CreateAssetMenu(fileName = "RandomAttackAction", menuName = "Boss/Actions/RandomAttackAction")]
public class RandomAttackAction : BossAction
{
    public enum RepeatPick
    {
        /// <summary>หยิบครั้งเดียว ใช้ทุกนัด — วง วง วง</summary>
        SameEachTime,
        /// <summary>หยิบใหม่ทุกนัด — ผูก roll = ทอย roll ใหม่ทุกนัด (ค่าที่กองอื่นที่แชร์ roll เห็นก็เปลี่ยนตาม)</summary>
        RepickEachTime,
        /// <summary>สลับตามลำดับในกอง เริ่มจากช่องที่ roll ได้ — วง โดนัท วง · ผู้เล่นอ่านออก</summary>
        Alternate,
    }

    [Header("Pool of potential actions to pick from")]
    public List<BossAction> attackPool;

    [Header("Number of random attacks to execute simultaneously")]
    [Range(1, 5)]
    public int attackCount = 1;

    [Tooltip("วินาทีที่หน่วงเล็กน้อยระหว่างแต่ละท่าที่สุ่มได้ (เพื่อไม่ให้ปล่อยพร้อมกันสนิท)")]
    public float delayBetweenPicks = 0.4f;

    [Header("Repeat")]
    [Tooltip("ยิงซ้ำกี่นัด — 1 = ครั้งเดียวเหมือนเดิม")]
    [Min(1)] public int repeatCount = 1;
    [Tooltip("เว้นกี่วินาทีระหว่างนัด (นับจากจุดเริ่มของนัดก่อน)")]
    [Min(0f)] public float repeatInterval = 0.6f;
    [Tooltip("แต่ละนัดหยิบยังไง — ครั้งเดียวใช้ทุกนัด / หยิบใหม่ทุกนัด / สลับตามลำดับในกอง")]
    public RepeatPick repeatPick = RepeatPick.SameEachTime;

    public override float GetEditorDuration()
    {
        if (s_editorDurationDepth > 8) return actionDelay;
        s_editorDurationDepth++;
        try
        {
            float longest = 1f;
            if (attackPool != null)
                foreach (var a in attackPool)
                    if (a != null) longest = Mathf.Max(longest, a.GetEditorDuration());
            return actionDelay
                 + Mathf.Max(0, repeatCount - 1) * repeatInterval
                 + Mathf.Max(0, attackCount - 1) * delayBetweenPicks
                 + longest;
        }
        finally { s_editorDurationDepth--; }
    }

    List<BossAction> ValidPool()
    {
        var list = new List<BossAction>();
        if (attackPool != null)
            foreach (var a in attackPool) if (a != null) list.Add(a);
        return list;
    }

    /// <summary>
    /// ท่าที่นัดที่ round หยิบได้ เมื่อ roll ได้ค่า value — พรีวิวใน editor ใช้
    /// value -1 = ไม่ผูก roll (แสดงช่องแรก) · RepickEachTime ในพรีวิวเดาจาก value+round (ของจริงทอยใหม่)
    /// </summary>
    public BossAction PickForPreview(int value, int round = 0)
    {
        var valid = ValidPool();
        if (valid.Count == 0) return null;
        int v = Mathf.Max(0, value);
        return repeatPick switch
        {
            RepeatPick.Alternate      => valid[(v + round) % valid.Count],
            RepeatPick.RepickEachTime => valid[new System.Random(v * 7919 + round).Next(valid.Count)],
            _                         => valid[v % valid.Count],
        };
    }

    /// <summary>นัดที่กำลังเล่นอยู่ ณ เวลาหลังจุดเริ่มคลิป — พรีวิวใช้เลือก round</summary>
    public int RoundAt(float secondsIntoClip)
    {
        if (repeatCount <= 1 || repeatInterval <= 0f) return 0;
        return Mathf.Clamp(Mathf.FloorToInt((secondsIntoClip - actionDelay) / repeatInterval), 0, repeatCount - 1);
    }

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f)
        {
            yield return new WaitForSeconds(actionDelay);
        }

        var pool = ValidPool();
        if (pool.Count == 0) yield break;

        var boss = runner as BossController;
        int gen  = RunGenerationOf(runner);
        int rolled = GetRoll(runner);             // -1 = ไม่ผูก roll
        int start  = rolled >= 0 ? rolled : Random.Range(0, pool.Count);
        List<BossAction> first = null;

        for (int round = 0; round < Mathf.Max(1, repeatCount); round++)
        {
            if (!RunStillValid(runner, gen)) yield break;

            List<BossAction> chosen;
            switch (repeatPick)
            {
                case RepeatPick.Alternate:
                    chosen = PickRun(pool, start + round);
                    break;

                case RepeatPick.RepickEachTime:
                    if (round > 0 && rolled >= 0 && boss?.Rolls != null)
                    {
                        boss.Rolls.Roll(rollName);
                        rolled = GetRoll(runner);
                    }
                    chosen = rolled >= 0 ? PickRun(pool, rolled) : PickRandom(pool);
                    break;

                default:   // SameEachTime
                    first ??= rolled >= 0 ? PickRun(pool, rolled) : PickRandom(pool);
                    chosen = first;
                    break;
            }

            // ท่าที่หยิบได้มี roll ของตัวเอง (หมุน/พลิก) — ทอยก่อนยิง ไม่งั้น Peek ได้ค่าเก่า/ไม่มีค่า
            RollForSubActions(runner, chosen);

            for (int i = 0; i < chosen.Count; i++)
            {
                // delayBetweenPicks ทำให้ตัวท้ายๆ ยิงหลังตัวแรกหลายวินาที — พอเปลี่ยนเฟสคั่นกลาง
                // ที่เหลือจะไปโผล่ในเฟสใหม่ ถ้าไม่เช็ครอบตรงนี้
                if (!RunStillValid(runner, gen)) yield break;
                runner.StartCoroutine(chosen[i].ExecuteCoroutine(runner, telegraphPrefab));
                if (delayBetweenPicks > 0f && i < chosen.Count - 1)
                    yield return new WaitForSeconds(delayBetweenPicks);
            }

            if (round < repeatCount - 1 && repeatInterval > 0f)
                yield return new WaitForSeconds(repeatInterval);
        }
    }

    /// <summary>ช่อง from, from+1, … (วนกอง) · attackCount ตัว ไม่ซ้ำ</summary>
    List<BossAction> PickRun(List<BossAction> pool, int from)
    {
        var chosen = new List<BossAction>();
        int n = Mathf.Min(attackCount, pool.Count);
        for (int i = 0; i < n; i++) chosen.Add(pool[((from + i) % pool.Count + pool.Count) % pool.Count]);
        return chosen;
    }

    List<BossAction> PickRandom(List<BossAction> pool)
    {
        var available = new List<BossAction>(pool);
        var chosen = new List<BossAction>();
        int n = Mathf.Min(attackCount, available.Count);
        for (int i = 0; i < n; i++)
        {
            int index = Random.Range(0, available.Count);
            chosen.Add(available[index]);
            available.RemoveAt(index);   // ไม่ซ้ำในนัดเดียวกัน
        }
        return chosen;
    }
}
