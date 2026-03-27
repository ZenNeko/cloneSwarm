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

    private float      attackTimer;
    private playermove playerMove;

    public override void OnNetworkSpawn()
    {
        playerMove = GetComponent<playermove>();
    }

    // ── Update: Owner ตัดสินใจยิง, Server spawn projectile ─────────────────
    void Update()
    {
        if (!IsOwner) return;
        if (playerMove != null && playerMove.isDead.Value) return;

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
        // Flatten ให้อยู่บนระนาบ XZ ก่อนเสมอ — ป้องกัน projectile จมดิน
        // เมื่อ enemy อยู่ใกล้และต่ำกว่า spawnPos ทำให้ targetDir.y ติดลบ
        Vector3 flat = target.position - spawnPos;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f) flat = transform.forward;   // กรณี enemy อยู่ใต้ตัวพอดี
        Vector3    targetDir = flat.normalized;
        int        count     = Mathf.Max(1, projCount);

        for (int i = 0; i < count; i++)
        {
            float   angle = (i - (count - 1) * 0.5f) * spread;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * targetDir;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));

            Projectile p = proj.GetComponent<Projectile>();
            if (p == null) { Destroy(proj); continue; }

            // ✅ Init ก่อน Spawn เสมอ — ป้องกัน Update รันก่อน target ถูกตั้ง
            p.damage = dmg;
            p.speed  = projSpeed;

            // count=1 → homing ตาม target
            // count>1 → ทุกลูกยิงตรง (direction) ป้องกันทับซ้อน
            if (count == 1) p.Init(target);
            else            p.InitDirection(dir);

            proj.GetComponent<NetworkObject>()?.Spawn(true);
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
