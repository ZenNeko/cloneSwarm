using UnityEngine;

/// <summary>
/// Shotgun â€” MouseAim, spread pellets
/// à¹ƒà¸Šà¹‰à¹„à¸”à¹‰à¸—à¸±à¹‰à¸‡ Normal (Shotgun) à¹à¸¥à¸° Super (Blunderbuss)
///
/// Blunderbuss à¸•à¹ˆà¸²à¸‡à¸à¸±à¸™à¸—à¸µà¹ˆ:  explodeOnHit = true, explosionRadius à¹ƒà¸«à¸à¹ˆà¸‚à¸¶à¹‰à¸™
/// à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸²à¸œà¹ˆà¸²à¸™ WeaponData.levels[0].levelUpText à¸«à¸£à¸·à¸­ Inspector à¸‚à¸­à¸‡ prefab
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=12/pellet, cd=1.2s, count=4, range=7
///   Lv2: dmg=14/pellet, cd=1.1s, count=4, range=7
///   Lv3: dmg=16/pellet, cd=1.0s, count=5, range=8
///   Lv4: dmg=18/pellet, cd=0.95s, count=5, range=8
///   Lv5: dmg=22/pellet, cd=0.85s, count=6, range=9
/// </summary>
public class ShotgunWeapon : WeaponBase
{
    [Tooltip("à¸¡à¸¸à¸¡à¸à¸£à¸°à¸ˆà¸²à¸¢à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸” (à¸­à¸‡à¸¨à¸²) à¹€à¸Šà¹ˆà¸™ 40 = à¸à¸£à¸°à¸ˆà¸²à¸¢ 40Â° à¸£à¸§à¸¡")]
    public float spreadAngle = 40f;

    [Header("Blunderbuss Super")]
    [Tooltip("true = à¸à¸£à¸°à¸ªà¸¸à¸™à¸£à¸°à¹€à¸šà¸´à¸” AoE à¹€à¸¡à¸·à¹ˆà¸­à¸–à¸¶à¸‡à¸¨à¸±à¸•à¸£à¸¹ (à¹ƒà¸Šà¹‰à¸ªà¸³à¸«à¸£à¸±à¸š Super version)")]
    public bool  explodeOnHit    = false;
    public float explosionRadius = 2.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();

        // damage à¹à¸šà¹ˆà¸‡à¸•à¹ˆà¸­ pellet à¹à¸•à¹ˆà¸‚à¸±à¹‰à¸™à¸•à¹ˆà¸³ 1
        float dmgPerPellet = Mathf.Max(1f, ld.damage / Mathf.Max(1, ld.projectileCount));
        float pelletDmg    = RollDamage(dmgPerPellet, out bool isCrit);

        if (explodeOnHit)
        {
            // Blunderbuss: à¸¢à¸´à¸‡ 1 à¸à¸£à¸°à¸ªà¸¸à¸™à¸«à¸™à¸±à¸ â†’ à¸£à¸°à¹€à¸šà¸´à¸” AoE à¸šà¸™à¹€à¸›à¹‰à¸²à¸«à¸¡à¸²à¸¢
            // à¸ªà¸£à¹‰à¸²à¸‡à¹€à¸›à¹‡à¸™ grenade à¸—à¸µà¹ˆà¸šà¸´à¸™à¸•à¸£à¸‡ à¹à¸•à¹ˆà¸£à¸°à¹€à¸šà¸´à¸”à¸—à¸±à¸™à¸—à¸µà¸—à¸µà¹ˆà¸Šà¸™
            var targetPos = pos + dir * ld.range;
            ThrowGrenade(pos, targetPos, ld.damage, explosionRadius, fuseTime: 0.05f);
        }
        else
        {
            FireProjectile(pos, dir, pelletDmg, ld.projectileSpeed,
                ld.projectileCount,
                spreadAngle / Mathf.Max(1, ld.projectileCount - 1),
                isCrit: isCrit);
        }
    }
}
