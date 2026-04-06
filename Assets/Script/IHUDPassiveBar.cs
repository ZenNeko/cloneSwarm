using UnityEngine;

/// <summary>
/// Interface สำหรับ Passive ที่ต้องการแสดงใน Charge/Progress Bar ของ HUD
///
/// ทุก script ที่ต้องการ bar ให้ implement interface นี้
/// GameHUD จะค้นหา IHUDPassiveBar ผ่าน GetComponentInChildren อัตโนมัติ
///
/// ตัวอย่าง:
///   public class GunnerPassiveWeapon : WeaponBase, IHUDPassiveBar { ... }
/// </summary>
public interface IHUDPassiveBar
{
    /// <summary>ค่า fill 0-1 ของ bar</summary>
    float NormalizedValue { get; }

    /// <summary>ข้อความที่แสดงบน bar เช่น "12 / 30", "BUFFED!", "READY!"</summary>
    string BarText { get; }

    /// <summary>
    /// true = passive นี้ใช้งานอยู่จริงสำหรับ character นี้
    /// ใช้เลือก passive bar ที่ถูกต้องเมื่อมีหลาย IHUDPassiveBar บน player
    /// (เช่น ChargeManager มีอยู่เสมอ แต่ active เฉพาะ Riven)
    /// </summary>
    bool IsActivePassive { get; }

    /// <summary>true = bar เต็ม / triggered → ใช้ TriggeredColor และ text พิเศษ</summary>
    bool IsTriggered { get; }

    /// <summary>สีปกติของ bar</summary>
    Color BarColor { get; }

    /// <summary>สีเมื่อ triggered / full</summary>
    Color TriggeredColor { get; }
}
