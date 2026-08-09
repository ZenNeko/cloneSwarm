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
    private PlayerWeaponManager  weaponManager;
    private PlayerStatManager    statManager;
    private playermove           playerMove;
    private PlayerAugmentManager augmentManager;
    private CharacterData        myCharacter;   // ตัวละครของ player นี้

    // ── State ─────────────────────────────────────────────────────────────
    private List<UpgradeCardInfo> currentOptions = new();
    private bool                  hasPicked;
    // phase ไหนกำลังเปิดอยู่ — auto-pick ต้องแจ้งกลับคนละ ServerRpc
    private bool                  _isOrbPhase;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        weaponManager  = GetComponent<PlayerWeaponManager>();
        statManager    = GetComponent<PlayerStatManager>();
        playerMove     = GetComponent<playermove>();
        augmentManager = GetComponent<PlayerAugmentManager>();
        myCharacter    = GetComponent<PlayerWeaponManager>()?.characterData
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
        _isOrbPhase    = false;
        hasPicked      = false;

        // เลเวลที่กำหนดไว้ → ให้เลือก Augment แทน card ปกติ
        bool isAugmentLevel = SharedExperienceManager.Instance?.IsAugmentLevel(newLevel) ?? false;
        currentOptions = isAugmentLevel
            ? PickAugmentCards(cardsPerLevel)
            : PickCards(cardsPerLevel, isOrbReward: false);

        // ถ้า augment pool หมด (เลือกครบทุกใบแล้ว) → ตกกลับเป็น card ปกติ
        if (isAugmentLevel && currentOptions.Count == 0)
            currentOptions = PickCards(cardsPerLevel, isOrbReward: false);

        if (currentOptions.Count == 0) { NotifyLevelUpPicked(); return; }
        RecommendCards(currentOptions);
        LevelUpUI.Instance?.Show(currentOptions, ApplyCard, newLevel);
    }

    // ── Orb Reward — ทุกคนได้ 1 card จาก weapon/stat ที่ตัวเองมีอยู่แล้ว ──
    void OnOrbPhaseStart()
    {
        _isOrbPhase    = true;
        hasPicked      = false;
        currentOptions = PickCards(1, isOrbReward: true, ownedOnly: true);
        if (currentOptions.Count == 0) { NotifyOrbPicked(); return; }
        RecommendCards(currentOptions);
        LevelUpUI.Instance?.Show(currentOptions, ApplyOrbCard, 0);
    }

    void OnUpgradePhaseEnd() { LevelUpUI.Instance?.Hide(); hasPicked = false; }

    void OnForceAutoPick()
    {
        if (hasPicked) return;
        if (currentOptions.Count > 0)
        {
            if (_isOrbPhase) ApplyOrbCard(currentOptions[0]);
            else             ApplyCard(currentOptions[0]);
        }
        else
        {
            if (_isOrbPhase) NotifyOrbPicked();
            else             NotifyLevelUpPicked();
        }
    }

    // ── Synergy Card Recommendation ───────────────────────────────────────
    void RecommendCards(List<UpgradeCardInfo> options)
    {
        if (options == null || options.Count == 0) return;

        // Reset
        foreach (var opt in options) opt.isRecommended = false;

        var equippedWeapons = weaponManager.GetEquippedWeapons();
        float[] scores = new float[options.Count];

        for (int i = 0; i < options.Count; i++)
        {
            var card = options[i];
            float score = 0f;

            if (card.type == UpgradeCardType.WeaponSuper || card.type == UpgradeCardType.WeaponFusion)
            {
                score = 20f; // แนะนำทันที
            }
            else if (card.weapon != null)
            {
                if (card.type == UpgradeCardType.WeaponLevelUp)
                {
                    score = 10f; // แนะนำให้อัปอาวุธที่ถืออยู่ให้ตัน
                    
                    if (card.weapon.tier == WeaponTier.Normal && card.targetLevel == card.weapon.MaxLevel)
                    {
                        score += 5f; // ใกล้ขึ้น Super
                    }
                }
                else if (card.type == UpgradeCardType.WeaponNew)
                {
                    // เช็คคู่ฟิวชัน
                    foreach (var recipe in allRecipes)
                    {
                        if (recipe == null || recipe.fusionResult == null) continue;
                        
                        bool isPartA = card.weapon.superVersion != null && card.weapon.superVersion == recipe.superWeaponA;
                        bool isPartB = card.weapon.superVersion != null && card.weapon.superVersion == recipe.superWeaponB;

                        if (isPartA || isPartB)
                        {
                            var partnerSuper = isPartA ? recipe.superWeaponB : recipe.superWeaponA;
                            if (partnerSuper != null)
                            {
                                WeaponData partnerNormal = null;
                                foreach (var wAll in allWeapons)
                                {
                                    if (wAll != null && wAll.superVersion == partnerSuper)
                                    {
                                        partnerNormal = wAll;
                                        break;
                                    }
                                }

                                bool hasPartner = false;
                                foreach (var owned in equippedWeapons)
                                {
                                    if (owned == null) continue;
                                    if (owned == partnerSuper || owned == partnerNormal)
                                    {
                                        hasPartner = true;
                                        break;
                                    }
                                }

                                if (hasPartner)
                                {
                                    score += 15f; // แนะนำอย่างยิ่ง
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (card.type == UpgradeCardType.Stat && card.stat != null)
            {
                // ตรวจสอบเงื่อนไข Super
                foreach (var w in equippedWeapons)
                {
                    if (w == null || w.tier != WeaponTier.Normal || w.superVersion == null) continue;
                    if (weaponManager.HasWeapon(w.superVersion)) continue; 

                    if (w.superConditions != null)
                    {
                        foreach (var cond in w.superConditions)
                        {
                            if (cond.conditionType == SuperConditionType.StatAtLevel &&
                                cond.requiredStatType == card.stat.statType)
                            {
                                int currentLv = statManager.GetStatLevel(cond.requiredStatType);
                                if (currentLv < cond.requiredLevel)
                                {
                                    score += 12f; // แนะนำเพื่อปลดล็อค Super
                                }
                            }
                        }
                    }
                }

                // แนะนำ Core Stat ตามประเภทตัวละคร (Synergy) ตามที่ GDD กำหนด
                if (myCharacter != null)
                {
                    string charName = myCharacter.characterName.ToLower();
                    if (charName.Contains("hunter") || charName.Contains("gunner"))
                    {
                        if (card.stat.statType == StatType.AbilityHaste || 
                            card.stat.statType == StatType.ProjectileCount || 
                            card.stat.statType == StatType.Damage)
                        {
                            score += 6f; // แนะนำความเร่ง/จำนวนกระสุน/พลังโจมตีสำหรับสายยิง
                        }
                    }
                    else if (charName.Contains("riven") || charName.Contains("melee") || charName.Contains("warrior"))
                    {
                        if (card.stat.statType == StatType.MoveSpeed || 
                            card.stat.statType == StatType.AreaSize || 
                            card.stat.statType == StatType.Armor)
                        {
                            score += 6f; // แนะนำความเร็ว/ระยะฟัน/เกราะสำหรับสายฟันประชิด
                        }
                    }
                }

                if (score < 0.1f)
                {
                    score = 2f; 
                }
            }

            scores[i] = score;
        }

        // หาคะแนนสูงสุด
        float maxScore = 0f;
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] > maxScore) maxScore = scores[i];
        }

        // ปักป้ายการ์ดแนะนำ (คะแนนสูงสุดและผ่านเกณฑ์ขั้นต่ำ)
        if (maxScore > 0.1f)
        {
            for (int i = 0; i < scores.Length; i++)
            {
                if (Mathf.Abs(scores[i] - maxScore) < 0.01f)
                {
                    options[i].isRecommended = true;
                }
            }
        }
    }

    // ── Synergy Icon Resolution Helper ────────────────────────────────────
    public Sprite GetStatIcon(StatType type)
    {
        if (allStats != null)
        {
            foreach (var s in allStats)
            {
                if (s != null && s.statType == type) return s.icon;
            }
        }
        return null;
    }

    private void PopulateSynergyInfo(UpgradeCardInfo card)
    {
        if (card == null) return;
        card.synergyIcons.Clear();
        card.showSynergy = false;

        if (card.weapon != null)
        {
            // Weapon Card: Show the stat(s) required to evolve this normal weapon to Super
            if (card.weapon.tier == WeaponTier.Normal && card.weapon.superVersion != null)
            {
                bool hasWeapon = weaponManager != null && weaponManager.HasWeapon(card.weapon);
                bool hasStat = false;

                if (card.weapon.superConditions != null && statManager != null)
                {
                    foreach (var cond in card.weapon.superConditions)
                    {
                        if (cond.conditionType == SuperConditionType.StatAtLevel)
                        {
                            if (statManager.GetStatLevel(cond.requiredStatType) > 0)
                            {
                                hasStat = true;
                                break;
                            }
                        }
                    }
                }

                // Show synergy badge only if player owns the weapon OR already owns the synergistic stat
                if (hasWeapon || hasStat)
                {
                    if (card.weapon.superConditions != null)
                    {
                        foreach (var cond in card.weapon.superConditions)
                        {
                            if (cond.conditionType == SuperConditionType.StatAtLevel)
                            {
                                Sprite icon = GetStatIcon(cond.requiredStatType);
                                if (icon != null && !card.synergyIcons.Contains(icon))
                                {
                                    card.synergyIcons.Add(icon);
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (card.type == UpgradeCardType.Stat && card.stat != null)
        {
            // Stat Card: Show the weapon(s) in player's inventory that evolve with this stat
            if (weaponManager != null)
            {
                var equipped = weaponManager.GetEquippedWeapons();
                if (equipped != null)
                {
                    foreach (var w in equipped)
                    {
                        if (w == null || w.tier != WeaponTier.Normal || w.superVersion == null) continue;
                        if (weaponManager.HasWeapon(w.superVersion) || HasProgressedPast(w)) continue;

                        if (w.superConditions != null)
                        {
                            foreach (var cond in w.superConditions)
                            {
                                if (cond.conditionType == SuperConditionType.StatAtLevel &&
                                    cond.requiredStatType == card.stat.statType)
                                {
                                    if (w.icon != null && !card.synergyIcons.Contains(w.icon))
                                    {
                                        card.synergyIcons.Add(w.icon);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Limit to 3 icons maximum
        if (card.synergyIcons.Count > 3)
        {
            card.synergyIcons.RemoveRange(3, card.synergyIcons.Count - 3);
        }

        card.showSynergy = card.synergyIcons.Count > 0;
    }

    // ── Card Pool ─────────────────────────────────────────────────────────
    List<UpgradeCardInfo> PickCards(int count, bool isOrbReward, bool ownedOnly = false)
    {
        var pool = isOrbReward ? BuildOrbPool(ownedOnly) :
                   ownedOnly   ? BuildOwnedPool()        : BuildLevelUpPool();
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

        // Resolve evolution synergy info for each card in the picked result
        foreach (var card in result)
        {
            PopulateSynergyInfo(card);
        }

        return result;
    }

    // ── Augment Pool ──────────────────────────────────────────────────────
    /// <summary>
    /// สุ่ม Augment แบบถ่วงน้ำหนัก — ตัดใบที่ถือครบ maxStacks แล้ว
    /// และตัดใบที่เป็น exclusive ของตัวละครอื่น
    /// </summary>
    List<UpgradeCardInfo> PickAugmentCards(int count)
    {
        var result = new List<UpgradeCardInfo>();

        var db = CloneSwarm.Meta.MetaDatabase.Instance;
        if (db == null || db.augments == null || augmentManager == null) return result;

        var pool = new List<AugmentData>();
        foreach (var a in db.augments)
        {
            if (a == null) continue;
            if (a.exclusiveCharacter != null && a.exclusiveCharacter != myCharacter) continue;
            if (augmentManager.GetStackCount(a) >= a.maxStacks) continue;
            pool.Add(a);
        }

        var used = new HashSet<int>();
        for (int n = 0; n < count && used.Count < pool.Count; n++)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (!used.Contains(i)) total += Mathf.Max(0.01f, pool[i].weight);

            if (total <= 0f) break;

            float roll = Random.Range(0f, total);
            float acc  = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (used.Contains(i)) continue;
                acc += Mathf.Max(0.01f, pool[i].weight);
                if (roll <= acc)
                {
                    result.Add(new UpgradeCardInfo
                    {
                        type    = UpgradeCardType.Augment,
                        augment = pool[i]
                    });
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
                // กันออก card "WeaponNew" ของ Normal ที่ผู้เล่น upgrade ผ่านไปแล้ว
                // (มี Super หรือ Fusion ของ Normal นั้นอยู่ในมือ)
                if (HasProgressedPast(w)) continue;

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
    List<UpgradeCardInfo> BuildOrbPool(bool ownedOnly = false)
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

        // 3. Fallback — ถ้าไม่มี Super/Fusion → ใช้ของที่มีอยู่ (BuildOwnedPool) หรือ Level Up pool
        if (pool.Count == 0)
            return ownedOnly ? BuildOwnedPool() : BuildLevelUpPool();

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
            case UpgradeCardType.Augment:
                augmentManager?.Acquire(card.augment);
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

    // ── Upgrade-chain Check ───────────────────────────────────────────────
    /// <summary>
    /// true = ผู้เล่น upgrade ผ่าน Normal `w` ไปแล้ว
    /// (มี Super ของ w อยู่ในมือ หรือ มี Fusion ที่ใช้ Super ของ w เป็น input)
    ///
    /// ใช้กัน "WeaponNew" card ของ Normal ที่ถูก replace ไปแล้ว
    /// เช่น LightningChain → Stormcaller (Super) → ThunderRail (Fusion)
    /// ทุกขั้น Normal LightningChain ไม่ควรขึ้น card ใหม่อีก
    /// </summary>
    bool HasProgressedPast(WeaponData normalWeapon)
    {
        if (normalWeapon == null) return false;
        var super = normalWeapon.superVersion;
        if (super == null) return false;

        // มี Super ของ Normal นั้นอยู่
        if (weaponManager.HasWeapon(super)) return true;

        // มี Fusion ที่ใช้ Super นี้เป็น input อยู่ (Super หายไป → Fusion แทน)
        if (allRecipes != null)
        {
            foreach (var recipe in allRecipes)
            {
                if (recipe == null || recipe.fusionResult == null) continue;
                if (recipe.superWeaponA != super && recipe.superWeaponB != super) continue;
                if (weaponManager.HasWeapon(recipe.fusionResult)) return true;
            }
        }

        return false;
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
