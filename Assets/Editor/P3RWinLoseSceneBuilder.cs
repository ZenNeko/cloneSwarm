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
    /// สร้างซีนต้นแบบจอสรุปผล (Win / Lose) ทั้งซีนด้วยโค้ด ตาม design handoff
    /// `design_handoff_ingame_screens/README.md` หัวข้อ "Screen 2 — Win / Lose"
    ///
    /// เมนู: Tools > Clone Swarm > Build P3R Win Lose Scene
    ///
    /// **แผ่นทแยง — จุดที่แบบขอในสิ่งที่ uGUI ทำตรงๆ ไม่ได้**
    /// แบบใช้ `clip-path: polygon(0 0, 100% 0, calc(100% - 190px) 100%, 0 100%)`
    /// คือสี่เหลี่ยมที่มุมล่างขวาถูกดึงเข้ามา 190px · uGUI ไม่มี clip-path
    ///
    /// ที่ทำแทน: <see cref="UIShear"/> บน Image ที่กว้างเกินจอแล้วดันซ้ายออกไปนอกเฟรม
    /// เฉือน tan(θ)·1080 = 190 → θ = atan(190/1080) ≈ 10°
    /// UIShear เฉือนรอบกึ่งกลาง ขอบบนจึงขยับ +95 ขอบล่าง −95 — วางขอบขวาที่ 980
    /// จะได้ขอบบนที่ 1075 (= 56% ของ 1920) ขอบล่างที่ 885 (= 1075−190) ตรงตามแบบเป๊ะ
    /// ขอบซ้ายก็เอียงด้วยแต่มันอยู่นอกจอ จึงมองไม่เห็น
    ///
    /// **ที่ยังไม่ได้ทำ:** ลายเส้นทับ 115° (`repeating-linear-gradient`) ต้องใช้ sprite แบบ tile
    /// builder สร้าง GameObject `Scanlines` ให้แล้วแต่ **ปิดไว้** เพราะ Image ที่ไม่มี sprite
    /// จะเรนเดอร์เป็นสีทึบเต็มจอซึ่งผิดกว่าไม่มี — ลาก sprite ใส่แล้วค่อยเปิด
    /// </summary>
    public static class P3RWinLoseSceneBuilder
    {
        private const string ScenePath  = "Assets/GameScenes/Proto_WinLose.unity";
        private const string ThemePath  = "Assets/ScriptableObjects/UI/P3RTheme.asset";
        private const string PrefabDir  = "Assets/Prefab/UI/P3R";
        private const string PartyPrefab  = PrefabDir + "/ResultPartyRow.prefab";
        private const string RewardPrefab = PrefabDir + "/RewardLine.prefab";

        // ── layout ที่ 1920x1080 (ทุกตัวเลขอ่านตรงจาก handoff) ──────────────
        private const float RefW = 1920f, RefH = 1080f;
        private const float DiagAngle = 10f;      // atan(190/1080)
        private const float DiagRightEdge = 980f; // 1075 − 95 (ครึ่งหนึ่งของระยะเฉือน)

        // ── palette ─────────────────────────────────────────────────────────
        private static readonly Color Backdrop  = new Color32(0x06, 0x08, 0x12, 0xF2); // rgba(6,8,18,.95)
        private static readonly Color DiagWin   = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color Teal      = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        private static readonly Color Primary   = new Color32(0x18, 0x24, 0xD8, 0xFF);
        private static readonly Color RowBg     = new Color(10 / 255f, 14 / 255f, 30 / 255f, 0.66f);
        private static readonly Color HostGold  = new Color32(0xFF, 0xE6, 0x33, 0xFF);
        private static readonly Color HairLine  = new Color(1f, 1f, 1f, 0.2f);

        private static P3RTheme theme;

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Build P3R Win Lose Scene")]
        public static void Build()
        {
            // batchmode ไม่มีใครกดปุ่มได้ — DisplayDialog คืน false เสมอแล้ว Build() ออกเงียบๆ
            // อาการคือรัน -executeMethod แล้ว exit 0 แต่ซีนไม่ถูกสร้างใหม่ ไม่มี error ให้เห็น
            if (!Application.isBatchMode && File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("สร้างซีนต้นแบบ Win/Lose ใหม่",
                    $"{ScenePath} มีอยู่แล้ว\n\nสร้างทับของเดิม? งานที่จัดมือในซีนนั้นจะหายทั้งหมด",
                    "สร้างทับ", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ต้องโหลด asset **หลัง** NewScene เสมอ — NewScene ปลด asset ที่ไม่มีใครอ้างถึงทิ้ง
            // ใน batchmode ตัวแปรที่โหลดไว้ก่อนจึงกลายเป็น fake-null แล้ว P3RMenuItem.Apply()
            // ออกที่เช็ก null เงียบๆ · อาการคือซีนถูกสร้างจนจบ exit 0 แต่ฟอนต์/สี/แถบไม่ถูกใส่เลย
            // ในเอดิเตอร์ปกติไม่เจอ เพราะเอดิเตอร์ถือ asset ไว้ให้อยู่แล้ว
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            theme = AssetDatabase.LoadAssetAtPath<P3RTheme>(ThemePath);
            if (theme == null)
                Debug.LogWarning($"[P3R] ไม่พบ {ThemePath} — จะใช้ฟอนต์ default ของ TMP แทน " +
                                 "รัน Build P3R Main Menu Scene ก่อนหนึ่งครั้งเพื่อสร้าง theme");

            BuildCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ScenePath));

            Debug.Log(
                $"[P3R] สร้าง {ScenePath} เรียบร้อย\n" +
                $"prefab แถวปาร์ตี้/บัญชีรางวัลเซฟไว้ที่ {PrefabDir}\n" +
                "ขั้นถัดไป:\n" +
                "  1) ซีนสร้างเป็นสถานะ WIN · สถานะ LOSE สลับเองตอนรัน (WinLoseUI เปลี่ยน 4 อย่าง:\n" +
                "     คำผล · สีคำผล · สีแผ่นทแยง · ป้ายรอง)\n" +
                "  2) ลาก sprite ลายเส้น 115° ใส่ Scanlines แล้วเปิด GameObject (ตอนนี้ปิดไว้)\n" +
                "  3) แถวตัวอย่างในซีนเป็น mockup เฉยๆ — ตอนรัน BuildPartyRows/BuildRewardLines ล้างทิ้งแล้วสร้างใหม่");
        }

        // ═══════════════════════════════════════════════════════════════════
        // SCENE SHELL
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0A, 0x0E, 0x1E, 0xFF);
            go.transform.position = new Vector3(0f, 1f, -10f);
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(RefW, RefH);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        // ═══════════════════════════════════════════════════════════════════
        // SCREEN
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = NewRect("Panel_WinLose", canvas.transform);
            Stretch(root);
            root.gameObject.AddComponent<CanvasGroup>();
            var ui = root.gameObject.AddComponent<WinLoseUI>();
            ui.panelRoot = root.gameObject;

            // ── ชั้นพื้น ────────────────────────────────────────────────────
            var backdrop = NewImage("Backdrop", root, Backdrop);
            Stretch(backdrop.rectTransform);

            var diag = BuildDiagonal(root);
            ui.diagonalPanel = diag;

            // ลายเส้น 115° — ปิดไว้จนกว่าจะมี sprite (ดูคอมเมนต์หัวคลาส)
            var scan = NewImage("Scanlines", root, new Color(1f, 1f, 1f, 0.05f));
            Stretch(scan.rectTransform);
            scan.type = Image.Type.Tiled;
            scan.gameObject.SetActive(false);

            BuildLeftColumn(root, ui);
            BuildRightColumn(root, ui);
            BuildButtons(root, ui);
        }

        /// <summary>แผ่นทแยงซ้าย — ดูคำอธิบายวิธีทำที่คอมเมนต์หัวคลาส</summary>
        private static Image BuildDiagonal(RectTransform parent)
        {
            var img = NewImage("DiagonalPanel", parent, DiagWin);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            // กว้างเกินขอบซ้ายไปมาก เพื่อให้ขอบซ้ายที่เอียงอยู่นอกจอ
            rt.sizeDelta = new Vector2(DiagRightEdge + 600f, 0f);
            rt.anchoredPosition = new Vector2(DiagRightEdge, 0f);

            var shear = img.gameObject.AddComponent<UIShear>();
            shear.angleDegrees = DiagAngle;
            return img;
        }

        // ── คอลัมน์ซ้าย: ผล · สเตต · ปาร์ตี้ ────────────────────────────────
        private static void BuildLeftColumn(RectTransform parent, WinLoseUI ui)
        {
            var col = NewRect("LeftColumn", parent);
            TopLeft(col, 110f, 200f, 800f, 700f);

            float y = 0f;

            var sub = NewText("Subtitle", col, "ARENA 01 · เคลียร์แล้ว", 20f, TextAlignmentOptions.TopLeft);
            Mono(sub, 0.30f);
            TopLeft(sub.rectTransform, 0f, y, 800f, 28f);
            ui.subtitleLabel = sub;
            y += 34f;

            var result = NewText("ResultLabel", col, "VICTORY", 200f, TextAlignmentOptions.TopLeft);
            P3RText.SetTracking(result, -5.5f);                       // letter-spacing:-0.055em
            result.lineSpacing = -18f;                             // line-height .82
            result.rectTransform.localScale = new Vector3(0.82f, 1f, 1f);
            result.rectTransform.pivot = new Vector2(0f, 1f);      // ย่อ scaleX แล้วขอบซ้ายต้องไม่ขยับ
            TopLeft(result.rectTransform, 0f, y, 800f, 190f);
            ui.resultLabel = result;
            y += 190f + 56f;                                       // margin-top:56

            // แถวสเตต — ป้ายกับค่าเป็น TMP คนละตัวตามแบบ (ระยะห่าง 64)
            var stats = NewRect("StatRow", col);
            TopLeft(stats, 0f, y, 800f, 90f);
            float x = 0f;
            ui.timeValueLabel  = BuildStat(stats, "TIME",  "15:32", ref x);
            ui.levelValueLabel = BuildStat(stats, "LEVEL", "14",    ref x);
            ui.waveValueLabel  = BuildStat(stats, "WAVE",  "12",    ref x);
            y += 90f + 44f;                                        // margin: 44px 0 22px

            var line = NewImage("Divider", col, HairLine);
            TopLeft(line.rectTransform, 0f, y, 800f, 1f);
            y += 1f + 22f;

            var partyHead = NewText("PartyHeader", col, "ปาร์ตี้", 15f, TextAlignmentOptions.TopLeft);
            Mono(partyHead, 0.24f);
            TopLeft(partyHead.rectTransform, 0f, y, 800f, 22f);
            y += 22f + 16f;

            var rows = NewRect("PartyRows", col);
            TopLeft(rows, 0f, y, 800f, 220f);
            var vlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;                                     // handoff: ระยะห่าง 12
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            ui.partyRowsParent = rows;

            ui.partyRowPrefab = BuildPartyRowPrefab();
            SpawnSample(ui.partyRowPrefab, rows, r => r.Bind("Riven",  0, true));
            SpawnSample(ui.partyRowPrefab, rows, r => r.Bind("Gunner", 1, false));
        }

        private static TextMeshProUGUI BuildStat(RectTransform parent, string label, string value, ref float x)
        {
            var cell = NewRect($"Stat_{label}", parent);
            TopLeft(cell, x, 0f, 200f, 90f);

            var lbl = NewText("Label", cell, label, 15f, TextAlignmentOptions.TopLeft);
            Mono(lbl, 0.22f);
            TopLeft(lbl.rectTransform, 0f, 0f, 200f, 20f);

            var val = NewText("Value", cell, value, 58f, TextAlignmentOptions.TopLeft);
            val.lineSpacing = -8f;                                 // line-height .9
            TopLeft(val.rectTransform, 0f, 24f, 200f, 66f);

            // ระยะห่าง 64 วัดจากขอบขวาของตัวเลข — ใช้ความกว้างจริงเพื่อไม่ให้ห่างเกินตอนคำสั้น
            val.ForceMeshUpdate();
            x += Mathf.Max(val.preferredWidth, lbl.preferredWidth) + 64f;
            return val;
        }

        // ── คอลัมน์ขวา: บัญชีรางวัล ─────────────────────────────────────────
        private static void BuildRightColumn(RectTransform parent, WinLoseUI ui)
        {
            var col = NewRect("RightColumn", parent);
            TopRight(col, 120f, 200f, 600f, 640f);

            float y = 0f;

            var head = NewText("RewardHeader", col, "บัญชีรางวัล", 15f, TextAlignmentOptions.TopLeft);
            Mono(head, 0.24f);
            TopLeft(head.rectTransform, 0f, y, 600f, 22f);
            y += 22f + 14f;

            var lines = NewRect("RewardLines", col);
            TopLeft(lines, 0f, y, 600f, 260f);
            var vlg = lines.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            ui.rewardLinesParent = lines;

            ui.rewardLinePrefab = BuildRewardLinePrefab();
            SpawnSample(ui.rewardLinePrefab, lines, r => r.Bind("รอดถึงนาที 15", 750));
            SpawnSample(ui.rewardLinePrefab, lines, r => r.Bind("ศัตรูที่ล้ม 214 ตัว", 400));
            SpawnSample(ui.rewardLinePrefab, lines, r => { r.Bind("โบนัสชนะ", 90); r.SetDividerVisible(false); });
            y += 260f + 24f;                                       // แถวรวม padding 24px 0 0

            var totalLbl = NewText("TotalLabel", col, "ได้รับรวม", 26f, TextAlignmentOptions.MidlineLeft);
            totalLbl.color = Teal;
            TopLeft(totalLbl.rectTransform, 0f, y + 24f, 300f, 40f);

            var totalVal = NewText("TotalValue", col, "+1,240 G", 66f, TextAlignmentOptions.MidlineRight);
            totalVal.color = Teal;
            totalVal.lineSpacing = -9f;
            TopRight(totalVal.rectTransform, 0f, y, 400f, 88f);
            ui.goldEarnedLabel = totalVal;
            y += 88f + 22f;

            var div = NewImage("Divider", col, HairLine);
            TopLeft(div.rectTransform, 0f, y, 600f, 1f);
            y += 1f + 20f;

            var vaultLbl = NewText("VaultLabel", col, "ยอดทองในคลัง", 22f, TextAlignmentOptions.MidlineLeft);
            vaultLbl.color = new Color(1f, 1f, 1f, 0.55f);
            TopLeft(vaultLbl.rectTransform, 0f, y + 8f, 300f, 34f);

            var vaultVal = NewText("VaultValue", col, "8,430 G", 40f, TextAlignmentOptions.MidlineRight);
            TopRight(vaultVal.rectTransform, 0f, y, 400f, 50f);
            ui.goldTotalValueLabel = vaultVal;
        }

        // ── ปุ่ม ────────────────────────────────────────────────────────────
        private static void BuildButtons(RectTransform parent, WinLoseUI ui)
        {
            var bar = NewRect("Buttons", parent);
            BottomRight(bar, 120f, 104f, 600f, 130f);

            // เรียงจากขวาไปซ้าย: ปุ่มหลักอยู่ริมขวาสุด
            ui.playAgainButton = BuildButton(bar, "Btn_PlayAgain", "เล่นอีกครั้ง",
                                             primary: true, xFromRight: 0f, out var againGroup);
            ui.playAgainGroup = againGroup;
            ui.returnButton = BuildButton(bar, "Btn_Menu", "เมนูหลัก",
                                          primary: false, xFromRight: 290f + 18f, out _);

            var wait = NewText("WaitingForHost", bar, "รอโฮสต์เริ่มรอบใหม่", 17f, TextAlignmentOptions.MidlineRight);
            Mono(wait, 0.2f);
            wait.color = new Color(1f, 1f, 1f, 0.6f);
            BottomRight(wait.rectTransform, 0f, -34f, 600f, 26f);
            wait.gameObject.SetActive(false);
            ui.waitingForHostLabel = wait.gameObject;
        }

        private static Button BuildButton(RectTransform parent, string name, string label,
                                          bool primary, float xFromRight, out CanvasGroup group)
        {
            var rt = NewRect(name, parent);
            TopRight(rt, xFromRight, 0f, 290f, 84f);       // handoff: สูง 84 กว้าง 290
            group = rt.gameObject.AddComponent<CanvasGroup>();

            // พื้นหลังเอียง ตัวหนังสือไม่เอียง — TMP ไม่รับ BaseMeshEffect อยู่แล้ว
            // จึงต้องแยกเป็นสองชิ้น ไม่ใช่ใส่ UIShear ที่ตัวปุ่มทั้งอัน
            var bg = NewImage("Bg", rt, primary ? Primary : new Color(1f, 1f, 1f, 0f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            bg.gameObject.AddComponent<UIShear>().angleDegrees = 9f;   // CSS skewX(-9deg)

            // กรอบ 2px ของปุ่มรอง — **ห้ามใช้ UnityEngine.UI.Outline**
            // Outline ทำงานโดยก๊อป mesh ของ graphic ไปวาดซ้ำแบบเลื่อนตำแหน่ง
            // พื้นปุ่มรองเป็น alpha 0 เงาที่ก๊อปไปจึงโปร่งใสตาม = มองไม่เห็นอะไรเลย
            // ต้องวาดเป็นแถบสี่ด้านจริง และทั้งสี่ต้องเฉือนรอบจุดเดียวกัน (pivotOnParent)
            // ไม่งั้นแถบบนกับล่างจะเลื่อนคนละทางแล้วกรอบแตกออกจากกัน
            if (!primary) BuildSkewedBorder(rt, new Color(1f, 1f, 1f, 0.3f), 2f, 9f);

            var txt = NewText("Label", rt, label, 34f, TextAlignmentOptions.Midline);
            Stretch(txt.rectTransform);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            // ไม่ให้โฟกัสค้าง — บั๊กเดิมที่ handoff-ui-2026-08-22 §7 บันทึกไว้
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }

        /// <summary>
        /// กรอบเอียงที่ประกอบจากแถบสี่ด้าน · ทุกแถบเฉือนรอบกึ่งกลางพาเรนต์เดียวกัน
        /// แถบบน/ล่างยืดเต็มความกว้าง แถบซ้าย/ขวาเว้นความหนาไว้ไม่ให้มุมทับกันสองชั้น
        /// (ทับกันแล้วมุมจะเข้มกว่าด้านอื่นเพราะอัลฟาซ้อน)
        /// </summary>
        private static void BuildSkewedBorder(RectTransform target, Color color, float thickness, float shear)
        {
            Edge("Border_Top",    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness),  new Vector2(0f, 0f));
            Edge("Border_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness),  new Vector2(0f, 0f));
            Edge("Border_Left",   new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, -thickness * 2f), new Vector2(0f, 0f));
            Edge("Border_Right",  new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, -thickness * 2f), new Vector2(0f, 0f));

            void Edge(string name, Vector2 aMin, Vector2 aMax, Vector2 size, Vector2 pos)
            {
                var img = NewImage(name, target, color);
                var rt  = img.rectTransform;
                rt.anchorMin = aMin;
                rt.anchorMax = aMax;
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;

                var sh = img.gameObject.AddComponent<UIShear>();
                sh.angleDegrees  = shear;
                sh.pivotOnParent = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // PREFABS
        // ═══════════════════════════════════════════════════════════════════
        private static ResultPartyRowUI BuildPartyRowPrefab()
        {
            var rt = NewRect("ResultPartyRow", null);
            rt.sizeDelta = new Vector2(800f, 94f);                  // handoff: สูง 94

            var bg = NewImage("Bg", rt, RowBg);
            Stretch(bg.rectTransform);

            var accent = NewImage("Accent", rt, HostGold);          // เส้นซ้าย 6px
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(6f, 0f); art.anchoredPosition = Vector2.zero;

            // พอร์เทรต 8:1 — แถบยาวแนวนอน ไม่ใช่จัตุรัส (handoff เน้นข้อนี้)
            // 58 × 8 = 464 ใกล้ 470 ที่ handoff เขียนไว้ · ใช้ค่าที่หารลงตัวเพื่อให้
            // ทุกที่ในเกมใช้อัตราส่วนเดียวกันเป๊ะ ภาพชุดเดียวจึงใส่ได้หมด
            const float PortH = 58f;
            var port = P3RBuilderKit.PortraitWithName(rt, 22f, 18f, PortH, "Riven", 28f, out var name);

            float subX = 22f + PortH * P3RBuilderKit.PortraitAspect + 26f;
            var sub = NewText("Sub", rt, "P1 · HOST", 15f, TextAlignmentOptions.MidlineLeft);
            Mono(sub, 0.16f);
            sub.color = new Color(1f, 1f, 1f, 0.6f);
            TopLeft(sub.rectTransform, subX, 36f, 260f, 24f);

            var row = rt.gameObject.AddComponent<ResultPartyRowUI>();
            row.accentBar = accent; row.portrait = port;
            row.nameLabel = name;   row.subLabel = sub;

            return SavePrefab(rt.gameObject, PartyPrefab).GetComponent<ResultPartyRowUI>();
        }

        private static RewardLineUI BuildRewardLinePrefab()
        {
            var rt = NewRect("RewardLine", null);
            rt.sizeDelta = new Vector2(600f, 76f);                  // padding 18px 0 + เนื้อ ~40

            var reason = NewText("Reason", rt, "รอดถึงนาที 15", 24f, TextAlignmentOptions.MidlineLeft);
            reason.color = new Color(1f, 1f, 1f, 0.72f);
            TopLeft(reason.rectTransform, 0f, 18f, 380f, 40f);

            var value = NewText("Value", rt, "+750", 30f, TextAlignmentOptions.MidlineRight);
            Mono(value, 0.06f);
            TopRight(value.rectTransform, 0f, 18f, 220f, 40f);

            var div = NewImage("Divider", rt, new Color(1f, 1f, 1f, 0.1f));
            var drt = div.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f); drt.anchorMax = new Vector2(1f, 0f);
            drt.pivot = new Vector2(0.5f, 0f);
            drt.sizeDelta = new Vector2(0f, 1f); drt.anchoredPosition = Vector2.zero;

            var line = rt.gameObject.AddComponent<RewardLineUI>();
            line.reasonLabel = reason; line.valueLabel = value; line.divider = div;

            return SavePrefab(rt.gameObject, RewardPrefab).GetComponent<RewardLineUI>();
        }

        /// <summary>
        /// เรียกตัวของ kit — **ห้ามเขียนเองซ้ำ** ตัวของ kit มีตัวกันไม่ให้ทับงานที่แก้ด้วยมือ
        /// สำเนาที่เคยอยู่ตรงนี้ไม่มีตัวกัน ResultPartyRow กับ RewardLine จึงยังโดนทับได้
        /// ทั้งที่ prefab ตัวอื่นปลอดภัยแล้ว — เป็นกับดักแบบเดียวกับที่ P3RBuilderKit เกิดมาแก้
        /// </summary>
        private static GameObject SavePrefab(GameObject template, string path)
            => P3RBuilderKit.SavePrefab(template, path);

        /// <summary>วางตัวอย่างในซีนให้ดูเหมือน mockup — ตอนรันโค้ดล้างทิ้งแล้วสร้างใหม่จากข้อมูลจริง</summary>
        private static void SpawnSample<T>(T prefab, RectTransform parent, System.Action<T> bind)
            where T : MonoBehaviour
        {
            if (prefab == null) return;
            var clone = (T)PrefabUtility.InstantiatePrefab(prefab, parent);
            bind(clone);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text,
                                               float size, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (theme != null && theme.font != null) t.font = theme.font;
            return t;
        }

        /// <summary>
        /// ป้ายกำกับสไตล์ mono ของแบบ — โปรเจกต์มีแต่ Sarabun ไม่มีฟอนต์ mono จริง
        /// จึงเลียนด้วยตัวพิมพ์ใหญ่ + ถ่างระยะตัวอักษร ซึ่งให้ความรู้สึกใกล้ที่สุด
        /// ถ้าวันหนึ่งซื้อฟอนต์ mono มา ให้เปลี่ยนที่เมธอดนี้ที่เดียว
        /// </summary>
        private static void Mono(TextMeshProUGUI t, float emSpacing)
        {
            P3RText.SetTracking(t, emSpacing * 100f);   // TMP นับเป็น % ของ em
            P3RText.TryUpperCase(t);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void TopRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-x, -y);
        }

        private static void BottomRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-x, y);
        }
    }
}
