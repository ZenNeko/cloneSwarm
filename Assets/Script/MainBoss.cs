using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss AI — อยู่บน Boss Prefab ร่วมกับ Enemy.cs
///
/// Phases:
///   Phase 1 (100-60% HP): CircleAoE ทุก 4s
///   Phase 2  (60-30% HP): CircleAoE + LineAoE สลับกัน ทุก 3s
///   Phase 3  (30-0%  HP): Circle + Line ทุก 2s (enrage)
///
/// TelegraphZone Prefab ต้องมี TelegraphZone.cs + NetworkObject
/// </summary>
[RequireComponent(typeof(Enemy))]
public class MainBoss : NetworkBehaviour
{
    [Header("Phase Thresholds (% HP)")]
    public float phase2Threshold = 0.60f;
    public float phase3Threshold = 0.30f;

    [Header("Attack Intervals (seconds)")]
    public float phase1Interval = 4f;
    public float phase2Interval = 3f;
    public float phase3Interval = 2f;

    [Header("Circle AoE")]
    public float circleRadius   = 4f;
    public float circleDamage   = 25f;
    public float circleWarnTime = 2.5f;

    [Header("Line AoE")]
    public float lineLength     = 10f;
    public float lineWidth      = 2f;
    public float lineDamage     = 35f;
    public float lineWarnTime   = 2.5f;

    [Header("Prefabs")]
    [Tooltip("Prefab ที่มี TelegraphZone.cs + NetworkObject")]
    public GameObject telegraphZonePrefab;

    // ── Refs ──────────────────────────────────────────────────────────────
    private Enemy enemy;
    private int   currentPhase  = 0;
    private bool  attackLoopRunning;
    private int   attackIndex   = 0;   // สลับ circle/line ใน phase 2+

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        enemy = GetComponent<Enemy>();
        if (enemy == null) { Debug.LogError("[MainBoss] Enemy component not found!"); return; }

        // Subscribe ให้รู้เมื่อ HP เปลี่ยน
        enemy.netHealth.OnValueChanged += OnHealthChanged;

        StartCoroutine(AttackLoop());
        attackLoopRunning = true;
        Debug.Log("[MainBoss] Boss spawned — Phase 1");
    }

    public override void OnNetworkDespawn()
    {
        if (enemy != null)
            enemy.netHealth.OnValueChanged -= OnHealthChanged;
    }

    // ── Phase Tracking ────────────────────────────────────────────────────
    void OnHealthChanged(float _, float hp)
    {
        if (!IsServer) return;

        float pct = enemy.maxHealth > 0 ? hp / enemy.maxHealth : 0f;
        int newPhase = pct > phase2Threshold ? 1
                     : pct > phase3Threshold ? 2
                     : 3;

        if (newPhase != currentPhase)
        {
            currentPhase = newPhase;
            PhaseChangedClientRpc(currentPhase);
            Debug.Log($"[MainBoss] Phase → {currentPhase} ({pct:P0} HP)");
        }
    }

    [ClientRpc]
    void PhaseChangedClientRpc(int phase)
    {
        Debug.Log($"[MainBoss] ⚡ Phase {phase} started!");
        // HUD สามารถ subscribe event นี้ได้ผ่าน static event ถ้าต้องการ
    }

    // ── Attack Loop (Server only) ─────────────────────────────────────────
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(3f);   // หน่วงก่อนโจมตีแรก

        while (true)
        {
            float interval = currentPhase switch
            {
                3 => phase3Interval,
                2 => phase2Interval,
                _ => phase1Interval,
            };

            yield return new WaitForSeconds(interval);

            if (!NetworkObject.IsSpawned) yield break;

            // Phase 1: Circle เท่านั้น
            // Phase 2+: สลับ Circle / Line
            bool useCircle = currentPhase == 1 || attackIndex % 2 == 0;
            attackIndex++;

            if (useCircle)
                SpawnCircleAoE();
            else
                SpawnLineAoE();
        }
    }

    // ── Spawn AoE ─────────────────────────────────────────────────────────
    void SpawnCircleAoE()
    {
        if (telegraphZonePrefab == null)
        {
            Debug.LogWarning("[MainBoss] telegraphZonePrefab not assigned!");
            return;
        }

        // วางที่ตำแหน่ง boss
        var go   = Instantiate(telegraphZonePrefab, transform.position, Quaternion.identity);
        var zone = go.GetComponent<TelegraphZone>();
        var no   = go.GetComponent<NetworkObject>();

        if (zone == null || no == null) { Destroy(go); return; }

        zone.aoeType         = TelegraphZone.AoEType.Circle;
        zone.radius          = circleRadius;
        zone.warningDuration = circleWarnTime;
        zone.damage          = circleDamage;

        no.Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🔴 Circle AoE spawned");
    }

    void SpawnLineAoE()
    {
        if (telegraphZonePrefab == null) return;

        // หาผู้เล่นที่ใกล้ที่สุดแล้วยิงไปทิศนั้น
        Transform target = FindNearestPlayer();
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dir.y = 0f;

        Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

        // วางจุดกึ่งกลางไปข้างหน้า boss
        Vector3 pos = transform.position + dir * (lineLength * 0.5f);

        var go   = Instantiate(telegraphZonePrefab, pos, rot);
        var zone = go.GetComponent<TelegraphZone>();
        var no   = go.GetComponent<NetworkObject>();

        if (zone == null || no == null) { Destroy(go); return; }

        zone.aoeType         = TelegraphZone.AoEType.Line;
        zone.lineLength      = lineLength;
        zone.lineWidth       = lineWidth;
        zone.warningDuration = lineWarnTime;
        zone.damage          = lineDamage;

        no.Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🟠 Line AoE spawned");
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null) return null;
        Transform nearest = null;
        float     minDist = float.MaxValue;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            float d = Vector3.Distance(transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj.transform; }
        }
        return nearest;
    }
}
