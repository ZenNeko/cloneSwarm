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

        SharedExperienceManager.OnUpgradePhaseStart += OnLevelUpPhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   += OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     += OnForceAutoPick;
        SharedExperienceManager.OnOrbPhaseStart     += OnOrbPhaseStart;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        SharedExperienceManager.OnUpgradePhaseStart -= OnLevelUpPhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   -= OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     -= OnForceAutoPick;
        SharedExperienceManager.OnOrbPhaseStart     -= OnOrbPhaseStart;
    }

    // ── Level Up (3 cards) ────────────────────────────────────────────────
    void OnLevelUpPhaseStart(int newLevel)
    {
        hasPicked      = false;
        currentOptions = PickCards(cardsPerLevel, isOrbReward: false);
        if (currentOptions.Count == 0) { NotifyLevelUpPicked(); return; }
        LevelUpUI.Instance?.Show(currentOptions, ApplyCard, newLevel);
    }

    // ── Orb Reward (1 card) ───────────────────────────────────────────────
    void OnOrbPhaseStart()
    {
        hasPicked      = false;
        currentOptions = PickCards(1, isOrbReward: true);
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
    List<UpgradeCardInfo> PickCards(int count, bool isOrbReward)
    {
        var pool = isOrbReward ? BuildOrbPool() : BuildLevelUpPool();
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
