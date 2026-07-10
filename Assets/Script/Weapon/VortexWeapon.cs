using UnityEngine;

/// <summary>
/// Vortex Stream — spawn projectile ต่อเนื่องที่ rotating angle รอบผู้เล่น
/// กระสุนออกเป็น spiral, ดาเมจทุก enemy ที่ผ่าน
///
/// ใช้ projectilePrefab จาก WeaponData (sphere/orb เล็กๆ)
///
/// Level data แนะนำ:
///   Lv1: dmg=15, cd=0.30s, range=12, projSpeed=10
///   Lv2: dmg=18, cd=0.27s, range=13, projSpeed=11
///   Lv3: dmg=22, cd=0.24s, range=14, projSpeed=12
///   Lv4: dmg=27, cd=0.21s, range=15, projSpeed=13
///   Lv5: dmg=35, cd=0.18s, range=16, projSpeed=14
///
/// Super: SpiralGalaxyWeapon (2 streams CW+CCW)
/// Fusion: Spiral Galaxy + OrbitalCannon = CosmicStormWeapon
/// </summary>
public class VortexWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;
    [Tooltip("องศา/วินาที ที่ angle หมุน (บวก = ทวนเข็ม)")]
    public float rotSpeed = 140f;

    [Tooltip("ระยะที่ spawn projectile ออกจากผู้เล่น")]
    public float spawnRadius = 1.2f;

    // ใช้เพื่อให้ subclass (SpiralGalaxy) เพิ่ม stream ที่ 2 ได้
    protected float orbitAngle;

    protected override void OnFire(WeaponLevelData ld)
    {
        // อัปเดต angle
        orbitAngle += rotSpeed * ld.cooldown;   // ล่วงหน้าตาม cooldown

        SpawnOrbAt(orbitAngle, ld);
    }

    protected void SpawnOrbAt(float angleDeg, WeaponLevelData ld)
    {
        float   dmg   = RollDamage(ld.damage, out bool isCrit);
        float   speed = ld.projectileSpeed > 0f ? ld.projectileSpeed : 10f;
        float   range = ld.range;

        float   rad      = Mathf.Deg2Rad * angleDeg;
        Vector3 offset   = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * spawnRadius;
        Vector3 spawnPos = transform.position + Vector3.up * 0.8f + offset;

        // ทิศยิง = tangent ของ orbit (หมุน 90° จาก radial)
        Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

        FireProjectile(spawnPos, tangent, dmg, speed, count: 1, spreadDeg: 0f, maxRange: range, isCrit: isCrit);
        ShowVfx(ResolveHitVfx("VortexSpawn"), spawnPos, isAttackHit: false);
    }
}
