using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// เฝ้าช่องข้อความหนึ่งช่อง แล้วปรับ **ระยะถ่าง** กับ **ขนาด** ให้เองเมื่อข้อความเป็นภาษาไทย
    ///
    /// ═══ ปัญหาที่ตัวนี้เกิดมาแก้ ═══
    ///
    /// <see cref="P3RText"/> ตัดสินใจได้เฉพาะ **ตอนที่ถูกเรียก** — builder เรียกมันตอนสร้างซีน
    /// ซึ่งตอนนั้นข้อความยังเป็นตัวอย่างภาษาอังกฤษ มันจึงใส่ระยะถ่างให้ตามแบบ
    /// พอเกมรันแล้วมีคนเขียนชื่อที่แปลแล้วลงไป (`CharacterData.DisplayName` ·
    /// `"ทองไม่พอ — ต้องการ 1,000 G"`) ค่าจากตอนสร้างยังค้างอยู่
    ///
    /// จุดที่เขียนทับมีหลายสิบที่ กระจายอยู่ในสคริปต์ที่ไม่ได้เป็นของงาน P3R
    /// (`CharacterSelectUI` · `MapCardUI` · `TalentTileUI` …) การไล่แก้ทุกจุดแปลว่า
    /// ต้องจำกฎนี้ตลอดไปทุกครั้งที่มีคนเขียนโค้ดใหม่ ซึ่งเป็นกฎที่ลืมง่ายและพังเงียบ
    ///
    /// ตัวนี้ย้ายการตัดสินใจมาไว้ที่ **ตัวช่องเอง** — ใครจะเขียนข้อความยังไงก็ได้
    ///
    /// ═══ สองอย่างที่ปรับให้ ═══
    ///
    /// **ระยะถ่าง** — สระและวรรณยุกต์ไทยเป็นอักขระไม่มีความกว้าง ถ่างแล้วมันหลุด
    /// จากพยัญชนะ · ไทยได้ 0 เสมอ
    ///
    /// **ขนาด** — ไทยไม่มีตัวพิมพ์ใหญ่ ความสูงที่ตาเห็นจึงเท่าตัวพิมพ์เล็กของละติน
    /// วางข้างคำ ALL-CAPS ขนาดเท่ากันแล้วไทยดูเบากว่าเสมอ (เห็นชัดที่สุดตรงปุ่ม
    /// `English` / `ไทย` ที่อยู่ติดกันในจอ Config) · ขยายขึ้นตาม <see cref="thaiFontScale"/>
    ///
    /// ═══ ทำไมใช้ LateUpdate ไม่ใช่ event ═══
    ///
    /// TMP ไม่มี event "ข้อความเปลี่ยน" ต่อตัวให้ subscribe · ที่มีคือ
    /// `TMPro_EventManager.TEXT_CHANGED_EVENT` ซึ่งเป็น global และยิง **หลัง** จัดหน้าเสร็จ
    /// การไปแก้ขนาด/ระยะถ่างตรงนั้นทำให้ต้องจัดหน้าใหม่ซึ่งยิง event ซ้ำ เสี่ยงวนไม่จบ
    ///
    /// เทียบสตริงใน LateUpdate เทียบ reference ก่อน (string interning) แล้วค่อยเทียบเนื้อ
    /// ถูกกว่าการจัดหน้าใหม่หลายเท่า และแก้ทันก่อนเฟรมถูกวาด
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class P3RThaiTracking : MonoBehaviour
    {
        [Tooltip("ระยะถ่างที่ตั้งใจไว้สำหรับข้อความละติน — ภาษาไทยจะได้ 0 เสมอ")]
        public float latinSpacing;

        [Tooltip("ขนาดตัวอักษรสำหรับข้อความละติน · 0 = อ่านค่าที่ตั้งอยู่ตอน Awake มาใช้")]
        public float latinFontSize;

        [Tooltip("ตัวคูณขนาดตอนเป็นภาษาไทย · 1 = ไม่ชดเชย")]
        [Range(1f, 1.3f)] public float thaiFontScale = 1.08f;

        private TMP_Text label;
        private string   lastText;
        private bool     cached;

        private void Awake() => EnsureCached();

        /// <summary>
        /// หาป้ายกับเก็บค่าตั้งต้น — เรียกได้ทั้งจาก Awake และจาก Apply
        ///
        /// ═══ ทำไมไม่ทำใน Awake อย่างเดียว ═══
        ///
        /// `Apply()` เป็น public และเอกสารบอกว่าเรียกเองได้ · ของเดิมขึ้นต้นด้วย
        /// `if (label == null) return;` ซึ่งแปลว่าใครเรียกก่อน Awake จะได้ **ความเงียบ**
        /// ไม่ใช่ผลลัพธ์ และไม่มีอะไรบอกว่าไม่ได้ทำอะไรเลย
        ///
        /// ตอนรันจริงไม่มีอาการเพราะ Awake วิ่งทันทีที่ Instantiate · ที่เจอคือตอน
        /// เครื่องมือ edit mode โคลนป้ายไปเรนเดอร์แผ่นพิสูจน์ แล้วได้ภาพของป้ายที่
        /// ยังถือระยะถ่างของละตินอยู่ — วรรณยุกต์ลอยออกจากพยัญชนะทั้งแผ่น
        /// ซึ่งดูเหมือนฟอนต์พัง ทั้งที่เกมจริงไม่เป็น
        ///
        /// เก็บค่าครั้งเดียวด้วย `cached` — เรียกซ้ำไม่ทับค่าที่ตัวเองเพิ่งเขียนลงไป
        /// (ถ้าเก็บใหม่ทุกครั้ง latinSpacing จะกลายเป็น 0 ทันทีที่เจอข้อความไทยหนึ่งครั้ง
        /// แล้วข้อความละตินหลังจากนั้นจะไม่มีระยะถ่างอีกเลย)
        /// </summary>
        private void EnsureCached()
        {
            if (cached) return;

            label = GetComponent<TMP_Text>();
            if (label == null) return;      // ยังไม่มีป้าย — ลองใหม่รอบหน้า

            // ค่าที่ builder ใส่ไว้คือค่าที่ตั้งใจ — เก็บไว้ก่อนที่ใครจะมาเขียนทับ
            if (Mathf.Approximately(latinSpacing, 0f))  latinSpacing  = label.characterSpacing;
            if (latinFontSize <= 0f)                    latinFontSize = label.fontSize;

            cached   = true;
            lastText = null;      // บังคับให้คิดรอบแรกเสมอ
        }

        private void OnEnable() => lastText = null;

        private void LateUpdate()
        {
            if (label == null) return;

            string now = label.text;
            if (ReferenceEquals(now, lastText) || now == lastText) return;
            lastText = now;

            Apply();
        }

        /// <summary>คิดใหม่ทันที — เรียกเองได้เมื่อเปลี่ยนข้อความแล้วอยากเห็นผลในเฟรมเดียวกัน</summary>
        public void Apply()
        {
            EnsureCached();
            if (label == null) return;

            bool thai = P3RText.HasThai(label.text);

            label.characterSpacing = thai ? 0f : latinSpacing;

            // ไม่แตะขนาดเมื่อเปิด auto-size — TMP คุมขนาดเองอยู่แล้ว การไปเขียนทับจะสู้กัน
            if (!label.enableAutoSizing && latinFontSize > 0f)
                label.fontSize = thai ? latinFontSize * thaiFontScale : latinFontSize;
        }
    }
}
