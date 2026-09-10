using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ TITLE ตาม design handoff จอที่ 1
    /// เมนู: Tools > Clone Swarm > Build P3R Title Scene
    ///
    /// **จอนี้ไม่มีอะไรรองรับในเกมมาก่อนเลย** — ไม่มีทั้งซีนและสคริปต์
    /// <see cref="TitleScreenUI"/> จึงเป็นของใหม่ทั้งชิ้น แต่จงใจเขียนให้ไม่รู้จัก
    /// การโหลดซีน มันแค่ยิง event ออกไป จะเอาไปวางเป็นซีนแยกหรือ panel ก็ได้
    ///
    /// **ตัวหนังสือกลวง** — ใช้วิธีเดียวกับหัวเรื่องจอ Level Up คือ face เป็นสีพื้นจอ
    /// ไม่ใช่โปร่งใส · face โปร่งจะทำให้ vertex alpha กลืนเส้นขอบไปด้วยจนหายทั้งตัว
    /// </summary>
    public static class P3RTitleSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_Title.unity";

        private const float PadL = 128f;
        private const float BarW = 14f;

        [MenuItem("Tools/Clone Swarm/Build P3R Title Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "TITLE", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป — **ต้องตัดสินก่อนเอาลงเกม**:\n" +
                "  ก) เป็น panel แรกใน MenuScene → ต่อ TitleScreenUI.OnAdvance เข้า\n" +
                "     MenuManager.ShowPanel(mainPanel) · ข้อดี: ไม่ต้องโหลดซีนเพิ่ม\n" +
                "     และใช้ระบบสลับ panel ที่มีอยู่แล้ว — **ทางที่แนะนำ**\n" +
                "  ข) เป็นซีนแยก → ต่อเข้า SceneManager.LoadScene(\"MenuScene\")\n" +
                "     และต้องเพิ่มเข้า Build Settings เป็นซีนแรก\n" +
                "อื่นๆ:\n" +
                "  1) ภาพ title ยังว่าง — ลาก Sprite ใส่ Art แล้วเปิด component\n" +
                "  2) เลข version อ่านจาก Application.version ตอนรัน ที่เห็นในซีนเป็นของโชว์");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;
            var ui   = root.gameObject.AddComponent<TitleScreenUI>();

            var backdrop = NewImage("Backdrop", root, InkDeep);
            Stretch(backdrop.rectTransform);

            // ภาพ title เต็มจอ — ปิดไว้เพราะยังไม่มี sprite
            // Image ที่ไม่มี sprite จะเป็นสี่เหลี่ยมทึบเต็มจอบังทุกอย่างข้างหลัง
            var art = NewImage("Art", root, new Color(1f, 1f, 1f, 0.9f));
            Stretch(art.rectTransform);
            art.preserveAspect = true;
            art.enabled = false;

            var scrim = NewImage("Scrim", root, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.6f));
            Stretch(scrim.rectTransform);

            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.04f));
            Stretch(scan.rectTransform);
            scan.gameObject.SetActive(false);

            // ── แถบน้ำเงินแนวตั้งชิดขอบซ้าย ─────────────────────────────────
            var bar = NewImage("EdgeBar", root, Primary);
            var brt = bar.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot     = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(BarW, 0f);
            brt.anchoredPosition = Vector2.zero;

            // ── บนซ้าย ──────────────────────────────────────────────────────
            var brand = NewMono("Brand", root, "CLONE SWARM", 24f, 0.22f,
                                TextAlignmentOptions.MidlineLeft);
            TopLeft(brand.rectTransform, PadL, 96f, 620f, 34f);

            var tagline = NewMono("Tagline", root, "CO-OP   SURVIVOR   ARENA", 15f, 0.52f,
                                  TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.45f));
            TopLeft(tagline.rectTransform, PadL, 132f, 720f, 26f);

            // ── กลางซ้าย: PRESS / ANY / KEY ตัวกลวงใหญ่มาก ──────────────────
            var promptRoot = NewRect("Prompt", root);
            promptRoot.anchorMin = promptRoot.anchorMax = new Vector2(0f, 0.5f);
            promptRoot.pivot     = new Vector2(0f, 0.5f);
            promptRoot.sizeDelta = new Vector2(1000f, 480f);
            promptRoot.anchoredPosition = new Vector2(PadL, -30f);

            var group = promptRoot.gameObject.AddComponent<CanvasGroup>();
            ui.promptGroup = group;

            var prompt = NewText("Label", promptRoot, "PRESS\nANY\nKEY", 168f,
                                 TextAlignmentOptions.TopLeft);
            MonoStyle(prompt, 0f);
            prompt.lineSpacing  = -26f;
            prompt.fontStyle    = FontStyles.Italic;
            // face = สีพื้นจอ ไม่ใช่โปร่ง · โปร่งแล้ว vertex alpha จะกลืนเส้นขอบไปด้วย
            prompt.color        = InkDeep;
            prompt.outlineColor = Color.white;
            prompt.outlineWidth = 0.24f;
            Stretch(prompt.rectTransform);
            prompt.rectTransform.localScale = new Vector3(0.86f, 1f, 1f);

            // ── ล่างซ้าย / ล่างขวา ──────────────────────────────────────────
            var version = NewMono("Version", root, "v0.8.2", 16f, 0.18f,
                                  TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.4f));
            BottomLeft(version.rectTransform, PadL, 64f, 300f, 26f);
            ui.versionLabel = version;

            var copy = NewMono("Copyright", root, "© 2026 CLONE SWARM", 15f, 0.2f,
                               TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.32f));
            BottomRight(copy.rectTransform, PadL, 64f, 460f, 26f);
        }
    }
}
