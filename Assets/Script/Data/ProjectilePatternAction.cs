using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// บอสยิงกระสุนเป็นลวดลาย (bullet-hell) — กระสุนจริงที่บินได้ ไม่ใช่วงเตือน
///
/// <b>ลวดลาย</b>
/// • Ring   — รอบตัว 360° เท่าๆ กัน · ตั้ง spinPerVolley ให้แต่ละชุดหมุนต่อ = เกลียว (spiral)
/// • Fan    — พัดกางตาม spreadAngle หันไปทางเป้า
/// • Aimed  — ยิงตรงใส่เป้า (Volley หลายนัดต่อกัน = ปืนกล)
/// • Random — กระจายสุ่มในมุม spreadAngle
///
/// <b>เป้า</b> (ทิศกลางของ Fan / Aimed / Random และมุมเริ่มของ Ring)
/// ผู้เล่นใกล้สุด / สุ่มผู้เล่น / ทุกคน (ยิงลวดลายเดียวกันใส่ทีละคน) / มุมคงที่
///
/// roll หมุน/พลิก (SnapAngle / MirrorX / MirrorZ) หมุน "มุมคงที่" ได้ — พลิกลวดลายต่อไฟต์
/// ดาเมจคูณตามระดับความยาก (bossDamageMult) เหมือนท่าอื่น
///
/// กระสุนลงทะเบียนกับบอส — บอสตายหรือเปลี่ยนเฟส กระสุนที่ยังบินอยู่หายไปด้วย
/// </summary>
[CreateAssetMenu(fileName = "ProjectilePatternAction", menuName = "Boss/Actions/ProjectilePatternAction")]
public class ProjectilePatternAction : BossAction
{
    public enum Pattern { Ring, Fan, Aimed, Random }
    public enum Aim { NearestPlayer, RandomPlayer, AllPlayers, FixedAngle }
    public enum Origin { Boss, ArenaCenter }

    [Header("กระสุน")]
    [Tooltip("ว่าง = ใช้ projectilePrefab บน BossController\nต้องมี EnemyProjectile + NetworkObject และอยู่ใน DefaultNetworkPrefabs")]
    public GameObject projectilePrefab;
    [Min(0.1f)] public float speed = 8f;
    public float damage = 15f;
    [Tooltip("บินได้ไกลสุด (เมตร) แล้วหายไป")]
    [Min(0.5f)] public float maxRange = 25f;

    [Header("ลวดลาย")]
    public Pattern pattern = Pattern.Ring;
    [Tooltip("กระสุนต่อชุด")]
    [Min(1)] public int count = 12;
    [Tooltip("Fan / Random: มุมกางทั้งหมด (องศา)")]
    [Range(0f, 360f)] public float spreadAngle = 60f;

    [Header("เป้า")]
    public Aim aim = Aim.NearestPlayer;
    [Tooltip("Aim = FixedAngle: 0 = เหนือ (+Z) · หมุนตามเข็ม")]
    public float angleDegrees = 0f;
    public Origin origin = Origin.Boss;
    [Tooltip("ยิงออกจากจุดที่เลื่อนจาก origin เท่านี้")]
    public Vector3 originOffset = Vector3.zero;

    [Header("ยิงหลายชุด")]
    [Min(1)] public int volleys = 1;
    [Min(0f)] public float volleyInterval = 0.4f;
    [Tooltip("แต่ละชุดหมุนต่อจากชุดก่อนกี่องศา — Ring + ค่านี้ = เกลียว")]
    public float spinPerVolley = 0f;
    [Tooltip("หาเป้าใหม่ทุกชุด (ไล่ตามคนที่วิ่งหนี) · ปิด = ล็อกทิศตั้งแต่ชุดแรก")]
    public bool reaimEachVolley = true;

    protected override bool UsesArenaPivot => false;

    public override float GetEditorDuration()
        => actionDelay + Mathf.Max(0, volleys - 1) * volleyInterval + maxRange / Mathf.Max(0.1f, speed);

    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);
        if (runner == null) yield break;

        var boss   = runner as BossController;
        var prefab = projectilePrefab != null ? projectilePrefab : boss?.projectilePrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[ProjectilePatternAction] {name}: ไม่มี projectilePrefab ทั้งบนท่าและบน BossController");
            yield break;
        }

        int gen = RunGenerationOf(runner);
        float dmg = damage * TuningOf(runner).bossDamageMult;

        List<float> lockedAims = null;
        for (int v = 0; v < Mathf.Max(1, volleys); v++)
        {
            if (!RunStillValid(runner, gen)) yield break;

            var world = AoEWorld.FromRunner(runner);
            Vector3 from = OriginOf(world, runner);

            if (lockedAims == null || reaimEachVolley)
                lockedAims = AimAngles(world, from);

            float spin = spinPerVolley * v;
            foreach (float baseAngle in lockedAims)
                foreach (float a in PatternAngles(baseAngle + spin))
                    Fire(runner, boss, prefab, from, a, dmg);

            if (v < volleys - 1 && volleyInterval > 0f)
                yield return new WaitForSeconds(volleyInterval);
        }

        // คืนเมื่อกระสุนชุดสุดท้ายบินสุดระยะ — ดูสัญญาใน BossAction.ExecuteCoroutine
        yield return new WaitForSeconds(maxRange / Mathf.Max(0.1f, speed));
    }

    Vector3 OriginOf(in AoEWorld world, NetworkBehaviour runner)
    {
        Vector3 p = origin == Origin.ArenaCenter && world.arena != null
            ? world.arena.center
            : world.bossPos;
        p.y = world.bossPos.y;
        return p + originOffset;
    }

    /// <summary>มุมกลาง (องศารอบแกน Y · 0 = +Z) ของแต่ละลวดลาย — AllPlayers ได้หนึ่งมุมต่อคน</summary>
    List<float> AimAngles(in AoEWorld world, Vector3 from)
    {
        var list = new List<float>();
        switch (aim)
        {
            case Aim.FixedAngle:
            {
                var tf = GetRollTransform(world);
                var rot = tf.Apply(Quaternion.Euler(0f, angleDegrees, 0f));
                list.Add(rot.eulerAngles.y);
                break;
            }
            case Aim.AllPlayers:
                if (world.alivePlayers != null)
                    foreach (var p in world.alivePlayers) list.Add(AngleTo(from, p));
                break;
            case Aim.RandomPlayer:
                if (world.PlayerCount > 0)
                    list.Add(AngleTo(from, world.alivePlayers[UnityEngine.Random.Range(0, world.PlayerCount)]));
                break;
            default:
                if (world.TryNearestPlayer(from, out var near)) list.Add(AngleTo(from, near));
                break;
        }
        if (list.Count == 0) list.Add(angleDegrees);   // ไม่มีผู้เล่นเหลือ — ยิงตามมุมที่ตั้ง
        return list;
    }

    static float AngleTo(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    IEnumerable<float> PatternAngles(float center)
    {
        int n = Mathf.Max(1, count);
        switch (pattern)
        {
            case Pattern.Ring:
                for (int i = 0; i < n; i++) yield return center + 360f / n * i;
                break;
            case Pattern.Fan:
                if (n == 1) { yield return center; break; }
                for (int i = 0; i < n; i++) yield return center - spreadAngle * 0.5f + spreadAngle * i / (n - 1);
                break;
            case Pattern.Random:
                for (int i = 0; i < n; i++) yield return center + UnityEngine.Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f);
                break;
            default:   // Aimed — ทุกนัดในชุดยิงทิศเดียวกัน (count > 1 = กระสุนซ้อน · ใช้ volleys ทำปืนกล)
                for (int i = 0; i < n; i++) yield return center;
                break;
        }
    }

    void Fire(NetworkBehaviour runner, BossController boss, GameObject prefab, Vector3 from, float angle, float dmg)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        var go   = Instantiate(prefab, from, Quaternion.LookRotation(dir));
        var proj = go.GetComponent<EnemyProjectile>();
        var no   = go.GetComponent<NetworkObject>();
        if (proj == null || no == null)
        {
            Debug.LogWarning($"[ProjectilePatternAction] {name}: '{prefab.name}' ไม่มี EnemyProjectile หรือ NetworkObject");
            Destroy(go);
            return;
        }

        no.Spawn(true);
        proj.Init(dir, speed, dmg, maxRange);
        boss?.RegisterMechanic(no);
    }
}
