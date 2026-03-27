using UnityEngine;

public enum UpgradeType
{
    Damage,
    AttackSpeed,
    AttackRange,
    MoveSpeed,
    MaxHealth,
    ProjectileSpeed,
    ExpBonus,         // เพิ่ม EXP multiplier (shared)
    MultiProjectile,  // เพิ่มจำนวน projectile ต่อยิง
    HealthRegen       // ฟื้น HP ต่อวินาที
}

public enum UpgradeApplicationMode
{
    Additive,         // stat += value
    Multiplicative    // stat *= (1 + value)
}

[CreateAssetMenu(fileName = "Upgrade_New", menuName = "LoL Swarm/Weapon Upgrade")]
public class WeaponUpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string upgradeName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Effect")]
    public UpgradeType upgradeType;
    public UpgradeApplicationMode mode = UpgradeApplicationMode.Additive;
    public float value;

    [Header("Stacking")]
    [Tooltip("0 = ไม่จำกัด stack")]
    public int maxStacks = 5;
}
