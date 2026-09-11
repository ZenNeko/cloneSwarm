using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ LOBBY ตาม design handoff จอที่ 4
    /// เมนู: Tools > Clone Swarm > Build P3R Lobby Scene
    ///
    /// **แถวปาร์ตี้เป็น prefab ไม่ใช่ของฝังในซีน** — `Assets/Prefab/UI/P3R/LobbyPartyRow.prefab`
    /// ซีนต้นแบบย้ายลงเกมไม่ได้ แต่ prefab ย้ายได้ · อะไรที่มีโอกาสได้ใช้จริงต้องออกมาทางนี้
    ///
    /// **ชิปสกิลอ่านปุ่มจาก binding จริง** — แบบวาด `Q` / `E` ซึ่งล้าสมัยตั้งแต่ `28154a4f`
    /// ที่ย้ายสกิลไปคลิกซ้าย/ขวา · <see cref="LobbyAbilityChipUI"/> ถาม Input System เอง
    /// </summary>
    public static class P3RLobbySceneBuilder
    {
        private const string ScenePath  = "Assets/GameScenes/Proto_Lobby.unity";
        private const string RowPrefab  = PrefabDir + "/LobbyPartyRow.prefab";
        private const string ChipPrefab = PrefabDir + "/LobbyAbilityChip.prefab";

        // ── layout ที่ 1920x1080 ────────────────────────────────────────────
        private const float TopBarH  = 84f;
        private const float TabBarH  = 62f;
        private const float ContentY = TopBarH + TabBarH;      // 146
        private const float BottomH  = 104f;
        private const float PadX     = 64f;
        private const float ColGap   = 32f;
        private const float ColW     = (RefW - PadX * 2f - ColGap) * 0.5f;   // 880

        private static readonly Color TopBar   = new Color32(0x08, 0x0B, 0x18, 0xFF);
        private static readonly Color PanelBg  = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color RowBg    = new Color32(0x11, 0x18, 0x38, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Lobby Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "LOBBY", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                $"prefab แถวปาร์ตี้/ชิปสกิลเซฟไว้ที่ {PrefabDir}\n" +
                "ขั้นถัดไป:\n" +
                "  1) แถวในซีนเป็นตัวอย่างเฉยๆ — ตอนรัน LobbyUI.RefreshP3RPartyRows() ล้างแล้วสร้างใหม่\n" +
                "  2) ป้ายปุ่มบนชิปสกิลตอนนี้เป็นชื่อช่อง (Q/E) เพราะ Input System ยังไม่ init ในซีน\n" +
                "     กด Play แล้วจะกลายเป็นปุ่มจริงที่ผูกไว้ใน AbilityInputActions\n" +
                "  3) ฟิลด์ P3R บน LobbyUI เป็นของเพิ่ม ปล่อยว่างได้ — ซีน MenuScene เดิมยังใช้\n" +
                "     partyRowTemplate แบบเก่าอยู่ได้โดยไม่ต้องแก้อะไร\n" +
                "  4) ลาก sprite ลายเส้น 115° ใส่ Scanlines แล้วเปิด GameObject (ตอนนี้ปิดไว้)");
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

            var ui = root.gameObject.AddComponent<LobbyUI>();

            BuildTopBar(root, ui);
            BuildTabBar(root);

            var rowPrefab  = BuildPartyRowPrefab();
            var chipPrefab = BuildAbilityChipPrefab();

            BuildLeftColumn(root, chipPrefab);
            BuildRightColumn(root, ui, rowPrefab);
            BuildBottomBar(root, ui);
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTopBar(RectTransform root, LobbyUI ui)
        {
            var bar = NewImage("TopBar", root, TopBar);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, TopBarH);
            brt.anchoredPosition = Vector2.zero;

            var title = NewMono("Title", brt, "CLONE SWARM", 22f, 0.26f,
                                TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, PadX, 26f, 460f, 34f);

            // ── ยอดทอง · รหัสห้อง · ปุ่มคัดลอก ชิดขวา ────────────────────────
            var gold = NewMono("Gold", brt, "8,420 G", 22f, 0.10f,
                               TextAlignmentOptions.MidlineRight, Gold);
            TopRight(gold.rectTransform, PadX + 360f, 26f, 220f, 34f);
            ui.goldText = gold;

            var copyRoot = NewRect("CopyCode", brt);
            TopRight(copyRoot, PadX + 190f, 22f, 140f, 40f);
            var copyBg = NewImage("Bg", copyRoot, Lift(TopBar, 0.10f));
            Stretch(copyBg.rectTransform);
            copyBg.raycastTarget = true;
            Shear(copyBg);
            var copyLabel = NewMono("Label", copyRoot, "COPY", 15f, 0.22f,
                                    TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            Stretch(copyLabel.rectTransform);
            var copyBtn = copyRoot.gameObject.AddComponent<Button>();
            copyBtn.targetGraphic = copyBg;
            var cnav = copyBtn.navigation; cnav.mode = Navigation.Mode.None; copyBtn.navigation = cnav;
            ui.copyCodeButton = copyBtn;

            var code = NewMono("RoomCode", brt, "7K4M2P", 22f, 0.24f,
                               TextAlignmentOptions.MidlineRight);
            TopRight(code.rectTransform, PadX, 26f, 180f, 34f);
            ui.roomCodeLabel = code;

            var rule = NewImage("Rule", brt, FaintLine);
            var rrt = rule.rectTransform;
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 1f); rrt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// แถบแท็บสี่อัน · อันที่ active พื้นน้ำเงินเอียง
        /// จงใจไม่ใส่ component <c>TabBar</c> — มันต้องการ panel จริงต่อแท็บ
        /// ซึ่งซีนต้นแบบไม่มี · ที่นี่สนใจแค่หน้าตา ส่วนการสลับแท็บของจริงอยู่ใน MenuScene แล้ว
        /// </summary>
        private static void BuildTabBar(RectTransform root)
        {
            var bar = NewRect("TabBar", root);
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, TabBarH);
            bar.anchoredPosition = new Vector2(0f, -TopBarH);

            string[] tabs = { "LOBBY", "MAP", "CHARACTER", "SHOP" };
            float x = PadX;
            for (int i = 0; i < tabs.Length; i++)
            {
                float w = 176f;
                var tab = NewRect($"Tab_{tabs[i]}", bar);
                tab.anchorMin = tab.anchorMax = new Vector2(0f, 0.5f);
                tab.pivot     = new Vector2(0f, 0.5f);
                tab.sizeDelta = new Vector2(w, 42f);
                tab.anchoredPosition = new Vector2(x, 0f);

                bool active = i == 0;
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

                var label = NewMono("Label", tab, tabs[i], 16f, 0.18f, TextAlignmentOptions.Center,
                                    active ? Color.white : new Color(1f, 1f, 1f, 0.55f));
                Stretch(label.rectTransform);

                x += w + 10f;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildLeftColumn(RectTransform root, LobbyAbilityChipUI chipPrefab)
        {
            float h = RefH - ContentY - BottomH - 24f;

            var col = NewImage("LeftColumn", root, PanelBg);
            TopLeft(col.rectTransform, PadX, ContentY + 12f, ColW, h);
            AccentBar(col.rectTransform, Primary);

            var crt = col.rectTransform;

            // พอร์เทรต — ปิด Image ไว้เพราะยังไม่มี sprite (Char_Hunter/Gunner ยังไม่มี portrait)
            var portrait = NewImage("Portrait", crt, Lift(PanelBg, 0.06f));
            TopLeft(portrait.rectTransform, 0f, 0f, ColW, 470f);

            var portraitHint = NewMono("PortraitHint", portrait.rectTransform, "PORTRAIT", 15f, 0.3f,
                                       TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.18f));
            Stretch(portraitHint.rectTransform);

            var head = NewMono("Head", crt, "YOUR CHARACTER", 15f, 0.28f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(head.rectTransform, 28f, 500f, 400f, 26f);

            var name = NewText("Name", crt, "RIVEN", 52f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(name, 0.02f);
            TopLeft(name.rectTransform, 28f, 528f, 500f, 62f);

            // ปุ่มเปลี่ยนตัวละคร — ป้ายปุ่ม C มาจากแบบ ยังไม่ได้ผูกจริงในโปรเจกต์
            var change = NewRect("ChangeButton", crt);
            TopRight(change, 28f, 538f, 210f, 44f);
            var chBg = NewImage("Bg", change, Lift(PanelBg, 0.10f));
            Stretch(chBg.rectTransform);
            Shear(chBg);
            var chLabel = NewMono("Label", change, "CHANGE · C", 15f, 0.18f,
                                  TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            Stretch(chLabel.rectTransform);

            var tags = NewMono("Tags", crt, "DUELIST · MELEE · HP 120 · SPD 6.0", 17f, 0.14f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.55f));
            TopLeft(tags.rectTransform, 28f, 598f, ColW - 56f, 28f);

            // ── ชิปสกิล ─────────────────────────────────────────────────────
            var chips = NewRect("AbilityChips", crt);
            TopLeft(chips, 28f, 644f, ColW - 56f, 120f);

            // ช่องว่าง = ไม่มีปุ่ม (อาวุธประจำตัว) · "Q"/"E" คือ **ชื่อช่อง** ไม่ใช่ชื่อปุ่ม
            (string slot, string label)[] entries =
            {
                ("",  "RUNIC BLADE"),
                ("Q", "VALOR"),
                ("E", "BLADE OF EXILE"),
            };

            float cx = 0f, cy = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                float w = entries[i].label.Length > 10 ? 320f : 240f;
                if (cx + w > ColW - 56f) { cx = 0f; cy += 56f; }

                var chip = (LobbyAbilityChipUI)PrefabUtility.InstantiatePrefab(chipPrefab, chips);
                chip.gameObject.SetActive(true);
                chip.name = $"Chip_{i}";
                var rt = (RectTransform)chip.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(w, 46f);
                rt.anchoredPosition = new Vector2(cx, -cy);
                chip.Bind(entries[i].slot, entries[i].label);

                cx += w + 12f;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildRightColumn(RectTransform root, LobbyUI ui, LobbyPartyRowUI rowPrefab)
        {
            float h = RefH - ContentY - BottomH - 24f;

            var col = NewImage("RightColumn", root, PanelBg);
            TopRight(col.rectTransform, PadX, ContentY + 12f, ColW, h);
            var crt = col.rectTransform;

            var count = NewMono("PartyCount", crt, "PARTY · 3 / 4", 17f, 0.24f,
                                TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.7f));
            TopLeft(count.rectTransform, 28f, 24f, 400f, 28f);
            ui.partyCountLabel = count;

            var ready = NewMono("ReadyCount", crt, "2 READY", 17f, 0.24f,
                                TextAlignmentOptions.MidlineRight, Teal);
            TopRight(ready.rectTransform, 28f, 24f, 260f, 28f);
            ui.readyCountLabel = ready;

            var rule = NewImage("Rule", crt, FaintLine);
            var rrt = rule.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.sizeDelta = new Vector2(-56f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -62f);

            // ── ที่วางแถวปาร์ตี้ ─────────────────────────────────────────────
            var rows = NewRect("PartyRows", crt);
            TopLeft(rows, 28f, 78f, ColW - 56f, 4f * 96f);
            ui.partyContainer = rows;
            ui.partyRowPrefab = rowPrefab;

            // ตัวอย่างในซีน — ตอนรัน RefreshP3RPartyRows() ล้างทิ้งแล้วสร้างใหม่จากข้อมูลจริง
            (string name, string detail, bool ready, bool host, bool you)[] sample =
            {
                ("RIVEN",  "HP 120", true,  true,  true),
                ("HUNTER", "HP 100", true,  false, false),
                ("GUNNER", "HP 90",  false, false, false),
            };

            for (int i = 0; i < 4; i++)
            {
                var row = (LobbyPartyRowUI)PrefabUtility.InstantiatePrefab(rowPrefab, rows);
                row.gameObject.SetActive(true);
                row.name = $"Sample_{i}";
                var rt = (RectTransform)row.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(ColW - 56f, 86f);
                rt.anchoredPosition = new Vector2(0f, -i * 96f);

                if (i >= sample.Length) { row.SetEmpty(); continue; }
                var s = sample[i];
                row.Bind(i, s.name, s.host, s.you, s.ready, s.detail, SlotColor(i));
            }

            // ── พรีวิวแมพ ────────────────────────────────────────────────────
            var mapHead = NewMono("MapHead", crt, "MAP", 15f, 0.28f,
                                  TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(mapHead.rectTransform, 28f, 470f, 300f, 26f);

            var mapBox = NewImage("MapPreview", crt, Lift(PanelBg, 0.06f));
            TopLeft(mapBox.rectTransform, 28f, 502f, ColW - 56f, 236f);
            ui.mapImage = mapBox;
            mapBox.enabled = true;      // ยังไม่มี sprite — เป็นกล่องเปล่าให้เห็นระยะ

            var mapHint = NewMono("MapHint", mapBox.rectTransform, "MAP PREVIEW", 15f, 0.3f,
                                  TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.18f));
            Stretch(mapHint.rectTransform);

            var mapName = NewText("MapName", crt, "ARENA 01", 26f, TextAlignmentOptions.MidlineLeft);
            MonoStyle(mapName, 0.06f);
            TopLeft(mapName.rectTransform, 28f, 748f, 420f, 34f);
            ui.mapNameLabel = mapName;

            var diff = NewMono("Difficulty", crt, "NORMAL", 17f, 0.22f,
                               TextAlignmentOptions.MidlineRight, Amber);
            TopRight(diff.rectTransform, 28f, 752f, 260f, 28f);
            ui.difficultyLabel = diff;
        }

        private static Color SlotColor(int i)
        {
            Color[] c = { BlueSlot, Teal, Gold, Orange };
            return c[Mathf.Abs(i) % c.Length];
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBottomBar(RectTransform root, LobbyUI ui)
        {
            var bar = NewRect("BottomBar", root);
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BottomH);
            bar.anchoredPosition = Vector2.zero;

            ui.backButton = BarButton(bar, "Back", "BACK", left: true, x: PadX,
                                      w: 220f, filled: false, tint: new Color(1f, 1f, 1f, 0.7f));

            ui.readyButton     = BarButton(bar, "Ready", "READY", left: false, x: PadX + 300f,
                                           w: 260f, filled: false, tint: Teal);
            ui.startRunButton  = BarButton(bar, "StartRun", "START RUN", left: false, x: PadX,
                                           w: 280f, filled: true, tint: Primary);

            var readyLabel = ui.readyButton.GetComponentInChildren<TextMeshProUGUI>();
            ui.readyButtonText = readyLabel;
        }

        private static Button BarButton(RectTransform bar, string name, string text,
                                        bool left, float x, float w, bool filled, Color tint)
        {
            const float h = 56f;
            var root = NewRect($"Btn_{name}", bar);
            if (left) BottomLeft(root, x, 24f, w, h);
            else      BottomRight(root, x, 24f, w, h);

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
            // ปิด navigation ไม่ให้โฟกัสค้างแล้วโดน Space ยิงซ้ำทีหลัง
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }

        // ═══════════════════════════════════════════════════════════════════
        // PREFABS
        // ═══════════════════════════════════════════════════════════════════
        private static LobbyPartyRowUI BuildPartyRowPrefab()
        {
            var rt = NewRect("LobbyPartyRow", null);
            rt.sizeDelta = new Vector2(824f, 86f);

            var bg = NewImage("Bg", rt, RowBg);
            Stretch(bg.rectTransform);

            var accent = AccentBar(rt, BlueSlot);

            // ── สถานะ "มีคนนั่งอยู่" ─────────────────────────────────────────
            var filled = NewRect("Filled", rt);
            Stretch(filled);

            var slot = NewMono("Slot", filled, "PLAYER 1", 14f, 0.26f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(slot.rectTransform, 24f, 16f, 220f, 22f);

            var hostBadge  = Badge(filled, "HostBadge", "HOST", 250f, Gold);
            var youBadge   = Badge(filled, "YouBadge",  "YOU",  330f, Teal);

            var name = NewText("Name", filled, "RIVEN", 26f, TextAlignmentOptions.MidlineLeft);
            TopLeft(name.rectTransform, 24f, 42f, 300f, 34f);

            var detail = NewText("Detail", filled, "HP 120", 18f, TextAlignmentOptions.MidlineLeft,
                                 new Color(1f, 1f, 1f, 0.55f));
            TopLeft(detail.rectTransform, 330f, 46f, 240f, 28f);

            var status = NewMono("Status", filled, "READY", 17f, 0.18f,
                                 TextAlignmentOptions.MidlineRight, Teal);
            TopRight(status.rectTransform, 24f, 42f, 300f, 32f);

            // ── สถานะ "ช่องว่าง" ────────────────────────────────────────────
            var empty = NewRect("Empty", rt);
            Stretch(empty);
            var emptyLabel = NewMono("Label", empty, "EMPTY SLOT · INVITE", 17f, 0.24f,
                                     TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.28f));
            Stretch(emptyLabel.rectTransform);
            // เส้นประของแบบยังไม่มี sprite — ใช้กรอบทึบจางแทนไปก่อน
            AddBorder(rt, "EmptyBorder", 1f, new Color(1f, 1f, 1f, 0.14f));
            empty.gameObject.SetActive(false);

            var row = rt.gameObject.AddComponent<LobbyPartyRowUI>();
            row.background   = bg;
            row.accentBar    = accent;
            row.slotLabel    = slot;
            row.nameLabel    = name;
            row.detailLabel  = detail;
            row.statusLabel  = status;
            row.hostBadge    = hostBadge;
            row.youBadge     = youBadge;
            row.filledGroup  = filled.gameObject;
            row.emptyGroup   = empty.gameObject;

            return SavePrefab(rt.gameObject, RowPrefab).GetComponent<LobbyPartyRowUI>();
        }

        private static GameObject Badge(RectTransform parent, string name, string text,
                                        float x, Color tint)
        {
            var root = NewRect(name, parent);
            TopLeft(root, x, 14f, 74f, 24f);

            var bg = NewImage("Bg", root, Over(tint, RowBg, 0.22f));
            Stretch(bg.rectTransform);
            Shear(bg);

            var label = NewMono("Label", root, text, 12f, 0.2f, TextAlignmentOptions.Center, tint);
            Stretch(label.rectTransform);
            return root.gameObject;
        }

        private static LobbyAbilityChipUI BuildAbilityChipPrefab()
        {
            var rt = NewRect("LobbyAbilityChip", null);
            rt.sizeDelta = new Vector2(240f, 46f);

            var bg = NewImage("Bg", rt, Lift(PanelBg, 0.08f));
            Stretch(bg.rectTransform);
            Shear(bg);

            var keyGroup = NewRect("KeyGroup", rt);
            keyGroup.anchorMin = new Vector2(0f, 0f); keyGroup.anchorMax = new Vector2(0f, 1f);
            keyGroup.pivot = new Vector2(0f, 0.5f);
            keyGroup.sizeDelta = new Vector2(50f, -12f);
            keyGroup.anchoredPosition = new Vector2(6f, 0f);

            var keyBg = NewImage("KeyBg", keyGroup, Over(Primary, PanelBg, 0.30f));
            Stretch(keyBg.rectTransform);

            var key = NewMono("Key", keyGroup, "Q", 14f, 0.10f, TextAlignmentOptions.Center, Color.white);
            Stretch(key.rectTransform);

            var label = NewText("Name", rt, "VALOR", 17f, TextAlignmentOptions.MidlineLeft,
                                new Color(1f, 1f, 1f, 0.88f));
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(66f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);

            var chip = rt.gameObject.AddComponent<LobbyAbilityChipUI>();
            chip.background = bg;
            chip.keyLabel   = key;
            chip.keyGroup   = keyGroup.gameObject;
            chip.nameLabel  = label;

            return SavePrefab(rt.gameObject, ChipPrefab).GetComponent<LobbyAbilityChipUI>();
        }
    }
}
