using UnityEngine;

/// <summary>เงื่อนไขที่ทำให้ augment ทำงาน</summary>
public enum AugmentTrigger
{
    EveryNKills,   // ทุกๆ N ตัวที่ "ทีม" ฆ่าได้ (นับจาก Enemy.OnAnyEnemyDied)
    OnLevelUp,     // ทุกครั้งที่ shared level เพิ่ม
    OnTakeDamage,  // ทุกครั้งที่เจ้าของ augment โดนตี
    Periodic,      // ทุกๆ N วินาที
}

/// <summary>ผลที่เกิดเมื่อ trigger ทำงาน</summary>
public enum AugmentEffect
{
    HealFlat,        // ฟื้น HP เท่ากับ magnitude
    HealPercent,     // ฟื้น HP เป็น % ของ maxHealth (0.1 = 10%)
    Shield,          // เพิ่ม shield เท่ากับ magnitude
    TempHaste,       // +haste ชั่วคราว duration วินาที
    TempDamage,      // +% damage ชั่วคราว
    TempMoveSpeed,   // +% ความเร็วชั่วคราว
    PlayVfx,         // เล่น VFX ที่ตัวผู้เล่น (ใช้คู่กับ effect อื่นเพื่อความสวย)
}

/// <summary>
/// Augment ที่ทำงานตามเงื่อนไข — Assets &gt; Create &gt; LoL Swarm/Augment/Trigger Augment
///
/// ตัวอย่าง "Bloodthirst": EveryNKills(20) → HealPercent 0.05
/// ตัวอย่าง "Adrenaline":  OnTakeDamage    → TempMoveSpeed 0.30 นาน 2 วิ
/// </summary>
[CreateAssetMenu(fileName = "Aug_Trigger_New", menuName = "LoL Swarm/Augment/Trigger Augment")]
public class TriggerAugment : AugmentData
{
    [Header("Trigger")]
    public AugmentTrigger trigger = AugmentTrigger.EveryNKills;
    [Tooltip("EveryNKills → จำนวนศัตรู  ·  Periodic → วินาที  ·  trigger อื่นไม่ใช้")]
    [Min(1f)]
    public float triggerAmount = 20f;
    [Tooltip("วินาทีที่ต้องรอก่อน trigger ทำงานได้อีก (0 = ไม่มี cooldown)")]
    [Min(0f)]
    public float internalCooldown = 0f;

    [Header("Effect")]
    public AugmentEffect effect = AugmentEffect.HealPercent;
    [Tooltip("ขนาดของผล — % → decimal, flat → ใส่ตรงๆ")]
    public float magnitude = 0.05f;
    [Tooltip("วินาทีของบัฟชั่วคราว (ใช้กับ Temp* เท่านั้น)")]
    public float duration = 3f;

    [Header("Presentation")]
    [Tooltip("VFX key ใน NetworkedVFXPool ที่เล่นตอน trigger ทำงาน — เว้นว่าง/None = ไม่เล่น")]
    public string vfxKeyOnTrigger = "";

    /// <summary>ลงทะเบียนกับ manager — manager เป็นคนคอย tick และเรียก Fire ให้</summary>
    public override void OnAcquire(PlayerAugmentManager ctx)
    {
        ctx?.RegisterTrigger(this);
    }
}
