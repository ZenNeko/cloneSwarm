using UnityEngine;

/// <summary>
/// Pistol — MouseAim, Single/Multi projectile
///
/// LevelData example:
///   Lv1: dmg=20, cd=1.0s, count=1
///   Lv2: dmg=25, cd=0.9s, count=1
///   Lv3: dmg=30, cd=0.85s, count=2
///   Lv4: dmg=38, cd=0.8s,  count=2
///   Lv5: dmg=48, cd=0.7s,  count=3
/// </summary>
public class PistolWeapon : WeaponBase
{
    [Tooltip("มุมกระจายระหว่าง projectile (องศา) — ใช้เมื่อ count > 1")]
    public float spreadAngle = 15f;

    protected override void OnInit()
    {
        aimMode = AimMode.MouseAim;
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        Vector3 dir      = GetAimDirection();

        // ld ถูก apply stat แล้วจาก BuildEffectiveLevelData
        // RollDamage ตรวจ crit chance อีกครั้ง
        float dmg = RollDamage(ld.damage);

        manager.FireProjectileServerRpc(
            spawnPos, dir, dmg,
            ld.projectileSpeed,
            ld.projectileCount,
            spreadAngle
        );
    }
}
