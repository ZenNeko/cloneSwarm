using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>ข้อมูลหนึ่งคนที่จอสรุปผลต้องใช้ — เท่านี้พอ ไม่มีเลเวลไม่มีดาเมจ (ดู <see cref="ResultPartyRowUI"/>)</summary>
    public struct PartyMemberInfo
    {
        public string displayName;
        public int    slotIndex;    // 0..3 · ใช้เขียน P1..P4 และเลือกสี
        public bool   isHost;
    }

    /// <summary>
    /// แถวสมาชิกปาร์ตี้หนึ่งแถวในจอสรุปผล (Win/Lose)
    ///
    /// **แถวนี้ไม่แสดงเลเวลและดาเมจ โดยเจตนา** — handoff ขัดกันเองสองที่:
    /// หัวข้อ State Management บอกให้เก็บ "เลเวล, ดาเมจรวม" แต่หัวข้อ layout บอกว่าแถวนี้ไม่แสดง
    /// เลือกตาม layout เพราะเหตุผลที่เอกสารให้ไว้ฟังขึ้น — จอนี้สรุปผล "ของรอบ" ไม่ใช่สกอร์บอร์ดเทียบกันเอง
    /// และตัวเลขรายคนอยู่ใน HUD ตอนเล่นอยู่แล้ว · อยากได้สกอร์บอร์ดค่อยทำเป็นจอแยก
    ///
    /// ขนาดตาม handoff (กรอบ 1920×1080): สูง 94 · พื้น rgba(10,14,30,.66) · padding 0 22 · ระยะภายใน 20
    /// เส้นซ้าย 6px (host #FFE633 / คนอื่น rgba(255,255,255,.24))
    /// กล่องพอร์เทรต **470×58 เป็นแถบยาวแนวนอน** ไม่ใช่สี่เหลี่ยมจัตุรัส — เผื่อภาพตัวละครแบบ banner
    ///
    /// โครงที่ builder สร้างให้:
    ///   PartyRow  (Image พื้น · ResultPartyRowUI)
    ///   ├─ Accent    Image 6px ชิดซ้ายเต็มความสูง
    ///   ├─ Portrait  Image 470×58 (placeholder จนกว่าจะมีภาพจริง)
    ///   └─ NameBlock
    ///      ├─ Name   TextMeshProUGUI 28px หนา
    ///      └─ Sub    TextMeshProUGUI mono 15px  "P1 · HOST"
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultPartyRowUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("เส้นเน้นซ้าย 6px — ลายเซ็นของการ์ดทุกใบในระบบ")]
        public Image accentBar;

        [Tooltip("กล่องพอร์เทรต 470×58 — ยังไม่มีภาพจริง (Char_*.portrait ยังขาด)")]
        public Image portrait;

        [Tooltip("ชื่อ — handoff: 28px หนา 800")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("บรรทัดรอง — handoff: mono 15px  P1 · HOST / P2")]
        public TextMeshProUGUI subLabel;

        [Header("── Look ───────────────────────────────")]
        public Color hostAccent  = new Color32(0xFF, 0xE6, 0x33, 0xFF);
        public Color otherAccent = new Color(1f, 1f, 1f, 0.24f);

        // ═══════════════════════════════════════════════════════════════════
        // Bind
        // ═══════════════════════════════════════════════════════════════════
        public void Bind(PartyMemberInfo info) => Bind(info.displayName, info.slotIndex, info.isHost);

        public void Bind(string displayName, int slotIndex, bool isHost)
        {
            if (nameLabel != null)
                nameLabel.text = string.IsNullOrEmpty(displayName) ? "ผู้เล่น" : displayName;

            if (subLabel != null)
            {
                // slot ติดลบ = หา slot ไม่เจอ (registry ยังไม่พร้อม) — ยังต้องโชว์แถวได้อยู่ดี
                string pTag = slotIndex >= 0 ? $"P{slotIndex + 1}" : "P?";
                subLabel.text = isHost ? $"{pTag} · HOST" : pTag;
            }

            if (accentBar != null) accentBar.color = isHost ? hostAccent : otherAccent;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Roster
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// อ่านรายชื่อปาร์ตี้จากของที่ **client ก็เห็น** — ไม่ต้องเพิ่ม RPC ใหม่
        ///
        /// • ช่องผู้เล่น: <see cref="PlayerSlotRegistry.Slots"/> เป็น NetworkList ที่ replicate ให้ทุกคน
        ///   (CLAUDE.md ข้อ 11 — ห้ามใช้ clientId % 4)
        /// • ชื่อ: หา player object จาก SpawnManager.SpawnedObjectsList แล้วอ่าน PlayerVisual.CharacterIndex
        ///   ซึ่งเป็น NetworkVariable จึงเชื่อถือได้ทั้ง host และ client
        ///   **ห้ามใช้ SpawnManager.GetPlayerNetworkObject** — NGO 2.x log error ทันทีเมื่อ client
        ///   ถามหา player object ของคนอื่น
        /// • host: clientId == NetworkManager.ServerClientId
        ///
        /// ยังไม่มีระบบ "ชื่อผู้เล่นที่ตั้งเอง" ในโปรเจกต์ (LobbyState เก็บแค่ characterName)
        /// จึงใช้ชื่อตัวละครเป็นชื่อที่โชว์ไปก่อน — วันไหนมีชื่อผู้เล่นจริงให้แก้ที่เมธอดนี้ที่เดียว
        /// </summary>
        public static List<PartyMemberInfo> CollectFromNetwork()
        {
            var result = new List<PartyMemberInfo>(4);
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return result;

            // clientId → ชื่อที่จะโชว์ (เดินลิสต์ที่ spawn ครั้งเดียว แทนที่จะค้นซ้ำทุก slot)
            var names = new Dictionary<ulong, string>();
            if (nm.SpawnManager != null)
            {
                foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (no == null || !no.IsPlayerObject) continue;
                    names[no.OwnerClientId] = ResolveDisplayName(no);
                }
            }

            var registry = PlayerSlotRegistry.Instance;
            if (registry != null && registry.Slots != null)
            {
                for (int slot = 0; slot < registry.Slots.Count; slot++)
                {
                    ulong id = registry.Slots[slot];
                    if (id == ulong.MaxValue) continue;   // ช่องว่าง

                    result.Add(new PartyMemberInfo
                    {
                        displayName = names.TryGetValue(id, out var n) ? n : $"ผู้เล่น {slot + 1}",
                        slotIndex   = slot,
                        isHost      = id == NetworkManager.ServerClientId,
                    });
                }
                return result;
            }

            // registry ยังไม่ spawn (โซโล/ทดสอบ) — ยังดีกว่าโชว์จอเปล่า
            int fallbackSlot = 0;
            foreach (var kv in names)
            {
                result.Add(new PartyMemberInfo
                {
                    displayName = kv.Value,
                    slotIndex   = fallbackSlot++,
                    isHost      = kv.Key == NetworkManager.ServerClientId,
                });
            }
            return result;
        }

        static string ResolveDisplayName(NetworkObject playerObject)
        {
            var visual = playerObject.GetComponent<PlayerVisual>();
            if (visual != null)
            {
                var cd = visual.GetCharacterData(visual.CharacterIndex);
                if (cd != null) return cd.DisplayName;   // DisplayName ไม่ใช่ characterName ที่เป็น ID
            }

            var pwm = playerObject.GetComponent<PlayerWeaponManager>();
            if (pwm != null && pwm.characterData != null) return pwm.characterData.DisplayName;

            return null;
        }
    }
}
