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
        /// <summary>แม่แบบการ์ด — **ไฟล์ใหม่ ไม่ทับ LevelUpcard(Button).prefab ของจอเดิม**</summary>
        private const string CardPrefabPath = "Assets/Prefab/UI/P3R/LevelUpCard.prefab";
        /// <summary>แถวเทียบสเตตัส — ไฟล์ใหม่ ไม่ทับ Assets/Prefab/UpgradeStatRowUI.prefab ของเดิม</summary>
        private const string StatRowPrefabPath = "Assets/Prefab/UI/P3R/LevelUpStatRow.prefab";
        private const string ThemePath = "Assets/ScriptableObjects/UI/P3RTheme.asset";
        private const string UiAssetDir = "Assets/ScriptableObjects/UI";
        private const string RadialPath = UiAssetDir + "/LevelUpWash_Radial.png";
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
            ("SUPER",  Amber,    InkOnAmber,  "Lv 4 / 5",  "Orbital Storm", true,  true),
            ("STAT",   Green,    InkOnGreen,  "NEW",       "Crit Chance",   false, false),
        };

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Build P3R Level Up Scene")]
        public static void Build()
        {
            // batchmode ไม่มีใครกดปุ่มได้ — DisplayDialog คืน false เสมอแล้ว Build() ออกเงียบๆ
            // อาการคือรัน -executeMethod แล้ว exit 0 แต่ซีนไม่ถูกสร้างใหม่ ไม่มี error ให้เห็น
            if (!Application.isBatchMode && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "สร้างซีนต้นแบบ Level Up ใหม่",
                    $"{ScenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือไว้ในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // โหลด asset **หลัง** NewScene เสมอ — NewScene ปลด asset ที่ไม่มีใครอ้างถึงทิ้ง
            // ใน batchmode ตัวแปรที่โหลดไว้ก่อนจะกลายเป็น fake-null แล้วซีนออกมาไม่มีฟอนต์/สี
            // โดยไม่มี error ให้เห็น (เคยกินเวลาสองวันมาแล้วในจอเมนูหลัก)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var theme = LoadTheme();
            displayFont = theme != null && theme.font != null
                ? theme.font
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath) ?? displayFont;

            if (displayFont == null)
                Debug.LogWarning($"[P3R LevelUp] หา font ไม่เจอที่ {FontPath} — ตัวหนังสือจะตกกลับไป TMP default");

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

            // radial-gradient ของจริง — อบเป็น sprite เพราะ uGUI วาดเกรเดียนต์เองไม่ได้
            // อบในพิกัดกล่องแบบ normalize แล้วยืดเต็มจอ · ตรงกับที่ CSS เขียนวงรีเป็น % ของกล่อง
            // (120% 90%) อยู่แล้ว การยืดไม่เท่ากันสองแกนจึงเป็นสิ่งที่ตั้งใจ ไม่ใช่ความเพี้ยน
            var wash = NewImage("Wash", root, Color.white);
            Stretch(wash.rectTransform);
            wash.sprite = LoadOrCreateRadialSprite();
            if (wash.sprite == null)
            {
                // ยังดีกว่าปล่อยขาวทั้งจอถ้าสร้าง sprite ไม่สำเร็จ
                wash.color = new Color32(0x18, 0x24, 0xD8, 0x4D);
                Debug.LogWarning("[P3R LevelUp] สร้าง sprite พื้นหลังไม่สำเร็จ — ใช้สีทึบแทน");
            }

            // ที่สำหรับ sprite ลายเส้นทับพื้น 115° — ปิดไว้จนกว่าจะมีของจริง
            var lines = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.05f));
            Stretch(lines.rectTransform);
            lines.gameObject.SetActive(false);
        }

        /// <summary>
        /// อบ radial-gradient ตามที่แบบเขียนไว้เป็น PNG
        /// `radial-gradient(120% 90% at 50% 45%, rgba(24,36,216,.72), rgba(10,14,30,.94) 62%, rgba(6,8,18,.98))`
        /// คงค่า alpha ไว้ตามแบบ — จอนี้เปิดทับ gameplay ที่ยังเดินอยู่ (co-op) จึงต้องโปร่งจริง
        /// </summary>
        private static Sprite LoadOrCreateRadialSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RadialPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(UiAssetDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "UI");

            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px  = new Color[n * n];

            var c0 = new Color(24 / 255f, 36 / 255f, 216 / 255f, 0.72f);
            var c1 = new Color(10 / 255f, 14 / 255f,  30 / 255f, 0.94f);
            var c2 = new Color( 6 / 255f,  8 / 255f,  18 / 255f, 0.98f);

            for (int y = 0; y < n; y++)
            {
                // v กลับแกน — Texture2D นับ y จากล่าง ส่วน CSS นับจากบน
                float v = 1f - y / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float u  = x / (float)(n - 1);
                    float dx = (u - 0.50f) / 1.20f;      // วงรี 120% ของความกว้าง
                    float dy = (v - 0.45f) / 0.90f;      // 90% ของความสูง · ศูนย์กลางที่ 45%
                    float t  = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);

                    px[y * n + x] = t <= 0.62f
                        ? Color.Lerp(c0, c1, t / 0.62f)
                        : Color.Lerp(c1, c2, (t - 0.62f) / 0.38f);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(RadialPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(RadialPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(RadialPath) is TextureImporter imp)
            {
                imp.textureType         = TextureImporterType.Sprite;
                imp.spriteImportMode    = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled       = false;
                imp.wrapMode            = TextureWrapMode.Clamp;
                imp.filterMode          = FilterMode.Bilinear;
                // บีบอัดแล้วไล่เฉดจะเป็นแถบทันที · 256×256 เล็กพอที่จะไม่ต้องประหยัด
                imp.textureCompression  = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize      = 256;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RadialPath);
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

            ui.cardGap = gap;

            // ── การ์ดเป็น **prefab** ไม่ใช่ลูกที่ฝังไว้ในซีน ────────────────────
            //
            // กติกาของโปรเจกต์ (game-ui/SKILL.md): ของที่ instantiate หลายใบตอนรัน
            // ต้องเป็น prefab · จอเดิมก่อน P3R ก็ทำแบบนั้น (LevelUpcard(Button).prefab
            // ห้าใบ) แต่ builder ของ P3R กลับสร้างสามใบตายตัวด้วยมือ เพราะเขียนตาม
            // **ภาพ mockup** ไม่ใช่ตามพฤติกรรมตอนรัน
            //
            // ชี้ไฟล์ prefab ไม่ใช่ของในซีน — ของในซีนเป็น fileID ที่เปลี่ยนทุกครั้งที่ย้ายจอ
            var template = BuildCardPrefab(statRowTemplate, cardW, cardH);
            ui.cardTemplate = template;

            if (ui.cardSlots == null) ui.cardSlots = new System.Collections.Generic.List<UpgradeCardUI>();
            ui.cardSlots.Clear();

            // ตัวอย่างในซีนต้นแบบ — มีไว้ให้ภาพเรนเดอร์เห็นจอที่มีของ
            // ตอนรันจริง LevelUpUI ปิดตัวอย่างทิ้งแล้วสร้างจากแม่แบบเอง
            // และ P3RScreenWirer ลบทุกตัวที่ชื่อขึ้นต้น Sample_ ตอนจอลงซีนจริง
            for (int i = 0; i < Cards.Length; i++)
            {
                int index = i;
                var sample = P3RBuilderKit.SpawnSample(template, container, c =>
                {
                    c.name = $"Sample_Card_{Cards[index].type}";
                    var rt = (RectTransform)c.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot     = new Vector2(0f, 1f);
                    rt.sizeDelta = new Vector2(cardW, cardH);
                    rt.anchoredPosition = new Vector2(index * (cardW + gap), 0f);
                    BindSample(c, Cards[index]);
                });
                ui.cardSlots.Add(sample);
            }
        }

        /// <summary>
        /// เติมค่าตัวอย่างลงการ์ดในซีนต้นแบบ — **ไม่มีผลตอนรัน**
        /// `UpgradeCardUI.Populate` เขียนทับทุกช่องที่นี่ตั้งอยู่แล้ว
        /// </summary>
        private static void BindSample(UpgradeCardUI c,
            (string type, Color accent, Color onAccent, string status,
             string name, bool recommended, bool useStatRows) data)
        {
            if (c.nameText)  c.nameText.text  = data.name;
            if (c.levelText) c.levelText.text = data.status;

            if (c.cardBackground) c.cardBackground.color = data.accent;
            foreach (var t in c.accentTargets)
                if (t?.graphic != null)
                    t.graphic.color = new Color(data.accent.r, data.accent.g, data.accent.b, t.alpha);

            var typeLabel = c.transform.Find("Header/TypeLabel")?.GetComponent<TextMeshProUGUI>();
            if (typeLabel) { typeLabel.text = data.type; typeLabel.color = data.onAccent; }
            if (c.levelText) c.levelText.color = data.onAccent;

            // ป้าย "แนะนำ" ถูกถอดออกทั้งระบบตาม ADR-009 — เจ้าของตัดสินว่าชักจูงผู้เล่นเกินไป
            // ตัวอย่างในซีนต้นแบบต้องปิดตามด้วย ไม่งั้นภาพอ้างอิงจะโกหกว่าฟีเจอร์ยังมีอยู่
            // (ตอนรันจริง `UpgradeCardUI.Populate` บังคับปิดให้อยู่แล้ว แต่ภาพนี้ไม่ผ่านตรงนั้น)
            if (c.recommendedRibbon) c.recommendedRibbon.SetActive(false);
            if (c.statRowsContainer) c.statRowsContainer.gameObject.SetActive(data.useStatRows);
            if (c.descriptionText)   c.descriptionText.gameObject.SetActive(!data.useStatRows);

            if (data.useStatRows && c.statRowPrefab != null && c.statRowsContainer != null)
            {
                SpawnStatRow((RectTransform)c.statRowsContainer, c.statRowPrefab, "DAMAGE",   "42",   "58");
                SpawnStatRow((RectTransform)c.statRowsContainer, c.statRowPrefab, "AREA",     "3.0",  "3.6");
                SpawnStatRow((RectTransform)c.statRowsContainer, c.statRowPrefab, "COOLDOWN", "1.4s", "1.1s");
            }

            // ตัวอย่างในซีนต้นแบบก็อยู่ระดับเดียวกันหมด ให้ตรงกับตอนรันจริง
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
        /// <summary>
        /// หนึ่งชั้นของหัวเรื่อง — ทุกใบเหมือนกันหมดยกเว้นตำแหน่งเหลื่อมกับสี
        ///
        /// `UIShear` ใช้กับ TMP ไม่ได้ (ดูคอมเมนต์ในไฟล์นั้น) — skewX(-11deg) ของแบบ
        /// จึงใช้ `Italic` แทน ทิศทางตรงกันแต่องศาเป็นของฟอนต์ ไม่ใช่ 11 เป๊ะ
        /// </summary>
        private static TextMeshProUGUI TitleLayer(RectTransform parent, string name, Vector2 offset)
        {
            var t = NewText(name, parent, "LEVEL\nUP!");
            t.font        = displayFont;
            t.fontSize    = 150f;
            t.lineSpacing = -18f;                 // ≈ line-height 0.84
            P3RText.SetTracking(t, -3.5f);        // letter-spacing -0.035em
            t.fontStyle   = FontStyles.Italic;
            t.alignment   = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;

            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(700f, 300f);
            rt.anchoredPosition = offset;
            return t;
        }

        private static void BuildTitle(RectTransform section, LevelUpUI ui)
        {
            // ── หัวเรื่องสามชั้น ───────────────────────────────────────────
            //
            // ตัวทึบ + เส้นโครงเหลื่อม + เงานูน · ทำด้วย TMP ใบเดียวไม่ได้เพราะ
            // `outlineWidth` ให้ได้แค่ขอบรอบตัวอักษรตรงกลาง ไม่ใช่สำเนาที่เหลื่อมออกไป
            //
            // ลำดับใน Hierarchy = ลำดับการวาด ลูกคนหลังทับลูกคนก่อน:
            //   Title_Shadow  เงานูน  เหลื่อมลงขวา   วาดก่อน อยู่หลังสุด
            //   Title         ตัวทึบ   ตำแหน่งจริง
            //   Title_Wire    เส้นโครง เหลื่อมขึ้นซ้าย วาดหลัง อยู่หน้าสุด
            //
            // ตัวที่โค้ดอื่นเขียนข้อความใส่คือ `Title` — อีกสองใบตามผ่าน P3RLayeredText
            var group = NewRect("TitleGroup", section);
            group.anchorMin = group.anchorMax = new Vector2(0f, 1f);
            group.pivot     = new Vector2(0f, 1f);
            group.sizeDelta = new Vector2(700f, 300f);
            group.anchoredPosition = new Vector2(86f, -96f);
            group.localScale = new Vector3(0.84f, 1f, 1f);   // scaleX(0.84) ทั้งกลุ่ม

            var shadow = TitleLayer(group, "Title_Shadow", new Vector2(10f, -10f));
            shadow.color = InkDeep;

            var t = TitleLayer(group, "Title", Vector2.zero);
            t.color = Gold;

            // **face ของใบเส้นโครงต้องเป็นสีเดียวกับใบทึบ ห้ามโปร่ง** —
            // ตั้ง alpha 0 แล้ว vertex alpha ของ TMP จะคูณทับ outline ไปด้วย
            // ขอบจางหายตามกัน · ตั้งเป็นสีเดียวกันแล้วส่วนที่ทับกันกลืนเป็นเนื้อเดียว
            // เหลือแค่ขอบของสำเนาที่เหลื่อมออกไปให้เห็น ซึ่งคือเอฟเฟกต์ที่ต้องการ
            var wire = TitleLayer(group, "Title_Wire", new Vector2(-6f, 6f));
            wire.color        = Gold;
            wire.outlineColor = Teal;
            wire.outlineWidth = 0.18f;            // ≈ เส้น 2px ที่ fontSize 150

            var layered = group.gameObject.AddComponent<P3RLayeredText>();
            layered.source    = t;
            layered.followers = new[] { shadow, wire };

            ui.levelLabel = t;
            // หัวเรื่องเป็นสองบรรทัดตามแบบ — เลเวลใหม่ไปอยู่ในข้อความเดียวกันไม่ได้
            ui.titleFormat        = "LEVEL\nUP!";
            ui.titleFormatNoLevel = "LEVEL\nUP!";

            // เลเวลใหม่เป็น **ป้ายแยกใต้หัวเรื่อง**
            //
            // จอเดิมโชว์ "LEVEL UP!   Level 5" บรรทัดเดียว · พอมาเป็นแบบ P3R ที่หัวเรื่อง
            // เป็นสองบรรทัดตัวใหญ่มีเส้นขอบ เลขเลยถูกตัดทิ้งไปทั้งที่มันเป็นข้อมูลที่ผู้เล่น
            // เคยได้ — แยกป้ายจึงได้ทั้งทรงของแบบและข้อมูลที่หายไป
            // LevelUpUI ซ่อนป้ายนี้เองตอน Orb phase ที่ไม่มีเลเวลจะบอก
            var lv = NewMono("LevelValue", section, "Lv 1", 34f, 0.16f);
            lv.alignment = TextAlignmentOptions.MidlineLeft;
            lv.color     = Gold;
            var lrt = lv.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot     = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(420f, 46f);
            lrt.anchoredPosition = new Vector2(96f, -478f);
            ui.levelValueLabel = lv;
        }

        /// <summary>
        /// แม่แบบการ์ดหนึ่งใบ — **เป็นกลาง ไม่มีสีหรือข้อความประจำชนิด**
        ///
        /// ทุกอย่างที่ต่างกันตามชนิดการ์ด (สีเน้น · ชื่อ · ป้ายเลเวล · การยกใบแนะนำ)
        /// เป็นหน้าที่ของ `UpgradeCardUI.Populate` ตอนรัน · ค่าที่ใส่ตรงนี้เป็นแค่
        /// ค่าตั้งต้นให้เปิด prefab ดูแล้วเห็นโครง
        ///
        /// `parent = null` = สร้างลอยไว้ก่อน แล้ว SavePrefab เขียนลงไฟล์และลบตัวชั่วคราวทิ้ง
        /// (กติกาเดียวกับการ์ดตัวละคร/แผนที่)
        /// </summary>
        private static UpgradeCardUI BuildCardPrefab(UpgradeStatRowUI statRowTemplate,
                                                     float w, float h)
        {
            const string type = "", status = "", name = "";
            var accent = BlueSlot;              // ค่าตั้งต้นเฉยๆ · Populate ย้อมใหม่ทุกครั้ง
            var onAccent = Color.white;
            const bool recommended = false, useStatRows = true;

            var rt = NewRect("LevelUpCard", null);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;

            var card = rt.gameObject.AddComponent<UpgradeCardUI>();

            // **การ์ดทุกใบอยู่ระดับ Y เท่ากัน** — แบบเดิมยกใบแนะนำขึ้น 22px เป็นสัญญาณ
            // ลำดับชั้น แต่เจ้าของเลือกให้เรียบเสมอกัน · ป้าย "แนะนำ" ยังบอกใบที่แนะนำอยู่
            // ตั้งเป็น 0 ไม่ใช่ลบโค้ดทิ้ง — อยากได้กลับเมื่อไรติ๊กที่ Inspector ได้เลย
            card.recommendedLift = 0f;

            // ① พื้นการ์ด — ตัวรับเมาส์ของทั้งใบ
            var body = NewImage("Body", rt, CardBg);
            Stretch(body.rectTransform);
            body.raycastTarget = true;

            // ② กรอบ 2px — สีที่ใส่ตรงนี้เป็นแค่ค่าตั้งต้นให้ดูภาพต้นแบบออก
            //    ตอนรัน UpgradeCardUI ย้อมใหม่ตามชนิดการ์ดผ่าน accentTargets
            AddBorder(rt, "Border", 2f, accent);
            foreach (var edge in rt.Find("Border").GetComponentsInChildren<Image>(true))
                card.accentTargets.Add(new UpgradeCardUI.AccentTarget { graphic = edge, alpha = 1f });

            // ③ วงเรืองแสง 6px — ปิดไว้ เปิดเฉพาะตอนชี้ (LevelUpUI.glowOnlyOnHover)
            var glow = NewRect("Glow", rt);
            Stretch(glow);
            glow.offsetMin = new Vector2(-6f, -6f);
            glow.offsetMax = new Vector2(6f, 6f);
            AddBorder(glow, "GlowEdge", 6f, new Color(accent.r, accent.g, accent.b, 0.18f));
            glow.gameObject.SetActive(false);
            card.recommendedGlowOutline = glow.gameObject;
            foreach (var edge in glow.Find("GlowEdge").GetComponentsInChildren<Image>(true))
                card.accentTargets.Add(new UpgradeCardUI.AccentTarget { graphic = edge, alpha = 0.18f });

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
            card.accentTargets.Add(new UpgradeCardUI.AccentTarget { graphic = iconArea, alpha = 0.13f });
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
            P3RText.SetTracking(nameText, -3f);
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

            // แถวสเตตตัวอย่าง — UpgradeCardUI.Populate() ล้าง container แล้วสร้างใหม่จาก
            // GetStatChanges() ของการ์ดจริงตอนรัน ตัวอย่างจึงหายไปเองไม่ต้องตามลบ
            // แบบระบุว่าโชว์เฉพาะสเตตที่เปลี่ยน ไม่ใช่ยกตารางทั้งชุดมา — ตัวอย่างจึงมีแค่ 2–3 แถว

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

            return P3RBuilderKit.SavePrefab(rt.gameObject, CardPrefabPath).GetComponent<UpgradeCardUI>();
        }

        /// <summary>
        /// แถวสเตต ก่อน → หลัง หนึ่งแถว · เก็บไว้ในซีนแบบปิดไว้แล้วให้การ์ดใช้เป็นต้นแบบ
        /// (Instantiate ใช้ instance ในซีนเป็นต้นแบบได้ ไม่จำเป็นต้องเป็น prefab asset)
        /// </summary>
        /// <summary>
        /// แม่แบบแถวเทียบสเตตัส — **ต้องเป็น prefab ไม่ใช่ของในซีน**
        ///
        /// การ์ดถือ reference ตัวนี้ไว้ที่ `UpgradeCardUI.statRowPrefab` · ถ้าแม่แบบเป็น
        /// object ในซีน พอการ์ดถูกเซฟเป็น prefab asset reference นั้นจะกลายเป็น **null
        /// ทันทีและเงียบสนิท** — แถวเทียบสเตตัสจะไม่ขึ้นเลยตอนเล่น ทั้งที่ตอนเปิดซีน
        /// ต้นแบบดูยังมีอยู่ (เจอมาแล้วรอบนี้ จับได้จากภาพเรนเดอร์ ไม่ใช่จากคอมไพเลอร์)
        /// </summary>
        private static UpgradeStatRowUI BuildStatRowTemplate(RectTransform panel)
        {
            var rt = NewRect("StatRow_Template", null);
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

            return P3RBuilderKit.SavePrefab(rt.gameObject, StatRowPrefabPath)
                                .GetComponent<UpgradeStatRowUI>();

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
            P3RText.SetTracking(num, -4f);
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

            // ช่องเป็น **prefab asset** ไม่ใช่ลูกของแถบ — ของที่ถูก Instantiate ซ้ำ
            // สิบกว่าครั้งตอนรันต้องเป็น prefab ตามกติกาของโปรเจกต์
            // นิยามหน้าตาอยู่ที่ P3RBuildStripBuilder ที่เดียว ไม่ใช่ก๊อปมาไว้ที่นี่อีกชุด
            strip.slotTemplate = P3RBuildStripBuilder.BuildSlotPrefab(monoFont, displayFont);

            // เติมของตัวอย่างให้เห็นในซีน — ตอนรัน BuildStripUI.RefreshFromLocalPlayer()
            // ล้างแล้วสร้างใหม่จาก PlayerWeaponManager จริง ตัวอย่างจึงไม่กลายเป็นของค้าง
            // ช่องที่เหลือ SetEntries เติมเป็นช่องว่างกรอบเส้นประให้เอง
            strip.SetEntries(
                new[]
                {
                    new BuildStripUI.Entry { abbrev = "BLD", level = 3, highlight = true },
                    new BuildStripUI.Entry { abbrev = "ARC", level = 2 },
                    new BuildStripUI.Entry { abbrev = "ORB", level = 1 },
                },
                new[]
                {
                    new BuildStripUI.Entry { abbrev = "ATK", level = 2 },
                    new BuildStripUI.Entry { abbrev = "HST", level = 1 },
                });
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
            var edge = NewImage("Edge", row, accent);
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

        private static void SpawnStatRow(RectTransform parent, UpgradeStatRowUI template,
                                         string statName, string before, string after)
        {
            if (template == null) return;
            var row = Object.Instantiate(template, parent);
            row.gameObject.SetActive(true);
            row.SetData(statName, before, after);
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
            P3RText.SetTracking(t, letterSpacingEm * 100f);
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
