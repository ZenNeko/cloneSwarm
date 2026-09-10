using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถวสมาชิกปาร์ตี้หนึ่งบรรทัดในจอ Pause — พอร์เทรต · ชื่อ · เลเวล · หลอด HP
    ///
    /// **ตัวนี้ไม่รู้จัก netcode เลยโดยเจตนา**
    /// มันไม่ไปหา <c>playermove</c> เอง ไม่ subscribe NetworkVariable ใดๆ —
    /// รับค่าที่ย่อยแล้วเข้ามาทาง <see cref="SetIdentity"/> / <see cref="SetVitals"/> เท่านั้น
    /// เหตุผล: แถวนี้ถูกใช้ซ้ำจาก pool (จำนวนแถว = จำนวนคนในห้องจริง ซึ่งเปลี่ยนได้ตอนคนหลุด)
    /// ถ้าแต่ละแถวไปผูก NetworkVariable เอง จะต้องมีโค้ด unsubscribe ตอนคืน pool
    /// ซึ่งเป็นจุดที่ลืมกันบ่อยที่สุดและรั่วเงียบ · ให้ <c>PauseMenuUI</c> เป็นคนอ่านที่เดียวแล้วป้อนลงมา
    ///
    /// **ใครเรียก:** <c>PauseMenuUI.RefreshParty()</c> — เรียกทุกครั้งที่เปิดเมนู และ tick ซ้ำระหว่างเปิด
    /// (co-op โลกยังเดินอยู่ HP เปลี่ยนได้ตลอดเวลาที่เมนูค้างอยู่)
    ///
    /// โครงที่ <c>P3RPauseSceneBuilder</c> สร้างให้ (760×90):
    ///   PartyRow      Image พื้น #111838 · PausePartyRowUI
    ///   ├─ Accent     Image 6×90 ชิดซ้าย — ลายเซ็น border-left ของการ์ดทุกใบในระบบ
    ///   ├─ Portrait   Image กรอบ 56×56 · ลูกในคือรูปจริง
    ///   ├─ NameBlock  กว้างคงที่ 190 — ชื่อ 27px + บรรทัดรอง mono 15px
    ///   └─ Vitals     Lv ซ้าย / HP ตัวเลขขวา / หลอด HP สูง 9
    /// </summary>
    [DisallowMultipleComponent]
    public class PausePartyRowUI : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        // WIRING — builder เป็นคนใส่ให้ · ลากมือใน Inspector ก็ได้
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Wiring ─────────────────────────────")]
        public Image             background;
        [Tooltip("เส้นเน้นซ้าย 6px — สีบอกว่าเป็นตัวเราเอง/host หรือคนอื่น")]
        public Image             accent;
        [Tooltip("กรอบพอร์เทรต — พื้นจางๆ ที่เห็นตอนยังไม่มีรูป")]
        public Image             portraitFrame;
        [Tooltip("รูปตัวละครจริง · ยังไม่มี Char_*.portrait ครบ จึงต้องซ่อนได้เมื่อ sprite = null")]
        public Image             portrait;
        public TextMeshProUGUI   nameText;
        [Tooltip("บรรทัดรอง mono — `P1 · HOST` / `P2`")]
        public TextMeshProUGUI   subText;
        public TextMeshProUGUI   levelText;
        [Tooltip("`182 / 220`")]
        public TextMeshProUGUI   hpText;
        [Tooltip("หลอดที่เติม — ย่อด้วย anchorMax.x ไม่ใช่ fillAmount\n" +
                 "Image.fillAmount ใช้ไม่ได้เมื่อไม่มี sprite (uGUI ตกไปวาด quad เต็มใบเงียบๆ)")]
        public RectTransform     hpFill;
        public Image             hpFillImage;

        // ═══════════════════════════════════════════════════════════════════
        // PALETTE — ค่าตรงจาก design handoff · เปิดให้จูนใน Inspector ได้
        // แยกจาก P3RTheme เพราะ theme ถือแค่สีของ "แถบเมนู" ไม่ได้ถือสีสถานะ HP
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Palette ────────────────────────────")]
        [Tooltip("เส้นซ้ายของตัวเราเอง/host")]
        public Color accentSelf   = new Color32(0x18, 0x24, 0xD8, 0xFF);
        [Tooltip("เส้นซ้ายของคนอื่น — rgba(255,255,255,.22)")]
        public Color accentOther  = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("HP ปกติ")]
        public Color hpHealthy    = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        [Tooltip("HP ต่ำกว่าครึ่ง")]
        public Color hpLow        = new Color32(0xD9, 0x9A, 0x1A, 0xFF);
        [Tooltip("คนที่ล้มแล้ว — ทั้งแถวจางลง")]
        [Range(0.1f, 1f)] public float deadAlpha = 0.45f;

        // ── runtime ────────────────────────────────────────────────────────
        private CanvasGroup group;

        private void Awake() => CacheRefs();

        private void CacheRefs()
        {
            if (group == null && !TryGetComponent(out group))
                group = gameObject.AddComponent<CanvasGroup>();
        }

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC API — PauseMenuUI ป้อนค่าเข้ามาทางนี้เท่านั้น
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// ตั้งตัวตนของแถว — ส่วนที่ไม่เปลี่ยนระหว่างที่เมนูเปิดอยู่
        /// </summary>
        /// <param name="displayName">ชื่อที่โชว์ — ต้องมาจาก <c>CharacterData.DisplayName</c> ไม่ใช่ characterName</param>
        /// <param name="subLabel">บรรทัดรอง เช่น `P1 · HOST` — สร้างจาก PlayerSlotRegistry.GetSlot()</param>
        /// <param name="portraitSprite">null ได้ — จะซ่อนรูปเหลือแค่กรอบเปล่า (พอร์เทรตยังทำไม่ครบ)</param>
        /// <param name="highlight">true = ตัวเราเองหรือ host → เส้นซ้ายสีน้ำเงิน</param>
        public void SetIdentity(string displayName, string subLabel, Sprite portraitSprite, bool highlight)
        {
            if (nameText != null) nameText.text = displayName ?? "";
            if (subText  != null) subText.text  = subLabel    ?? "";

            if (accent != null) accent.color = highlight ? accentSelf : accentOther;

            if (portrait != null)
            {
                portrait.sprite  = portraitSprite;
                // ปิด Image ไปเลยตอนไม่มี sprite — ไม่งั้น uGUI วาดสี่เหลี่ยมทึบทับกรอบ
                portrait.enabled = portraitSprite != null;
            }
        }

        /// <summary>
        /// ตั้งค่าที่เปลี่ยนตลอดเวลา — เรียกซ้ำได้ทุก tick ไม่มีการ allocate นอกจาก string ของ TMP
        /// </summary>
        /// <param name="level">เลเวลปาร์ตี้ (โปรเจกต์นี้ XP เป็น shared pool เลเวลจึงเท่ากันทุกคน)</param>
        public void SetVitals(int level, float hp, float maxHp, bool isDead)
        {
            CacheRefs();

            if (levelText != null) levelText.text = $"Lv {level}";

            float safeMax = Mathf.Max(1f, maxHp);
            float ratio   = Mathf.Clamp01(hp / safeMax);

            if (hpText != null)
                hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, hp))} / {Mathf.CeilToInt(safeMax)}";

            if (hpFill != null)
            {
                // ยืด/หดด้วย anchorMax แทน localScale — หลอดสูงแค่ 9px ถ้าใช้ scale
                // ขอบจะเบลอเพราะ mesh ถูกย่อทั้งใบ ส่วน anchor ทำให้ขอบยังคมเสมอ
                var max = hpFill.anchorMax;
                max.x = ratio;
                hpFill.anchorMax = max;
                hpFill.offsetMin = Vector2.zero;
                hpFill.offsetMax = Vector2.zero;
            }

            if (hpFillImage != null)
            {
                // เส้นแบ่งที่ครึ่งเดียวตาม handoff — ไม่มีขั้นกลาง เพราะจุดประสงค์คือ
                // "เหลือบตาแล้วรู้ทันทีว่าใครกำลังจะตาย" ไม่ใช่การไล่เฉดสวยงาม
                hpFillImage.color = ratio < 0.5f ? hpLow : hpHealthy;
                hpFillImage.enabled = ratio > 0.001f;
            }

            if (group != null) group.alpha = isDead ? deadAlpha : 1f;
        }
    }
}
