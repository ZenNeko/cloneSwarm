using UnityEngine;

/// <summary>
/// Split Spike Weapon (อาวุธหนามแยกร่างสะท้อน) — Super version ของ Spike Weapon
/// ยิงหนามแหลมทะลวงออกไป เมื่อเด้งกับกำแพงจะแยกร่างจาก 1 เป็น 2 ลูก เฉียงออก 30 องศา
///
/// Super tier — 1 level
///   dmg=90, cd=1.0s, speed=18, range=35, count=2
/// </summary>
public class SplitSpikeWeapon : SpikeWeapon
{
    // ใช้สืบทอดโครงสร้างและตรรกะ OnFire ทั้งหมดจาก SpikeWeapon
    // แต่ออกแบบไว้แยกคลาสเพื่อให้ Unity Editor สามารถอ้างอิงประเภทสคริปต์ได้ตรงตามประเภท Super Version
}
