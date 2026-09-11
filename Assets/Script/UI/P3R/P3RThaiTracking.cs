using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// เฝ้าช่องข้อความหนึ่งช่อง แล้วปิดการถ่างระยะตัวอักษรเองเมื่อข้อความกลายเป็นภาษาไทย
    ///
    /// ═══ ปัญหาที่ตัวนี้เกิดมาแก้ ═══
    ///
    /// <see cref="P3RText"/> ตัดสินใจได้เฉพาะ **ตอนที่ถูกเรียก** — builder เรียกมันตอนสร้างซีน
    /// ซึ่งตอนนั้นข้อความยังเป็นตัวอย่างภาษาอังกฤษ มันจึงใส่ระยะถ่างให้ตามแบบ
    /// พอเกมรันแล้วมีคนเขียนชื่อที่แปลแล้วลงไป (`CharacterData.DisplayName` ·
    /// `"ทองไม่พอ — ต้องการ 1,000 G"`) ระยะถ่างยังค้างอยู่จากตอนสร้าง
    ///
    /// จุดที่เขียนทับมีหลายสิบที่ กระจายอยู่ในสคริปต์ที่ไม่ได้เป็นของงาน P3R
    /// (`CharacterSelectUI` · `MapCardUI` · `TalentTileUI` …) การไล่แก้ทุกจุดให้เรียก
    /// `P3RText.SetTextAndTracking` แปลว่าต้องจำกฎนี้ตลอดไปทุกครั้งที่มีคนเขียนโค้ดใหม่
    /// ซึ่งเป็นกฎที่ลืมง่ายและพังเงียบ
    ///
    /// ตัวนี้ย้ายการตัดสินใจมาไว้ที่ **ตัวช่องเอง** — ใครจะเขียนข้อความยังไงก็ได้
    /// ระยะถ่างจะถูกคิดใหม่ให้ตรงกับข้อความจริงเสมอ
    ///
    /// ═══ ทำไมใช้ LateUpdate ไม่ใช่ event ═══
    ///
    /// TMP ไม่มี event "ข้อความเปลี่ยน" ต่อตัวให้ subscribe · ที่มีคือ
    /// `TMPro_EventManager.TEXT_CHANGED_EVENT` ซึ่งเป็น global และยิง **หลัง** จัดหน้าเสร็จ
    /// การไปแก้ระยะถ่างตรงนั้นทำให้ต้องจัดหน้าใหม่ซึ่งยิง event ซ้ำ เสี่ยงวนไม่จบ
    ///
    /// เทียบสตริงใน LateUpdate เป็นการเทียบ reference ก่อน (string interning) แล้วค่อย
    /// เทียบเนื้อ — ถูกกว่าการจัดหน้าใหม่หลายเท่า และแก้ก่อนเฟรมถูกวาดพอดี
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class P3RThaiTracking : MonoBehaviour
    {
        [Tooltip("ระยะถ่างที่ตั้งใจไว้สำหรับข้อความละติน — ภาษาไทยจะได้ 0 เสมอ\n" +
                 "ปล่อย 0 ไว้แล้วมันจะอ่านค่าที่ตั้งอยู่ตอน Awake มาใช้เอง")]
        public float latinSpacing;

        private TMP_Text label;
        private string   lastText;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();

            // ค่าที่ builder ใส่ไว้คือค่าที่ตั้งใจ — เก็บไว้ก่อนที่ใครจะมาเขียนทับ
            if (Mathf.Approximately(latinSpacing, 0f))
                latinSpacing = label.characterSpacing;

            lastText = null;      // บังคับให้คิดรอบแรกเสมอ
        }

        private void OnEnable() => lastText = null;

        private void LateUpdate()
        {
            if (label == null) return;

            string now = label.text;
            if (ReferenceEquals(now, lastText) || now == lastText) return;

            lastText = now;
            P3RText.SetTracking(label, latinSpacing);
        }
    }
}
