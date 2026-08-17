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
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public bool  isCrit;

    [Tooltip("VFX ที่แสดงเมื่อระเบิด — prefab กำหนดใน NetworkedVFXPool.vfxTypeMappings")]
    [VFXKey]
    public string explosionVfxType = "GrenadeExplosion";

    private Vector3 direction;
    private bool    hasExploded;
    private float   maxLifetime = 6f; // ป้องกันกระสุนลอยค้างนอกแผนที่
    private float   lifeTimer;

    public void InitDirection(Vector3 dir)
    {
        direction = dir.normalized;
    }

    void Update()
    {
        if (!IsServer || hasExploded) return;
        transform.position += direction * moveSpeed * Time.deltaTime;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetime)
        {
            Explode();
        }
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
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                if (weaponManager != null)
                {
                    weaponManager.RegisterWeaponDamage(weaponName, damage);
                }
            }
        }

        ShowExplosionClientRpc(center, explosionRadius);

        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowExplosionClientRpc(Vector3 pos, float radius)
    {
        // ถอด HitEffect ที่ซ้ำซ้อนออก เหลือเพียงระเบิดลูกใหญ่
        string vfxKey = (NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.HasMapping(explosionVfxType))
            ? explosionVfxType
            : "GrenadeExplosion";
        VFXFactory.Play(vfxKey, pos);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
#endif
}
