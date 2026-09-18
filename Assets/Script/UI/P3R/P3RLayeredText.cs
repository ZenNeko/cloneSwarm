using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// หัวเรื่องใหญ่ที่ทำจาก TMP ซ้อนกันหลายใบ — ตัวนี้ทำให้ทุกใบพูดข้อความเดียวกัน
    ///
    /// ═══ ทำไมต้องซ้อนหลายใบ ═══
    ///
    /// หน้าตาแบบ P3R (ตัวทึบ + เส้นโครงเหลื่อม + เงานูน) ทำด้วย TMP ใบเดียวไม่ได้:
    /// `outlineWidth` ให้ได้แค่ขอบรอบตัวอักษรตรงกลาง ไม่ใช่สำเนาที่เหลื่อมออกไป
    /// และ **ตัวอักษรกลวงจริงก็ทำตรงๆ ไม่ได้** — ตั้ง face เป็น alpha 0 แล้ว
    /// vertex alpha ของ TMP จะคูณทับ outline ไปด้วย ขอบจางหายตามกัน
    ///
    /// ═══ ทำไมต้องมี component นี้ ═══
    ///
    /// คำเดียวกันถูกเก็บไว้ n ที่ · แก้ที่เดียวแล้วอีกสองใบค้างคำเก่า ซึ่งเห็นเป็น
    /// เงาของคำเก่าโผล่อยู่ใต้คำใหม่ — อาการที่หาต้นเหตุยากผิดกับความง่ายของมัน
    /// ตัวนี้ทำให้ `source` เป็นใบเดียวที่ถือข้อความจริง ที่เหลือคือเงาของมัน
    ///
    /// **ทำงานตอนรันเท่านั้น ไม่แตะอะไรใน Editor** — ข้อความของหัวเรื่องเป็นงาน
    /// จัดวางที่คนตั้งไว้ในซีน (`LevelUpUI` เลิกเขียนทับแล้วด้วยเหตุผลเดียวกัน)
    /// ถ้าแก้คำในซีนแล้วยังไม่ตามกัน ให้แก้ที่ `source` ใบเดียวแล้วกด Play
    ///
    /// มิเรอร์ใน `LateUpdate` ด้วยการเทียบสตริง ไม่ใช่ subscribe event —
    /// เป็นท่าเดียวกับที่ `P3RMenuItem` ใช้เฝ้าข้อความของตัวเองอยู่แล้ว
    /// (ของแบบนี้เปลี่ยนไม่กี่ครั้งต่อรอบ การเทียบสตริงหนึ่งครั้งต่อเฟรมถูกกว่า
    ///  การผูกกับ `TMPro_EventManager.TEXT_CHANGED_EVENT` ซึ่งยิงทุกครั้งที่
    ///  **ข้อความไหนก็ได้ในเกม** เปลี่ยน)
    ///
    /// ═══ ลำดับการวาด ═══
    ///
    /// uGUI วาดตามลำดับพี่น้อง ลูกคนหลังทับลูกคนก่อน — เรียงใน Hierarchy ให้
    /// เงาอยู่บนสุด (วาดก่อน) แล้วไล่ลงมาหาใบที่ต้องอยู่หน้าสุด
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RLayeredText : MonoBehaviour
    {
        [Tooltip("ใบที่ถือข้อความจริง — ตัวที่โค้ดอื่นเขียนใส่")]
        public TextMeshProUGUI source;

        [Tooltip("ใบที่ต้องพูดตาม (เงา · เส้นโครง) — สีกับตำแหน่งเป็นของใครของมัน")]
        public TextMeshProUGUI[] followers;

        private string last;

        private void OnEnable() => Mirror(force: true);

        private void LateUpdate()
        {
            if (source == null) return;
            if (source.text == last) return;
            Mirror(force: false);
        }

        private void Mirror(bool force)
        {
            if (source == null) return;
            if (!force && source.text == last) return;

            last = source.text;
            if (followers == null) return;

            foreach (var f in followers)
            {
                if (f == null) continue;
                f.text = last;
            }
        }
    }
}
