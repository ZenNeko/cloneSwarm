using System.Collections;
using UnityEngine;

/// <summary>
/// TendrilStorm — Super version ของ Tentacle (Lance)
/// ยิง tendril หลักไปข้างหน้า แล้ว spawn อีก N ตัวในทิศทางสุ่มรอบตัว
///
/// Holocure ref: Lv7 Summon Tentacle "Chain another 3 Tentacles in a chain at a random direction"
///
/// Super tier — 1 level (กำหนดใน WD_TendrilStorm.asset)
///   แนะนำ: dmg=90, cd=0.4s, range=8.0, count=3 (chain count)
/// </summary>
public class TendrilStormWeapon : LanceWeapon
{
    [Header("Tendril Storm")]
    [Tooltip("จำนวน tendril เพิ่มเติมที่ยิงทิศสุ่ม (นอกจาก main forward)")]
    public int   chainCount       = 3;
    [Tooltip("ดาเมจ tendril สุ่ม = main * mult (0.7 = -30%)")]
    [Range(0.3f, 1f)]
    public float chainDamageMult  = 0.7f;
    [Tooltip("delay ระหว่าง tendril แต่ละเส้น (วินาที) — 0 = ยิงพร้อมกัน")]
    public float chainDelay       = 0.05f;

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

        // Main tendril — forward (knockback applied via base class)
        DoThrust(origin, dir, dmg, range, isCrit);

        // Calculate tip of the main lance
        Vector3 lanceTip = origin + dir * range;

        // Random direction tendrils starting from the tip of the main lance
        if (chainCount > 0)
            StartCoroutine(SpawnRandomTendrils(lanceTip, dmg * chainDamageMult, range, isCrit));
    }

    IEnumerator SpawnRandomTendrils(Vector3 lanceTip, float dmg, float range, bool isCrit)
    {
        for (int i = 0; i < chainCount; i++)
        {
            if (chainDelay > 0f) yield return new WaitForSeconds(chainDelay);

            Vector2 rnd2D     = Random.insideUnitCircle.normalized;
            Vector3 randomDir = new Vector3(rnd2D.x, 0f, rnd2D.y);

            DoThrust(lanceTip, randomDir, dmg, range, isCrit);
        }
    }
}
