using Unity.Netcode;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    public float speed    = 12f;
    public float damage   = 20f;
    public float maxRange = 20f;

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
        if (!IsServer) return;

        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection * speed * Time.deltaTime;
        }
        else
        {
            if (target == null) { NetworkObject.Despawn(true); return; }
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        }

        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            NetworkObject.Despawn(true);
    }

    // ── Collision ─────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Enemy")) return;

        other.GetComponent<Enemy>()?.EnemyTakeDamage(damage);
        NetworkObject.Despawn(true);
    }
}
