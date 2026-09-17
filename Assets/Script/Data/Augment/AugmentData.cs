using UnityEngine;

/// <summary>
/// Augment — พลังพิเศษที่เลือกได้เป็นการ์ดใบหนึ่ง แต่ได้จากทางเฉพาะ
/// (เลเวลที่กำหนดใน <c>SharedExperienceManager.augmentLevels</c> หรือเก็บ orb ที่ตั้ง reward = Augment)
///
/// ═══ ไม่มีระดับ ไม่มีเลเวล — มี "ช่วงเวลาที่ออกได้" แทน ═══
///
/// แบบ TFT: augment ไม่ได้แบ่งเป็น Silver/Gold/Prismatic ในเกมนี้ และถือได้ใบละครั้งเดียว
/// สิ่งที่กำหนดว่าใบไหนจะโผล่คือ **นาทีที่มันออกได้** — Expedition กับ Silver Spoon
/// มีความหมายเฉพาะตอนต้นเกม ส่วนของแรงๆ อย่าง Call to Chaos ต้องรอให้เกมเดินไปก่อน
///
/// ระดับความแรงจึงถูกคุมด้วย **ตำแหน่งในเวลา** ไม่ใช่ป้ายที่แปะไว้:
/// ใบที่แรงมากตั้งให้ออกได้เฉพาะช่วงท้าย ไม่ต้องมีคำว่า Prismatic มาบอก
///
/// ═══ ทำไมถึงดีกว่าป้ายระดับ ═══
///
/// ป้ายระดับบอก **ราคา** แต่ไม่บอก **จังหวะ** · augment ที่ให้ของตอนต้นเกมแรงมาก
/// แต่ได้ตอนนาทีที่ 14 คือใบเปล่า ซึ่งป้าย "Gold" ไม่ได้กันเรื่องนั้นเลย
/// ช่วงเวลากันได้ตรงๆ และเป็นค่าที่ designer จูนได้โดยไม่ต้องแตะโค้ด
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
    [Tooltip("น้ำหนักการสุ่มภายในใบที่ออกได้ในช่วงเวลานั้น\n\n" +
             "**ไม่ใช่ระดับความแรง** — ความแรงคุมด้วยช่วงเวลาข้างล่าง\n" +
             "ตัวนี้บอกแค่ว่า 'เจอบ่อยแค่ไหน' เทียบกับใบอื่นที่ออกได้พร้อมกัน")]
    public float weight = 100f;
    [Tooltip("ว่าง = ทุกตัวละครสุ่มได้ / ใส่ = เฉพาะตัวละครนี้")]
    public CharacterData exclusiveCharacter;

    [Header("Availability — นาทีของเกม")]
    [Tooltip("นาทีที่เริ่มออกได้ · 0 = ตั้งแต่เริ่มเกม\n\n" +
             "รันหนึ่งรอบยาว 15 นาที (บอสใหญ่ที่ GameTimeline.mainBossTimeMin)\n" +
             "0–5 = ต้นเกม · 5–10 = กลางเกม · 10–15 = ท้ายเกม")]
    [Min(0f)]
    public float availableFromMinutes = 0f;

    [Tooltip("นาทีสุดท้ายที่ยังออกได้ · **0 = ไม่มีกำหนดปิด** (ออกได้จนจบเกม)\n\n" +
             "ใช้กับใบที่มีความหมายเฉพาะตอนต้น — ได้ตอนท้ายแล้วเป็นใบเปล่า\n" +
             "เช่นใบที่ให้ผลสะสมตามเวลาที่เหลือ")]
    [Min(0f)]
    public float availableUntilMinutes = 0f;

    /// <summary>
    /// ใบนี้ออกได้ที่นาทีนี้ไหม
    /// </summary>
    /// <param name="gameSeconds">
    /// เวลาของเกมเป็น **วินาที** — ค่าที่ <c>GameTimeline.GetGameTime()</c> คืนมา
    /// รับเป็นวินาทีเพราะผู้เรียกทุกคนมีค่านั้นอยู่แล้ว การให้แต่ละที่หารเองคือ
    /// การเปิดช่องให้ลืมหารสักที่หนึ่ง แล้วใบนั้นจะออกเร็วกว่าที่ตั้งไว้ 60 เท่าโดยเงียบ
    /// </param>
    public bool IsAvailableAt(float gameSeconds)
    {
        float minutes = gameSeconds / 60f;

        if (minutes < availableFromMinutes) return false;
        if (availableUntilMinutes > 0f && minutes > availableUntilMinutes) return false;
        return true;
    }

    /// <summary>ข้อความช่วงเวลาสำหรับเครื่องมือฝั่ง Editor — ไม่ได้โชว์ให้ผู้เล่นเห็น</summary>
    public string WindowLabel =>
        availableUntilMinutes > 0f
            ? $"{availableFromMinutes:0.#}–{availableUntilMinutes:0.#} นาที"
            : $"{availableFromMinutes:0.#} นาทีขึ้นไป";

    /// <summary>ทำงานตอนผู้เล่นเลือกการ์ดใบนี้ — รันบน owner client (และ server ผ่าน mirror RPC)</summary>
    public abstract void OnAcquire(PlayerAugmentManager ctx);
}
