using UnityEngine;

/// <summary>
/// Radiant Aura â€” passive damage field à¸£à¸­à¸šà¸•à¸±à¸§à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™ à¹„à¸¡à¹ˆà¸•à¹‰à¸­à¸‡ aim
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=8,  cd=1.0s, range=2.5
///   Lv2: dmg=11, cd=0.9s, range=2.8
///   Lv3: dmg=14, cd=0.8s, range=3.2
///   Lv4: dmg=18, cd=0.7s, range=3.6
///   Lv5: dmg=22, cd=0.6s, range=4.0
///
/// Super: DeathFieldWeapon (range à¹ƒà¸«à¸à¹ˆ + enemy à¸—à¸µà¹ˆà¸•à¸²à¸¢à¸£à¸°à¹€à¸šà¸´à¸”)
/// Fusion: Death Field + Minefield = ExplosiveAuraWeapon
/// </summary>
public class RadiantAuraWeapon : WeaponBase
{
    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   radius = ld.range;
        float   dmg    = RollDamage(ld.damage, out bool isCrit);

        if (manager.statManager != null)
        {
            radius *= manager.statManager.GetAreaMultiplier();
            dmg    *= manager.statManager.GetPowerMultiplier();
        }

        FireMelee(center, radius, dmg);
        if (!string.IsNullOrEmpty(weaponVfxType) && weaponVfxType != "None")
        {
            float scale = radius > 0f ? ComputeVfxScale(weaponVfxType, radius) : 1f;
            manager.BroadcastVfxParentedServerRpc(weaponVfxType, scale, isLoop: true);
        }
    }
}
