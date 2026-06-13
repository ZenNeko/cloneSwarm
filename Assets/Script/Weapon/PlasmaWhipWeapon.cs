using UnityEngine;

/// <summary>
/// Plasma Whip â€” FUSION: Railgun (Super Laser) + Chainsaw (Super Whip)
///
/// à¸à¸¥à¹„à¸:
///   â€¢ Spin AoE à¸£à¸­à¸šà¸•à¸±à¸§ (à¹€à¸«à¸¡à¸·à¸­à¸™ Chainsaw à¹à¸•à¹ˆ radius à¹ƒà¸«à¸à¹ˆà¸à¸§à¹ˆà¸²)
///   â€¢ à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™ à¸¢à¸´à¸‡ Raycast 4 à¸—à¸´à¸¨ (N/S/E/W)
///   â€¢ Cooldown à¸›à¸²à¸™à¸à¸¥à¸²à¸‡
///
/// Level data (Fusion tier, 1 level):
///   dmg=90, cd=1.0s, count=4 (ray à¸ˆà¸³à¸™à¸§à¸™), range=6 (whip+ray range)
/// </summary>
public class PlasmaWhipWeapon : WeaponBase
{
    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   dmg    = RollDamage(ld.damage, out bool isCrit);
        float   range  = ld.range;

        if (manager.statManager != null)
            range *= manager.statManager.GetAreaMultiplier();

        // 1. Melee spin à¸£à¸­à¸šà¸•à¸±à¸§ â€” Enemy.cs spawn HitEffect à¹€à¸­à¸‡à¸•à¸­à¸™ TakeDamage
        FireMelee(center, range, dmg * 0.6f);

        // 2. Raycast N à¸—à¸´à¸¨ à¸•à¸²à¸¡à¸ˆà¸³à¸™à¸§à¸™ projectileCount
        int rays = Mathf.Max(1, ld.projectileCount);
        for (int i = 0; i < rays; i++)
        {
            float   angle = (360f / rays) * i;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            FireRaycast(center, dir, dmg, range, isCrit: isCrit);
        }
    }
}
