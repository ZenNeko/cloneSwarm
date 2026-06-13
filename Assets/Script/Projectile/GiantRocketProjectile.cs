using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Giant Rocket Projectile — Gunner E
/// ✦ บินแนวนอนจากผู้เล่นตามทิศที่คำนวณบน client
/// ✦ ระเบิด AoE เฉพาะเมื่อชน Enemy
/// ✦ หมดระยะ (maxRange) = หายไป ไม่ระเบิด
/// ✦ Damage = baseDamage × (1 + missingHP% × maxBonusMultiplier)
/// </summary>
public class GiantRocketProjectile : NetworkBehaviour
{
    [HideInInspector] public float baseDamage;
    [HideInInspector] public float moveSpeed;
    [HideInInspector] public float maxRange;
    [HideInInspector] public float explosionRadius;
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager weaponManager;

    public float maxBonusMultiplier = 2f;

    [Tooltip("VFX ที่แสดงเมื่อระเบิด — prefab กำหนดใน NetworkedVFXPool.vfxTypeMappings")]
    [VFXKey]
    public string explosionVfxType = "GrenadeExplosion";

    private Vector3 direction;
    private Vector3 spawnPos;
    private bool    hasExploded;

    public void InitDirection(Vector3 spawn, Vector3 dir)
    {
        spawnPos  = spawn;
        direction = dir;   // horizontal + normalized แล้วจาก server
    }

    // ── Update (Server only) ──────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || hasExploded) return;

        transform.position += direction * moveSpeed * Time.deltaTime;

        // หมดระยะ → ระเบิด AoE
        if (Vector3.Distance(spawnPos, transform.position) >= maxRange)
            Explode(transform.position);
    }

    // ── ระเบิดเมื่อชน Enemy เท่านั้น ──────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hasExploded) return;
        if (other.GetComponent<Enemy>() == null) return;

        Explode(transform.position);
    }

    void Explode(Vector3 center)
    {
        if (hasExploded) return;
        hasExploded = true;

        var cols = PlayerWeaponManager.OverlapEnemy(center, explosionRadius);
        foreach (var c in cols)
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy == null) continue;

            float missingFrac = Mathf.Clamp01(1f - enemy.GetHealthPercent());
            float finalDamage = baseDamage * (1f + missingFrac * maxBonusMultiplier);
            enemy.EnemyTakeDamage(finalDamage);
            if (weaponManager != null)
            {
                weaponManager.RegisterWeaponDamage(weaponName, finalDamage);
            }
        }

        ShowExplosionClientRpc(center, explosionRadius);

        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowExplosionClientRpc(Vector3 pos, float radius)
    {
        VFXFactory.Play("HitEffect", pos);
        VFXFactory.Play(explosionVfxType, pos);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(spawnPos, direction * maxRange);
    }
#endif
}
