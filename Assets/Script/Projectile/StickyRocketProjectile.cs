using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sticky Rocket Projectile — Gunner Rocket Mode (Q)
/// ✦ เคลื่อนไปข้างหน้า
/// ✦ OnTriggerEnter → ระเบิด AoE ทันทีที่ชน Enemy + Despawn
///
/// ต้องมี:
///   - NetworkObject component
///   - Rigidbody (Is Kinematic = true — เคลื่อนด้วย code)
///   - Collider (Is Trigger = true)
/// </summary>
public class StickyRocketProjectile : NetworkBehaviour
{
    [HideInInspector] public float damage;
    [HideInInspector] public float moveSpeed;
    [HideInInspector] public float explosionRadius;

    [Header("VFX")]
    [Tooltip("Index ใน NetworkedVFXPool.vfxEntries สำหรับ explosion VFX (-1 = ไม่มี)")]
    public int explosionVfxId = 0;

    private Vector3 direction;
    private bool    hasExploded;

    public void InitDirection(Vector3 dir)
    {
        direction = dir.normalized;
    }

    void Update()
    {
        if (!IsServer || hasExploded) return;
        transform.position += direction * moveSpeed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hasExploded) return;
        if (other.GetComponent<Enemy>() == null) return;

        Explode();
    }

    void Explode()
    {
        hasExploded = true;

        Vector3 center = transform.position;

        var cols = PlayerWeaponManager.OverlapEnemy(center, explosionRadius);
        foreach (var c in cols)
            c.GetComponent<Enemy>()?.EnemyTakeDamage(damage);

        ShowExplosionClientRpc(center, explosionRadius);

        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowExplosionClientRpc(Vector3 pos, float radius)
    {
        if (explosionVfxId < 0) return;
        NetworkedVFXPool.Instance?.PlayFromPool(explosionVfxId, pos, radius / 2f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
#endif
}
