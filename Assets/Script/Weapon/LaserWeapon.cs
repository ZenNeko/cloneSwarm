using UnityEngine;

/// <summary>
/// Laser — MouseAim, Piercing projectile เร็ว
///
/// Level data แนะนำ:
///   Lv1: dmg=30, cd=2.0s, count=1, range=12, projSpeed=25, piercing=true
///   Lv2: dmg=38, cd=1.8s, count=1, range=14
///   Lv3: dmg=48, cd=1.6s, count=1, range=15
///   Lv4: dmg=60, cd=1.4s, count=2, range=16
///   Lv5: dmg=75, cd=1.2s, count=2, range=18, piercing=true
/// </summary>
public class LaserWeapon : WeaponBase
{
    protected override void OnInit() => aimMode = AimMode.MouseAim;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();
        float   dmg = RollDamage(ld.damage);

        manager.FireProjectileServerRpc(
            pos, dir, dmg,
            ld.projectileSpeed,
            ld.projectileCount,
            spreadDeg: 15f,
            piercing: true          // Laser เจาะทะลุเสมอ
        );
    }
}
