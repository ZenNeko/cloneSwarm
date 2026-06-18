using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Chain — ยิงสายฟ้าไปหา enemy HP สูงสุด แล้ว chain ต่อ N ตัว
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

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        string beamVfx = ResolveHitVfx("Default");

        manager.FireChainServerRpc(
            origin, dmg, ld.range, chainCount, chainSearchRadius, chainDamageMult,
            searchHighestHP: true,
            weaponName: data != null ? data.weaponName : "Unknown",
            beamVfx: beamVfx,
            hitVfx: "HitEffect",
            isCrit: isCrit
        );
    }
}
