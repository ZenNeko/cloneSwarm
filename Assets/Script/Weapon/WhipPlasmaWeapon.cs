using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WhipPlasma â€” Super version à¸‚à¸­à¸‡ Whip (Tentacle style)
///
/// à¸à¸¥à¹„à¸ Tentacle:
///   1. à¹€à¸«à¸§à¸µà¹ˆà¸¢à¸‡ tentacle à¸«à¸¥à¸±à¸à¹„à¸›à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸² (line AoE + knockback)
///   2. à¸›à¸¥à¸²à¸¢ tentacle "à¸à¸£à¸°à¸”à¸­à¸™" à¹„à¸›à¸«à¸² enemy à¹ƒà¸à¸¥à¹‰à¹€à¸„à¸µà¸¢à¸‡ N à¸•à¸±à¸§ (chain beam)
///   3. à¹à¸•à¹ˆà¸¥à¸° chain hit à¸—à¸³ damage à¸¥à¸”à¸¥à¸‡à¸—à¸µà¸¥à¸°à¸‚à¸±à¹‰à¸™
///   4. à¸—à¸¸à¸ enemy à¸—à¸µà¹ˆà¹‚à¸”à¸™ â€” à¸œà¸¥à¸±à¸ (knockback) à¸­à¸­à¸à¸ˆà¸²à¸à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™
///
/// Super tier â€” 1 level
///   dmg=55, cd=0.3s, range=5.5, count=3 (chain targets)
///
/// Fusion: PlasmaWhipWeapon (Railgun + WhipPlasma)
/// </summary>
public class WhipPlasmaWeapon : WhipWeapon
{
    [Header("Tentacle Chain")]
    [Tooltip("à¸ˆà¸³à¸™à¸§à¸™ chain à¸•à¹ˆà¸­à¸ˆà¸²à¸ main hit")]
    public int   chainCount          = 3;
    [Tooltip("à¸”à¸²à¹€à¸¡à¸ˆà¸¥à¸”à¸¥à¸‡à¸•à¹ˆà¸­ chain (0.7 = -30%)")]
    [Range(0.3f, 1f)]
    public float chainDamageMult     = 0.7f;
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸«à¸² chain target à¸–à¸±à¸”à¹„à¸›")]
    public float chainSearchRadius   = 8f;

    [Header("Knockback")]
    [Tooltip("à¹à¸£à¸‡à¸œà¸¥à¸±à¸ main hit")]
    public float knockbackForce      = 4f;
    [Tooltip("à¹à¸£à¸‡à¸œà¸¥à¸±à¸ chain hit (à¹€à¸šà¸²à¸à¸§à¹ˆà¸² main)")]
    public float chainKnockbackForce = 2f;

    [Header("Tentacle Timing")]
    [Tooltip("delay à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡ chain à¹à¸•à¹ˆà¸¥à¸° bounce (à¸§à¸´à¸™à¸²à¸—à¸µ)")]
    public float chainDelay          = 0.06f;

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

        // â”€â”€ Main Tentacle Strike (narrow line) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int        mask       = LayerMask.GetMask("Enemy");
        Vector3    boxCenter  = origin + dir * (range * 0.5f);
        Vector3    halfExtent = new Vector3(width * 0.5f, 1f, range * 0.5f);
        Quaternion rot        = Quaternion.LookRotation(dir);
        var        cols       = Physics.OverlapBox(boxCenter, halfExtent, rot, mask);
        var        hitSet     = new HashSet<int>();
        var        mainHits   = new List<Enemy>();

        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || hitSet.Contains(e.GetInstanceID())) continue;

            FireMelee(e.transform.position, 0.3f, dmg, isCrit);
            hitSet.Add(e.GetInstanceID());
            mainHits.Add(e);

            ApplyKnockback(e, knockbackForce);
        }

        // VFX: WhipSlash arc à¸—à¸µà¹ˆà¸ˆà¸¸à¸”à¸à¸¥à¸²à¸‡
        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(ResolveHitVfx("WhipSlash"), vfxPos, range, isCrit, isAttackHit: false, direction: dir);

        // â”€â”€ Chain Tentacle Bounce â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // à¹€à¸£à¸´à¹ˆà¸¡ chain à¸ˆà¸²à¸ enemy à¸—à¸µà¹ˆà¸­à¸¢à¸¹à¹ˆà¹„à¸à¸¥à¸ªà¸¸à¸”à¹ƒà¸™ main hit (à¸›à¸¥à¸²à¸¢ tentacle)
        Enemy chainStart = GetFarthestEnemy(mainHits, origin);
        if (chainStart != null)
            StartCoroutine(ChainBounce(chainStart, dmg, isCrit, hitSet, mask));
    }

    IEnumerator ChainBounce(Enemy startEnemy, float baseDmg, bool isCrit,
                            HashSet<int> hitSet, int mask)
    {
        Vector3 prevPos = startEnemy.transform.position + Vector3.up * 0.5f;
        float   curDmg  = baseDmg * chainDamageMult;

        for (int i = 0; i < chainCount; i++)
        {
            if (chainDelay > 0f) yield return new WaitForSeconds(chainDelay);

            Enemy next = FindNearestUnhit(prevPos, chainSearchRadius, mask, hitSet);
            if (next == null) yield break;

            Vector3 nextPos = next.transform.position + Vector3.up * 0.5f;

            FireMelee(next.transform.position, 0.3f, curDmg, isCrit);
            hitSet.Add(next.GetInstanceID());

            ApplyKnockback(next, chainKnockbackForce);

            // Beam VFX à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡ bounce (tentacle line)
            manager.BroadcastBeamServerRpc(prevPos, nextPos, ResolveSecondaryVfx("Default"), "HitEffect");

            prevPos  = nextPos;
            curDmg  *= chainDamageMult;
        }
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void ApplyKnockback(Enemy e, float force)
    {
        if (e == null || force <= 0f) return;
        Vector3 pushDir = e.transform.position - transform.position;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude > 0.001f)
            e.transform.position += pushDir.normalized * force;
    }

    Enemy GetFarthestEnemy(List<Enemy> list, Vector3 origin)
    {
        Enemy best = null;
        float maxD = -1f;
        foreach (var e in list)
        {
            if (e == null) continue;
            float d = Vector3.Distance(origin, e.transform.position);
            if (d > maxD) { maxD = d; best = e; }
        }
        return best;
    }

    Enemy FindNearestUnhit(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var   cols = Physics.OverlapSphere(center, radius, mask);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            float d = Vector3.Distance(center, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        return best;
    }
}
