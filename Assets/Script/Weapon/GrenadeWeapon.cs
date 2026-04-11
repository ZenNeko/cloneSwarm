using UnityEngine;

/// <summary>
/// Grenade — Auto-place รอบตัว player สุ่มตำแหน่ง → ระเบิด AoE
/// aimMode = AutoNearest (ตั้งใน WeaponData)
///
/// Level data แนะนำ:
///   Lv1: dmg=50,  cd=3.0s, count=1, range=10
///   Lv2: dmg=65,  cd=2.7s, count=1, range=11
///   Lv3: dmg=82,  cd=2.5s, count=2, range=12
///   Lv4: dmg=100, cd=2.3s, count=2, range=13
///   Lv5: dmg=125, cd=2.0s, count=3, range=14
/// </summary>
public class GrenadeWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิด (จะ scale ตาม AreaSize stat)")]
    public float explosionRadius = 3f;
    [Tooltip("เวลา fuse ก่อนระเบิด (วินาที)")]
    public float fuseTime        = 1.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage, out bool _);
        float   radius   = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);

        for (int i = 0; i < count; i++)
        {
            // random position รอบตัวผู้เล่นใน range
            Vector2 rnd       = Random.insideUnitCircle * ld.range;
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            manager.ThrowGrenadeServerRpc(spawnPos, targetPos, dmg, radius, fuseTime);
        }
    }
}
