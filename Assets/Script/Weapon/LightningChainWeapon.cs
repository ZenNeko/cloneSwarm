using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Chain — ยิงสายฟ้าไปหา enemy HP สูงสุด แล้ว chain ต่อ N ตัว
///
/// Level data แนะนำ:
///   Lv1: dmg=25, cd=1.8s, count=2 (chain targets), range=12
///   Lv2: dmg=32, cd=1.6s, count=2, range=13
///   Lv3: dmg=40, cd=1.4s, count=3, range=14
///   Lv4: dmg=50, cd=1.2s, count=3, range=15
///   Lv5: dmg=60, cd=1.0s, count=4, range=16
///
/// Super: StormcallerWeapon (chain 6 ตัว + mini lightning zone)
/// Fusion: Stormcaller + Railgun = ThunderRailWeapon
/// </summary>
public class LightningChainWeapon : WeaponBase
{
    [Tooltip("รัศมีหา chain target ถัดไปจากจุดที่ชนล่าสุด")]
    public float chainSearchRadius = 8f;
    [Tooltip("ดาเมจลดลงต่อ chain (0.8 = -20% ต่อตัว)")]
    [Range(0.3f, 1f)]
    public float chainDamageMult   = 0.8f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg        = RollDamage(ld.damage, out bool isCrit);
        int   chainCount = Mathf.Max(1, ld.projectileCount);   // count = จำนวน chain targets

        if (manager.statManager != null)
            dmg *= manager.statManager.GetPowerMultiplier();

        // หา enemy HP สูงสุดในรัศมี range เป็นเป้าแรก
        int   mask    = LayerMask.GetMask("Enemy");
        var   origin  = transform.position;
        var   hitSet  = new HashSet<int>();

        Enemy firstTarget = FindHighestHPEnemy(origin, ld.range, mask, hitSet);
        if (firstTarget == null) return;

        Vector3 prevPos = transform.position + Vector3.up * 0.8f;
        Enemy   current = firstTarget;
        float   curDmg  = dmg;

        for (int i = 0; i <= chainCount; i++)
        {
            if (current == null) break;

            Vector3 targetPos = current.transform.position + Vector3.up * 0.8f;

            // ดาเมจ + VFX beam — HitEffect/CritHitEffect เกิดอัตโนมัติใน Enemy.NotifyHitClientRpc
            current.EnemyTakeDamage(curDmg, isCrit);
            BroadcastLightningBeam(prevPos, targetPos);

            hitSet.Add(current.GetInstanceID());
            prevPos = targetPos;
            curDmg  *= chainDamageMult;

            // หา chain target ถัดไป (ยังไม่โดน, ใกล้ที่สุด)
            current = FindNearestUnhitEnemy(targetPos, chainSearchRadius, mask, hitSet);
        }
    }

    Enemy FindHighestHPEnemy(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var cols    = Physics.OverlapSphere(center, radius, mask);
        Enemy best  = null;
        float bestHP = -1f;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null) continue;
            if (exclude.Contains(e.GetInstanceID())) continue;
            float hp = e.netHealth.Value;
            if (hp > bestHP) { bestHP = hp; best = e; }
        }
        return best;
    }

    Enemy FindNearestUnhitEnemy(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var cols   = Physics.OverlapSphere(center, radius, mask);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
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
        manager.BroadcastBeamServerRpc(from, to, (int)VFXType.None);
    }
}
