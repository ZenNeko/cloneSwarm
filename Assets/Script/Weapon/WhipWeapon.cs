using UnityEngine;

/// <summary>
/// Whip â€” Tentacle style: à¸•à¸µ AoE à¹€à¸›à¹‡à¸™à¹€à¸ªà¹‰à¸™à¸•à¸£à¸‡à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸² (forward line)
/// à¸«à¸² nearest enemy â†’ à¸¢à¸´à¸‡ line AoE (OverlapBox) à¹ƒà¸™à¸—à¸´à¸¨à¸™à¸±à¹‰à¸™
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=35, cd=1.8s, range=2.5
///   Lv2: dmg=44, cd=1.6s, range=2.8
///   Lv3: dmg=55, cd=1.4s, range=3.2
///   Lv4: dmg=68, cd=1.2s, range=3.6
///   Lv5: dmg=85, cd=1.0s, range=4.0
///
/// Super: ChainsawWeapon (knockback + chain)
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class WhipWeapon : WeaponBase
{
    [Header("Tentacle Config")]
    [Tooltip("à¸„à¸§à¸²à¸¡à¸à¸§à¹‰à¸²à¸‡à¸‚à¸­à¸‡ line AoE")]
    public float width = 1.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = GetAimDirection();
        float   dmg    = RollDamage(ld.damage, out bool isCrit);
        float   range  = ld.range;

        if (manager.statManager != null)
        {
            dmg   *= manager.statManager.GetPowerMultiplier();
            range *= manager.statManager.GetAreaMultiplier();
        }

        // Line AoE à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸² (Physics.OverlapBox à¸œà¹ˆà¸²à¸™ server)
        FireLineAoE(origin, dir, dmg, range, width, isCrit);
        // VFX à¸—à¸µà¹ˆà¸ˆà¸¸à¸”à¸à¸¥à¸²à¸‡à¸‚à¸­à¸‡ line
        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(ResolveHitVfx("WhipSlash"), vfxPos, range, isCrit, isAttackHit: false, direction: dir);
    }

    /// <summary>à¸«à¸² nearest enemy à¹à¸¥à¹‰à¸§à¸«à¸±à¸™à¸«à¸™à¹‰à¸²à¹„à¸› â€” à¹ƒà¸Šà¹‰à¹ƒà¸™ subclass à¹„à¸”à¹‰</summary>
    protected Vector3 GetForwardDirection()
    {
        int   mask    = LayerMask.GetMask("Enemy");
        var   cols    = Physics.OverlapSphere(transform.position, 20f, mask);
        float minDist = float.MaxValue;
        Vector3 dir   = transform.forward;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist)
            {
                minDist = d;
                dir     = (c.transform.position - transform.position).normalized;
            }
        }
        dir.y = 0f;
        return dir == Vector3.zero ? transform.forward : dir;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        float range = data.GetLevelData(currentLevel).range;
        Vector3 dir    = transform.forward;
        Vector3 center = transform.position + dir * (range * 0.5f) + Vector3.up * 0.5f;

        Gizmos.color  = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(dir), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 2f, range));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
