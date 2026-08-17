using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ค่าคงที่ของ "แหล่งที่มา" โล่ — ใช้กับ <see cref="ShieldStack.Refresh"/> / <see cref="playermove.RefreshShield"/>
/// เพื่อไม่ให้ผู้เรียกคิดเลข sourceId เอาเองแล้วชนกันโดยไม่ตั้งใจ (ADR-008 D1 หมายเหตุท้าย: รองรับ
/// กฎ "ชื่อเดียวกันทับทิ้ง" แบบ FF14 ต่อแหล่ง ไม่ใช่ทั้งระบบ)
///
/// เพิ่มค่าใหม่ที่นี่เวลามีแหล่งโล่ที่ต้อง "รีเฟรชชั้นเดียว ไม่สะสม" (ดู ADR-008 D5)
/// แหล่งที่ยังสแตกได้อิสระ (BunnyHop / StormBunny / Augment Second Wind) ใช้ <see cref="Unassigned"/>
/// ผ่าน overload <c>AddShield(float amount, ...)</c> ตามปกติ ไม่ต้องมี id เฉพาะ
/// </summary>
public static class ShieldSourceId
{
    /// <summary>ไม่ผูกกับแหล่งเฉพาะ — เพิ่มเป็นชั้นใหม่เสมอ (สแตกได้อิสระ) ห้ามใช้กับ RefreshShield</summary>
    public const int Unassigned = 0;

    /// <summary>เสา Support Arena (ADR-008 D5) — ยืนในวงแล้วรีเฟรชชั้นเดียว ออกจากวงแล้วสลาย
    /// ไม่ใช่พอกไปเรื่อยจนลู่เข้า 3 เท่าของ maxHealth เหมือนพฤติกรรมเดิม</summary>
    public const int SupportArena = 1;
}

/// <summary>
/// หนึ่งชั้นของโล่ (ADR-008 D1) — ก้อนข้อมูลล้วน ไม่มี MonoBehaviour/NetworkVariable
///
/// มี field <see cref="duration"/> เพิ่มจากโครงที่ ADR ร่างไว้ (amount/initialAmount/expireAt/decays/sourceId)
/// เพราะสูตร decay ต่อเฟรม `initialAmount / duration` (ADR-008 D2) ต้องมี duration คงที่ตลอดอายุชั้น —
/// ถ้าคำนวณย้อนจาก expireAt ทุกเฟรมจะเจอบั๊กแบบเดียวกับที่กำลังแก้ (ค่าที่ใช้คำนวณเปลี่ยนได้ระหว่างทาง)
/// duration ถูก "ตรึง" ไว้ตอนสร้างชั้น (คูณ Duration stat ณ ขณะนั้นแล้ว) — ของเดิมก็ตรึงจาก snapshot
/// เหมือนกัน ต่างกันแค่วิธีใช้ค่าที่ตรึงไว้ (ลบสะสม ไม่ใช่ Lerp จาก snapshot)
/// </summary>
[System.Serializable]
public struct ShieldLayer
{
    /// <summary>ค่าที่เหลืออยู่ตอนนี้ — ลดลงทั้งจาก decay และจาก Absorb</summary>
    public float amount;
    /// <summary>ค่าตอนสร้างชั้น — ใช้เป็นตัวตั้งของอัตรา decay เท่านั้น ไม่ลดตาม amount</summary>
    public float initialAmount;
    /// <summary>อายุที่ตรึงไว้ตอนสร้าง/รีเฟรชชั้น (วินาที) — คูณ Duration stat ไปแล้วโดยผู้เรียก</summary>
    public float duration;
    /// <summary>Time.time ที่ชั้นนี้หมดอายุเต็มที่ (now ตอนสร้าง + duration) — ใช้จัดลำดับ Absorb
    /// และเป็นเงื่อนไข "หายทันที" ของชั้นที่ decays == false</summary>
    public float expireAt;
    /// <summary>true = ลดค่าต่อเนื่องทุกเฟรม (แบบ Sterak's Gage) · false = อยู่เต็มค่าจนถึง expireAt
    /// แล้วหายทันที (แบบ Immortal Shieldbow)</summary>
    public bool decays;
    /// <summary>แหล่งที่มา — ดู <see cref="ShieldSourceId"/> · Unassigned = สแตกอิสระ ไม่ใช้กับ Refresh</summary>
    public int sourceId;
}

/// <summary>
/// คณิตล้วนของระบบ shield แบบ "รายการชั้น" (ADR-008 D1/D2) — แยกออกจาก playermove (NetworkBehaviour)
/// เพื่อให้เทสได้โดยไม่ต้องมี scene/GameObject/NetworkVariable ทำนองเดียวกับที่ TelegraphGeometry.cs
/// แยกออกจาก TelegraphZone (ดู Assets/Editor/Tests/ShieldStackTests.cs)
///
/// playermove เป็นแค่ผู้เรียก (thin caller): ถือ List&lt;ShieldLayer&gt; ฝั่ง server, เรียกเมธอดที่นี่,
/// แล้ว sync ผลรวมเข้า netShieldHP.Value เอง — ไฟล์นี้ไม่รู้จัก NetworkVariable เลย
///
/// กติกาที่ห้ามเปลี่ยนโดยไม่ได้ตั้งใจ (ADR-008):
///  - Absorb ดูดจากชั้นที่ "ใกล้หมดอายุที่สุดก่อน" (expireAt น้อยสุดก่อน) — โมเดล LoL
///  - Tick ลดค่าเป็นการลบตรงๆ ต่อเฟรม ไม่ใช่ Lerp จาก snapshot — Absorb กับ Tick ต้องบวก/ลบผลกัน
///    ได้ตามลำดับใดก็ได้โดยไม่มีใครเขียนทับใคร (นี่คือบั๊ก CRITICAL ที่ ADR-008 แก้)
///  - ชั้นที่ decays == false อยู่เต็มค่าจนถึง expireAt แล้วหายทันที ไม่ค่อยๆ ลด
/// </summary>
public static class ShieldStack
{
    /// <summary>เกณฑ์ว่าค่าที่รับเข้ามาใช้ได้ไหม — ปฏิเสธ NaN/Infinity/ค่าติดลบ/ศูนย์ (ADR-008 D3)</summary>
    public static bool IsValidAmount(float amount) => amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);

    /// <summary>เพิ่มชั้นใหม่เข้า list เสมอ (สแตก) — ไม่ตรวจ/ไม่แทนที่ sourceId เดิม ใช้กับแหล่งที่ตั้งใจให้สแตกอิสระ</summary>
    public static void Add(List<ShieldLayer> layers, float amount, float duration, bool decays, int sourceId, float now)
    {
        if (!IsValidAmount(amount) || duration <= 0f) return;

        layers.Add(new ShieldLayer
        {
            amount        = amount,
            initialAmount = amount,
            duration      = duration,
            expireAt      = now + duration,
            decays        = decays,
            sourceId      = sourceId,
        });
    }

    /// <summary>
    /// แทนที่ชั้นที่มี sourceId เดียวกัน (amount/duration/expiry/decays ใหม่ทั้งหมด) แทนการสแตกซ้อน
    /// ถ้ายังไม่มีชั้นของ sourceId นี้ในระบบ → สร้างใหม่ (เหมือน Add) ใช้กับแหล่งแบบ TFT (ADR-008 D5)
    /// </summary>
    public static void Refresh(List<ShieldLayer> layers, int sourceId, float amount, float duration, bool decays, float now)
    {
        if (!IsValidAmount(amount) || duration <= 0f) return;

        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].sourceId != sourceId) continue;

            var l = layers[i];
            l.amount        = amount;
            l.initialAmount = amount;
            l.duration      = duration;
            l.expireAt      = now + duration;
            l.decays        = decays;
            layers[i] = l;
            return;
        }

        Add(layers, amount, duration, decays, sourceId, now);
    }

    /// <summary>
    /// หักดาเมจจากชั้นที่ใกล้หมดอายุที่สุดก่อน ไล่ไปเรื่อยๆ จนดาเมจหมดหรือชั้นหมด
    /// คืนดาเมจที่เหลือหลังโล่ดูดไม่ไหว (overkill) — ผู้เรียกเอาไปหักเลือดต่อ
    /// ชั้นที่ amount เหลือ 0 หรือน้อยกว่าถูกลบออกจาก list ทันที (กัน list โตไม่จำกัด)
    /// </summary>
    public static float Absorb(List<ShieldLayer> layers, float damage, float now)
    {
        if (float.IsNaN(damage)) return 0f;        // ค่าเสีย — ไม่ดูดอะไร ไม่ส่ง NaN ต่อให้ TakeDamage
        if (damage <= 0f) return damage;
        if (layers.Count == 0) return damage;

        // หาชั้นที่ใกล้หมดอายุที่สุดซ้ำไปเรื่อยๆ แทนการเรียงลำดับ
        //
        // เมธอดนี้ถูกเรียกทุกครั้งที่ผู้เล่นโดนตี ซึ่งในเกมแนวนี้คือหลายครั้งต่อวินาทีต่อคน
        // การจอง List<int> + Sort ที่มี lambda closure ทุกครั้งจะกลายเป็นขยะ GC ที่เห็นผลจริง
        // จำนวนชั้นตามปกติมีแค่ 1-3 การสแกนหาค่าน้อยสุดซ้ำจึงถูกกว่าและไม่จองหน่วยความจำเลย
        float remaining = damage;
        while (remaining > 0f)
        {
            int   best     = -1;
            float bestExp  = float.MaxValue;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].amount <= 0f) continue;
                if (layers[i].expireAt >= bestExp) continue;
                bestExp = layers[i].expireAt;
                best    = i;
            }
            if (best < 0) break;   // ไม่เหลือชั้นที่ยังมีค่า

            var l = layers[best];
            float absorbed = Mathf.Min(l.amount, remaining);
            l.amount    -= absorbed;
            remaining   -= absorbed;
            layers[best] = l;
        }

        // ลบจากท้ายไปหน้า — RemoveAll รับ predicate ที่เป็น closure จองหน่วยความจำเหมือนกัน
        for (int i = layers.Count - 1; i >= 0; i--)
            if (layers[i].amount <= 0f) layers.RemoveAt(i);

        return remaining;
    }

    /// <summary>
    /// ลดค่าทุกชั้นตามเวลาที่ผ่านไปหนึ่งเฟรม (ADR-008 D2 — ลบรายเฟรม ไม่ใช่ Lerp จาก snapshot)
    /// ชั้น decays == true ลดต่อเนื่อง · ชั้น decays == false อยู่เต็มจนถึง expireAt แล้วหายทันที
    /// ชั้นที่ amount ถึง 0 ถูกลบออกจาก list ทันที
    /// </summary>
    public static void Tick(List<ShieldLayer> layers, float deltaTime, float now)
    {
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var l = layers[i];

            if (l.decays)
            {
                if (l.duration > 0f)
                    l.amount = Mathf.Max(0f, l.amount - (l.initialAmount / l.duration) * deltaTime);
                layers[i] = l;
            }
            else if (now >= l.expireAt)
            {
                l.amount = 0f;
                layers[i] = l;
            }

            if (layers[i].amount <= 0f)
                layers.RemoveAt(i);
        }
    }

    /// <summary>ผลรวมของทุกชั้น — ค่าที่ sync เข้า netShieldHP.Value</summary>
    public static float Sum(List<ShieldLayer> layers)
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++) total += layers[i].amount;
        return total;
    }
}
