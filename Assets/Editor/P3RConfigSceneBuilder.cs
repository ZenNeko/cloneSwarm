using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ CONFIG ตาม design handoff จอที่ 7
    /// เมนู: Tools > Clone Swarm > Build P3R Config Scene
    ///
    /// **จอนี้เป็นรีสกินล้วน — ไม่มีตรรกะใหม่เลย**
    /// <c>SettingsMenuUI</c> มีครบอยู่แล้วทั้ง slider สามตัว ระดับกราฟิก ภาษา
    /// ปุ่มคืนค่าเริ่มต้น และปุ่มกลับ · builder ตัวนี้แค่ประกอบหน้าตาแล้วต่อสายเข้าไป
    ///
    /// **ปุ่มแบ่งช่อง vs dropdown — ทำไมถึงได้ทั้งสองอย่าง**
    /// แบบวาดระดับกราฟิกกับภาษาเป็นปุ่มแบ่งช่อง แต่โค้ดเป็น <c>TMP_Dropdown</c>
    /// ที่ซีน `MenuScene` ต่อสายไว้แล้ว · การเปลี่ยน field เป็นปุ่มจะทำให้สายในซีนขาดเงียบๆ
    /// จึงเก็บ dropdown ไว้ (ซ่อนด้วย CanvasGroup alpha 0) แล้วให้
    /// <see cref="P3RSegmentedControl"/> เป็นคนขับ — ได้หน้าตาตามแบบ โดยตรรกะเดิมไม่ถูกแตะ
    /// </summary>
    public static class P3RConfigSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_Config.unity";

        // ── layout ที่ 1920x1080 ────────────────────────────────────────────
        private const float PanelW  = 1120f;
        private const float PanelH  = 812f;
        private const float PadX    = 56f;
        private const float RowW    = PanelW - PadX * 2f;   // 1008
        private const float LabelW  = 200f;
        private const float TrackX  = 224f;
        private const float TrackW  = 540f;
        private const float TrackH  = 10f;
        private const float RowH    = 56f;

        // ── สีเฉพาะจอนี้ ────────────────────────────────────────────────────
        private static readonly Color Backdrop  = InkDeep;
        private static readonly Color PanelBg   = new Color32(0x0D, 0x12, 0x26, 0xFF);
        // ผสมล่วงหน้าเป็นสีทึบ — ดูเหตุผลที่ P3RBuilderKit.Over()
        private static readonly Color TrackBg   = Lift(PanelBg, 0.10f);
        private static readonly Color SegNormal = Lift(PanelBg, 0.06f);
        private static readonly Color SegHover  = Lift(PanelBg, 0.14f);

        [MenuItem("Tools/Clone Swarm/Build P3R Config Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "CONFIG", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) ปุ่มระดับกราฟิก/ภาษาสร้างตอนรัน — ในซีนจะเห็นแค่แม่แบบหนึ่งช่องที่ปิดไว้\n" +
                "     กด Play เพื่อดูของจริง (รายการภาษาโผล่หลัง Localization init เสร็จ)\n" +
                "  2) dropdown สองตัวซ่อนอยู่ใต้ CanvasGroup alpha 0 — อย่าลบ มันคือตัวที่ถือตรรกะ\n" +
                "  3) ลาก sprite ลายเส้น 115° ใส่ Scanlines แล้วเปิด GameObject (ตอนนี้ปิดไว้)");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;

            var backdrop = NewImage("Backdrop", root, Backdrop);
            Stretch(backdrop.rectTransform);

            // ลายเส้นทับ 115° — ปิดไว้เพราะ Image ที่ไม่มี sprite จะเป็นสีทึบเต็มจอ
            // ซึ่งผิดกว่าไม่มีเลย · ลาก sprite ใส่แล้วค่อยเปิด
            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.04f));
            Stretch(scan.rectTransform);
            scan.gameObject.SetActive(false);

            var panel = NewImage("ConfigPanel", root, PanelBg);
            Center(panel.rectTransform, PanelW, PanelH);
            AccentBar(panel.rectTransform, Primary);
            AddBorder(panel.rectTransform, "PanelBorder", 1f, HairLine);

            var ui = panel.gameObject.AddComponent<SettingsMenuUI>();
            var prt = panel.rectTransform;

            BuildHeader(prt);

            // ── AUDIO ───────────────────────────────────────────────────────
            Section(prt, "AUDIO", 128f);
            var master = SliderRow(prt, "Master", 180f, 0.8f);
            var music  = SliderRow(prt, "Music",  248f, 0.55f);
            var sfx    = SliderRow(prt, "SFX",    316f, 0.7f);
            ui.masterSlider = master.slider; ui.masterValueText = master.value;
            ui.musicSlider  = music.slider;  ui.musicValueText  = music.value;
            ui.sfxSlider    = sfx.slider;    ui.sfxValueText    = sfx.value;

            // ── GRAPHICS ────────────────────────────────────────────────────
            Section(prt, "GRAPHICS", 400f);
            ui.qualityDropdown = SegmentRow(prt, "Quality", 452f, 132f,
                                            new[] { "Low", "Medium", "High", "Ultra" });

            // ── LANGUAGE ────────────────────────────────────────────────────
            Section(prt, "LANGUAGE", 536f);
            // รายการภาษาเติมเองตอนรันจาก Locale ที่มีจริง — ที่ใส่ไว้เป็นแค่ของโชว์ในซีน
            ui.languageDropdown = SegmentRow(prt, "Locale", 588f, 168f,
                                             new[] { "English", "ไทย" });

            // ── FOOTER ──────────────────────────────────────────────────────
            var divider = NewImage("FooterDivider", prt, FaintLine);
            var drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.sizeDelta = new Vector2(-PadX * 2f, 1f);
            drt.anchoredPosition = new Vector2(0f, -672f);

            ui.resetDefaultsButton = FooterButton(prt, "ResetDefaults", "RESET DEFAULTS",
                                                  left: true, filled: false, tint: Orange);
            ui.backButton          = FooterButton(prt, "Back", "BACK",
                                                  left: false, filled: true, tint: Primary);
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildHeader(RectTransform panel)
        {
            var title = NewText("Title", panel, "CONFIG", 54f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(title, 0.06f);
            TopLeft(title.rectTransform, PadX, 40f, 460f, 64f);

            var hint = NewMono("EscHint", panel, "ESC TO GO BACK", 16f, 0.24f,
                               TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.42f));
            TopRight(hint.rectTransform, PadX, 56f, 380f, 32f);

            var rule = NewImage("HeaderRule", panel, HairLine);
            var rrt = rule.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(-PadX * 2f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -112f);
        }

        /// <summary>หัวข้อหมวด — ป้าย mono เล็กพร้อมขีดน้ำเงินสั้นๆ นำหน้า</summary>
        private static void Section(RectTransform panel, string text, float y)
        {
            var tick = NewImage($"Tick_{text}", panel, Primary);
            TopLeft(tick.rectTransform, PadX, y + 6f, 4f, 18f);

            var t = NewMono($"Section_{text}", panel, text, 17f, 0.28f,
                            TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.55f));
            TopLeft(t.rectTransform, PadX + 16f, y, 420f, 30f);
        }

        private readonly struct SliderRefs
        {
            public readonly Slider slider;
            public readonly TextMeshProUGUI value;
            public SliderRefs(Slider s, TextMeshProUGUI v) { slider = s; value = v; }
        }

        /// <summary>
        /// แถวสไลเดอร์ · รางเทา เติมน้ำเงิน ไม่มีหัวจับ (ตามแบบ)
        ///
        /// <c>handleRect</c> ปล่อย null ได้ — Slider จะใช้ fill area เป็นพื้นที่ลากแทน
        /// พื้นรางต้องรับ raycast ไม่งั้นลากไม่ได้
        /// </summary>
        private static SliderRefs SliderRow(RectTransform panel, string name, float y, float preview)
        {
            var row = NewRect($"Row_{name}", panel);
            TopLeft(row, PadX, y, RowW, RowH);

            var label = NewText($"{name}Label", row, name, 24f, TextAlignmentOptions.MidlineLeft,
                                new Color(1f, 1f, 1f, 0.86f));
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(LabelW, RowH);
            lrt.anchoredPosition = Vector2.zero;

            // ── ตัว Slider ─────────────────────────────────────────────────
            var sliderRt = NewRect($"{name}Slider", row);
            sliderRt.anchorMin = sliderRt.anchorMax = new Vector2(0f, 0.5f);
            sliderRt.pivot = new Vector2(0f, 0.5f);
            sliderRt.sizeDelta = new Vector2(TrackW, RowH);
            sliderRt.anchoredPosition = new Vector2(TrackX, 0f);

            var bg = NewImage("Background", sliderRt, TrackBg);
            bg.raycastTarget = true;                       // พื้นที่รับการลาก
            var bgrt = bg.rectTransform;
            bgrt.anchorMin = new Vector2(0f, 0.5f); bgrt.anchorMax = new Vector2(1f, 0.5f);
            bgrt.pivot = new Vector2(0.5f, 0.5f);
            bgrt.sizeDelta = new Vector2(0f, TrackH); bgrt.anchoredPosition = Vector2.zero;

            var fillArea = NewRect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, TrackH); fillArea.anchoredPosition = Vector2.zero;

            var fill = NewImage("Fill", fillArea, Primary);
            Stretch(fill.rectTransform);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            // ไม่มีสถานะ hover/pressed บนราง ตามแบบ — และ ColorTint จะไปทับสีรางที่ตั้งไว้
            slider.transition    = Selectable.Transition.None;
            slider.targetGraphic = bg;
            slider.fillRect      = fill.rectTransform;
            slider.handleRect    = null;
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = 0f;
            slider.maxValue      = 1f;
            slider.wholeNumbers  = false;
            slider.SetValueWithoutNotify(preview);   // ของโชว์ในซีน · ตอนรัน sync จาก SoundManager
            // ห้ามให้โฟกัสค้าง — ไม่งั้น Space/Enter ครั้งถัดไปไปโดนสไลเดอร์ที่ไม่ได้มองอยู่
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;

            var value = NewMono($"{name}Value", row, $"{Mathf.RoundToInt(preview * 100)}%",
                                24f, 0.06f, TextAlignmentOptions.MidlineRight);
            var vrt = value.rectTransform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.sizeDelta = new Vector2(140f, RowH);
            vrt.anchoredPosition = Vector2.zero;

            return new SliderRefs(slider, value);
        }

        /// <summary>
        /// แถวปุ่มแบ่งช่อง — คืน dropdown ที่ซ่อนไว้ เพราะนั่นคือตัวที่ <c>SettingsMenuUI</c> ต่อสาย
        /// ปุ่มจริงสร้างตอนรันโดย <see cref="P3RSegmentedControl"/> จาก options ของ dropdown
        /// </summary>
        private static TMP_Dropdown SegmentRow(RectTransform panel, string name, float y,
                                               float segW, string[] previewOptions)
        {
            var row = NewRect($"Row_{name}", panel);
            TopLeft(row, PadX, y, RowW, RowH);

            var label = NewText($"{name}Label", row, name, 24f, TextAlignmentOptions.MidlineLeft,
                                new Color(1f, 1f, 1f, 0.86f));
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(LabelW, RowH);
            lrt.anchoredPosition = Vector2.zero;

            // ── dropdown ตัวจริง ซ่อนไว้ ────────────────────────────────────
            // GameObject ต้อง active อยู่ ไม่งั้น OnEnable ของมันไม่วิ่งและ callback ตาย
            // จึงซ่อนด้วย CanvasGroup แทนการ SetActive(false)
            var hidden = NewRect($"{name}Dropdown", row);
            hidden.anchorMin = hidden.anchorMax = new Vector2(0f, 0.5f);
            hidden.pivot = new Vector2(0f, 0.5f);
            hidden.sizeDelta = new Vector2(10f, 10f);
            hidden.anchoredPosition = new Vector2(TrackX, 0f);
            var cg = hidden.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            var dropdown = hidden.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(previewOptions));
            var dnav = dropdown.navigation; dnav.mode = Navigation.Mode.None; dropdown.navigation = dnav;

            // ── ที่วางปุ่ม + แม่แบบหนึ่งช่อง ─────────────────────────────────
            var container = NewRect($"{name}Segments", row);
            container.anchorMin = container.anchorMax = new Vector2(0f, 0.5f);
            container.pivot = new Vector2(0f, 0.5f);
            container.sizeDelta = new Vector2(RowW - TrackX, RowH);
            container.anchoredPosition = new Vector2(TrackX, 0f);

            var template = BuildSegmentTemplate(container, segW);

            var control = row.gameObject.AddComponent<P3RSegmentedControl>();
            control.source           = dropdown;
            control.segmentTemplate  = template;
            control.segmentContainer = container;
            control.segmentWidth     = segW;
            control.segmentHeight    = 44f;
            control.segmentSpacing   = 8f;
            control.labelTracking    = 8f;

            // ปุ่มตัวอย่างในซีน — ตอนรัน Rebuild() ล้างทิ้งแล้วสร้างใหม่จาก options จริง
            // ถ้าไม่วางไว้ ซีนกับ PNG จะเห็นแค่ชื่อแถวเปล่าๆ ตรวจงานส่วนที่ตรงกับแบบที่สุดไม่ได้เลย
            for (int i = 0; i < previewOptions.Length; i++)
            {
                var seg = Object.Instantiate(template, container);
                seg.gameObject.SetActive(true);
                seg.name = $"Preview_{i}";

                var srt = (RectTransform)seg.transform;
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot     = new Vector2(0f, 0.5f);
                srt.sizeDelta = new Vector2(segW, control.segmentHeight);
                srt.anchoredPosition = new Vector2(i * (segW + control.segmentSpacing), 0f);

                seg.Init(control, i, previewOptions[i], control.labelTracking);
                seg.SetSelected(i == 0);
            }

            return dropdown;
        }

        /// <summary>แม่แบบปุ่มหนึ่งช่อง · ปิดไว้ · ตอนรันถูก Instantiate ตามจำนวนตัวเลือกจริง</summary>
        private static P3RSegmentButton BuildSegmentTemplate(RectTransform parent, float w)
        {
            var seg = NewRect("SegmentTemplate", parent);
            seg.anchorMin = seg.anchorMax = new Vector2(0f, 0.5f);
            seg.pivot = new Vector2(0f, 0.5f);
            seg.sizeDelta = new Vector2(w, 44f);
            seg.anchoredPosition = Vector2.zero;

            var bg = NewImage("Bg", seg, SegNormal);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;              // ตัวรับคลิกของ P3RSegmentButton
            Shear(bg);                            // พื้นเอียง · ตัวหนังสือไม่เอียง ตามแบบ

            var label = NewText("Label", seg, "OPTION", 19f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var btn = seg.gameObject.AddComponent<P3RSegmentButton>();
            btn.background   = bg;
            btn.label        = label;
            btn.selectedBg   = Primary;
            btn.normalBg     = SegNormal;
            btn.hoverBg      = SegHover;
            btn.selectedText = Color.white;
            btn.normalText   = new Color(1f, 1f, 1f, 0.66f);

            seg.gameObject.SetActive(false);
            return btn;
        }

        /// <summary>
        /// ปุ่มท้ายจอ — <c>filled</c> = พื้นทึบ · ไม่งั้นเป็นกรอบเปล่าสีตาม <paramref name="tint"/>
        /// ใช้ <c>Button</c> จริงเพราะ <c>SettingsMenuUI</c> ประกาศ field เป็น Button
        /// แต่ปิด navigation ทิ้ง เพื่อไม่ให้โฟกัสค้างแล้วโดน Space ยิงซ้ำทีหลัง
        /// </summary>
        private static Button FooterButton(RectTransform panel, string name, string text,
                                           bool left, bool filled, Color tint)
        {
            const float w = 320f, h = 62f, y = 62f;

            var root = NewRect($"Btn_{name}", panel);
            if (left) BottomLeft(root, PadX, y - 20f, w, h);
            else      BottomRight(root, PadX, y - 20f, w, h);

            var bg = NewImage("Bg", root, filled ? tint : Over(tint, PanelBg, 0.10f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            if (!filled) AddBorder(root, "Border", 1.5f, tint, shear: 9f);

            var label = NewMono("Label", root, text, 21f, 0.14f, TextAlignmentOptions.Center,
                                filled ? Color.white : tint);
            Stretch(label.rectTransform);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }
    }
}
