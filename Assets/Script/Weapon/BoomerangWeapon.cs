using UnityEngine;

/// <summary>
/// Boomerang â€” à¸¢à¸´à¸‡ projectile à¸­à¸­à¸à¹„à¸› maxRange à¹à¸¥à¹‰à¸§à¸šà¸´à¸™à¸à¸¥à¸±à¸š, pierce à¸—à¸¸à¸ enemy
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=40, cd=2.0s, range=8,  projectileSpeed=14
///   Lv2: dmg=52, cd=1.8s, range=9,  projectileSpeed=14
///   Lv3: dmg=65, cd=1.6s, range=10, projectileSpeed=15
///   Lv4: dmg=78, cd=1.4s, range=11, projectileSpeed=15
///   Lv5: dmg=90, cd=1.3s, range=12, projectileSpeed=16
///
/// Super: TriRangWeapon (3 boomerangs spread 30Â°)
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
        Vector3 dir      = GetAimDirection();   // à¹ƒà¸Šà¹‰ WeaponBase.GetAimDirection() â€” à¸£à¸­à¸‡à¸£à¸±à¸š MouseAim + AutoNearest

        // à¸¢à¸´à¸‡ 1 boomerang (projectileCount=1 à¸—à¸µà¹ˆ Lv1-5 à¸ˆà¸²à¸ level data)
        // TriRang Super à¸ˆà¸° override à¹€à¸›à¹‡à¸™ 3 à¸¥à¸¹à¸
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
