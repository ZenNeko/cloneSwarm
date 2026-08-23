using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แหล่งความจริงเดียวของลุคเมนูสไตล์ Persona 3 Reload
    ///
    /// ทุกค่าที่ตาเห็นอยู่ในไฟล์นี้ไฟล์เดียว — เปลี่ยนที่นี่แล้วทั้งเมนูเปลี่ยนตาม
    /// ห้ามฮาร์ดโค้ดสี/ฟอนต์/จังหวะลงใน component ตัวอื่น ไม่งั้นจะต้องไล่แก้หลายที่
    ///
    /// สร้าง asset: Assets > Create > Clone Swarm > UI > P3R Theme
    /// หรือปล่อยให้ Tools > Clone Swarm > Build P3R Main Menu Scene สร้างให้อัตโนมัติ
    /// </summary>
    [CreateAssetMenu(fileName = "P3RTheme", menuName = "Clone Swarm/UI/P3R Theme")]
    public class P3RTheme : ScriptableObject
    {
        // ═══════════════════════════════════════════════════════════════════
        // PALETTE — สามสีจบ ห้ามมีสีที่สี่
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Palette ────────────────────────────")]
        [Tooltip("แถบทึบหลังรายการที่ถูกเลือก — น้ำเงินเข้มอิ่ม ไม่ใช่ฟ้า")]
        public Color barColor = new Color32(0x18, 0x24, 0xD8, 0xFF);

        [Tooltip("รายการปกติ — ขาวล้วน")]
        public Color textNormal = Color.white;

        [Tooltip("รายการที่ถูกเลือก — ขาวเหมือนกัน ต่างกันที่แถบข้างหลัง")]
        public Color textSelected = Color.white;

        [Tooltip("รายการที่กดไม่ได้ — เทาอมน้ำตาล กลืนพื้นหลัง")]
        public Color textDisabled = new Color32(0x8C, 0x86, 0x78, 0xFF);

        [Tooltip("เส้นขอบตัวอักษรตอนถูกเลือก — 0 = ไม่มีขอบ")]
        [Range(0f, 0.4f)] public float selectedOutline = 0.12f;

        // ═══════════════════════════════════════════════════════════════════
        // TYPE — P3R พึ่งตัวหนังสือมากกว่าพึ่งสี ข้อนี้สำคัญที่สุด
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Type ───────────────────────────────")]
        [Tooltip("ฟอนต์รายการเมนู — ควรเป็นน้ำหนักหนักสุดที่มี (Sarabun-ExtraBold SDF)")]
        public TMP_FontAsset font;

        public float fontSize = 84f;

        [Tooltip("บีบตัวอักษรให้แคบลงด้วยการย่อ scale.x\n" +
                 "TMP ไม่มี condensed จริง นี่คือวิธีปลอมที่ใกล้ที่สุด · 1 = ไม่บีบ")]
        [Range(0.55f, 1f)] public float horizontalScale = 0.82f;

        [Tooltip("ระยะห่างตัวอักษร (%) — P3R ชิดมาก ค่าติดลบคือชิดขึ้น")]
        public float characterSpacing = -4f;

        [Tooltip("ระยะห่างระหว่างรายการ (px) วัดจากกึ่งกลางถึงกึ่งกลาง")]
        public float rowPitch = 96f;

        // ═══════════════════════════════════════════════════════════════════
        // SELECTION BAR
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Selection Bar ──────────────────────")]
        [Tooltip("แถบสูงกี่ px — ควรพอดี cap height ไม่ใช่ทั้งบรรทัด")]
        public float barHeight = 64f;

        [Tooltip("แถบยื่นเลยตัวอักษรไปทางซ้ายกี่ px")]
        public float barExtendLeft = 320f;

        [Tooltip("แถบยื่นเลยขอบขวาของรายการไปกี่ px — ตั้งเท่ากับ marginRight ของ builder (130)\n" +
                 "แถบจะไปจบพอดีขอบจอ · ใส่มากกว่านั้นคือปล่อยให้เลือดออกนอกจอแบบ P3R")]
        public float barBleedRight = 130f;

        [Tooltip("เลื่อนแถบขึ้น/ลงเทียบกับกึ่งกลางบรรทัด — ปรับให้พอดีตากับฟอนต์ที่ใช้")]
        public float barOffsetY = 4f;

        // ═══════════════════════════════════════════════════════════════════
        // MOTION — กระชากแล้วหยุด ห้ามนุ่ม
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Motion ─────────────────────────────")]
        [Tooltip("ระยะที่รายการไถลเข้ามาจากทางขวาตอนเปิดหน้า")]
        public float introSlideDistance = 420f;

        [Tooltip("รายการถัดไปเริ่มช้ากว่าตัวก่อนหน้ากี่วินาที")]
        public float introStagger = 0.045f;

        [Tooltip("รายการหนึ่งใช้เวลาเข้าที่กี่วินาที")]
        public float introDuration = 0.26f;

        [Tooltip("แถบกางออกกี่วินาทีตอนย้ายการเลือก — สั้นกว่า 0.12 จะดูกระด้าง ยาวกว่า 0.2 จะดูอืด")]
        public float barTweenDuration = 0.14f;

        [Tooltip("ความแรงของการเด้งเกินแล้วดีดกลับ · 0 = ไม่เด้ง · 1.7 คือค่ามาตรฐาน ease-out-back")]
        [Range(0f, 3f)] public float overshoot = 1.7f;

        // ═══════════════════════════════════════════════════════════════════
        // SFX
        // ═══════════════════════════════════════════════════════════════════
        [Header("── SFX ────────────────────────────────")]
        [Tooltip("เสียงตอนเลื่อนขึ้น/ลง — ต้องสั้นและแห้ง ไม่มีหาง")]
        public AudioClip moveSfx;
        public AudioClip confirmSfx;
        public AudioClip cancelSfx;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;

        // ═══════════════════════════════════════════════════════════════════
        // EASING
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ease-out-back — วิ่งเลยเป้าแล้วดีดกลับ นี่คือจังหวะของ P3R
        /// ต่างจาก SmoothDamp ที่ CarouselBase ใช้ (ค่อยๆ ชะลอเข้าเป้า ไม่เคยเลย)
        /// </summary>
        public float EaseOutBack(float t)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float p  = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
