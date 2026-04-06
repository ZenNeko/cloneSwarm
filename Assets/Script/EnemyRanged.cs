using UnityEngine;

/// <summary>
/// Add-on behaviour: enemy ยืนอยู่ห่าง player และยิงกระสุน bullet-hell ทุก fireCooldown วินาที
/// ต้องแนบกับ GameObject ที่มี Enemy.cs
/// Server-only logic
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyRanged : MonoBehaviour
{
    [Header("Positioning")]
    [Tooltip("ระยะที่ต้องการอยู่ห่าง player")]
    public float preferredDist = 8f;
    [Tooltip("ถ้า player เข้าใกล้กว่านี้ จะถอยหลัง")]
    public float retreatDist   = 5f;

    [Header("Projectile")]
    [Tooltip("Prefab ที่มี EnemyProjectile.cs + NetworkObject + Trigger Collider")]
    public GameObject projectilePrefab;
    public float shotDamage   = 15f;
    public float shotSpeed    = 8f;
    public float shotRange    = 16f;

    [Header("Bullet Hell Pattern")]
    [Tooltip("จำนวนกระสุนต่อการยิงหนึ่งครั้ง")]
    [Range(1, 9)] public int   shotCount     = 3;
    [Tooltip("มุม spread รวมระหว่างกระสุนซ้ายสุดถึงขวาสุด (องศา)")]
    public float spreadAngle  = 30f;
    [Tooltip("หน่วงเวลาระหว่างกระสุนแต่ละลูกใน burst เดียวกัน (วินาที)")]
    public float burstDelay   = 0.08f;
    [Tooltip("เวลารอระหว่าง burst (วินาที)")]
    public float fireCooldown = 2.5f;

    private Enemy enemy;
    private float fireTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void Start()
    {
        if (enemy == null || !enemy.IsServer) return;
        enemy.suppressDefaultMovement = true;
    }

    void Update()
    {
        if (enemy == null || !enemy.IsServer) return;

        Transform target = GetNearestPlayer();
        if (target == null) return;

        HandleMovement(target);

        fireTimer += Time.deltaTime;
        if (fireTimer >= fireCooldown)
        {
            fireTimer = 0f;
            StartCoroutine(FireBurst(target));
        }
    }

    void HandleMovement(Transform target)
    {
        float dist = Vector3.Distance(transform.position, target.position);

        if (dist < retreatDist)
        {
            // ถอยออกจาก player
            Vector3 away = (transform.position - target.position).normalized;
            away.y = 0f;
            transform.position += away * enemy.speed * Time.deltaTime;
        }
        else if (dist > preferredDist + 1f)
        {
            // เดินเข้าหาจนถึง preferredDist
            Vector3 toward = (target.position - transform.position).normalized;
            toward.y = 0f;
            transform.position += toward * enemy.speed * Time.deltaTime;
        }
    }

    System.Collections.IEnumerator FireBurst(Transform originalTarget)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[EnemyRanged] projectilePrefab ยังไม่ได้ assign!");
            yield break;
        }

        // คำนวณทิศยิงจาก snapshot ณ ตอนเริ่ม burst
        Transform target = originalTarget != null ? originalTarget : GetNearestPlayer();
        if (target == null) yield break;

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 baseDir = (target.position + Vector3.up * 0.8f - origin).normalized;
        baseDir.y = 0f;
        if (baseDir == Vector3.zero) baseDir = transform.forward;

        float halfSpread = (shotCount > 1) ? spreadAngle * 0.5f : 0f;
        float step       = (shotCount > 1) ? spreadAngle / (shotCount - 1) : 0f;

        for (int i = 0; i < shotCount; i++)
        {
            float  angle = -halfSpread + i * step;
            Vector3 dir  = Quaternion.Euler(0f, angle, 0f) * baseDir;

            SpawnProjectile(origin, dir);

            if (burstDelay > 0f && i < shotCount - 1)
                yield return new WaitForSeconds(burstDelay);
        }
    }

    void SpawnProjectile(Vector3 origin, Vector3 direction)
    {
        Quaternion rot = direction != Vector3.zero
            ? Quaternion.LookRotation(direction)
            : Quaternion.identity;

        var go = Object.Instantiate(projectilePrefab, origin, rot);
        var proj = go.GetComponent<EnemyProjectile>();
        var no   = go.GetComponent<Unity.Netcode.NetworkObject>();

        if (proj == null || no == null) { Object.Destroy(go); return; }

        proj.damage   = shotDamage;
        proj.speed    = shotSpeed;
        proj.maxRange = shotRange;

        no.Spawn(true);
        proj.Init(direction);
    }

    Transform GetNearestPlayer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null) return null;

        Transform nearest = null;
        float     minDist = float.MaxValue;
        foreach (var c in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
        {
            var obj = c.PlayerObject;
            if (obj == null) continue;
            float d = Vector3.Distance(transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj.transform; }
        }
        return nearest;
    }
}
