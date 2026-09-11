using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloneSwarm.Meta;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// กด Play แล้วไล่กดเมนูจริงโดยอัตโนมัติ — Tools > Clone Swarm > Smoke Test (Play)
    ///
    /// ตัวตรวจสายบอกได้แค่ว่า "มีสาย" ไม่ได้บอกว่า "กดแล้วได้ผลถูก"
    /// ตัวนี้เข้า play mode จริง กดรายการเมนูจริง แล้วดูว่า panel สลับถูกไหม
    /// พร้อมดักทุก error / exception ที่เกิดระหว่างนั้น
    ///
    /// **ข้ามการรีโหลด domain ยังไง** — การเข้า play mode ทำให้ static ทั้งหมดหาย
    /// จึงฝากสถานะไว้ที่ <c>SessionState</c> แล้วให้ <c>[InitializeOnLoadMethod]</c>
    /// หยิบกลับมาทำต่อหลังโหลดเสร็จ
    ///
    /// batchmode: -executeMethod CloneSwarm.EditorTools.P3RSmokeTest.Run **ห้ามใส่ -quit**
    /// (ต้องปล่อยให้ Unity อยู่ต่อจนเทสต์จบ แล้วมันเรียก EditorApplication.Exit เอง)
    /// </summary>
    public static class P3RSmokeTest
    {
        private const string StateKey  = "p3r.smoke.state";
        private const string MenuScene = "Assets/GameScenes/MenuScene.unity";

        [MenuItem("Tools/Clone Swarm/Smoke Test (Play)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Smoke] อยู่ใน play mode อยู่แล้ว — ออกก่อนแล้วค่อยรันใหม่");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            SessionState.SetString(StateKey, "running");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>วิ่งทุกครั้งที่ domain โหลดใหม่ — รวมถึงตอนเพิ่งเข้า play mode</summary>
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetString(StateKey, "") != "running") return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            // รอให้ซีนตั้งตัวก่อนหนึ่งจังหวะ แล้วค่อยปล่อยตัวขับเข้าไป
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying) return;
                var go = new GameObject("~P3RSmokeRunner") { hideFlags = HideFlags.HideAndDontSave };
                go.AddComponent<Runner>();
            };
        }

        internal static void Finish(string report, bool ok)
        {
            SessionState.SetString(StateKey, "");
            Debug.Log(report);

            if (Application.isBatchMode)
            {
                // ออกจาก play mode ก่อน ไม่งั้น Unity บ่นตอนปิด
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }
            else
            {
                EditorApplication.isPlaying = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>ตัวขับที่วิ่งอยู่ใน play mode จริง</summary>
        private class Runner : MonoBehaviour
        {
            private static readonly string NL = System.Environment.NewLine;
            private readonly List<string> lines  = new();
            private readonly List<string> errors = new();
            private int    step;
            private float  wait;
            private bool   failed;

            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
                Application.logMessageReceived += OnLog;
            }

            private void OnDestroy() => Application.logMessageReceived -= OnLog;

            private void OnLog(string msg, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                    errors.Add($"      [{type}] {msg.Split('\n')[0]}");
            }

            private void Update()
            {
                if (wait > 0f) { wait -= Time.unscaledDeltaTime; return; }

                switch (step++)
                {
                    case 0:
                        lines.Add("── เปิดซีน MenuScene");
                        wait = 1.2f;                       // ปล่อยให้อนิเมชันเข้าเมนูวิ่งจบก่อน
                        break;

                    case 1: CheckStartState(); break;

                    case 2: Press("settings", "P3R_Config"); break;
                    case 3: Verify(); ShowMain(); break;

                    // TALENT SHOP อยู่ใต้ hub — กดแล้ว hub เปิด แล้ว TabBar เลือกแท็บ shop
                    case 4: Press("shop", "P3R_Hub", "P3R_TalentShop"); break;
                    case 5: Verify(); CheckShop(); JumpTab("lobby", "P3R_Lobby"); break;
                    case 6: Verify(); JumpTab("character", "P3R_Character"); break;
                    case 7: Verify(); CheckCharacter(); break;
                    case 8: ShowMain(); break;

                    // 'play' เรียก StartHost เข้า NGO จริง — เส้นทางหลักของเกม
                    case 9:  Press("play", "P3R_Hub", "P3R_Lobby"); break;
                    case 10: Verify(); CheckHostStarted(); break;

                    // แท็บ MAP ต้องเช็ค **หลัง** host ขึ้น — LobbyUI.Refresh ซ่อนแท็บนี้
                    // ตอนไม่ใช่ host (SetTabVisible("map", lobbyMode && isHost))
                    // เพราะ client เปลี่ยนแมพไม่ได้อยู่แล้ว · ไม่ใช่บั๊ก
                    case 11: JumpTab("map", "P3R_MapSelect"); break;
                    case 12: Verify(); CheckMapSelect(); break;

                    // เปลี่ยนไปซีนเกม เพื่อตรวจสามจอที่เพิ่งแก้บั๊ก script หาย
                    case 13: GoToGameScene(); break;
                    case 14: CheckInGameScreens(); break;

                    case 15: Report(); break;
                }
            }

            // ── ขั้นตอน ─────────────────────────────────────────────────────
            private void CheckStartState()
            {
                Require(Find("P3R_Main")?.activeInHierarchy == true,   "P3R_Main เปิดอยู่ตอนเริ่ม");
                Require(Find("P3R_Config")?.activeInHierarchy != true, "P3R_Config ปิดอยู่ตอนเริ่ม");
                Require(Find("P3R_Hub")?.activeInHierarchy != true,    "P3R_Hub ปิดอยู่ตอนเริ่ม");

                var list = FindList();
                Require(list != null,                                  "หา P3RMenuList ใน P3R_Main เจอ");
                Require(list != null && list.GetComponent<P3RMenuBridge>() != null,
                        "MenuList มี P3RMenuBridge");
                Require(list != null && list.items.Count > 0,          "MenuList มีรายการ");

                var bar = Find("P3R_Hub")?.GetComponent<TabBar>();
                Require(bar != null && bar.tabs.Count == 4,            "P3R_Hub มี TabBar ครบสี่แท็บ");
                Require(bar != null && bar.tabs.All(t => t.panel != null),
                        "ทุกแท็บชี้ panel จริง");
                wait = 0.2f;
            }

            /// <summary>กดรายการเมนูจริงผ่าน API เดียวกับที่เมาส์ใช้</summary>
            private void Press(string id, params string[] expectActive)
            {
                var list = FindList();
                var item = list?.items.FirstOrDefault(i => i != null && i.id == id);

                if (item == null) { Require(false, $"หา รายการ '{id}' เจอ"); wait = 0.2f; return; }

                list.ConfirmItem(item);
                lines.Add($"── กด '{id}'");
                Expect($"กด '{id}'", expectActive);
            }

            /// <summary>กดแท็บผ่าน TabBar ตัวจริง เหมือนที่ P3RTabJump ทำ</summary>
            private void JumpTab(string tabId, params string[] expectActive)
            {
                var bar = Find("P3R_Hub")?.GetComponent<TabBar>();
                if (bar == null) { Require(false, "หา TabBar เจอ"); wait = 0.2f; return; }

                // ทำเหมือน P3RTabJump เป๊ะ — แท็บ lobby/map ถูกซ่อนตอนอยู่โหมดร้าน
                // ถ้าไม่สลับโหมดกลับก่อน Select() จะมองข้ามแท็บที่ซ่อนอยู่แล้วไม่ทำอะไร
                if (tabId is "lobby" or "map")
                {
                    var lobby = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                    if (lobby != null) lobby.SetMode(HubMode.Lobby);
                }

                bar.Select(tabId);
                lines.Add($"── สลับไปแท็บ '{tabId}'");
                Expect($"แท็บ '{tabId}'", expectActive);
            }

            /// <summary>
            /// ร้าน talent — ช่องต้องมาจาก MetaDatabase จริง ไม่ใช่ของตัวอย่างที่ builder วางไว้
            /// เคยเจอทั้งสองชุดซ้อนกัน เพราะ BuildTiles ล้างเฉพาะช่องที่ตัวเองสร้าง
            /// </summary>
            private void CheckShop()
            {
                var panel = Find("P3R_TalentShop");
                var shop  = panel == null ? null : panel.GetComponentInChildren<TalentShopUI>(true);

                Require(shop != null, "หา TalentShopUI เจอ");
                if (shop?.tilesContainer == null) { Require(false, "tilesContainer ต่อไว้"); return; }

                var kids = shop.tilesContainer.Cast<Transform>()
                               .Where(t => t.gameObject.activeSelf).ToList();

                Require(kids.Count > 0,                              "ร้านสร้างช่อง talent ออกมา");
                Require(kids.All(t => !t.name.StartsWith("Sample_")), "ไม่มีช่องตัวอย่างค้างอยู่");
                Require(kids.All(t => t.name.StartsWith("Tile_")),    "ทุกช่องมาจากข้อมูลจริง (Tile_*)");
                Require(shop.tilesContainer.GetComponent<GridLayoutGroup>() != null,
                        "tilesContainer มี GridLayoutGroup (ไม่งั้นช่องกองทับกัน)");
            }

            /// <summary>
            /// จอเลือกตัวละคร — รายการต้องถูกยกมาจากตัวคุมเดิมตอนต่อสาย
            /// ถ้าว่าง carousel จะไม่สร้างการ์ดเลย เหลือแต่ของตัวอย่างที่กดไม่ได้
            /// </summary>
            private void CheckCharacter()
            {
                var panel = Find("P3R_Character");
                var sel   = panel == null ? null : panel.GetComponentInChildren<CharacterSelectUI>(true);

                Require(sel != null,                        "หา CharacterSelectUI เจอ");
                Require(sel != null && sel.characters.Count > 0,
                        $"มีรายการตัวละคร ({sel?.characters.Count ?? 0} ตัว)");

                if (sel?.cardsContainer == null) { Require(false, "cardsContainer ต่อไว้"); return; }

                var kids = sel.cardsContainer.Cast<Transform>()
                              .Where(t => t.gameObject.activeSelf).ToList();

                Require(kids.Count > 0,                              "carousel สร้างการ์ดออกมา");
                Require(kids.All(t => !t.name.StartsWith("Sample_")), "ไม่มีการ์ดตัวอย่างค้างอยู่");
                Require(kids.Any(t => t.GetComponent<CharacterCardUI>() != null),
                        "การ์ดมี CharacterCardUI (กดเลือกได้)");
            }

            /// <summary>จอเลือกแมพ — รายการแมพต้องถูกยกมาจากตัวคุมเดิมเหมือนจอตัวละคร</summary>
            private void CheckMapSelect()
            {
                var panel = Find("P3R_MapSelect");
                var ui    = panel == null ? null : panel.GetComponentInChildren<MapSelectUI>(true);

                Require(ui != null, "หา MapSelectUI เจอ");
                if (ui == null) return;

                Require(ui.lobbyUI != null, "MapSelectUI.lobbyUI ต่อไว้ (ไม่งั้นเลือกแมพแล้วไม่ส่งขึ้นเน็ตเวิร์ก)");

                var lobby = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                Require(lobby != null && lobby.maps.Count > 0,
                        $"LobbyUI มีรายการแมพ ({lobby?.maps.Count ?? 0} แมพ)");

                if (ui.cardsContainer == null) { Require(false, "cardsContainer ต่อไว้"); return; }
                var kids = ui.cardsContainer.Cast<Transform>()
                             .Where(t => t.gameObject.activeSelf).ToList();
                Require(kids.All(t => !t.name.StartsWith("Sample_")), "ไม่มีการ์ดแมพตัวอย่างค้างอยู่");
            }

            /// <summary>'play' ต้องสั่ง NGO ขึ้นจริง ไม่ใช่แค่สลับหน้า</summary>
            private void CheckHostStarted()
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                Require(nm != null,                 "มี NetworkManager ในซีน");
                Require(nm != null && nm.IsListening, "StartHost ขึ้นแล้ว (IsListening)");
                wait = 0.5f;
            }

            /// <summary>
            /// ปิด NGO แล้วเปลี่ยนไปซีนเกม
            /// ต้อง Shutdown ก่อน ไม่งั้น NGO จะพยายามซิงค์ซีนให้ แล้วชนกับการโหลดตรงๆ
            /// </summary>
            private void GoToGameScene()
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm != null && nm.IsListening) nm.Shutdown();

                lines.Add("── เปลี่ยนไปซีน SampleScene");
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
                wait = 2.5f;                       // ให้ Awake/Start ของทั้งซีนวิ่งจบ
            }

            /// <summary>
            /// สามจอในเกม — ตรวจสองบั๊กที่เพิ่งแก้ตรงนี้
            ///   singleton ตั้ง Instance ใน Awake ได้ไหม (panel ต้อง active ตอนโหลดซีน)
            ///   component หายจากการก๊อปข้ามซีนไหม (คลาสรองในไฟล์เดียวกัน)
            /// </summary>
            private void CheckInGameScreens()
            {
                Require(LevelUpUI.Instance  != null, "LevelUpUI.Instance ไม่เป็น null");
                Require(WinLoseUI.Instance  != null, "WinLoseUI.Instance ไม่เป็น null");
                Require(FindAnyObjectByType<PauseMenuUI>(FindObjectsInactive.Include) != null,
                        "หา PauseMenuUI เจอ");

                // แถบ build — ช่องเคยกลายเป็น script หายทั้งแถบ
                var strip = FindAnyObjectByType<BuildStripUI>(FindObjectsInactive.Include);
                Require(strip != null, "หา BuildStripUI เจอ");
                Require(strip != null && strip.slotTemplate != null,
                        "BuildStripUI.slotTemplate ไม่หลุด (คลาสรองข้ามซีนแล้วเคยกลายเป็น null)");

                // component ที่สคริปต์หายจะโผล่เป็น null ใน GetComponents
                int broken = 0;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!go.scene.IsValid()) continue;
                    foreach (var c in go.GetComponents<Component>())
                        if (c == null) broken++;
                }
                Require(broken == 0, $"ไม่มี component ที่สคริปต์หายในซีนเกม (เจอ {broken})");
            }

            private void ShowMain()
            {
                var mm = FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);
                if (mm != null) mm.ShowMain();
                Require(Find("P3R_Main")?.activeInHierarchy == true, "ShowMain() พากลับ P3R_Main ได้");

                // เปิด P3R_Main ใหม่ = P3RMenuList.OnEnable เล่นอนิเมชันเข้าอีกรอบ
                // และระหว่างนั้น ConfirmItem() จะถูกปฏิเสธเงียบๆ (introPlaying)
                // ไม่ใช่บั๊ก — ตั้งใจกันไม่ให้กดทะลุตอนของยังบินเข้า · เทสต์ต้องรอให้จบ
                wait = 1.6f;
            }

            // ── ตรวจผลในรอบถัดไป ────────────────────────────────────────────
            // panel สลับทันทีที่เรียก แต่ปล่อยหนึ่งจังหวะให้ OnEnable ของทุกตัววิ่งจบก่อน
            private string[] pending;
            private string   pendingLabel;

            private void Expect(string label, string[] names)
            {
                pending = names; pendingLabel = label; wait = 0.35f;
            }

            private void Verify()
            {
                if (pending == null) return;
                foreach (var n in pending)
                    Require(Find(n)?.activeInHierarchy == true, $"{pendingLabel} แล้ว {n} เปิด");
                pending = null;
            }

            private void Report()
            {
                Verify();

                var sb = new StringBuilder("[Smoke] ผลทดสอบกด Play" + NL + NL);
                foreach (var l in lines) sb.AppendLine(l);

                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"   error ระหว่างรัน {errors.Count} รายการ:");
                    foreach (var e in errors.Distinct().Take(15)) sb.AppendLine(e);
                    failed = true;
                }
                else sb.AppendLine(NL + "   ไม่มี error ระหว่างรัน");

                sb.AppendLine();
                sb.AppendLine(failed ? "   ผล: ไม่ผ่าน" : "   ผล: ผ่าน");

                Finish(sb.ToString(), !failed);
            }

            // ── ตัวช่วย ──────────────────────────────────────────────────────
            private void Require(bool ok, string what)
            {
                lines.Add($"   {(ok ? "ผ่าน" : "ไม่ผ่าน")}  {what}");
                if (!ok) failed = true;
            }

            private static GameObject Find(string name)
                => Resources.FindObjectsOfTypeAll<GameObject>()
                            .FirstOrDefault(g => g.name == name && g.scene.IsValid());

            private static P3RMenuList FindList()
            {
                var main = Find("P3R_Main");
                return main == null ? null : main.GetComponentInChildren<P3RMenuList>(true);
            }
        }
    }
}
