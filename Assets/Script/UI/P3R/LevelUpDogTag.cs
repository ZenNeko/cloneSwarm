using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{

// ── ทำไมคลาสนี้ต้องอยู่ไฟล์ของตัวเอง ────────────────────────────────────────
// Unity ผูก script reference ในซีนให้เฉพาะคลาสที่ **ชื่อตรงกับชื่อไฟล์**
// คลาส MonoBehaviour ตัวที่สองในไฟล์เดียวกันได้ reference ที่ไม่รอดการก๊อปข้ามซีน
// อาการที่เจอ: ย้ายจอ Level Up เข้า SampleScene แล้วช่องในแถบ build กลายเป็น
// m_Script: {fileID: 0} ทั้งหมด — component หายไปเงียบๆ ทั้งที่ GameObject ยังอยู่
// ────────────────────────────────────────────────────────────────────────────


    /// <summary>
    /// ป้าย dog-tag หนึ่งใบ — ที่เก็บ reference ให้ <see cref="LevelUpDogTagUI"/> สั่ง
    /// แยกเป็นคลาสเพราะป้ายถูก clone จาก template ในซีน reference จึงต้องติดกับ GameObject
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelUpDogTag : MonoBehaviour
    {
        [Tooltip("พื้นป้าย")]
        public Image background;
        [Tooltip("กรอบ 2px สี่ด้าน เรียง บน/ล่าง/ซ้าย/ขวา")]
        public Image[] borderEdges = new Image[4];
        [Tooltip("ตัวหนังสือในป้าย (P1 / P2 …)")]
        public TextMeshProUGUI label;
        [Tooltip("ใช้คุม opacity ตอนกะพริบ — ถูกเพิ่มโดย builder")]
        public CanvasGroup group;

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetState(bool picked, Color border, Color textColor)
        {
            foreach (var e in borderEdges)
            {
                if (e == null) continue;
                e.color = border;
            }
            if (label != null) label.color = textColor;
            if (background != null)
                // คนที่เลือกแล้วได้พื้นเขียวจางๆ เพิ่ม เพื่อให้แยกออกแม้มองผ่านๆ
                background.color = picked
                    ? new Color(border.r, border.g, border.b, 0.14f)
                    : new Color(1f, 1f, 1f, 0.05f);
        }

        public void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }
    }
}
