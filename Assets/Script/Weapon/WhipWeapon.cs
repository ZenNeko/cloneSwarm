using UnityEngine;

/// <summary>
/// Whip — AutoNearest, Melee Spin 360° รอบตัวผู้เล่น
/// ใช้สำหรับ Normal (Whip) และ Super (Chainsaw)
///
/// Chainsaw ต่างกัน: cooldown สั้นมาก, radius ใหญ่ขึ้น, isContinuous = true
///
/// Level data แนะนำ:
///   Lv1: dmg=35, cd=1.8s, count=1, range=2.5
///   Lv2: dmg=44, cd=1.6s, count=1, range=2.8
///   Lv3: dmg=55, cd=1.4s, count=1, range=3.2
///   Lv4: dmg=68, cd=1.2s, count=1, range=3.6
///   Lv5: dmg=85, cd=1.0s, count=1, range=4.0
///
/// Chainsaw (Super):
///   Single level — dmg=40, cd=0.25s, range=5.0
/// </summary>
public class WhipWeapon : WeaponBase
{
    protected override void OnInit() => aimMode = AimMode.AutoNearest;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   radius = ld.range;
        float   dmg    = RollDamage(ld.damage);

        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        manager.FireMeleeServerRpc(center, radius, dmg);
    }
}
