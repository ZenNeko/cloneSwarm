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
    /// สร้างซีนต้นแบบ **จอ Pause** ทั้งซีนด้วยโค้ด ตามตัวเลขทุกตัวใน design handoff
    /// (`design_handoff_ingame_screens/README.md` → "Screen 3 — Pause")
    ///
    /// **ทำไมเป็น Editor script ไม่ใช่ซีนที่จัดมือ** — เหตุผลเดียวกับ <see cref="P3RMenuSceneBuilder"/>:
    /// ซีน Unity เป็น YAML ที่แก้ด้วยมือแล้วพังเงียบ · สร้างด้วยโค้ดทำให้รันซ้ำได้ อ่าน diff รู้เรื่อง
    /// และเปลี่ยน layout ทีเดียวเห็นผลทั้งซีน · ลุคผ่านแล้วค่อยจัดมือต่อได้ตามสบาย
    ///
    /// เมนู: Tools > Clone Swarm > Build P3R Pause Scene
    ///
    /// ═══════════════════════════════════════════════════════════════════
    /// สิ่งที่ซีนนี้ **ไม่มี** และต้องรู้ก่อนเอาไปใช้จริง
    ///   • ไม่มี NetworkManager / ผู้เล่นจริง → แถวปาร์ตี้จะว่างเปล่าตอนกด Play ในซีนนี้
    ///     (PauseMenuUI อ่านจาก playermove ที่อยู่ในซีนจริงเท่านั้น) · ซีนนี้ใช้ดูทรงอย่างเดียว
    ///   • ไม่มี SoundManager → สไลเดอร์จะไม่ขยับอะไร แต่ลากได้ปกติ
    ///   • ป้าย CO-OP จะขึ้นสถานะ SOLO เสมอ เพราะไม่มี NetworkManager
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    public static class P3RPauseSceneBuilder
    {
        private const string ScenePath    = "Assets/GameScenes/Proto_Pause.unity";
        private const string UiAssetDir   = "Assets/ScriptableObjects/UI";
        private const string ThemePath    = UiAssetDir + "/P3RTheme_Pause.asset";
        private const string ScrimPath    = UiAssetDir + "/PauseScrim_Gradient.png";
        private const string FontHeavy    = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-ExtraBold SDF.asset";
        // ในโปรเจกต์ยังไม่มีฟอนต์ mono จริง (ไม่มี SDF ตัวไหนเป็น monospace เลย)
        // handoff เรียกกลุ่มนี้ว่า "mono" แต่สิ่งที่มันสื่อจริงๆ คือ "ป้ายระบบ ตัวเล็ก ตัวห่าง"
        // Sarabun-SemiBold + characterSpacing บวก ให้ผลตานั้นได้โดยไม่ต้องเพิ่มฟอนต์ใหม่เข้าโปรเจกต์
        private const string FontMono     = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-SemiBold SDF.asset";
        private const string SfxDir       = "Assets/Free UI Click Sound Effects Pack/AUDIO/Button/";

        // ── layout ที่กรอบ 1920×1080 (ทุกตัวเลขอ่านตรงจาก handoff) ─────────
        private const float RefWidth   = 1920f;
        private const float RefHeight  = 1080f;

        private const float MarginLeft  = 104f;   // หัวเรื่อง + รายการเมนู
        private const float MarginRight = 130f;   // คอลัมน์ขวา (ปาร์ตี้ + ป้าย)

        private const float TitleTop      = 88f;
        private const float TitleSize     = 96f;
        private const float TitleScaleX   = 0.84f;
        private const float TitleGapHint  = 18f;

        private const float MenuTop       = 300f;
        private const float RowHeight     = 88f;
        private const float RowNameSize   = 62f;
        private const float RowOrderSize  = 16f;
        private const float RowOrderWidth = 36f;
        private const float RowOrderGap   = 16f;
        private const float BarWidth      = 760f;
        private const float BarHeight     = 58f;
        private const float BarLeftBleed  = -300f;  // ยื่นซ้ายออกนอกจอ
        private const float ShearDegrees  = 9f;     // = CSS skewX(-9deg) · ดูหมายเหตุใน UIShear

        private const float PartyWidth    = 760f;
        private const float PartyRowH     = 90f;
        private const float PartyGap      = 12f;
        private const float PartyBottom   = 236f;
        private const float PortraitSize  = 56f;
        private const float NameBlockW    = 190f;
        private const float RowPadX       = 22f;
        private const float RowInnerGap   = 18f;
        private const float AccentWidth   = 6f;
        private const float HpBarHeight   = 9f;

        private const float BadgeWidth    = 620f;
        private const float BadgeBottom   = 104f;

        // แผงย่อยเสียง — **ไม่มีในแบบ** เป็นทางประนีประนอมเพราะซีนเกมยังไม่มีจอ Config
        // (ดูคำอธิบายเต็มหัวไฟล์ PauseMenuUI.cs) ตัวเลขชุดนี้จึงเลือกเองให้เข้าชุดกับที่เหลือ
        private const float SubPanelWidth  = 620f;
        private const float SubPanelHeight = 372f;
        private const float SubPanelTop    = 300f;

        // ── palette (design tokens) ─────────────────────────────────────────
        private static readonly Color Ink       = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
        private static readonly Color Card      = new Color32(0x11, 0x18, 0x38, 0xFF);
        private static readonly Color Panel     = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color Primary   = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color Teal      = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color Faint     = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color FrameLine = new Color(1f, 1f, 1f, 0.24f);
        private static readonly Color FrameFill = new Color(1f, 1f, 1f, 0.07f);
        private static readonly Color TrackFill = new Color(1f, 1f, 1f, 0.14f);

        /// <summary>id ต้องตรงกับค่าคงที่ใน <see cref="PauseMenuUI"/> ไม่งั้นกดแล้วไม่มีอะไรเกิดขึ้น</summary>
        private static readonly (string id, string order, string label)[] Items =
        {
            (PauseMenuUI.MenuIdResume,   "01", "กลับเข้าเกม"),
            (PauseMenuUI.MenuIdSettings, "02", "ตั้งค่า"),
            (PauseMenuUI.MenuIdQuit,     "03", "ออกไปเมนูหลัก"),
        };

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Build P3R Pause Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "สร้างซีนต้นแบบจอ Pause ใหม่",
                    $"{ScenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือไว้ในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var theme     = LoadOrCreateTheme();
            var fontMono  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMono);
            var scrim     = LoadOrCreateScrimSprite();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();
            var canvas = BuildCanvas();

            // PauseMenuUI ต้องอยู่ **นอก** panelRoot — Start() ของมันสั่ง panelRoot.SetActive(false)
            // ถ้าตัวมันอยู่ข้างในด้วย มันจะปิดตัวเองแล้ว Update() ไม่ทำงาน กด ESC ไม่ติด
            var host = NewRect("PauseMenu", canvas.transform);
            Stretch(host);
            var ui = host.gameObject.AddComponent<PauseMenuUI>();

            var panel = NewRect("PausePanel", host);
            Stretch(panel);
            ui.panelRoot = panel.gameObject;

            BuildScrim(panel, scrim);
            BuildTitle(panel, theme, fontMono);
            BuildMenuList(panel, theme, fontMono, ui);
            BuildPartyColumn(panel, theme, fontMono, ui);
            BuildCoopBadge(panel, theme, fontMono, ui);
            BuildSettingsSubPanel(panel, theme, fontMono, ui);

            EditorUtility.SetDirty(ui);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ScenePath));
            Debug.Log(
                $"[P3R Pause] สร้าง {ScenePath} เรียบร้อย\n" +
                $"  theme = {ThemePath} · ฉากทับ = {ScrimPath}\n" +
                "ขั้นถัดไป:\n" +
                "  1) กด Play แล้วกด ESC เพื่อเปิด/ปิด · ลูกศร/WASD เลื่อน · Enter ยืนยัน · เมาส์ชี้ = เลือก\n" +
                "  2) แถวปาร์ตี้จะว่างในซีนนี้ (ไม่มี playermove จริง) — ต้องดูของจริงใน SampleScene\n" +
                "  3) จูนลุคที่ P3RTheme_Pause.asset ตัวเดียว — อย่าไปแก้ P3RTheme.asset ของเมนูหลัก\n" +
                "  ⚠ ถ้าจะยกไปใส่ SampleScene: ก๊อป PauseMenu ทั้งกิ่งไปวาง แล้วต่อสายให้ครบ\n" +
                "     PauseMenuUI ตัวเดิมในซีนนั้นต่อ slider/label/button ไว้แล้ว ห้ามลบทิ้ง");
        }

        // ═══════════════════════════════════════════════════════════════════
        // THEME — asset แยกจากเมนูหลัก
        //
        // **การแม็ปค่าแถบเลือกกับ P3RTheme (สำคัญ อ่านก่อนแก้ตัวเลข)**
        // P3RMenuItem.Apply() คำนวณแถบแบบนี้ตายตัว:
        //     sizeDelta        = (barExtendLeft + barBleedRight, barHeight)
        //     anchoredPosition = (barBleedRight, barOffsetY)
        // ชื่อฟิลด์ตั้งมาสำหรับเมนูหลักซึ่ง **ชิดขวา** (pivot ขวา แถบกางจากขวาไปซ้าย)
        // จอ pause ชิดซ้าย (pivot ซ้าย แถบกางจากซ้ายไปขวา) ความหมายจึงกลับด้าน:
        //     barBleedRight  = ระยะที่แถบยื่นเลย "ขอบซ้าย" ของรายการ · ค่าลบ = ยื่นออกนอกจอ = -300
        //     barExtendLeft  = ตัวเติมให้ผลรวมเท่าความกว้างจริง 760  → 760 - (-300) = 1060
        // เลือกทางนี้แทนการ hardcode rect ในตัวสร้าง เพราะจะได้มีตัวเลขชุดเดียวที่ designer จูนได้
        // ═══════════════════════════════════════════════════════════════════
        private static P3RTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<P3RTheme>(ThemePath);
            if (existing != null) return existing;   // มีแล้วไม่ทับ — ค่าที่จูนไว้ต้องรอด

            if (!AssetDatabase.IsValidFolder(UiAssetDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "UI");

            var t = ScriptableObject.CreateInstance<P3RTheme>();
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontHeavy);
            if (t.font == null)
                Debug.LogWarning($"[P3R Pause] หา font ไม่เจอที่ {FontHeavy} — ต้องลากใส่ P3RTheme_Pause.font เอง");

            t.barColor        = Primary;
            t.textNormal      = Color.white;
            t.textSelected    = Color.white;   // แบบระบุว่าต่างกันที่แถบข้างหลัง ไม่ใช่สีตัวอักษร
            t.textDisabled    = new Color32(0x8C, 0x86, 0x78, 0xFF);
            t.selectedOutline = 0f;

            t.fontSize        = RowNameSize;
            t.horizontalScale = 1f;    // แถวเมนูไม่บีบ — บีบเฉพาะหัวเรื่อง PAUSED (scaleX .84)
            t.characterSpacing = -3.5f; // = letter-spacing -0.035em ตามหน่วยที่ P3RTheme ใช้อยู่
            t.rowPitch        = RowHeight;

            t.barHeight       = BarHeight;
            t.barBleedRight   = BarLeftBleed;                 // ดูบล็อกคำอธิบายข้างบน
            t.barExtendLeft   = BarWidth - BarLeftBleed;      // 760 - (-300) = 1060
            t.barOffsetY      = 3f;                           // ชดเชย cap height ของ Sarabun ที่ 62px

            t.introSlideDistance = 260f;   // สั้นกว่าเมนูหลัก — จอนี้เป็น overlay ต้องเข้าที่ไว
            t.introStagger       = 0.05f;
            t.introDuration      = 0.24f;
            t.barTweenDuration   = 0.14f;
            t.overshoot          = 1.7f;

            t.moveSfx    = LoadClip("SFX_UI_Button_Organic_Plastic_Thin_Generic_1.wav");
            t.confirmSfx = LoadClip("SFX_UI_Button_Keyboard_Enter_Thick_1.wav");
            t.cancelSfx  = LoadClip("SFX_UI_Button_Organic_Plastic_Thin_Negative_Back_1.wav");

            AssetDatabase.CreateAsset(t, ThemePath);
            AssetDatabase.SaveAssets();
            return t;
        }

        private static AudioClip LoadClip(string file) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + file);

        // ═══════════════════════════════════════════════════════════════════
        // SCRIM — ฉากทับไล่เฉดแนวนอน
        //
        // uGUI ไม่มี gradient ในตัว · ทางเลือกที่พิจารณา:
        //   (ก) ซ้อน Image หลายใบไล่ alpha  → 12-24 draw call และเห็นรอยต่อเป็นแถบบนพื้นเกือบดำ
        //   (ข) เขียน BaseMeshEffect ระบายสี vertex → ต้องเพิ่มไฟล์สคริปต์ใหม่นอกขอบเขตงานนี้
        //   (ค) **สร้าง PNG ไล่เฉดตอน build แล้วอ้างเป็น Sprite** ← เลือกอันนี้
        // (ค) ให้ draw call เดียว ไล่เฉดเนียนจริง ตรวจสอบด้วยตาในโปรเจกต์ได้ และตัวไฟล์
        // เป็นผลผลิตของ builder เอง (รันซ้ำแล้วไม่สร้างซ้ำ ถ้ามีอยู่แล้วจะใช้ของเดิม)
        //
        // CSS: linear-gradient(90deg, rgba(6,8,18,.96) 0%, rgba(6,8,18,.88) 48%, rgba(6,8,18,.5) 100%)
        // 90deg ของ CSS = ซ้ายไปขวา · ฝั่งขวาจางกว่าเพื่อให้ยังเห็น gameplay
        // ═══════════════════════════════════════════════════════════════════
        private static Sprite LoadOrCreateScrimSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ScrimPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(UiAssetDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "UI");

            const int w = 256, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px  = new Color32[w * h];

            for (int x = 0; x < w; x++)
            {
                float u = w == 1 ? 0f : x / (float)(w - 1);
                // สามจุดหยุดตาม CSS · แบ่งสองช่วงเชิงเส้นที่ 48%
                float a = u <= 0.48f
                    ? Mathf.Lerp(0.96f, 0.88f, u / 0.48f)
                    : Mathf.Lerp(0.88f, 0.50f, (u - 0.48f) / 0.52f);

                var c = new Color32(6, 8, 18, (byte)Mathf.RoundToInt(a * 255f));
                for (int y = 0; y < h; y++) px[y * w + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(ScrimPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(ScrimPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(ScrimPath) is TextureImporter imp)
            {
                imp.textureType         = TextureImporterType.Sprite;
                imp.spriteImportMode    = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled       = false;
                imp.wrapMode            = TextureWrapMode.Clamp;
                imp.filterMode          = FilterMode.Bilinear;
                imp.npotScale           = TextureImporterNPOTScale.None;
                // บีบอัดแล้วไล่เฉดจะเป็นแถบทันที — ภาพนี้เล็กมาก (256×4) ไม่ต้องประหยัด
                imp.textureCompression  = TextureImporterCompression.Uncompressed;
                imp.maxTextureSize      = 256;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(ScrimPath);
        }

        // ═══════════════════════════════════════════════════════════════════
        // SCENE PARTS
        // ═══════════════════════════════════════════════════════════════════
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
            var go = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // ทับ HUD แต่ยังต่ำกว่าจอ Level Up / Win-Lose

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            return canvas;
        }

        private static void BuildScrim(RectTransform panel, Sprite scrim)
        {
            var img = NewImage("Scrim", panel, Color.white);
            Stretch(img.rectTransform);
            img.sprite = scrim;
            img.type   = Image.Type.Simple;
            // ต้องกินคลิก ไม่งั้นเมาส์ทะลุไปโดน gameplay ที่อยู่ข้างหลังตอนเมนูเปิด
            img.raycastTarget = true;

            if (scrim == null)
                Debug.LogWarning("[P3R Pause] ไม่มี sprite ฉากทับ — จะได้พื้นขาวทึบแทน ให้รัน builder ใหม่");
        }

        private static void BuildTitle(RectTransform panel, P3RTheme theme, TMP_FontAsset mono)
        {
            var title = NewText("Title", panel, "PAUSED");
            SetFont(title, theme.font);
            title.fontSize         = TitleSize;
            title.lineSpacing      = -10f;    // = line-height 0.9 โดยประมาณของ TMP
            title.characterSpacing = -4f;
            title.color            = Color.white;
            title.alignment        = TextAlignmentOptions.TopLeft;
            var trt = title.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot     = new Vector2(0f, 1f);
            trt.sizeDelta = new Vector2(900f, TitleSize * 1.1f);
            trt.anchoredPosition = new Vector2(MarginLeft, -TitleTop);
            // บีบแนวนอนแบบเดียวกับหัวเรื่องอื่นในระบบ — TMP ไม่มี condensed จริง
            trt.localScale = new Vector3(TitleScaleX, 1f, 1f);

            var hint = NewText("TitleHint", panel, "ESC เพื่อกลับเข้าเกม");
            SetFont(hint, mono);
            hint.fontSize         = 17f;
            hint.characterSpacing = 28f;   // = .28em
            hint.color            = new Color(1f, 1f, 1f, 0.55f);
            hint.alignment        = TextAlignmentOptions.TopLeft;
            var hrt = hint.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot     = new Vector2(0f, 1f);
            hrt.sizeDelta = new Vector2(900f, 30f);
            hrt.anchoredPosition = new Vector2(MarginLeft, -(TitleTop + TitleSize * 0.9f + TitleGapHint));
        }

        // ── รายการเมนู ─────────────────────────────────────────────────────
        private static void BuildMenuList(RectTransform panel, P3RTheme theme, TMP_FontAsset mono, PauseMenuUI ui)
        {
            var rt = NewRect("MenuList", panel);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(BarWidth, RowHeight * Items.Length);
            rt.anchoredPosition = new Vector2(MarginLeft, -MenuTop);

            var list = rt.gameObject.AddComponent<P3RMenuList>();
            list.theme = theme;
            // ESC เป็นของ PauseMenuUI คนเดียว (ดูคำอธิบายใน PauseMenuUI.Start)
            list.listenEscape = false;

            for (int i = 0; i < Items.Length; i++)
                list.items.Add(BuildItem(rt, theme, mono, Items[i], i));

            list.SetIndex(0, instant: true, playSfx: false);
            ui.menuList = list;
            EditorUtility.SetDirty(rt.gameObject);
        }

        private static P3RMenuItem BuildItem(RectTransform parent, P3RTheme theme, TMP_FontAsset mono,
                                             (string id, string order, string label) data, int index)
        {
            var rt = NewRect($"Item_{data.id}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(BarWidth, RowHeight);
            rt.anchoredPosition = new Vector2(0f, -(index * RowHeight + RowHeight * 0.5f));

            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var item  = rt.gameObject.AddComponent<P3RMenuItem>();

            // ① พื้นที่รับเมาส์ — alpha 0 แต่ยัง raycast ได้ · event bubble ขึ้นไปที่ P3RMenuItem
            var hit = NewImage("HitArea", rt, new Color(1f, 1f, 1f, 0f));
            Stretch(hit.rectTransform);
            hit.raycastTarget = true;

            // ② แถบ — pivot **ซ้าย** (ตรงข้ามเมนูหลัก) เพื่อให้ scale.x กางจากซ้ายไปขวา
            //    ขนาด/ตำแหน่งมาจาก theme ตอน Apply() · ดูบล็อกการแม็ปค่าที่ LoadOrCreateTheme
            var bar = NewImage("Bar", rt, theme.barColor);
            var brt = bar.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot     = new Vector2(0f, 0.5f);
            bar.gameObject.AddComponent<UIShear>().angleDegrees = ShearDegrees;

            // ③ เลขลำดับ — ป้ายนิ่ง ไม่มีใครขับตอนรัน จึงไม่ต้องเก็บ reference
            var order = NewText("Order", rt, data.order);
            SetFont(order, mono);
            order.fontSize         = RowOrderSize;
            order.characterSpacing = 12f;
            order.color            = new Color(1f, 1f, 1f, 0.55f);
            order.alignment        = TextAlignmentOptions.MidlineLeft;
            var ort = order.rectTransform;
            ort.anchorMin = ort.anchorMax = new Vector2(0f, 0.5f);
            ort.pivot     = new Vector2(0f, 0.5f);
            ort.sizeDelta = new Vector2(RowOrderWidth, RowHeight);
            ort.anchoredPosition = new Vector2(0f, 0f);

            // ④ ชื่อรายการ
            var label = NewText("Label", rt, data.label);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot     = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(BarWidth - RowOrderWidth - RowOrderGap, RowHeight);
            lrt.anchoredPosition = new Vector2(RowOrderWidth + RowOrderGap, 0f);

            item.id           = data.id;
            item.labelText    = data.label;
            item.interactable = true;
            item.label        = label;
            item.bar          = bar;
            item.group        = group;
            item.Apply(theme);

            // Apply() ฮาร์ดโค้ด alignment = Right (เมนูหลักชิดขวา) — ทับหลังเรียกเสมอ
            // ตอนรัน PauseMenuUI.ApplyLeftAlignedRows() ทำซ้ำให้อีกครั้ง เพราะ P3RMenuList.Awake
            // จะเรียก Apply() ใหม่ทุกครั้งที่ panel ถูกเปิด
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return item;
        }

        // ── คอลัมน์ปาร์ตี้ ─────────────────────────────────────────────────
        private static void BuildPartyColumn(RectTransform panel, P3RTheme theme, TMP_FontAsset mono, PauseMenuUI ui)
        {
            // pivot ล่าง → PauseMenuUI เพิ่ม sizeDelta.y ตามจำนวนคน แล้วแถวงอกขึ้นบน
            // ขอบล่างของแถวสุดท้ายจึงอยู่ที่ 236 เสมอ ไม่ว่าจะ 1 คนหรือ 4 คน
            var col = NewRect("PartyColumn", panel);
            col.anchorMin = col.anchorMax = new Vector2(1f, 0f);
            col.pivot     = new Vector2(1f, 0f);
            col.sizeDelta = new Vector2(PartyWidth, 0f);
            col.anchoredPosition = new Vector2(-MarginRight, PartyBottom);

            ui.partyRowContainer = col;
            ui.partyRowHeight    = PartyRowH;
            ui.partyRowSpacing   = PartyGap;
            ui.partyRowTemplate  = BuildPartyRowTemplate(col, theme, mono);
        }

        /// <summary>
        /// แถวต้นแบบที่ <c>PauseMenuUI</c> โคลนออกไปตามจำนวนคนจริงในห้อง
        /// เก็บเป็น GameObject ปิดอยู่ในซีน ไม่ใช่ prefab แยก — แถวนี้ใช้ที่จอนี้จอเดียว
        /// ทำเป็น prefab แล้วต้องคอยตามซิงก์ตอนปรับดีไซน์โดยไม่ได้อะไรกลับมา
        /// </summary>
        private static PausePartyRowUI BuildPartyRowTemplate(RectTransform parent, P3RTheme theme, TMP_FontAsset mono)
        {
            var bg = NewImage("PartyRow_Template", parent, Card);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(PartyWidth, PartyRowH);
            rt.anchoredPosition = Vector2.zero;

            var row = bg.gameObject.AddComponent<PausePartyRowUI>();
            bg.gameObject.AddComponent<CanvasGroup>();
            row.background = bg;

            // ── เส้นเน้นซ้าย 6px — ลายเซ็นของการ์ดทุกใบในระบบ
            var accent = NewImage("Accent", rt, Primary);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot     = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(AccentWidth, 0f);
            art.anchoredPosition = new Vector2(AccentWidth * 0.5f, 0f);
            row.accent = accent;

            // ── พอร์เทรต · เนื้อหาเริ่มหลังเส้นเน้น (border-box ของ CSS)
            float contentX = AccentWidth + RowPadX;
            var frame = NewImage("PortraitFrame", rt, FrameFill);
            var frt = frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot     = new Vector2(0f, 0.5f);
            frt.sizeDelta = new Vector2(PortraitSize, PortraitSize);
            frt.anchoredPosition = new Vector2(contentX, 0f);
            row.portraitFrame = frame;

            var portrait = NewImage("Portrait", frt, Color.white);
            Stretch(portrait.rectTransform);
            portrait.preserveAspect = true;
            portrait.enabled = false;   // ยังไม่มี Char_*.portrait ครบ — เปิดเมื่อมี sprite จริง
            row.portrait = portrait;

            // ── บล็อกชื่อ กว้างคงที่ 190
            float nameX = contentX + PortraitSize + RowInnerGap;
            var nameText = NewText("Name", rt, "Gunner");
            SetFont(nameText, theme.font);
            nameText.fontSize  = 27f;
            nameText.characterSpacing = -2f;
            nameText.color     = Color.white;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            PlaceLeft(nameText.rectTransform, nameX, NameBlockW, 34f, 13f);
            row.nameText = nameText;

            var subText = NewText("Sub", rt, "P1 · HOST");
            SetFont(subText, mono);
            subText.fontSize  = 15f;
            subText.characterSpacing = 16f;   // = .16em
            subText.color     = new Color(1f, 1f, 1f, 0.55f);
            subText.alignment = TextAlignmentOptions.MidlineLeft;
            PlaceLeft(subText.rectTransform, nameX, NameBlockW, 22f, -14f);
            row.subText = subText;

            // ── ฝั่งขวา: Lv ซ้าย / HP ขวา / หลอดใต้ลงมา
            float vitalsX = nameX + NameBlockW + RowInnerGap;
            float vitalsW = PartyWidth - vitalsX - RowPadX;

            var levelText = NewText("Level", rt, "Lv 14");
            SetFont(levelText, mono);
            levelText.fontSize  = 15f;
            levelText.characterSpacing = 10f;
            levelText.color     = new Color(1f, 1f, 1f, 0.5f);
            levelText.alignment = TextAlignmentOptions.MidlineLeft;
            PlaceLeft(levelText.rectTransform, vitalsX, vitalsW * 0.5f, 24f, 12f);
            row.levelText = levelText;

            var hpText = NewText("Hp", rt, "182 / 220");
            SetFont(hpText, mono);
            hpText.fontSize  = 18f;
            hpText.characterSpacing = 8f;
            hpText.color     = new Color(1f, 1f, 1f, 0.75f);
            hpText.alignment = TextAlignmentOptions.MidlineRight;
            PlaceLeft(hpText.rectTransform, vitalsX + vitalsW * 0.5f, vitalsW * 0.5f, 24f, 12f);
            row.hpText = hpText;

            var track = NewImage("HpTrack", rt, TrackFill);
            PlaceLeft(track.rectTransform, vitalsX, vitalsW, HpBarHeight, -12f);

            var fill = NewImage("HpFill", track.rectTransform, Teal);
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;   // SetVitals ย่อ anchorMax.x ตามสัดส่วน HP
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            row.hpFill      = fr;
            row.hpFillImage = fill;

            bg.gameObject.SetActive(false);   // ต้นแบบ — ห้ามโชว์
            return row;
        }

        /// <summary>วางกล่องชิดซ้ายของแถว โดยยึดกึ่งกลางแนวตั้งแล้วเลื่อนด้วย offsetY</summary>
        private static void PlaceLeft(RectTransform rt, float x, float w, float h, float offsetY)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, offsetY);
        }

        // ── ป้าย CO-OP / SOLO ──────────────────────────────────────────────
        private static void BuildCoopBadge(RectTransform panel, P3RTheme theme, TMP_FontAsset mono, PauseMenuUI ui)
        {
            var bg = NewImage("CoopBadge", panel, new Color(216f / 255f, 32f / 255f, 32f / 255f, 0.1f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(BadgeWidth, 120f);
            rt.anchoredPosition = new Vector2(-MarginRight, BadgeBottom);

            // ข้อความ co-op ยาวกว่า solo มาก → ปล่อยให้สูงตามเนื้อหา
            // pivot ล่าง ทำให้ขอบล่างค้างที่ 104 เสมอ ป้ายโตขึ้นบนแทน
            var vlg = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)(AccentWidth + 24f), 24, 20, 20);
            vlg.spacing = 8f;
            vlg.childControlWidth  = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var accent = NewImage("Accent", rt, new Color32(0xD8, 0x20, 0x20, 0xFF));
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot     = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(AccentWidth, 0f);
            art.anchoredPosition = new Vector2(AccentWidth * 0.5f, 0f);
            // ต้องหลุดจาก layout ไม่งั้น VerticalLayoutGroup จะจับมันมาต่อแถวกับตัวหนังสือ
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ui.coopBadgeAccent = accent;

            var tag = NewText("Tag", rt, "CO-OP");
            SetFont(tag, mono);
            tag.fontSize         = 15f;
            tag.characterSpacing = 20f;   // = .2em
            tag.color            = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
            tag.alignment        = TextAlignmentOptions.TopLeft;
            AddLayoutHeight(tag.gameObject, 20f);

            var body = NewText("Text", rt, "อยู่ในห้องกับเพื่อน — โลกยังเดินอยู่ ตัวละครยังโดนตีได้");
            SetFont(body, theme.font);
            body.fontSize          = 24f;
            body.lineSpacing       = 8f;   // = line-height 1.4 โดยประมาณ
            body.color             = new Color(1f, 1f, 1f, 0.85f);
            body.alignment         = TextAlignmentOptions.TopLeft;
            body.textWrappingMode  = TextWrappingModes.Normal;

            ui.coopBadgeRoot       = bg.gameObject;
            ui.coopBadgeBackground = bg;
            ui.coopBadgeTag        = tag;
            ui.coopBadgeText       = body;
        }

        // ── แผงย่อยตั้งค่าเสียง (ทางประนีประนอม — ไม่มีในแบบ) ──────────────
        private static void BuildSettingsSubPanel(RectTransform panel, P3RTheme theme, TMP_FontAsset mono, PauseMenuUI ui)
        {
            var bg = NewImage("SettingsSubPanel", panel, Panel);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(SubPanelWidth, SubPanelHeight);
            rt.anchoredPosition = new Vector2(-MarginRight, -SubPanelTop);
            bg.raycastTarget = true;   // กันคลิกทะลุไปโดนแถวเมนูที่อยู่ข้างหลัง

            var accent = NewImage("Accent", rt, Primary);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot     = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(AccentWidth, 0f);
            art.anchoredPosition = new Vector2(AccentWidth * 0.5f, 0f);

            var header = NewText("Header", rt, "ตั้งค่าเสียง");
            SetFont(header, mono);
            header.fontSize         = 15f;
            header.characterSpacing = 24f;   // = .24em
            header.color            = new Color(1f, 1f, 1f, 0.6f);
            header.alignment        = TextAlignmentOptions.TopLeft;
            var hrt = header.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot     = new Vector2(0f, 1f);
            hrt.sizeDelta = new Vector2(400f, 24f);
            hrt.anchoredPosition = new Vector2(30f, -22f);

            ui.masterSlider = BuildSliderRow(rt, theme, mono, "MASTER", -70f, out var masterLabel);
            ui.musicSlider  = BuildSliderRow(rt, theme, mono, "MUSIC",  -134f, out var musicLabel);
            ui.sfxSlider    = BuildSliderRow(rt, theme, mono, "SFX",    -198f, out var sfxLabel);
            ui.masterValueText = masterLabel;
            ui.musicValueText  = musicLabel;
            ui.sfxValueText    = sfxLabel;

            ui.resetDefaultsButton = BuildButton(rt, theme, "คืนค่าเริ่มต้น", new Vector2(30f, 26f),
                                                 new Vector2(0f, 0f), new Vector2(0f, 0f), primary: false);
            ui.settingsBackButton  = BuildButton(rt, theme, "ย้อนกลับ", new Vector2(-30f, 26f),
                                                 new Vector2(1f, 0f), new Vector2(1f, 0f), primary: true);

            ui.settingsSubPanel = bg.gameObject;
            bg.gameObject.SetActive(false);
        }

        private static Slider BuildSliderRow(RectTransform parent, P3RTheme theme, TMP_FontAsset mono,
                                             string caption, float y, out TextMeshProUGUI valueText)
        {
            var label = NewText($"Label_{caption}", parent, caption);
            SetFont(label, mono);
            label.fontSize         = 15f;
            label.characterSpacing = 20f;
            label.color            = new Color(1f, 1f, 1f, 0.7f);
            label.alignment        = TextAlignmentOptions.MidlineLeft;
            PlaceTopLeft(label.rectTransform, 30f, y, 150f, 40f);

            var sliderRt = NewRect($"Slider_{caption}", parent);
            PlaceTopLeft(sliderRt, 190f, y, 320f, 40f);
            var slider = sliderRt.gameObject.AddComponent<Slider>();

            var track = NewImage("Background", sliderRt, TrackFill);
            var trt = track.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(1f, 0.5f);
            trt.pivot     = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(0f, HpBarHeight);
            trt.anchoredPosition = Vector2.zero;
            track.raycastTarget = true;

            var fillArea = NewRect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot     = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, HpBarHeight);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = NewImage("Fill", fillArea, theme.barColor);
            Stretch(fill.rectTransform);

            var handleArea = NewRect("Handle Slide Area", sliderRt);
            Stretch(handleArea);
            var handle = NewImage("Handle", handleArea, Color.white);
            var hrt = handle.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot     = new Vector2(0.5f, 0.5f);
            hrt.sizeDelta = new Vector2(16f, -8f);
            handle.raycastTarget = true;

            slider.fillRect      = fill.rectTransform;
            slider.handleRect    = hrt;
            slider.targetGraphic = handle;
            slider.direction     = Slider.Direction.LeftToRight;
            slider.minValue      = 0f;
            slider.maxValue      = 1f;
            slider.wholeNumbers  = false;
            slider.value         = 1f;

            valueText = NewText($"Value_{caption}", parent, "100%");
            SetFont(valueText, mono);
            valueText.fontSize  = 18f;
            valueText.color     = Color.white;
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            PlaceTopLeft(valueText.rectTransform, 520f, y, 70f, 40f);

            return slider;
        }

        /// <summary>
        /// ปุ่มทรงเดียวกับที่ handoff กำหนด: **พื้นหลังเอียง ตัวหนังสือไม่เอียง**
        /// (UIShear ใช้กับ TextMeshProUGUI ไม่ได้อยู่แล้ว — ข้อจำกัดตรงกับแบบพอดี)
        /// </summary>
        private static Button BuildButton(RectTransform parent, P3RTheme theme, string caption,
                                          Vector2 pos, Vector2 anchor, Vector2 pivot, bool primary)
        {
            var bg = NewImage($"Btn_{caption}", parent, primary ? theme.barColor : new Color(1f, 1f, 1f, 0.08f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = pivot;
            rt.sizeDelta = new Vector2(220f, 56f);
            rt.anchoredPosition = pos;
            bg.raycastTarget = true;
            bg.gameObject.AddComponent<UIShear>().angleDegrees = ShearDegrees;

            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;

            var label = NewText("Label", rt, caption);
            SetFont(label, theme.font);
            label.fontSize  = 24f;
            label.color     = primary ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            label.alignment = TextAlignmentOptions.Midline;
            Stretch(label.rectTransform);
            return btn;
        }

        private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static void AddLayoutHeight(GameObject go, float h)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight       = h;
            le.preferredHeight = h;
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS — สำเนาทรงเดียวกับ P3RMenuSceneBuilder ตั้งใจให้อ่านคู่กันได้
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
            return t;
        }

        /// <summary>ใส่ฟอนต์เฉพาะตอนที่มีจริง — set null จะทำให้ TMP ตกกลับไปฟอนต์ default เงียบๆ</summary>
        private static void SetFont(TMP_Text t, TMP_FontAsset font)
        {
            if (font != null) t.font = font;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
