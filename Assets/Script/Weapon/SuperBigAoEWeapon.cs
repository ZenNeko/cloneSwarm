using UnityEngine;

/// <summary>
/// SuperBigAoEWeapon — ร่างพัฒนา (Super Version) ของ BigAoEWeapon
/// เมื่อชาร์จพลังและเกิดระเบิดตูมยักษ์แล้ว จะเพิ่มระดับ (Tier Up) ให้กับ Exp orb ทั้งหมดในรัศมี (เพิ่มมูลค่า EXP, ขยายขนาด, และย้อมสีทอง)
/// พร้อมกับบัฟเพิ่มพลังดาเมจ (+30% Damage) ให้แก่เพื่อนและผู้เล่นทุกคนที่อยู่ในระยะระเบิดเป็นเวลา 6 วินาที โดยมี UI Canvas ลอยตัวแจ้งเตือน
/// </summary>
public class SuperBigAoEWeapon : BigAoEWeapon
{
    [Header("Super Big AoE Settings")]
    [Tooltip("เปอร์เซ็นต์ดาเมจที่บัฟให้ผู้เล่นที่ได้รับผลกระทบ (เช่น 0.3 = +30%)")]
    public float damageBuffAmount = 0.3f;
    [Tooltip("ระยะเวลาบัฟดาเมจ (วินาที)")]
    public float buffDuration = 6.0f;
    [Tooltip("ตัวคูณมูลค่า EXP ของลูกแก้วที่อยู่ในเขตระเบิด (เช่น 2.0 = เพิ่ม EXP 2 เท่า)")]
    public float expUpgradeMultiplier = 2.0f;

    protected override void Explode(Vector3 center, float radius, float dmg, bool isCrit)
    {
        // 1. ดำเนินการระเบิดและทำความเสียหายปกติ
        base.Explode(center, radius, dmg, isCrit);

        // 2. เรียกใช้งาน ServerRpc เพื่อทำการบัฟเพลเยอร์ในระยะและอัปเกรดลูกแก้ว ExpOrb ทั้งหมดบนเซิร์ฟเวอร์
        if (manager != null)
        {
            manager.ApplySuperBigAoEExplosionServerRpc(center, radius, damageBuffAmount, buffDuration, expUpgradeMultiplier);
        }
    }
}
