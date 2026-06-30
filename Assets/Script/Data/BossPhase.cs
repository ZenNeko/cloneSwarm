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

    [Tooltip("ความแรงของการสั่นกล้องเมื่อเข้าสู่ Phase นี้")]
    public float cameraShakeMagnitude = 0.4f;

    [Tooltip("รายการท่าโจมตีประจำ Phase นี้ (ทำงานเรียงตามลำดับ หรือปล่อยคู่ขนาน)")]
    public List<BossAction> actions = new List<BossAction>();
}
