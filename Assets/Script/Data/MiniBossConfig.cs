using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bonus drop entry — prefab GameObject + count + scatter radius
/// ใช้ใน MiniBossConfig.bonusDrops เพื่อ drop ของพิเศษ (เช่น ObjectiveOrb) ตอนบอสตาย
/// </summary>
[System.Serializable]
public class BonusDrop
{
    [Tooltip("Prefab ที่มี NetworkObject — spawn ตอนบอสตาย")]
    public GameObject prefab;
    [Tooltip("จำนวนที่จะ spawn")]
    [Min(0)] public int count = 1;
    [Tooltip("รัศมีกระจายรอบบอส (0 = ที่ตำแหน่งบอสทั้งหมด)")]
    public float scatterRadius = 1.5f;
}

/// <summary>
/// Data-driven config สำหรับ Mini Boss — รองรับหลาย mechanic + custom drops
///
/// **Mechanics**: ใส่ได้หลายตัวใน `mechanics` list — บอสจะหมุนใช้ตามลำดับ
///   เช่น  [CircleLine, Tether]  →  Circle → Tether → Line → Tether → Circle → ...
///   (CircleLine สลับ Circle ↔ Line ภายในเอง, mechanic อื่นใช้ทุกครั้งที่ถึงรอบ)
///
/// **Drops**:
///   • extraExpOrbs   → จำนวน ExpOrb เพิ่มเติม (ใช้ enemy.expOrbPrefab)
///   • bonusDrops[]   → custom prefab อื่นๆ เช่น ObjectiveOrb, special pickup
///
/// สร้างผ่าน Right-click → Create → Game → MiniBossConfig
/// </summary>
[CreateAssetMenu(fileName = "MiniBossConfig", menuName = "Game/MiniBossConfig")]
public class MiniBossConfig : ScriptableObject
{
    public enum Mechanic { CircleLine, Tether, Chase }

    [Header("Mechanics (ใส่ได้หลายตัว — บอสจะหมุนใช้ตามลำดับ)")]
    [Tooltip("ใส่ mechanic อย่างน้อย 1 ตัว — บอสจะ cycle ผ่าน list นี้ทุก attackInterval\n" +
             "ตัวอย่าง [CircleLine, Tether] → Circle → Tether → Line → Tether → Circle → ...")]
    public List<Mechanic> mechanics = new() { Mechanic.CircleLine };

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
    [Header("Death Drops — ExpOrb")]
    [Tooltip("จำนวน ExpOrb เพิ่มเติมที่ spawn ตอนตาย (เพิ่มจาก orb default ของ Enemy)")]
    [Min(0)] public int extraExpOrbs       = 4;
    [Tooltip("EXP ของ orb เพิ่มเติมแต่ละลูก")]
    public float        extraExpPerOrb    = 5f;
    [Tooltip("รัศมีกระจาย ExpOrb รอบบอสเมื่อ spawn")]
    public float        dropScatterRadius = 2f;

    [Header("Death Drops — Bonus GameObjects")]
    [Tooltip("Custom prefab drops เพิ่มเติม — เช่น ObjectiveOrb, special pickup\n" +
             "ทุก prefab ต้องมี NetworkObject component")]
    public BonusDrop[] bonusDrops = new BonusDrop[0];
}
