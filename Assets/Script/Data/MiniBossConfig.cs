using UnityEngine;

/// <summary>
/// Data-driven config สำหรับ Mini Boss
///   Mechanic: Circle ↔ Line สลับ (เหมือน Phase 1 ของ MainBoss แต่ค่าน้อยกว่า)
///
/// สร้างผ่าน Right-click → Create → Game → MiniBossConfig
/// </summary>
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "Game/MiniBossConfig")]
public class MiniBossConfig : ScriptableObject
{
    [Header("Attack Timing")]
    [Tooltip("ความถี่ attack (วินาที) — Mini Boss attack ช้ากว่า Boss หลัก")]
    public float attackInterval   = 5f;
    [Tooltip("หน่วงก่อน attack แรก")]
    public float firstAttackDelay = 4f;

    [Header("Circle AoE")]
    [Tooltip("รัศมีวงกลม — เล็กกว่า Boss หลัก")]
    public float circleRadius     = 3f;
    [Tooltip("ดาเมจ")]
    public float circleDamage     = 18f;
    [Tooltip("ระยะ warning")]
    public float circleWarnTime   = 2.5f;

    [Header("Line AoE")]
    [Tooltip("ความยาวลำตัว Line")]
    public float lineLength       = 8f;
    [Tooltip("ความกว้าง Line")]
    public float lineWidth        = 1.8f;
    [Tooltip("ดาเมจ")]
    public float lineDamage       = 22f;
    [Tooltip("ระยะ warning")]
    public float lineWarnTime     = 2.5f;
}
