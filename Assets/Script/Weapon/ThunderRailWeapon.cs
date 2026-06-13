using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thunder Rail — Fusion: Stormcaller + Railgun
/// Railgun pierce ยิงทะลุกระสุนเลเซอร์แนวตรง + chain lightning และ mini lightning zone บน Server (Server Authority)
/// </summary>
public class ThunderRailWeapon : WeaponBase
{
    [Tooltip("จำนวน chain targets จากแต่ละ enemy ที่โดน railgun")]
    public int   chainTargets       = 3;
    [Tooltip("เปอร์เซ็นต์ดาเมจ chain จากดาเมจเลเซอร์หลัก (0.4 = 40%)")]
    [Range(0.1f, 2f)]
    public float chainDamageRatio   = 0.4f;
    [Tooltip("รัศมีหา chain target")]
    public float chainSearchRadius  = 8f;

    [Header("Lightning Zone (ทุก target ที่โดน)")]
    [Tooltip("รัศมี mini AoE zone")]
    public float zoneRadius = 2.5f;
    [Tooltip("เปอร์เซ็นต์ดาเมจ zone จากดาเมจเลเซอร์หลัก (0.2 = 20%)")]
    [Range(0.05f, 1f)]
    public float zoneDamageRatio    = 0.2f;
    [Tooltip("จำนวน tick ของ zone")]
    public int   zoneTicks  = 1;
    [Tooltip("หน่วงระหว่าง tick (วินาที)")]
    public float zoneTickInterval = 0.3f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg = RollDamage(ld.damage, out bool isCrit);

        // คำนวณความเสียหายเป็นเปอร์เซ็นต์จากดาเมจเลเซอร์หลักหลัก (dmg ซึ่งคำนวณเลเวลอาวุธและสเตตัสผู้เล่นแล้ว)
        float actualChainDmg = dmg * chainDamageRatio;
        float actualZoneDmg  = dmg * zoneDamageRatio;

        // คำนวณทิศทางยิงฝั่ง Client แล้วส่งขึ้น Server
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 dir    = GetAimDirection();
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir = dir.normalized;

        int beamCount = Mathf.Max(1, ld.projectileCount);

        // ดึงคีย์ VFX จากตัวแปร weapon VFX (weaponVfxType) หรือใช้ Beam_Railgun เป็น fallback
        string vfxKey = ResolveHitVfx("Beam_Railgun");
        // ดึงคีย์ 2nd VFX (secondaryVfxType) หรือใช้ Stormcaller_AOE เป็น fallback
        string zoneVfxKey = ResolveSecondaryVfx("Stormcaller_AOE");

        // เรียก ServerRpc เพื่อทำดาเมจและบันทึกสถิติดาเมจของอาวุธอย่างถูกต้อง
        manager.FireThunderRailServerRpc(
            origin, dir, dmg, ld.range, beamCount,
            chainTargets, actualChainDmg, chainSearchRadius,
            zoneRadius, actualZoneDmg, zoneTicks, zoneTickInterval,
            isCrit, data != null ? data.weaponName : "Unknown",
            vfxKey,
            zoneVfxKey
        );
    }



    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}
