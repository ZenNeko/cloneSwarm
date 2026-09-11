using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ต่อสายจอ P3R ที่ย้ายเข้ามาแล้วให้เกมใช้งานจริง
    /// เมนู: Tools > Clone Swarm > Wire P3R Screens > …
    ///
    /// <see cref="P3RScreenMigrator"/> แค่วาง panel เข้าไปแบบปิดไว้ ไม่แตะสายของเดิมเลย
    /// ไฟล์นี้คือขั้นที่สอง — สลับสายให้ชี้ panel ใหม่ แล้วปลดของเดิมออกจากทาง
    ///
    /// ═══ ทำไมต้องปลด component เดิมออก ไม่ใช่แค่ปิด panel เดิม ═══
    ///
    /// ตัวคุมจอเดิมแปดตัวอยู่บน **Canvas root** ไม่ใช่บน panel ของตัวเอง
    /// (`LobbyUI` · `TalentShopUI` · `CharacterSelectUI` · `SettingsMenuUI` · `MapSelectUI`
    ///  บน `MenuScene/Canvas` · `LevelUpUI` · `PauseMenuUI` · `WinLoseUI` บน `SampleScene/HUDCanvas`)
    ///
    /// Canvas root เปิดตลอดเวลา ปิด panel เดิมจึงไม่ได้หยุดตัวคุมเลย มันยัง `OnEnable`
    /// ยัง subscribe static event ยังเขียน static field ทับกัน
    /// `CharacterSelectUI.SelectedCharacter` เป็น static ที่ `PlayerWeaponManager` อ่านข้ามซีน —
    /// มีสองตัวเขียน = ตัวที่เขียนทีหลังชนะ ซึ่งเป็นบั๊กจริงไม่ใช่แค่เปลืองเฟรม
    ///
    /// จึง **ลบ component เดิมออกจาก Canvas root** ไม่ใช่แค่ปิด panel
    /// นี่คือสิ่งที่ ADR-001 F1 อยากได้ตั้งแต่แรก แต่ระบุไว้แค่ `TalentShopUI` ตัวเดียว
    ///
    /// ═══ ความปลอดภัย ═══
    /// รายงานทุกอย่างที่จะเปลี่ยนก่อน แล้วถามหนึ่งครั้ง · ย้อนด้วย `git checkout` ไฟล์ซีน
    /// panel เดิมถูก **ปิด** ไม่ได้ลบ — ยังเปิดดูเทียบได้ และลบเองทีหลังเมื่อมั่นใจ
    /// </summary>
    public static class P3RScreenWirer
    {
        private const string MenuRoot    = "Tools/Clone Swarm/Wire P3R Screens/";
        private const string MenuScene   = "Assets/GameScenes/MenuScene.unity";
        private const string SampleScene = "Assets/GameScenes/SampleScene.unity";

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem(MenuRoot + "0 · รายงานสายปัจจุบัน ไม่แก้อะไร", priority = 0)]
        public static void Report()
        {
            var sb = new StringBuilder("[Wire] สายปัจจุบัน\n\n");
            ReportScene(sb, MenuScene);
            ReportScene(sb, SampleScene);
            Debug.Log(sb.ToString());
        }

        private static void ReportScene(StringBuilder sb, string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            sb.AppendLine($"── {System.IO.Path.GetFileNameWithoutExtension(scenePath)}");

            foreach (var mb in AllBehaviours(scene))
            {
                string type = mb.GetType().Name;
                if (type is not ("MenuManager" or "TabBar")) continue;

                sb.AppendLine($"   {type}  บน  {HierarchyPath(mb.transform)}");
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                        sb.AppendLine($"      {it.name,-22} → {Name(it.objectReferenceValue)}");
                    else if (it.isArray && it.name == "tabs")
                        for (int i = 0; i < it.arraySize; i++)
                        {
                            var e = it.GetArrayElementAtIndex(i);
                            sb.AppendLine($"      tabs[{i}] id={e.FindPropertyRelative("id").stringValue,-10} " +
                                          $"panel → {Name(e.FindPropertyRelative("panel").objectReferenceValue)}");
                        }
                }
            }

            // ตัวคุมจอที่อยู่ผิดที่ — บน Canvas root แทนที่จะอยู่บน panel ของตัวเอง
            var strays = FindStrayControllers(scene);
            if (strays.Count > 0)
            {
                sb.AppendLine("   ตัวคุมจอที่อยู่บน Canvas root (ควรอยู่บน panel ของตัวเอง):");
                foreach (var s in strays) sb.AppendLine($"      {s.GetType().Name}  บน  {HierarchyPath(s.transform)}");
            }
            sb.AppendLine();
        }

        // ═══════════════════════════════════════════════════════════════════
        // WIRE
        // ═══════════════════════════════════════════════════════════════════
        [MenuItem(MenuRoot + "1 · ต่อสาย MenuScene", priority = 20)]
        public static void WireMenuScene() => Wire(MenuScene);

        [MenuItem(MenuRoot + "2 · ต่อสาย SampleScene", priority = 21)]
        public static void WireSampleScene() => Wire(SampleScene);

        /// <summary>ให้ batchmode เรียกได้: -executeMethod … .WireFromCommandLine -scene Menu|Sample</summary>
        public static void WireFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-scene")
                {
                    Wire(args[i + 1] == "Sample" ? SampleScene : MenuScene);
                    return;
                }
            Debug.LogError("[Wire] ต้องส่ง -scene Menu หรือ -scene Sample");
        }

        private static void Wire(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            var plan = new List<string>();
            var apply = new List<System.Action>();

            BuildPlan(scene, plan, apply);

            if (plan.Count == 0)
            {
                Debug.Log($"[Wire] {sceneName}: ไม่มีอะไรต้องเปลี่ยน");
                return;
            }

            string body = string.Join("\n", plan);
            Debug.Log($"[Wire] {sceneName} — จะเปลี่ยน {plan.Count} อย่าง:\n{body}");

            // batchmode ไม่มีใครกดปุ่มได้ — ต้องส่ง -confirm มาด้วยถึงจะลงมือจริง
            // ไม่งั้นรันแล้วมันจะแก้ซีนทันทีโดยไม่มีใครทันเห็นแผน ซึ่งเคยเกิดมาแล้ว
            if (Application.isBatchMode &&
                !System.Environment.GetCommandLineArgs().Contains("-confirm"))
            {
                Debug.Log($"[Wire] {sceneName}: dry-run · ส่ง -confirm มาด้วยถึงจะลงมือจริง");
                return;
            }

            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog($"ต่อสาย {sceneName}",
                    $"จะเปลี่ยน {plan.Count} อย่าง (รายละเอียดเต็มใน Console)\n\n" +
                    "panel เดิมถูก **ปิด** ไม่ได้ลบ\n" +
                    "ตัวคุมจอเดิมบน Canvas root ถูก **ลบ** เพราะปิด panel ไม่ได้หยุดมัน\n\n" +
                    "ย้อนได้ด้วย git checkout ไฟล์ซีน",
                    "ต่อสายเลย", "ยกเลิก"))
                return;

            foreach (var a in apply) a();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Wire] {sceneName}: เสร็จ {plan.Count} อย่าง");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static void BuildPlan(Scene scene, List<string> plan, List<System.Action> apply)
        {
            var panels = FindPanels(scene);
            ReplacedPanels.Clear();

            // ── 1. สลับ object reference ที่ชี้ panel เดิม ให้ชี้ panel ใหม่ ──
            foreach (var mb in AllBehaviours(scene))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                bool dirty = false;

                var it = so.GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;

                    if (it.propertyType == SerializedPropertyType.ObjectReference)
                        dirty |= TryRetarget(mb, it, panels, plan, apply);

                    else if (it.isArray && it.name == "tabs")
                        for (int i = 0; i < it.arraySize; i++)
                        {
                            var panelProp = it.GetArrayElementAtIndex(i).FindPropertyRelative("panel");
                            string id = it.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
                            dirty |= TryRetargetTab(mb, panelProp, id, panels, plan, apply);
                        }
                }

                if (dirty) { var captured = so; apply.Add(() => captured.ApplyModifiedPropertiesWithoutUndo()); }
            }

            // ── 2. ปิด panel เดิมที่ไม่มีใครชี้แล้ว ───────────────────────────
            // ไม่ลบ — ยังเปิดดูเทียบได้ และลบเองทีหลังเมื่อมั่นใจ
            foreach (var old in ReplacedPanels)
            {
                if (old == null || !old.activeSelf) continue;
                plan.Add($"   ปิด panel เดิม  {HierarchyPath(old.transform)}");
                var captured = old;
                apply.Add(() => captured.SetActive(false));
            }

            // ── 3. ลบตัวคุมจอเดิมที่อยู่บน Canvas root ───────────────────────
            // ปิด panel ไม่ได้หยุดมัน เพราะมันไม่ได้อยู่บน panel — อยู่บน Canvas ที่เปิดตลอด
            foreach (var stray in FindStrayControllers(scene))
            {
                if (panels.Values.Any(p => p != null && HasComponentOfType(p, stray.GetType())))
                {
                    plan.Add($"   ลบ {stray.GetType().Name} ออกจาก {HierarchyPath(stray.transform)}  " +
                             "(panel ใหม่มีตัวนี้แล้ว)");
                    var captured = stray;
                    apply.Add(() => Object.DestroyImmediate(captured));
                }
                else
                {
                    plan.Add($"   [ข้าม] {stray.GetType().Name} บน {HierarchyPath(stray.transform)} " +
                             "— panel ใหม่ไม่มีตัวนี้ ปล่อยไว้");
                }
            }

            // ── 4. เปิด panel ที่ต้องเปิดตอนเริ่ม ─────────────────────────────
            //
            // P3R_Main = หน้าแรกของเมนู
            //
            // สามจอในเกมต้องเปิดด้วย **เหตุผลคนละอย่าง** — LevelUpUI · WinLoseUI เป็น
            // singleton ที่ตั้ง Instance ใน Awake() และ Awake ไม่วิ่งบน GameObject ที่ปิดอยู่
            // ปิดไว้ = Instance เป็น null ตลอดรอบ = จอเลเวลอัปกับจอจบเกมไม่ขึ้นเลย
            // ทั้งสามตัวสั่ง panelRoot.SetActive(false) ให้ตัวเองใน Awake/Start อยู่แล้ว
            // เปิด panel นอกจึงไม่ได้แปลว่าผู้เล่นจะเห็นจอค้างอยู่
            foreach (var name in new[] { "P3R_Main", "P3R_LevelUp", "P3R_Pause", "P3R_WinLose" })
            {
                if (!panels.TryGetValue(name, out var go) || go == null || go.activeSelf) continue;
                string why = name == "P3R_Main" ? "หน้าแรกของเมนู"
                                                : "singleton ตั้ง Instance ใน Awake ซึ่งไม่วิ่งตอนปิดอยู่";
                plan.Add($"   เปิด {name} ({why})");
                var captured = go;
                apply.Add(() => captured.SetActive(true));
            }
        }

        /// <summary>
        /// สลับ field ที่ชี้ของเดิม ให้ชี้ panel ใหม่ที่เทียบเท่ากัน
        /// จับคู่ด้วย **ชนิดของ component** ไม่ใช่ชื่อ field — ชื่อ field เปลี่ยนได้ ชนิดไม่เปลี่ยน
        /// </summary>
        private static bool TryRetarget(MonoBehaviour owner, SerializedProperty prop,
                                        Dictionary<string, GameObject> panels,
                                        List<string> plan, List<System.Action> apply)
        {
            var current = prop.objectReferenceValue;
            if (current == null) return false;

            // field ที่ชี้ component (เช่น MenuManager.lobbyUI) → หาตัวเดียวกันบน panel ใหม่
            if (current is Component comp)
            {
                // **เฉพาะตัวคุมจอ** — เคยจับคู่ด้วยชนิดล้วนแล้วมันลาก Button ทุกตัวในซีน
                // ไปชี้ปุ่มแรกที่เจอใน panel ใหม่ · 170 จุดในครั้งเดียว
                // ตัวคุมจอมีตัวเดียวต่อ panel การจับคู่ด้วยชนิดจึงปลอดภัยเฉพาะกับมัน
                if (!IsController(comp.GetType())) return false;

                var replacement = FindInPanels(panels, comp.GetType());
                if (replacement == null || replacement == comp) return false;
                if (IsInsideAnyPanel(comp.transform, panels)) return false;   // ชี้ของใหม่อยู่แล้ว

                plan.Add($"   {owner.GetType().Name}.{prop.name}  {Name(current)} → {Name(replacement)}");
                prop.objectReferenceValue = replacement;
                return true;
            }

            // field ที่ชี้ GameObject → **ตารางที่ระบุชื่อไว้ชัด** ไม่เดาจากชนิด
            if (current is GameObject go)
            {
                if (IsInsideAnyPanel(go.transform, panels)) return false;

                string mapKey = $"{owner.GetType().Name}.{prop.name}";
                if (!PanelFieldMap.TryGetValue(mapKey, out var key)) return false;
                if (!panels.TryGetValue(key, out var target) || target == null) return false;

                plan.Add($"   {mapKey}  {go.name} → {key}");
                ReplacedPanels.Add(go);
                prop.objectReferenceValue = target;
                return true;
            }

            return false;
        }

        private static bool TryRetargetTab(MonoBehaviour owner, SerializedProperty panelProp, string id,
                                           Dictionary<string, GameObject> panels,
                                           List<string> plan, List<System.Action> apply)
        {
            string key = id switch
            {
                "lobby"     => "P3R_Lobby",
                "map"       => "P3R_MapSelect",
                "character" => "P3R_Character",
                "shop"      => "P3R_TalentShop",
                _           => null,
            };
            if (key == null || !panels.TryGetValue(key, out var panel) || panel == null) return false;

            var current = panelProp.objectReferenceValue as GameObject;
            if (current == panel) return false;

            plan.Add($"   TabBar.tabs[{id}].panel  {Name(current)} → {key}");
            panelProp.objectReferenceValue = panel;
            return true;
        }

        /// <summary>
        /// field ไหนควรชี้ panel ไหน — เขียนไว้ตรงๆ ตัวเดียวที่ตัดสินเรื่องนี้
        /// การ "เดา" จากชนิด component พังมาแล้ว ตารางนี้จึงเป็นความตั้งใจ ไม่ใช่ความขี้เกียจ
        /// </summary>
        private static readonly Dictionary<string, string> PanelFieldMap = new()
        {
            ["MenuManager.mainPanel"]     = "P3R_Main",
            ["MenuManager.settingsPanel"] = "P3R_Config",
            ["MenuManager.loadingPanel"]  = "P3R_Loading",
            ["MenuManager.lobbyPanel"]    = "P3R_Lobby",
        };

        /// <summary>panel เดิมที่เพิ่งถูกแทนที่ — เอาไว้ปิดทีหลัง</summary>
        private static readonly HashSet<GameObject> ReplacedPanels = new();

        // ═══════════════════════════════════════════════════════════════════
        // LOOKUP
        // ═══════════════════════════════════════════════════════════════════
        private static Dictionary<string, GameObject> FindPanels(Scene scene)
        {
            var map = new Dictionary<string, GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("P3R_") && !map.ContainsKey(t.name))
                        map[t.name] = t.gameObject;
            return map;
        }

        private static Component FindInPanels(Dictionary<string, GameObject> panels, System.Type type)
        {
            foreach (var p in panels.Values)
            {
                if (p == null) continue;
                var c = p.GetComponentInChildren(type, true);
                if (c != null) return c;
            }
            return null;
        }

        private static bool HasComponentOfType(GameObject panel, System.Type type)
            => panel.GetComponentInChildren(type, true) != null;

        private static bool IsInsideAnyPanel(Transform t, Dictionary<string, GameObject> panels)
            => panels.Values.Any(p => p != null && t.IsChildOf(p.transform));

        /// <summary>
        /// ของเดิมชิ้นนี้ตรงกับ panel ใหม่อันไหน — ดูจากชนิด component ที่มันถือ
        /// เช่น panel เดิมที่มี `LobbyUI` อยู่ข้างใน → คู่กับ `Panel_Lobby`
        /// </summary>
        private static string GuessPanelFor(GameObject old, Dictionary<string, GameObject> panels)
        {
            var oldTypes = old.GetComponentsInChildren<MonoBehaviour>(true)
                              .Where(m => m != null && IsController(m.GetType()))
                              .Select(m => m.GetType())
                              .ToHashSet();
            if (oldTypes.Count == 0) return null;

            foreach (var kv in panels)
            {
                if (kv.Value == null) continue;
                var newTypes = kv.Value.GetComponentsInChildren<MonoBehaviour>(true)
                                 .Where(m => m != null && IsController(m.GetType()))
                                 .Select(m => m.GetType())
                                 .ToHashSet();
                if (oldTypes.Overlaps(newTypes)) return kv.Key;
            }
            return null;
        }

        private static List<GameObject> FindOldPanels(Scene scene, Dictionary<string, GameObject> panels)
        {
            var result = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!go.activeSelf) continue;
                    if (go.name.StartsWith("P3R_")) continue;
                    if (IsInsideAnyPanel(t, panels)) continue;
                    if (GuessPanelFor(go, panels) == null) continue;
                    // เอาเฉพาะชั้นบนสุดที่ match — ไม่ต้องปิดลูกซ้ำ
                    if (t.parent != null && GuessPanelFor(t.parent.gameObject, panels) != null) continue;
                    result.Add(go);
                }
            return result;
        }

        /// <summary>ตัวคุมจอที่อยู่บน GameObject ที่มี Canvas — คือ Canvas root ไม่ใช่ panel</summary>
        private static List<MonoBehaviour> FindStrayControllers(Scene scene)
            => AllBehaviours(scene)
               .Where(m => m != null
                        && IsController(m.GetType())
                        && m.GetComponent<Canvas>() != null)
               .ToList();

        private static readonly HashSet<string> ControllerNames = new()
        {
            "MenuManager", "LobbyUI", "MapSelectUI", "CharacterSelectUI", "TalentShopUI",
            "SettingsMenuUI", "LevelUpUI", "PauseMenuUI", "WinLoseUI",
            "LoadingScreenUI", "TitleScreenUI",
        };

        private static bool IsController(System.Type t) => ControllerNames.Contains(t.Name);

        private static IEnumerable<MonoBehaviour> AllBehaviours(Scene scene)
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(m => m != null);

        private static string Name(Object o) => o == null ? "(ว่าง)" : o.name;

        private static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
