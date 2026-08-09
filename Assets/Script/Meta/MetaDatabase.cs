using System.Collections.Generic;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// รวม asset ทั้งหมดที่ระบบ meta ต้องใช้ไว้ที่เดียว
    ///
    /// **ต้องวางไว้ที่ `Assets/Resources/MetaDatabase.asset`** — โค้ดโหลดด้วย
    /// `Resources.Load` เพื่อให้ทั้ง MenuScene และ gameplay scene เข้าถึงได้
    /// โดยไม่ต้องลาก reference ใน Inspector ทุกที่
    ///
    /// สร้างอัตโนมัติได้จาก  Tools &gt; Clone Swarm &gt; Meta &gt; Create Default Talents + Database
    /// </summary>
    [CreateAssetMenu(fileName = "MetaDatabase", menuName = "LoL Swarm/Meta/Meta Database")]
    public class MetaDatabase : ScriptableObject
    {
        const string ResourcePath = "MetaDatabase";

        [Header("Talent Shop")]
        public List<TalentData> talents = new();

        [Header("Characters (เรียงตามที่อยากให้โชว์ในหน้าเลือกตัว)")]
        public List<CharacterData> characters = new();

        [Header("Augments (สุ่มตอน level ที่กำหนดใน SharedExperienceManager)")]
        public List<AugmentData> augments = new();

        [Header("Status Effects")]
        public List<StatusEffectData> statuses = new();

        [Header("Economy")]
        [Tooltip("ทองที่ได้ = expReward ของ enemy × ค่านี้")]
        public float goldPerExp = 0.1f;
        [Tooltip("โบนัสทองเมื่อชนะ (ฆ่า Main Boss สำเร็จ)")]
        public int   winBonusGold = 500;
        [Tooltip("โบนัสทองต่อ 1 นาทีที่รอด")]
        public int   goldPerMinuteSurvived = 20;

        // ═══════════════════════════════════════════════════════════════════
        // Singleton access
        // ═══════════════════════════════════════════════════════════════════
        static MetaDatabase _instance;
        static bool         _warned;

        public static MetaDatabase Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = Resources.Load<MetaDatabase>(ResourcePath);
                if (_instance == null && !_warned)
                {
                    _warned = true;
                    Debug.LogError(
                        "[Meta] ไม่พบ Assets/Resources/MetaDatabase.asset — " +
                        "รัน Tools > Clone Swarm > Meta > Create Default Talents + Database ก่อน");
                }
                return _instance;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Lookups
        // ═══════════════════════════════════════════════════════════════════
        public TalentData GetTalent(string talentId)
        {
            if (string.IsNullOrEmpty(talentId)) return null;
            for (int i = 0; i < talents.Count; i++)
                if (talents[i] != null && talents[i].talentId == talentId) return talents[i];
            return null;
        }

        /// <summary>หา talent ที่บวกให้ StatType นี้ (mode = Stat)</summary>
        public TalentData GetTalentByStat(StatType type)
        {
            for (int i = 0; i < talents.Count; i++)
                if (talents[i] != null &&
                    talents[i].mode == TalentEffectMode.Stat &&
                    talents[i].statType == type) return talents[i];
            return null;
        }

        /// <summary>หา talent พิเศษที่ไม่ใช่ stat (GoldFind / SecondChance)</summary>
        public TalentData GetTalentByMode(TalentEffectMode mode)
        {
            for (int i = 0; i < talents.Count; i++)
                if (talents[i] != null && talents[i].mode == mode) return talents[i];
            return null;
        }

        public CharacterData GetCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return null;
            for (int i = 0; i < characters.Count; i++)
                if (characters[i] != null && characters[i].characterName == characterName)
                    return characters[i];
            return null;
        }

        public StatusEffectData GetStatus(string statusId)
        {
            if (string.IsNullOrEmpty(statusId)) return null;
            for (int i = 0; i < statuses.Count; i++)
                if (statuses[i] != null && statuses[i].statusId == statusId)
                    return statuses[i];
            return null;
        }

        /// <summary>talents เรียงตาม sortOrder — ร้านค้าใช้</summary>
        public List<TalentData> GetSortedTalents()
        {
            var list = new List<TalentData>();
            foreach (var t in talents) if (t != null) list.Add(t);
            list.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            return list;
        }
    }
}
