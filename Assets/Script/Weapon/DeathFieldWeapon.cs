using UnityEngine;

/// <summary>
/// Death Field — Super version ของ Radiant Aura
/// field ใหญ่ขึ้น + enemy ที่ตายใน field ระเบิด mini AoE รอบตัว
///
/// Super tier — 1 level เท่านั้น
///   dmg=30/tick, cd=0.5s, range=6.0
///
/// Fusion: Death Field + Minefield = ExplosiveAuraWeapon
/// </summary>
public class DeathFieldWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิดเมื่อ enemy ตายใน field")]
    public float deathExplosionRadius = 3f;
    [Tooltip("ดาเมจของ mini explosion")]
    public float deathExplosionDamage = 40f;

    protected override void OnInit()
    {
        Enemy.OnAnyEnemyDiedAt += OnEnemyDiedAt;
    }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyDiedAt -= OnEnemyDiedAt;
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   radius = ld.range;
        float   dmg    = RollDamage(ld.damage, out bool isCrit);

        if (manager.statManager != null)
        {
            radius *= manager.statManager.GetAreaMultiplier();
            dmg    *= manager.statManager.GetPowerMultiplier();
        }

        manager.FireMeleeServerRpc(center, radius, dmg);
        ShowVfx(VFXType.OrbiterHit, center, radius, isCrit);
    }

    void OnEnemyDiedAt(Vector3 deathPos)
    {
        if (manager == null || !manager.IsOwner) return;

        var ld = data?.GetLevelData(currentLevel);
        if (ld == null) return;

        float fieldRadius = ld.range * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);

        // ระเบิดเฉพาะ enemy ที่ตายอยู่ใน field ของเรา
        float dist = Vector3.Distance(transform.position, deathPos);
        if (dist > fieldRadius) return;

        Vector3 explosionCenter = deathPos + Vector3.up * 0.5f;
        manager.FireMeleeServerRpc(explosionCenter, deathExplosionRadius, deathExplosionDamage);
        ShowVfx(VFXType.GrenadeExplosion, explosionCenter, deathExplosionRadius);
    }
}
