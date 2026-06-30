using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stormcaller — Super version ของ Lightning Chain
/// chain 6 ตัว + ทุก target ที่โดนสร้าง mini lightning zone (AoE ซ้ำ 1s) บน Server (Server Authority)
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
    [Tooltip("เปอร์เซ็นต์ดาเมจ zone จากดาเมจหลัก (0.3 = 30%)")]
    [Range(0.05f, 1f)]
    public float zoneDamageRatio    = 0.3f;
    [Tooltip("จำนวน tick ของ zone")]
    public int   zoneTicks  = 1;
    [Tooltip("หน่วงระหว่าง tick (วินาที)")]
    public float zoneTickInterval = 0.3f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg        = RollDamage(ld.damage, out bool isCrit);
        int   chainCount = Mathf.Max(1, ld.projectileCount);

        // คำนวณดาเมจของโซนเป็นเปอร์เซ็นต์จากดาเมจหลัก (ซึ่งรวมสเตตัสและเลเวลอาวุธเรียบร้อยแล้ว)
        float actualZoneDmg = dmg * zoneDamageRatio;

        // ดึงคีย์หลักและรองของ VFX (หลัก: ลำแสงสายฟ้าชิ่ง, รอง: โซนระเบิดสายฟ้าลงพื้น)
        string beamVfxKey = ResolveHitVfx("Default");
        string zoneVfxKey = ResolveSecondaryVfx("Stormcaller_AOE");

        // ส่งข้อมูลไปประมวลผลการทำงานและความเสียหายบนฝั่ง Server
        manager.FireChainServerRpc(
            transform.position + Vector3.up * 0.8f, dmg, ld.range, chainCount, chainSearchRadius, chainDamageMult,
            searchHighestHP: true,
            weaponName: data != null ? data.weaponName : "Unknown",
            beamVfx: beamVfxKey,
            hitVfx: "None",
            zoneRadius: zoneRadius,
            zoneDamage: actualZoneDmg,
            zoneTicks: zoneTicks,
            zoneTickInterval: zoneTickInterval,
            zoneVfx: zoneVfxKey,
            isCrit: isCrit
        );
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // วาดขอบเขตของ mini lightning zone AoE (สีเหลือง)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}
