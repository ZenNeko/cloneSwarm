using UnityEngine;

/// <summary>
/// Elite Modifier — เพิ่มความสามารถพิเศษให้ enemy ปกติ
///
/// Behavior types:
///   Shield    — มี HP ชั้นพิเศษ ก่อนจะโดน netHealth
///   Rage      — เมื่อ HP < threshold → speed/damage boost
///   Split     — ตายแล้วแตกเป็น mini copies
///   Exploder  — ตายแล้ว AoE ทำ damage
///
/// สร้างผ่าน Right-click → Create → Game → EliteModifierDef
/// </summary>
[CreateAssetMenu(fileName = "EliteMod", menuName = "Game/EliteModifierDef")]
public class EliteModifierDef : ScriptableObject
{
    public enum BehaviorType { Shield, Rage, Split, Exploder }

    [Header("Identity")]
    [Tooltip("unique id — ใช้สำหรับ network sync")]
    public string id = "elite_default";
    [Tooltip("ชื่อแสดงใน UI/log")]
    public string displayName = "Elite";

    [Header("Visual")]
    [Tooltip("สีของ outline รอบตัว")]
    public Color outlineColor = Color.yellow;
    [Tooltip("Crown prefab วางเหนือหัว — ปล่อยว่างได้ถ้าไม่ต้องการ")]
    public GameObject crownPrefab;

    [Header("Stat Bonuses (multipliers)")]
    [Tooltip("HP × bonus (1 = no change, 2 = double HP)")]
    public float healthMultBonus = 1.5f;
    public float speedMultBonus  = 1f;
    public float expMultBonus    = 2f;

    [Header("Behavior")]
    public BehaviorType behaviorType = BehaviorType.Shield;

    [Header("Shield (BehaviorType.Shield)")]
    [Tooltip("HP ชั้นพิเศษก่อนจะโดน netHealth")]
    public float shieldHP = 50f;

    [Header("Rage (BehaviorType.Rage)")]
    [Tooltip("trigger rage เมื่อ HP <= threshold (0-1)")]
    [Range(0f, 1f)]
    public float rageHpThreshold = 0.3f;
    [Tooltip("speed multiplier ตอน rage")]
    public float rageSpeedMult   = 1.8f;

    [Header("Split (BehaviorType.Split)")]
    [Tooltip("Prefab mini copy ที่จะ spawn ตอนตาย")]
    public GameObject splitPrefab;
    [Tooltip("จำนวน mini copy ที่ spawn")]
    [Range(1, 8)]
    public int splitCount = 2;

    [Header("Exploder (BehaviorType.Exploder)")]
    [Tooltip("รัศมี AoE ตอนระเบิด")]
    public float exploderRadius = 3f;
    [Tooltip("ดาเมจ AoE ต่อ player")]
    public float exploderDamage = 30f;
    [Tooltip("VFX ตอนระเบิด — default GrenadeExplosion")]
    public VFXType exploderVfxType = VFXType.GrenadeExplosion;
}
