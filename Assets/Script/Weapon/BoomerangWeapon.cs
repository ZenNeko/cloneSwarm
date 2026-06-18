using UnityEngine;

/// <summary>
/// Boomerang — ยิง projectile ออกไป maxRange แล้วบินกลับ, pierce ทุก enemy
///
/// Level data แนะนำ:
///   Lv1: dmg=40, cd=2.0s, range=8,  projectileSpeed=14
///   Lv2: dmg=52, cd=1.8s, range=9,  projectileSpeed=14
///   Lv3: dmg=65, cd=1.6s, range=10, projectileSpeed=15
///   Lv4: dmg=78, cd=1.4s, range=11, projectileSpeed=15
///   Lv5: dmg=90, cd=1.3s, range=12, projectileSpeed=16
///
/// Super: TriRangWeapon (3 boomerangs spread 30°)
/// Fusion: Tri-Rang + StarRing = SatelliteRingWeapon
/// </summary>
public class BoomerangWeapon : WeaponBase
{
    protected override void OnFire(WeaponLevelData ld)
    {
        float   dmg   = RollDamage(ld.damage, out bool isCrit);
        float   range = ld.range;
        float   speed = ld.projectileSpeed > 0f ? ld.projectileSpeed : 14f;

        if (manager.statManager != null)
        {
            dmg   *= manager.statManager.GetPowerMultiplier();
            range *= manager.statManager.GetAreaMultiplier();
        }

        Vector3 spawnPos = transform.position + Vector3.up * 0.8f;
        Vector3 dir      = GetAimDirection();   // ใช้ WeaponBase.GetAimDirection() — รองรับ MouseAim + AutoNearest

        // ยิง 1 boomerang (projectileCount=1 ที่ Lv1-5 จาก level data)
        // TriRang Super จะ override เป็น 3 ลูก
        int count = Mathf.Max(1, ld.projectileCount);
        float spreadStep = count > 1 ? 30f / (count - 1) : 0f;
        float startAngle = count > 1 ? -15f : 0f;

        for (int i = 0; i < count; i++)
        {
            float   angle   = startAngle + i * spreadStep;
            Vector3 fireDir = Quaternion.Euler(0f, angle, 0f) * dir;
            SpawnBoomerang(spawnPos, fireDir, dmg, speed, range, isCrit);
        }
    }
}
