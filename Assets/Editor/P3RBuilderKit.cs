using System.IO;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เครื่องมือกลางของ builder ทุกตัว — เดิมทุกไฟล์เขียน NewRect/NewText/NewImage/BuildCanvas
    /// ซ้ำกันเอง สี่ชุด ผลคือกฎที่แก้แล้วต้องไปจำใหม่ทุกไฟล์ และมันหลุดจริงมาแล้ว
    /// (`Mono()` ของจอ Win/Lose กรองไทย แต่ `NewMono()` ของจอ Level Up เป็นคนละตัว)
    ///
    /// **กฎสองข้อที่ไฟล์นี้บังคับให้ถูกโดยไม่ต้องจำ**
    ///
    /// 1. **โหลด theme หลัง NewScene เสมอ** — <see cref="BeginScene"/> เป็นคนโหลดให้เอง
    ///    หลังเปิดซีนใหม่แล้ว ผู้เรียกจึงไม่มีโอกาสโหลดผิดจังหวะ
    ///    (โหลดก่อนแล้ว batchmode จะปลด asset ทิ้งจนกลายเป็น fake-null · ซีนออกมาไม่มีฟอนต์
    ///    ไม่มีสี โดย exit 0 ไม่มี error — เคยกินเวลาไปสองวัน)
    ///
    /// 2. **ห้ามเขียน `characterSpacing` ตรงๆ** — ทางเดียวที่ถ่างระยะได้คือ <see cref="MonoStyle"/>
    ///    ซึ่งวิ่งผ่าน <see cref="P3RText"/> ที่กรองภาษาไทยให้ · สระและวรรณยุกต์ไทยเป็นอักขระ
    ///    ไม่มีความกว้าง ถ่างแล้วมันหลุดออกจากพยัญชนะ
    ///
    /// **สิ่งที่ไฟล์นี้ยังไม่ได้แก้ให้:** ข้อความที่เปลี่ยนตอน**รัน** ยังต้องเขียนผ่าน
    /// `P3RText.SetTextAndTracking` เองที่ฝั่ง runtime — kit ตัวนี้คุมได้แค่ตอนสร้างซีน
    /// </summary>
    public static class P3RBuilderKit
    {
        public const string ThemePath      = "Assets/ScriptableObjects/UI/P3RTheme.asset";
        public const string PrefabDir      = "Assets/Prefab/UI/P3R";
        public const float  RefW           = 1920f;
        public const float  RefH           = 1080f;

        // ── พาเลตต์กลาง — ค่าตรงจาก design handoff ──────────────────────────
        // เดิมแต่ละ builder ประกาศสีเองซ้ำๆ แล้วเริ่มเพี้ยนจากกัน
        public static readonly Color Ink        = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
        public static readonly Color InkDeep    = new Color32(0x06, 0x08, 0x12, 0xFF);
        public static readonly Color Card       = new Color32(0x11, 0x18, 0x38, 0xFF);
        public static readonly Color Panel      = new Color32(0x0D, 0x12, 0x26, 0xFF);
        public static readonly Color Primary    = new Color32(0x18, 0x24, 0xD8, 0xFF);
        public static readonly Color Gold       = new Color32(0xFF, 0xE6, 0x33, 0xFF);
        public static readonly Color Amber      = new Color32(0xD9, 0x9A, 0x1A, 0xFF);
        public static readonly Color BlueSlot   = new Color32(0x40, 0x73, 0xD9, 0xFF);
        public static readonly Color Green      = new Color32(0x4D, 0xA6, 0x59, 0xFF);
        public static readonly Color Teal       = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        public static readonly Color Orange     = new Color32(0xF0, 0x86, 0x54, 0xFF);
        public static readonly Color Red        = new Color32(0xD8, 0x20, 0x20, 0xFF);
        public static readonly Color MutedInk   = new Color32(0x8C, 0x86, 0x78, 0xFF);
        public static readonly Color HairLine   = new Color(1f, 1f, 1f, 0.20f);
        public static readonly Color FaintLine  = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>theme ของซีนที่กำลังสร้าง — <see cref="BeginScene"/> เป็นคนตั้ง</summary>
        public static P3RTheme Theme { get; private set; }

        /// <summary>
        /// ผสมสีล่วงหน้าแล้วคืนค่า **ทึบ** — ใช้แทนการวาง Image ที่ alpha ต่ำทับพื้น
        ///
        /// **ทำไมต้องมี:** โปรเจกต์อยู่ใน Linear color space · uGUI ผสมสีใน linear
        /// ผลที่ได้จึงสว่างกว่าที่ค่า alpha แบบ CSS บอกไว้มาก — ขาว 10% ที่แบบตั้งใจให้เป็น
        /// เทาเข้มแทบมองไม่เห็น ออกมาเป็นเทากลางที่เด่นกว่าตัวหนังสือรอบๆ เสียอีก
        /// (วัดจริงจากภาพเรนเดอร์: ขาว .10 บนพื้น #0D1226 ได้ #5B5D61 แทนที่จะเป็น #252A3C)
        ///
        /// สีทึบยังได้ผลพลอยได้อีกข้อ — ไม่ต้องพึ่งว่าอะไรอยู่ข้างหลัง ย้ายไปวางบนพื้นอื่นแล้วไม่เพี้ยน
        /// </summary>
        public static Color Over(Color top, Color under, float alpha)
            => new Color(Mathf.Lerp(under.r, top.r, alpha),
                         Mathf.Lerp(under.g, top.g, alpha),
                         Mathf.Lerp(under.b, top.b, alpha),
                         1f);

        /// <summary>ยกพื้นให้สว่างขึ้นด้วยขาวจางๆ — เคสที่พบบ่อยที่สุดของ <see cref="Over"/></summary>
        public static Color Lift(Color under, float alpha) => Over(Color.white, under, alpha);

        // ═══════════════════════════════════════════════════════════════════
        // SCENE LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// เปิดซีนเปล่าใหม่แล้วโหลด theme ให้ — คืน <c>false</c> ถ้าผู้ใช้ยกเลิก
        /// ผู้เรียกต้องเช็คค่าที่คืนแล้ว <c>return</c> ทันที
        /// </summary>
        public static bool BeginScene(string scenePath, string screenName, out Scene scene)
        {
            scene = default;

            // batchmode ไม่มีใครกดปุ่มได้ — DisplayDialog คืน false เสมอ
            // ถ้าไม่กันตรงนี้ อาการคือ -executeMethod แล้ว exit 0 แต่ซีนไม่ถูกสร้างใหม่ ไม่มี error
            if (!Application.isBatchMode && File.Exists(scenePath) &&
                !EditorUtility.DisplayDialog($"สร้างซีนต้นแบบ {screenName} ใหม่",
                    $"{scenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // โหลด **หลัง** NewScene เท่านั้น — ดูเหตุผลในคอมเมนต์หัวคลาส
            Theme = AssetDatabase.LoadAssetAtPath<P3RTheme>(ThemePath);
            if (Theme == null)
                Debug.LogWarning($"[P3R] ไม่พบ {ThemePath} — จะใช้ฟอนต์ default ของ TMP แทน " +
                                 "รัน Build P3R Main Menu Scene ก่อนหนึ่งครั้งเพื่อสร้าง theme");

            return true;
        }

        /// <summary>เซฟซีน รีเฟรช แล้ว ping ให้เห็นใน Project window</summary>
        public static void EndScene(Scene scene, string scenePath, string nextSteps)
        {
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(scenePath));
            Debug.Log($"[P3R] สร้าง {scenePath} เรียบร้อย\n{nextSteps}");
        }

        // ═══════════════════════════════════════════════════════════════════
        // SCENE SHELL
        // ═══════════════════════════════════════════════════════════════════
        public static Camera BuildCamera(Color? background = null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background ?? Ink;
            go.transform.position = new Vector3(0f, 1f, -10f);
            return cam;
        }

        public static EventSystem BuildEventSystem()
            => new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
               .GetComponent<EventSystem>();

        public static Canvas BuildCanvas(string name = "MenuCanvas")
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(RefW, RefH);
            // 0.5 = ยึดกลางระหว่างกว้างกับสูง · จอ ultrawide ไม่ทำให้ของโตเกิน
            s.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight  = 0.5f;
            return canvas;
        }

        // ═══════════════════════════════════════════════════════════════════
        // NODES
        // ═══════════════════════════════════════════════════════════════════
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// Image สำหรับงานประดับ — **`raycastTarget` ปิดไว้**
        ///
        /// จอ P3R หนึ่งจอมี Image หลายสิบตัวที่เป็นพื้นหลัง/เส้นคั่น/แถบสี ล้วนไม่ต้องรับคลิก
        /// ปล่อยให้รับทั้งหมดคือให้ EventSystem ไล่ raycast ของที่ไม่มีใครกดทุกเฟรม
        ///
        /// **อะไรที่ต้องกดได้ ต้องเปิดกลับเอง** — `bg.raycastTarget = true;`
        /// ลืมเปิดแล้วจะได้ของที่ดูครบทุกอย่าง (มี Button · interactable ติ๊ก · listener ต่อแล้ว)
        /// แต่กดไม่ติด และ **ไม่มีอะไรฟ้องเลย** เพราะ EventSystem ไม่รู้ด้วยซ้ำว่าเมาส์ชี้โดนมัน
        /// เคยกินเวลาหาสองรอบกับการ์ดเลือกตัวละคร · ดู EnsureClickable ใน CarouselBase
        /// </summary>
        public static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI NewText(string name, Transform parent, string text,
                                              float size, TextAlignmentOptions align,
                                              Color? color = null)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text              = text;
            t.fontSize          = size;
            t.alignment         = align;
            t.color             = color ?? Color.white;
            t.raycastTarget     = false;
            t.textWrappingMode  = TextWrappingModes.NoWrap;
            if (Theme != null && Theme.font != null) t.font = Theme.font;
            return t;
        }

        /// <summary>
        /// ป้ายกำกับสไตล์ mono ของแบบ — โปรเจกต์มีแต่ Sarabun ไม่มีฟอนต์ mono จริง
        /// จึงเลียนด้วยตัวพิมพ์ใหญ่ + ถ่างระยะตัวอักษร ซึ่งให้ความรู้สึกใกล้ที่สุด
        /// ถ้าวันหนึ่งซื้อฟอนต์ mono มา ให้เปลี่ยนที่เมธอดนี้ที่เดียว
        ///
        /// <paramref name="emSpacing"/> เป็นหน่วย em ตามที่แบบเขียน (`.16em`) ไม่ใช่หน่วยของ TMP
        /// </summary>
        public static void MonoStyle(TextMeshProUGUI t, float emSpacing)
        {
            P3RText.SetTracking(t, emSpacing * 100f);   // TMP นับเป็น % ของ em
            P3RText.TryUpperCase(t);
        }

        /// <summary>
        /// เปิดให้ข้อความขึ้นบรรทัดใหม่ได้ **พร้อมตัดคำไทยให้ถูก**
        ///
        /// ภาษาไทยไม่มีช่องว่างระหว่างคำ TMP จึงตัดตรงไหนก็ได้ที่พอดีขอบ
        /// ผลคือคำโดนผ่ากลาง — `บรรทัด` กลายเป็น `บร` ขึ้นบรรทัดใหม่เป็น `รทัด`
        ///
        /// <c>ThaiTextNurse</c> (จากแพ็กเกจ ThaiTextCare) เป็น ITextPreprocessor
        /// ที่แทรกช่องว่างความกว้างศูนย์ตามขอบคำจากพจนานุกรม TMP จึงตัดถูกที่
        ///
        /// **ใช้ตัวนี้แทนการเขียน <c>textWrappingMode = Normal</c> ตรงๆ เสมอ** —
        /// ไม่งั้นข้อความไทยที่ยาวพอจะขึ้นบรรทัดจะโดนผ่าคำโดยไม่มีใครสังเกต
        /// จนกว่าจะมีคนอ่านภาษาไทยมาเห็น
        /// </summary>
        public static void Wrap(TMP_Text t)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (t.GetComponent<PhEngine.ThaiTextCare.ThaiTextNurse>() == null)
                t.gameObject.AddComponent<PhEngine.ThaiTextCare.ThaiTextNurse>();
        }

        /// <summary>ป้าย mono สำเร็จรูป — ที่พบบ่อยที่สุดในแบบ</summary>
        public static TextMeshProUGUI NewMono(string name, Transform parent, string text,
                                              float size, float emSpacing,
                                              TextAlignmentOptions align, Color? color = null)
        {
            var t = NewText(name, parent, text, size, align, color);
            MonoStyle(t, emSpacing);
            return t;
        }

        // ═══════════════════════════════════════════════════════════════════
        // DECORATION
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// เส้นเน้นซ้าย 6px — ลายเซ็นของการ์ดทุกใบในแบบ (`border-left: 6px`)
        /// </summary>
        public static Image AccentBar(RectTransform parent, Color color, float width = 6f)
        {
            var bar = NewImage("Accent", parent, color);
            var rt  = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, 0f);
            rt.anchoredPosition = Vector2.zero;
            return bar;
        }

        /// <summary>
        /// กรอบสี่ด้านที่วาดด้วย Image จริงสี่ชิ้น
        ///
        /// **ทำไมไม่ใช้ `UnityEngine.UI.Outline`:** มันทำงานด้วยการซ้ำ mesh ของ graphic เดิม
        /// พื้นการ์ดที่ alpha 0 (ซึ่งเป็นกรณีปกติของกรอบเปล่า) จึงได้กรอบ alpha 0 ตามไปด้วย
        /// = มองไม่เห็นอะไรเลย
        ///
        /// <paramref name="shear"/> ไม่ใช่ 0 → ใส่ <see cref="UIShear"/> ให้ทุกด้าน
        /// พร้อม <c>pivotOnParent</c> ไม่งั้นแถบบนกับล่างเฉือนคนละจุดแล้วกรอบแตกออกจากกัน
        /// </summary>
        public static void AddBorder(RectTransform target, string name, float thickness,
                                     Color color, float shear = 0f)
        {
            var root = NewRect(name, target);
            Stretch(root);

            Edge("Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
            Edge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
            Edge("Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
            Edge("Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));

            void Edge(string edgeName, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
            {
                var img = NewImage(edgeName, root, color);
                var rt  = img.rectTransform;
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
                rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
                if (!Mathf.Approximately(shear, 0f))
                {
                    var sh = img.gameObject.AddComponent<UIShear>();
                    sh.angleDegrees  = shear;
                    sh.pivotOnParent = true;
                }
            }
        }

        /// <summary>เฉือนกล่องให้เป็นสี่เหลี่ยมด้านขนาน · `skewX(-9deg)` ของแบบ = 9 ที่นี่</summary>
        public static UIShear Shear(Graphic g, float angle = 9f, bool pivotOnParent = false)
        {
            var sh = g.gameObject.AddComponent<UIShear>();
            sh.angleDegrees  = angle;
            sh.pivotOnParent = pivotOnParent;
            return sh;
        }

        // ═══════════════════════════════════════════════════════════════════
        // LAYOUT
        // ═══════════════════════════════════════════════════════════════════
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>ยืดเต็มพาเรนต์แล้วเว้นขอบเข้ามาเท่ากันทุกด้าน</summary>
        public static void Inset(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static void TopRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-x, -y);
        }

        public static void BottomLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        public static void BottomRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-x, y);
        }

        /// <summary>กลางจอแนวนอน วัดจากขอบบน — ใช้กับพาเนลที่ลอยกลาง</summary>
        public static void CenterTop(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        public static void Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        // ═══════════════════════════════════════════════════════════════════
        // PREFAB
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// เซฟแม่แบบเป็น prefab asset แล้วลบตัวในซีนทิ้ง
        ///
        /// **prefab ย้ายลงซีนจริงได้ · ซีนต้นแบบย้ายไม่ได้** — อะไรที่มีโอกาสได้ใช้ในเกมจริง
        /// ควรออกมาทางนี้ ไม่ใช่ฝังไว้ใน Proto_*.unity เฉยๆ
        /// </summary>
        public static GameObject SavePrefab(GameObject template, string path)
        {
            // ต้องสร้างผ่าน AssetDatabase ไม่ใช่ Directory.CreateDirectory
            // ไม่งั้นโฟลเดอร์ยังไม่มี .meta ตอน SaveAsPrefabAsset แล้วเซฟไม่ลง
            if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
                AssetDatabase.CreateFolder("Assets/Prefab", "UI");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/Prefab/UI", "P3R");

            var asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);
            return asset;
        }

        /// <summary>วางตัวอย่างในซีนให้ดูเหมือน mockup — ตอนรันโค้ดจริงล้างทิ้งแล้วสร้างใหม่</summary>
        public static T SpawnSample<T>(T prefab, RectTransform parent, System.Action<T> bind)
            where T : MonoBehaviour
        {
            if (prefab == null) return null;
            var clone = (T)PrefabUtility.InstantiatePrefab(prefab, parent);
            bind?.Invoke(clone);
            return clone;
        }
    }
}
