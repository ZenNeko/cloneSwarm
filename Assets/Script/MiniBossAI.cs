using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mini-Boss AI — driven by MiniBossConfig ScriptableObject
///
/// Type A (ChaseAoE):
///   Boss สุ่มเลือก player 1 คน → AoE วงกลมตามหลัง (radius/damage เล็กกว่า Boss หลัก)
///
/// Type B (FloorHazard):
///   พื้นที่อันตราย + Safe Zone 2 จุด (ใหญ่กว่า Boss หลัก = เล่นง่ายกว่า)
///
/// Attack Loop:
///   รอ firstAttackDelay → attack ตาม mechanic ทุก attackInterval วินาที
///
/// Prefab ต้องการ: NetworkObject + Enemy.cs + MiniBossAI.cs
/// </summary>
[RequireComponent(typeof(Enemy))]
public class MiniBossAI : NetworkBehaviour
{
    [Header("Config")]
    [Tooltip("ScriptableObject กำหนด mechanic type + parameters (สร้างจาก Game/MiniBossConfig)")]
    public MiniBossConfig config;

    [Header("Prefabs")]
    [Tooltip("TelegraphZone prefab (NetworkObject + TelegraphZone.cs)")]
    public GameObject telegraphZonePrefab;
    [Tooltip("FloorHazard prefab (NetworkObject + FloorHazard.cs)")]
    public GameObject floorHazardPrefab;

    // ── Internal ──────────────────────────────────────────────────────────
    private Enemy enemy;

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
        Debug.Log($"[MiniBossAI] Spawned — Type: {config.mechanic}");
    }

    // ── Attack Loop (Server only) ─────────────────────────────────────────
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(config.firstAttackDelay);

        while (true)
        {
            if (!NetworkObject.IsSpawned) yield break;

            switch (config.mechanic)
            {
                case MiniBossConfig.MiniBossType.ChaseAoE:
                    SpawnChaseAoE();
                    break;
                case MiniBossConfig.MiniBossType.FloorHazard:
                    SpawnFloorHazard();
                    break;
            }

            yield return new WaitForSeconds(config.attackInterval);
        }
    }

    // ── Type A: Chase AoE ─────────────────────────────────────────────────
    void SpawnChaseAoE()
    {
        if (telegraphZonePrefab == null)
        {
            Debug.LogWarning("[MiniBossAI] telegraphZonePrefab not assigned!");
            return;
        }
        if (NetworkManager.Singleton == null) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count == 0) return;

        var target = clients[Random.Range(0, clients.Count)];
        if (target.PlayerObject == null) return;

        Vector3 startPos = target.PlayerObject.transform.position;
        startPos.y = transform.position.y;

        var go   = Instantiate(telegraphZonePrefab, startPos, Quaternion.identity);
        var zone = go.GetComponent<TelegraphZone>();
        var no   = go.GetComponent<NetworkObject>();
        if (zone == null || no == null) { Destroy(go); return; }

        zone.aoeType             = TelegraphZone.AoEType.Chase;
        zone.radius              = config.chaseRadius;
        zone.warningDuration     = config.chaseWarnTime;
        zone.damage              = config.chaseDamage;
        zone.chaseTargetClientId = target.ClientId;

        no.Spawn(true);
        zone.BroadcastInit();
        Debug.Log($"[MiniBossAI] 🎯 Chase AoE → Client {target.ClientId}");
    }

    // ── Type B: Floor Hazard ──────────────────────────────────────────────
    void SpawnFloorHazard()
    {
        if (floorHazardPrefab == null)
        {
            Debug.LogWarning("[MiniBossAI] floorHazardPrefab not assigned!");
            return;
        }

        Vector3 center = transform.position;

        var go     = Instantiate(floorHazardPrefab, center, Quaternion.identity);
        var hazard = go.GetComponent<FloorHazard>();
        var no     = go.GetComponent<NetworkObject>();
        if (hazard == null || no == null) { Destroy(go); return; }

        no.Spawn(true);
        hazard.Activate(
            center,
            config.hazardArenaRadius,
            config.hazardSafeZoneCount,
            config.hazardSafeZoneRadius,
            config.hazardWarnTime,
            config.hazardDamage);

        Debug.Log($"[MiniBossAI] ☢ Floor Hazard — {config.hazardSafeZoneCount} safe zone(s)");
    }
}
