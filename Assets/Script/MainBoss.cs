using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss AI — อยู่บน Boss Prefab ร่วมกับ Enemy.cs
///
/// Phases:
///   Phase 1 (100-60% HP): Circle ↔ Line สลับกัน ทุก 4s
///   Phase 2  (60-30% HP): Circle → Cross → Chase ทุก 3s
///   Phase 3  (30-0%  HP): สลับ 1/2 AoE ต่อ tick (random pick) ทุก 2s
///                          • tick 0,2,4… → 1 AoE | tick 1,3,5… → 2 AoE
///                          • Pool 7 อย่าง: Circle / Cross+ / CrossX / Line / Donut / Chase / Tether
///                          • ห้ามใช้ซ้ำของที่เพิ่งใช้ — ต้องรอ AoE อื่น 1 ครั้งก่อน
///                          • Cross+ และ CrossX แยกกัน — ใช้ Cross+ ติดต่อกันก็ได้ (ถ้าเลือก CrossX แทรก)
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

    [Header("Display")]
    [Tooltip("ชื่อที่แสดงใน Canvas HP bar และ World HP bar — ถ้าว่างจะใช้ 'BOSS'")]
    public string bossDisplayName = "BOSS";

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

    [Header("Line AoE (Phase 1 / Phase 3)")]
    public float lineLength     = 10f;
    public float lineWidth      = 2f;
    public float lineDamage     = 35f;
    public float lineWarnTime   = 2.5f;

    [Header("Cross AoE (Phase 2)")]
    public float crossLineLength = 8f;
    public float crossLineWidth  = 2f;
    public float crossDamage     = 30f;
    public float crossWarnTime   = 2.5f;

    [Header("Donut AoE (Phase 3)")]
    public float donutRadius      = 6f;
    public float donutInnerRadius = 2f;
    public float donutDamage      = 35f;
    public float donutWarnTime    = 2.5f;

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
    [Tooltip("Solo: ระยะ offset จากผู้เล่นที่จะ spawn เสา anchor (เมตร)\n" +
             "เล็กๆ ก็พอ เพื่อให้ผู้เล่นต้องวิ่งหนีจริงๆ")]
    public float tetherSoloSpawnOffset = 2f;

    [Header("Prefabs")]
    [Tooltip("Prefab ที่มี TelegraphZone.cs + NetworkObject")]
    public GameObject telegraphZonePrefab;

    // ── Refs ──────────────────────────────────────────────────────────────
    private Enemy enemy;
    private int   currentPhase  = 0;
    private bool  attackLoopRunning;
    private int   attackIndex   = 0;

    // Phase 3: queue เก็บ AoE ที่ใช้ไปครั้งล่าสุด — ห้ามใช้ซ้ำ (cooldown 1 ครั้ง)
    private readonly Queue<int> phase3RecentAttacks = new Queue<int>();
    private const int PHASE3_COOLDOWN_USES = 1;
    private const int PHASE3_POOL_SIZE     = 7;   // Circle, Cross+, CrossX, Line, Donut, Chase, Tether

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

    [Header("Phase Transition")]
    [Tooltip("วินาทีที่บอส invincible ตอนเปลี่ยน phase (block damage + skip attack)")]
    public float phaseTransitionDuration = 1.5f;
    [Tooltip("camera shake magnitude")]
    public float phaseShakeMagnitude     = 0.4f;

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
            StartCoroutine(PhaseTransitionInvincibility());
            Debug.Log($"[MainBoss] Phase → {currentPhase} ({pct:P0} HP)");
        }
    }

    /// <summary>Server-side: ทำให้บอส invincible ระหว่างเปลี่ยน phase</summary>
    IEnumerator PhaseTransitionInvincibility()
    {
        if (enemy == null) yield break;
        enemy.serverInvincible = true;
        yield return new WaitForSeconds(phaseTransitionDuration);
        enemy.serverInvincible = false;
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

        // Shockwave VFX + camera shake บนทุก client
        NetworkedVFXPool.Instance?.PlayByName("PhaseShockwave", transform.position);
        CameraShake.Instance?.Shake(0.5f, phaseShakeMagnitude);
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
            if (enemy != null && enemy.serverInvincible) continue;  // skip attack during phase transition

            switch (currentPhase)
            {
                case 1:
                    // Phase 1 rotation: Circle ↔ Line
                    switch (attackIndex % 2)
                    {
                        case 0: SpawnCircleAoE(); break;
                        case 1: SpawnLineAoE();   break;
                    }
                    attackIndex++;
                    break;
                case 2:
                    // Phase 2 rotation: Circle → Cross (random +/X) → Chase
                    switch (attackIndex % 3)
                    {
                        case 0: SpawnCircleAoE();                       break;
                        case 1: SpawnCrossAoE(isX: Random.value < 0.5f); break;
                        case 2: SpawnChaseAoE();                        break;
                    }
                    attackIndex++;
                    break;
                case 3:
                    // Phase 3 — สลับ 1/2 AoE per tick, random pick, cooldown 2 ครั้ง
                    int aoeCount = (attackIndex % 2 == 0) ? 1 : 2;
                    foreach (int idx in PickPhase3Attacks(aoeCount))
                        ExecutePhase3Attack(idx);
                    attackIndex++;
                    break;
                default:
                    SpawnCircleAoE();
                    break;
            }
        }
    }

    // ── Phase 3 Picker ────────────────────────────────────────────────────
    /// <summary>
    /// สุ่ม AoE indices สำหรับ Phase 3 — ห้ามใช้ซ้ำของที่อยู่ใน recent queue
    /// (cap = PHASE3_COOLDOWN_USES). อัปเดต queue หลังเลือกแต่ละครั้ง
    /// </summary>
    IEnumerable<int> PickPhase3Attacks(int count)
    {
        var picks = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            var available = Enumerable.Range(0, PHASE3_POOL_SIZE)
                .Where(idx => !phase3RecentAttacks.Contains(idx))
                .ToList();
            if (available.Count == 0) break;

            int pick = available[Random.Range(0, available.Count)];
            picks.Add(pick);

            phase3RecentAttacks.Enqueue(pick);
            if (phase3RecentAttacks.Count > PHASE3_COOLDOWN_USES)
                phase3RecentAttacks.Dequeue();
        }
        return picks;
    }

    void ExecutePhase3Attack(int idx)
    {
        switch (idx)
        {
            case 0: SpawnCircleAoE();        break;
            case 1: SpawnCrossAoE(isX: false); break;  // +
            case 2: SpawnCrossAoE(isX: true);  break;  // X
            case 3: SpawnLineAoE();          break;
            case 4: SpawnDonutAoE();         break;
            case 5: SpawnChaseAoE();         break;
            case 6: SpawnTether();           break;
        }
    }

    // ── Spawn AoE ─────────────────────────────────────────────────────────
    void SpawnCircleAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = AoEType.Circle;
        zone.radius          = circleRadius;
        zone.warningDuration = circleWarnTime;
        zone.damage          = circleDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🔴 Circle AoE spawned");
    }

    void SpawnLineAoE()
    {
        // หันแถบ Line ไปทาง player ที่ใกล้ที่สุด เพื่อให้ลำตัว Line ผ่านเข้ามาที่บอส
        Transform target = FindNearestPlayer();
        Vector3 dir = target != null
            ? (target.position - transform.position).normalized
            : transform.forward;
        dir.y = 0f;
        Quaternion rot = dir != Vector3.zero ? Quaternion.LookRotation(dir) : Quaternion.identity;

        var zone = SpawnZone(transform.position, rot);
        if (zone == null) return;

        zone.aoeType         = AoEType.Line;
        zone.lineLength      = lineLength;
        zone.lineWidth       = lineWidth;
        zone.warningDuration = lineWarnTime;
        zone.damage          = lineDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] ▬ Line AoE spawned");
    }

    /// <summary>Cross AoE — isX=true → หมุน 45° (X form), isX=false → axis-aligned (+ form)</summary>
    void SpawnCrossAoE(bool isX)
    {
        Quaternion rot = isX ? Quaternion.Euler(0f, 45f, 0f) : Quaternion.identity;

        var zone = SpawnZone(transform.position, rot);
        if (zone == null) return;

        zone.aoeType         = AoEType.Cross;
        zone.lineLength      = crossLineLength;
        zone.lineWidth       = crossLineWidth;
        zone.warningDuration = crossWarnTime;
        zone.damage          = crossDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log($"[MainBoss] {(isX ? "✕" : "✙")} Cross AoE spawned ({(isX ? "X" : "+")})");
    }

    void SpawnDonutAoE()
    {
        var zone = SpawnZone(transform.position, Quaternion.identity);
        if (zone == null) return;

        zone.aoeType         = AoEType.Donut;
        zone.radius          = donutRadius;
        zone.innerRadius     = donutInnerRadius;
        zone.warningDuration = donutWarnTime;
        zone.damage          = donutDamage;

        zone.GetComponent<Unity.Netcode.NetworkObject>().Spawn(true);
        zone.BroadcastInit();
        Debug.Log("[MainBoss] 🍩 Donut AoE spawned");
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

        zone.aoeType             = AoEType.Chase;
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
        if (clients.Count == 0) return;

        // เลือกตำแหน่ง spawn:
        //   Solo  → offset random direction ใกล้ผู้เล่น (ผู้เล่นต้องวิ่งหนีเสา)
        //   Co-op → ตำแหน่งบอส (เป็นแค่ logical link ระหว่าง 2 players)
        Vector3 spawnPos = transform.position;
        if (clients.Count == 1 && clients[0].PlayerObject != null)
        {
            Vector3 playerPos = clients[0].PlayerObject.transform.position;
            Vector2 rand      = Random.insideUnitCircle.normalized * tetherSoloSpawnOffset;
            spawnPos = new Vector3(playerPos.x + rand.x, playerPos.y, playerPos.z + rand.y);
        }

        var go     = Instantiate(tetherPrefab, spawnPos, Quaternion.identity);
        var tether = go.GetComponent<BossTether>();
        var no     = go.GetComponent<Unity.Netcode.NetworkObject>();
        if (tether == null || no == null) { Destroy(go); return; }

        // ปรับค่าจาก inspector
        tether.requiredDistance = tetherDistance;
        tether.duration         = tetherDuration;
        tether.failDamage       = tetherFailDamage;

        no.Spawn(true);

        if (clients.Count == 1)
        {
            // SOLO MODE — เสา anchor ใกล้ผู้เล่น
            tether.ActivateSolo(clients[0].ClientId);
            Debug.Log($"[MainBoss] 🔗 Solo Tether → Client {clients[0].ClientId} (pillar at {spawnPos})");
        }
        else
        {
            // CO-OP MODE — สุ่ม 2 players ที่แตกต่างกัน
            int idxA = Random.Range(0, clients.Count);
            int idxB;
            do { idxB = Random.Range(0, clients.Count); } while (idxB == idxA);

            tether.Activate(clients[idxA].ClientId, clients[idxB].ClientId);
            Debug.Log($"[MainBoss] 🔗 Tether: Client {clients[idxA].ClientId} ↔ Client {clients[idxB].ClientId}");
        }
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
