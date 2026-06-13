using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blade Storm — Super version ของ Dual Slash
/// slash 120° arc × 3 ครั้ง สลับหน้า-หลัง-หน้า (rapid burst) ทำงานผ่าน Server Authority
/// </summary>
public class BladeStormWeapon : WeaponBase
{
    [Tooltip("จำนวนครั้งที่ slash ใน burst เดียว")]
    public int   burstCount    = 6;
    [Tooltip("หน่วงระหว่าง slash แต่ละครั้งใน burst (วินาที)")]
    public float burstInterval = 0.12f;
    [Tooltip("มุม arc ของ slash แต่ละครั้ง (องศา)")]
    public float arcAngle      = 120f;

    private bool isBursting;

    protected override void OnFire(WeaponLevelData ld)
    {
        if (isBursting) return;
        StartCoroutine(BurstSequence(ld));
    }

    IEnumerator BurstSequence(WeaponLevelData ld)
    {
        isBursting = true;
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = ld.range;

        if (manager.statManager != null)
        {
            dmg    *= manager.statManager.GetPowerMultiplier();
            radius *= manager.statManager.GetAreaMultiplier();
        }

        Vector3 center = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = GetForwardDir();

        for (int i = 0; i < burstCount; i++)
        {
            // สลับหน้า-หลัง: 0=หน้า, 1=หลัง, 2=หน้า
            Vector3 slashDir = (i % 2 == 0) ? dir : -dir;
            // ใช้ FireArcMelee เพื่อส่งคำสั่งทำดาเมจและลงทะเบียนสถิติไปทำบน Server (ServerRpc)
            FireArcMelee(center, slashDir, radius, arcAngle, dmg, isCrit);
            ShowVfx(ResolveHitVfx("SlashHit"), center + slashDir * (radius * 0.4f), radius, isCrit, isAttackHit: false, direction: slashDir);

            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        isBursting = false;
    }

    Vector3 GetForwardDir()
    {
        int   mask    = LayerMask.GetMask("Enemy");
        var   cols    = Physics.OverlapSphere(transform.position, 20f, mask);
        float minDist = float.MaxValue;
        Vector3 dir   = transform.forward;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; dir = (c.transform.position - transform.position).normalized; }
        }
        dir.y = 0f;
        return dir == Vector3.zero ? transform.forward : dir;
    }
}
