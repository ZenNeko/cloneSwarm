using UnityEngine;

/// <summary>
/// Shotgun — MouseAim, spread pellets
/// ใช้ได้ทั้ง Normal (Shotgun) และ Super (Blunderbuss)
///
/// Blunderbuss ต่างกันที่:  explodeOnHit = true, explosionRadius ใหญ่ขึ้น
/// ตั้งค่าผ่าน WeaponData.levels[0].levelUpText หรือ Inspector ของ prefab
///
/// Level data แนะนำ:
///   Lv1: dmg=12/pellet, cd=1.2s, count=4, range=7
///   Lv2: dmg=14/pellet, cd=1.1s, count=4, range=7
///   Lv3: dmg=16/pellet, cd=1.0s, count=5, range=8
///   Lv4: dmg=18/pellet, cd=0.95s, count=5, range=8
///   Lv5: dmg=22/pellet, cd=0.85s, count=6, range=9
/// </summary>
public class ShotgunWeapon : WeaponBase
{
    [Tooltip("มุมกระจายทั้งหมด (องศา) เช่น 40 = กระจาย 40° รวม")]
    public float spreadAngle = 40f;

    [Header("Blunderbuss Super")]
    [Tooltip("true = กระสุนระเบิด AoE เมื่อถึงศัตรู (ใช้สำหรับ Super version)")]
    public bool  explodeOnHit    = false;
    public float explosionRadius = 2.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();

        // damage แบ่งต่อ pellet แต่ขั้นต่ำ 1
        float dmgPerPellet = Mathf.Max(1f, ld.damage / Mathf.Max(1, ld.projectileCount));
        float crit         = RollDamage(dmgPerPellet);

        if (explodeOnHit)
        {
            // Blunderbuss: ยิง 1 กระสุนหนัก → ระเบิด AoE บนเป้าหมาย
            // สร้างเป็น grenade ที่บินตรง แต่ระเบิดทันทีที่ชน
            var targetPos = pos + dir * ld.range;
            manager.ThrowGrenadeServerRpc(pos, targetPos, ld.damage, explosionRadius, fuseTime: 0.05f);
        }
        else
        {
            FireProjectile(pos, dir, crit, ld.projectileSpeed,
                ld.projectileCount,
                spreadAngle / Mathf.Max(1, ld.projectileCount - 1));
        }
    }
}
