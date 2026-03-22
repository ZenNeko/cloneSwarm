using UnityEngine;

public class PlayerWeapon : MonoBehaviour
{
    public GameObject projectilePrefab;
    public float attackRange       = 8f;
    public float attackSpeed       = 1f;    // จำนวนยิงต่อวินาที
    public float damage            = 20f;
    public float projectileSpeed   = 12f;   // ส่งให้ Projectile ตอน spawn
    public int   multiProjectileCount = 1;  // จำนวน projectile ต่อการยิง
    [Tooltip("มุมกระจายระหว่าง projectile (องศา)")]
    public float spreadAngle       = 15f;   // องศาระหว่าง projectile แต่ละลูก

    private float attackTimer;

    void Update()
    {
        attackTimer += Time.deltaTime;

        if (attackTimer >= 1f / attackSpeed)
        {
            Transform target = FindNearestEnemy();
            if (target != null)
            {
                Shoot(target);
                attackTimer = 0f;
            }
        }
    }

    Transform FindNearestEnemy()
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        Transform nearest = null;
        float minDist = attackRange;

        foreach (Enemy e in enemies)
        {
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = e.transform;
            }
        }

        return nearest;
    }

    void Shoot(Transform target)
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos  = transform.position + Vector3.up * 0.5f;
        Vector3 targetDir = (target.position - spawnPos).normalized;

        int count = Mathf.Max(1, multiProjectileCount);

        for (int i = 0; i < count; i++)
        {
            // คำนวณมุม: กระจาย symmetric รอบ targetDir
            float angle   = (i - (count - 1) * 0.5f) * spreadAngle;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * targetDir;

            GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            Projectile p    = proj.GetComponent<Projectile>();
            if (p == null) continue;

            p.damage = damage;
            p.speed  = projectileSpeed;

            if (i == 0)
                p.Init(target);          // กระสุนตรงกลาง — homing
            else
                p.InitDirection(dir);    // กระสุนข้างๆ — บินตรง
        }
    }

    // แสดง attack range ใน Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
