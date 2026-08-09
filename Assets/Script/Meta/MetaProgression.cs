using System;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// หน้าบ้านของระบบ meta — ทุกอย่างที่ UI และ gameplay ต้องเรียก อยู่ที่นี่
    /// (SaveManager ดูแลไฟล์, MetaDatabase ดูแล asset, class นี้ดูแล "กฎ")
    /// </summary>
    public static class MetaProgression
    {
        /// <summary>ยิงเมื่อทองเปลี่ยน — HUD ร้านค้า subscribe</summary>
        public static event Action<int> OnGoldChanged;
        /// <summary>ยิงเมื่อซื้อ talent สำเร็จ (talentId, levelใหม่)</summary>
        public static event Action<string, int> OnTalentPurchased;
        /// <summary>ยิงเมื่อปลดล็อกตัวละครสำเร็จ</summary>
        public static event Action<CharacterData> OnCharacterUnlocked;

        // ═══════════════════════════════════════════════════════════════════
        // Gold
        // ═══════════════════════════════════════════════════════════════════
        public static int Gold => SaveManager.Data.gold;

        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            var d = SaveManager.Data;
            d.gold         += amount;
            d.lifetimeGold += amount;
            SaveManager.MarkDirty();
            OnGoldChanged?.Invoke(d.gold);
        }

        static bool TrySpendGold(int amount)
        {
            var d = SaveManager.Data;
            if (amount < 0 || d.gold < amount) return false;
            d.gold -= amount;
            SaveManager.MarkDirty();
            OnGoldChanged?.Invoke(d.gold);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Talents
        // ═══════════════════════════════════════════════════════════════════
        public static int GetTalentLevel(TalentData t)
            => t == null ? 0 : Mathf.Clamp(SaveManager.Data.GetTalentLevel(t.talentId), 0, t.MaxLevel);

        /// <summary>ค่าผลรวมของ talent ที่บวกให้ StatType นี้ (0 = ไม่มี talent หรือยังไม่ซื้อ)</summary>
        public static float GetStatTalentValue(StatType type)
        {
            var t = MetaDatabase.Instance?.GetTalentByStat(type);
            return t == null ? 0f : t.GetTotalValue(GetTalentLevel(t));
        }

        /// <summary>ค่าผลรวมของ talent พิเศษ (GoldFind / SecondChance)</summary>
        public static float GetSpecialTalentValue(TalentEffectMode mode)
        {
            var t = MetaDatabase.Instance?.GetTalentByMode(mode);
            return t == null ? 0f : t.GetTotalValue(GetTalentLevel(t));
        }

        public static bool HasSecondChance()
        {
            var t = MetaDatabase.Instance?.GetTalentByMode(TalentEffectMode.SecondChance);
            return t != null && GetTalentLevel(t) > 0;
        }

        public static bool CanUpgradeTalent(TalentData t)
        {
            if (t == null) return false;
            int lv = GetTalentLevel(t);
            if (lv >= t.MaxLevel) return false;
            int cost = t.GetCostToUpgrade(lv);
            return cost >= 0 && SaveManager.Data.gold >= cost;
        }

        /// <summary>ซื้อ talent 1 เลเวล — คืน false ถ้าทองไม่พอหรือตันแล้ว</summary>
        public static bool TryUpgradeTalent(TalentData t)
        {
            if (t == null) return false;

            int lv = GetTalentLevel(t);
            if (lv >= t.MaxLevel) return false;

            int cost = t.GetCostToUpgrade(lv);
            if (cost < 0 || !TrySpendGold(cost)) return false;

            SaveManager.Data.SetTalentLevel(t.talentId, lv + 1);
            SaveManager.Save();                      // ซื้อของ = เขียนทันที ไม่รอ flush
            OnTalentPurchased?.Invoke(t.talentId, lv + 1);
            Debug.Log($"[Meta] ซื้อ {t.talentName} Lv{lv + 1} (-{cost}g)");
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Character unlocks
        // ═══════════════════════════════════════════════════════════════════
        public static bool IsCharacterUnlocked(CharacterData cd)
        {
            if (cd == null) return false;
            if (cd.unlockedByDefault) return true;
            return SaveManager.Data.IsCharacterUnlocked(cd.characterName);
        }

        public static bool CanUnlockCharacter(CharacterData cd)
            => cd != null
               && !IsCharacterUnlocked(cd)
               && SaveManager.Data.gold >= cd.unlockCost;

        public static bool TryUnlockCharacter(CharacterData cd)
        {
            if (cd == null || IsCharacterUnlocked(cd)) return false;
            if (!TrySpendGold(cd.unlockCost))         return false;

            SaveManager.Data.UnlockCharacter(cd.characterName);
            SaveManager.Save();
            OnCharacterUnlocked?.Invoke(cd);
            Debug.Log($"[Meta] ปลดล็อก {cd.characterName} (-{cd.unlockCost}g)");
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Run results
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// บันทึกผลจบเกม + จ่ายทอง — เรียกครั้งเดียวต่อ run จาก RunRewardTracker
        /// </summary>
        public static void RecordRunResult(bool won, int goldEarned, float survivedSec, int level, int kills)
        {
            var d = SaveManager.Data;

            d.totalRuns++;
            if (won) d.totalWins++;
            d.totalKills  += Mathf.Max(0, kills);
            d.bestTimeSec  = Mathf.Max(d.bestTimeSec, survivedSec);
            d.highestLevel = Mathf.Max(d.highestLevel, level);

            AddGold(goldEarned);   // AddGold เรียก MarkDirty ให้แล้ว
            SaveManager.Save();    // จบเกม = เขียนทันที กัน alt-F4 แล้วทองหาย

            Debug.Log($"[Meta] จบเกม {(won ? "WIN" : "LOSE")} · +{goldEarned}g · " +
                      $"รวม {d.gold}g · run ที่ {d.totalRuns}");
        }
    }
}
