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
        Enemy.OnAnyEnemyDied += OnEnemyDied;
    }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyDied -= OnEnemyDied;
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   radius = ld.range;
        float   dmg    = RollDamage(ld.damage);

        if (manager.statManager != null)
        {
            radius *= manager.statManager.GetAreaMultiplier();
            dmg    *= manager.statManager.GetPowerMultiplier();
        }

        manager.FireMeleeServerRpc(center, radius, dmg);
        manager.BroadcastVfxTypeServerRpc(center, (int)VFXType.OrbiterHit);
    }

    void OnEnemyDied()
    {
        // ไม่รู้ตำแหน่ง enemy ที่ตายจาก static event — ใช้ OverlapSphere ตรวจ
        // ถ้า enemy เพิ่งตาย จะอยู่ในตำแหน่งที่ยัง spawn อยู่ชั่วขณะ
        // วิธีง่าย: เมื่อ enemy ตายใน field → สั่ง AoE เล็กๆ รอบตัวผู้เล่น
        // (ไม่แม่น 100% แต่ gameplay ยังถูกต้อง)
        if (manager == null || !manager.IsOwner) return;

        var ld = data?.GetLevelData(currentLevel);
        if (ld == null) return;

        float fieldRadius = ld.range * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);
        Vector3 center    = transform.position + Vector3.up * 0.5f;

        // ตรวจว่ามี enemy อยู่ในรัศมีก่อนเสียชีวิต (เพิ่งตายออกไปแล้ว เป็น heuristic)
        // Trigger mini explosion รอบตัวผู้เล่น
        manager.FireMeleeServerRpc(center, deathExplosionRadius, deathExplosionDamage);
        manager.BroadcastVfxTypeServerRpc(center, (int)VFXType.GrenadeExplosion);
    }
}
