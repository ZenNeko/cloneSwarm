using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างซีนต้นแบบจอ JOIN ROOM — กล่องใส่รหัสห้องที่ลอยทับจออื่น
    /// เมนู: Tools > Clone Swarm > Build P3R Join Room Scene
    ///
    /// **ตัวนี้เป็น modal ไม่ใช่จอเต็ม** — ต่างจากอีกสิบเอ็ดจอตรงที่มันไม่เคยอยู่ลำพัง
    /// มันลอยทับเมนูหลักหรือล็อบบี้ที่เปิดอยู่ · โครงจึงเป็น
    ///
    ///   P3R_JoinRoom      ← **เปิดค้างไว้เสมอ** (JoinRoomPanel.Awake ตั้ง Instance ตรงนี้)
    ///   └─ PanelRoot      ← ตัวที่เปิด/ปิดจริง · Open() / Close() แตะตัวนี้
    ///      ├─ Scrim       แผ่นทึบกันคลิกทะลุไปโดนของข้างหลัง
    ///      └─ Dialog      กล่องกลางจอ
    ///
    /// ถ้าเอา JoinRoomPanel ไปแปะบน GameObject ที่ปิดอยู่ `Awake` จะไม่ทำงาน
    /// `Instance` เป็น null แล้วปุ่ม JOIN ตายเงียบทั้งในเมนูหลักและในล็อบบี้
    /// — P3RScreenWirer จึงมี P3R_JoinRoom อยู่ในรายชื่อ panel ที่ต้องเปิดค้าง
    ///
    /// **ไม่มีปุ่มไหนต่อสายที่นี่** — `JoinRoomPanel.Start()` ผูก listener เองทั้งหมด
    /// builder มีหน้าที่แค่วางของแล้วยัดลงช่อง Inspector ให้ถูกตัว
    /// </summary>
    public static class P3RJoinRoomSceneBuilder
    {
        private const string ScenePath = "Assets/GameScenes/Proto_JoinRoom.unity";

        private const float DialogW = 720f;
        private const float DialogH = 420f;
        private const float Pad     = 44f;

        private static readonly Color TopBar  = new Color32(0x08, 0x0B, 0x18, 0xFF);
        private static readonly Color PanelBg = new Color32(0x0D, 0x12, 0x26, 0xFF);
        private static readonly Color FieldBg = new Color32(0x11, 0x18, 0x38, 0xFF);

        [MenuItem("Tools/Clone Swarm/Build P3R Join Room Scene")]
        public static void Build()
        {
            if (!BeginScene(ScenePath, "JOIN ROOM", out var scene)) return;

            BuildCamera(InkDeep);
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildScreen(canvas);

            EndScene(scene, ScenePath,
                "ขั้นถัดไป:\n" +
                "  1) P3R_JoinRoom ต้อง **เปิดค้างไว้** ในซีนจริง — JoinRoomPanel.Awake ตั้ง Instance\n" +
                "     ตัวที่เปิด/ปิดคือลูกชื่อ PanelRoot ซึ่ง Awake ปิดให้เองแล้ว\n" +
                "  2) ไม่ต้องต่อปุ่มเอง — JoinRoomPanel.Start() ผูก confirm/cancel ให้\n" +
                "  3) ข้อความสถานะมาจาก GameSessionManager.OnStatus (เช่น 'ไม่พบห้องนี้')");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildScreen(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;
            var ui   = root.gameObject.AddComponent<JoinRoomPanel>();

            // ตัวที่เปิด/ปิด — **ห้ามเป็นตัวเดียวกับที่แปะสคริปต์** ไม่งั้น Awake ไม่วิ่ง
            var panelRoot = NewRect("PanelRoot", root);
            Stretch(panelRoot);
            ui.panelRoot = panelRoot.gameObject;

            // แผ่นทึบกันคลิกทะลุ — modal ที่ปล่อยให้กดของข้างหลังได้คือ modal ที่พัง
            var scrim = NewImage("Scrim", panelRoot, new Color(6 / 255f, 8 / 255f, 18 / 255f, 0.86f));
            Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;

            BuildDialog(panelRoot, ui);
        }

        private static void BuildDialog(RectTransform parent, JoinRoomPanel ui)
        {
            var dialog = NewImage("Dialog", parent, PanelBg);
            var drt = dialog.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot     = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(DialogW, DialogH);
            drt.anchoredPosition = Vector2.zero;
            AccentBar(drt, Teal);

            var head = NewImage("Head", drt, TopBar);
            var hrt = head.rectTransform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(0f, 74f);
            hrt.anchoredPosition = Vector2.zero;

            var title = NewMono("Title", hrt, "เข้าห้องเพื่อน", 24f, 0.12f,
                                TextAlignmentOptions.MidlineLeft);
            TopLeft(title.rectTransform, Pad, 22f, 420f, 34f);

            var hint = NewMono("Hint", drt, "ใส่รหัสห้อง 6 ตัวที่เพื่อนส่งมา", 16f, 0.06f,
                               TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
            TopLeft(hint.rectTransform, Pad, 100f, DialogW - Pad * 2f, 26f);

            ui.codeInput  = BuildCodeField(drt);
            ui.statusText = BuildStatus(drt);

            // ปุ่มยืนยันชิดขวา ยกเลิกชิดซ้าย — ทิศเดียวกับแถบล่างของทุกจอในเกม
            ui.cancelButton  = DialogButton(drt, "Cancel",  "ยกเลิก", left: true,  x: Pad,
                                            w: 200f, filled: false, tint: new Color(1f, 1f, 1f, 0.7f));
            ui.confirmButton = DialogButton(drt, "Confirm", "เข้าห้อง", left: false, x: Pad,
                                            w: 240f, filled: true,  tint: Teal);
        }

        /// <summary>
        /// ช่องรหัสห้อง — ตัวใหญ่ ถ่างระยะ อ่านทีละตัวได้
        ///
        /// `TMP_InputField` ต้องมี `textViewport` + `textComponent` ครบถึงจะพิมพ์ได้
        /// ขาดตัวใดตัวหนึ่งช่องจะรับคลิกแต่ไม่มีตัวหนังสือโผล่ ซึ่งดูเหมือนคีย์บอร์ดพัง
        /// </summary>
        private static TMP_InputField BuildCodeField(RectTransform dialog)
        {
            var field = NewImage("CodeInput", dialog, FieldBg);
            TopLeft(field.rectTransform, Pad, 138f, DialogW - Pad * 2f, 82f);
            field.raycastTarget = true;
            AddBorder(field.rectTransform, "Border", 1.5f, Over(Teal, FieldBg, 0.45f));

            var viewport = NewRect("TextArea", field.rectTransform);
            viewport.anchorMin = new Vector2(0f, 0f); viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(22f, 6f); viewport.offsetMax = new Vector2(-22f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = NewMono("Text", viewport, "", 34f, 0.3f, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);

            var placeholder = NewMono("Placeholder", viewport, "ABC123", 34f, 0.3f,
                                      TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.22f));
            Stretch(placeholder.rectTransform);

            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic  = field;
            input.textViewport   = viewport;
            input.textComponent  = text;
            input.placeholder    = placeholder;
            input.characterLimit = 12;
            // รหัสห้องมีแต่ตัวอักษรกับตัวเลข — กันอักขระอื่นตั้งแต่ตอนพิมพ์ ดีกว่าไปเจอ
            // "ไม่พบห้องนี้" ทีหลังแล้วเดาไม่ออกว่าพิมพ์อะไรผิด (ตัวพิมพ์เล็ก/ใหญ่ไม่เกี่ยง)
            input.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
            input.onFocusSelectAll = true;

            return input;
        }

        private static TextMeshProUGUI BuildStatus(RectTransform dialog)
        {
            // ข้อความจาก GameSessionManager.OnStatus — ทั้งความคืบหน้าและข้อความ error
            // เริ่มด้วยค่าว่างเสมอ (Open() ล้างให้) ไม่งั้นจะค้างข้อความของครั้งก่อน
            var status = NewMono("Status", dialog, "", 16f, 0.04f,
                                 TextAlignmentOptions.MidlineLeft, Amber);
            TopLeft(status.rectTransform, Pad, 232f, DialogW - Pad * 2f, 48f);
            Wrap(status);
            return status;
        }

        private static Button DialogButton(RectTransform dialog, string name, string text,
                                           bool left, float x, float w, bool filled, Color tint)
        {
            const float h = 56f;
            var root = NewRect($"Btn_{name}", dialog);
            if (left) BottomLeft(root, x, Pad, w, h);
            else      BottomRight(root, x, Pad, w, h);

            var bg = NewImage("Bg", root, filled ? tint : Over(tint, PanelBg, 0.10f));
            Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            Shear(bg);

            if (!filled) AddBorder(root, "Border", 1.5f, tint, shear: 9f);

            var label = NewMono("Label", root, text, 18f, 0.14f, TextAlignmentOptions.Center,
                                filled ? InkDeep : tint);
            Stretch(label.rectTransform);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            return btn;
        }
    }
}
