using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stormcaller — Super version ของ Lightning Chain
/// chain 6 ตัว + ทุก target ที่โดนสร้าง mini lightning zone (AoE ซ้ำ 1s)
///
/// Super tier — 1 level
///   dmg=80, cd=2.5s, count=6 (chain targets), range=18
///
/// Fusion: Stormcaller + Railgun = ThunderRailWeapon
/// </summary>
public class StormcallerWeapon : WeaponBase
{
    [Tooltip("รัศมีหา chain target ถัดไป")]
    public float chainSearchRadius = 10f;
    [Tooltip("ดาเมจลดลงต่อ chain")]
    [Range(0.3f, 1f)]
    public float chainDamageMult   = 0.85f;

    [Header("Lightning Zone (ทุก target ที่โดน)")]
    [Tooltip("รัศมี mini AoE zone")]
    public float zoneRadius = 2.5f;
    [Tooltip("ดาเมจ zone ต่อ tick")]
    public float zoneDamage = 25f;
    [Tooltip("จำนวน tick ของ zone")]
    public int   zoneTicks  = 3;
    [Tooltip("หน่วงระหว่าง tick (วินาที)")]
    public float zoneTickInterval = 0.3f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg        = RollDamage(ld.damage, out bool isCrit);
        int   chainCount = Mathf.Max(1, ld.projectileCount);

        if (manager.statManager != null)
            dmg *= manager.statManager.GetPowerMultiplier();

        int mask   = LayerMask.GetMask("Enemy");
        var hitSet = new HashSet<int>();

        Enemy first = FindHighestHPEnemy(transform.position, ld.range, mask, hitSet);
        if (first == null) return;

        Vector3 prevPos = transform.position + Vector3.up * 0.8f;
        Enemy   current = first;
        float   curDmg  = dmg;

        var hitPositions = new List<Vector3>();

        for (int i = 0; i <= chainCount; i++)
        {
            if (current == null) break;

            Vector3 targetPos = current.transform.position + Vector3.up * 0.8f;

            // HitEffect/CritHitEffect เกิดอัตโนมัติใน Enemy.NotifyHitClientRpc
            current.EnemyTakeDamage(curDmg, isCrit);
            manager.BroadcastBeamServerRpc(prevPos, targetPos, "None");

            hitPositions.Add(current.transform.position);
            hitSet.Add(current.GetInstanceID());
            prevPos = targetPos;
            curDmg *= chainDamageMult;

            current = FindNearestUnhitEnemy(targetPos, chainSearchRadius, mask, hitSet);
        }

        // Spawn mini lightning zone ที่ทุก target ที่โดน
        StartCoroutine(SpawnLightningZones(hitPositions, isCrit));
    }

    IEnumerator SpawnLightningZones(List<Vector3> positions, bool isCrit)
    {
        float effectiveZoneDmg = zoneDamage;
        if (manager.statManager != null)
            effectiveZoneDmg *= manager.statManager.GetPowerMultiplier();

        for (int tick = 0; tick < zoneTicks; tick++)
        {
            yield return new WaitForSeconds(zoneTickInterval);
            foreach (var pos in positions)
            {
                manager.FireMeleeServerRpc(pos + Vector3.up * 0.5f, zoneRadius, effectiveZoneDmg);
                // VFX: ใช้ weaponVfxType (designer set ใน Inspector ของ weapon prefab) — ถ้า None ไม่ทำอะไร
                // (Enemy.cs spawn HitEffect ที่ตัวมันเอง ไม่ต้องซ้ำที่ pos)
                if (!string.IsNullOrEmpty(weaponVfxType) && weaponVfxType != "None")
                    ShowVfx(weaponVfxType, pos + Vector3.up * 0.5f, isCrit: isCrit);
            }
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
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            if (e.netHealth.Value > bestHP) { bestHP = e.netHealth.Value; best = e; }
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
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            float d = Vector3.Distance(center, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        return best;
 
 
 
 
    }
}
