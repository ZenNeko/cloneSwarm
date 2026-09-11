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

                    case 9: Report(); break;
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
