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
    [Tooltip("VFX เสริมตอนเก็บ — เลือก key จาก VFXDatabase · None = ไม่เล่นอะไรเพิ่ม · " +
             "(OrbVisual.PlayCollectEffect หรือ OrbPickup ด้านล่างเล่นอยู่แล้ว ตัวนี้เป็นของแถม)")]
    [VFXKey]
    public string collectVfxKey = "None";

    [Header("Pickup")]
    [Tooltip("ระยะ base ที่ orb เริ่มวิ่งเข้าหา player")]
    public float attractRadius = 4f;
    public float moveSpeed    = 8f;
    public float pickupRadius = 0.4f;

    private Transform currentTarget;
    private bool      collected = false;
    private bool      forceAttract = false;

    public void ForceAttractTo(Transform target)
    {
        if (target == null) return;
        currentTarget = target;
        forceAttract = true;
        moveSpeed = 15f; // Speed up when magnetized
    }

    // ── OnNetworkSpawn ────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        currentTarget = FindNearestPlayer();
    }

    // ── Update: Server only ───────────────────────────────────────────────
    void Update()
    {
        if (!IsServer) return;

        if (!forceAttract && (Time.frameCount % 90 == 0 || currentTarget == null))
            currentTarget = FindNearestPlayer();

        if (currentTarget == null) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);

        if (dist <= pickupRadius)
        {
            Collect(currentTarget);
            return;
        }

        if (forceAttract || dist <= GetAttractRadius())
        {
            transform.position = Vector3.MoveTowards(
                transform.position, currentTarget.position, moveSpeed * Time.deltaTime);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    float GetAttractRadius()
    {
        if (currentTarget == null) return attractRadius;

        var sm = currentTarget.GetComponent<PlayerStatManager>();
        float mult = sm != null ? sm.GetPickupRadiusMultiplier() : 1f;
        return attractRadius * mult;
    }

    Transform FindNearestPlayer()
    {
        if (NetworkManager.Singleton == null) return null;

        Transform nearest = null;
        float     minDist = float.MaxValue;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = client.PlayerObject;
            if (obj == null) continue;

            float dist = Vector3.Distance(transform.position, obj.transform.position);
            if (dist < minDist) { minDist = dist; nearest = obj.transform; }
        }
        return nearest;
    }

    void Collect(Transform player)
    {
        if (collected) return;
        
        var pm = player != null ? player.GetComponent<playermove>() : null;
        if (pm == null) return;

        collected = true;

        // ส่ง clientId ของคนที่เก็บ → แสดง card เฉพาะคนนั้น
        ulong collectorId = pm.OwnerClientId;
        SharedExperienceManager.Instance?.StartOrbPhaseForPlayer(collectorId);

        // แจ้งเตือนทุก Client ให้เล่น VFX
        PlayCollectEffectClientRpc();

        // Despawn orb
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    // ── Trigger Fallback ──────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            var pm = other.GetComponentInParent<playermove>();
            if (pm != null) Collect(pm.transform);
        }
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc]
    void PlayCollectEffectClientRpc()
    {
        // เล่น VFX ผ่าน OrbVisual บน Client (ถ้ามี)
        var visual = GetComponent<OrbVisual>();
        if (visual != null)
        {
            visual.PlayCollectEffect();
        }
        else
        {
            // fallback
            VFXFactory.Play("OrbPickup", transform.position);
        }

        // VFX เสริม — ผ่าน pool ตาม CLAUDE.md ข้อ 2 ห้าม Instantiate prefab ตรงๆ
        //
        // ของเดิมเป็นช่อง GameObject collectEffect ที่ Instantiate ดิบๆ และ prefab ที่ต่อไว้
        // (Sparks blue.prefab) ถูกลบไปตั้งแต่ commit bdad616c — โค้ดเช็ค null แล้วข้ามเงียบ
        // เลยไม่มีใครรู้ว่ามันหายไป · เปลี่ยนเป็น key แล้วปัญหาหมดทั้งสองอย่างพร้อมกัน
        if (!string.IsNullOrEmpty(collectVfxKey) && collectVfxKey != "None")
            VFXFactory.Play(collectVfxKey, transform.position);
    }
}
