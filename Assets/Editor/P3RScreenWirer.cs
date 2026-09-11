using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.Events;
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

            AuditDeadControls(sb, scene);

            // ตัวคุมจอที่อยู่ผิดที่ — บน Canvas root แทนที่จะอยู่บน panel ของตัวเอง
            var strays = FindStrayControllers(scene);
            if (strays.Count > 0)
            {
                sb.AppendLine("   ตัวคุมจอที่อยู่บน Canvas root (ควรอยู่บน panel ของตัวเอง):");
                foreach (var s in strays) sb.AppendLine($"      {s.GetType().Name}  บน  {HierarchyPath(s.transform)}");
            }
            sb.AppendLine();
        }

        /// <summary>
        /// หา "ปุ่มที่กดแล้วไม่เกิดอะไร" ใน panel P3R ทุกอัน
        ///
        /// ปุ่มจะทำงานได้สามทาง — มี persistent listener ใน onClick, ถูก field ของ
        /// สคริปต์ไหนสักตัวอ้างถึง (แล้วสคริปต์นั้น AddListener เองตอน OnEnable),
        /// หรือมี component บนตัวมันเองที่ต่อให้ (เช่น <see cref="P3RTabJump"/>)
        /// ไม่เข้าสักทาง = ปุ่มตาย · builder สร้างปุ่มสวยๆ ไว้ได้โดยไม่มีใครสังเกตว่ามันไม่ทำงาน
        /// </summary>
        private static void AuditDeadControls(StringBuilder sb, Scene scene)
        {
            var panels = FindPanels(scene);
            if (panels.Count == 0) return;

            // เก็บทุก object ที่ถูก field ไหนสักตัวในซีนอ้างถึง
            var referenced = new HashSet<Object>();
            foreach (var mb in AllBehaviours(scene))
            {
                var it = new SerializedObject(mb).GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.propertyType == SerializedPropertyType.ObjectReference &&
                        it.objectReferenceValue != null)
                        referenced.Add(it.objectReferenceValue);
                }
            }

            var dead = new List<string>();
            foreach (var kv in panels)
            {
                if (kv.Value == null) continue;

                foreach (var btn in kv.Value.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    if (btn.onClick.GetPersistentEventCount() > 0) continue;
                    if (referenced.Contains(btn)) continue;
                    if (btn.GetComponent<P3RTabJump>() != null) continue;
                    dead.Add($"      {HierarchyPath(btn.transform)}");
                }

                foreach (var list in kv.Value.GetComponentsInChildren<P3RMenuList>(true))
                {
                    if (list.GetComponent<P3RMenuBridge>() != null) continue;
                    if (list.onConfirmEvent.GetPersistentEventCount() > 0) continue;
                    // ถูก field ของสคริปต์อื่นถืออยู่ก็ใช้ได้ — PauseMenuUI.menuList เป็นแบบนั้น
                    // มัน subscribe OnConfirm เองใน OnEnable
                    if (referenced.Contains(list)) continue;
                    dead.Add($"      {HierarchyPath(list.transform)} (P3RMenuList ไม่มีตัวรับ)");
                }
            }

            if (dead.Count == 0)
            {
                sb.AppendLine("   ปุ่มใน panel P3R ต่อครบทุกตัว");
                return;
            }

            sb.AppendLine($"   ปุ่มที่กดแล้วไม่เกิดอะไร {dead.Count} ตัว:");
            foreach (var d in dead) sb.AppendLine(d);
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

            // ต้องมี hub ก่อนคำนวณแผนที่เหลือ เพราะ MenuManager.lobbyPanel ต้องชี้มัน
            EnsureHub(scene, panels, plan);

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
                    // **ยกข้อมูลมาก่อนลบ** — ตัวเดิมถือรายการที่คนตั้งไว้ใน Inspector
                    // (CharacterSelectUI.characters · LobbyUI.maps) ตัวใหม่ที่ builder สร้าง
                    // ไม่มีข้อมูลพวกนี้ · ลบทิ้งเฉยๆ = จอเลือกตัวละครว่างเปล่า กดอะไรไม่ได้
                    var replacement = FindInPanels(panels, stray.GetType());
                    if (replacement is MonoBehaviour target)
                        CarryOverAssetData(stray, target, plan);

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

            // ── 3.5 ต่อ "การกด" ไม่ใช่แค่สายอ้างอิง ──────────────────────────
            // สายอ้างอิง panel ทำให้จอ **โผล่** ได้ แต่ไม่ได้ทำให้มัน **กดได้**
            // เมนูหลัก P3R ไม่ได้ใช้ Button เลย MenuManager จึงต่อเข้าไม่ได้ตรงๆ
            // และปุ่ม BACK/CONFIRM ของ MapSelect กับ TalentShop ถูกสร้างไว้เฉยๆ ไม่ได้ต่อกับอะไร
            WireInteractions(scene, panels, plan, apply);

            // ── 3.7 ลบของตัวอย่างที่ builder วางไว้ ───────────────────────────
            // ตัวคุมจอส่วนใหญ่ล้างเฉพาะของที่ **ตัวเองสร้าง** ตอนรัน
            // (TalentShopUI ล้างจาก tiles list · CarouselBase ล้างจาก views ของตัวเอง)
            // ของตัวอย่างจาก builder ไม่ได้อยู่ในลิสต์พวกนั้น เลยค้างอยู่แล้วซ้อนกับของจริง
            // ในซีนต้นแบบมันมีประโยชน์ (เห็นหน้าตาโดยไม่ต้อง Play) ในซีนจริงมันคือขยะ
            foreach (var panel in panels.Values)
            {
                if (panel == null) continue;
                foreach (var t in panel.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Sample_")) continue;
                    plan.Add($"   ลบของตัวอย่าง {HierarchyPath(t)}");
                    var go = t.gameObject;
                    apply.Add(() => { if (go != null) Object.DestroyImmediate(go); });
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
            foreach (var name in new[] { "P3R_Main", "P3R_JoinRoom", "P3R_LevelUp", "P3R_Pause", "P3R_WinLose" })
            {
                if (!panels.TryGetValue(name, out var go) || go == null || go.activeSelf) continue;
                string why = name == "P3R_Main" ? "หน้าแรกของเมนู"
                                              : name == "P3R_JoinRoom" ? "modal ที่ Awake ตั้ง Instance · ตัวที่เปิด/ปิดคือลูกชื่อ PanelRoot"
                                                : "singleton ตั้ง Instance ใน Awake ซึ่งไม่วิ่งตอนปิดอยู่";
                plan.Add($"   เปิด {name} ({why})");
                var captured = go;
                apply.Add(() => captured.SetActive(true));
            }

            // ── 4.1 ปิด panel ที่ต้องปิดตอนเริ่ม ───────────────────────────────
            //
            // สถานะเริ่มต้นของซีนต้อง **บังคับทั้งสองทาง** ไม่ใช่แค่ทางเปิด
            // ที่ผ่านมามีแต่รายชื่อ "ต้องเปิด" panel อื่นจึงเป็นอะไรก็ได้ตามที่บังเอิญค้างไว้
            // และเคยเปลี่ยนเองระหว่างรันไปป์ไลน์โดยไม่มีใครสั่ง — สถานะที่ไม่มีใครยืนยัน
            // คือสถานะที่ไล่ต้นเหตุไม่ได้เวลามันผิด
            //
            // P3R_Title ปิดไว้เพราะ **ยังไม่มีใครปิดมันตอนรัน** — onAdvanceEvent เรียก
            // MenuManager.ShowMain() ซึ่งเปิด P3R_Main แต่ไม่แตะ P3R_Title
            // เปิดค้างไว้ = จอไตเติลคลุมเมนูตลอดไป · เปิดได้เมื่อมีคนสั่งปิดมันแล้วเท่านั้น
            foreach (var name in new[] { "P3R_Title", "P3R_Config", "P3R_Loading", "P3R_Hub" })
            {
                if (!panels.TryGetValue(name, out var go) || go == null || !go.activeSelf) continue;
                string why = name == "P3R_Title"
                    ? "ยังไม่มีใครสั่งปิดมันตอนรัน — เปิดไว้แล้วจะคลุมเมนูถาวร"
                    : "เปิดเมื่อผู้เล่นสั่งเท่านั้น";
                plan.Add($"   ปิด {name} ({why})");
                var captured = go;
                apply.Add(() => captured.SetActive(false));
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

            // ── ช่องที่ **ว่างอยู่** ก็ต้องเติม ไม่ใช่ข้าม ───────────────────
            // component ที่ builder สร้างเริ่มต้นด้วยช่องว่างหลายช่อง (MapSelectUI.lobbyUI)
            // ตัวเดิมที่ถูกลบเคยถือค่าไว้ แต่ค่านั้นชี้ของใน scene จึงไม่ถูกยกมาด้วย
            // CarryOverAssetData (ซึ่งยกเฉพาะ asset) · ผลคือช่องนั้นว่างตลอดไปแบบเงียบๆ
            if (current == null)
            {
                // ช่องที่ชี้ panel แล้ว panel นั้นถูกสร้างใหม่ (ย้ายจอซ้ำ) จะกลายเป็น null
                // ตารางชื่อรู้อยู่แล้วว่าช่องนี้ควรชี้ panel ไหน — เติมกลับให้ตรงนั้นเลย
                string mapKeyEmpty = $"{owner.GetType().Name}.{prop.name}";
                if (PanelFieldMap.TryGetValue(mapKeyEmpty, out var wantPanel) &&
                    panels.TryGetValue(wantPanel, out var panelGo) && panelGo != null)
                {
                    plan.Add($"   เติมช่องว่าง {mapKeyEmpty} → {wantPanel}");
                    prop.objectReferenceValue = panelGo;
                    return true;
                }

                var field = owner.GetType().GetField(prop.name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field == null || !IsController(field.FieldType)) return false;

                var fill = FindInPanels(panels, field.FieldType);
                if (fill == null) return false;

                plan.Add($"   เติมช่องว่าง {owner.GetType().Name}.{prop.name} → {Name(fill)}");
                prop.objectReferenceValue = fill;
                return true;
            }

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
        /// สร้าง <c>P3R_Hub</c> แล้วย้ายสี่จอที่เป็นแท็บเข้าไปข้างใน
        ///
        /// **ทำไมต้องมีชั้นนี้** — `MenuManager.lobbyPanel` กับ `TabBar.tabs[lobby].panel`
        /// เป็นคนละบทบาท แต่ตอนย้ายเข้ามาแบนๆ มันกลายเป็น GameObject เดียวกัน
        /// พอกด TALENT SHOP → `TabBar.Select("shop")` ปิดทุก panel ที่ไม่ใช่แท็บ shop
        /// ซึ่งรวม `P3R_Lobby` ที่เป็น hub ด้วย → ทั้งหน้าหายไปเลย
        /// ซีนเดิมแยกไว้ถูกแล้ว (`LobbyPanel` เป็น hub · `Panel_Lobby` เป็นแท็บข้างใน)
        ///
        /// และ `TabBar` ตัวจริงอยู่ใน `LobbyPanel` เดิมซึ่งถูกปิดไปแล้ว = ไม่ทำงาน
        /// จึงสร้างตัวใหม่บน hub พร้อมลงทะเบียนสี่แท็บให้
        ///
        /// ทำทันทีไม่รอ apply เพราะแผนขั้นต่อไปต้องเห็น hub · ถ้ายกเลิกก็แค่ไม่เซฟ
        /// </summary>
        private static void EnsureHub(Scene scene, Dictionary<string, GameObject> panels,
                                      List<string> plan)
        {
            string[] tabIds   = { "lobby",     "map",            "character",     "shop" };
            string[] tabPanels = { "P3R_Lobby", "P3R_MapSelect", "P3R_Character", "P3R_TalentShop" };

            // ไม่มีจอแท็บสักอัน = ซีนนี้ไม่ใช่เมนู ข้ามไป
            if (!tabPanels.Any(panels.ContainsKey)) return;

            if (!panels.TryGetValue("P3R_Hub", out var hub) || hub == null)
            {
                var anchor = panels[tabPanels.First(panels.ContainsKey)].transform.parent;
                hub = new GameObject("P3R_Hub", typeof(RectTransform));
                hub.transform.SetParent(anchor, false);

                var rt = (RectTransform)hub.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                hub.SetActive(false);
                panels["P3R_Hub"] = hub;
                plan.Add("   สร้าง P3R_Hub (ชั้นที่ถือสี่แท็บ — ไม่งั้นกดแท็บแล้ว hub ปิดตัวเอง)");
            }

            for (int i = 0; i < tabPanels.Length; i++)
            {
                if (!panels.TryGetValue(tabPanels[i], out var panel) || panel == null) continue;
                if (panel.transform.parent == hub.transform) continue;
                plan.Add($"   ย้าย {tabPanels[i]} เข้า P3R_Hub");
                panel.transform.SetParent(hub.transform, false);
                StretchToParent(panel);
            }

            var bar = hub.GetComponent<TabBar>();
            if (bar == null)
            {
                bar = hub.AddComponent<TabBar>();
                plan.Add("   ใส่ TabBar บน P3R_Hub (ตัวเดิมอยู่ใน LobbyPanel ที่ถูกปิดไปแล้ว)");
            }

            bar.tabs.Clear();
            for (int i = 0; i < tabIds.Length; i++)
            {
                panels.TryGetValue(tabPanels[i], out var panel);
                bar.tabs.Add(new TabEntry { id = tabIds[i], panel = panel });
            }
            EditorUtility.SetDirty(bar);

            // LobbyUI ใช้ tabBar สั่งซ่อน/โชว์แท็บตามสิทธิ์ host — ต้องชี้ตัวใหม่
            var lobby = panels.TryGetValue("P3R_Lobby", out var lp) && lp != null
                      ? lp.GetComponentInChildren<LobbyUI>(true) : null;
            if (lobby != null && lobby.tabBar != bar)
            {
                plan.Add("   LobbyUI.tabBar → TabBar บน P3R_Hub");
                lobby.tabBar = bar;
                EditorUtility.SetDirty(lobby);
            }
        }

        /// <summary>
        /// ยกค่าที่ชี้ไป **asset** จากตัวเดิมมาใส่ตัวใหม่ เฉพาะช่องที่ตัวใหม่ยังว่าง
        ///
        /// ทำเฉพาะ asset (ScriptableObject · prefab · sprite) ไม่ยกของที่ชี้ใน scene
        /// เพราะของใน scene ของตัวใหม่คือลูกของ panel ตัวเอง ซึ่งถูกต้องอยู่แล้ว
        /// การยกมาทับจะทำให้ตัวใหม่ไปชี้ของในจอเก่าที่กำลังจะถูกปิด
        ///
        /// ทำทันทีไม่รอ apply เพราะเป็นการเติมค่า ไม่ใช่การทำลาย · ยกเลิกก็แค่ไม่เซฟ
        /// </summary>
        private static void CarryOverAssetData(MonoBehaviour from, MonoBehaviour to, List<string> plan)
        {
            var src = new SerializedObject(from);
            var dst = new SerializedObject(to);
            bool changed = false;

            var it = src.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;

                var d = dst.FindProperty(it.propertyPath);
                if (d == null) continue;

                // ลิสต์ของ asset — ยกทั้งลิสต์เมื่อตัวใหม่ว่าง
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
                {
                    if (it.arraySize == 0 || d.arraySize > 0) continue;
                    if (!AllPersistentRefs(it)) continue;

                    d.arraySize = it.arraySize;
                    for (int i = 0; i < it.arraySize; i++)
                        d.GetArrayElementAtIndex(i).objectReferenceValue =
                            it.GetArrayElementAtIndex(i).objectReferenceValue;

                    plan.Add($"   ยก {from.GetType().Name}.{it.name} ({it.arraySize} รายการ) มาใส่ตัวใหม่");
                    changed = true;
                }
                else if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (it.objectReferenceValue == null || d.objectReferenceValue != null) continue;
                    if (!EditorUtility.IsPersistent(it.objectReferenceValue)) continue;

                    d.objectReferenceValue = it.objectReferenceValue;
                    plan.Add($"   ยก {from.GetType().Name}.{it.name} → {it.objectReferenceValue.name}");
                    changed = true;
                }
            }

            if (changed) dst.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AllPersistentRefs(SerializedProperty arrayProp)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var e = arrayProp.GetArrayElementAtIndex(i);
                if (e.propertyType != SerializedPropertyType.ObjectReference) return false;
                if (e.objectReferenceValue != null && !EditorUtility.IsPersistent(e.objectReferenceValue))
                    return false;
            }
            return true;
        }

        private static void StretchToParent(GameObject go)
        {
            if (go.transform is not RectTransform rt) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// ต่อสิ่งที่ทำให้ "กดแล้วเกิดอะไรขึ้น" — คนละเรื่องกับสายที่ทำให้จอโผล่
        /// </summary>
        private static void WireInteractions(Scene scene, Dictionary<string, GameObject> panels,
                                             List<string> plan, List<System.Action> apply)
        {
            var menuManager = Object.FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);

            // ── เมนูหลัก: P3RMenuList → MenuManager ─────────────────────────
            if (panels.TryGetValue("P3R_Main", out var main) && main != null)
            {
                var list = main.GetComponentInChildren<P3RMenuList>(true);
                if (list != null && list.GetComponent<P3RMenuBridge>() == null)
                {
                    plan.Add("   ใส่ P3RMenuBridge บน MenuList (เมนูหลักจะกดติด)");
                    var go = list.gameObject;
                    apply.Add(() =>
                    {
                        var bridge = go.AddComponent<P3RMenuBridge>();
                        bridge.menuManager = menuManager;
                    });
                }

                // ตัวต้นแบบที่แค่ Debug.Log — ทิ้งไว้จะยิงซ้ำกับ bridge ตัวจริง
                var proto = main.GetComponentInChildren<P3RMainMenuProto>(true);
                if (proto != null)
                {
                    plan.Add("   ลบ P3RMainMenuProto (ตัวต้นแบบที่แค่ Debug.Log)");
                    var captured = proto;
                    apply.Add(() => Object.DestroyImmediate(captured));
                }
            }

            // ── ปุ่มท้ายจอที่ยังไม่ได้ต่อ → สลับแท็บ ──────────────────────────
            // ปุ่มแท็บในทุกจอ — แต่ละจอมีแถบแท็บของตัวเอง กดข้ามไปจอไหนก็ได้
            string[] tabPanels = { "P3R_Lobby", "P3R_MapSelect", "P3R_Character", "P3R_TalentShop" };
            (string obj, string id)[] tabButtons =
            {
                ("Tab_LOBBY", "lobby"), ("Tab_MAP", "map"),
                ("Tab_CHARACTER", "character"), ("Tab_SHOP", "shop"),
            };
            foreach (var panelName in tabPanels)
                foreach (var (obj, id) in tabButtons)
                    AddTabJump(panels, panelName, obj, id, plan, apply);

            foreach (var panelName in tabPanels)
                AddTabStrip(panels, panelName, tabButtons, plan, apply);

            AddTabJump(panels, "P3R_MapSelect",  "Btn_Back",    "back",  plan, apply);
            AddTabJump(panels, "P3R_MapSelect",  "Btn_Confirm", "lobby", plan, apply);
            // ทั้งสามจอเข้าได้ทั้งจากเมนูหลักและจากล็อบบี้ — ร้านเข้าตรงจากเมนูหลักได้
            // ส่วน CHARACTER กดจากแถบแท็บในร้านได้ (Tab_CHARACTER ไม่ถูกซ่อนตอนโหมด Shop)
            // "back" ให้ P3RTabJump ตัดสินปลายทาง **และป้ายบนปุ่ม** ตามโหมดของ hub
            AddTabJump(panels, "P3R_TalentShop", "Btn_Back",    "back",  plan, apply);
            AddTabJump(panels, "P3R_Character",  "Btn_Back",    "back",  plan, apply);

            // ── Title: กดอะไรก็ได้ → หน้าแรก ────────────────────────────────
            if (panels.TryGetValue("P3R_Title", out var title) && title != null && menuManager != null)
            {
                var t = title.GetComponentInChildren<TitleScreenUI>(true);
                if (t != null && t.onAdvanceEvent.GetPersistentEventCount() == 0)
                {
                    plan.Add("   ต่อ TitleScreenUI.onAdvanceEvent → MenuManager.ShowMain()");
                    var captured = t;
                    var mm = menuManager;
                    apply.Add(() => UnityEventTools.AddVoidPersistentListener(
                                        captured.onAdvanceEvent, mm.ShowMain));
                }
            }
        }

        /// <summary>
        /// ให้แถบแท็บที่จอนี้วาดไว้ ฟังคำสั่งซ่อน/โชว์จาก <see cref="TabBar"/> ตัวจริง
        ///
        /// `LobbyUI.Refresh()` สั่ง `SetTabVisible("lobby", false)` ตอนเข้าร้านจากเมนูหลัก
        /// มาตลอด แต่ `TabBar.tabs[].button` ว่างทั้งสี่ช่อง คำสั่งจึงไปไม่ถึงปุ่มที่เห็นบนจอ
        /// (ปุ่มจริงเป็นสำเนาที่แต่ละ panel วาดเอง) · P3RTabStrip เป็นสายที่ขาดอยู่
        /// </summary>
        private static void AddTabStrip(Dictionary<string, GameObject> panels, string panelName,
                                        (string obj, string id)[] tabButtons,
                                        List<string> plan, List<System.Action> apply)
        {
            if (!panels.TryGetValue(panelName, out var panel) || panel == null) return;

            var strip = panel.GetComponentsInChildren<Transform>(true)
                             .FirstOrDefault(t => t.name == "TabBar");
            if (strip == null) return;
            if (strip.GetComponent<P3RTabStrip>() != null) return;

            var found = new List<(string id, RectTransform rt)>();
            foreach (var (obj, id) in tabButtons)
            {
                var b = strip.Find(obj) as RectTransform;
                if (b != null) found.Add((id, b));
            }
            if (found.Count == 0) return;

            plan.Add($"   ใส่ P3RTabStrip บน {panelName}/TabBar ({found.Count} แท็บ) — " +
                     "แท็บที่ถูกซ่อนจะหายจริง ไม่ใช่ซ่อนแค่ใน flag");

            var go = strip.gameObject;
            var entries = found;
            apply.Add(() =>
            {
                var comp = go.AddComponent<P3RTabStrip>();
                foreach (var (id, rt) in entries)
                    comp.entries.Add(new P3RTabStrip.Entry { id = id, button = rt });
            });
        }

        private static void AddTabJump(Dictionary<string, GameObject> panels, string panelName,
                                       string buttonName, string tabId,
                                       List<string> plan, List<System.Action> apply)
        {
            if (!panels.TryGetValue(panelName, out var panel) || panel == null) return;

            foreach (var t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != buttonName) continue;
                if (t.GetComponent<UnityEngine.UI.Button>() == null) continue;

                // มีอยู่แล้วแต่ชี้ผิดที่ก็ต้องแก้ — เคย `return` ทิ้งเฉยๆ แล้วปุ่มที่เคย
                // ต่อไว้ผิดจะค้างผิดตลอดไป เพราะการต่อสายรอบถัดไปมองว่า "ต่อแล้ว"
                var existing = t.GetComponent<P3RTabJump>();
                if (existing != null)
                {
                    if (existing.tabId == tabId) return;
                    plan.Add($"   แก้ P3RTabJump บน {panelName}/{buttonName}  " +
                             $"{existing.tabId} → {tabId}");
                    var captured = existing;
                    apply.Add(() => captured.tabId = tabId);
                    return;
                }

                plan.Add($"   ใส่ P3RTabJump บน {panelName}/{buttonName} → แท็บ {tabId}");
                var go = t.gameObject;
                apply.Add(() => go.AddComponent<P3RTabJump>().tabId = tabId);
                return;
            }
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
            ["MenuManager.lobbyPanel"]    = "P3R_Hub",
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

            // JoinRoomPanel เป็น **singleton ที่ตั้ง Instance ใน Awake** และตัวเก่าอยู่บน Canvas
            // ซึ่ง active ตลอด · ปล่อยไว้คู่กับตัวบน P3R_JoinRoom แล้วสองตัวจะแย่งกันเป็น Instance
            // ตัวที่ Awake ก่อนชนะ ซึ่งไม่มีใครคุมลำดับได้ · อาการคือปุ่ม JOIN บางรอบเปิดจอเก่า
            // บางรอบเปิดจอใหม่ บางรอบไม่เปิดเลย แล้วแต่ลำดับที่ Unity เรียก Awake
            "JoinRoomPanel",
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
