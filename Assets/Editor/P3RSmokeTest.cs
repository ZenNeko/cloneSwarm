using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloneSwarm.Meta;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
                // ── รอเงื่อนไขจนเป็นจริง ไม่ใช่รอเวลาแล้วเช็คครั้งเดียว ──────────
                // การรอเวลาตายตัวทำให้เทสต์ตกสลับผ่านสลับ เวลาเครื่องสะดุด
                // (โหลด asset · domain reload · NGO เพิ่งขึ้น) — เทสต์ที่ flake
                // แย่กว่าไม่มีเทสต์ เพราะมันสอนให้คนเลิกเชื่อผลของมัน
                if (waitCond != null)
                {
                    if (waitCond())                        { Finish(true);  }
                    else if (Time.unscaledTime < deadline)  { return; }
                    else                                    { Finish(false); }

                    void Finish(bool ok)
                    {
                        onWaitDone?.Invoke(ok);
                        waitCond = null; onWaitDone = null;
                    }
                    return;
                }

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
                    case 8: CheckCharacterClick(); break;
                    case 9: ShowMain(); break;

                    // 'play' เรียก StartHost เข้า NGO จริง — เส้นทางหลักของเกม
                    case 10:  Press("play", "P3R_Hub", "P3R_Lobby"); break;
                    case 11: Verify(); CheckHostStarted(); break;

                    // แท็บ MAP ต้องเช็ค **หลัง** host ขึ้น — LobbyUI.Refresh ซ่อนแท็บนี้
                    // ตอนไม่ใช่ host (SetTabVisible("map", lobbyMode && isHost))
                    // เพราะ client เปลี่ยนแมพไม่ได้อยู่แล้ว · ไม่ใช่บั๊ก
                    case 12: JumpTab("map", "P3R_MapSelect"); break;
                    case 13: Verify(); CheckMapSelect(); break;

                    // เปลี่ยนไปซีนเกม เพื่อตรวจสามจอที่เพิ่งแก้บั๊ก script หาย
                    case 14: GoToGameScene(); break;
                    case 15: CheckInGameScreens(); break;

                    case 16: Report(); break;
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

                CheckCardLayout("ลิสต์ตัวละคร", sel.cardsContainer, sel.characters.Count, vertical: true);
            }

            /// <summary>
            /// **กดการ์ดจริงด้วย raycast** แล้วดูว่าตัวที่เลือกเปลี่ยนไหม
            ///
            /// ตัวตรวจก่อนหน้ายืนยันได้แค่ว่า "การ์ดมี CharacterCardUI" ซึ่งผ่านได้
            /// ทั้งที่กดไม่ติด · อาการ "เลือกตัวละครไม่ได้" เกิดมาสองรอบแล้วโดยผ่านทุกเทสต์
            ///
            /// ยิง raycast ที่พิกัดกลางการ์ดจริงๆ ไม่ใช่เรียก onClick.Invoke() ตรงๆ —
            /// การเรียกตรงข้ามขั้นตอนที่เมาส์จริงต้องผ่าน (มีอะไรบังอยู่ไหม · Graphic
            /// ตัวไหนรับ raycast · event วิ่งขึ้นไปเจอ handler ไหม) ซึ่งเป็นจุดที่พังจริง
            /// </summary>
            private void CheckCharacterClick()
            {
                var panel = Find("P3R_Character");
                var sel   = panel == null ? null : panel.GetComponentInChildren<CharacterSelectUI>(true);
                if (sel?.cardsContainer == null) { Require(false, "กดการ์ด: หา container ไม่เจอ"); return; }

                var cards = sel.cardsContainer.Cast<Transform>()
                               .Where(t => t.gameObject.activeInHierarchy)
                               .OfType<RectTransform>().ToList();
                if (cards.Count < 2) { Require(false, $"กดการ์ด: มีการ์ด {cards.Count} ใบ ทดสอบไม่ได้"); return; }

                var es = EventSystem.current;
                Require(es != null, "กดการ์ด: มี EventSystem ในซีน");
                if (es == null) return;

                var canvas = panel.GetComponentInParent<Canvas>();
                var cam    = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                             ? canvas.worldCamera : null;

                // เลือกใบที่ **ไม่ใช่** ตัวที่เลือกอยู่ ไม่งั้นกดแล้วค่าไม่เปลี่ยนก็แยกไม่ออก
                var before = CharacterSelectUI.SelectedCharacter;
                var target = cards.FirstOrDefault(c =>
                {
                    var name = c.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                    return before == null || name == null || name.text != before.DisplayName;
                }) ?? cards[1];

                var ped = new PointerEventData(es)
                {
                    position = RectTransformUtility.WorldToScreenPoint(cam, target.position),
                    button   = PointerEventData.InputButton.Left,
                };

                var hits = new List<RaycastResult>();
                es.RaycastAll(ped, hits);

                Require(hits.Count > 0, "กดการ์ด: raycast โดนอะไรสักอย่างตรงกลางการ์ด");
                if (hits.Count == 0) return;

                var top = hits[0].gameObject;
                bool ours = top.transform == target || top.transform.IsChildOf(target);
                Require(ours, ours
                    ? "กดการ์ด: ตัวบนสุดที่ raycast โดนอยู่ในการ์ดใบนั้น"
                    : $"กดการ์ด: มี '{HierarchyPath(top.transform)}' บังการ์ดอยู่ — คลิกไปไม่ถึง");

                // handler วิ่งขึ้นจากตัวที่โดน เหมือน EventSystem ทำจริง
                var handled = ExecuteEvents.ExecuteHierarchy(top, ped, ExecuteEvents.pointerClickHandler);
                Require(handled != null,
                        handled != null
                            ? $"กดการ์ด: '{handled.name}' รับคลิกไว้"
                            : "กดการ์ด: ไม่มีใครรับคลิกเลย (Button หายหรือ interactable ปิดอยู่)");

                var after = CharacterSelectUI.SelectedCharacter;
                Require(after != before,
                        after != before
                            ? $"กดการ์ด: ตัวที่เลือกเปลี่ยนเป็น '{after?.characterName}'"
                            : $"กดการ์ด: ตัวที่เลือกยังเป็น '{before?.characterName}' เหมือนเดิม — " +
                              "กดติดแต่ไม่คอมมิต (มักเพราะตัวนั้นยังล็อกอยู่)");
            }

            private static string HierarchyPath(Transform t)
            {
                var parts = new List<string>();
                for (var c = t; c != null; c = c.parent) parts.Add(c.name);
                parts.Reverse();
                return string.Join("/", parts);
            }

            /// <summary>
            /// ตรวจ **เลย์เอาต์** ไม่ใช่โครงสร้าง — สามข้อนี้คือสิ่งที่ตัวตรวจเดิมมองไม่เห็น
            /// จนจอเลือกตัวละครกับจอเลือกแมพวางผิดแบบอยู่หลายรอบโดยผ่านทุกเทสต์
            ///
            ///   จำนวนการ์ดเกินจำนวนข้อมูล = การ์ดซ้ำ (ของ 1 ชิ้นเคยได้การ์ด 4 ใบ)
            ///   การ์ดอยู่นอกกรอบ mask     = ไปทับจออื่น (เคยทับแถบแท็บ)
            ///   ลำดับไม่เรียงตามแกน       = วางผิดแกน (แถบแมพที่ควรนอนเคยวางตั้ง)
            /// </summary>
            private void CheckCardLayout(string label, Transform container, int expected, bool vertical)
            {
                var cards = container.Cast<Transform>()
                                     .Where(t => t.gameObject.activeSelf)
                                     .OfType<RectTransform>().ToList();

                Require(cards.Count == expected,
                        $"{label}: การ์ด {cards.Count} ใบ ตรงกับข้อมูล {expected} ชิ้น" +
                        (cards.Count > expected ? " — เกินแปลว่ามีใบซ้ำ" : ""));

                var mask = container.GetComponentInParent<RectMask2D>();
                Require(mask != null, $"{label}: มี RectMask2D ครอบอยู่ (ไม่งั้นการ์ดล้นไปทับจออื่น)");
                if (mask != null)
                {
                    var clip = WorldRect(mask.rectTransform);
                    int outside = cards.Count(c => !ContainsWithin(clip, WorldRect(c)));
                    Require(outside == 0, $"{label}: การ์ดทุกใบอยู่ในกรอบแผง (ล้นออกไป {outside} ใบ)");
                }

                if (cards.Count >= 2)
                {
                    bool ordered = true;
                    for (int i = 1; i < cards.Count && ordered; i++)
                        ordered = vertical ? cards[i].position.y < cards[i - 1].position.y
                                           : cards[i].position.x > cards[i - 1].position.x;
                    Require(ordered, $"{label}: เรียงไปทาง{(vertical ? "ล่าง" : "ขวา")}ทีละใบ ไม่กองทับกัน");
                }
            }

            private static Rect WorldRect(RectTransform rt)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y),
                                       Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
            }

            /// <summary>inner อยู่ใน outer ไหม — เผื่อ 1px กันค่าปัดเศษของการจัดหน้า</summary>
            private static bool ContainsWithin(Rect outer, Rect inner, float slack = 1f)
                => inner.xMin >= outer.xMin - slack && inner.xMax <= outer.xMax + slack
                && inner.yMin >= outer.yMin - slack && inner.yMax <= outer.yMax + slack;

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

                // แถบแมพวาง **นอน** ต่างจากลิสต์ตัวละคร — ตั้งผิดแกนคือบั๊กที่เคยเกิดจริง
                if (lobby != null)
                    CheckCardLayout("แถบแมพ", ui.cardsContainer, lobby.maps.Count, vertical: false);
            }

            /// <summary>'play' ต้องสั่ง NGO ขึ้นจริง ไม่ใช่แค่สลับหน้า</summary>
            private void CheckHostStarted()
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                Require(nm != null, "มี NetworkManager ในซีน");
                if (nm == null) return;

                WaitUntil(() => nm.IsListening,
                          ok => Require(ok, "StartHost ขึ้นแล้ว (IsListening)" + (ok ? "" : " — หมดเวลารอ")));
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

            private System.Func<bool>     waitCond;
            private System.Action<bool>   onWaitDone;
            private float                 deadline;

            /// <summary>รอจนเงื่อนไขเป็นจริง หรือหมดเวลา แล้วค่อยบันทึกผล</summary>
            private void WaitUntil(System.Func<bool> cond, System.Action<bool> done, float timeout = 6f)
            {
                waitCond = cond; onWaitDone = done; deadline = Time.unscaledTime + timeout;
            }

            private void Expect(string label, string[] names)
            {
                pending = names; pendingLabel = label;
                WaitUntil(() => names.All(n => Find(n)?.activeInHierarchy == true), _ => Verify());
            }

            private void Verify()
            {
                if (pending == null) return;
                foreach (var n in pending)
                {
                    var go = Find(n);
                    bool ok = go != null && go.activeInHierarchy;
                    Require(ok, $"{pendingLabel} แล้ว {n} เปิด");

                    // ตกแล้วต้องรู้ว่าตกเพราะอะไร — ไม่เจอ object, ตัวมันปิด, หรือพ่อปิด
                    if (!ok) lines.Add($"        └ {Diagnose(n, go)}");
                }
                pending = null;
            }

            /// <summary>ไล่สายพ่อขึ้นไปหาว่าใครเป็นคนปิด — สาเหตุที่พบบ่อยที่สุดคือพ่อปิด ไม่ใช่ตัวมันเอง</summary>
            private static string Diagnose(string name, GameObject go)
            {
                if (go == null) return $"ไม่พบ GameObject ชื่อ {name} ในซีน";

                var t = go.transform;
                while (t != null)
                {
                    if (!t.gameObject.activeSelf)
                        return t == go.transform
                             ? $"{name} ปิดอยู่เอง"
                             : $"{name} เปิดอยู่ แต่พ่อ '{t.name}' ปิด";
                    t = t.parent;
                }
                return $"{name} เปิดอยู่ทั้งสาย แต่ activeInHierarchy ยังเป็น false (?)";
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
