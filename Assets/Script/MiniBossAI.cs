using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Mini-Boss AI — driven by MiniBossConfig ScriptableObject
///
/// Mechanic ทั้ง 3 แบบ (เลือกใน config):
///   • CircleLine — Circle ↔ Line สลับกัน (Phase 1 ของ MainBoss)
///   • Tether    — เสาผูกกับผู้เล่น ต้องวิ่งหนี
///   • Chase     — วงแดงตามหลังผู้เล่น
///
/// Death Drops: spawn extra ExpOrb กระจายรอบบอส
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
    [Tooltip("TelegraphZone prefab (NetworkObject + TelegraphZone.cs) — ใช้กับ CircleLine + Chase")]
    public GameObject telegraphZonePrefab;
    [Tooltip("BossTether prefab (NetworkObject + BossTether.cs) — ใช้กับ Tether mechanic")]
    public GameObject tetherPrefab;

    // ── Internal ──────────────────────────────────────────────────────────
    private Enemy enemy;
    private int   mechanicIndex    = 0;   // หมุนใน config.mechanics list
    private int   circleLineToggle = 0;   // alternation ภายใน CircleLine (0=Circle, 1=Line)
    private bool  deathHandled;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (config == null)
        {
            Debug.LogWarning("[MiniBossAI] config not assigned — no attacks!");
            return;
        }
        if (config.mechanics == null || config.mechanics.Count == 0)
        {
            Debug.LogWarning("[MiniBossAI] config.mechanics is empty — no attacks!");
            return;
        }

        enemy = GetComponent<Enemy>();
        if (enemy != null) enemy.onDeath.AddListener(OnDeath);

        StartCoroutine(AttackLoop());
        Debug.Log($"[MiniBossAI] Spawned — Mechanics: [{string.Join(", ", config.mechanics)}]");
    }

    public override void OnNetworkDespawn()
    {
        if (enemy != null) enemy.onDeath.RemoveListener(OnDeath);
    }

    // ── Attack Loop (Server only) ─────────────────────────────────────────
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(config.firstAttackDelay);

        while (true)
        {
            if (!NetworkObject.IsSpawned) yield break;

            // เลือก mechanic ตามลำดับใน list (cycle)
            var mech = config.mechanics[mechanicIndex % config.mechanics.Count];
            switch (mech)
            {
                case MiniBossConfig.Mechanic.CircleLine:
                    // alternate Circle ↔ Line ภายใน mechanic เอง (ไม่กระทบ outer rotation)
                    if (circleLineToggle % 2 == 0) SpawnCircleAoE();
                    else                           SpawnLineAoE();
                    circleLineToggle++;
                    break;
                case MiniBossConfig.Mechanic.Tether:
                    SpawnTether();
                    break;
                case MiniBossConfig.Mechanic.Chase:
                    SpawnChaseAoE();
                    break;
            }
            mechanicIndex++;

            yield return new WaitForSeconds(config.attackInterval);
        }
    }

    // ── Mechanic: CircleLine ──────────────────────────────────────────────
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
        Debug.Log("[MiniBossAI] 🔴 Circle AoE");
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
        Debug.Log("[MiniBossAI] ▬ Line AoE");
    }

    // ── Mechanic: Chase ───────────────────────────────────────────────────
    void SpawnChaseAoE()
    {
        if (NetworkManager.Singleton == null) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count == 0) return;

        var target = clients[Random.Range(0, clients.Count)];
        if (target.PlayerObject == null) return;

        Vector3 startPos = target.PlayerObject.transform.position;
        startPos.y = transform.position.y;

        var zone = SpawnZone(startPos, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType             = AoEType.Chase;
        zone.radius              = config.chaseRadius;
        zone.warningDuration     = config.chaseWarnTime;
        zone.damage              = config.chaseDamage;
        zone.chaseTargetClientId = target.ClientId;

        zone.GetComponent<NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log($"[MiniBossAI] 🎯 Chase AoE → Client {target.ClientId}");
    }

    // ── Mechanic: Tether ──────────────────────────────────────────────────
    void SpawnTether()
    {
        if (tetherPrefab == null)
        {
            Debug.LogWarning("[MiniBossAI] tetherPrefab not assigned!");
            return;
        }
        if (NetworkManager.Singleton == null) return;

        var clients = new System.Collections.Generic.List<NetworkClient>(
            NetworkManager.Singleton.ConnectedClientsList);
        if (clients.Count == 0) return;

        // เลือกตำแหน่ง spawn:
        //   Solo  → offset random direction ใกล้ผู้เล่น
        //   Co-op → ตำแหน่งบอส (logical link เท่านั้น)
        Vector3 spawnPos = transform.position;
        if (clients.Count == 1 && clients[0].PlayerObject != null)
        {
            Vector3 playerPos = clients[0].PlayerObject.transform.position;
            Vector2 rand      = Random.insideUnitCircle.normalized * config.tetherSoloOffset;
            spawnPos = new Vector3(playerPos.x + rand.x, playerPos.y, playerPos.z + rand.y);
        }

        var go     = Instantiate(tetherPrefab, spawnPos, Quaternion.identity);
        var tether = go.GetComponent<BossTether>();
        var no     = go.GetComponent<NetworkObject>();
        if (tether == null || no == null) { Destroy(go); return; }

        tether.requiredDistance = config.tetherDistance;
        tether.duration         = config.tetherDuration;
        tether.failDamage       = config.tetherFailDamage;

        no.Spawn(true);

        if (clients.Count == 1)
        {
            tether.ActivateSolo(clients[0].ClientId);
            Debug.Log($"[MiniBossAI] 🔗 Solo Tether → Client {clients[0].ClientId}");
        }
        else
        {
            int idxA = Random.Range(0, clients.Count);
            int idxB;
            do { idxB = Random.Range(0, clients.Count); } while (idxB == idxA);

            tether.Activate(clients[idxA].ClientId, clients[idxB].ClientId);
            Debug.Log($"[MiniBossAI] 🔗 Tether: Client {clients[idxA].ClientId} ↔ Client {clients[idxB].ClientId}");
        }
    }

    // ── Death Drops ───────────────────────────────────────────────────────
    void OnDeath()
    {
        if (!IsServer || deathHandled) return;
        deathHandled = true;
        SpawnExtraDrops();
    }

    void SpawnExtraDrops()
    {
        if (config == null) return;

        // ── ExpOrb extras ─────────────────────────────────────────────────
        if (config.extraExpOrbs > 0)
        {
            if (enemy == null || enemy.expOrbPrefab == null)
            {
                Debug.LogWarning("[MiniBossAI] expOrbPrefab not assigned on Enemy — skip exp drops");
            }
            else
            {
                for (int i = 0; i < config.extraExpOrbs; i++)
                {
                    Vector2 rand = Random.insideUnitCircle * config.dropScatterRadius;
                    Vector3 pos  = transform.position + new Vector3(rand.x, 0f, rand.y);

                    var orb = Instantiate(enemy.expOrbPrefab, pos, Quaternion.identity);
                    orb.GetComponent<NetworkObject>()?.Spawn(true);
                    orb.GetComponent<ExpOrb>()?.SetExpAmount(config.extraExpPerOrb);
                }
                Debug.Log($"[MiniBossAI] 💎 Dropped {config.extraExpOrbs} exp orbs ({config.extraExpPerOrb} each)");
            }
        }

        // ── Bonus GameObject drops (ObjectiveOrb, special items, etc.) ────
        if (config.bonusDrops != null)
        {
            foreach (var drop in config.bonusDrops)
            {
                if (drop == null || drop.prefab == null || drop.count <= 0) continue;

                for (int i = 0; i < drop.count; i++)
                {
                    Vector2 rand = drop.scatterRadius > 0f
                        ? Random.insideUnitCircle * drop.scatterRadius
                        : Vector2.zero;
                    Vector3 pos = transform.position + new Vector3(rand.x, 0f, rand.y);

                    var go = Instantiate(drop.prefab, pos, Quaternion.identity);
                    var no = go.GetComponent<NetworkObject>();
                    if (no != null) no.Spawn(true);
                    else Debug.LogWarning($"[MiniBossAI] BonusDrop '{drop.prefab.name}' has no NetworkObject — won't sync to clients");
                }
                Debug.Log($"[MiniBossAI] 🎁 Dropped {drop.count}× '{drop.prefab.name}'");
            }
        }
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
