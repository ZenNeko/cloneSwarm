using Unity.Netcode;
using UnityEngine;

public class PlayerWeapon : NetworkBehaviour
{
    public GameObject projectilePrefab;
    public float attackRange          = 8f;
    public float attackSpeed          = 1f;
    public float damage               = 20f;
    public float projectileSpeed      = 12f;
    public int   multiProjectileCount = 1;
    [Tooltip("มุมกระจายระหว่าง projectile (องศา)")]
    public float spreadAngle          = 15f;

    private float attackTimer;

    // ── Update: Owner ตัดสินใจยิง, Server spawn projectile ─────────────────
    void Update()
    {
        if (!IsOwner) return;

        attackTimer += Time.deltaTime;
        if (attackTimer < 1f / attackSpeed) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        NetworkObject targetNet = target.GetComponent<NetworkObject>();
        if (targetNet == null) return;

        attackTimer = 0f;

        // ส่งข้อมูลยิงไปให้ Server spawn projectile จริง
        ShootServerRpc(
            targetNet.NetworkObjectId,
            transform.position + Vector3.up * 0.5f,
            damage,
            projectileSpeed,
            multiProjectileCount,
            spreadAngle
        );
    }

    // ── Server: Spawn Projectiles ─────────────────────────────────────────
    [ServerRpc]
    void ShootServerRpc(
        ulong targetNetId, Vector3 spawnPos,
        float dmg, float projSpeed, int projCount, float spread)
    {
        // หา target ใน NetworkSpawnManager
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out NetworkObject targetObj))
            return;

        Transform  target    = targetObj.transform;
        Vector3    targetDir = (target.position - spawnPos).normalized;
        int        count     = Mathf.Max(1, projCount);

        for (int i = 0; i < count; i++)
        {
            float   angle = (i - (count - 1) * 0.5f) * spread;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * targetDir;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            proj.GetComponent<NetworkObject>()?.Spawn(true);

            Projectile p = proj.GetComponent<Projectile>();
            if (p == null) continue;

            p.damage = dmg;
            p.speed  = projSpeed;

            if (i == 0) p.Init(target);       // กลาง — homing
            else        p.InitDirection(dir);  // ข้างๆ — บินตรง
        }
    }

    // ── Find Nearest Enemy ────────────────────────────────────────────────
    Transform FindNearestEnemy()
    {
        Enemy[]   enemies = FindObjectsOfType<Enemy>();
        Transform nearest = null;
        float     minDist = attackRange;

        foreach (Enemy e in enemies)
        {
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < minDist) { minDist = dist; nearest = e.transform; }
        }
        return nearest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
