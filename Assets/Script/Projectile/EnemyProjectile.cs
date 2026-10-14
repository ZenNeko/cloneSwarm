using Unity.Netcode;
using UnityEngine;

/// <summary>
/// กระสุนของ Enemy — บินตรง ชน player → ดาเมจ → despawn
/// ต้องมี Collider (Is Trigger) และ NetworkObject บน prefab
/// </summary>
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Stats")]
    public float speed    = 8f;
    public float damage   = 15f;
    public float maxRange = 16f;

    private Vector3 moveDir;
    private Vector3 startPos;

    // ── Init (Server เรียกหลัง Spawn) ────────────────────────────────────
    public void Init(Vector3 direction)
    {
        moveDir  = direction.normalized;
        startPos = transform.position;
    }

    /// <summary>
    /// ท่าบอสตั้งค่ากระสุนเองต่อนัด (ProjectilePatternAction) — prefab เดียวใช้ได้หลายท่า
    /// เรียกก่อน Spawn ได้ · หันหัวกระสุนตามทิศให้ด้วย (กระสุนยาวจะได้ไม่บินเฉียง)
    /// </summary>
    public void Init(Vector3 direction, float newSpeed, float newDamage, float newMaxRange)
    {
        Init(direction);
        speed    = newSpeed;
        damage   = newDamage;
        maxRange = newMaxRange;
        if (moveDir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(moveDir);
    }

    // ── Movement (Server only) ────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        transform.position += moveDir * speed * Time.deltaTime;

        if (Vector3.Distance(startPos, transform.position) >= maxRange)
            SafeDespawn();
    }

    // ── Collision (Server) ────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        var player = other.GetComponent<playermove>();
        if (player == null) return;

        player.TakeDamage(damage);
        SafeDespawn();
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
