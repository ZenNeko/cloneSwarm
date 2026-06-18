using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ExpOrb : NetworkBehaviour
{
    public static readonly List<ExpOrb> ActiveOrbs = new();

    [Header("EXP")]
    public float expAmount = 10f;

    [Header("Pickup")]
    [Tooltip("ระยะ base ที่ orb เริ่มวิ่งเข้าหา player")]
    public float attractRadius = 4f;
    [Tooltip("อัตราส่วน attractRadius ต่อ AttackRange ของ PlayerWeapon (0 = ใช้ attractRadius คงที่)")]
    public float attractRadiusRatio = 0.5f;
    public float moveSpeed    = 8f;
    public float pickupRadius = 0.4f;

    [Header("Bob Animation")]
    public float bobHeight = 0.2f;
    public float bobSpeed  = 2f;

    private Transform currentTarget;
    private Vector3   startPos;
    private bool      forceAttract = false;

    // ── Init (เรียกจาก Enemy หลัง Spawn) ─────────────────────────────────
    public void SetExpAmount(float amount) => expAmount = amount;

    public void ForceAttractTo(Transform target)
    {
        if (target == null) return;
        currentTarget = target;
        forceAttract = true;
        moveSpeed = 15f;
    }

    public override void OnNetworkSpawn()
    {
        if (!ActiveOrbs.Contains(this)) ActiveOrbs.Add(this);
        if (!IsServer) return;
        startPos      = transform.position;
        currentTarget = FindNearestPlayer();
    }

    public override void OnNetworkDespawn()
    {
        ActiveOrbs.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveOrbs.Remove(this);
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
            Collect();
            return;
        }

        if (forceAttract || dist <= GetAttractRadius())
        {
            transform.position = Vector3.MoveTowards(
                transform.position, currentTarget.position, moveSpeed * Time.deltaTime);
        }
        else
        {
            // Bob animation รอ player เข้ามา
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }
    }

    // ── Collect ───────────────────────────────────────────────────────────
    void Collect()
    {
        float finalExp = expAmount;
        // Find nearest player stat manager
        if (currentTarget != null)
        {
            var sm = currentTarget.GetComponent<PlayerStatManager>();
            if (sm != null) finalExp *= sm.GetExpMultiplier();
        }
        SharedExperienceManager.Instance?.AddExp(finalExp);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (other.CompareTag("Player"))
        {
            var pm = other.GetComponentInParent<playermove>();
            if (pm != null) Collect();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    float GetAttractRadius()
    {
        if (currentTarget == null) return attractRadius;

        // Use new PlayerStatManager PickupRadius multiplier
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, GetAttractRadius());
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
