using UnityEngine;

/// <summary>
/// Data-driven config สำหรับ Mini Boss — เลือก mechanic ได้ 3 แบบ:
///   • CircleLine — Phase 1 ของ MainBoss (Circle ↔ Line สลับกัน)
///   • Tether    — เสาผูกกับผู้เล่น ต้องวิ่งหนีเสา
///   • Chase     — วงแดงตามหลังผู้เล่น ระเบิดท้าย warning
///
/// Death Drops: spawn extra ExpOrb หลายลูกกระจายรอบบอส
///
/// สร้างผ่าน Right-click → Create → Game → MiniBossConfig
/// </summary>
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "Game/MiniBossConfig")]
public class MiniBossConfig : ScriptableObject
{
    public enum Mechanic { CircleLine, Tether, Chase }

    [Header("Mechanic")]
    [Tooltip("ประเภท attack ของ mini boss นี้")]
    public Mechanic mechanic = Mechanic.CircleLine;

    [Header("Attack Timing")]
    [Tooltip("ความถี่ attack (วินาที)")]
    public float attackInterval   = 5f;
    [Tooltip("หน่วงก่อน attack แรก")]
    public float firstAttackDelay = 4f;

    // ── Mechanic: CircleLine ──────────────────────────────────────────────
    [Header("Circle AoE  (CircleLine mechanic)")]
    public float circleRadius   = 3f;
    public float circleDamage   = 18f;
    public float circleWarnTime = 2.5f;

    [Header("Line AoE  (CircleLine mechanic)")]
    public float lineLength     = 8f;
    public float lineWidth      = 1.8f;
    public float lineDamage     = 22f;
    public float lineWarnTime   = 2.5f;

    // ── Mechanic: Tether ──────────────────────────────────────────────────
    [Header("Tether  (Tether mechanic)")]
    [Tooltip("ระยะที่ต้องวิ่งหนีเสา (เมตร)")]
    public float tetherDistance     = 6f;
    [Tooltip("เวลา tether (วินาที)")]
    public float tetherDuration     = 5f;
    [Tooltip("ดาเมจถ้าวิ่งไม่ทัน")]
    public float tetherFailDamage   = 30f;
    [Tooltip("Solo: offset สุ่มจากผู้เล่นที่ spawn เสา (เมตร)")]
    public float tetherSoloOffset   = 2f;

    // ── Mechanic: Chase ───────────────────────────────────────────────────
    [Header("Chase AoE  (Chase mechanic)")]
    public float chaseRadius   = 2.5f;
    public float chaseDamage   = 20f;
    public float chaseWarnTime = 3f;

    // ── Death Drops ───────────────────────────────────────────────────────
    [Header("Death Drops")]
    [Tooltip("จำนวน ExpOrb เพิ่มเติมที่ spawn ตอนตาย (เพิ่มจาก orb default ของ Enemy)")]
    [Min(0)] public int extraExpOrbs       = 4;
    [Tooltip("EXP ของ orb เพิ่มเติมแต่ละลูก")]
    public float        extraExpPerOrb    = 5f;
    [Tooltip("รัศมีกระจาย orb รอบบอสเมื่อ spawn")]
    public float        dropScatterRadius = 2f;
}
