using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Objective Orb — เก็บได้เหมือน EXP orb
/// เมื่อ player ชนจะ pause ทุกคนแล้วแสดง 1-card reward UI
///
/// Setup:
///   1. ใส่ script นี้บน prefab ที่มี Collider (isTrigger = true)
///   2. ใส่ NetworkObject component
///   3. วางใน scene หรือ spawn จาก WaveManager/GameTimeline
/// </summary>
public class ObjectiveOrb : NetworkBehaviour
{
    [Header("Visuals")]
    [Tooltip("Effect ที่เล่นก่อน destroy (optional)")]
    public GameObject collectEffect;

    private bool collected = false;

    // ── Trigger ───────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        // Only Owner ของ player นั้น → ส่ง ServerRpc
        var pm = other.GetComponent<playermove>();
        if (pm == null || !pm.IsOwner) return;

        collected = true;
        CollectServerRpc();
    }

    // ── Server ────────────────────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    void CollectServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // ส่ง clientId ของคนที่เก็บ → แสดง card เฉพาะคนนั้น
        ulong collectorId = rpcParams.Receive.SenderClientId;
        SharedExperienceManager.Instance?.StartOrbPhaseForPlayer(collectorId);

        // Spawn collect effect
        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        // Despawn orb
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }
}
