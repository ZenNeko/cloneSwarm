using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ข้อมูล Phase ของบอส กำหนด HP ที่เปลี่ยน Phase และคิวท่าโจมตี
/// </summary>
[System.Serializable]
public class BossPhase
{
    [Tooltip("เปอร์เซ็นต์เลือดที่ลดลงมาถึงจุดนี้แล้วจะเปลี่ยนเข้าสู่ Phase ถัดไป (0.0 - 1.0) เช่น 0.75")]
    [Range(0f, 1f)]
    public float transitionHealthPct = 0f;

    [Tooltip("ระยะเวลาอมตะเมื่อเปลี่ยนเข้าสู่ Phase นี้ (วินาที)")]
    public float invincibilityDuration = 1.5f;

    [Tooltip("ช่วงเวลาระหว่างการโจมตีเริ่มต้นของเฟสนี้ (หากเป็น 0 หรือน้อยกว่า จะอิงตาม Config หลัก)")]
    public float attackInterval = -1f;

    [Tooltip("ความแรงของการสั่นกล้องเมื่อเข้าสู่ Phase นี้")]
    public float cameraShakeMagnitude = 0.4f;

    [Tooltip("รายการท่าโจมตีประจำ Phase นี้ (ทำงานเรียงตามลำดับ หรือปล่อยคู่ขนาน)")]
    public List<BossAction> actions = new List<BossAction>();

    [Header("Enrage Settings")]
    [Tooltip("เวลาจำกัดของ Phase นี้ (วินาที) ถ้าเกินเวลา บอสจะสลับไปยิงท่า Enrage รัวๆ (-1 = ไม่มีเวลาจำกัด)")]
    public float enrageTime = -1f;

    [Tooltip("รายการท่าโจมตีเมื่อหมดเวลา (สุ่มยิงหรือยิงเรียงตามลำดับ)")]
    public List<BossAction> enrageActions = new List<BossAction>();
}
