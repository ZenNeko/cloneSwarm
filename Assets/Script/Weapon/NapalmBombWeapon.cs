using UnityEngine;

/// <summary>
/// Napalm Bomb (ระเบิดนาปาล์ม) — Super version ของ Molotov Cocktail
/// โยนระเบิดขนาดยักษ์ระเบิดสร้างความเสียหายรุนแรง และกระจายเปลวเพลิงนาปาล์มล้อมรอบเป็นสระเพลิงขนาดใหญ่
///
/// Super tier — 1 level
///   dmg=150, cd=2.5s, range=9, radius=4.5
/// </summary>
public class NapalmBombWeapon : MolotovWeapon
{
    // ใช้สืบทอดโครงสร้างและตรรกะ OnFire ทั้งหมดจาก MolotovWeapon
    // แต่ออกแบบไว้แยกคลาสเพื่อให้ Unity Editor สามารถอ้างอิงประเภทสคริปต์ได้ตรงตามประเภท Super Version
}
