using UnityEngine;

/// <summary>
/// Minefield â€” Super Grenade
/// Auto-place grenade à¸£à¸­à¸šà¸•à¸±à¸§ player â†’ à¸£à¸°à¹€à¸šà¸´à¸”à¹à¸¥à¹‰à¸§à¸¡à¸µ child explosions (cluster)
///
/// Level data (Super tier, 1 level):
///   dmg=120, cd=2.5s, count=3 (à¸ˆà¸³à¸™à¸§à¸™ grenade à¸•à¹ˆà¸­ volley), range=6 (à¸§à¸²à¸‡à¹ƒà¸™ radius à¸™à¸µà¹‰)
/// </summary>
public class MinefieldWeapon : WeaponBase
{
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸£à¸°à¹€à¸šà¸´à¸”à¸‚à¸­à¸‡ grenade à¹à¸•à¹ˆà¸¥à¸°à¸¥à¸¹à¸")]
    public float explosionRadius = 3f;
    [Tooltip("à¹€à¸§à¸¥à¸² fuse à¸à¹ˆà¸­à¸™à¸£à¸°à¹€à¸šà¸´à¸” (à¸§à¸´à¸™à¸²à¸—à¸µ)")]
    public float fuseTime = 1.2f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage, out bool _);
        float   radius   = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);

        for (int i = 0; i < count; i++)
        {
            // random position à¸£à¸­à¸šà¸•à¸±à¸§à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™à¹ƒà¸™ range
            Vector2 rnd       = Random.insideUnitCircle * ld.range;
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            // cluster = true â†’ à¸£à¸°à¹€à¸šà¸´à¸”à¹à¸¥à¹‰à¸§à¸¡à¸µ child explosions (pellets à¸à¸£à¸°à¸ˆà¸²à¸¢à¸£à¸­à¸šà¸ˆà¸¸à¸”à¸£à¸°à¹€à¸šà¸´à¸”)
            ThrowGrenade(spawnPos, targetPos, dmg, radius, fuseTime, cluster: true);
        }
    }
}
