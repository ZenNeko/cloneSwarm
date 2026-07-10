using UnityEngine;

/// <summary>
/// Molotov Cocktail (ระเบิดขวดไฟ) — Normal Weapon
/// โยนขวดไฟลงพื้นเพื่อสร้างความเสียหายเริ่มแรกและทิ้งพื้นที่เพลิงเผาไหม้เป็นวินาที
///
/// Level data แนะนำ:
///   Lv1: dmg=25, cd=2.0s, range=6, radius=2.5
///   Lv2: dmg=32, cd=1.8s, range=6.5, radius=2.8
///   Lv3: dmg=40, cd=1.6s, range=7, radius=3.1
///   Lv4: dmg=50, cd=1.5s, range=7.5, radius=3.4
///   Lv5: dmg=65, cd=1.3s, range=8, radius=3.8
///
/// Super version: NapalmBombWeapon
/// </summary>
public class MolotovWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Molotov projectile prefab (ต้องมี NetworkObject + MolotovProjectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    [Header("Molotov Settings")]
    [Tooltip("รัศมีการระเบิดและพื้นที่ไฟไหม้ (scale ตาม AreaSize stat)")]
    public float explosionRadius = 2.5f;
    [Tooltip("เวลาลอยในอากาศก่อนระเบิด (วินาที)")]
    public float fuseTime = 1.2f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage, out bool isCrit);
        float   radius   = ld.radius > 0f ? ld.radius : explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);
        Vector3 dir = GetAimDirection();

        for (int i = 0; i < count; i++)
        {
            Vector3 targetPos;
            if (count > 1)
            {
                // หากมีหลายลูก ให้กระจายพิกัดตกแบบสุ่มรอบทิศทาง
                Vector2 rnd = Random.insideUnitCircle.normalized * (ld.range * Random.Range(0.4f, 1f));
                targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            }
            else
            {
                // หากมีลูกเดียว ให้โยนตรงไปยังเป้าหมายที่เล็งไว้
                targetPos = transform.position + dir * ld.range;
            }

            ThrowGrenade(spawnPos, targetPos, dmg, radius, fuseTime, isCrit: isCrit);
        }
    }
}
