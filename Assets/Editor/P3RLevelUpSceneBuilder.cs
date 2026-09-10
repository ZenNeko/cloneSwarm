using System.IO;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบ "จอ Level Up" ทั้งซีนด้วยโค้ด ตาม design handoff §Screen 1
    ///
    /// **ทำไมเป็น Editor script ไม่ใช่ซีนที่จัดมือ** — เหตุผลเดียวกับ P3RMenuSceneBuilder:
    /// ซีน Unity เป็น YAML ที่แก้ด้วยมือแล้วพังเงียบ · สร้างด้วยโค้ดทำให้รันซ้ำได้ อ่าน diff รู้เรื่อง
    /// และตัวเลขทุกตัวในแบบมีที่อยู่จริงในโค้ดให้ไล่ตรวจได้
    ///
    /// ทุกตัวเลข px ในไฟล์นี้อ่านที่กรอบ 1920×1080 ตรงตามที่ handoff ระบุ
    ///
    /// เมนู: Tools > Clone Swarm > Build P3R Level Up Scene
    /// </summary>
    public static class P3RLevelUpSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_LevelUp.unity";
        private const string ThemePath = "Assets/ScriptableObjects/UI/P3RTheme.asset";
        private const string ThemeDir  = "Assets/ScriptableObjects/UI";
        private const string FontPath  = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-ExtraBold SDF.asset";
        // โปรเจกต์ไม่มีฟอนต์ mono จริง — ป้าย/ตัวเลขระบบใช้ SemiBold + letterSpacing แทน
        // ตัวอักษรจะไม่กว้างเท่ากันทุกตัวเหมือน ui-monospace แต่ "ความรู้สึกป้ายระบบ" ยังอยู่
        private const string MonoFontPath = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-SemiBold SDF.asset";

        // ── stage ──────────────────────────────────────────────────────────
        private const float RefW = 1920f, RefH = 1080f;

        // ── design tokens (ตรงกับตารางใน handoff) ──────────────────────────
        private static readonly Color Ink        = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
        private static readonly Color InkDeep    = new Color32(0x06, 0x08, 0x12, 0xFA);
        private static readonly Color CardBg     = new Color32(0x11, 0x18, 0x38, 0xFF);
        private static readonly Color Gold     = new Color32(0xFF, 0xE6, 0x33, 0xFF);
        private static readonly Color Amber      = new Color32(0xD9, 0x9A, 0x1A, 0xFF);
        private static readonly Color BlueSlot   = new Color32(0x40, 0x73, 0xD9, 0xFF);
        private static readonly Color Green      = new Color32(0x4D, 0xA6, 0x59, 0xFF);
        private static readonly Color Teal       = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color ArrowGrey  = new Color32(0x8C, 0x86, 0x78, 0xFF);
        private static readonly Color InkOnAmber = new Color32(0x1A, 0x12, 0x00, 0xFF);
        private static readonly Color InkOnGreen = new Color32(0x04, 0x21, 0x1B, 0xFF);

        private static TMP_FontAsset displayFont;
        private static TMP_FontAsset monoFont;

        /// <summary>การ์ดตัวอย่างสามใบ — WEAPON / SUPER / STAT ครบทุกสีประเภทที่แบบใช้</summary>
        private static readonly (string type, Color accent, Color onAccent, string status,
                                 string name, bool recommended, bool useStatRows)[] Cards =
        {
            ("WEAPON", BlueSlot, Color.white, "Lv 3 / 5", "Arc Blade",     false, true),
            ("SUPER",  Amber,    InkOnAmber,  "★ Lv 4 / 5", "Orbital Storm", true,  true),
            ("STAT",   Green,    InkOnGreen,  "NEW",       "Crit Chance",   false, false),
        };

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Build P3R Level Up Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "สร้างซีนต้นแบบ Level Up ใหม่",
                    $"{ScenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือไว้ในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var theme = LoadTheme();
            displayFont = theme != null && theme.font != null
                ? theme.font
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath) ?? displayFont;

            if (displayFont == null)
                Debug.LogWarning($"[P3R LevelUp] หา font ไม่เจอที่ {FontPath} — ตัวหนังสือจะตกกลับไป TMP default");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();
            var canvas = BuildCanvas();

            var panel = NewRect("LevelUpPanel", canvas.transform);
            Stretch(panel);

            var ui = panel.gameObject.AddComponent<LevelUpUI>();
            ui.panelRoot = panel.gameObject;

            BuildBackground(panel);

            // template ของแถวสเตต ต้องมีก่อนสร้างการ์ด — การ์ดถือ reference ไปใช้ Instantiate
            var statRowTemplate = BuildStatRowTemplate(panel);

            BuildCardsSection(panel, ui, statRowTemplate);
            BuildTimer(panel, ui);
            BuildWaitingStrip(panel, ui);
            BuildHint(panel, ui);
            BuildBuildStrip(panel, ui);

            // ซีนต้นแบบต้องเห็นของตอนเปิดดู — Awake ของ LevelUpUI จะปิด panelRoot ให้เองตอนกด Play
            panel.gameObject.SetActive(true);
            EditorUtility.SetDirty(ui);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ScenePath));
            Debug.Log(
                $"[P3R LevelUp] สร้าง {ScenePath} เรียบร้อย\n" +
                "ที่ยังเป็น placeholder และต้องเติมด้วยมือ:\n" +
                "  1) พื้นหลัง radial-gradient + ลายเส้น 115° — uGUI วาดเกรเดียนต์เองไม่ได้\n" +
                "     ตอนนี้เป็นสีทึบซ้อนกัน · ลาก sprite ใส่ Background/Wash กับ Background/Scanlines\n" +
                "  2) ไอคอนในการ์ด (88×88) กับช่องของในแถบ build (62×62) ยังเป็นกล่องเปล่า\n" +
                "     ไอคอนจริงอยู่ที่ Assets/500FreeSkillIcons/\n" +
                "  3) ไม่มีฟอนต์ mono ในโปรเจกต์ — ป้ายระบบใช้ Sarabun-SemiBold + letterSpacing แทน\n" +
                "  4) กด Play แล้วจอจะซ่อนตัวเอง (LevelUpUI.Awake) จนกว่า UpgradeManager จะเรียก Show()");
        }

        // ═══════════════════════════════════════════════════════════════════
        // SCENE FRAME
        // ═══════════════════════════════════════════════════════════════════
        private static P3RTheme LoadTheme()
        {
            var t = AssetDatabase.LoadAssetAtPath<P3RTheme>(ThemePath);
            if (t == null)
                Debug.LogWarning($"[P3R LevelUp] ไม่พบ {ThemePath} — รัน Tools > Clone Swarm > Build P3R Main Menu Scene " +
                                 "หนึ่งครั้งเพื่อให้มันสร้าง theme ให้ก่อน (ที่นี่จะใช้ค่าฟอนต์สำรองไปพลาง)");
            else if (!AssetDatabase.IsValidFolder(ThemeDir))
                Debug.LogWarning($"[P3R LevelUp] โฟลเดอร์ {ThemeDir} หายไป");
            return t;
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ink;
            go.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static void BuildEventSystem()
        {
            // ต้องเป็น InputSystemUIInputModule — ProjectSettings activeInputHandler = 1
            // StandaloneInputModule ตัวเก่าจะไม่รับอินพุตเลยและไม่มี error ให้เห็น
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("LevelUpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            return canvas;
        }

        /// <summary>
        /// พื้นจอ — แบบใช้ radial-gradient + ลายเส้น 115° ซึ่ง uGUI ทำเองไม่ได้
        /// ที่นี่วางเป็นชั้นสีทึบไว้ให้ contrast ถูก แล้วเปิดช่องให้ลาก sprite มาแทนทีหลัง
        /// </summary>
        private static void BuildBackground(RectTransform panel)
        {
            var root = NewRect("Background", panel);
            Stretch(root);

            var ink = NewImage("Ink", root, InkDeep);
            Stretch(ink.rectTransform);
            // ตัวเดียวในจอที่กินเมาส์ — กันคลิกทะลุไปโดน gameplay ข้างหลัง
            ink.raycastTarget = true;

            // ก้อนน้ำเงินกลางจอแทนใจกลางของ radial-gradient
            var wash = NewImage("Wash", root, new Color32(0x18, 0x24, 0xD8, 0x4D));
            var wrt = wash.rectTransform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.55f);
            wrt.pivot     = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(RefW * 1.2f, RefH * 0.9f);

            // ที่สำหรับ sprite ลายเส้นทับพื้น 115° — ปิดไว้จนกว่าจะมีของจริง
            var lines = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.05f));
            Stretch(lines.rectTransform);
            lines.gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════════════════
        // CARDS
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildCardsSection(RectTransform panel, LevelUpUI ui, UpgradeStatRowUI statRowTemplate)
        {
            var section = NewRect("CardsSection", panel);
            Stretch(section);
            ui.cardsSection = section.gameObject;

            BuildTitle(section, ui);

            // การ์ดสามใบ: top 300 · จัดกึ่งกลาง · ระยะห่าง 30 · ใบละ 340×480
            const float cardW = 340f, cardH = 480f, gap = 30f;
            float totalW = cardW * 3f + gap * 2f;

            var container = NewRect("CardsContainer", section);
            container.anchorMin = container.anchorMax = new Vector2(0.5f, 1f);
            container.pivot     = new Vector2(0.5f, 1f);
            container.sizeDelta = new Vector2(totalW, cardH);
            container.anchoredPosition = new Vector2(0f, -300f);
            ui.cardsContainer = container.gameObject;

            if (ui.cardSlots == null) ui.cardSlots = new System.Collections.Generic.List<UpgradeCardUI>();
            ui.cardSlots.Clear();
            for (int i = 0; i < Cards.Length; i++)
            {
                var card = BuildCard(container, i, cardW, cardH, gap, statRowTemplate);
                ui.cardSlots.Add(card);
            }
        }

        /// <summary>
        /// หัวเรื่อง LEVEL / UP! — ซ้าย 86 / บน 96 · 150px/0.84 · โครงร่างทอง 3px
        ///
        /// สองข้อจำกัดของ TMP ที่ต้องยอม:
        ///  • UIShear ใช้กับ TMP ไม่ได้ (ดูคอมเมนต์ในไฟล์นั้น) — skewX(-11deg) จึงใช้ Italic แทน
        ///    ทิศทางตรงกัน (ยอดตัวอักษรเอนไปทางขวา) แต่องศาเป็นของฟอนต์ ไม่ใช่ 11 เป๊ะ
        ///  • ตัวอักษรกลวงจริง (face โปร่ง) ทำให้ vertex alpha กลืนขอบไปด้วย
        ///    จึงใช้ face สีพื้นจอแทน — บนพื้นมืดอ่านออกเหมือนกลวง
        /// </summary>
        private static void BuildTitle(RectTransform section, LevelUpUI ui)
        {
            var t = NewText("Title", section, "LEVEL\nUP!");
            t.font        = displayFont;
            t.fontSize    = 150f;
            t.lineSpacing = -18f;                 // ≈ line-height 0.84
            t.characterSpacing = -3.5f;           // letter-spacing -0.035em
            t.fontStyle   = FontStyles.Italic;
            t.color       = Ink;
            t.outlineColor = Gold;
            t.outlineWidth = 0.28f;               // ≈ text-stroke 3px ที่ 150px
            t.alignment   = TextAlignmentOptions.TopLeft;

            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(700f, 300f);
            rt.anchoredPosition = new Vector2(86f, -96f);
            rt.localScale = new Vector3(0.84f, 1f, 1f);   // scaleX(0.84)

            ui.levelLabel = t;
            // หัวเรื่องเป็นสองบรรทัดตามแบบ — เลเวลใหม่ไปอยู่ในข้อความเดียวกันไม่ได้
            ui.titleFormat        = "LEVEL\nUP!";
            ui.titleFormatNoLevel = "LEVEL\nUP!";
        }

        private static UpgradeCardUI BuildCard(RectTransform container, int i,
                                               float w, float h, float gap,
                                               UpgradeStatRowUI statRowTemplate)
        {
            var (type, accent, onAccent, status, name, recommended, useStatRows) = Cards[i];

            var rt = NewRect($"Card_{type}", container);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(i * (w + gap), 0f);

            var card = rt.gameObject.AddComponent<UpgradeCardUI>();

            // ① พื้นการ์ด — ตัวรับเมาส์ของทั้งใบ
            var body = NewImage("Body", rt, CardBg);
            Stretch(body.rectTransform);
            body.raycastTarget = true;

            // ② กรอบ 2px สีตามประเภท
            AddBorder(rt, "Border", 2f, accent);

            // ③ วงเรืองแสง 6px — ปิดไว้ เปิดเฉพาะตอนชี้ (LevelUpUI.glowOnlyOnHover)
            var glow = NewRect("Glow", rt);
            Stretch(glow);
            glow.offsetMin = new Vector2(-6f, -6f);
            glow.offsetMax = new Vector2(6f, 6f);
            AddBorder(glow, "GlowEdge", 6f, new Color(accent.r, accent.g, accent.b, 0.18f));
            glow.gameObject.SetActive(false);
            card.recommendedGlowOutline = glow.gameObject;

            // ④ หัวการ์ด สูง 52 — UpgradeCardUI จะย้อมสีตัวนี้ตามประเภทตอน Populate
            var header = NewImage("Header", rt, accent);
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot     = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, 52f);
            hrt.anchoredPosition = Vector2.zero;
            card.cardBackground = header;

            var typeLabel = NewMono("TypeLabel", hrt, type, 15f, 0.20f);
            typeLabel.color     = onAccent;
            typeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            Inset(typeLabel.rectTransform, 16f, 0f);

            var statusLabel = NewMono("LevelText", hrt, status, 15f, 0.14f);
            statusLabel.color     = onAccent;
            statusLabel.alignment = TextAlignmentOptions.MidlineRight;
            Inset(statusLabel.rectTransform, 16f, 0f);
            card.levelText = statusLabel;

            // ⑤ ช่องไอคอน สูง 150 · พื้นสีประเภทจาง + เส้นล่าง 1px
            var iconArea = NewImage("IconArea", rt, new Color(accent.r, accent.g, accent.b, 0.13f));
            var irt = iconArea.rectTransform;
            irt.anchorMin = new Vector2(0f, 1f);
            irt.anchorMax = new Vector2(1f, 1f);
            irt.pivot     = new Vector2(0.5f, 1f);
            irt.sizeDelta = new Vector2(0f, 150f);
            irt.anchoredPosition = new Vector2(0f, -52f);

            var underline = NewImage("Underline", irt, new Color(1f, 1f, 1f, 0.10f));
            var urt = underline.rectTransform;
            urt.anchorMin = new Vector2(0f, 0f);
            urt.anchorMax = new Vector2(1f, 0f);
            urt.pivot     = new Vector2(0.5f, 0f);
            urt.sizeDelta = new Vector2(0f, 1f);

            var icon = NewImage("Icon", irt, new Color(1f, 1f, 1f, 0.85f));
            var icrt = icon.rectTransform;
            icrt.anchorMin = icrt.anchorMax = new Vector2(0.5f, 0.5f);
            icrt.pivot     = new Vector2(0.5f, 0.5f);
            icrt.sizeDelta = new Vector2(88f, 88f);
            // ไม่มี sprite → เรนเดอร์เป็นสี่เหลี่ยมขาว 88×88 = "ที่ของไอคอน"
            // ปล่อยให้ enabled ติดไว้ เพราะ UpgradeCardUI.Populate ตั้งแค่ sprite ไม่ได้เปิด component ให้
            AddBorder(icrt, "IconBox", 1f, new Color(1f, 1f, 1f, 0.22f));
            card.iconImage = icon;

            // ⑥ ชื่ออัปเกรด 40px · padding 22 20 0
            var nameText = NewText("NameText", rt, name);
            nameText.font      = displayFont;
            nameText.fontSize  = 40f;
            nameText.lineSpacing = -12f;
            nameText.characterSpacing = -3f;
            nameText.color     = Color.white;
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.textWrappingMode = TextWrappingModes.Normal;
            var nrt = nameText.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot     = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(-40f, 96f);
            nrt.anchoredPosition = new Vector2(0f, -(52f + 150f + 22f));
            card.nameText = nameText;

            // ⑦ เนื้อหาล่างสุด — padding 0 20 20
            var bottom = NewRect("Bottom", rt);
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot     = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(-40f, 176f);
            bottom.anchoredPosition = new Vector2(0f, 20f);

            var desc = NewText("DescriptionText", bottom, "คำบรรยายอัปเกรดตัวอย่างสองบรรทัดพอให้เห็นระยะจริง");
            desc.font      = displayFont;
            desc.fontSize  = 21f;
            desc.lineSpacing = 8f;               // ≈ line-height 1.45
            desc.color     = new Color(1f, 1f, 1f, 0.82f);
            desc.alignment = TextAlignmentOptions.TopLeft;
            desc.textWrappingMode = TextWrappingModes.Normal;
            Stretch(desc.rectTransform);
            desc.gameObject.SetActive(!useStatRows);
            card.descriptionText = desc;

            // แถวสเตต ก่อน → หลัง · ระยะห่าง 9px · UpgradeCardUI เป็นคน Instantiate เอง
            var rows = NewRect("StatRows", bottom);
            rows.anchorMin = new Vector2(0f, 1f);
            rows.anchorMax = new Vector2(1f, 1f);
            rows.pivot     = new Vector2(0.5f, 1f);
            rows.sizeDelta = new Vector2(0f, 140f);
            var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 9f;
            vlg.childControlHeight = false;
            vlg.childControlWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;
            vlg.childAlignment = TextAnchor.UpperLeft;
            rows.gameObject.SetActive(useStatRows);
            card.statRowsContainer = rows;
            card.statRowPrefab     = statRowTemplate;

            // แถว SYNERGY — ป้าย mono 15 + ช่อง 36×36 สองช่อง
            var synergy = NewRect("SynergyRow", bottom);
            synergy.anchorMin = new Vector2(0f, 0f);
            synergy.anchorMax = new Vector2(1f, 0f);
            synergy.pivot     = new Vector2(0.5f, 0f);
            synergy.sizeDelta = new Vector2(0f, 36f);

            var synLabel = NewMono("SynergyLabel", synergy, "SYNERGY", 15f, 0.20f);
            synLabel.color     = new Color(1f, 1f, 1f, 0.55f);
            synLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var slrt = synLabel.rectTransform;
            slrt.anchorMin = slrt.anchorMax = new Vector2(0f, 0.5f);
            slrt.pivot     = new Vector2(0f, 0.5f);
            slrt.sizeDelta = new Vector2(110f, 36f);

            card.synergyIconImages.Clear();
            for (int s = 0; s < 2; s++)
            {
                var box = NewImage($"SynergyIcon_{s}", synergy, new Color(1f, 1f, 1f, 0.10f));
                var brt = box.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot     = new Vector2(0f, 0.5f);
                brt.sizeDelta = new Vector2(36f, 36f);
                brt.anchoredPosition = new Vector2(118f + s * 44f, 0f);
                AddBorder(brt, "Box", 1f, new Color(1f, 1f, 1f, 0.22f));
                card.synergyIconImages.Add(box);
            }
            synergy.gameObject.SetActive(false);
            card.evolutionBadge = synergy.gameObject;

            // ⑧ ป้าย "แนะนำ" สูง 34 ยื่นเหนือหัวการ์ด
            var ribbon = NewImage("RecommendedRibbon", rt, Amber);
            var rbrt = ribbon.rectTransform;
            rbrt.anchorMin = rbrt.anchorMax = new Vector2(0f, 1f);
            rbrt.pivot     = new Vector2(0f, 0f);
            rbrt.sizeDelta = new Vector2(120f, 34f);
            rbrt.anchoredPosition = Vector2.zero;
            var ribbonText = NewMono("Label", rbrt, "แนะนำ", 15f, 0.16f);
            ribbonText.color     = InkOnAmber;
            ribbonText.alignment = TextAlignmentOptions.Midline;
            Stretch(ribbonText.rectTransform);
            ribbon.gameObject.SetActive(recommended);
            card.recommendedRibbon = ribbon.gameObject;

            // ⑨ ปุ่มคลิกทั้งใบ — โปร่งใส ทับทุกอย่าง เพื่อให้คลิกตรงไหนของการ์ดก็เลือก
            var btnRt = NewRect("SelectButton", rt);
            Stretch(btnRt);
            var btnImg = btnRt.gameObject.AddComponent<Image>();
            btnImg.color = new Color(1f, 1f, 1f, 0f);
            btnImg.raycastTarget = true;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition    = Selectable.Transition.None;   // hover ใช้วงเรืองแสงแทน ไม่ใช้ tint
            card.selectButton = btn;

            return card;
        }

        /// <summary>
        /// แถวสเตต ก่อน → หลัง หนึ่งแถว · เก็บไว้ในซีนแบบปิดไว้แล้วให้การ์ดใช้เป็นต้นแบบ
        /// (Instantiate ใช้ instance ในซีนเป็นต้นแบบได้ ไม่จำเป็นต้องเป็น prefab asset)
        /// </summary>
        private static UpgradeStatRowUI BuildStatRowTemplate(RectTransform panel)
        {
            var rt = NewRect("StatRow_Template", panel);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(300f, 26f);

            var row = rt.gameObject.AddComponent<UpgradeStatRowUI>();

            var name = NewText("Name", rt, "Damage");
            name.font      = displayFont;
            name.fontSize  = 19f;
            name.color     = new Color(1f, 1f, 1f, 0.9f);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            Place(name.rectTransform, 0f, 130f);
            row.nameText = name;

            var before = NewMono("Before", rt, "120", 19f, 0.06f);
            before.color     = new Color(1f, 1f, 1f, 0.45f);
            before.alignment = TextAlignmentOptions.MidlineRight;
            Place(before.rectTransform, 132f, 60f);
            row.beforeText = before;

            var arrow = NewText("Arrow", rt, "→");
            arrow.font      = displayFont;
            arrow.fontSize  = 19f;
            arrow.color     = ArrowGrey;
            arrow.alignment = TextAlignmentOptions.Midline;
            Place(arrow.rectTransform, 196f, 24f);
            row.arrowIcon = arrow.gameObject;

            var after = NewMono("After", rt, "148", 22f, 0.06f);
            after.color     = Teal;
            after.fontStyle = FontStyles.Bold;
            after.alignment = TextAlignmentOptions.MidlineLeft;
            Place(after.rectTransform, 224f, 76f);
            row.afterText = after;

            rt.gameObject.SetActive(false);
            return row;

            // วางกล่องลูกแบบชิดซ้ายด้วย x/ความกว้าง — อ่านง่ายกว่าคำนวณ offsetMin/Max ทีละตัว
            static void Place(RectTransform r, float x, float w)
            {
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot     = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(w, 0f);
                r.anchoredPosition = new Vector2(x, 0f);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // TIMER — ขวา 104 / บน 112
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildTimer(RectTransform panel, LevelUpUI ui)
        {
            var root = NewRect("Timer", panel);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
            root.pivot     = new Vector2(1f, 1f);
            root.sizeDelta = new Vector2(320f, 200f);
            root.anchoredPosition = new Vector2(-104f, -112f);

            var num = NewText("Number", root, "28");
            num.font      = displayFont;
            num.fontSize  = 122f;
            num.characterSpacing = -4f;
            num.color     = Color.white;
            num.alignment = TextAlignmentOptions.TopRight;
            var nrt = num.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot     = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(0f, 140f);
            ui.timerLabel = num;

            // แถบ 180×6 ใต้ตัวเลข — track จาง แล้วเติมทองตามสัดส่วนเวลาที่เหลือ
            var track = NewImage("BarTrack", root, new Color(1f, 1f, 1f, 0.16f));
            var trt = track.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot     = new Vector2(1f, 1f);
            trt.sizeDelta = new Vector2(180f, 6f);
            trt.anchoredPosition = new Vector2(0f, -146f);

            var fill = NewImage("BarFill", trt, Gold);
            Stretch(fill.rectTransform);
            fill.type            = Image.Type.Filled;
            fill.fillMethod      = Image.FillMethod.Horizontal;
            fill.fillOrigin      = (int)Image.OriginHorizontal.Left;
            fill.fillAmount      = 1f;
            // Filled ต้องมี sprite ถึงจะคำนวณ fill ได้ — ไม่งั้นมันวาดเต็มตลอด
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            ui.timerFillBar = fill;

            var secs = NewMono("SecondsLabel", root, "SECONDS", 15f, 0.24f);
            secs.color     = new Color(1f, 1f, 1f, 0.5f);
            secs.alignment = TextAlignmentOptions.TopRight;
            var srt = secs.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot     = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(0f, 22f);
            srt.anchoredPosition = new Vector2(0f, -160f);
        }

        // ═══════════════════════════════════════════════════════════════════
        // WAITING STRIP — ซ้าย 86 / ล่าง 96
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildWaitingStrip(RectTransform panel, LevelUpUI ui)
        {
            var root = NewRect("WaitingStrip", panel);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot     = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(760f, 110f);
            root.anchoredPosition = new Vector2(86f, 96f);
            ui.waitingStrip = root.gameObject;

            var dogTags = root.gameObject.AddComponent<LevelUpDogTagUI>();
            ui.dogTags = dogTags;

            var label = NewMono("Label", root, "รอผู้เล่น 1 / 2", 17f, 0.20f);
            label.color     = new Color(1f, 1f, 1f, 0.7f);
            label.alignment = TextAlignmentOptions.TopLeft;
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot     = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(500f, 26f);
            dogTags.countLabel = label;

            var area = NewRect("TagArea", root);
            area.anchorMin = area.anchorMax = new Vector2(0f, 0f);
            area.pivot     = new Vector2(0f, 0f);
            area.sizeDelta = new Vector2(700f, 52f);
            dogTags.tagArea = area;

            dogTags.tagTemplate = BuildDogTagTemplate(root);
        }

        private static LevelUpDogTag BuildDogTagTemplate(RectTransform parent)
        {
            var rt = NewRect("DogTag_Template", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(52f, 52f);

            var tag = rt.gameObject.AddComponent<LevelUpDogTag>();
            tag.group = rt.gameObject.AddComponent<CanvasGroup>();

            var bg = NewImage("Bg", rt, new Color(1f, 1f, 1f, 0.05f));
            Stretch(bg.rectTransform);
            tag.background = bg;

            var label = NewMono("Label", rt, "P1", 17f, 0.10f);
            label.color     = new Color(1f, 1f, 1f, 0.55f);
            label.alignment = TextAlignmentOptions.Midline;
            Stretch(label.rectTransform);
            tag.label = label;

            tag.borderEdges = AddBorder(rt, "Border", 2f, new Color(1f, 1f, 1f, 0.22f));

            rt.gameObject.SetActive(false);
            return tag;
        }

        /// <summary>คำใบ้ปุ่ม — จอนี้มีเวลาจำกัด ถ้าไม่บอก ผู้เล่นจะไม่รู้ว่ากด 1/2/3 ได้</summary>
        private static void BuildHint(RectTransform panel, LevelUpUI ui)
        {
            var hint = NewMono("KeyHint", panel, ui.hintText, 17f, 0.20f);
            hint.color     = new Color(1f, 1f, 1f, 0.42f);
            hint.alignment = TextAlignmentOptions.BottomLeft;
            var rt = hint.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(600f, 26f);
            // ใต้แถบสถานะรอเพื่อน (ล่าง 96) — แบบเขียนว่า "ข้าง" แต่ฝั่งซ้ายเต็มด้วย dog-tag แล้ว
            rt.anchoredPosition = new Vector2(86f, 56f);
            ui.hintLabel = hint;
        }

        // ═══════════════════════════════════════════════════════════════════
        // BUILD STRIP — ขวา 57 / ล่าง 45 · กว้าง 600 · สองแถว ระยะห่าง 14
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildBuildStrip(RectTransform panel, LevelUpUI ui)
        {
            const float stripW = 600f, rowH = 90f, rowGap = 14f;
            const float padX = 20f, labelW = 132f, edgeW = 6f;

            var root = NewRect("BuildStrip", panel);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
            root.pivot     = new Vector2(1f, 0f);
            root.sizeDelta = new Vector2(stripW, rowH * 2f + rowGap);
            root.anchoredPosition = new Vector2(-57f, 45f);

            var strip = root.gameObject.AddComponent<BuildStripUI>();
            ui.buildStrip = strip;

            strip.weaponSlotArea  = BuildStripRow(root, "Row_Weapons",  "WEAPONS",  BlueSlot,
                                                  0f, rowH, padX, labelW, edgeW);
            strip.passiveSlotArea = BuildStripRow(root, "Row_Passives", "PASSIVES", Green,
                                                  -(rowH + rowGap), rowH, padX, labelW, edgeW);
            strip.slotTemplate = BuildSlotTemplate(root);
        }

        private static RectTransform BuildStripRow(RectTransform parent, string name, string label,
                                                   Color accent, float y, float rowH,
                                                   float padX, float labelW, float edgeW)
        {
            var row = NewRect(name, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot     = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, rowH);
            row.anchoredPosition = new Vector2(0f, y);

            var bg = NewImage("Bg", row, new Color32(0x0A, 0x0E, 0x1E, 0xB8));   // rgba(10,14,30,.72)
            Stretch(bg.rectTransform);

            // เส้นเน้นซ้าย 6px — ลายเซ็นของการ์ดทุกใบในระบบ (design tokens §รูปทรง)
            var edge = NewImage("LeftEdge", row, accent);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, 0f);
            ert.anchorMax = new Vector2(0f, 1f);
            ert.pivot     = new Vector2(0f, 0.5f);
            ert.sizeDelta = new Vector2(edgeW, 0f);

            var text = NewMono("Label", row, label, 15f, 0.20f);
            text.color     = new Color(1f, 1f, 1f, 0.62f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot     = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(labelW, 26f);
            trt.anchoredPosition = new Vector2(edgeW + padX, 0f);

            var area = NewRect("SlotArea", row);
            area.anchorMin = area.anchorMax = new Vector2(0f, 0.5f);
            area.pivot     = new Vector2(0f, 0.5f);
            area.sizeDelta = new Vector2(420f, 62f);
            area.anchoredPosition = new Vector2(edgeW + padX + labelW, 0f);
            return area;
        }

        private static BuildStripSlot BuildSlotTemplate(RectTransform parent)
        {
            var rt = NewRect("Slot_Template", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(62f, 62f);

            var slot = rt.gameObject.AddComponent<BuildStripSlot>();

            var fill = NewImage("Fill", rt, new Color(1f, 1f, 1f, 0.08f));
            Stretch(fill.rectTransform);
            slot.fill = fill;

            var icon = NewImage("Icon", rt, Color.white);
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(8f, 8f);
            icon.rectTransform.offsetMax = new Vector2(-8f, -8f);
            icon.enabled = false;
            slot.icon = icon;

            var abbrev = NewMono("Abbrev", rt, "BLD", 15f, 0.10f);
            abbrev.color     = new Color(1f, 1f, 1f, 0.75f);
            abbrev.alignment = TextAlignmentOptions.Midline;
            Stretch(abbrev.rectTransform);
            slot.abbrevLabel = abbrev;

            var lv = NewMono("Level", rt, "Lv3", 15f, 0.02f);
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

            slot.borderEdges = AddBorder(rt, "Border", 1f, new Color(1f, 1f, 1f, 0.26f));

            rt.gameObject.SetActive(false);
            return slot;
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var rt  = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;   // เปิดเฉพาะตัวที่ต้องรับเมาส์จริงๆ
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text)
        {
            var rt = NewRect(name, parent);
            var t  = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text             = text;
            t.raycastTarget    = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (displayFont != null) t.font = displayFont;
            return t;
        }

        /// <summary>
        /// ป้าย/ตัวเลขระบบ — แบบระบุ mono + letter-spacing .16–.30em
        /// TMP นับ characterSpacing เป็น % ของ em ค่าจึงเป็น em×100
        /// </summary>
        private static TextMeshProUGUI NewMono(string name, Transform parent, string text,
                                               float size, float letterSpacingEm)
        {
            var t = NewText(name, parent, text);
            if (monoFont != null) t.font = monoFont;
            t.fontSize         = size;
            t.characterSpacing = letterSpacingEm * 100f;
            return t;
        }

        /// <summary>
        /// กรอบสี่ด้านจาก Image สี่ตัว — uGUI ไม่มี border ในตัว และ Outline component เป็นเงาไม่ใช่กรอบ
        /// เรียง บน / ล่าง / ซ้าย / ขวา ให้ตรงกับที่ BuildStripSlot กับ LevelUpDogTag คาดไว้
        /// </summary>
        private static Image[] AddBorder(RectTransform parent, string name, float thickness, Color color)
        {
            var root = NewRect(name, parent);
            Stretch(root);

            var edges = new Image[4];

            edges[0] = NewImage("Top", root, color);
            SetEdge(edges[0].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, thickness));

            edges[1] = NewImage("Bottom", root, color);
            SetEdge(edges[1].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0.5f, 0f), new Vector2(0f, thickness));

            edges[2] = NewImage("Left", root, color);
            SetEdge(edges[2].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), new Vector2(thickness, 0f));

            edges[3] = NewImage("Right", root, color);
            SetEdge(edges[3].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                    new Vector2(1f, 0.5f), new Vector2(thickness, 0f));

            return edges;

            static void SetEdge(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
            {
                r.anchorMin = aMin;
                r.anchorMax = aMax;
                r.pivot     = pivot;
                r.sizeDelta = size;
                r.anchoredPosition = Vector2.zero;
            }
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>ยืดเต็มพ่อแล้วหดเข้ามาเท่ากันทุกด้าน</summary>
        private static void Inset(RectTransform rt, float x, float y)
        {
            Stretch(rt);
            rt.offsetMin = new Vector2(x, y);
            rt.offsetMax = new Vector2(-x, -y);
        }
    }
}
