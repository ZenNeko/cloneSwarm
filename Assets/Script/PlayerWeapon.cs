using UnityEngine;

public class PlayerWeapon : MonoBehaviour
{
    public GameObject projectilePrefab;
    public float attackRange = 8f;
    public float attackSpeed = 1f; // จำนวนยิงต่อวินาที
    public float damage = 20f;

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

        // spawn กลางตัว player เล็กน้อย
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        GameObject proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

        Projectile p = proj.GetComponent<Projectile>();
        if (p != null)
        {
            p.damage = damage;
            p.Init(target);
        }
    }

    // แสดง attack range ใน Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
