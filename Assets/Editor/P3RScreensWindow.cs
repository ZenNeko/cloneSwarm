using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// แผงควบคุมจอ P3R ทั้งหมดในที่เดียว — Window > Clone Swarm > P3R Screens
    ///
    /// วงจรการทำงานคือ **แก้โค้ด builder → สร้างใหม่ → ดู** ซึ่งเดิมต้องไล่หาเมนู
    /// สามที่คนละกลุ่ม (Build · Capture · Migrate) แล้วจำเองว่าจอไหนทำถึงไหน
    /// หน้าต่างนี้รวมให้อยู่แถวเดียวต่อจอ พร้อมบอกสถานะว่าอยู่ขั้นไหนแล้ว
    ///
    /// **Solo** คือปุ่มที่ได้ใช้บ่อยสุด — เปิด panel ที่เลือก ปิดที่เหลือ ในซีนที่เปิดอยู่
    /// ใช้ตอนจัดหน้าใน Scene view · ทำงานเฉพาะ edit mode และไม่เซฟให้
    /// </summary>
    public class P3RScreensWindow : EditorWindow
    {
        [MenuItem("Window/Clone Swarm/P3R Screens")]
        public static void Open() => GetWindow<P3RScreensWindow>("P3R Screens").minSize = new Vector2(560f, 420f);

        // ═══════════════════════════════════════════════════════════════════
        private readonly struct Row
        {
            public readonly string Label;     // ชื่อที่โชว์ + คีย์ของ migrator
            public readonly string Builder;   // ชื่อคลาส builder · null = ไม่มี
            public readonly string Proto;     // ชื่อซีนต้นแบบ
            public readonly string Panel;     // ชื่อ panel ในซีนจริง · null = ไม่ย้าย
            public readonly string Target;    // ซีนปลายทาง

            public Row(string label, string builder, string proto, string panel, string target)
            { Label = label; Builder = builder; Proto = proto; Panel = panel; Target = target; }

            public string ProtoPath => $"Assets/GameScenes/{Proto}.unity";
        }

        private const string MenuScene   = "Assets/GameScenes/MenuScene.unity";
        private const string SampleScene = "Assets/GameScenes/SampleScene.unity";

        private static readonly Row[] Rows =
        {
            new("TITLE",        "P3RTitleSceneBuilder",      "Proto_Title",       "P3R_Title",       MenuScene),
            new("MAIN MENU",    "P3RMenuSceneBuilder",       "Proto_P3RMenu",     "P3R_Main",        MenuScene),
            new("CHARACTER",    "P3RCharacterSceneBuilder",  "Proto_Character",   "P3R_Character",   MenuScene),
            new("LOBBY",        "P3RLobbySceneBuilder",      "Proto_Lobby",       "P3R_Lobby",       MenuScene),
            new("MAP SELECT",   "P3RMapSelectSceneBuilder",  "Proto_MapSelect",   "P3R_MapSelect",   MenuScene),
            new("TALENT SHOP",  "P3RTalentShopSceneBuilder", "Proto_TalentShop",  "P3R_TalentShop",  MenuScene),
            new("CONFIG",       "P3RConfigSceneBuilder",     "Proto_Config",      "P3R_Config",      MenuScene),
            new("LOADING",      "P3RLoadingSceneBuilder",    "Proto_Loading",     "P3R_Loading",     MenuScene),
            new("LEVEL UP",     "P3RLevelUpSceneBuilder",    "Proto_LevelUp",     "P3R_LevelUp",     SampleScene),
            new("WIN / LOSE",   "P3RWinLoseSceneBuilder",    "Proto_WinLose",     "P3R_WinLose",     SampleScene),
            new("PAUSED",       "P3RPauseSceneBuilder",      "Proto_Pause",       "P3R_Pause",       SampleScene),
            // HUD เป็นภาพอ้างอิง ไม่มี panel ให้ย้าย — ดูเหตุผลใน P3RGameplayHudSceneBuilder
            new("GAMEPLAY HUD", "P3RGameplayHudSceneBuilder","Proto_GameplayHUD", null,              null),
        };

        private Vector2 scroll;
        private P3RTheme theme;

        // ═══════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(6f);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var row in Rows) DrawRow(row);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("จอ P3R ทั้งหมด", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("เรนเดอร์ PNG ทุกจอ", GUILayout.Height(24f)))
                    P3RScreenshotTool.CaptureAll();

                if (GUILayout.Button("เปิดโฟลเดอร์ภาพ", GUILayout.Height(24f)))
                {
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
                    Directory.CreateDirectory(dir);
                    EditorUtility.RevealInFinder(dir);
                }

                if (GUILayout.Button("ซ่อมฟอนต์ไทย", GUILayout.Height(24f)))
                    P3RThaiFontFix.FixAll();
            }

            // theme คือที่เดียวที่คุมสี/ฟอนต์ของทุกจอ — วางไว้ตรงนี้จะได้ไม่ต้องไปหาใน Project
            if (theme == null)
                theme = AssetDatabase.LoadAssetAtPath<P3RTheme>(P3RBuilderKit.ThemePath);

            theme = (P3RTheme)EditorGUILayout.ObjectField("Theme", theme, typeof(P3RTheme), false);
            EditorGUILayout.HelpBox(
                "แก้ค่าใน Theme แล้วต้อง **สร้างซีนต้นแบบใหม่** ถึงจะเห็นผล — " +
                "ค่าถูกอบลงซีนตอนสร้าง ไม่ได้อ่านตอนรัน",
                MessageType.None);
        }

        private void DrawRow(Row row)
        {
            bool hasProto = File.Exists(row.ProtoPath);
            bool migrated = row.Panel != null && HasPanel(row.Target, row.Panel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                // ── สถานะ ────────────────────────────────────────────────────
                string dot = !hasProto ? "·" : migrated ? "●" : "○";
                GUILayout.Label(dot, GUILayout.Width(14f));
                GUILayout.Label(row.Label, EditorStyles.boldLabel, GUILayout.Width(108f));

                using (new EditorGUI.DisabledScope(row.Builder == null))
                    if (GUILayout.Button("สร้าง", GUILayout.Width(52f)))
                        InvokeBuilder(row.Builder);

                using (new EditorGUI.DisabledScope(!hasProto))
                    if (GUILayout.Button("เปิดต้นแบบ", GUILayout.Width(80f)))
                        OpenSceneAsking(row.ProtoPath);

                using (new EditorGUI.DisabledScope(!hasProto || row.Panel == null))
                    if (GUILayout.Button("ย้ายลงซีน", GUILayout.Width(76f)))
                        P3RScreenMigrator.Migrate(row.Label);

                using (new EditorGUI.DisabledScope(!migrated))
                    if (GUILayout.Button("Solo", GUILayout.Width(52f)))
                        Solo(row);

                GUILayout.FlexibleSpace();
                GUILayout.Label(row.Panel == null ? "อ้างอิงเท่านั้น"
                                : migrated        ? "ในซีนแล้ว"
                                                  : "ยังไม่ย้าย",
                                EditorStyles.miniLabel, GUILayout.Width(88f));
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("รายงานสถานะการย้าย", GUILayout.Height(22f)))
                    P3RScreenMigrator.Report();
                if (GUILayout.Button("รายงานสาย", GUILayout.Height(22f)))
                    P3RScreenWirer.Report();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("ต่อสาย MenuScene", GUILayout.Height(22f)))
                    P3RScreenWirer.WireMenuScene();
                if (GUILayout.Button("ต่อสาย SampleScene", GUILayout.Height(22f)))
                    P3RScreenWirer.WireSampleScene();
            }

            EditorGUILayout.LabelField("● ย้ายแล้ว   ○ มีต้นแบบ ยังไม่ย้าย   · ยังไม่มีต้นแบบ",
                                       EditorStyles.miniLabel);
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// เปิดเฉพาะ panel ที่เลือก ปิด P3R_* ที่เหลือ — ใช้ตอนจัดหน้าใน Scene view
        ///
        /// **ไม่เซฟให้** เพราะสถานะเปิด/ปิดของ panel เป็นข้อมูลจริงของซีน
        /// (P3R_Main ต้องเปิด · สามจอในเกมต้องเปิดเพราะ singleton)
        /// เซฟอัตโนมัติหลัง solo = ทำลายสถานะนั้นโดยไม่มีใครตั้งใจ
        /// </summary>
        private void Solo(Row row)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[P3R] Solo ใช้ได้เฉพาะตอนไม่ Play — ตอน Play การเปิดสองจอพร้อมกัน " +
                                 "ทำให้ตัวคุมจอชนกัน");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != row.Target)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(row.Target, OpenSceneMode.Single);
            }

            GameObject picked = null;
            foreach (var go in AllP3RPanels(scene))
            {
                bool on = go.name == row.Panel;
                go.SetActive(on);
                if (on) picked = go;
            }

            if (picked == null) { Debug.LogWarning($"[P3R] ไม่พบ {row.Panel} ใน {scene.name}"); return; }

            Selection.activeGameObject = picked;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"[P3R] Solo {row.Panel} — ปิด P3R_* ที่เหลือแล้ว · **ยังไม่เซฟ** " +
                      "กด Ctrl+Z หรือเปิดซีนใหม่เพื่อคืนสถานะเดิม");
        }

        private static IEnumerable<GameObject> AllP3RPanels(Scene scene)
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name.StartsWith("P3R_"))
                    .Select(t => t.gameObject);

        /// <summary>
        /// ซีนนี้มี panel ชื่อนี้แล้วหรือยัง
        /// ซีนที่เปิดอยู่อ่านจาก hierarchy ตรงๆ · ซีนที่ยังไม่เปิดอ่านจาก **ไฟล์**
        /// การเปิดซีนทุกครั้งที่ OnGUI วาดใหม่จะทำให้เอดิเตอร์ค้างสนิท
        /// </summary>
        private static bool HasPanel(string scenePath, string panelName)
        {
            var open = EditorSceneManager.GetSceneByPath(scenePath);
            if (open.IsValid() && open.isLoaded)
                return AllP3RPanels(open).Any(g => g.name == panelName);

            if (!File.Exists(scenePath)) return false;

            // แคชตามเวลาแก้ไฟล์ — ไฟล์ซีนใหญ่หลายเมกะไบต์ อ่านทุกเฟรมไม่ไหว
            long stamp = File.GetLastWriteTimeUtc(scenePath).Ticks;
            if (!Cache.TryGetValue(scenePath, out var entry) || entry.stamp != stamp)
            {
            if (!File.Exists(scenePath)) return false;

                var names = new HashSet<string>();
                foreach (var line in File.ReadLines(scenePath))
                    if (line.StartsWith("  m_Name: P3R_"))
                        names.Add(line.Substring("  m_Name: ".Length).Trim());

                entry = (stamp, names);
                Cache[scenePath] = entry;
            }
            return entry.names.Contains(panelName);
        }

        private static readonly Dictionary<string, (long stamp, HashSet<string> names)> Cache = new();

        private static void InvokeBuilder(string typeName)
        {
            var type = typeof(P3RScreensWindow).Assembly
                       .GetType($"CloneSwarm.EditorTools.{typeName}");
            var build = type?.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);

            if (build == null) { Debug.LogError($"[P3R] ไม่พบ {typeName}.Build()"); return; }
            build.Invoke(null, null);
        }

        private static void OpenSceneAsking(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
