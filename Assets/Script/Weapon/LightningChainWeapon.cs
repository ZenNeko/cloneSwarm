using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Chain â€” à¸¢à¸´à¸‡à¸ªà¸²à¸¢à¸Ÿà¹‰à¸²à¹„à¸›à¸«à¸² enemy HP à¸ªà¸¹à¸‡à¸ªà¸¸à¸” à¹à¸¥à¹‰à¸§ chain à¸•à¹ˆà¸­ N à¸•à¸±à¸§
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=25, cd=1.8s, count=2 (chain targets), range=12
///   Lv2: dmg=32, cd=1.6s, count=2, range=13
///   Lv3: dmg=40, cd=1.4s, count=3, range=14
///   Lv4: dmg=50, cd=1.2s, count=3, range=15
///   Lv5: dmg=60, cd=1.0s, count=4, range=16
///
/// Super: StormcallerWeapon (chain 6 à¸•à¸±à¸§ + mini lightning zone)
/// Fusion: Stormcaller + Railgun = ThunderRailWeapon
/// </summary>
public class LightningChainWeapon : WeaponBase
{
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸«à¸² chain target à¸–à¸±à¸”à¹„à¸›à¸ˆà¸²à¸à¸ˆà¸¸à¸”à¸—à¸µà¹ˆà¸Šà¸™à¸¥à¹ˆà¸²à¸ªà¸¸à¸”")]
    public float chainSearchRadius = 8f;
    [Tooltip("à¸”à¸²à¹€à¸¡à¸ˆà¸¥à¸”à¸¥à¸‡à¸•à¹ˆà¸­ chain (0.8 = -20% à¸•à¹ˆà¸­à¸•à¸±à¸§)")]
    [Range(0.3f, 1f)]
    public float chainDamageMult   = 0.8f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg        = RollDamage(ld.damage, out bool isCrit);
        int   chainCount = Mathf.Max(1, ld.projectileCount);   // count = à¸ˆà¸³à¸™à¸§à¸™ chain targets

        if (manager.statManager != null)
            dmg *= manager.statManager.GetPowerMultiplier();

        // à¸«à¸² enemy HP à¸ªà¸¹à¸‡à¸ªà¸¸à¸”à¹ƒà¸™à¸£à¸±à¸¨à¸¡à¸µ range à¹€à¸›à¹‡à¸™à¹€à¸›à¹‰à¸²à¹à¸£à¸
        var   origin  = transform.position;
        var   hitSet  = new HashSet<int>();

        Enemy firstTarget = FindHighestHPEnemy(origin, ld.range, hitSet);
        if (firstTarget == null) return;

        Vector3 prevPos = transform.position + Vector3.up * 0.8f;
        Enemy   current = firstTarget;
        float   curDmg  = dmg;

        for (int i = 0; i <= chainCount; i++)
        {
            if (current == null) break;

            Vector3 targetPos = current.transform.position + Vector3.up * 0.8f;

            // server-authoritative damage (radius 0.5 = à¸•à¸µà¹€à¸‰à¸žà¸²à¸°à¸•à¸±à¸§à¸™à¸±à¹‰à¸™)
            FireMelee(current.transform.position, 0.5f, curDmg, isCrit);
            BroadcastLightningBeam(prevPos, targetPos);

            hitSet.Add(current.GetInstanceID());
            prevPos = targetPos;
            curDmg  *= chainDamageMult;

            // à¸«à¸² chain target à¸–à¸±à¸”à¹„à¸› (à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹‚à¸”à¸™, à¹ƒà¸à¸¥à¹‰à¸—à¸µà¹ˆà¸ªà¸¸à¸”)
            current = FindNearestUnhitEnemy(targetPos, chainSearchRadius, hitSet);
        }
    }

    Enemy FindHighestHPEnemy(Vector3 center, float radius, HashSet<int> exclude)
    {
        var cols    = Physics.OverlapSphere(center, radius);
        Enemy best  = null;
        float bestHP = -1f;
        foreach (var c in cols)
        {
            if (!c.CompareTag("Enemy")) continue;
            var e = c.GetComponent<Enemy>();
            if (e == null) continue;
            if (exclude.Contains(e.GetInstanceID())) continue;
            float hp = e.netHealth.Value;
            if (hp > bestHP) { bestHP = hp; best = e; }
        }
        return best;
    }

    Enemy FindNearestUnhitEnemy(Vector3 center, float radius, HashSet<int> exclude)
    {
        var cols   = Physics.OverlapSphere(center, radius);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            if (!c.CompareTag("Enemy")) continue;
            var e = c.GetComponent<Enemy>();
            if (e == null) continue;
            if (exclude.Contains(e.GetInstanceID())) continue;
            float d = Vector3.Distance(center, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        return best;
    }

    void BroadcastLightningBeam(Vector3 from, Vector3 to)
    {
        // à¹ƒà¸Šà¹‰ HitEffect à¹€à¸›à¹‡à¸™ burst à¸—à¸µà¹ˆà¸›à¸¥à¸²à¸¢ â†’ à¸¡à¸µ spark à¸—à¸µà¹ˆ chain target à¹à¸¡à¹‰ beamPrefab à¹„à¸¡à¹ˆ assign
        manager.BroadcastBeamServerRpc(from, to, ResolveHitVfx("Default"), "HitEffect");
    }
}
