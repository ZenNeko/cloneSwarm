using CloneSwarm.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// บัญชีรางวัลหนึ่งบรรทัดในจอสรุปผล — "รอดถึงนาที 15   +750"
    ///
    /// ตัวนี้ไม่รู้จักเน็ตเวิร์กและไม่คิดเลขเอง — รับค่าที่ server คำนวณมาแล้วมาแสดงอย่างเดียว
    /// (แพตเทิร์นเดียวกับ <see cref="P3RMenuItem"/> ที่ P3RMenuList เป็นคนสั่งทั้งหมด)
    ///
    /// **ทำไมคำไทยอยู่ที่นี่ ไม่ได้อยู่ใน RunRewardBreakdown** — struct ตัวนั้นเดินทางข้ามเน็ตเวิร์ก
    /// แบกข้อความไปด้วยคือเปลืองแบนด์วิดท์และผูกภาษาของเครื่อง server ให้ทุกคน
    /// ฝั่ง server ส่งมาแค่ "เหตุผล + ตัวเลข" ฝั่งจอเป็นคนแปลเป็นคำ
    ///
    /// โครงที่ builder สร้างให้:
    ///   RewardLine  (RectTransform สูง ~64 · RewardLineUI)
    ///   ├─ Reason   TextMeshProUGUI 24px alpha .72 ชิดซ้าย
    ///   ├─ Value    TextMeshProUGUI mono 30px หนา ขาว ชิดขวา
    ///   └─ Divider  Image 1px rgba(255,255,255,.1) ที่ขอบล่าง
    /// </summary>
    [DisallowMultipleComponent]
    public class RewardLineUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("ชื่อเหตุผล — handoff: 24px · alpha .72")]
        public TextMeshProUGUI reasonLabel;

        [Tooltip("ค่าทอง — handoff: mono 30px หนา สีขาว")]
        public TextMeshProUGUI valueLabel;

        [Tooltip("เส้นคั่นล่าง 1px rgba(255,255,255,.1) — ปล่อยว่างได้ถ้าไม่ต้องการ")]
        public Image divider;

        [Header("── Look ───────────────────────────────")]
        [Tooltip("สีค่าปกติ — ขาวล้วนตาม handoff")]
        public Color valueColor = Color.white;

        [Tooltip("สีค่าตอนติดลบ (ยังไม่มีเหตุผลไหนติดลบ แต่กันไว้ให้เห็นชัดถ้ามีวันนั้น)")]
        public Color negativeValueColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>ผูกค่าดิบ — ใช้เมื่ออยากเขียนคำเอง</summary>
        public void Bind(string reasonText, int gold)
        {
            if (reasonLabel != null) reasonLabel.text = reasonText;
            if (valueLabel != null)
            {
                // เครื่องหมายมาก่อนเสมอ — ตาไล่คอลัมน์ขวาแล้วต้องรู้ทันทีว่าบวกหรือลบ
                valueLabel.text  = gold >= 0 ? $"+{gold:N0}" : $"-{Mathf.Abs(gold):N0}";
                valueLabel.color = gold >= 0 ? valueColor : negativeValueColor;
            }
        }

        /// <summary>ผูกจากบัญชีที่ server ส่งมา — แปลเหตุผลเป็นคำไทยให้เอง</summary>
        public void Bind(RewardReason reason, int gold, int count)
            => Bind(DefaultLabel(reason, count), gold);

        public void SetDividerVisible(bool on)
        {
            if (divider != null) divider.enabled = on;
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// คำไทยของแต่ละเหตุผล · <paramref name="count"/> คือจำนวนที่เกี่ยวข้อง
        /// (นาทีที่รอด / จำนวนศัตรู) — เหตุผลที่ไม่มีจำนวนส่งอะไรมาก็ได้
        ///
        /// ยังไม่ผ่าน Unity Localization เพราะ String Table ของจอนี้ยังไม่ถูกวาง
        /// — เมื่อวางแล้วให้เปลี่ยนแค่เมธอดนี้เมธอดเดียว จุดเรียกไม่ต้องแก้
        /// </summary>
        public static string DefaultLabel(RewardReason reason, int count) => reason switch
        {
            RewardReason.TimeSurvived  => count > 0 ? $"รอดถึงนาที {count}" : "เวลาที่รอด",
            RewardReason.EnemyKills    => count > 0 ? $"ศัตรูที่กำจัด {count:N0} ตัว" : "ศัตรูที่กำจัด",
            RewardReason.ObjectiveGold => "ทองพิเศษระหว่างรอบ",
            RewardReason.WinBonus      => "ล้ม Main Boss สำเร็จ",
            RewardReason.GoldFind      => "โบนัสนักล่าสมบัติ",
            _                          => "อื่นๆ",
        };
    }
}
