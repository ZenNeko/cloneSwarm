using CloneSwarm.Meta;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ TALENT SHOP ตาม design handoff จอที่ 6
    /// เมนู: Tools > Clone Swarm > Build P3R Talent Shop Scene
    ///
    /// **`RUNS · WINS · BEST` มีอยู่แล้ว** — สเปกเขียนว่า "ต้องเช็คว่ามี getter"
    /// แต่ `TalentShopUI.RefreshAll()` อ่านจาก `SaveManager.Data` แล้วเติม `statsText` เอง
    /// ตั้งแต่แรก · จอนี้แค่ต่อสายให้ตรงที่
    ///
    /// **บั๊กเงินหายที่สเปกเตือนไว้ ไม่จริงแล้ว** — `SaveManager` ติดตั้ง `SaveAutoFlush`
    /// เองผ่าน `[RuntimeInitializeOnLoadMethod]` ซึ่ง flush ตอน quit / pause / เสียโฟกัส
    /// การที่ `TalentShopUI` อยู่บน Canvas จึงเป็นเรื่องของ lifecycle ไม่ใช่เรื่องข้อมูลหาย
    /// </summary>
    public static class P3RTalentShopSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_TalentShop.unity";

        private const float TopBarH  = 84f;
        private const float TabBarH  = 62f;
        private const float ContentY = TopBarH + TabBarH;
        private const float PadX     = 64f;
        private const float RightW   = 620f;
        private const float Gap      = 24f;

        private static readonly Color TopBar  = new Color32(0x08, 0x0B, 0x18, 0xFF);
        private static readonly Color PanelBg = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color TileBg  = new Color32(0x11, 0x18, 0x38, 0xFF);

        // 14 talent ตามที่แบบระบุ
        private static readonly string[] TalentNames =
        {
            "Attack Power", "Max Health", "Armor",      "Crit Chance",
            "Move Speed",   "Haste",      "Area Size",  "Duration",
            "Projectiles",  "Regen",      "Magnet",     "EXP Gain",
            "Gold Gain",    "Revive",
        };

        [MenuItem("Tools/Clone Swarm/Build P3R Talent Shop Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "TALENT SHOP", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) ช่องในซีนเป็นตัวอย่าง — ตอนรัน TalentShopUI.BuildTiles() สร้างใหม่จาก\n" +
                "     TalentData จริงที่ MetaDatabase ถืออยู่\n" +
                "  2) RUNS · WINS · BEST ทำงานอยู่แล้ว อ่านจาก SaveManager.Data ตรงๆ\n" +
                "  3) ป้าย 'หลังซื้อเหลือ …' เป็นฟิลด์ใหม่ afterPurchaseText — ปล่อยว่างได้\n" +
                "  4) ไอคอนยังว่าง — TalentData.icon ของหลายตัวยังไม่ได้ใส่");
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

            var ui = root.gameObject.AddComponent<TalentShopUI>();

            BuildChrome(root, ui);
            BuildGrid(root, ui);
            BuildDetail(root, ui);
        }

        private static void BuildChrome(RectTransform root, TalentShopUI ui)
        {
            var bar = NewImage("TopBar", root, TopBar);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, TopBarH);
            brt.anchoredPosition = Vector2.zero;

            var title = NewMono("Title", brt, "CLONE SWARM", 22f, 0.26f, TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, PadX, 26f, 460f, 34f);

            // RUNS · WINS · BEST — TalentShopUI เติมเองจาก SaveManager.Data
            var stats = NewMono("Stats", brt, "RUNS 12 · WINS 3 · BEST 14:22", 16f, 0.2f,
                                TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            Center(stats.rectTransform, 700f, 30f);
            ui.statsText = stats;

            var gold = NewMono("Gold", brt, "8,420 G", 22f, 0.10f,
                               TextAlignmentOptions.MidlineRight, Gold);
            TopRight(gold.rectTransform, PadX, 26f, 240f, 34f);
            ui.goldText = gold;

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

                bool active = i == 3;
                var bg = NewImage("Bg", tab, active ? Primary : Lift(InkDeep, 0.05f));
                Stretch(bg.rectTransform);
                Shear(bg);

                // แท็บต้องกดได้จริง — P3RScreenWirer เอา P3RTabJump มาใส่ตอนย้ายลงซีนจริง
                // ในซีนต้นแบบมันยังกดไม่ไปไหนเพราะไม่มี TabBar ให้ไป ซึ่งถูกต้องแล้ว
                bg.raycastTarget = true;
                var tabBtn = tab.gameObject.AddComponent<Button>();
                tabBtn.targetGraphic = bg;
                var tabNav = tabBtn.navigation; tabNav.mode = Navigation.Mode.None;
                tabBtn.navigation = tabNav;

                var label = NewMono("Label", tab, names[i], 16f, 0.18f, TextAlignmentOptions.Center,
                                    active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                Stretch(label.rectTransform);
                x += 186f;
            }

            // ปุ่ม BACK มุมขวาบนของแถบแท็บ
            var back = NewRect("Btn_Back", tabs);
            TopRight(back, PadX, 10f, 180f, 42f);
            var bbg = NewImage("Bg", back, Lift(InkDeep, 0.08f));
            Stretch(bbg.rectTransform);
            bbg.raycastTarget = true;
            Shear(bbg);
            var blabel = NewMono("Label", back, "BACK", 16f, 0.2f, TextAlignmentOptions.Center,
                                 new Color(1f, 1f, 1f, 0.75f));
            Stretch(blabel.rectTransform);
            var bbtn = back.gameObject.AddComponent<Button>();
            bbtn.targetGraphic = bbg;
            var nav = bbtn.navigation; nav.mode = Navigation.Mode.None; bbtn.navigation = nav;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildGrid(RectTransform root, TalentShopUI ui)
        {
            float h = RefH - ContentY - 36f;
            float w = RefW - PadX * 2f - RightW - Gap;

            var panel = NewImage("GridPanel", root, PanelBg);
            TopLeft(panel.rectTransform, PadX, ContentY + 12f, w, h);

            var tiles = NewRect("Tiles", panel.rectTransform);
            Inset(tiles, 20f, 20f, 20f, 20f);
            ui.tilesContainer = tiles;

            float tileW = (w - 40f - 16f) * 0.5f;

            // **ต้องมี GridLayoutGroup** — TalentShopUI.BuildTiles() Instantiate ช่องเข้ามา
            // เฉยๆ ไม่ได้วางตำแหน่งให้ · ไม่มี layout = ช่องจริงทั้ง 14 ไปกองทับกันที่จุดเดียว
            var grid = tiles.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(tileW, 82f);
            grid.spacing         = new Vector2(16f, 10f);
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment  = TextAnchor.UpperLeft;

            var template = BuildTileTemplate(tiles);
            ui.tileTemplate = template.gameObject;

            // ── ตัวอย่างในซีน 14 ช่อง ────────────────────────────────────────
            // GridLayoutGroup จัดตำแหน่งให้เอง ที่ตั้งไว้ในลูปเป็นแค่ขนาด
            for (int i = 0; i < TalentNames.Length; i++)
            {
                var tile = Object.Instantiate(template, tiles);
                tile.gameObject.SetActive(true);
                tile.name = $"Sample_{i}";

                var rt = (RectTransform)tile.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(tileW, 82f);
                rt.anchoredPosition = new Vector2((i % 2) * (tileW + 16f), -(i / 2) * 92f);

                var label = tile.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if (label != null) P3RText.SetTextAndTracking(label, TalentNames[i], 8f);

                bool selected = i == 0;
                var bg = tile.transform.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = selected ? Over(Primary, TileBg, 0.55f) : TileBg;

                var frame = tile.transform.Find("SelectedFrame");
                if (frame != null) frame.gameObject.SetActive(selected);

                // เม็ดเลเวล — ตัวอย่างให้เห็นทั้งแบบซื้อแล้วและยังไม่ซื้อ
                var pips = tile.transform.Find("Pips");
                if (pips != null)
                {
                    int filled = i == 0 ? 3 : (i % 4 == 0 ? 1 : 0);
                    for (int p = 0; p < pips.childCount; p++)
                    {
                        var img = pips.GetChild(p).GetComponent<Image>();
                        if (img != null) img.color = p < filled ? Teal : Lift(TileBg, 0.10f);
                    }
                }
            }

            template.gameObject.SetActive(false);
        }

        private static RectTransform BuildTileTemplate(RectTransform parent)
        {
            var rt = NewRect("TalentTileTemplate", parent);
            rt.sizeDelta = new Vector2(560f, 82f);

            var bg = NewImage("Bg", rt, TileBg);
            Stretch(bg.rectTransform);

            var icon = NewImage("Icon", rt, Over(Orange, TileBg, 0.75f));
            TopLeft(icon.rectTransform, 18f, 18f, 46f, 46f);
            Shear(icon);

            var name = NewMono("Name", rt, "Attack Power", 19f, 0.08f,
                               TextAlignmentOptions.MidlineLeft);
            TopLeft(name.rectTransform, 80f, 16f, 320f, 30f);

            // ── เม็ดบอกเลเวล 5 เม็ด ────────────────────────────────────────
            var pips = NewRect("Pips", rt);
            TopLeft(pips, 80f, 48f, 200f, 18f);
            for (int i = 0; i < 5; i++)
            {
                var pip = NewImage($"Pip{i}", pips, Lift(TileBg, 0.10f));
                var prt = pip.rectTransform;
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
                prt.pivot     = new Vector2(0f, 0.5f);
                prt.sizeDelta = new Vector2(22f, 5f);
                prt.anchoredPosition = new Vector2(i * 28f, 0f);
            }

            var cost = NewMono("Cost", rt, "400", 20f, 0.06f,
                               TextAlignmentOptions.MidlineRight, Gold);
            TopRight(cost.rectTransform, 20f, 26f, 160f, 32f);

            var frame = NewRect("SelectedFrame", rt);
            Stretch(frame);
            AddBorder(frame, "Border", 2f, Primary);
            frame.gameObject.SetActive(false);

            var hit = NewImage("HitArea", rt, new Color(0f, 0f, 0f, 0f));
            Stretch(hit.rectTransform);
            hit.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

            var tile = rt.gameObject.AddComponent<TalentTileUI>();
            tile.background    = bg;
            tile.iconImage     = icon;
            tile.nameText      = name;
            tile.costText      = cost;
            tile.tileButton    = btn;
            tile.pipsContainer = pips;
            tile.selectedFrame = frame.gameObject;

            return rt;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildDetail(RectTransform root, TalentShopUI ui)
        {
            float h = RefH - ContentY - 36f;

            var panel = NewImage("DetailPanel", root, PanelBg);
            TopRight(panel.rectTransform, PadX, ContentY + 12f, RightW, h);
            var prt = panel.rectTransform;
            AccentBar(prt, Orange);

            var icon = NewImage("Icon", prt, Over(Orange, PanelBg, 0.8f));
            TopLeft(icon.rectTransform, 32f, 32f, 84f, 84f);
            Shear(icon);
            ui.detailIcon = icon;

            var name = NewText("Name", prt, "Attack Power", 38f, TextAlignmentOptions.MidlineLeft);
            TopLeft(name.rectTransform, 136f, 34f, 440f, 46f);
            ui.detailName = name;

            var category = NewMono("Category", prt, "OFFENCE", 15f, 0.28f,
                                   TextAlignmentOptions.MidlineLeft, Orange);
            TopLeft(category.rectTransform, 136f, 82f, 300f, 26f);
            ui.detailCategoryText = category;

            var desc = NewText("Description", prt,
                               "เพิ่มความเสียหายของอาวุธและสกิลทุกชิ้นเป็นเปอร์เซ็นต์คงที่ " +
                               "คิดก่อนคริติคอล",
                               19f, TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.68f));
            Wrap(desc);
            TopLeft(desc.rectTransform, 32f, 148f, RightW - 64f, 72f);
            ui.detailDescription = desc;

            var rule = NewImage("Rule", prt, FaintLine);
            var rrt = rule.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(-64f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -238f);

            // ── Lv 3 / 5 + เม็ด ─────────────────────────────────────────────
            var level = NewMono("Level", prt, "Lv 3 / 5", 22f, 0.12f,
                                TextAlignmentOptions.MidlineLeft);
            TopLeft(level.rectTransform, 32f, 262f, 200f, 32f);
            ui.detailLevelText = level;

            var pips = NewRect("Pips", prt);
            TopLeft(pips, 220f, 272f, 260f, 16f);
            for (int i = 0; i < 5; i++)
            {
                var pip = NewImage($"Pip{i}", pips, i < 3 ? Teal : Lift(PanelBg, 0.10f));
                var pr = pip.rectTransform;
                pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
                pr.pivot     = new Vector2(0f, 0.5f);
                pr.sizeDelta = new Vector2(34f, 6f);
                pr.anchoredPosition = new Vector2(i * 42f, 0f);
            }

            // ── NOW › NEXT ──────────────────────────────────────────────────
            //
            // สามช่องนี้วางเรียงกันในแนวนอนบนความกว้าง 556px ที่แผงมี — ทุกช่องจึงต้อง
            // **กว้างตายตัวและไม่ทับกัน** และตัวหนังสือต้องตัดด้วย `…` ไม่ใช่ล้นออกไป
            //
            // เคยพังเพราะ builder ตั้งความกว้างจากตัวอย่าง `+9%` แต่ของจริงที่ TalentShopUI
            // เขียนลงไปคือ `+0% PICKUP RADIUS` ซึ่งยาวกว่าช่องเท่าตัว · NOW เลยไหลไปทับ NEXT
            // ตัวเลขสองชุดซ้อนกันอ่านไม่ออกทั้งคู่ · แก้ที่ต้นทางด้วย FormatValueShort
            // แล้วกันซ้ำอีกชั้นที่นี่ด้วยความกว้างตายตัว + Ellipsis
            const float ValW  = 184f;   // 32 + 184 + 40(arrow) + 184 = 440 · เหลือขอบอีก 116
            const float ArrowW = 40f;
            const float NextX = 32f + ValW + ArrowW;

            var nowHead = NewMono("NowHead", prt, "NOW", 13f, 0.26f,
                                  TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.4f));
            TopLeft(nowHead.rectTransform, 32f, 320f, 120f, 22f);

            var now = NewMono("NowValue", prt, "+9%", 30f, 0.04f, TextAlignmentOptions.MidlineLeft);
            TopLeft(now.rectTransform, 32f, 342f, ValW, 40f);
            now.overflowMode = TextOverflowModes.Ellipsis;
            ui.detailCurrentValue = now;

            var arrow = NewText("Arrow", prt, "›", 34f, TextAlignmentOptions.Center,
                                new Color(1f, 1f, 1f, 0.4f));
            TopLeft(arrow.rectTransform, 32f + ValW, 342f, ArrowW, 40f);
            ui.detailArrow = arrow.gameObject;

            var next = NewMono("NextValue", prt, "+12%", 30f, 0.04f,
                               TextAlignmentOptions.MidlineLeft, Teal);
            TopLeft(next.rectTransform, NextX, 342f, ValW, 40f);
            next.overflowMode = TextOverflowModes.Ellipsis;
            ui.detailNextValue = next;

            // หัวข้อ NEXT เป็น **ลูกของช่องค่า** ไม่ใช่พี่น้องกัน — TalentShopUI ซ่อนช่องค่า
            // ตอนตันเลเวล (`detailNextValue.gameObject.SetActive(false)`) หัวข้อจึงต้องหายไปด้วย
            // ไม่งั้นจะเหลือคำว่า NEXT ลอยอยู่เหนือที่ว่าง ทั้งที่ไม่มีเลเวลถัดไปแล้ว
            var nextHead = NewMono("Head", next.rectTransform, "NEXT", 13f, 0.26f,
                                   TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.4f));
            var nhrt = nextHead.rectTransform;
            nhrt.anchorMin = new Vector2(0f, 1f); nhrt.anchorMax = new Vector2(0f, 1f);
            nhrt.pivot = new Vector2(0f, 0f);
            nhrt.sizeDelta = new Vector2(120f, 22f);
            nhrt.anchoredPosition = Vector2.zero;

            // หมายเหตุตันเลเวลไปอยู่ **ช่องของ NEXT** ไม่ใช่ทับ NOW
            // ของเดิมวางที่ x=32 y=342 ซึ่งเป็นพิกัดเดียวกับ NowValue เป๊ะ · ตอนตันจึงเห็น
            // ข้อความเขียวซ้อนตัวเลขขาวอยู่ที่เดียวกัน · ผู้เล่นยังควรเห็นค่าปัจจุบันของตัวเอง
            var maxed = NewMono("MaxedNote", prt, "ถึงเลเวลสูงสุดแล้ว", 18f, 0f,
                                TextAlignmentOptions.MidlineLeft, Teal);
            TopLeft(maxed.rectTransform, NextX, 342f, RightW - 64f - NextX + 32f, 40f);
            ui.detailMaxedNote = maxed.gameObject;
            maxed.gameObject.SetActive(false);

            var perLevel = NewMono("PerLevel", prt, "DAMAGE +3% / LEVEL", 14f, 0.22f,
                                   TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.42f));
            TopLeft(perLevel.rectTransform, 32f, 392f, RightW - 64f, 24f);
            ui.detailPerLevelText = perLevel;

            // ── ปุ่มซื้อ ─────────────────────────────────────────────────────
            var buyRoot = NewRect("BuyButton", prt);
            BottomLeft(buyRoot, 32f, 88f, RightW - 64f, 72f);

            var buyBg = NewImage("Bg", buyRoot, ui.buyAffordableColor);
            Stretch(buyBg.rectTransform);
            buyBg.raycastTarget = true;
            Shear(buyBg);

            var buyLabel = NewMono("Label", buyRoot, "UPGRADE", 22f, 0.18f,
                                   TextAlignmentOptions.MidlineLeft, InkDeep);
            var blrt = buyLabel.rectTransform;
            blrt.anchorMin = new Vector2(0f, 0f); blrt.anchorMax = new Vector2(1f, 1f);
            blrt.offsetMin = new Vector2(32f, 0f); blrt.offsetMax = new Vector2(-32f, 0f);

            var buyCost = NewMono("Cost", buyRoot, "400", 24f, 0.06f,
                                  TextAlignmentOptions.MidlineRight, InkDeep);
            var bcrt = buyCost.rectTransform;
            bcrt.anchorMin = new Vector2(0f, 0f); bcrt.anchorMax = new Vector2(1f, 1f);
            bcrt.offsetMin = new Vector2(32f, 0f); bcrt.offsetMax = new Vector2(-32f, 0f);
            ui.buyCostText = buyCost;

            var buyBtn = buyRoot.gameObject.AddComponent<Button>();
            buyBtn.targetGraphic = buyBg;
            var bnav = buyBtn.navigation; bnav.mode = Navigation.Mode.None; buyBtn.navigation = bnav;
            ui.buyButton = buyBtn;

            var after = NewMono("AfterPurchase", prt, "หลังซื้อเหลือ 8,020 G", 15f, 0f,
                                TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.45f));
            BottomLeft(after.rectTransform, 32f, 52f, RightW - 64f, 26f);
            ui.afterPurchaseText = after;
        }
    }
}
