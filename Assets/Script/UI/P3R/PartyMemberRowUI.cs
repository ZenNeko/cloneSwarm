using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถวเพื่อนร่วมทีมหนึ่งบรรทัดบน HUD ตอนเล่น — เส้นสีประจำช่อง · PLAYER n · ชื่อตัวละคร · HP หรือเวลาฟื้น
    ///
    /// **ตัวนี้ไม่รู้จัก netcode เลยโดยเจตนา** — เหมือน <see cref="PausePartyRowUI"/>
    /// ไม่ไปหา <c>playermove</c> เอง ไม่ subscribe NetworkVariable ใดๆ รับค่าที่ย่อยแล้วเข้ามา
    ///
    /// เหตุผลเดิม: แถวถูกใช้ซ้ำจาก pool (จำนวนคนในห้องเปลี่ยนได้ตอนคนหลุด)
    /// ถ้าแต่ละแถวผูก NetworkVariable เอง ต้องมีโค้ดถอด subscribe ตอนคืน pool
    /// ซึ่งเป็นจุดที่ลืมกันบ่อยที่สุดและรั่วเงียบ · ให้ <see cref="PartyMemberHUD"/> อ่านที่เดียวแล้วป้อนลงมา
    ///
    /// โครงที่ตัวสร้างวางให้ (414×48):
    ///   PartyMemberRow   Image พื้น · PartyMemberRowUI
    ///   ├─ Accent        Image 4×48 ชิดซ้าย — ลายเซ็น border-left เดียวกับการ์ดทุกใบ
    ///   ├─ NameBlock     PLAYER n (mono เล็ก) เหนือ ชื่อตัวละคร (หนา)
    ///   └─ Value         ขวาสุด — "240 / 240" หรือ "REVIVE 6S"
    /// </summary>
    [DisallowMultipleComponent]
    public class PartyMemberRowUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        public Image           background;
        [Tooltip("เส้นเน้นซ้าย — สีประจำช่องผู้เล่น (PlayerSlotRegistry) ไม่ใช่สีสถานะ")]
        public Image           accent;
        [Tooltip("'PLAYER 1' — mono ตัวเล็ก")]
        public TextMeshProUGUI playerLabel;
        [Tooltip("ชื่อตัวละคร 'HUNTER' — ตัวหนา")]
        public TextMeshProUGUI nameText;
        [Tooltip("'240 / 240' ตอนเป็น · 'REVIVE 6S' ตอนล้ม")]
        public TextMeshProUGUI valueText;

        [Header("── Palette ────────────────────────────")]
        [Tooltip("สีตัวเลข HP ตอนยังเป็นอยู่")]
        public Color aliveColor  = new Color32(0x3E, 0xE0, 0xC4, 0xFF);
        [Tooltip("สีข้อความตอนล้มรอฟื้น")]
        public Color downedColor = new Color32(0xE8, 0x3A, 0x3A, 0xFF);
        [Tooltip("สีชื่อตอนล้ม — จางลงเพื่อให้แถวที่ยังเป็นเด่นกว่า")]
        public Color downedNameColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("สีชื่อปกติ")]
        public Color nameColor   = Color.white;

        /// <summary>ชื่อช่องผู้เล่น + สีเส้นซ้าย — เปลี่ยนไม่บ่อย แยกจากค่าที่เปลี่ยนทุกเฟรม</summary>
        public void SetIdentity(int slotNumber, string characterName, Color slotColor)
        {
            if (playerLabel != null) playerLabel.text = $"PLAYER {slotNumber}";
            if (nameText    != null) nameText.text    = string.IsNullOrEmpty(characterName)
                                                      ? "—" : characterName.ToUpperInvariant();
            if (accent      != null) accent.color     = slotColor;
        }

        /// <summary>ยังเป็นอยู่ — โชว์ HP ปัจจุบัน/สูงสุด</summary>
        public void SetAlive(float hp, float maxHp)
        {
            if (nameText  != null) nameText.color  = nameColor;
            if (valueText == null) return;

            valueText.color = aliveColor;
            // ปัดขึ้น — HP 0.4 ที่โชว์เป็น "0" อ่านว่าตายแล้วทั้งที่ยังไม่ตาย
            valueText.text  = $"{Mathf.CeilToInt(Mathf.Max(0f, hp))} / {Mathf.CeilToInt(Mathf.Max(1f, maxHp))}";
        }

        /// <summary>
        /// ล้มรอฟื้น — โชว์เวลาที่เหลือ
        ///
        /// ปัดขึ้นเช่นกัน · เหลือ 0.2 วิแล้วโชว์ "REVIVE 0S" อ่านแล้วเหมือนค้าง
        /// </summary>
        public void SetDowned(float secondsLeft)
        {
            if (nameText  != null) nameText.color  = downedNameColor;
            if (valueText == null) return;

            valueText.color = downedColor;
            int s = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
            valueText.text  = s > 0 ? $"REVIVE {s}S" : "REVIVE";
        }
    }
}
