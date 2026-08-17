using UnityEngine;

/// <summary>
/// Splitter Bomb — Super Grenade
/// Auto-place grenade รอบตัว player → ระเบิดแล้วมี child explosions (cluster)
///
/// Level data (Super tier, 1 level):
///   dmg=120, cd=2.5s, count=3 (จำนวน grenade ต่อ volley), range=6 (วางใน radius นี้)
/// </summary>
public class SplitterBombWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + GrenadeProjectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;

    // ── ทุกอย่างของ Splitter Bomb ตั้งที่นี่ที่เดียว ────────────────────────
    // ค่าด้านล่างถูกส่งไปเขียนทับ prefab ลูกระเบิดตอนยิง จูนอาวุธจึงจบในคอมโพเนนต์นี้
    // ไม่ต้องเด้งไปเปิด prefab ลูกอีกที
    //
    // ข้อยกเว้นเดียวที่ยังต้องตั้งบน prefab ลูกระเบิดคือ **prefab ของลูกที่จะแตกออกมา**
    // (`clusterProjectilePrefab`) เพราะ prefab reference ส่งข้ามเน็ตเวิร์กไม่ได้

    [Header("ระเบิดลูกแม่")]
    [Tooltip("รัศมีระเบิดของลูกแม่ (คูณ Area stat ให้อัตโนมัติ)")]
    public float explosionRadius = 3f;
    [Tooltip("เวลาหน่วงก่อนลูกแม่ระเบิด (วินาที)")]
    public float fuseTime = 1.2f;

    [Header("ลูกที่แตกออกมาตอนระเบิด")]
    [Tooltip("ปิด overrideProjectile = ใช้ค่าที่ตั้งบน prefab ลูกระเบิดแทน")]
    public GrenadeClusterSettings clusterSettings = new GrenadeClusterSettings
    {
        overrideProjectile = true,
        pellets            = 8,
        dmgPercent         = 0.4f,
        projSpeed          = 6f,
        spreadRadius       = 6f,
        childRadius        = 1.5f,
        childFuse          = 1f,
    };

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage, out bool isCrit);

        float radius = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);

        for (int i = 0; i < count; i++)
        {
            // random position รอบตัวผู้เล่นใน range
            Vector2 rnd       = Random.insideUnitCircle * ld.range;
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            // cluster = true → ระเบิดแล้วแตกลูกใหม่ออกมาตาม clusterSettings
            ThrowGrenade(spawnPos, targetPos, dmg, radius, fuseTime, cluster: true, isCrit: isCrit,
                         clusterSettings: clusterSettings);
        }
    }
}
