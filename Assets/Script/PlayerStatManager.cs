using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// จัดการ Stat Upgrades ของผู้เล่น (max 6 slots, Lv1-5 ต่อ type)
/// ทำงานบน Owner เท่านั้น
/// WeaponBase และ playermove อ่าน multipliers จาก class นี้ทุก frame
/// </summary>
public class PlayerStatManager : NetworkBehaviour
{
    public const int MaxStatSlots = 6;

    private Dictionary<StatType, int>   statLevels    = new();   // type → current level (1-based)
    private Dictionary<StatType, float> statTotals    = new();   // type → total accumulated
    private List<StatData>              equippedStats = new();   // ordered list for HUD display

    // ── Weapon Multipliers (WeaponBase อ่าน) ─────────────────────────────
    /// <summary>+10% damage per Lv — 1.0 = no bonus</summary>
    public float GetPowerMultiplier()
        => 1f + GetTotal(StatType.Damage);

    /// <summary>Ability Haste → cooldown multiplier  (Haste / (100 + Haste))</summary>
    public float GetCooldownMultiplier()
    {
        float haste = GetTotal(StatType.AbilityHaste);
        return haste > 0f ? 100f / (100f + haste) : 1f;
    }

    /// <summary>+11% per Lv → range / AoE scale</summary>
    public float GetAreaMultiplier()
        => 1f + GetTotal(StatType.AreaSize);

    /// <summary>+12% per Lv → projectile maxRange scale</summary>
    public float GetDurationMultiplier()
        => 1f + GetTotal(StatType.Duration);

    /// <summary>crit chance 0–1</summary>
    public float GetCritChance()
        => Mathf.Clamp01(GetTotal(StatType.CriticalChance));

    /// <summary>extra projectiles ทุก weapon (integer sum)</summary>
    public int GetBonusProjectileCount()
        => Mathf.RoundToInt(GetTotal(StatType.ProjectileCount));

    // ── Player Multipliers (playermove อ่าน) ─────────────────────────────
    public float GetMoveSpeedMultiplier()
        => 1f + GetTotal(StatType.MoveSpeed);

    public float GetArmorValue()
        => GetTotal(StatType.Armor);

    public float GetPickupRadiusMultiplier()
        => 1f + GetTotal(StatType.PickupRadius);

    public float GetExpMultiplier()
        => 1f + GetTotal(StatType.ExpBonus);

    // ── Queries ───────────────────────────────────────────────────────────
    public int  GetStatLevel(StatType type)
        => statLevels.TryGetValue(type, out int l) ? l : 0;

    public bool HasStatSlotAvailable()
        => statLevels.Count < MaxStatSlots;

    public bool CanUpgradeStat(StatData s)
        => s != null && GetStatLevel(s.statType) < s.MaxLevel;

    /// <summary>คืน list ของ (StatData, level 1-based) ที่ equipped อยู่ สำหรับ HUD</summary>
    public List<(StatData sd, int level)> GetEquippedStats()
    {
        var result = new List<(StatData, int)>();
        foreach (var sd in equippedStats)
            result.Add((sd, GetStatLevel(sd.statType)));
        return result;
    }

    // ── Apply ─────────────────────────────────────────────────────────────
    public void ApplyStat(StatData stat, playermove pm)
    {
        if (stat == null) return;

        int lv = GetStatLevel(stat.statType);
        if (lv >= stat.MaxLevel) return;

        // Track StatData reference for HUD (only on first pick)
        if (lv == 0 && !equippedStats.Contains(stat))
            equippedStats.Add(stat);

        // ── ProjectileCount — ใช้ค่าที่ level ปัจจุบัน (ไม่ cumulative)
        float val;
        if (stat.statType == StatType.ProjectileCount)
        {
            // valuePerLevel เก็บ total count ณ level นั้น  เช่น [1,1,2,2,3]
            // หาค่า delta = current value − previous value
            float prev    = lv > 0 ? stat.GetValueAtLevel(lv - 1) : 0f;
            float current = stat.GetValueAtLevel(lv);
            val           = current - prev;   // เพิ่มจริงๆ ในรอบนี้
        }
        else
        {
            val = stat.GetValueAtLevel(lv);
        }

        statLevels[stat.statType] = lv + 1;
        if (!statTotals.ContainsKey(stat.statType)) statTotals[stat.statType] = 0f;
        statTotals[stat.statType] += val;

        // Side-effects ที่ต้องส่งผ่าน Network (NetworkVariable / ServerRpc)
        if (pm != null)
        {
            switch (stat.statType)
            {
                case StatType.MaxHealth:
                    pm.GainMaxHealth(val);
                    break;
                case StatType.HealthRegen:
                    pm.healthRegenPerSecond += val;
                    break;
                case StatType.HealOnFullBuild:
                    if (lv + 1 >= stat.MaxLevel)   // เฉพาะตอน max
                        pm.HealPercent(0.25f);
                    break;
            }
        }

        // Full-build gold (ถ้ามีระบบ Gold ในอนาคต)
        if (stat.statType == StatType.GainGold && lv + 1 >= stat.MaxLevel)
            Debug.Log("[StatManager] 💰 Full Build Bonus: +25 Gold (TODO: gold system)");

        Debug.Log($"[StatManager] ✅ {stat.statName} Lv{lv + 1}  (+{val})");
    }

    // ── Internal ──────────────────────────────────────────────────────────
    float GetTotal(StatType type)
        => statTotals.TryGetValue(type, out float v) ? v : 0f;
}
