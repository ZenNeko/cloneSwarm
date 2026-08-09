using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// โครงสร้างไฟล์เซฟ — serialize ด้วย JsonUtility
    ///
    /// กฎ: field ทุกตัวต้อง public + [Serializable] และ **ห้ามลบ field เก่า**
    /// (ถ้าเลิกใช้ให้ mark [Obsolete] แต่คงไว้ ไม่งั้นไฟล์เซฟเก่าอ่านไม่ออก)
    /// เพิ่ม field ใหม่ได้เสมอ — JsonUtility จะใส่ค่า default ให้เอง
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>เลข schema — เพิ่มทีละ 1 เมื่อโครงสร้างเปลี่ยนแบบ breaking</summary>
        public const int CurrentVersion = 1;

        public int    version    = CurrentVersion;
        public string profileId  = "";      // GUID ประจำเครื่อง — เผื่อ sync cloud ทีหลัง
        public long   createdUtc;
        public long   lastPlayedUtc;

        // ── Currency ──────────────────────────────────────────────────────
        public int gold          = 0;
        public int lifetimeGold  = 0;       // ทองสะสมทั้งหมด (ไม่ลดตอนใช้) — ใช้ทำ achievement

        // ── Progression ───────────────────────────────────────────────────
        public List<TalentEntry>     talents            = new();
        public List<string>          unlockedCharacters = new();   // characterName

        // ── Career Stats ──────────────────────────────────────────────────
        public int   totalRuns    = 0;
        public int   totalWins    = 0;
        public int   totalKills   = 0;
        public float bestTimeSec  = 0f;     // เวลารอดนานที่สุด
        public int   highestLevel = 0;

        // ═══════════════════════════════════════════════════════════════════
        // Nested types
        // ═══════════════════════════════════════════════════════════════════
        [Serializable]
        public class TalentEntry
        {
            public string id;      // TalentData.talentId
            public int    level;   // 0 = ยังไม่ซื้อ
        }

        // ═══════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════
        public int GetTalentLevel(string talentId)
        {
            if (string.IsNullOrEmpty(talentId)) return 0;
            for (int i = 0; i < talents.Count; i++)
                if (talents[i].id == talentId) return talents[i].level;
            return 0;
        }

        public void SetTalentLevel(string talentId, int level)
        {
            if (string.IsNullOrEmpty(talentId)) return;
            for (int i = 0; i < talents.Count; i++)
            {
                if (talents[i].id == talentId) { talents[i].level = level; return; }
            }
            talents.Add(new TalentEntry { id = talentId, level = level });
        }

        public bool IsCharacterUnlocked(string characterName)
            => !string.IsNullOrEmpty(characterName) && unlockedCharacters.Contains(characterName);

        public void UnlockCharacter(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return;
            if (!unlockedCharacters.Contains(characterName))
                unlockedCharacters.Add(characterName);
        }

        /// <summary>เซฟใหม่เอี่ยม — ตั้งค่าเริ่มต้นทั้งหมด</summary>
        public static SaveData CreateNew()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new SaveData
            {
                version       = CurrentVersion,
                profileId     = Guid.NewGuid().ToString("N"),
                createdUtc    = now,
                lastPlayedUtc = now,
            };
        }
    }
}
