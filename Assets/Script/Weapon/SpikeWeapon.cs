using UnityEngine;

/// <summary>
/// Spike Weapon (อาวุธหนามทะลวงเด้ง) — Normal Weapon
/// ยิงหนามแหลมคมออกไปข้างหน้า เจาะทะลุศัตรู และเด้งสะท้อนกับกำแพง
///
/// Level data แนะนำ:
///   Lv1: dmg=20, cd=1.5s, speed=14, range=20
///   Lv2: dmg=25, cd=1.35s, speed=15, range=22
///   Lv3: dmg=32, cd=1.2s, speed=16, range=24
///   Lv4: dmg=40, cd=1.1s, speed=17, range=26
///   Lv5: dmg=50, cd=0.95s, speed=18, range=30
///
/// Super version: SplitSpikeWeapon
/// </summary>
public class SpikeWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Spike projectile prefab (ต้องมี NetworkObject + BouncingSpikeProjectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage, out bool isCrit);
        float   speed    = ld.projectileSpeed > 0f ? ld.projectileSpeed : 14f;
        float   range    = ld.range;
        int     count    = Mathf.Max(1, ld.projectileCount);
        float   spread   = 10f; // มุมกระจายระหว่างกระสุน (องศา)

        Vector3 dir = GetAimDirection();

        // ยิงกระสุนหนามตามจำนวนนัด (projectileCount) แบบกระจายแนวพัด
        FireProjectile(spawnPos, dir, dmg, speed, count, spread, piercing: true, maxRange: range, isCrit: isCrit);
    }
}
