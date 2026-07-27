using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Fetch Item — pickup สำหรับ ZoneObjective Type B (FetchAndDeliver)
///
/// Flow:
///   Player ชน trigger → CollectServerRpc → server เพิ่ม playermove.carriedQuestItems
///   → despawn item
///   ผู้เล่นเดินกลับไปส่งที่ ZoneObjective → drain counter เข้า deliveredCount
///
/// Setup prefab:
///   1. ใส่ script นี้บน prefab
///   2. มี Collider (isTrigger = true)
///   3. มี NetworkObject component
///   4. (Optional) Visual mesh + glow VFX ดึงดูดสายตา
/// </summary>
public class FetchItem : NetworkBehaviour
{
    [Header("Visuals (Optional)")]
    [Tooltip("VFX ที่เล่นตอนเก็บ")]
    public GameObject collectEffect;

    [Tooltip("Icon ที่ใช้แสดงใน HUD counter (optional)")]
    public Sprite     itemIcon;

    // ── Static Events (UI subscribe เพื่อแสดง indicator) ─────────────────
    public static event System.Action<FetchItem> OnFetchItemSpawned;
    public static event System.Action<FetchItem> OnFetchItemDespawned;

    private bool collected;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        OnFetchItemSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        OnFetchItemDespawned?.Invoke(this);
    }

    // ── Trigger (Server Authority) ────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponentInParent<playermove>();
        if (pm == null) return;

        collected = true;

        // เพิ่มจำนวน carry item ใน playermove ของผู้เล่นคนนี้
        pm.AddCarriedQuestItem();

        // เล่น VFX บน client ทั้งหมด
        PlayCollectEffectClientRpc(transform.position);

        // Despawn item บน server
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void PlayCollectEffectClientRpc(Vector3 pos)
    {
        if (collectEffect != null)
            Instantiate(collectEffect, pos, Quaternion.identity);
    }
}
