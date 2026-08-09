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

    // ── Temporary stat bonuses (set by abilities / passives, cleared by them) ──
    /// <summary>Ability Haste bonus ชั่วคราว — บวกกับค่า permanent จาก stat cards</summary>
    [HideInInspector] public float tempAbilityHaste = 0f;

    /// <summary>Damage bonus ชั่วคราว (additive %) — set โดย SupportArenaWeapon / abilities</summary>
    [HideInInspector] public float tempDamageBonusMult = 0f;

    // ── Weapon Multipliers (WeaponBase อ่าน) ─────────────────────────────
    /// <summary>+10% damage per Lv — 1.0 = no bonus</summary>
    public float GetPowerMultiplier()
        => 1f + GetTotal(StatType.Damage) + tempDamageBonusMult;

    /// <summary>Ability Haste → cooldown multiplier (Haste / (100 + Haste))
    /// รวม tempAbilityHaste จาก Gunner passive หรือ abilities อื่น</summary>
    public float GetCooldownMultiplier()
    {
        float haste = GetTotal(StatType.AbilityHaste) + tempAbilityHaste;
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

    /// <summary>
    /// เพิ่มโบนัสถาวรเข้า total ตรงๆ **โดยไม่กิน stat slot และไม่ขึ้น stat level**
    ///
    /// ใช้โดยระบบที่ไม่ใช่ upgrade card:
    ///   • Talent ถาวรจาก Talent Shop  (PlayerTalentApplier)
    ///   • Augment                      (StatAugment / WeaponGrantAugment)
    ///
    /// เรียกได้ทั้งฝั่ง owner และ server — แต่ละฝั่งเก็บสำเนาของตัวเอง
    /// (side-effect ที่ต้องผ่าน network เช่น MaxHealth จะทำงานเฉพาะฝั่ง server)
    /// </summary>
    public void AddPermanentBonus(StatType type, float value, playermove pm = null)
    {
        if (Mathf.Approximately(value, 0f)) return;

        if (!statTotals.ContainsKey(type)) statTotals[type] = 0f;
        statTotals[type] += value;

        if (pm != null)
        {
            switch (type)
            {
                case StatType.MaxHealth:
                    pm.GainMaxHealth(value);        // server-only ข้างใน
                    break;
                case StatType.HealthRegen:
                    pm.healthRegenPerSecond += value;
                    break;
            }
        }
    }

    public void ApplyStat(StatData stat, playermove pm)
    {
        if (stat == null) return;

        ApplyStatLocal(stat, pm);

        if (IsOwner && !IsServer)
        {
            ApplyStatServerRpc(stat.statType);
        }
    }

    private void ApplyStatLocal(StatData stat, playermove pm)
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

        Debug.Log($"[StatManager] {(IsServer ? "[Server]" : "[Client]")} ✅ {stat.statName} Lv{lv + 1}  (value={val})");
    }

    [ServerRpc]
    private void ApplyStatServerRpc(StatType type)
    {
        var um = GetComponent<UpgradeManager>();
        if (um == null) return;

        StatData foundStat = null;
        foreach (var s in um.allStats)
        {
            if (s != null && s.statType == type)
            {
                foundStat = s;
                break;
            }
        }

        if (foundStat != null)
        {
            var pm = GetComponent<playermove>();
            ApplyStatLocal(foundStat, pm);
        }
        else
        {
            Debug.LogWarning($"[StatManager] Server could not find StatData for {type} in UpgradeManager.allStats");
        }
    }

    // ── Internal ──────────────────────────────────────────────────────────
    float GetTotal(StatType type)
        => statTotals.TryGetValue(type, out float v) ? v : 0f;
}
