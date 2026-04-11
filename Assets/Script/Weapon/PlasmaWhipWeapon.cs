using UnityEngine;

/// <summary>
/// Plasma Whip — FUSION: Railgun (Super Laser) + Chainsaw (Super Whip)
///
/// กลไก:
///   • Spin AoE รอบตัว (เหมือน Chainsaw แต่ radius ใหญ่กว่า)
///   • พร้อมกัน ยิง Raycast 4 ทิศ (N/S/E/W)
///   • Cooldown ปานกลาง
///
/// Level data (Fusion tier, 1 level):
///   dmg=90, cd=1.0s, count=4 (ray จำนวน), range=6 (whip+ray range)
/// </summary>
public class PlasmaWhipWeapon : WeaponBase
{
    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   dmg    = RollDamage(ld.damage, out bool isCrit);
        float   range  = ld.range;

        if (manager.statManager != null)
            range *= manager.statManager.GetAreaMultiplier();

        // 1. Melee spin รอบตัว
        manager.FireMeleeServerRpc(center, range, dmg * 0.6f);
        ShowBaseHitVfx(center, isCrit);

        // 2. Raycast N ทิศ ตามจำนวน projectileCount
        int rays = Mathf.Max(1, ld.projectileCount);
        for (int i = 0; i < rays; i++)
        {
            float   angle = (360f / rays) * i;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            manager.FireRaycastServerRpc(center, dir, dmg, range, isCrit: isCrit);
        }
    }
}
