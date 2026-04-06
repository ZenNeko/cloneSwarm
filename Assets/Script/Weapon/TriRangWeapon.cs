using UnityEngine;

/// <summary>
/// Tri-Rang — Super version ของ Boomerang
/// ยิง 3 Boomerang พร้อมกัน spread 30° + ระเบิด AoE เมื่อกลับถึงผู้เล่น
///
/// Super tier — 1 level
///   dmg=120, cd=2.0s, range=14, projectileSpeed=16, count=3
///
/// Fusion: Tri-Rang + StarRing = SatelliteRingWeapon
/// </summary>
public class TriRangWeapon : WeaponBase
{
    [Tooltip("มุม spread รวม (องศา)")]
    public float spreadAngle = 30f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg   = RollDamage(ld.damage);
        float range = ld.range;
        float speed = ld.projectileSpeed > 0f ? ld.projectileSpeed : 16f;
        int   count = Mathf.Max(1, ld.projectileCount);

        if (manager.statManager != null)
        {
            dmg   *= manager.statManager.GetPowerMultiplier();
            range *= manager.statManager.GetAreaMultiplier();
        }

        Vector3 spawnPos = transform.position + Vector3.up * 0.8f;
        Vector3 dir      = GetAimDirection();

        float halfSpread = count > 1 ? spreadAngle * 0.5f : 0f;
        float step       = count > 1 ? spreadAngle / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float   angle   = -halfSpread + i * step;
            Vector3 fireDir = Quaternion.Euler(0f, angle, 0f) * dir;
            manager.SpawnBoomerangServerRpc(spawnPos, fireDir, dmg, speed, range);
        }
    }

    Vector3 GetAimDirection()
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
