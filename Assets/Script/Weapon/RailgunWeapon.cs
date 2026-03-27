using UnityEngine;

/// <summary>
/// Railgun — Super Laser
/// Instant pierce raycast เต็มหน้าจอ, cooldown ยาว, damage สูงมาก
///
/// Level data (Super tier, 1 level):
///   dmg=200, cd=4.0s, range=50 (full screen)
/// </summary>
public class RailgunWeapon : WeaponBase
{
    protected override void OnInit() => aimMode = AimMode.MouseAim;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = GetAimDirection();
        float   dmg    = RollDamage(ld.damage);

        manager.FireRaycastServerRpc(origin, dir, dmg, maxDist: ld.range);
    }
}
