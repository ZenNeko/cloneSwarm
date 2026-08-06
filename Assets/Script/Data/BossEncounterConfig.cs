using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bonus drop entry — prefab GameObject + count + scatter radius
/// ใช้ใน BossEncounterConfig.bonusDrops เพื่อ drop ของพิเศษ (เช่น ObjectiveOrb) ตอนบอสตาย
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
/// Data-driven config สำหรับ Boss ทุกตัว (รวม MainBoss และ MiniBoss)
///
/// **Phases**: ระบุ Phase ตามลำดับ (เริ่มจาก Phase 1)
/// ถ้าเป็น Mini Boss ให้ใส่แค่ 1 Phase, ถ้าเป็น Main Boss ให้เพิ่ม Phase ตาม HP threshold
///
/// **Drops**:
///   • extraExpOrbs   → จำนวน ExpOrb เพิ่มเติม (ใช้ enemy.expOrbPrefab)
///   • bonusDrops[]   → custom prefab อื่นๆ เช่น ObjectiveOrb, special pickup
///
/// สร้างผ่าน Right-click → Create → Game → BossEncounterConfig
/// </summary>
[CreateAssetMenu(fileName = "BossEncounterConfig", menuName = "Game/BossEncounterConfig")]
public class BossEncounterConfig : ScriptableObject
{
    [Header("Arena")]
    [Tooltip("สนามของ encounter นี้ — ว่าง = ใช้ค่า default")]
    public ArenaDefinition arena;

    [Header("Roll Definitions")]
    [Tooltip("ค่าสุ่มที่ตั้งชื่อไว้ — คลิปอ้างด้วย rollName")]
    public RollDefinition[] rolls;

    [Header("Phases Configuration")]
    [Tooltip("ใส่ Phase ตามลำดับ (Mini Boss ใส่ 1 อัน, Main Boss ใส่ตามต้องการ)")]
    public List<BossPhase> phases = new List<BossPhase>();

    [Header("Attack Timing (Default)")]
    [Tooltip("ความถี่ในการเริ่มใช้ Action ถัดไป (วินาที) (ใช้ถ้า BossAction.cooldownAfter = 0)")]
    public float attackInterval = 5f;
    [Tooltip("หน่วงเวลาก่อนเริ่ม Action แรกเมื่อบอสเกิด")]
    public float firstAttackDelay = 4f;

    // ── Death Drops ───────────────────────────────────────────────────────
    [Header("Death Drops — ExpOrb")]
    [Tooltip("จำนวน ExpOrb เพิ่มเติมที่ spawn ตอนตาย (เพิ่มจาก orb default ของ Enemy)")]
    [Min(0)] public int extraExpOrbs = 4;
    [Tooltip("EXP ของ orb เพิ่มเติมแต่ละลูก")]
    public float extraExpPerOrb = 5f;
    [Tooltip("รัศมีกระจาย ExpOrb รอบบอสเมื่อ spawn")]
    public float dropScatterRadius = 2f;

    [Header("Death Drops — Bonus GameObjects")]
    [Tooltip("Custom prefab drops เพิ่มเติม — เช่น ObjectiveOrb, special pickup\nทุก prefab ต้องมี NetworkObject component")]
    public BonusDrop[] bonusDrops = new BonusDrop[0];
}
