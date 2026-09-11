using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถวผู้เล่นหนึ่งคนในปาร์ตี้บนจอ Lobby
    ///
    /// แทนแถวเดิมที่เป็น TMP บรรทัดเดียว (`Player 1: Riven [READY]`)
    /// แบบขอสองบรรทัด: ชื่อช่อง + ป้าย HOST/YOU บรรทัดบน · ตัวละคร + HP + สถานะบรรทัดล่าง
    ///
    /// **สีขอบซ้ายมาจาก <c>PlayerSlotRegistry.GetSlot()</c> ห้ามใช้ clientId % 4**
    /// (CLAUDE.md ข้อ 11) — clientId ไม่ได้เรียงจาก 0 และไม่ถูกใช้ซ้ำเมื่อมีคนออก
    /// ผู้เล่นคนเดิมจะเปลี่ยนสีกลางคันถ้าคำนวณจาก clientId
    ///
    /// รองรับสองสถานะ: มีคนนั่งอยู่ (<see cref="Bind"/>) กับช่องว่างรอคนเข้า (<see cref="SetEmpty"/>)
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyPartyRowUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        public Image           background;
        public Image           accentBar;
        [Tooltip("รูปตัวละครของผู้เล่นคนนี้ — ปิด Image ทิ้งเมื่อไม่มีรูป จะได้ไม่เหลือสี่เหลี่ยมสีเปล่า")]
        public Image           portraitImage;
        public TextMeshProUGUI slotLabel;      // PLAYER 0
        public TextMeshProUGUI nameLabel;      // RIVEN
        public TextMeshProUGUI detailLabel;    // HP 120/120 หรือ "กำลังเลือก…"
        public TextMeshProUGUI statusLabel;    // READY / PICKING…

        [Header("── Badges ─────────────────────────────")]
        public GameObject hostBadge;
        public GameObject youBadge;

        [Header("── Empty slot ─────────────────────────")]
        [Tooltip("กลุ่มที่โชว์ตอนช่องยังว่าง — เส้นประ + คำว่าเชิญเพื่อน")]
        public GameObject emptyGroup;
        [Tooltip("กลุ่มที่โชว์ตอนมีคนนั่งอยู่")]
        public GameObject filledGroup;

        [Header("── Colours ────────────────────────────")]
        public Color readyColor    = new Color32(0x2C, 0xC5, 0xA0, 0xFF);

        [Header("── Type ───────────────────────────────")]
        public float slotTracking   = 22f;
        public float statusTracking = 18f;

        /// <summary>
        /// <paramref name="accent"/> ควรมาจากสีประจำช่องของผู้เล่น ไม่ใช่คำนวณจาก clientId
        /// <paramref name="detail"/> ปล่อยว่างได้เมื่อยังไม่รู้ HP (ตอนอยู่ล็อบบี้ยังไม่ spawn)
        /// </summary>
        public void Bind(int slot, string displayName, bool isHost, bool isYou,
                         bool ready, string detail, Color accent, Sprite portrait = null)
        {
            if (emptyGroup  != null) emptyGroup.SetActive(false);
            if (filledGroup != null) filledGroup.SetActive(true);

            if (accentBar != null) accentBar.color = accent;

            if (slotLabel != null)
                P3RText.SetTextAndTracking(slotLabel,
                    slot >= 0 ? $"PLAYER {slot + 1}" : "PLAYER ?", slotTracking);

            // ชื่อตัวละครมาจาก String Table จึงเป็นไทยได้ — ห้ามถ่างระยะ
            if (nameLabel   != null) P3RText.SetTextAndTracking(nameLabel, displayName ?? "", 0f);
            if (detailLabel != null) P3RText.SetTextAndTracking(detailLabel, detail ?? "", 0f);

            if (portraitImage != null)
            {
                portraitImage.sprite  = portrait;
                portraitImage.enabled = portrait != null;
            }

            // **สถานะโชว์เฉพาะตอนพร้อมแล้ว** — "PICKING…" คือสถานะปกติของทุกคนที่เพิ่งเข้ามา
            // ป้ายที่ขึ้นตลอดจนกว่าจะพร้อมไม่ได้บอกอะไรเลย มันแค่ทำให้แถวรก
            // ที่ผู้เล่นต้องกวาดตาหาคือ "ใครพร้อมแล้วบ้าง" ซึ่งเห็นชัดกว่าเมื่อมีแค่คนที่พร้อม
            if (statusLabel != null)
            {
                if (statusLabel.gameObject.activeSelf != ready) statusLabel.gameObject.SetActive(ready);
                if (ready)
                {
                    P3RText.SetTextAndTracking(statusLabel, "READY", statusTracking);
                    statusLabel.color = readyColor;
                }
            }

            if (hostBadge != null) hostBadge.SetActive(isHost);
            if (youBadge  != null) youBadge.SetActive(isYou);
        }

        /// <summary>ช่องว่างรอคนเข้า — เส้นประตามแบบ</summary>
        public void SetEmpty()
        {
            if (filledGroup != null) filledGroup.SetActive(false);
            if (emptyGroup  != null) emptyGroup.SetActive(true);
            if (hostBadge   != null) hostBadge.SetActive(false);
            if (youBadge    != null) youBadge.SetActive(false);
            if (accentBar   != null) accentBar.color = new Color(1f, 1f, 1f, 0.14f);
            if (portraitImage != null) portraitImage.enabled = false;
        }
    }
}
