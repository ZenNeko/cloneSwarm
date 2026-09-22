using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ย้ายจอต้นแบบ `Proto_*.unity` เข้าไปเป็น panel ในซีนจริง — **ทีละจอ**
    /// เมนู: Tools > Clone Swarm > Migrate to Scene > …
    ///
    /// ═══ วิธีที่ใช้ และทำไมถึงใช้วิธีนี้ ═══
    ///
    /// builder ส่วนใหญ่วาง component ของจอ (`LobbyUI` · `TalentShopUI` · `TitleScreenUI` …)
    /// ไว้บน **Canvas root** ของซีนต้นแบบ ไม่ใช่บน panel ย่อย
    /// ถ้าย้ายเฉพาะลูกของ Canvas จะได้กล่องสวยๆ ที่ไม่มี component และสายที่ต่อไว้ขาดหมด
    ///
    /// ที่ทำแทน: ก๊อป **ทั้งก้อน Canvas** ไปเป็นลูกของ Canvas ในซีนจริง แล้ว**ถอด**
    /// `Canvas` / `CanvasScaler` / `GraphicRaycaster` ออกจากตัวที่ก๊อปมา
    /// เหลือ `RectTransform` ที่ยืดเต็มพ่อ = กลายเป็น panel ธรรมดาที่ component ยังอยู่ครบ
    /// `Object.Instantiate` รีแมปสายภายในก้อนให้เอง (เช่น `cardTemplate` ที่ชี้ไปลูกของตัวเอง)
    /// ส่วนสายที่ชี้ไป **asset** (prefab · sprite · ScriptableObject) ไม่ถูกแตะอยู่แล้ว
    ///
    /// ผลพลอยได้: component ไปอยู่บน panel ที่ถูกเปิด/ปิดจริง — `OnEnable` จึงวิ่งตอนเปิดแผง
    /// ไม่ใช่ตอนโหลดซีน ซึ่งเป็นปัญหา lifecycle เดียวกับที่ ADR-001 บ่นเรื่อง `TalentShopUI`
    ///
    /// ═══ สิ่งที่เครื่องมือนี้ **ไม่** ทำ และเป็นความตั้งใจ ═══
    ///
    /// **ไม่แตะสายของเดิม** — ไม่เขียนทับ `MenuManager.lobbyPanel` หรือ `TabBar.tabs[].panel`
    /// ให้เอง · panel ใหม่ถูกใส่เข้าไปแบบ **ปิดไว้** แล้วรายงานว่าต้องต่ออะไรบ้าง
    /// เหตุผล: การเขียนทับ field ในซีนที่ทำงานอยู่คือการทิ้งของเดิมโดยที่ยังไม่มีใครดูเทียบ
    /// เลย และถ้า panel ใหม่ยังไม่ครบ จะไม่เหลือทางกลับ · ต่อสายเองใช้เวลาสิบวินาที
    ///
    /// **ไม่ลบ panel เดิม** — ปล่อยให้อยู่ในซีนต่อไป ลบเองเมื่อมั่นใจแล้ว
    ///
    /// panel ใหม่ปิดอยู่ = component บนมันไม่ทำงาน (`OnEnable` ไม่วิ่ง ไม่ subscribe อะไร)
    /// จึงอยู่ร่วมกับของเดิมได้โดยไม่ชนกัน จนกว่าจะมีคนสลับสายเอง
    ///
    /// รันซ้ำได้ — เจอ panel ชื่อเดิมอยู่แล้วจะถามก่อนแล้วสร้างทับที่ตำแหน่งเดิม
    /// </summary>
    public static class P3RScreenMigrator
    {
        private const string MenuRoot = "Tools/Clone Swarm/Migrate to Scene/";

        private const string MenuScene   = "Assets/GameScenes/MenuScene.unity";
        private const string SampleScene = "Assets/GameScenes/SampleScene.unity";

        // ═══════════════════════════════════════════════════════════════════
        private readonly struct Screen
        {
            public readonly string Proto;
            public readonly string Target;
            public readonly string PanelName;
            public readonly string Wiring;

            public Screen(string proto, string target, string panelName, string wiring)
            {
                Proto = $"Assets/GameScenes/{proto}.unity";
                Target = target; PanelName = panelName; Wiring = wiring;
            }
        }

        private static readonly Dictionary<string, Screen> Screens = new()
        {
            ["TITLE"] = new Screen("Proto_Title", MenuScene, "P3R_Title",
                "ยังไม่มี field รองรับ — ต้องตัดสินก่อนว่าจะให้ MenuManager ถือ หรือให้ Title\n" +
                "   เป็นตัวเปิดเกมแล้วเรียก ShowPanel(mainPanel) ผ่าน TitleScreenUI.onAdvanceEvent"),

            ["MAIN MENU"] = new Screen("Proto_P3RMenu", MenuScene, "P3R_Main",
                "ต่อเข้า MenuManager.mainPanel (ของเดิมชี้ไป panel เก่าอยู่)"),

            ["CHARACTER"] = new Screen("Proto_Character", MenuScene, "P3R_Character",
                "ต่อเข้า TabBar.tabs[id=\"character\"].panel"),

            ["LOBBY"] = new Screen("Proto_Lobby", MenuScene, "P3R_Lobby",
                "ต่อเข้า MenuManager.lobbyPanel และ MenuManager.lobbyUI (LobbyUI อยู่บน panel นี้)\n" +
                "   และ TabBar.tabs[id=\"lobby\"].panel"),

            ["MAP SELECT"] = new Screen("Proto_MapSelect", MenuScene, "P3R_MapSelect",
                "ต่อเข้า TabBar.tabs[id=\"map\"].panel · และตั้ง MapSelectUI.lobbyUI ให้ชี้ LobbyUI ตัวจริง"),

            ["TALENT SHOP"] = new Screen("Proto_TalentShop", MenuScene, "P3R_TalentShop",
                "ต่อเข้า TabBar.tabs[id=\"shop\"].panel"),

            ["CONFIG"] = new Screen("Proto_Config", MenuScene, "P3R_Config",
                "ต่อเข้า MenuManager.settingsPanel และ MenuManager.settingsMenuUI"),

            ["LOADING"] = new Screen("Proto_Loading", MenuScene, "P3R_Loading",
                "ต่อเข้า MenuManager.loadingPanel · loadingText ของเดิมยังใช้ได้หรือชี้ไป\n" +
                "   LoadingScreenUI.contextLabel ก็ได้"),

            ["JOIN ROOM"] = new Screen("Proto_JoinRoom", MenuScene, "P3R_JoinRoom",
                "**ต้องเปิดค้างไว้** — JoinRoomPanel.Awake ตั้ง Instance ซึ่งไม่วิ่งบน GameObject ที่ปิด · " +
                "MenuManager.OnJoinRoomClicked() กับ LobbyUI.OnLobbyJoinClicked() เรียกผ่าน Instance ทั้งคู่"),

            ["LEVEL UP"] = new Screen("Proto_LevelUp", SampleScene, "P3R_LevelUp",
                "LevelUpUI อยู่ใน SampleScene แล้ว — ต่อสายที่เรียกใช้ให้ชี้ตัวใหม่ แล้วลบตัวเก่า"),

            ["PAUSED"] = new Screen("Proto_Pause", SampleScene, "P3R_Pause",
                "PauseMenuUI อยู่ใน SampleScene แล้ว — สลับสายแล้วลบตัวเก่า"),

            ["WIN / LOSE"] = new Screen("Proto_WinLose", SampleScene, "P3R_WinLose",
                "WinLoseUI อยู่ใน SampleScene แล้ว — สลับสายแล้วลบตัวเก่า"),
        };

        // ═══════════════════════════════════════════════════════════════════
        // MENU — ทีละจอตามที่ขอ
        // ═══════════════════════════════════════════════════════════════════
        [MenuItem(MenuRoot + "0 · รายงานอย่างเดียว ไม่แก้อะไร", priority = 0)]
        public static void Report()
        {
            var sb = new System.Text.StringBuilder("[Migrate] สถานะการย้ายจอต้นแบบลงซีนจริง\n\n");

            foreach (var kv in Screens)
            {
                bool protoExists = File.Exists(kv.Value.Proto);
                string target = Path.GetFileNameWithoutExtension(kv.Value.Target);
                bool already = protoExists && PanelExistsInScene(kv.Value.Target, kv.Value.PanelName);

                sb.AppendLine($"{kv.Key,-12} → {target,-12} {kv.Value.PanelName,-20} " +
                              (!protoExists ? "ไม่มีซีนต้นแบบ"
                               : already     ? "ย้ายแล้ว"
                                             : "ยังไม่ย้าย"));
            }

            sb.AppendLine();
            sb.AppendLine("GAMEPLAY HUD ไม่มีในรายการโดยตั้งใจ — Proto_GameplayHUD เป็นภาพอ้างอิงล้วน");
            sb.AppendLine("ไม่มี component จริงสักตัว · ย้ายเข้า SampleScene จะได้ HUD ปลอมทับ HUD จริง");
            sb.AppendLine("การรีสกิน HUD คือไปแก้สี/ฟอนต์ของ component ที่มีอยู่ ไม่ใช่ย้ายซีน");
            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuRoot + "TITLE",       priority = 20)] private static void M1()  => Migrate("TITLE");
        [MenuItem(MenuRoot + "MAIN MENU",   priority = 21)] private static void M2()  => Migrate("MAIN MENU");
        [MenuItem(MenuRoot + "CHARACTER",   priority = 22)] private static void M3()  => Migrate("CHARACTER");
        [MenuItem(MenuRoot + "LOBBY",       priority = 23)] private static void M4()  => Migrate("LOBBY");
        [MenuItem(MenuRoot + "MAP SELECT",  priority = 24)] private static void M5()  => Migrate("MAP SELECT");
        [MenuItem(MenuRoot + "TALENT SHOP", priority = 25)] private static void M6()  => Migrate("TALENT SHOP");
        [MenuItem(MenuRoot + "CONFIG",      priority = 26)] private static void M7()  => Migrate("CONFIG");
        [MenuItem(MenuRoot + "LOADING",     priority = 27)] private static void M8()  => Migrate("LOADING");
        [MenuItem(MenuRoot + "LEVEL UP",    priority = 40)] private static void M9()  => Migrate("LEVEL UP");
        [MenuItem(MenuRoot + "PAUSED",      priority = 41)] private static void M10() => Migrate("PAUSED");
        [MenuItem(MenuRoot + "WIN - LOSE",  priority = 42)] private static void M11() => Migrate("WIN / LOSE");

        /// <summary>ให้ batchmode เรียกได้: -executeMethod … .MigrateFromCommandLine -screen "LOBBY"</summary>
        public static void MigrateFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-screen") { Migrate(args[i + 1]); return; }

            Debug.LogError("[Migrate] ต้องส่ง -screen \"<ชื่อจอ>\" มาด้วย");
        }

        // ═══════════════════════════════════════════════════════════════════
        public static void Migrate(string key)
        {
            if (!Screens.TryGetValue(key, out var s))
            {
                Debug.LogError($"[Migrate] ไม่รู้จักจอ '{key}' · ที่มีคือ: {string.Join(" · ", Screens.Keys)}");
                return;
            }

            if (!File.Exists(s.Proto))   { Debug.LogError($"[Migrate] ไม่มี {s.Proto} — สร้างซีนต้นแบบก่อน"); return; }
            if (!File.Exists(s.Target))  { Debug.LogError($"[Migrate] ไม่มี {s.Target}"); return; }

            string targetName = Path.GetFileNameWithoutExtension(s.Target);
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog($"ย้ายจอ {key} ลง {targetName}",
                    $"จะเพิ่ม '{s.PanelName}' เข้าไปใน {targetName} แบบ **ปิดไว้**\n\n" +
                    "ไม่แตะสายของเดิม · ไม่ลบ panel เก่า\n" +
                    "ต่อสายเองหลังจากนี้ (จะบอกไว้ใน Console)",
                    "ย้ายเลย", "ยกเลิก"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ── เปิดซีนจริงก่อน แล้วค่อยเปิดต้นแบบซ้อนเข้ามา ──────────────────
            var targetScene = EditorSceneManager.OpenScene(s.Target, OpenSceneMode.Single);

            var targetCanvas = FindMainCanvas(targetScene);
            if (targetCanvas == null)
            {
                Debug.LogError($"[Migrate] หา Canvas ใน {targetName} ไม่เจอ");
                return;
            }

            var protoScene  = EditorSceneManager.OpenScene(s.Proto, OpenSceneMode.Additive);
            var protoCanvas = FindMainCanvas(protoScene);
            if (protoCanvas == null)
            {
                Debug.LogError($"[Migrate] หา Canvas ใน {Path.GetFileName(s.Proto)} ไม่เจอ");
                EditorSceneManager.CloseScene(protoScene, true);
                return;
            }

            // ── ของเดิมชื่อเดียวกัน: จำ **พ่อกับลำดับ** ไว้แล้วลบ ──────────────
            // ต้องจำพ่อด้วย ไม่ใช่แค่ลำดับ — สี่จอแท็บถูกย้ายไปอยู่ใต้ P3R_Hub แล้ว
            // วางใบใหม่กลับที่ Canvas จะได้ panel ซ้อนสองใบที่ไม่มีใครสังเกต
            int siblingIndex = -1;
            Transform parentSlot = targetCanvas.transform;
            Dictionary<string, List<Object>> carried = null;
            var existing = FindChildByName(targetCanvas.transform, s.PanelName);
            if (existing != null)
            {
                if (!Application.isBatchMode &&
                    !EditorUtility.DisplayDialog("มี panel ชื่อนี้อยู่แล้ว",
                        $"'{s.PanelName}' มีอยู่ใน {targetName} แล้ว\n\n" +
                        "สร้างทับ? งานที่จัดมือบน panel นั้นจะหายทั้งหมด",
                        "สร้างทับ", "ยกเลิก"))
                {
                    EditorSceneManager.CloseScene(protoScene, true);
                    return;
                }
                siblingIndex = existing.GetSiblingIndex();
                parentSlot   = existing.parent != null ? existing.parent : targetCanvas.transform;
                carried      = SnapshotAssetData(existing.gameObject);
                Object.DestroyImmediate(existing.gameObject);
            }

            // ── ก๊อปทั้งก้อน Canvas เข้าไปเป็นลูกของ Canvas จริง ──────────────
            // Instantiate พร้อม parent = ของที่ได้ไปอยู่ในซีนของ parent เลย
            // และสายภายในก้อน (เช่น cardTemplate ที่ชี้ไปลูกตัวเอง) ถูกรีแมปให้อัตโนมัติ
            var panel = Object.Instantiate(protoCanvas.gameObject, parentSlot);
            panel.name = s.PanelName;

            StripCanvasComponents(panel);
            StretchToParent(panel);
            RestoreAssetData(panel, carried);

            if (siblingIndex >= 0) panel.transform.SetSiblingIndex(siblingIndex);

            // ปิดไว้เป็นค่าปริยาย — component บน GameObject ที่ปิดอยู่จะไม่ OnEnable
            // ไม่ subscribe อะไร จึงอยู่ร่วมกับ panel เดิมได้โดยไม่ชนกันจนกว่าจะมีคนสลับสายเอง
            //
            // **ยกเว้นจอที่ถือ singleton** — `LevelUpUI` / `WinLoseUI` / `PauseMenuUI`
            // ตั้ง `Instance` ใน `Awake` ซึ่งไม่วิ่งบน object ที่ปิดอยู่ · ปิดทิ้งไว้แปลว่า
            // `Instance` เป็น null ตลอดเกม จอเลเวลอัปไม่เด้ง จอจบเกมไม่ขึ้น
            // และ **ไม่มีใครเปิดกลับให้** เพราะสิ่งเดียวที่จะเปิดคือจอเหล่านั้นเอง
            //
            // อาการนี้เกิดซ้ำมาหลายรอบแล้วและต้องไล่เปิดคืนด้วยมือทุกครั้ง — ต้นเหตุคือ
            // บรรทัดนี้ · จอพวกนี้ซ่อนตัวเองใน Awake/Start อยู่แล้ว "เปิด" ไม่ได้แปลว่า "เห็น"
            bool mustStayActive = System.Array.IndexOf(
                P3RPanelActivationFix.MustBeActive, s.PanelName) >= 0;

            panel.SetActive(mustStayActive);
            if (mustStayActive)
                Debug.Log($"[Migrate] '{s.PanelName}' เปิดค้างไว้ — เป็นจอที่ถือ singleton " +
                          "ถ้าปิด Awake ไม่วิ่งแล้ว Instance เป็น null ตลอดเกม " +
                          "(จอพวกนี้ซ่อนตัวเองใน Awake/Start อยู่แล้ว เปิดไม่ได้แปลว่าเห็น)");

            var duplicates = FindDuplicateComponents(targetScene, panel);

            EditorSceneManager.CloseScene(protoScene, true);
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);

            EditorGUIUtility.PingObject(panel);
            Selection.activeGameObject = panel;

            LogResult(key, s, targetName, panel, duplicates);
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void LogResult(string key, Screen s, string targetName,
                                      GameObject panel, List<string> duplicates)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Migrate] ย้าย {key} เข้า {targetName} เรียบร้อย — '{s.PanelName}' (ปิดไว้)");
            sb.AppendLine();
            sb.AppendLine("ต้องต่อสายเอง:");
            sb.AppendLine($"   {s.Wiring}");

            if (duplicates.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("component ชนิดเดียวกันที่มีอยู่แล้วในซีน (ของเดิม ยังไม่ได้ลบให้):");
                foreach (var d in duplicates) sb.AppendLine($"   {d}");
                sb.AppendLine("   ตอนนี้ยังไม่ชนกันเพราะ panel ใหม่ปิดอยู่");
                sb.AppendLine("   สลับสายให้ชี้ตัวใหม่ ทดสอบ แล้วค่อยลบของเก่า");
            }

            sb.AppendLine();
            sb.AppendLine("กลับได้ตลอด — ลบ GameObject นี้ทิ้ง ซีนก็กลับเป็นเหมือนเดิม");
            Debug.Log(sb.ToString(), panel);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// Canvas หลักของซีน = ตัวที่เป็น root และมีลูกเยอะที่สุด
        /// ซีนเกมมี Canvas หลายตัวได้ (HUD แยกชั้น) — เอาตัวที่มีของอยู่จริง
        /// </summary>
        private static Canvas FindMainCanvas(Scene scene)
        {
            Canvas best = null;
            int bestCount = -1;

            foreach (var root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas == null) continue;
                int count = root.transform.childCount;
                if (count > bestCount) { bestCount = count; best = canvas; }
            }
            return best;
        }

        // ═══════════════════════════════════════════════════════════════════
        // ยกข้อมูล asset ข้ามการสร้างทับ
        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// เก็บ **เฉพาะช่องที่ชี้ asset ในโปรเจกต์** ของทุกสคริปต์บน panel ที่กำลังจะถูกลบ
        ///
        /// ช่องพวกนี้คือของที่ **ไม่มีแหล่งอื่นให้ดึงกลับ** — `CharacterSelectUI.characters`
        /// กับ `LobbyUI.maps` ถูกลากใส่ใน Inspector ไม่ได้อยู่ในโค้ดหรือใน builder
        /// ลบ panel ทิ้งแล้วสร้างใหม่ = จอเลือกตัวละครว่างเปล่า กดอะไรก็ไม่มีอะไรเกิดขึ้น
        /// ซึ่งเป็นอาการที่ดูไม่ออกว่ามาจากการย้ายจอ
        ///
        /// ช่องที่ชี้ของ **ในซีน** ไม่ยกมา — ของที่มันชี้อยู่กำลังจะถูกลบไปพร้อมกัน
        /// สายพวกนั้นเป็นหน้าที่ของ <c>P3RScreenWirer</c> ซึ่งรู้จักโครงของซีนจริง
        /// </summary>
        private static Dictionary<string, List<Object>> SnapshotAssetData(GameObject panel)
        {
            var snap = new Dictionary<string, List<Object>>();

            foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !IsProjectType(mb.GetType())) continue;

                var it = new SerializedObject(mb).GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.name == "m_Script") continue;

                    string key = $"{mb.GetType().Name}.{it.propertyPath}";
                    if (snap.ContainsKey(key)) continue;   // ตัวแรกที่เจอชนะ

                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var v = it.objectReferenceValue;
                        if (v != null && EditorUtility.IsPersistent(v))
                            snap[key] = new List<Object> { v };
                    }
                    else if (it.isArray && it.propertyType == SerializedPropertyType.Generic
                             && it.arraySize > 0)
                    {
                        var list = new List<Object>(it.arraySize);
                        bool ok = true;
                        for (int i = 0; i < it.arraySize && ok; i++)
                        {
                            var e = it.GetArrayElementAtIndex(i);
                            if (e.propertyType != SerializedPropertyType.ObjectReference) { ok = false; break; }
                            var v = e.objectReferenceValue;
                            if (v != null && !EditorUtility.IsPersistent(v)) { ok = false; break; }
                            list.Add(v);
                        }
                        // ลิสต์ที่มีแต่ null ไม่ต้องยก — ยกไปก็ได้ความว่างเท่าเดิม
                        if (ok && list.Exists(v => v != null)) snap[key] = list;
                    }
                }
            }
            return snap;
        }

        /// <summary>
        /// ใส่ค่าที่เก็บไว้กลับลง panel ใหม่ — **เฉพาะช่องที่ยังว่าง** เท่านั้น
        /// ค่าที่ builder ตั้งมาถือว่าตั้งใจเสมอ ของเก่าห้ามทับ
        /// </summary>
        private static void RestoreAssetData(GameObject panel, Dictionary<string, List<Object>> snap)
        {
            if (snap == null || snap.Count == 0) return;

            int restored = 0;
            foreach (var mb in panel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !IsProjectType(mb.GetType())) continue;

                var so = new SerializedObject(mb);
                bool changed = false;

                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.name == "m_Script") continue;
                    if (!snap.TryGetValue($"{mb.GetType().Name}.{it.propertyPath}", out var saved)) continue;

                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (it.objectReferenceValue != null) continue;
                        it.objectReferenceValue = saved[0];
                    }
                    else if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
                    {
                        if (it.arraySize > 0) continue;
                        it.arraySize = saved.Count;
                        for (int i = 0; i < saved.Count; i++)
                            it.GetArrayElementAtIndex(i).objectReferenceValue = saved[i];
                    }
                    else continue;

                    changed = true;
                    restored++;
                }

                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (restored > 0)
                Debug.Log($"[Migrate] ยกข้อมูล asset จาก panel เดิมมาใส่ตัวใหม่ {restored} ช่อง " +
                          "(ลิสต์ตัวละคร/แมพ และของที่ลากใส่ Inspector ไว้)");
        }

        /// <summary>
        /// หา panel ชื่อนี้ **ทั้งใต้ Canvas** ไม่ใช่แค่ลูกชั้นแรก
        ///
        /// เคยหาแค่ลูกชั้นแรกแล้วพังเงียบ — `P3RScreenWirer.EnsureHub` ย้ายสี่จอแท็บ
        /// ไปไว้ใต้ `P3R_Hub` หลังการย้ายรอบแรก · พอย้ายซ้ำ ตัวหาไม่เจอของเดิมที่ลึกลงไป
        /// เลยสร้างใบใหม่ไว้ที่ Canvas แทนที่จะทับใบเก่า ได้ panel ชื่อซ้ำสองใบ
        /// ใบเก่าถือข้อมูลจริง (characters 3 ตัว) ใบใหม่ถือหน้าตาใหม่แต่ข้อมูลว่าง
        /// และตัวคุมจอที่เกมใช้คือใบเก่า — แก้ builder แล้วไม่เห็นผลเลยสักอย่าง
        ///
        /// คืน Transform ของใบเดิมเพื่อให้ผู้เรียกวางใบใหม่ไว้ **ที่เดิมทั้งพ่อและลำดับ**
        /// การย้ายซ้ำจึงได้ผลเหมือนเดิมทุกครั้ง ไม่ว่าจะย้ายไปแล้วกี่รอบ
        /// </summary>
        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var deeper = FindChildByName(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>
        /// ถอดของที่ทำให้มันเป็น "Canvas" ออก เหลือแค่ RectTransform
        /// Canvas ซ้อน Canvas ทำได้ในทางเทคนิค แต่จะได้ batching boundary เพิ่มมาฟรีๆ
        /// และ CanvasScaler ตัวในจะถูกเพิกเฉยเงียบๆ ซึ่งสับสนเวลาไล่บั๊กทีหลัง
        /// </summary>
        private static void StripCanvasComponents(GameObject go)
        {
            // เรียงจากตัวที่ขึ้นกับตัวอื่นก่อน — GraphicRaycaster ต้องการ Canvas
            foreach (var t in new[] { typeof(GraphicRaycaster), typeof(CanvasScaler), typeof(Canvas) })
            {
                var c = go.GetComponent(t);
                if (c != null) Object.DestroyImmediate(c);
            }
        }

        private static void StretchToParent(GameObject go)
        {
            if (go.transform is not RectTransform rt) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// หา component ชนิดเดียวกับที่อยู่บน panel ใหม่ ที่มีอยู่แล้วที่อื่นในซีน
        /// ไม่ได้ลบให้ แค่บอก — การลบของเดิมเป็นการตัดสินใจที่ต้องดูด้วยตาก่อน
        /// </summary>
        private static List<string> FindDuplicateComponents(Scene scene, GameObject panel)
        {
            // ต้องดูทั้งก้อน ไม่ใช่แค่ root — บางจอ (เช่น CONFIG) วาง component ไว้บน panel
            // ชั้นในไม่ใช่บน Canvas root · ถ้าดูแค่ root จะรายงานว่าไม่มีตัวซ้ำทั้งที่มี
            // เอาเฉพาะชนิดที่มี **ตัวเดียว** ในก้อนที่ย้ายมา = ตัวคุมจอ (LobbyUI · TalentShopUI)
            // ชนิดที่มีหลายตัว (UIShear · P3RSegmentButton) เป็น widget ระดับชิ้น
            // มันซ้ำกันเองอยู่แล้วทั้งซีน การรายงานว่าซ้ำคือเสียงรบกวนล้วนๆ
            var mine = panel.GetComponentsInChildren<MonoBehaviour>(true)
                            .Where(m => m != null)
                            .Select(m => m.GetType())
                            .Where(IsProjectType)
                            .GroupBy(t => t)
                            .Where(g => g.Count() == 1)
                            .Select(g => g.Key)
                            .ToHashSet();

            var found = new List<string>();
            if (mine.Count == 0) return found;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (!mine.Contains(mb.GetType())) continue;
                    if (mb.transform.IsChildOf(panel.transform)) continue;

                    found.Add($"{mb.GetType().Name}  บน  {HierarchyPath(mb.transform)}");
                }
            }
            return found;
        }

        /// <summary>
        /// นับเฉพาะ component ที่เราเขียนเอง — `Image` · `Button` · `TextMeshProUGUI`
        /// มีอยู่ทุกที่ในทุกซีนอยู่แล้ว การรายงานว่ามันซ้ำคือเสียงรบกวนล้วนๆ
        /// ที่อยากรู้คือ "จอนี้มีตัวคุมอยู่แล้วที่อื่นไหม" เช่น LobbyUI หรือ SettingsMenuUI
        /// </summary>
        private static bool IsProjectType(System.Type t)
        {
            string ns = t.Namespace ?? "";
            return !(ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor") ||
                     ns.StartsWith("Unity.")      || ns.StartsWith("TMPro")       ||
                     ns.StartsWith("PhEngine"));
        }

        private static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>เปิดซีนแบบ additive เพื่อเช็คว่ามี panel ชื่อนี้แล้วหรือยัง แล้วปิดทิ้ง</summary>
        private static bool PanelExistsInScene(string scenePath, string panelName)
        {
            var open = EditorSceneManager.GetSceneByPath(scenePath);
            bool wasOpen = open.IsValid() && open.isLoaded;

            var scene = wasOpen ? open : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            bool found = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == panelName) { found = true; break; }
                foreach (Transform child in root.transform)
                    if (child.name == panelName) { found = true; break; }
                if (found) break;
            }

            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            return found;
        }
    }
}
