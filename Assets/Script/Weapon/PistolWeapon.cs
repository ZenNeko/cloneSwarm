using System.Collections;
using UnityEngine;

/// <summary>
/// Pistol — Gunner's main weapon (MouseAim, Burst fire)
/// ปกติ: ยิง burst (count นัด ห่างกัน burstInterval)
/// ขณะ Rocket Mode active (Q): ยิง StickyRocket แทนทุกนัด
///
/// LevelData example:
///   Lv1: dmg=20, cd=1.0s, count=1, speed=16
///   Lv2: dmg=25, cd=0.9s, count=1, speed=17
///   Lv3: dmg=30, cd=0.85s, count=2, speed=18
///   Lv4: dmg=38, cd=0.8s,  count=2, speed=18
///   Lv5: dmg=48, cd=0.7s,  count=3, speed=20
/// </summary>
public class PistolWeapon : WeaponBase
{
    [Header("Burst")]
    [Tooltip("หน่วงเวลาระหว่าง burst แต่ละนัด (วินาที)")]
    public float burstInterval = 0.33f;

    [Header("Sticky Rocket (Rocket Mode)")]
    [Tooltip("รัศมีระเบิดของ Sticky Rocket")]
    public float stickyExplosionRadius = 2.5f;
    [Tooltip("ความเร็ว Sticky Rocket")]
    public float stickySpeed = 14f;

    private GunnerRocketMode rocketMode;

    protected override void OnFire(WeaponLevelData ld)
    {
        if (rocketMode == null)
            rocketMode = manager.GetComponentInChildren<GunnerRocketMode>();

        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();
        float   dmg = RollDamage(ld.damage);

        if (rocketMode != null && rocketMode.IsRocketModeActive)
            StartCoroutine(BurstFire(pos, dir, dmg, ld.projectileSpeed, ld.projectileCount, rocketMode: true));
        else
            StartCoroutine(BurstFire(pos, dir, dmg, ld.projectileSpeed, ld.projectileCount, rocketMode: false));
    }

    IEnumerator BurstFire(Vector3 spawnPos, Vector3 dir, float dmg, float speed, int count, bool rocketMode)
    {
        for (int i = 0; i < count; i++)
        {
            if (rocketMode)
                manager.SpawnStickyRocketServerRpc(spawnPos, dir, dmg, stickySpeed, stickyExplosionRadius);
            else
                FireProjectile(spawnPos, dir, dmg, speed);

            if (i < count - 1)
                yield return new WaitForSeconds(burstInterval);
        }
    }
}
