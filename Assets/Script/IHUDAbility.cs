using UnityEngine;

/// <summary>
/// Interface สำหรับ Ability ที่ต้องการแสดงใน HUD slot (Q / E / R)
///
/// ทุก AbilityBase / WeaponBase ที่ต้องการ slot ใน HUD ให้ implement interface นี้
/// GameHUD จะค้นหา IHUDAbility ผ่าน GetComponentsInChildren อัตโนมัติ
/// ไม่ต้อง hardcode ใน GameHUD เมื่อเพิ่ม character ใหม่
///
/// ตัวอย่าง:
///   public class MyAbility : AbilityBase, IHUDAbility { ... }
/// </summary>
public interface IHUDAbility
{
    /// <summary>"Q", "E", หรือ "R" — กำหนด slot ที่จะแสดงใน HUD</summary>
    string HUDSlotKey { get; }

    /// <summary>ข้อความบน key hint เช่น "Q", "E", "R"</summary>
    string HUDKeyLabel { get; }

    /// <summary>icon ที่จะแสดงใน HUD slot — null = ให้ HUD คงรูปที่ตั้งไว้ใน prefab
    /// AbilityBase implement ให้แล้ว (คืน AbilityData.icon) — subclass ไม่ต้องเขียนเอง
    /// WeaponBase ที่ implement interface นี้ต้องเขียนเอง</summary>
    Sprite HUDIcon { get; }

    // ── Cooldown ──────────────────────────────────────────────────────────
    bool  IsOnCooldown      { get; }
    float CooldownRemaining { get; }
    float CooldownMax       { get; }

    // ── Active Mode (toggle abilities เช่น Exile / RocketMode / Ultimate) ─
    /// <summary>true = ability กำลัง active อยู่ → แสดง glow + countdown</summary>
    bool  IsActiveMode    { get; }
    float ActiveRemaining { get; }   // ถ้าไม่มี active mode ให้คืน 0
    float ActiveMax       { get; }   // ถ้าไม่มี active mode ให้คืน 0
}
