using UnityEngine;

/// <summary>
/// Big Cannon — ยิงลูกปืนขนาดใหญ่ ดาเมจสูง ทะลุ enemy
///
/// กลไก:
///   • OnFire → FireProjectile ลูกเดียว piercing ขนาดใหญ่
///   • AimMode: AutoNearest
///
/// Level data แนะนำ:
///   Lv1: dmg=80,  cd=3.0s, count=1, range=15, speed=8
///   Lv2: dmg=100, cd=2.8s, count=1, range=16, speed=9
///   Lv3: dmg=130, cd=2.5s, count=1, range=17, speed=10
///   Lv4: dmg=160, cd=2.2s, count=1, range=18, speed=11
///   Lv5: dmg=200, cd=2.0s, count=1, range=20, speed=12
/// </summary>
public class BigCannonWeapon : WeaponBase
{
    [Header("Big Cannon Settings")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    protected override void OnFire(WeaponLevelData ld)
    {
        // สุ่มทิศทางจาก 8 ทิศ (มุมเพิ่มทีละ 45 องศา)
        float randomAngle = Random.Range(0, 8) * 45f;
        Vector3 dir = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f - dir * ld.range;
        float   dmg = RollDamage(ld.damage, out bool isCrit);

        FireProjectile(spawnPos, dir, dmg, ld.projectileSpeed,
            count: 1,
            spreadDeg: 0f,
            piercing: true,
            maxRange: ld.range * 3f,
            isCrit: isCrit);
    }
}
