using UnityEngine;

/// <summary>
/// Data-driven config สำหรับ Mini Boss 2 ประเภท
///   Type A — ChaseAoE  : วงแดงตามหลัง player (ง่ายกว่า boss หลัก)
///   Type B — FloorHazard: พื้นอันตราย + Safe Zone (ง่ายกว่า boss หลัก)
///
/// สร้างผ่าน Right-click → Create → Game → MiniBossConfig
/// </summary>
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "Game/MiniBossConfig")]
public class MiniBossConfig : ScriptableObject
{
    public enum MiniBossType { ChaseAoE, FloorHazard }

    [Header("Mechanic Type")]
    public MiniBossType mechanic = MiniBossType.ChaseAoE;

    [Header("Attack Timing")]
    [Tooltip("ความถี่ attack (วินาที) — Mini Boss attack ช้ากว่า Boss หลัก")]
    public float attackInterval = 8f;
    [Tooltip("หน่วงก่อน attack แรก")]
    public float firstAttackDelay = 4f;

    [Header("Chase AoE Settings (Type A)")]
    [Tooltip("รัศมีวงกลม Chase — เล็กกว่า Boss หลัก")]
    public float chaseRadius   = 2.5f;
    [Tooltip("ดาเมจ")]
    public float chaseDamage   = 20f;
    [Tooltip("ระยะ warning ที่วิ่งตาม")]
    public float chaseWarnTime = 3f;

    [Header("Floor Hazard Settings (Type B)")]
    [Tooltip("รัศมี Arena อันตราย")]
    public float hazardArenaRadius    = 14f;
    [Tooltip("จำนวน Safe Zone (1–3) — Mini Boss มีมากกว่า Boss หลัก = ง่ายกว่า")]
    [Range(1, 3)]
    public int   hazardSafeZoneCount  = 2;
    [Tooltip("รัศมีแต่ละ Safe Zone — ใหญ่กว่า Boss หลัก")]
    public float hazardSafeZoneRadius = 4f;
    [Tooltip("ระยะ warning")]
    public float hazardWarnTime       = 5f;
    [Tooltip("ดาเมจผู้เล่นที่อยู่นอก Safe Zone")]
    public float hazardDamage         = 25f;
}
