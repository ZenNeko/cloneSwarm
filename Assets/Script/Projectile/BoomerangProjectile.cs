using Unity.Netcode;
using UnityEngine;

/// <summary>
/// BoomerangProjectile — บินออกถึง maxRange แล้วบินกลับหา owner
/// ดาเมจทุก enemy ที่ผ่าน (ไม่ destroy เมื่อชน)
///
/// Prefab setup:
///   - NetworkObject
///   - Rigidbody (kinematic=true, useGravity=false)
///   - CapsuleCollider/BoxCollider isTrigger=true
///   - BoomerangProjectile script
/// </summary>
public class BoomerangProjectile : NetworkBehaviour
{
    [HideInInspector] public float   damage   = 40f;
    [HideInInspector] public float   speed    = 14f;
    [HideInInspector] public float   maxRange = 8f;
    [HideInInspector] public ulong   ownerClientId;
    [HideInInspector] public bool    isCrit;
    [HideInInspector] public string  weaponName = "Unknown";

    // ── State ────────────────────────────────────────────────────────────
    private enum Phase { Forward, Returning }
    private Phase   phase;
    private Vector3 startPos;
    private Vector3 moveDir;

    // ป้องกัน hit enemy เดิมซ้ำใน phase เดียวกัน (reset เมื่อเปลี่ยน phase)
    private System.Collections.Generic.HashSet<int> hitIds = new();

    // ── Init (Server) ─────────────────────────────────────────────────────
    public void Init(Vector3 direction)
    {
        moveDir  = direction.normalized;
        moveDir.y = 0f;
        startPos = transform.position;
        phase    = Phase.Forward;
    }

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        // Visual spin — ทุก client (smooth), หมุนรอบ world Y คง prefab offset
        transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

        if (!IsServer || !NetworkObject.IsSpawned) return;
        // ── Movement + Collision (server only) ─────────────────────────────

        if (phase == Phase.Forward)
        {
            transform.position += moveDir * speed * Time.deltaTime;

            float traveled = Vector3.Distance(startPos, transform.position);
            if (traveled >= maxRange)
            {
                phase = Phase.Returning;
                hitIds.Clear();
            }
        }
        else
        {
            // บินกลับหา owner
            Transform ownerTf = GetOwnerTransform();
            if (ownerTf == null) { SafeDespawn(); return; }

            Vector3 toOwner = (ownerTf.position - transform.position);
            toOwner.y = 0f;
            if (toOwner.magnitude < 0.8f) { SafeDespawn(); return; }

            transform.position += toOwner.normalized * speed * Time.deltaTime;
        }
    }

    // ── Collision ─────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        var enemy = other.GetComponent<Enemy>();
        if (enemy == null) return;

        int id = enemy.GetInstanceID();
        if (hitIds.Contains(id)) return;

        hitIds.Add(id);
        enemy.EnemyTakeDamage(damage);
        
        // Register weapon damage on owner
        Transform ownerTf = GetOwnerTransform();
        if (ownerTf != null)
        {
            var pwm = ownerTf.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.RegisterWeaponDamage(weaponName, damage);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetOwnerTransform()
    {
        if (NetworkManager.Singleton == null) return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(ownerClientId, out var client))
            return null;
        return client.PlayerObject?.transform;
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
