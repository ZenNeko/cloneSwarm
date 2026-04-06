using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-player upgrade manager — อยู่บน Player Prefab, ทำงานเฉพาะ Owner
///
/// Level Up   → สุ่ม 3 cards (weighted random) จาก WeaponData + StatData pool
/// Orb Reward → สุ่ม 1 card  (Super / Fusion / Weapon/Stat)
/// </summary>
public class UpgradeManager : NetworkBehaviour
{
    [Header("Pool")]
    [Tooltip("ลาก WeaponData ทั้งหมดมาใส่ที่นี่")]
    public List<WeaponData>          allWeapons = new();
    [Tooltip("ลาก StatData ทั้งหมดมาใส่ที่นี่")]
    public List<StatData>            allStats   = new();
    [Tooltip("ลาก WeaponFusionRecipe ทั้งหมดมาใส่ที่นี่")]
    public List<WeaponFusionRecipe>  allRecipes = new();
    public int cardsPerLevel = 3;

    // ── References ────────────────────────────────────────────────────────
    private PlayerWeaponManager weaponManager;
    private PlayerStatManager   statManager;
    private playermove           playerMove;
    private CharacterData        myCharacter;   // ตัวละครของ player นี้

    // ── State ─────────────────────────────────────────────────────────────
    private List<UpgradeCardInfo> currentOptions = new();
    private bool                  hasPicked;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        weaponManager = GetComponent<PlayerWeaponManager>();
        statManager   = GetComponent<PlayerStatManager>();
        playerMove    = GetComponent<playermove>();
        myCharacter   = GetComponent<PlayerWeaponManager>()?.characterData
                        ?? CharacterSelectUI.SelectedCharacter;

        SharedExperienceManager.OnUpgradePhaseStart += OnLevelUpPhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   += OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     += OnForceAutoPick;
        SharedExperienceManager.OnOrbPhaseStart     += OnOrbPhaseStart;   // Action<ulong>
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        SharedExperienceManager.OnUpgradePhaseStart -= OnLevelUpPhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   -= OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     -= OnForceAutoPick;
        SharedExperienceManager.OnOrbPhaseStart     -= OnOrbPhaseStart;   // Action<ulong>
    }

    // ── Level Up (3 cards) ────────────────────────────────────────────────
    void OnLevelUpPhaseStart(int newLevel)
    {
        hasPicked      = false;
        currentOptions = PickCards(cardsPerLevel, isOrbReward: false);
        if (currentOptions.Count == 0) { NotifyLevelUpPicked(); return; }
        LevelUpUI.Instance?.Show(currentOptions, ApplyCard, newLevel);
    }

    // ── Orb Reward — ทุกคนได้ 1 card จาก weapon/stat ที่ตัวเองมีอยู่แล้ว ──
    void OnOrbPhaseStart()
    {
        hasPicked      = false;
        currentOptions = PickCards(1, isOrbReward: false, ownedOnly: true);
        if (currentOptions.Count == 0) { NotifyOrbPicked(); return; }
        LevelUpUI.Instance?.Show(currentOptions, ApplyOrbCard, 0);
    }

    void OnUpgradePhaseEnd() { LevelUpUI.Instance?.Hide(); hasPicked = false; }

    void OnForceAutoPick()
    {
        if (hasPicked) return;
        if (currentOptions.Count > 0) ApplyCard(currentOptions[0]);
        else NotifyLevelUpPicked();
    }

    // ── Card Pool ─────────────────────────────────────────────────────────
    List<UpgradeCardInfo> PickCards(int count, bool isOrbReward, bool ownedOnly = false)
    {
        var pool = ownedOnly   ? BuildOwnedPool() :
                   isOrbReward ? BuildOrbPool()   : BuildLevelUpPool();
        if (pool.Count == 0) return new List<UpgradeCardInfo>();

        var result = new List<UpgradeCardInfo>();
        var used   = new HashSet<int>();

        for (int n = 0; n < count && used.Count < pool.Count; n++)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (!used.Contains(i)) total += pool[i].Weight;

            if (total <= 0f) break;

            float roll = Random.Range(0f, total);
            float acc  = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (used.Contains(i)) continue;
                acc += pool[i].Weight;
                if (roll <= acc)
                {
                    result.Add(pool[i]);
                    used.Add(i);
                    break;
                }
            }
        }
        return result;
    }

    // ── Level Up Pool — Weapon + Stat cards ───────────────────────────────
    List<UpgradeCardInfo> BuildLevelUpPool()
    {
        var pool = new List<UpgradeCardInfo>();

        foreach (var w in allWeapons)
        {
            if (w == null || w.tier != WeaponTier.Normal) continue;
            if (!IsWeaponAvailable(w)) continue;   // exclusive ของตัวละครอื่น → ข้าม

            if (!weaponManager.HasWeapon(w))
            {
                if (weaponManager.HasFreeSlot())
                    pool.Add(new UpgradeCardInfo
                    {
                        type        = UpgradeCardType.WeaponNew,
                        weapon      = w,
                        targetLevel = 1
                    });
            }
            else
            {
                int lv = weaponManager.GetWeaponLevel(w); // 0-indexed
                if (lv + 1 < w.MaxLevel)
                    pool.Add(new UpgradeCardInfo
                    {
                        type        = UpgradeCardType.WeaponLevelUp,
                        weapon      = w,
                        targetLevel = lv + 2  // 1-indexed display
                    });
            }
        }

        foreach (var s in allStats)
        {
            if (s == null) continue;
            int lv = statManager.GetStatLevel(s.statType);
            if (lv >= s.MaxLevel) continue;
            if (lv == 0 && !statManager.HasStatSlotAvailable()) continue;

            pool.Add(new UpgradeCardInfo
            {
                type             = UpgradeCardType.Stat,
                stat             = s,
                currentStatLevel = lv
            });
        }

        return pool;
    }

    // ── Orb Pool — Super / Fusion / fallback Weapon/Stat ─────────────────
    List<UpgradeCardInfo> BuildOrbPool()
    {
        var pool = new List<UpgradeCardInfo>();

        // 1. Super Upgrades — Normal Lv5 + conditions ครบ ยังไม่ได้ Super
        foreach (var w in weaponManager.GetEquippedWeapons())
        {
            if (w == null || w.tier != WeaponTier.Normal) continue;
            if (!IsWeaponAvailable(w)) continue;
            if (w.superVersion == null) continue;
            int lv = weaponManager.GetWeaponLevel(w);
            if (lv < w.MaxLevel - 1) continue;                       // ยังไม่ถึง Lv5
            if (weaponManager.HasWeapon(w.superVersion)) continue;    // มี super แล้ว
            if (!CheckSuperConditions(w)) continue;                   // conditions ไม่ครบ

            pool.Add(new UpgradeCardInfo
            {
                type   = UpgradeCardType.WeaponSuper,
                weapon = w
            });
        }

        // 2. Fusion — Super คู่ที่ตรง recipe
        var equipped = weaponManager.GetEquippedWeapons();
        foreach (var recipe in allRecipes)
        {
            if (recipe == null || recipe.fusionResult == null) continue;
            if (weaponManager.HasWeapon(recipe.fusionResult)) continue;  // มี fusion แล้ว

            bool hasA = equipped.Contains(recipe.superWeaponA);
            bool hasB = equipped.Contains(recipe.superWeaponB);
            if (!hasA || !hasB) continue;

            pool.Add(new UpgradeCardInfo
            {
                type        = UpgradeCardType.WeaponFusion,
                weapon      = recipe.fusionResult,
                fusionRecipe = recipe
            });
        }

        // 3. Fallback — ถ้าไม่มี Super/Fusion → ใช้ pool เดียวกับ Level Up
        if (pool.Count == 0)
            return BuildLevelUpPool();

        return pool;
    }

    // ── Owned Pool — weapon/stat ที่ผู้เล่นมีอยู่แล้ว + Super/Fusion ที่ทำได้ (ไม่มี WeaponNew) ──
    List<UpgradeCardInfo> BuildOwnedPool()
    {
        var pool     = new List<UpgradeCardInfo>();
        var equipped = weaponManager.GetEquippedWeapons();

        // WeaponLevelUp — weapon ที่มีอยู่ + ยังไม่ max level
        foreach (var w in equipped)
        {
            if (w == null) continue;
            if (!IsWeaponAvailable(w)) continue;
            int lv = weaponManager.GetWeaponLevel(w);
            if (lv + 1 >= w.MaxLevel) continue;
            pool.Add(new UpgradeCardInfo
            {
                type        = UpgradeCardType.WeaponLevelUp,
                weapon      = w,
                targetLevel = lv + 2
            });
        }

        // WeaponSuper — Normal Lv5 + conditions ครบ + ยังไม่มี super
        foreach (var w in equipped)
        {
            if (w == null || w.tier != WeaponTier.Normal) continue;
            if (w.superVersion == null) continue;
            if (weaponManager.GetWeaponLevel(w) < w.MaxLevel - 1) continue;
            if (weaponManager.HasWeapon(w.superVersion)) continue;
            if (!CheckSuperConditions(w)) continue;
            pool.Add(new UpgradeCardInfo
            {
                type   = UpgradeCardType.WeaponSuper,
                weapon = w
            });
        }

        // WeaponFusion — Super คู่ที่ตรง recipe + ยังไม่มี fusion
        foreach (var recipe in allRecipes)
        {
            if (recipe == null || recipe.fusionResult == null) continue;
            if (weaponManager.HasWeapon(recipe.fusionResult)) continue;
            if (!equipped.Contains(recipe.superWeaponA)) continue;
            if (!equipped.Contains(recipe.superWeaponB)) continue;
            pool.Add(new UpgradeCardInfo
            {
                type         = UpgradeCardType.WeaponFusion,
                weapon       = recipe.fusionResult,
                fusionRecipe = recipe
            });
        }

        // Stat — stat ที่มีอยู่แล้ว (lv > 0) + ยังไม่ max
        foreach (var s in allStats)
        {
            if (s == null) continue;
            int lv = statManager.GetStatLevel(s.statType);
            if (lv <= 0) continue;
            if (lv >= s.MaxLevel) continue;
            pool.Add(new UpgradeCardInfo
            {
                type             = UpgradeCardType.Stat,
                stat             = s,
                currentStatLevel = lv
            });
        }

        // fallback — ถ้าไม่มีอะไรใน owned pool → ใช้ level-up pool ปกติ
        if (pool.Count == 0)
            pool = BuildLevelUpPool();

        return pool;
    }

    // ── Apply ─────────────────────────────────────────────────────────────
    public void ApplyCard(UpgradeCardInfo card)
    {
        if (card == null || hasPicked) return;
        hasPicked = true;
        LevelUpUI.Instance?.HideCards();
        ApplyCardInternal(card);
        NotifyLevelUpPicked();
    }

    void ApplyOrbCard(UpgradeCardInfo card)
    {
        if (card == null || hasPicked) return;
        hasPicked = true;
        LevelUpUI.Instance?.HideCards();
        ApplyCardInternal(card);
        NotifyOrbPicked();
    }

    void ApplyCardInternal(UpgradeCardInfo card)
    {
        switch (card.type)
        {
            case UpgradeCardType.WeaponNew:
                weaponManager.AddWeapon(card.weapon);
                break;
            case UpgradeCardType.WeaponLevelUp:
                weaponManager.UpgradeWeapon(card.weapon);
                break;
            case UpgradeCardType.WeaponSuper:
                weaponManager.ReplaceWeapon(card.weapon, card.weapon.superVersion);
                break;
            case UpgradeCardType.WeaponFusion:
                if (card.fusionRecipe != null)
                    weaponManager.FuseWeapons(
                        card.fusionRecipe.superWeaponA,
                        card.fusionRecipe.superWeaponB,
                        card.fusionRecipe.fusionResult);
                break;
            case UpgradeCardType.Stat:
                statManager.ApplyStat(card.stat, playerMove);
                break;
        }
    }

    // ── Character Exclusive Check ─────────────────────────────────────────
    /// <summary>
    /// true = weapon นี้ใช้ได้กับตัวละครของ player นี้
    /// false = เป็น exclusive ของตัวละครอื่น → ไม่ขึ้นใน pool
    /// </summary>
    bool IsWeaponAvailable(WeaponData w)
    {
        if (w == null) return false;
        if (w.exclusiveCharacter == null) return true;          // ไม่ exclusive → ทุกคนได้
        return w.exclusiveCharacter == myCharacter;             // ตรงกับตัวละครของตัวเอง
    }

    // ── Super Condition Check ─────────────────────────────────────────────
    bool CheckSuperConditions(WeaponData w)
    {
        if (w.superConditions == null || w.superConditions.Length == 0) return true;

        foreach (var c in w.superConditions)
        {
            bool pass = c.conditionType switch
            {
                SuperConditionType.StatAtLevel =>
                    statManager.GetStatLevel(c.requiredStatType) >= c.requiredLevel,

                SuperConditionType.WeaponAtLevel =>
                    c.requiredWeapon != null &&
                    weaponManager.GetWeaponLevel(c.requiredWeapon) + 1 >= c.requiredLevel,

                SuperConditionType.PlayerLevel =>
                    (SharedExperienceManager.Instance?.GetCurrentLevel() ?? 0) >= c.requiredLevel,

                _ => true
            };

            if (!pass) return false;
        }
        return true;
    }

    // ── Notify ────────────────────────────────────────────────────────────
    void NotifyLevelUpPicked() =>
        SharedExperienceManager.Instance?.PlayerUpgradePickedServerRpc();

    void NotifyOrbPicked() =>
        SharedExperienceManager.Instance?.PlayerOrbPickedServerRpc();
}
