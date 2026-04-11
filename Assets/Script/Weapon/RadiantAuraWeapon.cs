using UnityEngine;

/// <summary>
/// Radiant Aura — passive damage field รอบตัวผู้เล่น ไม่ต้อง aim
///
/// Level data แนะนำ:
///   Lv1: dmg=8,  cd=1.0s, range=2.5
///   Lv2: dmg=11, cd=0.9s, range=2.8
///   Lv3: dmg=14, cd=0.8s, range=3.2
///   Lv4: dmg=18, cd=0.7s, range=3.6
///   Lv5: dmg=22, cd=0.6s, range=4.0
///
/// Super: DeathFieldWeapon (range ใหญ่ + enemy ที่ตายระเบิด)
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

        manager.FireMeleeServerRpc(center, radius, dmg);
        ShowVfx(VFXType.OrbiterHit, center, radius, isCrit);
    }
}
