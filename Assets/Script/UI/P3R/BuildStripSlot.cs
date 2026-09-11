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
    /// ช่องของหนึ่งช่องในแถบ build — เป็นแค่ที่เก็บ reference ให้ <see cref="BuildStripUI"/> สั่ง
    ///
    /// แยกเป็นคลาส (ไม่ใช่ struct ในตัวแม่) เพราะช่องถูก clone จาก template ในซีน
    /// การผูก reference จึงต้องอยู่ติดกับ GameObject ไม่ใช่ในลิสต์ของตัวแม่
    ///
    /// **กรอบเส้นประ:** uGUI ไม่มีเส้นประในตัว และโปรเจกต์นี้ไม่มี sprite เส้นประ
    /// จึงทำเป็น <see cref="Image"/> สี่ด้าน แล้วสลับ sprite เป็น texture ลายขีดที่สร้างในโค้ด
    /// (<see cref="DashSprite"/>) ตอน runtime · texture ที่สร้างในโค้ดไม่ถูก serialize ลงซีน
    /// จึงต้องสร้างใหม่ทุกครั้งที่โหลด — static cache ทำให้ทั้งเกมมีใบเดียว
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildStripSlot : MonoBehaviour
    {
        [Tooltip("พื้นช่อง — สีประเภทที่ 20% alpha")]
        public Image fill;
        [Tooltip("ไอคอนของ — ซ่อนเมื่อไม่มี sprite แล้วโชว์ตัวย่อแทน")]
        public Image icon;
        [Tooltip("เส้นขอบสี่ด้าน เรียง บน/ล่าง/ซ้าย/ขวา")]
        public Image[] borderEdges = new Image[4];
        [Tooltip("ตัวย่อสามตัวอักษร — โชว์เมื่อไม่มีไอคอน")]
        public TextMeshProUGUI abbrevLabel;
        [Tooltip("เลข Lv มุมล่างขวา")]
        public TextMeshProUGUI levelLabel;

        private static Sprite dashSprite;

        public void ShowEntry(BuildStripUI.Entry e, Color typeColor, Color border, Color levelColor)
        {
            if (fill != null)
            {
                fill.enabled = true;
                fill.color   = new Color(typeColor.r, typeColor.g, typeColor.b, 0.20f);
            }

            bool hasIcon = e.icon != null;
            if (icon != null)
            {
                icon.enabled = hasIcon;
                icon.sprite  = e.icon;
                icon.color   = Color.white;
            }
            if (abbrevLabel != null)
            {
                abbrevLabel.enabled = !hasIcon;
                abbrevLabel.text    = e.abbrev;
            }

            if (levelLabel != null)
            {
                levelLabel.enabled = e.level > 0;
                levelLabel.text    = e.level > 0 ? $"Lv{e.level}" : "";
                levelLabel.color   = levelColor;
            }

            SetBorder(border, dashed: false);
        }

        public void ShowEmpty(Color border)
        {
            if (fill != null)        fill.enabled        = false;
            if (icon != null)        icon.enabled        = false;
            if (abbrevLabel != null) abbrevLabel.enabled = false;
            if (levelLabel != null)  levelLabel.enabled  = false;
            SetBorder(border, dashed: true);
        }

        private void SetBorder(Color c, bool dashed)
        {
            var sprite = dashed ? DashSprite() : null;
            foreach (var edge in borderEdges)
            {
                if (edge == null) continue;
                edge.enabled = true;
                edge.color   = c;
                edge.sprite  = sprite;
                // Tiled ทำให้ลายขีดซ้ำตามความยาวจริงของด้าน แทนที่จะยืดขีดเดียวจนเบลอ
                edge.type    = dashed ? Image.Type.Tiled : Image.Type.Simple;
                edge.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>ลายขีด 6 ทึบ / 5 โปร่ง — สร้างครั้งเดียวต่อการรันหนึ่งครั้ง</summary>
        private static Sprite DashSprite()
        {
            if (dashSprite != null) return dashSprite;

            const int period = 11, solid = 6;
            var tex = new Texture2D(period, 1, TextureFormat.RGBA32, false)
            {
                name       = "P3R_DashPattern",
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Repeat,
                hideFlags  = HideFlags.HideAndDontSave
            };
            for (int x = 0; x < period; x++)
                tex.SetPixel(x, 0, x < solid ? Color.white : Color.clear);
            tex.Apply();

            dashSprite = Sprite.Create(tex, new Rect(0, 0, period, 1), new Vector2(0.5f, 0.5f), 1f);
            dashSprite.name      = "P3R_DashSprite";
            dashSprite.hideFlags = HideFlags.HideAndDontSave;
            return dashSprite;
        }
    }
}
