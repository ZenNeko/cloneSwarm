using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// บังคับผู้เล่นเดินไปทิศที่กำหนด (Forced March แบบ FFXIV)
///
/// ลำดับ: ลูกศรโผล่ใต้ตัวผู้เล่นทุกคน (ทุกคนเห็นของทุกคน) → ช่วงอ่าน <see cref="readTime"/>
/// ให้จัดตำแหน่งตัวเองก่อน → โดนบังคับเดินตามลูกศร <see cref="marchDuration"/> วินาที บังคับทิศไม่ได้
///
/// ตัวท่าไม่มีดาเมจเอง — ใช้คู่กับท่าวงใน timeline เดียวกัน (เดินตกขอบวง / เดินหนีออกจากวงที่ตั้งไว้)
///
/// การขยับผู้เล่นเป็น owner-authoritative จึงสั่งผ่าน ClientRpc ทางเดียวกับ knockback (ดู CLAUDE.md)
/// </summary>
[CreateAssetMenu(fileName = "ForcedMarchAction", menuName = "Boss/Actions/ForcedMarchAction")]
public class ForcedMarchAction : BossAction
{
    public enum Direction
    {
        /// <summary>ทุกคนทิศเดียวกัน ตาม angleDegrees (roll หมุน/พลิกได้)</summary>
        FixedAngle,
        /// <summary>เดินหนีออกจากบอส</summary>
        AwayFromBoss,
        /// <summary>เดินเข้าหาบอส</summary>
        TowardBoss,
        /// <summary>แต่ละคนได้ทิศสุ่มของตัวเอง จาก randomDirections ทิศ</summary>
        RandomEach,
    }

    [Header("ทิศ")]
    public Direction direction = Direction.RandomEach;
    [Tooltip("FixedAngle: 0 = เหนือ (+Z) · หมุนตามเข็ม")]
    public float angleDegrees = 0f;
    [Tooltip("RandomEach: สุ่มจากกี่ทิศ — 4 = บน/ล่าง/ซ้าย/ขวา · 8 = รวมแนวทแยง")]
    [Range(2, 8)] public int randomDirections = 4;

    [Header("เวลา")]
    [Tooltip("วินาทีที่ลูกศรโชว์ก่อนเริ่มเดิน — ช่วงให้ผู้เล่นจัดตำแหน่ง (คูณตามระดับความยากเหมือนเวลาเตือน)")]
    [Min(0f)] public float readTime = 3f;
    [Tooltip("วินาทีที่โดนบังคับเดิน")]
    [Min(0.1f)] public float marchDuration = 2f;
    [Tooltip("ความเร็วเดิน (เมตร/วินาที) · 0 = ความเร็วเดินปกติของผู้เล่นแต่ละคน")]
    [Min(0f)] public float marchSpeed = 0f;

    protected override bool UsesArenaPivot => false;

    public override float GetEditorDuration() => actionDelay + readTime + marchDuration;

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);
        if (runner == null || NetworkManager.Singleton == null) yield break;

        int gen = RunGenerationOf(runner);
        var world = AoEWorld.FromRunner(runner);
        Vector3 bossPos = runner.transform.position;

        var targets = new List<(playermove pm, Vector3 dir)>();
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = c.PlayerObject != null ? c.PlayerObject.GetComponent<playermove>() : null;
            if (pm == null || pm.isDead.Value) continue;
            targets.Add((pm, DirectionFor(world, pm.transform.position, bossPos)));
        }
        if (targets.Count == 0) yield break;

        float read = readTime * TuningOf(runner).bossWarningMult;
        foreach (var (pm, dir) in targets)
            pm.ShowForcedMarchMarkerClientRpc(dir, read + marchDuration);

        float t = 0f;
        while (t < read)
        {
            if (!RunStillValid(runner, gen)) { Cancel(targets); yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        foreach (var (pm, dir) in targets)
            if (pm != null && !pm.isDead.Value)
                pm.ForcedMarchClientRpc(dir, marchSpeed, marchDuration);

        t = 0f;
        while (t < marchDuration)
        {
            // บอสตาย/เปลี่ยนเฟสกลางทาง — ปล่อยผู้เล่นทันที ไม่ให้เดินต่อไปตกอะไรที่ไม่มีแล้ว
            if (!RunStillValid(runner, gen)) { Cancel(targets); yield break; }
            t += Time.deltaTime;
            yield return null;
        }
    }

    static void Cancel(List<(playermove pm, Vector3 dir)> targets)
    {
        foreach (var (pm, dir) in targets)
        {
            if (pm == null || !pm.IsSpawned) continue;
            pm.ShowForcedMarchMarkerClientRpc(dir, 0f);
            pm.ForcedMarchClientRpc(dir, 0f, 0f);
        }
    }

    Vector3 DirectionFor(in AoEWorld world, Vector3 playerPos, Vector3 bossPos)
    {
        Vector3 d;
        switch (direction)
        {
            case Direction.AwayFromBoss: d = playerPos - bossPos; break;
            case Direction.TowardBoss:   d = bossPos - playerPos; break;
            case Direction.RandomEach:
            {
                int n = Mathf.Clamp(randomDirections, 2, 8);
                float a = angleDegrees + 360f / n * Random.Range(0, n);
                d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                break;
            }
            default:
            {
                var rot = GetRollTransform(world).Apply(Quaternion.Euler(0f, angleDegrees, 0f));
                d = rot * Vector3.forward;
                break;
            }
        }

        d.y = 0f;
        // ยืนทับบอสพอดี (Away/Toward) — ไม่มีทิศ ใช้มุมที่ตั้งแทน
        if (d.sqrMagnitude < 0.0001f) d = Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
        return d.normalized;
    }
}
