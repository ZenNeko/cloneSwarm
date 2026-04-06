using UnityEngine;

/// <summary>
/// Minefield — Super Grenade
/// Auto-place grenade รอบตัว player → ระเบิดแล้วมี child explosions (cluster)
///
/// Level data (Super tier, 1 level):
///   dmg=120, cd=2.5s, count=3 (จำนวน grenade ต่อ volley), range=6 (วางใน radius นี้)
/// </summary>
public class MinefieldWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิดของ grenade แต่ละลูก")]
    public float explosionRadius = 3f;
    [Tooltip("เวลา fuse ก่อนระเบิด (วินาที)")]
    public float fuseTime = 1.2f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        float   dmg      = RollDamage(ld.damage);
        float   radius   = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);

        for (int i = 0; i < count; i++)
        {
            // random position รอบตัวผู้เล่นใน range
            Vector2 rnd       = Random.insideUnitCircle * ld.range;
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            // cluster = true → ระเบิดแล้วมี child explosions (pellets กระจายรอบจุดระเบิด)
            manager.ThrowGrenadeServerRpc(spawnPos, targetPos, dmg, radius, fuseTime, cluster: true);
        }
    }
}
