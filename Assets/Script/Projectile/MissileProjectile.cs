using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hunter Q — Homing Missile
/// Server-side movement: เคลื่อนที่หา target (ulong NetworkObjectId)
/// เมื่อถึง target หรือ target ตาย → ระเบิด AoE
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MissileProjectile : NetworkBehaviour
{
    [Header("Missile Config")]
    public float moveSpeed        = 14f;
    public float turnSpeed        = 180f;  // องศา/วินาที
    public float arrivalDistance  = 0.8f;  // ระยะที่ถือว่า "ถึง" target

    // ── Runtime (set by SpawnMissilesServerRpc) ───────────────────────────
    [HideInInspector] public float damage;
    [HideInInspector] public float explosionRadius;

    private ulong     targetNetId;
    private Transform targetTransform;
    private bool      hasExploded;
    private float     maxLifetime = 8f;
    private float     lifeTimer;

    // ── Init ──────────────────────────────────────────────────────────────
    public void Init(ulong netId, float dmg, float radius)
    {
        targetNetId    = netId;
        damage         = dmg;
        explosionRadius = radius;
    }

    // ── Update (Server only) ──────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || hasExploded) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetime) { Explode(transform.position); return; }

        // Lazy-find target transform
        if (targetTransform == null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var netObj))
                targetTransform = netObj.transform;
        }

        // Target หายไปหรือตายแล้ว — ระเบิดที่ตำแหน่งตัวเอง
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            Explode(transform.position);
            return;
        }

        Vector3 toTarget = targetTransform.position - transform.position;

        // ถึง target
        if (toTarget.magnitude <= arrivalDistance)
        {
            Explode(targetTransform.position);
            return;
        }

        // Homing movement
        Vector3 desiredDir  = toTarget.normalized;
        Vector3 currentDir  = transform.forward;
        Vector3 newDir      = Vector3.RotateTowards(currentDir, desiredDir,
            turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        transform.rotation  = Quaternion.LookRotation(newDir);
        transform.position += newDir * moveSpeed * Time.deltaTime;
    }

    // ── Explode ───────────────────────────────────────────────────────────
    void Explode(Vector3 center)
    {
        if (hasExploded) return;
        hasExploded = true;

        // Damage enemies in radius
        var cols = PlayerWeaponManager.OverlapEnemy(center, explosionRadius);
        foreach (var c in cols)
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage);

        ShowExplosionClientRpc(center);
        GetComponent<NetworkObject>()?.Despawn(true);
    }

    [ClientRpc]
    void ShowExplosionClientRpc(Vector3 pos)
    {
        VFXFactory.Play(VFXType.GrenadeExplosion, pos);
    }
}
