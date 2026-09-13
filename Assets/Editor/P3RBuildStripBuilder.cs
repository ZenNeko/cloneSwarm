using CloneSwarm.UI.P3R;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แถบ "ของที่ถืออยู่" สองแถว (WEAPONS / PASSIVES) — **ที่เดียวของทั้งเกม**
    ///
    /// ═══ ทำไมต้องแยกออกมา ═══
    ///
    /// แถบนี้เคยมีสองแบบที่ไม่เกี่ยวกันเลย — จอ Level Up ใช้ <see cref="BuildStripUI"/>
    /// (แถวพื้นเข้ม เส้นเน้นซ้าย 6px ช่อง 62px กรอบเส้นประตอนว่าง) ส่วน HUD ตอนเล่น
    /// ใช้ <c>WeaponStatHUD</c> วาดชิปเฉือนมุมพร้อมชื่อเต็ม ซึ่งเป็นคนละหน้าตากันสิ้นเชิง
    /// ทั้งที่แสดง **ของชุดเดียวกัน** ผู้เล่นจึงต้องเรียนรู้สองภาษาสำหรับข้อมูลอันเดียว
    ///
    /// ตอนนี้ทั้งสองจอเรียกตัวนี้ตัวเดียว แก้หน้าตาที่นี่ที่เดียวแล้วเปลี่ยนพร้อมกัน
    /// — ไม่ใช่ "ทำให้เหมือนกัน" ซึ่งอีกสามเดือนก็จะต่างกันอีก
    ///
    /// ═══ ตัวเรียกเป็นคนวางตำแหน่ง ═══
    ///
    /// สร้างเสร็จ root จะยังไม่มี anchor ที่ตั้งใจ — จอ Level Up วางล่างขวา
    /// ส่วน HUD วางล่างกลาง (ล่างขวาของ HUD เป็นช่องสกิลกับแถบ charge อยู่แล้ว)
    /// ใช้ <see cref="PlaceBottomRight"/> / <see cref="PlaceBottomCenter"/> ต่อได้เลย
    /// </summary>
    public static class P3RBuildStripBuilder
    {
        // ── ขนาดตามแบบ (design handoff §"แถบ build") ────────────────────────
        public const float StripWidth = 600f;
        public const float RowHeight  = 90f;
        public const float RowGap     = 14f;
        public const float SlotSize   = 62f;

        private const float PadX   = 20f;
        private const float LabelW = 132f;
        private const float EdgeW  = 6f;

        private static readonly Color RowBg  = new Color32(0x0A, 0x0E, 0x1E, 0xB8);
        private static readonly Color Blue   = new Color32(0x40, 0x73, 0xD9, 0xFF);
        private static readonly Color Green  = new Color32(0x4D, 0xA6, 0x59, 0xFF);

        /// <summary>ความสูงรวมของแถบ — ตัวเรียกใช้ตั้ง sizeDelta</summary>
        public static float TotalHeight => RowHeight * 2f + RowGap;

        /// <summary>
        /// สร้างแถบครบชุดใต้ <paramref name="parent"/> แล้วคืน component ที่ต่อสายแล้ว
        ///
        /// <paramref name="mono"/> คือฟอนต์ป้ายระบบ · <paramref name="display"/> ฟอนต์ทั่วไป
        /// ส่ง null ได้ทั้งคู่ — TMP จะใช้ฟอนต์ปริยายของโปรเจกต์แทน
        /// </summary>
        public static BuildStripUI Build(RectTransform parent, TMP_FontAsset mono,
                                         TMP_FontAsset display, string name = "BuildStrip")
        {
            var root = NewRect(name, parent);
            root.sizeDelta = new Vector2(StripWidth, TotalHeight);

            var strip = root.gameObject.AddComponent<BuildStripUI>();

            strip.weaponSlotArea  = Row(root, "Row_Weapons",  "WEAPONS",  Blue,  0f, mono, display);
            strip.passiveSlotArea = Row(root, "Row_Passives", "PASSIVES", Green,
                                        -(RowHeight + RowGap), mono, display);
            strip.slotTemplate    = SlotTemplate(root, mono, display);
            return strip;
        }

        /// <summary>วางล่างขวาแบบจอ Level Up</summary>
        public static void PlaceBottomRight(BuildStripUI strip, float x, float y)
        {
            var rt = (RectTransform)strip.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>วางล่างกลางแบบ HUD ตอนเล่น</summary>
        public static void PlaceBottomCenter(BuildStripUI strip, float y)
        {
            var rt = (RectTransform)strip.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // ═══════════════════════════════════════════════════════════════════
        // INTERNAL
        // ═══════════════════════════════════════════════════════════════════
        private static RectTransform Row(RectTransform parent, string name, string label,
                                         Color accent, float y,
                                         TMP_FontAsset mono, TMP_FontAsset display)
        {
            var row = NewRect(name, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot     = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowHeight);
            row.anchoredPosition = new Vector2(0f, y);

            var bg = NewImage("Bg", row, RowBg);
            Stretch(bg.rectTransform);

            // เส้นเน้นซ้าย 6px — ลายเซ็นของการ์ดทุกใบในระบบ (design tokens §รูปทรง)
            var edge = NewImage("LeftEdge", row, accent);
            var ert  = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(0f, 1f);
            ert.pivot     = new Vector2(0f, 0.5f);
            ert.sizeDelta = new Vector2(EdgeW, 0f);

            var text = Mono("Label", row, label, 15f, 0.20f, mono, display);
            text.color     = new Color(1f, 1f, 1f, 0.62f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot     = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(LabelW, 26f);
            trt.anchoredPosition = new Vector2(EdgeW + PadX, 0f);

            var area = NewRect("SlotArea", row);
            area.anchorMin = area.anchorMax = new Vector2(0f, 0.5f);
            area.pivot     = new Vector2(0f, 0.5f);
            area.sizeDelta = new Vector2(420f, SlotSize);
            area.anchoredPosition = new Vector2(EdgeW + PadX + LabelW, 0f);
            return area;
        }

        private static BuildStripSlot SlotTemplate(RectTransform parent,
                                                   TMP_FontAsset mono, TMP_FontAsset display)
        {
            var rt = NewRect("Slot_Template", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(SlotSize, SlotSize);

            var slot = rt.gameObject.AddComponent<BuildStripSlot>();

            var fill = NewImage("Fill", rt, new Color(1f, 1f, 1f, 0.08f));
            Stretch(fill.rectTransform);
            slot.fill = fill;

            var icon = NewImage("Icon", rt, Color.white);
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(8f, 8f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);
            // ปิดไว้ก่อน — Image ที่ไม่มี sprite วาดสี่เหลี่ยมทึบ ไม่ได้วาดเปล่า
            icon.enabled = false;
            slot.icon = icon;

            var abbrev = Mono("Abbrev", rt, "BLD", 15f, 0.10f, mono, display);
            abbrev.color     = new Color(1f, 1f, 1f, 0.75f);
            abbrev.alignment = TextAlignmentOptions.Midline;
            Stretch(abbrev.rectTransform);
            slot.abbrevLabel = abbrev;

            var lv = Mono("Level", rt, "Lv3", 15f, 0.02f, mono, display);
            lv.color     = Color.white;
            lv.fontStyle = FontStyles.Bold;
            lv.alignment = TextAlignmentOptions.BottomRight;
            var lrt = lv.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot     = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(-6f, 20f);
            lrt.anchoredPosition = new Vector2(0f, 3f);
            slot.levelLabel = lv;

            slot.borderEdges = Border(rt, "Border", 1f, new Color(1f, 1f, 1f, 0.26f));

            rt.gameObject.SetActive(false);
            return slot;
        }

        // ── helpers ────────────────────────────────────────────────────────
        // เขียนเองแทนการยืมจาก builder ของจอใดจอหนึ่ง — ตัวนี้ถูกเรียกจากสองจอ
        // ผูกกับ helper ของจอไหนก็เท่ากับผูกลำดับการสร้างเข้ากับจอนั้น
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI Mono(string name, Transform parent, string text,
                                            float size, float letterSpacingEm,
                                            TMP_FontAsset mono, TMP_FontAsset display)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text             = text;
            t.fontSize         = size;
            t.raycastTarget    = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;

            var font = mono != null ? mono : display;
            if (font != null) t.font = font;

            // TMP นับ characterSpacing เป็น % ของ em ค่าจึงเป็น em×100
            P3RText.SetTracking(t, letterSpacingEm * 100f);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// กรอบสี่ด้านเรียง บน/ล่าง/ซ้าย/ขวา — ลำดับต้องตรงกับที่
        /// <see cref="BuildStripSlot.borderEdges"/> คาดไว้
        /// </summary>
        private static Image[] Border(RectTransform target, string name, float thickness, Color color)
        {
            var root = NewRect(name, target);
            Stretch(root);

            return new[]
            {
                Edge("Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness)),
                Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness)),
                Edge("Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f)),
                Edge("Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f)),
            };

            Image Edge(string edgeName, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
            {
                var img = NewImage(edgeName, root, color);
                var rt  = img.rectTransform;
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
                rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
                return img;
            }
        }
    }
}
