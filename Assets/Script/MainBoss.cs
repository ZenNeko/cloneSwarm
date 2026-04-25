using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss AI — อยู่บน Boss Prefab ร่วมกับ Enemy.cs
///
/// Phases:
///   Phase 1 (100-60% HP): CircleAoE ทุก 4s
///   Phase 2  (60-30% HP): Circle → Cross → Chase ทุก 3s
///   Phase 3  (30-0%  HP): Circle → Spread → Chase → Tether → FloorHazard (enrage)
///
/// Static Events:
///   OnAnyBossSpawned  — fired on ALL clients when boss spawns (BossHUDUI subscribes)
///   OnAnyBossDespawned — fired on ALL clients when boss despawns
///
/// TelegraphZone Prefab ต้องมี TelegraphZone.cs + NetworkObject
/// </summary>
[RequireComponent(typeof(Enemy))]
public class MainBoss : NetworkBehaviour
{
    // ── Static Events (subscribe on all clients) ──────────────────────────
    public static event System.Action<MainBoss> OnAnyBossSpawned;
    public static event System.Action           OnAnyBossDespawned;
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

    [Header("Line AoE (Phase 1 fallback)")]
    public float lineLength     = 10f;
    public float lineWidth      = 2f;
    public float lineDamage     = 35f;
    public float lineWarnTime   = 2.5f;

    [Header("Cross AoE (Phase 2)")]
    public float crossLineLength = 8f;
    public float crossLineWidth  = 2f;
    public float crossDamage     = 30f;
    public float crossWarnTime   = 2.5f;

    [Header("Spread AoE (Phase 3)")]
    public float spreadLineLength = 12f;
    public float spreadLineWidth  = 1.2f;
    public float spreadDamage     = 20f;
    public float spreadWarnTime   = 2.5f;
    public int   spreadCount      = 5;
    public float spreadAngle      = 60f;

    [Header("Donut AoE (Phase 3)")]
    public float donutRadius      = 6f;
    public float donutInnerRadius = 2f;
    public float donutDamage      = 35f;
    public float donutWarnTime    = 2.5f;

    [Header("Cone AoE (Phase 3)")]
    public float coneRadius    = 8f;
    public float coneAngle     = 90f;
    public float coneLineWidth = 1.2f;
    public float coneDamage    = 30f;
    public float coneWarnTime  = 2.5f;

    [Header("Chase AoE (Phase 2 / Phase 3)")]
    [Tooltip("รัศมีวงกลม Chase")]
    public float chaseRadius   = 3f;
    [Tooltip("ดาเมจเมื่อ detonate")]
    public float chaseDamage   = 35f;
    [Tooltip("ระยะเวลา warning (ยาวกว่า AoE ปกติ — ให้เวลาผู้เล่นวิ่งหนี)")]
    public float chaseWarnTime = 4f;

    [Header("Tether (Phase 3)")]
    [Tooltip("Prefab ที่มี BossTether.cs + NetworkObject")]
    public GameObject tetherPrefab;
    [Tooltip("ระยะที่ต้องวิ่งแยก (เมตร)")]
    public float tetherDistance  = 8f;
    [Tooltip("เวลา tether (วินาที)")]
    public float tetherDuration  = 6f;
    [Tooltip("ดาเมจถ้าไม่แยกทัน")]
    public float tetherFailDamage = 40f;

    [Header("Floor Hazard (Phase 3)")]
    [Tooltip("Prefab ที่มี FloorHazard.cs + NetworkObject")]
    public GameObject floorHazardPrefab;
    [Tooltip("รัศมี arena อันตราย")]
    public float floorArenaRadius     = 18f;
    [Tooltip("จำนวน Safe Zone (1–2 สำหรับ boss หลัก)")]
    [Range(1, 2)]
    public int   floorSafeZoneCount   = 1;
    [Tooltip("รัศมีแต่ละ Safe Zone")]
    public float floorSafeZoneRadius  = 3f;
    [Tooltip("ระยะ warning")]
    public float floorWarnTime        = 6f;
    [Tooltip("ดาเมจผู้เล่นที่อยู่นอก Safe Zone")]
    public float floorDamage          = 50f;

    [Header("Prefabs")]
    [Tooltip("Prefab ที่มี TelegraphZone.cs + NetworkObject")]
    public GameObject telegraphZonePrefab;

    // ── Refs ──────────────────────────────────────────────────────────────
    private Enemy enemy;
    private int   currentPhase  = 0;
    private bool  attackLoopRunning;
    private int   attackIndex   = 0;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        // Fire on ALL clients so BossHUDUI can subscribe to HP
        OnAnyBossSpawned?.Invoke(this);

        if (!IsServer) return;

        enemy = GetComponent<Enemy>();
        if (enemy == null) { Debug.LogError("[MainBoss] Enemy component not found!"); return; }

        enemy.netHealth.OnValueChanged += OnHealthChanged;

        StartCoroutine(AttackLoop());
        attackLoopRunning = true;
        Debug.Log("[MainBoss] Boss spawned — Phase 1");
    }

    public override void OnNetworkDespawn()
    {
        // Fire on ALL clients
        OnAnyBossDespawned?.Invoke();

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
        string msg = phase switch
        {
            2 => "PHASE 2 — ENRAGE",
            3 => "PHASE 3 — FINAL FORM",
            _ => $"PHASE {phase}",
        };
        Color col = phase switch
        {
            2 => new Color(1f, 0.5f, 0f),
            3 => Color.red,
            _ => Color.white,
        };
        UnityEngine.Object.FindAnyObjectByType<GameHUD>()?.ShowAnnouncement(msg, col);
    }

    // ── Attack Loop (Server only) ─────────────────────────────────────────
    IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(3f);

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

            switch (currentPhase)
            {
                case 1:
                    SpawnCircleAoE();
                    break;
                case 2:
                    // Phase 2 rotation: Circle → Cross → Chase
                    switch (attackIndex % 3)
                    {
                        case 0: SpawnCircleAoE(); break;
                        case 1: SpawnCrossAoE();  break;
                        case 2: SpawnChaseAoE();  break;
                    }
                    attackIndex++;
                    break;
                case 3:
                    // Phase 3 rotation: Circle → Spread → Chase → Tether → FloorHazard
                    switch (attackIndex % 5)
                    {
                        case 0: SpawnCircleAoE();    break;
                        case 1: SpawnSpreadAoE();    break;
                        case 2: SpawnChaseAoE();     break;
                        case 3: SpawnTether();       break;
                        case 4: SpawnFloorHazard();  break;
                    }
                    attackIndex++;
                    break;
                default:
                    SpawnCircleAoE();
                    break;
            }
        }
    }

    // ── Spawn AoE ─────────────────────────────────────────────────────────
    void SpawnCircleAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = TelegraphZone.AoEType.Circle;
        zone.radius          = circleRadius;
        zone.warningDuration = circleWarnTime;
        zone.damage          = circleDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🔴 Circle AoE spawned");
    }

    void SpawnCrossAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = TelegraphZone.AoEType.Cross;
        zone.lineLength      = crossLineLength;
        zone.lineWidth       = crossLineWidth;
        zone.warningDuration = crossWarnTime;
        zone.damage          = crossDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] ✙ Cross AoE spawned");
    }

    void SpawnSpreadAoE()
    {
        Transform target = FindNearestPlayer();
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dir.y = 0f;
        Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

        var zone = SpawnZone(transform.position, rot);
        if (zone == null) return;

        zone.aoeType         = TelegraphZone.AoEType.Spread;
        zone.lineLength      = spreadLineLength;
        zone.lineWidth       = spreadLineWidth;
        zone.warningDuration = spreadWarnTime;
        zone.damage          = spreadDamage;
        zone.spreadCount     = spreadCount;
        zone.spreadAngle     = spreadAngle;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🌊 Spread AoE spawned");
    }

    void SpawnDonutAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = TelegraphZone.AoEType.Donut;
        zone.radius          = donutRadius;
        zone.innerRadius     = donutInnerRadius;
        zone.warningDuration = donutWarnTime;
        zone.damage          = donutDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🍩 Donut AoE spawned");
    }

    void SpawnConeAoE()
    {
        Transform target = FindNearestPlayer();
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dir.y = 0f;
        Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

        var zone = SpawnZone(transform.position, rot);
        if (zone == null) return;

        zone.aoeType         = TelegraphZone.AoEType.Cone;
        zone.radius          = coneRadius;
        zone.coneAngle       = coneAngle;
        zone.lineWidth       = coneLineWidth;
        zone.warningDuration = coneWarnTime;
        zone.damage          = coneDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🔺 Cone AoE spawned");
    }

    void SpawnChaseAoE()
    {
        if (NetworkManager.Singleton == null) return;

        // สุ่มเลือก player เป็น target
        var clients = new System.Collections.Generic.List<Unity.Netcode.NetworkClient>(
            NetworkManager.Singleton.ConnectedClientsList);
        if (clients.Count == 0) return;

        var target = clients[UnityEngine.Random.Range(0, clients.Count)];
        if (target.PlayerObject == null) return;

        // Spawn zone ที่ตำแหน่งเริ่มต้นของ target player
        Vector3 startPos = target.PlayerObject.transform.position;
        startPos.y = transform.position.y;  // คง Y เท่ากับ boss

        var zone = SpawnZone(startPos, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType             = TelegraphZone.AoEType.Chase;
        zone.radius              = chaseRadius;
        zone.warningDuration     = chaseWarnTime;
        zone.damage              = chaseDamage;
        zone.chaseTargetClientId = target.ClientId;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log($"[MainBoss] 🎯 Chase AoE → Client {target.ClientId}");
    }

    void SpawnTether()
    {
        if (tetherPrefab == null)
        {
            Debug.LogWarning("[MainBoss] tetherPrefab not assigned — skipping tether");
            return;
        }
        if (NetworkManager.Singleton == null) return;

        var clients = new System.Collections.Generic.List<Unity.Netcode.NetworkClient>(
            NetworkManager.Singleton.ConnectedClientsList);

        // ต้องการผู้เล่นอย่างน้อย 2 คน
        if (clients.Count < 2)
        {
            Debug.Log("[MainBoss] Tether skipped — only 1 player online");
            return;
        }

        // สุ่มเลือก 2 players ที่แตกต่างกัน
        int idxA = Random.Range(0, clients.Count);
        int idxB;
        do { idxB = Random.Range(0, clients.Count); } while (idxB == idxA);

        var go     = Instantiate(tetherPrefab, transform.position, Quaternion.identity);
        var tether = go.GetComponent<BossTether>();
        var no     = go.GetComponent<Unity.Netcode.NetworkObject>();
        if (tether == null || no == null) { Destroy(go); return; }

        // ปรับค่าจาก inspector
        tether.requiredDistance = tetherDistance;
        tether.duration         = tetherDuration;
        tether.failDamage       = tetherFailDamage;

        no.Spawn(true);
        tether.Activate(clients[idxA].ClientId, clients[idxB].ClientId);
        Debug.Log($"[MainBoss] 🔗 Tether: Client {clients[idxA].ClientId} ↔ Client {clients[idxB].ClientId}");
    }

    void SpawnFloorHazard()
    {
        if (floorHazardPrefab == null)
        {
            Debug.LogWarning("[MainBoss] floorHazardPrefab not assigned — skipping floor hazard");
            return;
        }

        Vector3 center = transform.position;

        var go     = Instantiate(floorHazardPrefab, center, Quaternion.identity);
        var hazard = go.GetComponent<FloorHazard>();
        var no     = go.GetComponent<Unity.Netcode.NetworkObject>();
        if (hazard == null || no == null) { Destroy(go); return; }

        no.Spawn(true);
        hazard.Activate(
            center,
            floorArenaRadius,
            floorSafeZoneCount,
            floorSafeZoneRadius,
            floorWarnTime,
            floorDamage);
        Debug.Log($"[MainBoss] ☢ Floor Hazard — {floorSafeZoneCount} safe zone(s)");
    }

    // SpawnZone สร้าง instance แต่ยังไม่ Spawn (caller จัดการ)
    TelegraphZone SpawnZone(Vector3 pos, Quaternion rot)
    {
        if (telegraphZonePrefab == null)
        {
            Debug.LogWarning("[MainBoss] telegraphZonePrefab not assigned!");
            return null;
        }

        var go   = Instantiate(telegraphZonePrefab, pos, rot);
        var zone = go.GetComponent<TelegraphZone>();
        var no   = go.GetComponent<Unity.Netcode.NetworkObject>();

        if (zone == null || no == null) { Destroy(go); return null; }
        return zone;
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
