using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// บอสพุ่งตัวหรือวาร์ปไปจุดเป้าหมาย
///
/// • <b>Dash</b> — เส้นเตือนจากตัวบอสถึงปลายทาง · บอสยืนนิ่งหันไปทางนั้นจนหมดเวลาเตือน
///   แล้วพุ่งไปตามเส้น ดาเมจลงบนทั้งเส้นตอนออกตัว
/// • <b>Teleport</b> — วงเตือนที่ปลายทาง · หมดเวลาเตือนบอสหายไปโผล่ในวง ดาเมจลงในวงตอนโผล่
///
/// ปลายทางใช้ "ที่ไหน" ชุดเดียวกับท่า AoE (ผู้เล่นใกล้สุด / สุ่มผู้เล่น / จุดในสนาม ...) และ roll
/// เชิงพื้นที่ทำงานเหมือนกัน — ถ้าได้หลายจุด (ใต้ผู้เล่นทุกคน) บอสไปจุดแรก
///
/// การเคลื่อนที่ของบอสเป็น server-authoritative อยู่แล้ว (Enemy ขยับ Rigidbody บน server ·
/// NetworkTransform ส่งให้ client) ท่านี้จึงขยับบอสบน server ตรงๆ ได้ ไม่เหมือนผู้เล่นที่ต้องผ่าน owner
/// </summary>
[CreateAssetMenu(fileName = "BossDashAction", menuName = "Boss/Actions/BossDashAction")]
public class BossDashAction : SpawnAoEActionBase
{
    public enum DashMode { Dash, Teleport }

    [Header("Dash / Teleport")]
    [Tooltip("Dash = พุ่งไปตามเส้น · Teleport = หายแล้วโผล่ที่ปลายทาง")]
    public DashMode mode = DashMode.Dash;

    [Tooltip("Dash: ความกว้างของเส้นที่โดนดาเมจ")]
    public float lineWidth = 4f;
    [Tooltip("Dash: วินาทีที่ใช้พุ่งจากต้นถึงปลาย — สั้น = ดูแรง")]
    [Min(0.05f)] public float dashDuration = 0.3f;
    [Tooltip("Dash: พุ่งเลยเป้าหมายไปอีกกี่เมตร — ยืนหลบหลังจุดเป้าหมายนิดเดียวจะไม่รอด")]
    [Min(0f)] public float overshoot = 3f;
    [Tooltip("Dash: ระยะพุ่งไกลสุด (เมตร) — 0 = ไม่จำกัด")]
    [Min(0f)] public float maxDistance = 0f;

    [Tooltip("Teleport: รัศมีวงที่โดนดาเมจตอนบอสโผล่")]
    public float radius = 5f;
    [Tooltip("Teleport: VFX ตรงจุดที่บอสหายไป (ว่าง = ไม่มี)")]
    public VFXAsset departVfx;

    [Tooltip("กันบอสหลุดขอบสนาม — ปลายทางถูกดึงเข้ามาให้ห่างขอบอย่างน้อยเท่านี้ (ต้องผูก arena ใน config)")]
    [Min(0f)] public float arenaEdgeMargin = 2f;

    // ปลายทางที่ใช้บ่อยที่สุดคือ "ผู้เล่นใกล้สุด" — ค่าเริ่มของฐาน (ที่ตัวบอส) ไม่มีความหมายกับท่าพุ่ง
    public BossDashAction() { targetingMode = TargetingMode.NearestPlayer; }

    protected override AoEType GetAoEType() => mode == DashMode.Dash ? AoEType.Line : AoEType.Circle;

    protected override void ConfigureTelegraphZone(TelegraphZone zone)
    {
        if (mode == DashMode.Dash) zone.lineWidth = lineWidth;
        else                       zone.radius    = radius;
    }

    float OneDashSeconds => warningDuration + (mode == DashMode.Dash ? dashDuration : 0f);

    public override float GetEditorDuration()
        => actionDelay + OneDashSeconds * Mathf.Max(1, repeatCount)
         + Mathf.Max(0, repeatCount - 1) * repeatInterval + 0.3f;

    /// <summary>
    /// ซ้ำ = พุ่งต่อกันหลายครั้ง แต่ละครั้งหาเป้าใหม่จากตำแหน่งที่บอสอยู่ตอนนั้น
    /// (ต่างจากท่า AoE ที่ระลอกซ้อนกันได้ — บอสมีตัวเดียว พุ่งพร้อมกันสองทางไม่ได้)
    /// </summary>
    public override IEnumerator ExecuteCoroutine(NetworkBehaviour runner, GameObject telegraphPrefab)
    {
        if (actionDelay > 0f) yield return new WaitForSeconds(actionDelay);
        if (runner == null || telegraphPrefab == null) yield break;

        int gen = RunGenerationOf(runner);
        int shots = Mathf.Max(1, repeatCount);
        for (int shot = 0; shot < shots; shot++)
        {
            if (!RunStillValid(runner, gen)) yield break;
            if (shot > 0 && rerollEachRepeat && !string.IsNullOrEmpty(rollName))
                (runner as BossController)?.Rolls?.Roll(rollName);

            yield return DashOnce(runner, telegraphPrefab, gen);

            if (shot < shots - 1 && repeatInterval > 0f)
                yield return new WaitForSeconds(repeatInterval);
        }
    }

    IEnumerator DashOnce(NetworkBehaviour runner, GameObject telegraphPrefab, int gen)
    {
        var enemy = runner.GetComponent<Enemy>();
        var rb    = runner.GetComponent<Rigidbody>();

        var world = AoEWorld.FromRunner(runner);
        var wave  = ResolveWave(world);
        Vector3 start = runner.transform.position;
        Vector3 end   = wave.Count > 0 ? wave[0].pos : start;
        end.y = start.y;

        Vector3 flat = end - start;
        Vector3 dir  = flat.sqrMagnitude > 0.0001f ? flat.normalized : runner.transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();

        if (mode == DashMode.Dash)
        {
            float dist = flat.magnitude + overshoot;
            if (maxDistance > 0f) dist = Mathf.Min(dist, maxDistance);
            end = start + dir * dist;
        }
        end = ClampInsideArena(world.arena, end, start.y);

        Quaternion face = Quaternion.LookRotation(dir);
        float travel = Vector3.Distance(start, end);

        if (enemy != null) enemy.scriptedMovement = true;
        try
        {
            SetPose(runner, rb, start, face);

            if (mode == DashMode.Dash)
            {
                // เส้นยาวเท่าระยะพุ่ง + ตัวบอส · ตำแหน่งของโซน Line คือจุดกึ่งกลาง (TelegraphGeometry.IsInLine)
                float length = travel + lineWidth;
                SpawnZoneAt(runner, telegraphPrefab, (start + end) * 0.5f, face,
                            z => z.lineLength = Mathf.Max(0.5f, length));
            }
            else
            {
                SpawnZoneAt(runner, telegraphPrefab, end, Quaternion.identity);
            }

            // บอสยืนนิ่งหันไปทางเป้าหมายตลอดช่วงเตือน — ผู้เล่นอ่านทิศจากตัวบอสได้ด้วย
            float warn = WarningFor(runner);
            float t = 0f;
            while (t < warn)
            {
                if (!RunStillValid(runner, gen)) yield break;
                SetPose(runner, rb, start, face);
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
            if (!RunStillValid(runner, gen)) yield break;

            if (mode == DashMode.Teleport)
            {
                var boss = runner as BossController;
                int vfx = ResolveDepartVfx();
                if (boss != null && vfx >= 0) boss.PlayVfxClientRpc(vfx, start, 1f);
                Teleport(runner, rb, end, face);
                yield break;
            }

            float d = Mathf.Max(0.05f, dashDuration);
            float e = 0f;
            while (e < d)
            {
                if (!RunStillValid(runner, gen)) yield break;
                e += Time.fixedDeltaTime;
                // ease-out — ออกตัวแรงแล้วเบรก อ่านง่ายกว่าความเร็วคงที่
                float k = 1f - (1f - Mathf.Clamp01(e / d)) * (1f - Mathf.Clamp01(e / d));
                SetPose(runner, rb, Vector3.Lerp(start, end, k), face);
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            if (enemy != null) enemy.scriptedMovement = false;
            if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
        }
    }

    static void SetPose(NetworkBehaviour runner, Rigidbody rb, Vector3 pos, Quaternion rot)
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.MovePosition(pos);
            rb.MoveRotation(rot);
        }
        else
        {
            runner.transform.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// วาร์ปผ่าน NetworkTransform.Teleport — ถ้าย้าย transform เฉยๆ client จะ interpolate
    /// บอสเลื่อนข้ามสนามให้เห็น แทนที่จะหายแล้วโผล่
    /// </summary>
    static void Teleport(NetworkBehaviour runner, Rigidbody rb, Vector3 pos, Quaternion rot)
    {
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = rot;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
        }

        var nt = runner.GetComponent<NetworkTransform>();
        if (nt != null) nt.Teleport(pos, rot, runner.transform.localScale);
        else            runner.transform.SetPositionAndRotation(pos, rot);
    }

    Vector3 ClampInsideArena(ArenaDefinition arena, Vector3 p, float y)
    {
        p.y = y;
        if (arena == null) return p;

        float r = Mathf.Max(0f, arena.radius - arenaEdgeMargin);
        Vector3 c = arena.center; c.y = y;
        Vector3 off = p - c;

        if (arena.shape == ArenaShape.Square)
        {
            off.x = Mathf.Clamp(off.x, -r, r);
            off.z = Mathf.Clamp(off.z, -r, r);
        }
        else if (off.magnitude > r)
        {
            off = off.normalized * r;
        }
        return c + off;
    }

    int ResolveDepartVfx()
    {
        var db = NetworkedVFXPool.Instance != null ? NetworkedVFXPool.Instance.vfxDatabase : null;
        return db != null ? db.GetIdForAsset(departVfx) : -1;
    }
}
