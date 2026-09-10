using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ LOADING ตาม design handoff จอที่ 8
    /// เมนู: Tools > Clone Swarm > Build P3R Loading Scene
    ///
    /// **สามอย่างที่แบบวาดไว้เหมือนมีอยู่แล้ว แต่ยังไม่มีในเกม** — ดูคอมเมนต์ที่จุดสร้างแต่ละอัน
    ///   1. ภาพ art เฉพาะจอโหลด — ยืม `MapData.previewImage` มาก่อน
    ///   2. ความคืบหน้าเป็นเปอร์เซ็นต์ — NGO ไม่มีให้ดึง ใช้แถบกวาดแทน
    ///   3. TIP รายตัวละคร/รายแมพ — ยังไม่มีที่เก็บในข้อมูล
    /// </summary>
    public static class P3RLoadingSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_Loading.unity";

        // ── layout ที่ 1920x1080 ────────────────────────────────────────────
        private const float PadL      = 120f;
        private const float TipY      = 62f;
        private const float BarY      = 152f;
        private const float BarW      = 760f;
        private const float BarH      = 6f;
        private const float LineH     = 108f;   // ความสูงต่อบรรทัดของหัวเรื่อง

        private static readonly Color Wash     = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
        private static readonly Color ArtTint  = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color BarTrack = Lift(new Color32(0x0A, 0x0E, 0x1E, 0xFF), 0.14f);

        [MenuItem("Tools/Clone Swarm/Build P3R Loading Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "LOADING", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) แถบความคืบหน้าเป็นแบบ **กวาดไปมา** ไม่ใช่เปอร์เซ็นต์ — NGO โหลดซีนผ่าน\n" +
                "     NetworkManager.SceneManager ซึ่งยิงเป็น event เป็นช่วงๆ ไม่มีค่าต่อเนื่องให้ดึง\n" +
                "     ถ้าวันหนึ่งมีแหล่งจริง เรียก LoadingScreenUI.SetProgress() แล้วมันสลับโหมดเอง\n" +
                "  2) ภาพพื้นหลังยังว่าง — ลาก Sprite ใส่ Art หรือเรียก SetArt(mapData.previewImage)\n" +
                "     แบบขอภาพ art เฉพาะจอโหลด ซึ่งสัดส่วนคนละแบบกับภาพพรีวิวจอเลือกแมพ\n" +
                "  3) TIP อ่านจาก fallbackTips บน component — ยังไม่มีที่เก็บทิปรายตัวละคร/รายแมพ\n" +
                "     ต้องเลือกระหว่างเพิ่มฟิลด์ใน CharacterData (แตะทุกไฟล์ Char_*) หรือทำ SO แยก\n" +
                "  4) SKIP ต่อกับ 'ไปทิปถัดไป' ตามที่สเปกเดาไว้ — ข้ามการโหลดจริงไม่ได้อยู่แล้ว");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;
            var ui   = root.gameObject.AddComponent<LoadingScreenUI>();

            // ── พื้นหลัง ────────────────────────────────────────────────────
            var backdrop = NewImage("Backdrop", root, Wash);
            Stretch(backdrop.rectTransform);

            // ภาพแมพเต็มจอ — ปิด Image ไว้เพราะยังไม่มี sprite
            // Image ที่ไม่มี sprite เรนเดอร์เป็นสี่เหลี่ยมทึบเต็มจอ ซึ่งบังทุกอย่างข้างหลัง
            var art = NewImage("Art", root, ArtTint);
            Stretch(art.rectTransform);
            art.preserveAspect = true;
            art.enabled = false;
            ui.artImage = art;

            // ม่านทึบไล่จากล่างซ้าย — ให้ตัวหนังสืออ่านออกทับภาพที่ยังไม่รู้ว่าสว่างแค่ไหน
            var scrim = NewImage("Scrim", root, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.72f));
            Stretch(scrim.rectTransform);

            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.04f));
            Stretch(scan.rectTransform);
            scan.gameObject.SetActive(false);   // รอ sprite ลายเส้น 115°

            // ── บล็อกล่างซ้าย ───────────────────────────────────────────────
            var context = NewMono("Context", root, "ARENA 01 · NORMAL", 19f, 0.30f,
                                  TextAlignmentOptions.BottomLeft, new Color(1f, 1f, 1f, 0.58f));
            BottomLeft(context.rectTransform, PadL, BarY + 44f + LineH * 2f, 900f, 32f);
            ui.contextLabel = context;

            var top = NewText("HeadlineTop", root, "NOW", 96f, TextAlignmentOptions.BottomLeft);
            MonoStyle(top, 0.02f);
            BottomLeft(top.rectTransform, PadL, BarY + 36f + LineH, 900f, LineH);
            ui.headlineTop = top;

            var bottom = NewText("HeadlineBottom", root, "LOADING", 96f, TextAlignmentOptions.BottomLeft);
            MonoStyle(bottom, 0.02f);
            BottomLeft(bottom.rectTransform, PadL, BarY + 36f, 900f, LineH);
            ui.headlineBottom = bottom;

            // ── แถบความคืบหน้า ──────────────────────────────────────────────
            var track = NewImage("ProgressTrack", root, BarTrack);
            BottomLeft(track.rectTransform, PadL, BarY, BarW, BarH);
            ui.progressTrack = track.rectTransform;

            var fill = NewImage("ProgressFill", track.rectTransform, Primary);
            // anchor ถูก LoadingScreenUI เขียนทับทุกเฟรมตอนกวาด — ที่ตั้งตรงนี้เป็นแค่ค่าตั้งต้น
            var frt = fill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(0.28f, 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            ui.progressFill = frt;

            // ── SKIP ล่างขวา ────────────────────────────────────────────────
            var skipRoot = NewRect("Skip", root);
            BottomRight(skipRoot, PadL, BarY - 8f, 240f, 44f);

            var skipHit = NewImage("HitArea", skipRoot, new Color(0f, 0f, 0f, 0f));
            Stretch(skipHit.rectTransform);
            skipHit.raycastTarget = true;

            var skip = NewMono("Label", skipRoot, "SKIP →", 20f, 0.20f,
                               TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.72f));
            Stretch(skip.rectTransform);
            ui.skipLabel = skip;

            // SKIP = ไปทิปถัดไป · ข้ามการโหลดจริงทำไม่ได้อยู่แล้ว
            // navigation ปิดทิ้ง ไม่ให้โฟกัสค้างแล้วโดน Space ยิงซ้ำทีหลัง
            var skipBtn = skipRoot.gameObject.AddComponent<Button>();
            skipBtn.targetGraphic = skipHit;
            var nav = skipBtn.navigation; nav.mode = Navigation.Mode.None; skipBtn.navigation = nav;
            var call = new UnityEngine.Events.UnityAction(ui.NextTip);
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(skipBtn.onClick, call);

            // ── TIP ล่างสุด ─────────────────────────────────────────────────
            var rule = NewImage("TipRule", root, FaintLine);
            var rrt = rule.rectTransform;
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(-PadL * 2f, 1f);
            rrt.anchoredPosition = new Vector2(0f, TipY + 46f);

            var tip = NewText("Tip", root,
                              "TIP — Riven สะสมพลังจากการเคลื่อนที่ ยืนนิ่งคือทิ้งพาสซีฟของเธอไปเปล่าๆ",
                              21f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.62f));
            BottomLeft(tip.rectTransform, PadL, TipY - 8f, RefW - PadL * 2f, 40f);
            ui.tipLabel = tip;
        }
    }
}
