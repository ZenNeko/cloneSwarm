using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    public float   speed    = 12f;
    public float   damage   = 20f;
    public float   maxRange = 20f;
    public bool    piercing = false;   // ถ้า true = ไม่ destroy เมื่อชน enemy

    [Header("VFX")]
    [Tooltip("Particle prefab ที่ spawn เมื่อชน — ตั้งค่าบน Projectile prefab แต่ละอัน\n" +
             "ปล่อยว่าง = ใช้ hitVFX (procedural VFXFactory)")]
    public GameObject hitVfxPrefab;
    [Tooltip("Fallback เมื่อไม่มี hitVfxPrefab")]
    public VFXType    hitVFX = VFXType.BulletHit;

    private Transform target;
    private Vector3   moveDirection;
    private Vector3   startPosition;

    // ── Init ──────────────────────────────────────────────────────────────
    /// <summary>Homing mode — ติดตาม target</summary>
    public void Init(Transform t)
    {
        target        = t;
        moveDirection = Vector3.zero;
        startPosition = transform.position;
    }

    /// <summary>Direction mode — บินตรง ไม่ homing</summary>
    public void InitDirection(Vector3 direction)
    {
        moveDirection = direction.normalized;
        target        = null;
        startPosition = transform.position;
    }

    // ── Update: Server only ───────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection * speed * Time.deltaTime;
        }
        else
        {
            if (target == null) { SafeDespawn(); return; }
            // ล็อค Y ให้อยู่ระดับเดิม — ป้องกัน homing ดิ่งลงพื้นเมื่อ enemy อยู่ต่ำกว่า spawnPos
            Vector3 targetFlat = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetFlat, speed * Time.deltaTime);
        }

        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            SafeDespawn();
    }

    // ── Collision ─────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;
        if (!other.CompareTag("Enemy")) return;

        other.GetComponent<Enemy>()?.EnemyTakeDamage(damage);
        ShowHitVfxClientRpc(transform.position);
        if (!piercing) SafeDespawn();
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowHitVfxClientRpc(Vector3 pos)
    {
        if (hitVfxPrefab != null)
            PlayAndDestroy(hitVfxPrefab, pos);
        else
            VFXFactory.Play(hitVFX, pos);
    }

    static void PlayAndDestroy(GameObject prefab, Vector3 pos)
    {
        var go = Object.Instantiate(prefab, pos, Quaternion.identity);
        var ps = go.GetComponent<ParticleSystem>();
        float ttl = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax + 0.5f
            : 3f;
        Object.Destroy(go, ttl);
    }
}
