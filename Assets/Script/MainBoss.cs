using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Boss AI — อยู่บน Boss Prefab ร่วมกับ Enemy.cs
///
/// Phases:
///   Phase 1 (100-60% HP): CircleAoE ทุก 4s
///   Phase 2  (60-30% HP): Circle + Cross สลับกัน ทุก 3s
///   Phase 3  (30-0%  HP): Circle + Spread ทุก 2s (enrage)
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
                    if (attackIndex % 2 == 0) SpawnCircleAoE();
                    else                      SpawnCrossAoE();
                    attackIndex++;
                    break;
                case 3:
                    if (attackIndex % 2 == 0) SpawnCircleAoE();
                    else                      SpawnSpreadAoE();
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
