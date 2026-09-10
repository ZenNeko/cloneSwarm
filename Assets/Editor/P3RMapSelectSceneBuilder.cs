using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ MAP SELECT ตาม design handoff จอที่ 5
    /// เมนู: Tools > Clone Swarm > Build P3R Map Select Scene
    ///
    /// **สามอย่างที่แบบวาดไว้เหมือนมีแล้ว แต่เกมยังไม่มี**
    ///   ตัวคูณ `ENEMY HP ×1.0` / `GOLD ×1.0` — `DifficultyTier` เป็น enum เปล่า
    ///     ไม่มีตัวคูณให้อ่าน จึงโชว์ `—` · การโชว์ ×1.0 จะเป็นการบอกผู้เล่น
    ///     ว่ามีระบบที่ยังไม่มีอยู่จริง
    ///   `LOCKED ×2` — ยังไม่มีระบบปลดล็อกแมพ จึงไม่โชว์ตัวเลข
    ///   `OPEN FIELD` — ยังไม่มีฟิลด์ประเภทแมพใน `MapData`
    ///
    /// **ชื่อระดับความยากใช้ชื่อ enum จริง** — แบบเขียน NORMAL/HARD/NIGHTMARE
    /// แต่ในเกมคือ Easy/Normal/Hard/Savage/Epic · โชว์ตามแบบแล้วผู้เล่นจะเลือก
    /// NIGHTMARE แล้วได้ Savage ซึ่งไม่ตรงกับที่เห็นทุกที่อื่น
    /// </summary>
    public static class P3RMapSelectSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_MapSelect.unity";

        private const float TopBarH  = 84f;
        private const float TabBarH  = 62f;
        private const float ContentY = TopBarH + TabBarH;
        private const float BottomH  = 96f;
        private const float PadX     = 64f;
        private const float PreviewH = 452f;

        private static readonly Color TopBar  = new Color32(0x08, 0x0B, 0x18, 0xFF);
        private static readonly Color PanelBg = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color CardBg  = new Color32(0x11, 0x18, 0x38, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Map Select Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "MAP SELECT", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) ชิปตัวคูณโชว์ '—' เพราะ DifficultyTier ไม่มีตัวคูณให้อ่านจริง\n" +
                "     ถ้าจะทำระบบนี้ ต้องตัดสินก่อนว่าตัวคูณอยู่ที่ไหน (enum · SO · WaveConfig)\n" +
                "  2) ป้าย LOCKED ปิดไว้ — ยังไม่มีระบบปลดล็อกแมพ\n" +
                "  3) โรสเตอร์แมพมีตัวเดียว (MapData_Arena01) การ์ดในซีนจึงเป็นของโชว์\n" +
                "     ตอนรัน MapCarousel สร้างใหม่จาก LobbyUI.maps จริง\n" +
                "  4) RUN 15 MIN เป็นสำเนาของ GameTimeline.mainBossTimeMin ที่อยู่ในซีนเกม\n" +
                "     อ่านข้ามซีนไม่ได้ · แก้ค่าที่ซีนเกมแล้วต้องมาแก้ที่ MapSelectUI ด้วย");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;

            var backdrop = NewImage("Backdrop", root, InkDeep);
            Stretch(backdrop.rectTransform);

            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.04f));
            Stretch(scan.rectTransform);
            scan.gameObject.SetActive(false);

            var ui = root.gameObject.AddComponent<MapSelectUI>();

            BuildChrome(root);
            BuildPreview(root, ui);
            BuildInfo(root, ui);
            BuildCards(root, ui);
            BuildBottomBar(root);
        }

        private static void BuildChrome(RectTransform root)
        {
            var bar = NewImage("TopBar", root, TopBar);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, TopBarH);
            brt.anchoredPosition = Vector2.zero;

            var title = NewMono("Title", brt, "CLONE SWARM", 22f, 0.26f, TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, PadX, 26f, 460f, 34f);

            var gold = NewMono("Gold", brt, "8,420 G", 22f, 0.10f,
                               TextAlignmentOptions.MidlineRight, Gold);
            TopRight(gold.rectTransform, PadX, 26f, 240f, 34f);

            // แถบแท็บ — MAP เป็นตัว active
            var tabs = NewRect("TabBar", root);
            tabs.anchorMin = new Vector2(0f, 1f); tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(0f, TabBarH);
            tabs.anchoredPosition = new Vector2(0f, -TopBarH);

            string[] names = { "LOBBY", "MAP", "CHARACTER", "SHOP" };
            float x = PadX;
            for (int i = 0; i < names.Length; i++)
            {
                var tab = NewRect($"Tab_{names[i]}", tabs);
                tab.anchorMin = tab.anchorMax = new Vector2(0f, 0.5f);
                tab.pivot = new Vector2(0f, 0.5f);
                tab.sizeDelta = new Vector2(176f, 42f);
                tab.anchoredPosition = new Vector2(x, 0f);

                bool active = i == 1;
                var bg = NewImage("Bg", tab, active ? Primary : Lift(InkDeep, 0.05f));
                Stretch(bg.rectTransform);
                Shear(bg);

                var label = NewMono("Label", tab, names[i], 16f, 0.18f, TextAlignmentOptions.Center,
                                    active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                Stretch(label.rectTransform);
                x += 186f;
            }
        }

        private static void BuildPreview(RectTransform root, MapSelectUI ui)
        {
            var box = NewImage("Preview", root, Lift(InkDeep, 0.05f));
            TopLeft(box.rectTransform, PadX, ContentY + 12f, RefW - PadX * 2f, PreviewH);
            ui.detailPreview = box;

            var hint = NewMono("Hint", box.rectTransform, "MAP PREVIEW", 17f, 0.3f,
                               TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.16f));
            Stretch(hint.rectTransform);

            // ม่านล่างให้ตัวหนังสือที่วางทับอ่านออกเมื่อมีภาพจริง
            var fade = NewImage("BottomFade", box.rectTransform, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.55f));
            var frt = fade.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.sizeDelta = new Vector2(0f, 120f);
            frt.anchoredPosition = Vector2.zero;
        }

        private static void BuildInfo(RectTransform root, MapSelectUI ui)
        {
            float y = ContentY + 12f + PreviewH + 22f;

            var type = NewMono("MapType", root, "OPEN FIELD", 16f, 0.28f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.55f));
            TopLeft(type.rectTransform, PadX, y, 320f, 26f);
            ui.mapTypeLabel = type;

            var run = NewMono("RunLength", root, $"RUN {ui.runLengthMinutes} MIN", 16f, 0.28f,
                              TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.55f));
            TopLeft(run.rectTransform, PadX + 250f, y, 320f, 26f);
            ui.runLengthLabel = run;

            // ── ชิปตัวคูณ ชิดขวา ────────────────────────────────────────────
            // โชว์ "—" ไม่ใช่ "×1.0" — เกมยังไม่มีระบบตัวคูณตามความยาก
            ui.enemyHpMultLabel = MultChip(root, "EnemyHpMult", "ENEMY HP", PadX + 250f, y);
            ui.goldMultLabel    = MultChip(root, "GoldMult",    "GOLD",     PadX,        y);

            var name = NewText("MapName", root, "ARENA 01", 60f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(name, 0.02f);
            TopLeft(name.rectTransform, PadX, y + 30f, 760f, 72f);
            ui.detailName = name;

            // ── ปุ่มเลือกความยาก ชิดขวา ─────────────────────────────────────
            var diffRoot = NewRect("DifficultyRow", root);
            TopRight(diffRoot, PadX, y + 44f, 790f, 44f);

            var template = SegmentTemplate(diffRoot, 148f);
            var selector = diffRoot.gameObject.AddComponent<P3RDifficultySelector>();
            selector.segmentTemplate  = template;
            selector.segmentContainer = diffRoot;
            selector.segmentWidth     = 148f;
            selector.segmentHeight    = 42f;
            selector.segmentSpacing   = 8f;
            ui.difficultySelector = selector;

            // ปุ่มตัวอย่างในซีน — ตอนรัน Rebuild() ล้างแล้วสร้างใหม่จาก tiers จริง
            var tiers = new[] { "EASY", "NORMAL", "HARD", "SAVAGE", "EPIC" };
            for (int i = 0; i < tiers.Length; i++)
            {
                var seg = Object.Instantiate(template, diffRoot);
                seg.gameObject.SetActive(true);
                seg.name = $"Preview_{tiers[i]}";
                var rt = (RectTransform)seg.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(148f, 42f);
                rt.anchoredPosition = new Vector2(i * 156f, 0f);
                seg.Init(selector, i, tiers[i], 12f);
                seg.SetSelected(i == 1);
            }

            var desc = NewText("Description", root,
                               "พื้นราบไม่มีที่กำบัง ศัตรูเข้ามาจากทุกขอบของสนาม " +
                               "การขยับจึงสำคัญกว่าการเลือกตำแหน่งยืน",
                               21f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.62f));
            Wrap(desc);                                   // ตัดคำไทยให้ถูก
            TopLeft(desc.rectTransform, PadX, y + 106f, 980f, 68f);
            ui.detailDesc = desc;
        }

        /// <summary>
        /// ชิปตัวคูณ — ป้ายชื่อ + ค่า
        /// ค่าเป็น `—` จนกว่าจะมีระบบจริง · ห้ามใส่ ×1.0 เพราะนั่นคือการอ้างว่ามีระบบแล้ว
        /// </summary>
        private static TextMeshProUGUI MultChip(RectTransform root, string name, string label,
                                                float xFromRight, float y)
        {
            var chip = NewRect($"Chip_{name}", root);
            TopRight(chip, xFromRight, y - 6f, 236f, 38f);

            var bg = NewImage("Bg", chip, Lift(InkDeep, 0.06f));
            Stretch(bg.rectTransform);
            Shear(bg);

            var head = NewMono("Label", chip, label, 14f, 0.22f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f));
            var hrt = head.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.offsetMin = new Vector2(16f, 0f); hrt.offsetMax = new Vector2(-16f, 0f);

            var value = NewMono("Value", chip, "—", 17f, 0.06f,
                                TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.75f));
            var vrt = value.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = new Vector2(16f, 0f); vrt.offsetMax = new Vector2(-16f, 0f);

            return value;
        }

        private static void BuildCards(RectTransform root, MapSelectUI ui)
        {
            float y = RefH - BottomH - 176f;

            var rowRoot = NewRect("CardsRow", root);
            TopLeft(rowRoot, PadX, y, RefW - PadX * 2f, 148f);

            var cards = NewRect("Cards", rowRoot);
            Stretch(cards);
            ui.cardsContainer = cards;

            // ── แม่แบบการ์ด ─────────────────────────────────────────────────
            var template = BuildCardTemplate(cards);
            ui.cardTemplate = template.gameObject;

            // การ์ดตัวอย่าง — โรสเตอร์จริงมีแมพเดียว ตอนรัน MapCarousel สร้างใหม่เอง
            string[] sample = { "ARENA 01", "FOUNDRY", "CRYO VAULT", "THE SPRAWL" };
            for (int i = 0; i < sample.Length; i++)
            {
                var card = Object.Instantiate(template, cards);
                card.gameObject.SetActive(true);
                card.name = $"Sample_{i}";
                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 148f);
                rt.anchoredPosition = new Vector2(i * 316f, 0f);

                var label = card.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (label != null) P3RText.SetTextAndTracking(label, sample[i], 14f);

                var bg = card.transform.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = i == 0 ? Over(Primary, CardBg, 0.55f) : CardBg;

                var sel = card.transform.Find("SelectedBorder");
                if (sel != null) sel.gameObject.SetActive(i == 0);
            }

            template.gameObject.SetActive(false);

            // ── ป้ายแมพที่ล็อก — ปิดไว้เพราะยังไม่มีระบบปลดล็อก ─────────────
            var locked = NewMono("LockedCount", root, "LOCKED", 16f, 0.24f,
                                 TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.35f));
            TopRight(locked.rectTransform, PadX, y - 34f, 300f, 26f);
            ui.lockedLabel = locked;
            locked.gameObject.SetActive(false);
        }

        private static RectTransform BuildCardTemplate(RectTransform parent)
        {
            var rt = NewRect("MapCardTemplate", parent);
            rt.sizeDelta = new Vector2(300f, 148f);

            var bg = NewImage("Bg", rt, CardBg);
            Stretch(bg.rectTransform);

            var preview = NewImage("Preview", rt, Lift(CardBg, 0.05f));
            var prt = preview.rectTransform;
            prt.anchorMin = new Vector2(0f, 1f); prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(0f, 96f);
            prt.anchoredPosition = Vector2.zero;

            var name = NewMono("Name", rt, "ARENA 01", 17f, 0.14f,
                               TextAlignmentOptions.MidlineLeft);
            BottomLeft(name.rectTransform, 16f, 8f, 268f, 40f);

            var selected = NewRect("SelectedBorder", rt);
            Stretch(selected);
            AddBorder(selected, "Border", 2f, Primary);
            selected.gameObject.SetActive(false);

            var card = rt.gameObject.AddComponent<MapCardUI>();
            card.bgImage      = bg;
            card.previewImage = preview;
            card.nameText     = name;

            return rt;
        }

        private static P3RSegmentButton SegmentTemplate(RectTransform parent, float w)
        {
            var seg = NewRect("SegmentTemplate", parent);
            seg.anchorMin = seg.anchorMax = new Vector2(0f, 0.5f);
            seg.pivot = new Vector2(0f, 0.5f);
            seg.sizeDelta = new Vector2(w, 42f);

            var bg = NewImage("Bg", seg, Lift(InkDeep, 0.06f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            var label = NewText("Label", seg, "TIER", 16f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            var btn = seg.gameObject.AddComponent<P3RSegmentButton>();
            btn.background   = bg;
            btn.label        = label;
            btn.selectedBg   = Primary;
            btn.normalBg     = Lift(InkDeep, 0.06f);
            btn.hoverBg      = Lift(InkDeep, 0.14f);
            btn.selectedText = Color.white;
            btn.normalText   = new Color(1f, 1f, 1f, 0.6f);

            seg.gameObject.SetActive(false);
            return btn;
        }

        private static void BuildBottomBar(RectTransform root)
        {
            var bar = NewRect("BottomBar", root);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BottomH);
            bar.anchoredPosition = Vector2.zero;

            BarButton(bar, "Back", "BACK", left: true, x: PadX, w: 220f,
                      filled: false, tint: new Color(1f, 1f, 1f, 0.7f));

            var hint = NewMono("Hint", bar, "A / D  BROWSE  ·  ENTER  CONFIRM", 15f, 0.22f,
                               TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.4f));
            Center(hint.rectTransform, 700f, 28f);

            BarButton(bar, "Confirm", "CONFIRM MAP", left: false, x: PadX, w: 300f,
                      filled: true, tint: Primary);
        }

        private static Button BarButton(RectTransform bar, string name, string text,
                                        bool left, float x, float w, bool filled, Color tint)
        {
            const float h = 54f;
            var root = NewRect($"Btn_{name}", bar);
            if (left) BottomLeft(root, x, 21f, w, h);
            else      BottomRight(root, x, 21f, w, h);

            var bg = NewImage("Bg", root, filled ? tint : Over(tint, InkDeep, 0.10f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            if (!filled) AddBorder(root, "Border", 1.5f, tint, shear: 9f);

            var label = NewMono("Label", root, text, 18f, 0.16f, TextAlignmentOptions.Center,
                                filled ? Color.white : tint);
            Stretch(label.rectTransform);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }
    }
}
