using UnityEngine;

/// <summary>
/// Railgun — Super Laser
/// Instant pierce raycast — รับผลจาก projectileCount
/// projectileCount > 1 → กระจาย beams 360°/count
///
/// Level data (Super tier, 1 level):
///   dmg=200, cd=4.0s, range=50 (full screen)
/// </summary>
public class RailgunWeapon : WeaponBase
{
    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 origin    = transform.position + Vector3.up * 0.5f;
        Vector3 dir       = GetAimDirection();
        float   dmg       = RollDamage(ld.damage, out bool isCrit);
        int     beamCount = Mathf.Max(1, ld.projectileCount);

        float angleStep = 360f / beamCount;

        for (int i = 0; i < beamCount; i++)
        {
            Vector3 beamDir = Quaternion.Euler(0f, i * angleStep, 0f) * dir;
            manager.FireRaycastServerRpc(origin, beamDir, dmg, maxDist: ld.range, vfxKey: ResolveHitVfx("Beam_Railgun"), isCrit: isCrit);
        }
    }
}
