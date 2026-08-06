using UnityEngine;

/// <summary>ระดับความแรงของ Augment — คุมสีการ์ดและน้ำหนักการสุ่ม</summary>
public enum AugmentRarity
{
    Silver,      // พื้นฐาน มีทุกรอบ
    Gold,        // แรงขึ้น เปลี่ยนสไตล์การเล่นเล็กน้อย
    Prismatic,   // build-defining
}

/// <summary>
/// Augment — พลังพิเศษที่เลือกได้ตอนถึงเลเวลที่กำหนด (สไตล์ LoL Swarm)
/// ต่างจาก Stat card ตรงที่ **ไม่กิน stat slot** และผลแรงกว่ามาก
///
/// นี่คือ base class — ใช้ subclass ที่มีอยู่:
///   • StatAugment        → โบนัสสเตตัสก้อนใหญ่
///   • TriggerAugment     → เอฟเฟกต์ที่ทำงานตามเงื่อนไข (ทุก N kill / โดนตี / เป็นระยะ)
///   • WeaponGrantAugment → แถมอาวุธหรือสกิลทันที
///
/// จะเขียน subclass ใหม่ก็ override <see cref="OnAcquire"/> พอ
/// </summary>
public abstract class AugmentData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("คีย์ถาวร — ใช้อ้างอิงข้ามเครื่อง อย่าเปลี่ยนหลังปล่อยเกม")]
    public string augmentId = "aug_new";
    public string augmentName = "New Augment";
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Pool")]
    public AugmentRarity rarity = AugmentRarity.Silver;
    [Tooltip("น้ำหนักการสุ่ม — Silver≈100  Gold≈40  Prismatic≈12")]
    public float weight = 100f;
    [Tooltip("ว่าง = ทุกตัวละครสุ่มได้ / ใส่ = เฉพาะตัวละครนี้")]
    public CharacterData exclusiveCharacter;
    [Tooltip("จำนวนครั้งสูงสุดที่ผู้เล่นคนเดียวถือ augment นี้ได้ใน 1 run")]
    [Min(1)]
    public int maxStacks = 1;

    /// <summary>ทำงานตอนผู้เล่นเลือกการ์ดใบนี้ — รันบน owner client (และ server ผ่าน mirror RPC)</summary>
    public abstract void OnAcquire(PlayerAugmentManager ctx);

    /// <summary>ข้อความ rarity สำหรับโชว์บนการ์ด</summary>
    public string RarityLabel => rarity switch
    {
        AugmentRarity.Silver    => "SILVER",
        AugmentRarity.Gold      => "GOLD ★",
        AugmentRarity.Prismatic => "PRISMATIC ★★★",
        _                       => "",
    };

    public Color RarityColor => rarity switch
    {
        AugmentRarity.Silver    => new Color(0.62f, 0.66f, 0.72f),
        AugmentRarity.Gold      => new Color(0.90f, 0.72f, 0.20f),
        AugmentRarity.Prismatic => new Color(0.70f, 0.35f, 0.95f),
        _                       => Color.white,
    };
}
