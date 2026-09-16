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
    [Tooltip("จำนวนการ์ด augment ต่อครั้ง — ใช้ทั้งตอนเลเวลที่กำหนดและตอนเก็บ orb\n\n" +
             "แยกจาก cardsPerLevel เพราะ augment เป็นของที่ได้นานๆ ครั้งและเปลี่ยนสไตล์การเล่น\n" +
             "จำนวนตัวเลือกจึงเป็นเรื่องบาลานซ์คนละเรื่องกับการ์ดอัปปกติ\n\n" +
             "ได้ไม่เกินจำนวนใบที่ยังเหลือใน pool — ตั้ง 5 แต่เหลือ 2 ใบก็ได้ 2")]
    [Min(1)]
    public int augmentCardCount = 3;

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
        SharedExperienceManager.OnOrbPhaseStart     += OnOrbPhaseStart;   // Action<OrbReward>
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        SharedExperienceManager.OnUpgradePhaseStart -= OnLevelUpPhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   -= OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     -= OnForceAutoPick;
        SharedExperienceManager.OnOrbPhaseStart     -= OnOrbPhaseStart;   // Action<OrbReward>
    }

    // ── Level Up (3 cards) ────────────────────────────────────────────────
    void OnLevelUpPhaseStart(int newLevel)
    {
        _isOrbPhase    = false;
        hasPicked      = false;

        // เลเวลที่กำหนดไว้ → ให้เลือก Augment แทน card ปกติ
        bool isAugmentLevel = SharedExperienceManager.Instance?.IsAugmentLevel(newLevel) ?? false;
        currentOptions = isAugmentLevel
            ? PickAugmentCards(augmentCardCount)
            : PickCards(cardsPerLevel, isOrbReward: false);

        // ถ้า augment pool หมด (เลือกครบทุกใบแล้ว) → ตกกลับเป็น card ปกติ
        //
        // **ต้องส่งเสียง** — การถอยกลับแบบเงียบทำให้ระบบ augment ทั้งก้อนไม่ทำงาน
        // อยู่หลายเดือนโดยไม่มีใครรู้ (MetaDatabase.augments ว่างเปล่า) ผู้เล่นเห็น
        // การ์ดปกติแล้วไม่รู้ว่าพลาดอะไร คนทำเกมก็ไม่เห็นอะไรผิดเพราะไม่มีอะไรผิด
        //
        // "เลือกครบทุกใบแล้ว" กับ "ไม่มีใบให้เลือกตั้งแต่แรก" หน้าตาเหมือนกันตรงนี้
        // แต่คนละเรื่องกันโดยสิ้นเชิง — แยกให้เห็นในข้อความ
        if (isAugmentLevel && currentOptions.Count == 0)
        {
            int pool = CloneSwarm.Meta.MetaDatabase.Instance?.augments?.Count ?? 0;
            Debug.LogWarning(
                $"[Upgrade] เลเวล {newLevel} ตั้งไว้ว่าแจก augment แต่ไม่มีใบให้เลือก → ถอยไปใช้การ์ดปกติ · " +
                (pool == 0
                    ? "MetaDatabase.augments ว่างเปล่า — รัน Tools > Clone Swarm > Meta > Create Sample Augments"
                    : $"ถือครบ maxStacks ทุกใบใน pool แล้ว ({pool} ใบ)"));

            currentOptions = PickCards(cardsPerLevel, isOrbReward: false);
        }

        if (currentOptions.Count == 0) { NotifyLevelUpPicked(); return; }
        LevelUpUI.Instance?.Show(currentOptions, ApplyCard, newLevel);
    }

    // ── Orb Reward — ทุกคนได้การ์ดเมื่อมีคนเก็บ orb ───────────────────────
    //
    // orb ปกติให้ใบเดียว (อัปของที่ถืออยู่ — ไม่ต้องคิดมาก)
    // orb augment ให้ `augmentCardCount` ใบ เพราะ augment เปลี่ยนสไตล์การเล่น
    // ให้ตัวเลือกเดียวคือบังคับ ไม่ใช่ให้เลือก
    //
    // กองที่สุ่มขึ้นกับชนิดของ orb · ที่เหลือเหมือนกันทุกอย่าง — จอเดียวกัน
    // callback เดียวกัน (`ApplyOrbCard`) การนับคนเลือกครบเหมือนกัน
    void OnOrbPhaseStart(OrbReward reward)
    {
        _isOrbPhase    = true;
        hasPicked      = false;

        currentOptions = reward == OrbReward.Augment
                       ? PickAugmentCards(augmentCardCount)
                       : PickCards(1, isOrbReward: true, ownedOnly: true);

        // orb ที่ให้ augment แต่ไม่มี augment ให้หยิบ — ถอยไปใช้กองปกติ **พร้อมบอก**
        // เหตุผลเดียวกับตอนเลเวลอัป: การถอยเงียบๆ คือสิ่งที่ทำให้ระบบตายโดยไม่มีใครรู้
        if (reward == OrbReward.Augment && currentOptions.Count == 0)
        {
            int pool = CloneSwarm.Meta.MetaDatabase.Instance?.augments?.Count ?? 0;
            Debug.LogWarning(
                "[Orb] orb ใบนี้ตั้งไว้ว่าให้ augment แต่ไม่มีใบให้เลือก → ถอยไปใช้การ์ดปกติ · " +
                (pool == 0
                    ? "MetaDatabase.augments ว่างเปล่า — รัน Tools > Clone Swarm > Meta > Create Sample Augments"
                    : $"ถือครบ maxStacks ทุกใบใน pool แล้ว ({pool} ใบ)"));

            currentOptions = PickCards(1, isOrbReward: true, ownedOnly: true);
        }

        if (currentOptions.Count == 0) { NotifyOrbPicked(); return; }
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

    // ══════════════════════════════════════════════════════════════════════
    // EVOLUTION SYNERGY — ช่องทางเดียวที่เกมใช้บอกทางผู้เล่น
    //
    // ระบบ "แนะนำ" ถูกถอดออกตาม ADR-009 เพราะชักจูงเกินไป — เกมไม่ควรบอกว่า
    // ควรกดใบไหน · สิ่งที่บอกได้คือ **อะไรเชื่อมกับอะไร และห่างอีกเท่าไร**
    // แล้วปล่อยให้ผู้เล่นตัดสินเอง
    //
    // เส้นแบ่งที่ต้องรักษา: ทุกข้อความในนี้เป็น **สภาพปัจจุบัน** ห้ามมีคำว่า
    // ควร/แนะนำ/คุ้ม/ดีที่สุด · "Lv5 · ขาด Armor อีก 2" คือข้อเท็จจริง
    // "ควรเอา Armor" คือคำแนะนำ — อันหลังคือสิ่งที่เพิ่งถอดทิ้งไป
    // ══════════════════════════════════════════════════════════════════════
    private void PopulateSynergyInfo(UpgradeCardInfo card)
    {
        if (card == null) return;
        card.synergyLines.Clear();
        card.showSynergy = false;

        if (card.weapon != null && card.weapon.tier == WeaponTier.Normal
                                && card.weapon.superVersion != null)
        {
            AddWeaponCardLines(card);
        }
        else if (card.type == UpgradeCardType.Stat && card.stat != null)
        {
            AddStatCardLines(card);
        }

        // ใกล้ครบที่สุดขึ้นก่อน — บรรทัดแรกคือบรรทัดที่มีโอกาสถูกอ่านจริงที่สุด
        card.synergyLines.Sort((a, b) => a.met == b.met ? 0 : (a.met ? -1 : 1));
        card.showSynergy = card.synergyLines.Count > 0;
    }

    /// <summary>
    /// การ์ดอาวุธ → บอกว่า **สเตตัสอะไรที่อาวุธใบนี้รออยู่** และตอนนี้มีเท่าไร
    ///
    /// โชว์ก็ต่อเมื่อผู้เล่นมีของอย่างน้อยครึ่งหนึ่งของสมการแล้ว (ถืออาวุธ หรือมีสเตตัสนั้นบ้าง)
    /// ไม่งั้นจะกลายเป็นการสอนของที่ยังไกลเกินไปจนกลายเป็นเสียงรบกวน
    /// </summary>
    private void AddWeaponCardLines(UpgradeCardInfo card)
    {
        var conds = card.weapon.superConditions;
        if (conds == null || conds.Length == 0) return;

        bool hasWeapon = weaponManager != null && weaponManager.HasWeapon(card.weapon);
        bool hasAnyStat = false;
        if (statManager != null)
            foreach (var c in conds)
                if (c.conditionType == SuperConditionType.StatAtLevel &&
                    statManager.GetStatLevel(c.requiredStatType) > 0)
                { hasAnyStat = true; break; }

        if (!hasWeapon && !hasAnyStat) return;

        foreach (var c in conds)
        {
            var line = DescribeCondition(c);
            if (line.icon != null || !string.IsNullOrEmpty(line.label))
                card.synergyLines.Add(line);
        }
    }

    /// <summary>
    /// การ์ดสเตตัส → บอกว่า **อาวุธที่ถืออยู่ตัวไหนรอสเตตัสใบนี้** และห่างอีกเท่าไร
    ///
    /// ตรงนี้คือจุดที่มีค่าที่สุดของทั้งระบบ — ผู้เล่นมองการ์ด "Armor" แล้วไม่มีทางรู้เลย
    /// ว่ามันไปต่อกับ Laser ที่ถืออยู่ ถ้าไม่มีบรรทัดนี้
    /// </summary>
    private void AddStatCardLines(UpgradeCardInfo card)
    {
        if (weaponManager == null || statManager == null) return;

        int have = statManager.GetStatLevel(card.stat.statType);

        foreach (var w in weaponManager.GetEquippedWeapons())
        {
            if (w == null || w.tier != WeaponTier.Normal || w.superVersion == null) continue;
            if (HasProgressedPast(w)) continue;
            if (w.superConditions == null) continue;

            foreach (var c in w.superConditions)
            {
                if (c.conditionType != SuperConditionType.StatAtLevel) continue;
                if (c.requiredStatType != card.stat.statType) continue;

                // เลเวลอาวุธนับ 0 ทั้งโค้ดเบส — ที่ตาเห็นต้อง +1 เสมอ
                int wLv    = weaponManager.GetWeaponLevel(w) + 1;
                int wMax   = w.MaxLevel;
                int missing = Mathf.Max(0, c.requiredLevel - have);

                string detail = wLv < wMax
                    ? $"Lv{wLv}/{wMax} · ต้อง {card.stat.statName} Lv{c.requiredLevel}"
                    : missing > 0
                        ? $"Lv{wMax} · ขาด {card.stat.statName} อีก {missing}"
                        : $"Lv{wMax} · พร้อมวิวัฒน์";

                card.synergyLines.Add(new SynergyLine
                {
                    icon   = w.icon,
                    label  = w.DisplayName,
                    detail = detail,
                    met    = wLv >= wMax && missing == 0,
                });
                break;   // อาวุธหนึ่งตัวขึ้นบรรทัดเดียวพอ
            }
        }
    }

    /// <summary>
    /// แปลงเงื่อนไข Super หนึ่งข้อเป็นบรรทัดที่อ่านออก
    ///
    /// รองรับ **ครบทั้งสามชนิด** ไม่ใช่แค่ `StatAtLevel` เหมือนโค้ดเดิม —
    /// ตอนนี้คอนเทนต์ยังใช้แต่ `StatAtLevel` แต่ `CheckSuperConditions` รองรับครบ
    /// อยู่แล้ว การที่ป้ายรองรับไม่ครบแปลว่าวันที่ดีไซเนอร์ใส่ชนิดใหม่ใบแรก
    /// ป้ายจะเงียบไปเฉยๆ โดยไม่มีอะไรฟ้อง
    /// </summary>
    private SynergyLine DescribeCondition(SuperCondition c)
    {
        switch (c.conditionType)
        {
            case SuperConditionType.StatAtLevel:
            {
                int have = statManager != null ? statManager.GetStatLevel(c.requiredStatType) : 0;
                var sd   = FindStat(c.requiredStatType);
                string nm = sd != null ? sd.statName : c.requiredStatType.ToString();
                return new SynergyLine
                {
                    icon   = sd != null ? sd.Icon : StatIconSet.For(c.requiredStatType),
                    label  = nm,
                    detail = have >= c.requiredLevel
                           ? $"Lv{have}/{c.requiredLevel} · ครบแล้ว"
                           : $"Lv{have}/{c.requiredLevel}",
                    met    = have >= c.requiredLevel,
                };
            }

            case SuperConditionType.WeaponAtLevel:
            {
                if (c.requiredWeapon == null) return default;
                int have = weaponManager != null
                         ? weaponManager.GetWeaponLevel(c.requiredWeapon) + 1 : 0;
                return new SynergyLine
                {
                    icon   = c.requiredWeapon.icon,
                    label  = c.requiredWeapon.DisplayName,
                    detail = $"Lv{have}/{c.requiredLevel}",
                    met    = have >= c.requiredLevel,
                };
            }

            case SuperConditionType.PlayerLevel:
            {
                int have = SharedExperienceManager.Instance?.GetCurrentLevel() ?? 0;
                return new SynergyLine
                {
                    icon   = null,
                    label  = "เลเวลผู้เล่น",
                    detail = $"Lv{have}/{c.requiredLevel}",
                    met    = have >= c.requiredLevel,
                };
            }
        }
        return default;
    }

    private StatData FindStat(StatType t)
    {
        if (allStats == null) return null;
        foreach (var s in allStats) if (s != null && s.statType == t) return s;
        return null;
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
