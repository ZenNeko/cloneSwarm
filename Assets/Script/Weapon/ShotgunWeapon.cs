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
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;
    [Tooltip("มุมกระจายทั้งหมด (องศา) เช่น 40 = กระจาย 40° รวม")]
    public float spreadAngle = 40f;

    [Header("Blunderbuss Super")]
    [Tooltip("true = กระสุนแต่ละนัดระเบิดเป็นวงตรงจุดที่ชน (ใช้สำหรับ Super version)")]
    public bool  explodeOnHit    = false;
    [Tooltip("รัศมีระเบิดของกระสุนแต่ละนัด — ใช้เมื่อ explodeOnHit เปิด")]
    public float explosionRadius = 2.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();

        float pelletDmg = RollDamage(ld.damage, out bool isCrit);

        // Blunderbuss = ลูกซองที่กระสุนทุกนัดระเบิดตรงจุดที่ชน
        //
        // เดิมสาขานี้โยน "ระเบิดโค้ง" ไปที่ pos + dir * range ซึ่งผิดจากที่ออกแบบไว้สามชั้น:
        // บินโค้งสูง 1.5 ม. · ใช้เวลาเกือบวินาทีกว่าจะถึง · และบินไปสุดระยะเสมอไม่ว่าศัตรู
        // จะยืนประชิดแค่ไหน เพราะ GrenadeProjectile ไม่มีการตรวจการชนเลยสักบรรทัด
        // ตอนนี้ยิง pellet ตามปกติแล้วให้แต่ละนัดระเบิดตอนกระทบจริง
        FireProjectile(pos, dir, pelletDmg, ld.projectileSpeed,
            ld.projectileCount,
            spreadAngle / Mathf.Max(1, ld.projectileCount - 1),
            isCrit: isCrit,
            explosionRadius: explodeOnHit ? explosionRadius : 0f);
    }
}
