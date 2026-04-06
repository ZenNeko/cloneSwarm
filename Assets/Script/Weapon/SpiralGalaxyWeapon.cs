using UnityEngine;

/// <summary>
/// Spiral Galaxy — Super version ของ Vortex
/// 2 streams หมุนสวนทางกัน (CW + CCW) พร้อมกัน
///
/// Super tier — 1 level
///   dmg=40, cd=0.20s, range=18, projectileSpeed=14
///
/// Fusion: Spiral Galaxy + OrbitalCannon = CosmicStormWeapon
/// </summary>
public class SpiralGalaxyWeapon : VortexWeapon
{
    [Tooltip("องศา/วินาที ของ stream ที่ 2 (ค่าลบ = สวนทาง)")]
    public float reverseRotSpeed = -140f;

    private float reverseAngle;

    protected override void OnFire(WeaponLevelData ld)
    {
        // Stream 1: CW (ใช้ base class orbitAngle)
        orbitAngle += rotSpeed * ld.cooldown;
        SpawnOrbAt(orbitAngle, ld);

        // Stream 2: CCW
        reverseAngle += reverseRotSpeed * ld.cooldown;
        SpawnOrbAt(reverseAngle, ld);
    }
}
