using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ยุบ TopBar + TabBar ของสี่จอแท็บให้เหลือ **หัวจออันเดียว** บน <c>P3R_Hub</c>
    /// เมนู: Tools > Clone Swarm > Unify Hub Header (หัวจอ hub อันเดียว)
    ///
    /// ═══ ทำไม ═══
    ///
    /// แต่ละจอ (Lobby · MapSelect · Character · TalentShop) วาดหัวของตัวเองไว้ที่ตำแหน่งเดียวกันเป๊ะ
    /// = สี่สำเนาของของชุดเดียว · ไฮไลต์แท็บถูกอบเป็นสีตอน build ทั้งที่ควรตามแท็บที่เลือกจริง
    /// และ `TabBar.SetTabVisible` ต้องมี <see cref="P3RTabStrip"/> สี่ตัวคอยตามซ่อนปุ่มทั้งสี่ชุด
    ///
    /// ═══ ทำในซีนเลย ไม่ build+migrate ใหม่ ═══
    ///
    /// การ migrate คือลบ panel ทั้งก้อนแล้วสร้างใหม่ — งานจัดซีนที่เจ้าของทำเองใน Editor
    /// (เช่น commit 2916c82a ที่ถอด PortraitHint · MapHint ฯลฯ) หายหมด
    /// ตัวนี้ย้ายของที่มีอยู่แล้วในซีนแทน แตะแค่ TopBar กับ TabBar ของสี่จอ:
    ///
    ///   1. หัวของ **Lobby** คือตัวที่อยู่รอด — ยกทั้ง TopBar/TabBar ขึ้นไปเป็น `P3R_Hub/HubHeader`
    ///      (เลือกตัวนี้เพราะมีสายรหัสห้อง/COPY/เชิญเพื่อน ที่ `LobbyUI` ถืออยู่ และงานจัดมือที่ทำไว้ติดไปด้วย)
    ///   2. ของบน TopBar ที่เป็นของจอเดียว (รหัสห้อง · RUNS/WINS/BEST) ย้ายเข้า
    ///      `HubHeader/TopBar/Extras_&lt;id&gt;` · <see cref="P3RHubHeader"/> เปิดเฉพาะกลุ่มของแท็บที่เลือก
    ///   3. ของบนแถวแท็บที่เป็นของจอเดียว (`Btn_Back`) **อยู่กับจอของมัน** — ย้ายลง `&lt;จอ&gt;/TabRow`
    ///      ที่กรอบเท่า TabBar เดิม ตำแหน่งบนจอจึงไม่ขยับ · แถวแท็บไม่มีพื้นทึบ จึงไม่บังกัน
    ///   4. สายที่ชี้เข้าหัวที่กำลังจะลบ (เช่น `CharacterSelectUI.goldText`) ถูกชี้ใหม่ไปตัวเดียวกันใน HubHeader
    ///      ตาม path · ถ้ามีสายไหนหาคู่ไม่เจอ **ไม่ลบ** หัวของจอนั้น แล้วรายงานไว้
    ///   5. `HubHeader` อยู่ลูกคนสุดท้ายของ hub — ทุกจอมี Backdrop เต็มจอ ถ้าหัวอยู่ก่อนจะโดนทับ
    ///
    /// รันซ้ำได้ · <see cref="P3RScreenWirer"/> เรียกตัวนี้ทุกครั้งที่ต่อสาย MenuScene
    /// เพราะ migrate จอแท็บใหม่เมื่อไรก็จะพาหัวของต้นแบบกลับเข้ามาด้วย (ซีนต้นแบบยังมีหัวของตัวเอง
    /// ไว้ให้ภาพ PNG ครบ — เหตุผลเดียวกับ <see cref="P3RBuildStripUnify"/>)
    /// </summary>
    public static class P3RHubHeaderUnify
    {
        private const string ScenePath  = "Assets/GameScenes/MenuScene.unity";
        public  const string HeaderName = "HubHeader";
        private const string TabRowName = "TabRow";

        // Lobby มาก่อน — ตัวแรกที่มีหัวครบคือตัวที่ถูกยกขึ้นไป
        private static readonly (string id, string panel)[] Screens =
        {
            ("lobby", "P3R_Lobby"), ("map", "P3R_MapSelect"),
            ("character", "P3R_Character"), ("shop", "P3R_TalentShop"),
        };

        private static readonly (string obj, string id)[] Tabs =
        {
            ("Tab_LOBBY", "lobby"), ("Tab_MAP", "map"),
            ("Tab_CHARACTER", "character"), ("Tab_SHOP", "shop"),
        };

        // ของกลางที่ทุกจอมีเหมือนกัน — ตัวที่เหลือถือเป็นของเฉพาะจอ
        private static readonly HashSet<string> CommonTop  = new() { "Title", "Gold", "Rule" };
        private static readonly HashSet<string> CommonTabs = new(Tabs.Select(t => t.obj));

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Unify Hub Header (หัวจอ hub อันเดียว)")]
        public static void Unify()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "ยุบหัวจอ hub ให้เหลืออันเดียว",
                    "จะยก TopBar/TabBar ของจอ Lobby ขึ้นไปเป็น P3R_Hub/HubHeader\n" +
                    "แล้วลบหัวของ MapSelect · Character · TalentShop\n" +
                    "(ของเฉพาะจอถูกย้ายออกมาก่อน · สายที่ชี้หัวเดิมถูกชี้ใหม่)\n\n" +
                    "ส่วนอื่นของซีนไม่ถูกแตะ · ควรมี working tree ที่สะอาดก่อนกด",
                    "ยุบ", "ยกเลิก"))
                return;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder("[หัวจอ hub] ยุบให้เหลืออันเดียว\n");
            int n = Apply(scene, log);

            if (n == 0) { Debug.Log(log.AppendLine("  ยุบไว้แล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.AppendLine($"  บันทึก {ScenePath} ({n} รายการ)").ToString());
        }

        /// <summary>batchmode: -executeMethod CloneSwarm.EditorTools.P3RHubHeaderUnify.UnifyFromCommandLine</summary>
        public static void UnifyFromCommandLine() => Unify();

        /// <summary>ซีนนี้ยังมีหัวซ้ำอยู่ไหม — ให้ <see cref="P3RScreenWirer"/> ใส่ในแผนได้</summary>
        public static bool Needed(Scene scene)
        {
            var hub = FindInScene(scene, "P3R_Hub");
            if (hub == null) return false;

            var header = hub.transform.Find(HeaderName);
            if (header == null)
                return Screens.Select(s => FindPanel(hub.transform, null, s.panel))
                              .Any(p => p != null && FindChild(p, "TopBar") != null && FindChild(p, "TabBar") != null);

            return Screens.Select(s => FindPanel(hub.transform, header, s.panel))
                          .Any(p => p != null && (FindChild(p, "TopBar") != null || FindChild(p, "TabBar") != null))
                || header.GetSiblingIndex() != hub.transform.childCount - 1;
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>ทำงานจริง · ไม่เซฟ — ผู้เรียกเซฟเอง · คืนจำนวนสิ่งที่เปลี่ยน</summary>
        public static int Apply(Scene scene, StringBuilder log)
        {
            var hubGo = FindInScene(scene, "P3R_Hub");
            if (hubGo == null) { log.AppendLine("  ไม่มี P3R_Hub — ข้าม (ต่อสาย MenuScene ก่อน)"); return 0; }

            var hub    = hubGo.transform;
            var tabBar = hubGo.GetComponent<TabBar>();
            int changes = 0;

            var header = hub.Find(HeaderName) as RectTransform;

            // ── 1. สร้างหัวจากจอแรกที่มีหัวครบ (Lobby มาก่อน) ────────────────
            if (header == null)
            {
                Transform srcPanel = null, srcTop = null, srcTabs = null;
                string srcId = null;
                foreach (var (id, name) in Screens)
                {
                    var p = FindPanel(hub, null, name);
                    if (p == null) continue;
                    var top = FindChild(p, "TopBar");
                    var tabs = FindChild(p, "TabBar");
                    if (top == null || tabs == null) continue;
                    (srcPanel, srcTop, srcTabs, srcId) = (p, top, tabs, id);
                    break;
                }

                if (srcPanel == null)
                {
                    log.AppendLine("  **ไม่มีจอแท็บไหนมีทั้ง TopBar และ TabBar** — ไม่มีอะไรให้ยกขึ้นเป็นหัว");
                    return 0;
                }

                header = new GameObject(HeaderName, typeof(RectTransform)).GetComponent<RectTransform>();
                header.SetParent(hub, false);
                Stretch(header);

                // worldPositionStays: false — จอแท็บยืดเต็ม hub เหมือน HubHeader
                // anchoredPosition เดิมจึงหมายถึงที่เดิมบนจอพอดี ไม่ขึ้นกับขนาด canvas ตอน batchmode
                srcTop.SetParent(header, false);
                srcTabs.SetParent(header, false);
                srcTop.SetSiblingIndex(0);
                changes++;
                log.AppendLine($"  ยกหัวของ {srcPanel.name} ขึ้นเป็น {Path(header)}");

                // ของเฉพาะจอที่ติดขึ้นมากับหัว — แยกออกเหมือนจออื่น
                var hTop0  = header.Find("TopBar");
                var hTabs0 = header.Find("TabBar");
                foreach (var c in Children(hTop0).Where(c => !CommonTop.Contains(c.name) && !IsExtras(c)))
                {
                    c.SetParent(EnsureExtras(header, hTop0, srcId, tabBar, log), false);
                    changes++;
                    log.AppendLine($"    ย้าย {c.name} → Extras_{srcId} (เห็นเฉพาะแท็บ {srcId})");
                }
                foreach (var c in Children(hTabs0).Where(c => !CommonTabs.Contains(c.name)))
                {
                    c.SetParent(EnsureTabRow(srcPanel, hTabs0 as RectTransform), false);
                    changes++;
                    log.AppendLine($"    ย้าย {c.name} กลับลง {srcPanel.name}/{TabRowName}");
                }
            }

            // ── 2. หัวต้องวาดทับทุกจอ ───────────────────────────────────────
            if (header.GetSiblingIndex() != hub.childCount - 1)
            {
                header.SetAsLastSibling();
                changes++;
                log.AppendLine($"  วาง {HeaderName} เป็นลูกคนสุดท้ายของ hub (Backdrop ของแต่ละจอเต็มจอ)");
            }

            var hTop  = header.Find("TopBar");
            var hTabs = header.Find("TabBar") as RectTransform;
            if (hTop == null || hTabs == null)
            {
                log.AppendLine($"  **{HeaderName} ไม่มี TopBar หรือ TabBar** — หยุด (ลบ {HeaderName} แล้วรันใหม่)");
                return changes;
            }

            changes += EnsureHeaderComponents(header, hTabs, tabBar, log);

            // ── 3. จอที่เหลือ: แยกของเฉพาะจอออก ชี้สายใหม่ แล้วลบหัวซ้ำ ────────
            foreach (var (id, name) in Screens)
            {
                var panel = FindPanel(hub, header, name);
                if (panel == null) continue;

                var top = FindChild(panel, "TopBar");
                if (top != null) changes += Dissolve(scene, top, hTop, panel, id, true, header, tabBar, log);

                var tabs = FindChild(panel, "TabBar");
                if (tabs != null) changes += Dissolve(scene, tabs, hTabs, panel, id, false, header, tabBar, log);
            }

            return changes;
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// ลบหัวซ้ำหนึ่งแถบ · ของกลาง = มีคู่ใน HubHeader อยู่แล้ว · ของเฉพาะจอ = ย้ายออกไปก่อน
        /// ถ้าที่ปลายทางมีชื่อเดียวกันอยู่แล้ว (migrate จอซ้ำ) ถือเป็นคู่ของมัน แล้วชี้สายไปหาแทน
        /// </summary>
        private static int Dissolve(Scene scene, Transform bar, Transform headerBar, Transform panel,
                                    string id, bool isTop, Transform header, TabBar tabBar, StringBuilder log)
        {
            int changes = 0;
            var common = isTop ? CommonTop : CommonTabs;
            var map = new Dictionary<Object, Object>();

            MapNode(bar, headerBar, map);

            foreach (var c in Children(bar))
            {
                if (common.Contains(c.name))
                {
                    MapSubtree(c, headerBar.Find(c.name), map);
                    continue;
                }

                var dest = isTop ? EnsureExtras(header, headerBar, id, tabBar, log)
                                 : EnsureTabRow(panel, (RectTransform)bar);
                var twin = dest.Find(c.name);
                if (twin != null) { MapSubtree(c, twin, map); continue; }

                c.SetParent(dest, false);
                changes++;
                log.AppendLine($"    ย้าย {panel.name}/{bar.name}/{c.name} → {Path(dest)}");
            }

            var doomed = new HashSet<Transform>(bar.GetComponentsInChildren<Transform>(true));
            var unresolved = Retarget(scene, doomed, map, log, out int retargeted);
            changes += retargeted;

            if (unresolved > 0)
            {
                log.AppendLine($"  **ไม่ลบ {Path(bar)}** — มี {unresolved} สายชี้เข้ามาแล้วหาคู่ใน {HeaderName} ไม่เจอ (ดูรายการด้านบน)");
                return changes;
            }

            log.AppendLine($"  ลบหัวซ้ำ {Path(bar)}");
            Object.DestroyImmediate(bar.gameObject);
            return changes + 1;
        }

        /// <summary>
        /// ชี้ทุกสายที่ชี้เข้า <paramref name="doomed"/> ไปหาคู่ใน <paramref name="map"/>
        /// ข้าม Transform ทั้งหมด — `m_Children`/`m_Father` คือโครงลำดับชั้น ไม่ใช่สาย
        /// ไล่แบบ <c>Next(true)</c> เพื่อให้เห็นช่องซ่อนอย่าง onClick ที่ต่อแบบ persistent ด้วย
        /// </summary>
        private static int Retarget(Scene scene, HashSet<Transform> doomed, Dictionary<Object, Object> map,
                                    StringBuilder log, out int retargeted)
        {
            int unresolved = 0;
            retargeted = 0;

            foreach (var root in scene.GetRootGameObjects())
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform) continue;
                if (doomed.Contains(comp.transform)) continue;

                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                bool dirty = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var v = it.objectReferenceValue;
                    if (v == null || !Points(v, doomed)) continue;

                    if (map.TryGetValue(v, out var to) && to != null)
                    {
                        it.objectReferenceValue = to;
                        dirty = true;
                        retargeted++;
                        log.AppendLine($"    ชี้ใหม่ {comp.GetType().Name}.{it.propertyPath} บน {Path(comp.transform)} → {Path(Tf(to))}");
                    }
                    else
                    {
                        unresolved++;
                        log.AppendLine($"    **หาคู่ไม่เจอ** {comp.GetType().Name}.{it.propertyPath} บน {Path(comp.transform)} → {Path(Tf(v))}");
                    }
                }
                if (dirty) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return unresolved;
        }

        /// <summary>P3RTabStrip (ไฮไลต์ + ซ่อนแท็บ) · P3RTabJump บนปุ่มแท็บ · P3RHubHeader</summary>
        private static int EnsureHeaderComponents(Transform header, RectTransform hTabs, TabBar tabBar,
                                                  StringBuilder log)
        {
            int changes = 0;

            var hh = header.GetComponent<P3RHubHeader>();
            if (hh == null) { hh = header.gameObject.AddComponent<P3RHubHeader>(); changes++; log.AppendLine($"  ใส่ P3RHubHeader บน {HeaderName}"); }
            if (hh.tabBar != tabBar) { hh.tabBar = tabBar; EditorUtility.SetDirty(hh); }

            var strip = hTabs.GetComponent<P3RTabStrip>();
            if (strip == null) { strip = hTabs.gameObject.AddComponent<P3RTabStrip>(); changes++; log.AppendLine($"  ใส่ P3RTabStrip บน {HeaderName}/TabBar"); }
            strip.tabBar = tabBar;

            var entries = new List<P3RTabStrip.Entry>();
            foreach (var (obj, id) in Tabs)
            {
                var b = hTabs.Find(obj) as RectTransform;
                if (b == null) continue;
                entries.Add(new P3RTabStrip.Entry
                {
                    id     = id,
                    button = b,
                    bg     = b.Find("Bg")?.GetComponent<Image>(),
                    label  = b.Find("Label")?.GetComponent<TMP_Text>(),
                });

                var jump = b.GetComponent<P3RTabJump>();
                if (jump == null && b.GetComponent<Button>() != null)
                {
                    b.gameObject.AddComponent<P3RTabJump>().tabId = id;
                    changes++;
                    log.AppendLine($"  ใส่ P3RTabJump บน {HeaderName}/TabBar/{obj} → แท็บ {id}");
                }
                else if (jump != null && jump.tabId != id)
                {
                    jump.tabId = id; EditorUtility.SetDirty(jump); changes++;
                }
            }
            strip.entries = entries;

            // ── สีไฮไลต์อ่านจากที่ builder อบไว้ ─────────────────────────────
            // แถบที่ยกมามีแท็บเดียวที่อบเป็นสี active — ตัวที่สีไม่เหมือนเพื่อนคือ active
            // อ่านครั้งเดียวตอนเปิดไฮไลต์ ไม่งั้นรอบถัดไปจะอ่านสีที่ runtime เขียนทับ (ซึ่งในซีนไม่มี แต่กันไว้)
            if (!strip.highlight)
            {
                var bgs = entries.Where(e => e.bg != null).Select(e => e.bg.color).ToList();
                var lbs = entries.Where(e => e.label != null).Select(e => e.label.color).ToList();
                if (bgs.Count >= 2 && TryOddOne(bgs, out var aBg, out var iBg))
                {
                    strip.activeBg = aBg; strip.inactiveBg = iBg;
                    if (lbs.Count >= 2 && TryOddOne(lbs, out var aL, out var iL))
                    { strip.activeLabel = aL; strip.inactiveLabel = iL; }
                    strip.highlight = true;
                    changes++;
                    log.AppendLine("  เปิดไฮไลต์แท็บตามแท็บที่เลือก (สีอ่านจากแท็บที่อบไว้)");
                }
                else log.AppendLine("  **อ่านสีไฮไลต์ไม่ได้** — ตั้ง P3RTabStrip.activeBg/inactiveBg เองแล้วติ๊ก highlight");
            }
            EditorUtility.SetDirty(strip);
            return changes;
        }

        /// <summary>หัวมีกลุ่ม Extras_&lt;id&gt; ใต้แถบนั้น และ P3RHubHeader รู้จักกลุ่มนี้</summary>
        private static Transform EnsureExtras(Transform header, Transform headerBar, string id,
                                              TabBar tabBar, StringBuilder log)
        {
            string name = "Extras_" + id;
            var g = headerBar.Find(name);
            if (g == null)
            {
                var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(headerBar, false);
                Stretch(rt);
                g = rt;
                log.AppendLine($"  สร้าง {Path(g)}");
            }

            // ห้ามใช้ ?? กับ GetComponent — ใน Editor ตัวที่ไม่มีคืน fake-null ซึ่ง ?? มองว่าไม่ null
            var hh = header.GetComponent<P3RHubHeader>();
            if (hh == null) hh = header.gameObject.AddComponent<P3RHubHeader>();
            if (hh.tabBar == null) hh.tabBar = tabBar;
            if (!hh.groups.Any(x => x != null && x.root == g.gameObject))
            {
                hh.groups.Add(new P3RHubHeader.Group { tabId = id, root = g.gameObject });
                EditorUtility.SetDirty(hh);
            }
            return g;
        }

        /// <summary>
        /// แถวของจอเองที่กรอบเท่า TabBar เดิมเป๊ะ — ของที่เคยอยู่บนแถวแท็บย้ายลงมาแล้ว
        /// ตำแหน่งบนจอไม่ขยับ โดยไม่ต้องพึ่ง worldPositionStays (ขนาด canvas ใน batchmode เชื่อไม่ได้)
        /// </summary>
        private static Transform EnsureTabRow(Transform panel, RectTransform like)
        {
            var row = panel.Find(TabRowName);
            if (row != null) return row;

            var rt = new GameObject(TabRowName, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = like.anchorMin; rt.anchorMax = like.anchorMax;
            rt.pivot     = like.pivot;     rt.sizeDelta = like.sizeDelta;
            rt.anchoredPosition = like.anchoredPosition;
            rt.localScale = Vector3.one;
            return rt;
        }

        // ── map: ของในหัวที่จะลบ → คู่ของมันใน HubHeader (GameObject และทุก component) ──
        private static void MapSubtree(Transform src, Transform dst, Dictionary<Object, Object> map)
        {
            foreach (var t in src.GetComponentsInChildren<Transform>(true))
            {
                string rel = RelPath(src, t);
                MapNode(t, dst == null ? null : rel.Length == 0 ? dst : dst.Find(rel), map);
            }
        }

        private static void MapNode(Transform src, Transform dst, Dictionary<Object, Object> map)
        {
            map[src.gameObject] = dst != null ? dst.gameObject : null;
            foreach (var c in src.GetComponents<Component>())
                if (c != null) map[c] = dst != null ? dst.GetComponent(c.GetType()) : null;
        }

        // ═══════════════════════════════════════════════════════════════════
        private static bool TryOddOne(List<Color> cs, out Color odd, out Color common)
        {
            var groups = cs.GroupBy(c => (Color32)c).OrderByDescending(g => g.Count()).ToList();
            common = groups[0].Key;
            odd    = groups.Count > 1 ? (Color)groups[^1].Key : common;
            return groups.Count == 2 && groups[^1].Count() == 1;
        }

        private static bool Points(Object v, HashSet<Transform> doomed)
            => Tf(v) is { } t && doomed.Contains(t);

        private static Transform Tf(Object o) => o switch
        {
            GameObject g => g.transform,
            Component c  => c.transform,
            _            => null,
        };

        private static bool IsExtras(Transform t) => t.name.StartsWith("Extras_");

        private static List<Transform> Children(Transform t)
            => t == null ? new List<Transform>() : t.Cast<Transform>().ToList();

        /// <summary>หาจอใต้ hub ทั้ง subtree แต่ไม่ลงไปใน HubHeader</summary>
        private static Transform FindPanel(Transform hub, Transform header, string name)
            => hub.GetComponentsInChildren<Transform>(true)
                  .FirstOrDefault(t => t.name == name && (header == null || !t.IsChildOf(header)));

        /// <summary>ลูกชื่อนี้ที่ตื้นที่สุด — หัวของจออยู่ชั้นแรกเสมอ แต่อย่าเชื่อ (ดู game-ui skill)</summary>
        private static Transform FindChild(Transform panel, string name)
            => panel.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != panel && t.name == name)
                    .OrderBy(Depth).FirstOrDefault();

        private static int Depth(Transform t) { int d = 0; for (; t.parent != null; t = t.parent) d++; return d; }

        private static string RelPath(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        private static string Path(Transform t)
        {
            if (t == null) return "(null)";
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
