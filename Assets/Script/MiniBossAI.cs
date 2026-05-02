using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mini-Boss AI — driven by MiniBossConfig ScriptableObject
///
/// Mechanic: Circle ↔ Line สลับ (Phase 1 ของ MainBoss)
///   tick 0,2,4… → Circle AoE ที่ตำแหน่งบอส
///   tick 1,3,5… → Line AoE หันไปทาง player ที่ใกล้ที่สุด
///
/// Prefab ต้องการ: NetworkObject + Enemy.cs + MiniBossAI.cs
/// </summary>
[RequireComponent(typeof(Enemy))]
public class MiniBossAI : NetworkBehaviour
{
    [Header("Config")]
    [Tooltip("ScriptableObject กำหนด attack parameters (สร้างจาก Game/MiniBossConfig)")]
    public MiniBossConfig config;

    [Header("Prefabs")]
    [Tooltip("TelegraphZone prefab (NetworkObject + TelegraphZone.cs)")]
    public GameObject telegraphZonePrefab;

    // ── Internal ──────────────────────────────────────────────────────────
    private Enemy enemy;
    private int   attackIndex = 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (config == null)
        {
            Debug.LogWarning("[MiniBossAI] config not assigned — no attacks!");
            return;
        }

        enemy = GetComponent<Enemy>();
        StartCoroutine(AttackLoop());
        Debug.Log("[MiniBossAI] Spawned — Circle ↔ Line");
    }

    // ── Attack Loop (Server only) ─────────────────────────────────────────
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(config.firstAttackDelay);

        while (true)
        {
            if (!NetworkObject.IsSpawned) yield break;

            // สลับ Circle ↔ Line
            if (attackIndex % 2 == 0) SpawnCircleAoE();
            else                      SpawnLineAoE();
            attackIndex++;

            yield return new WaitForSeconds(config.attackInterval);
        }
    }

    // ── Spawn AoE ─────────────────────────────────────────────────────────
    void SpawnCircleAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = AoEType.Circle;
        zone.radius          = config.circleRadius;
        zone.warningDuration = config.circleWarnTime;
        zone.damage          = config.circleDamage;

        zone.GetComponent<NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MiniBossAI] 🔴 Circle AoE spawned");
    }

    void SpawnLineAoE()
    {
        Transform target = FindNearestPlayer();
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dir.y = 0f;
        Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

        var zone = SpawnZone(transform.position, rot);
        if (zone == null) return;

        zone.aoeType         = AoEType.Line;
        zone.lineLength      = config.lineLength;
        zone.lineWidth       = config.lineWidth;
        zone.warningDuration = config.lineWarnTime;
        zone.damage          = config.lineDamage;

        zone.GetComponent<NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MiniBossAI] ▬ Line AoE spawned");
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    TelegraphZone SpawnZone(Vector3 pos, Quaternion rot)
    {
        if (telegraphZonePrefab == null)
        {
            Debug.LogWarning("[MiniBossAI] telegraphZonePrefab not assigned!");
            return null;
        }

        var go   = Instantiate(telegraphZonePrefab, pos, rot);
        var zone = go.GetComponent<TelegraphZone>();
        var no   = go.GetComponent<NetworkObject>();

        if (zone == null || no == null) { Destroy(go); return null; }
        return zone;
    }

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
