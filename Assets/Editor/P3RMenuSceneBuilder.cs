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
    /// สร้างซีนต้นแบบเมนูหลักสไตล์ Persona 3 Reload ทั้งซีนด้วยโค้ด
    ///
    /// **ทำไมถึงเป็น Editor script ไม่ใช่ซีนที่จัดมือ**
    /// ซีน Unity เป็น YAML ที่แก้ด้วยมือแล้วพังเงียบ · การสร้างด้วยโค้ดทำให้
    /// รันซ้ำได้ · อ่าน diff รู้เรื่อง · และเปลี่ยน layout ทีเดียวเห็นผลทั้งซีน
    /// ถ้าลุคผ่านแล้วค่อยจัดมือต่อได้ตามสบาย — ตัวนี้แค่พาไปถึงจุดเริ่มต้นที่ดู "ใช่" แล้ว
    ///
    /// เมนู: Tools > Clone Swarm > Build P3R Main Menu Scene
    /// </summary>
    public static class P3RMenuSceneBuilder
    {
        private const string ScenePath  = "Assets/GameScenes/Proto_P3RMenu.unity";
        private const string ThemeDir   = "Assets/ScriptableObjects/UI";
        private const string ThemePath  = ThemeDir + "/P3RTheme.asset";
        private const string FontPath   = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-ExtraBold SDF.asset";
        private const string SfxDir     = "Assets/Free UI Click Sound Effects Pack/AUDIO/Button/";

        // ── layout ที่ 1920x1080 ────────────────────────────────────────────
        private const float RefWidth    = 1920f;
        private const float RefHeight   = 1080f;
        private const float MarginRight = 130f;   // ต้องเท่ากับ P3RTheme.barBleedRight แถบถึงจะไปจบพอดีขอบจอ
        private const float BlockOffsetY = -40f;  // เลื่อนกลุ่มเมนูลงต่ำกว่ากึ่งกลางเล็กน้อยตามภาพต้นแบบ
        private const float ItemWidth   = 900f;   // ความกว้างพื้นที่รับเมาส์ วัดจากขอบขวาเข้ามา

        /// <summary>
        /// รายการเมนู — ใช้ชื่อจริงของเกม ไม่ใช่ของ P3R
        /// จงใจ เพราะสิ่งที่ต้องทดสอบคือ "คำของเราความยาวเท่านี้ ยังดูดีไหม"
        /// ไม่ใช่ "NEW GAME ดูดีไหม" ซึ่งเรารู้คำตอบอยู่แล้ว
        /// id ตรงกับปุ่มเดิมใน MenuManager.MainPanel เพื่อให้ย้ายไปต่อของจริงได้ทีหลัง
        /// </summary>
        private static readonly (string id, string label, bool enabled)[] Items =
        {
            ("play",     "PLAY",        true),
            ("join",     "JOIN ROOM",   true),
            ("continue", "CONTINUE",    false),  // ตัวเทา — ไว้ทดสอบสถานะกดไม่ได้ (เทียบ CHANGE EPISODE)
            ("shop",     "TALENT SHOP", true),
            ("settings", "CONFIG",      true),
            ("quit",     "QUIT",        true),
        };

        private static readonly (string key, string label)[] KeyHints =
        {
            ("Z",   "Credits"),
            ("Esc", "Quit"),
        };

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Build P3R Main Menu Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "สร้างซีนต้นแบบ P3R ใหม่",
                    $"{ScenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือไว้ในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var theme = LoadOrCreateTheme();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildBackground(canvas);
            BuildMainPanel(canvas, theme);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ScenePath));
            Debug.Log(
                $"[P3R] สร้าง {ScenePath} เรียบร้อย · theme อยู่ที่ {ThemePath}\n" +
                "ขั้นถัดไป:\n" +
                "  1) กด Play แล้วเลื่อนด้วย ลูกศร/WASD · ยืนยันด้วย Enter/Space · เมาส์ชี้ = เลือก\n" +
                "  2) ลาก sprite พื้นหลังใส่ Background/Image (ตอนนี้เป็นสีทึบ)\n" +
                "  3) จูนลุคที่ P3RTheme.asset ตัวเดียว — ทุกรายการเปลี่ยนตาม\n" +
                "     (แก้ค่าแล้วคลิกขวาที่ P3RMenuList > Apply Theme To Items เพื่อเห็นผลโดยไม่ต้อง Play)\n" +
                "  ⚠ ถ้าเปลี่ยน labelText เป็นภาษาไทย ลุค ALL-CAPS จะหายไปทันที — ไทยไม่มีตัวพิมพ์ใหญ่\n" +
                "     นั่นคือคำถามฟอนต์ที่ยังไม่ได้ตัดสิน ซีนนี้มีไว้ให้เห็นด้วยตาว่าต่างกันแค่ไหน");
        }

        // ═══════════════════════════════════════════════════════════════════
        // THEME
        // ═══════════════════════════════════════════════════════════════════
        private static P3RTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<P3RTheme>(ThemePath);
            if (existing != null) return existing;   // มีแล้วไม่ทับ — ค่าที่จูนไว้ต้องรอด

            if (!AssetDatabase.IsValidFolder(ThemeDir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "UI");

            var theme = ScriptableObject.CreateInstance<P3RTheme>();
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (theme.font == null)
                Debug.LogWarning($"[P3R] หา font ไม่เจอที่ {FontPath} — ต้องลากใส่ P3RTheme.font เอง");

            theme.moveSfx    = LoadClip("SFX_UI_Button_Organic_Plastic_Thin_Generic_1.wav");
            theme.confirmSfx = LoadClip("SFX_UI_Button_Keyboard_Enter_Thick_1.wav");
            theme.cancelSfx  = LoadClip("SFX_UI_Button_Organic_Plastic_Thin_Negative_Back_1.wav");

            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            return theme;
        }

        private static AudioClip LoadClip(string file) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + file);

        // ═══════════════════════════════════════════════════════════════════
        // SCENE PARTS
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildCamera()
        {
            var go  = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag  = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
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
            var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            return canvas;
        }

        private static void BuildBackground(Canvas canvas)
        {
            var root = NewRect("Background", canvas.transform);
            Stretch(root);

            // ที่สำหรับลาก sprite ฉากจริงใส่ทีหลัง · ตอนนี้เป็นสีทึบเพื่อให้ contrast อ่านออก
            var img = NewImage("Image", root, new Color32(0x0C, 0x12, 0x28, 0xFF));
            Stretch(img.rectTransform);

            // ผ้าคลุมไล่เฉดมุมขวาล่าง — P3R ใช้เงาแบบนี้กันตัวหนังสือจมพื้นหลังที่มีลาย
            var scrim = NewImage("Scrim", root, new Color(0f, 0f, 0f, 0.35f));
            var srt = scrim.rectTransform;
            srt.anchorMin = new Vector2(0.35f, 0f);
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            scrim.raycastTarget = false;
        }

        private static void BuildMainPanel(Canvas canvas, P3RTheme theme)
        {
            var panel = NewRect("MainPanel", canvas.transform);
            Stretch(panel);

            var listRt = BuildMenuList(panel, theme, out var list);
            BuildKeyHints(panel, theme);
            BuildVersionText(panel, theme);

            // ทำให้ตัวที่เลือกไว้ตอนแรกดูถูกต้องใน Scene view โดยไม่ต้องกด Play
            list.SetIndex(0, instant: true, playSfx: false);
            EditorUtility.SetDirty(listRt.gameObject);
        }

        private static RectTransform BuildMenuList(RectTransform panel, P3RTheme theme, out P3RMenuList list)
        {
            var rt = NewRect("MenuList", panel);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(ItemWidth, theme.rowPitch * Items.Length);
            rt.anchoredPosition = new Vector2(-MarginRight, BlockOffsetY);

            list       = rt.gameObject.AddComponent<P3RMenuList>();
            list.theme = theme;
            rt.gameObject.AddComponent<P3RMainMenuProto>();

            float startY = (Items.Length - 1) * theme.rowPitch * 0.5f;
            for (int i = 0; i < Items.Length; i++)
            {
                var item = BuildItem(rt, theme, Items[i], new Vector2(0f, startY - i * theme.rowPitch));
                list.items.Add(item);
            }
            return rt;
        }

        private static P3RMenuItem BuildItem(RectTransform parent, P3RTheme theme,
                                             (string id, string label, bool enabled) data, Vector2 pos)
        {
            var rt = NewRect($"Item_{data.id}", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(ItemWidth, theme.rowPitch);
            rt.anchoredPosition = pos;

            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var item  = rt.gameObject.AddComponent<P3RMenuItem>();

            // ① พื้นที่รับเมาส์ — alpha 0 แต่ยัง raycast ได้ · event จะ bubble ขึ้นไปที่ P3RMenuItem
            var hit = NewImage("HitArea", rt, new Color(1f, 1f, 1f, 0f));
            Stretch(hit.rectTransform);
            hit.raycastTarget = true;

            // ② แถบ — pivot ขวา เพื่อให้ scale.x กางจากขวาไปซ้าย
            var bar = NewImage("Bar", rt, theme.barColor);
            var brt = bar.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot     = new Vector2(1f, 0.5f);

            // ③ ตัวหนังสือ — pivot ขวา เพื่อให้ย่อ scale.x แล้วขอบขวาไม่ขยับ
            var label = NewText("Label", rt, data.label);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.pivot     = new Vector2(1f, 0.5f);
            lrt.sizeDelta = new Vector2(ItemWidth, theme.rowPitch);
            lrt.anchoredPosition = Vector2.zero;

            item.id           = data.id;
            item.labelText    = data.label;
            item.interactable = data.enabled;
            item.label        = label;
            item.bar          = bar;
            item.group        = group;
            item.Apply(theme);
            return item;
        }

        private static void BuildKeyHints(RectTransform panel, P3RTheme theme)
        {
            var root = NewRect("KeyHints", panel);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
            root.pivot     = new Vector2(1f, 0f);
            root.sizeDelta = new Vector2(600f, 48f);
            root.anchoredPosition = new Vector2(-MarginRight * 0.35f, 28f);

            // วางจากขวาไปซ้าย — ตัวสุดท้ายในลิสต์อยู่ริมขวาสุด เหมือนภาพต้นแบบ
            float x = 0f;
            for (int i = KeyHints.Length - 1; i >= 0; i--)
            {
                var (key, text) = KeyHints[i];

                var lbl = NewText($"Hint_{key}_Label", root, text);
                SetFont(lbl, theme);
                lbl.fontSize  = 26f;
                lbl.color     = Color.white;
                lbl.alignment = TextAlignmentOptions.MidlineRight;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
                lrt.pivot     = new Vector2(1f, 0.5f);
                lrt.sizeDelta = new Vector2(160f, 40f);
                lrt.anchoredPosition = new Vector2(x, 0f);
                // ต้องบังคับให้ TMP คำนวณก่อน ไม่งั้น preferredWidth เป็น 0 ตอนสร้างใน Editor
                // แล้วป้ายทั้งสองจะซ้อนทับกันพอดี
                lbl.ForceMeshUpdate();
                x -= lbl.preferredWidth + 10f;

                float boxW = Mathf.Max(34f, 20f + key.Length * 13f);
                var box = NewImage($"Hint_{key}_Box", root, Color.white);
                var brt = box.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot     = new Vector2(1f, 0.5f);
                brt.sizeDelta = new Vector2(boxW, 34f);
                brt.anchoredPosition = new Vector2(x, 0f);
                box.raycastTarget = false;

                var kt = NewText($"Hint_{key}_Key", brt, key);
                SetFont(kt, theme);
                kt.fontSize  = 20f;
                kt.color     = new Color32(0x0C, 0x12, 0x28, 0xFF);
                kt.alignment = TextAlignmentOptions.Midline;
                Stretch(kt.rectTransform);

                x -= boxW + 26f;
            }
        }

        private static void BuildVersionText(RectTransform panel, P3RTheme theme)
        {
            var t = NewText("VersionText", panel, $"v{Application.version}");
            SetFont(t, theme);
            t.fontSize  = 22f;
            t.color     = new Color(1f, 1f, 1f, 0.45f);
            t.alignment = TextAlignmentOptions.BottomLeft;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 40f);
            rt.anchoredPosition = new Vector2(40f, 28f);
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
            t.text            = text;
            t.raycastTarget   = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>ใส่ฟอนต์เฉพาะตอนที่ theme มีจริง — set null จะทำให้ TMP ตกกลับไปฟอนต์ default เงียบๆ</summary>
        private static void SetFont(TMP_Text t, P3RTheme theme)
        {
            if (theme != null && theme.font != null) t.font = theme.font;
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
