using UnityEngine;

/// <summary>
/// Tri-Rang — Super version ของ Boomerang
/// ยิง 3 Boomerang พร้อมกัน spread 30° + ระเบิด AoE เมื่อกลับถึงผู้เล่น
///
/// Super tier — 1 level
///   dmg=120, cd=2.0s, range=14, projectileSpeed=16, count=3
///
/// Fusion: Tri-Rang + StarRing = SatelliteRingWeapon
/// </summary>
public class TriRangWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + BoomerangProjectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    [Tooltip("มุม spread รวม (องศา)")]
    public float spreadAngle = 30f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg   = RollDamage(ld.damage, out bool isCrit);
        float range = ld.range;
        float speed = ld.projectileSpeed > 0f ? ld.projectileSpeed : 16f;
        int   count = Mathf.Max(1, ld.projectileCount);

        Vector3 spawnPos = transform.position + Vector3.up * 0.8f;
        Vector3 dir      = GetAimDirection();

        float halfSpread = count > 1 ? spreadAngle * 0.5f : 0f;
        float step       = count > 1 ? spreadAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float   angle   = -halfSpread + i * step;
            Vector3 fireDir = Quaternion.Euler(0f, angle, 0f) * dir;
            SpawnBoomerang(spawnPos, fireDir, dmg, speed, range, isCrit);
        }
    }
}
