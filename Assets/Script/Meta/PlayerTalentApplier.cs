using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// เอา Talent ถาวรจากไฟล์เซฟมาใส่ผู้เล่นตอนเริ่ม run
    /// วางบน Player Prefab
    ///
    /// Authority pattern (เหมือน PlayerStatManager.ApplyStat):
    ///   owner อ่านเลเวลจากเซฟ → apply ฝั่งตัวเอง → ส่ง **แค่เลเวล** ไป server
    ///   server clamp เลเวลกับ maxLevel แล้วอ่าน "ค่า" จาก MetaDatabase ของตัวเอง
    ///   → client ปลอมค่าโบนัสไม่ได้ ปลอมได้แค่ระดับ talent ที่อ้างว่าซื้อมา
    ///     (ซึ่งเป็นข้อมูลใน save ฝั่ง client อยู่แล้ว — co-op PvE ยอมรับได้)
    ///
    /// ลำดับสำคัญ: ต้องรันหลัง PlayerWeaponManager.SetBaseStats ไม่งั้น
    /// โบนัส MaxHealth จะถูก baseHealth ทับ → จึงหน่วง 2 เฟรมก่อน apply
    /// </summary>
    public class PlayerTalentApplier : NetworkBehaviour
    {
        PlayerStatManager stats;
        playermove        move;

        /// <summary>ตัวคูณทองท้ายเกมของผู้เล่นคนนี้ — server อ่านตอนจ่ายรางวัล</summary>
        public float GoldFindMultiplier { get; private set; } = 1f;

        /// <summary>ยังเหลือสิทธิ์ชุบชีวิตอยู่ไหม (server ใช้ใน playermove.Die)</summary>
        public bool SecondChanceAvailable { get; private set; }

        // ═══════════════════════════════════════════════════════════════════
        public override void OnNetworkSpawn()
        {
            stats = GetComponent<PlayerStatManager>();
            move  = GetComponent<playermove>();

            if (IsOwner) StartCoroutine(ApplyAfterBaseStats());
        }

        IEnumerator ApplyAfterBaseStats()
        {
            // 2 เฟรม — ให้ PlayerWeaponManager.OnNetworkSpawn → SetBaseStats เสร็จก่อน
            yield return null;
            yield return null;

            var db = MetaDatabase.Instance;
            if (db == null) yield break;

            var sorted = db.GetSortedTalents();
            var levels = new int[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
                levels[i] = MetaProgression.GetTalentLevel(sorted[i]);

            ApplyLevels(levels);

            if (!IsServer) ApplyTalentsServerRpc(levels);
        }

        [Rpc(SendTo.Server)]
        void ApplyTalentsServerRpc(int[] levels)
        {
            if (stats == null) stats = GetComponent<PlayerStatManager>();
            if (move  == null) move  = GetComponent<playermove>();
            ApplyLevels(levels);
        }

        /// <summary>
        /// levels เรียงตาม MetaDatabase.GetSortedTalents() — ทั้งสองฝั่งอ่าน asset ชุดเดียวกัน
        /// </summary>
        void ApplyLevels(int[] levels)
        {
            var db = MetaDatabase.Instance;
            if (db == null || levels == null) return;

            var sorted = db.GetSortedTalents();

            for (int i = 0; i < sorted.Count && i < levels.Length; i++)
            {
                var t  = sorted[i];
                if (t == null) continue;

                int lv = Mathf.Clamp(levels[i], 0, t.MaxLevel);   // ★ server clamp
                if (lv <= 0) continue;

                float value = t.GetTotalValue(lv);

                switch (t.mode)
                {
                    // stat ทุกประเภทเดินทางเดียวกันหมด — เพิ่ม StatType ใหม่ไม่ต้องแก้ไฟล์นี้
                    case TalentEffectMode.Stat:
                        stats?.AddPermanentBonus(t.statType, value, move);
                        break;

                    case TalentEffectMode.GoldFind:
                        GoldFindMultiplier = 1f + value;
                        break;

                    case TalentEffectMode.SecondChance:
                        SecondChanceAvailable = true;
                        break;
                }
            }

            Debug.Log($"[Talent] {(IsServer ? "[Server]" : "[Client]")} ใส่ talent แล้ว · " +
                      $"gold×{GoldFindMultiplier:F2} · secondChance={SecondChanceAvailable}");
        }

        /// <summary>server เรียกตอนใช้สิทธิ์ชุบชีวิตไปแล้ว</summary>
        public void ConsumeSecondChance()
        {
            if (!IsServer) return;
            SecondChanceAvailable = false;
        }
    }
}
