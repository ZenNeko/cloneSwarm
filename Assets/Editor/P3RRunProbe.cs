using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เล่นเกมจริงในโหมด headless แล้วอ่าน Console — ไม่ใช่การตรวจสาย แต่เป็นการดู
    /// ว่าเส้นทางจริงวิ่งจริง
    ///
    /// เมนู: Tools > Clone Swarm > Run Probe
    /// batchmode: -executeMethod CloneSwarm.EditorTools.P3RRunProbe.Run **ห้ามใส่ -quit**
    ///
    /// ═══ ทำไมสโมกเทสต์เดิมตอบเรื่องนี้ไม่ได้ ═══
    ///
    /// `P3RSmokeTest` ไปถึง SampleScene ด้วย `SceneManager.LoadScene` ธรรมดา ซึ่ง
    /// NGO ไม่รู้เรื่องเลย · NetworkObject ที่วางไว้ในซีนจึงไม่ถูก spawn แปลว่า
    /// `GameTimeline.OnNetworkSpawn` ไม่เคยวิ่ง และไม่มีนาฬิกาเกมให้เดิน
    ///
    /// เส้นทางจริงคือ `GameSessionManager.StartGame(scene)` → `nm.SceneManager.LoadScene`
    /// ตัวนี้จึงเดินทางนั้น ไม่ใช่ทางลัด — ถ้าเดินทางลัดแล้วผ่าน มันไม่ได้บอกอะไรเลย
    ///
    /// ═══ เร่งเวลา ไม่ใช่รอจริง ═══
    ///
    /// นัดหมายแรกที่น่าสนใจอยู่นาที 3 · เร่งได้เพราะนาฬิกาเกมสะสมจาก
    /// `Time.deltaTime` ซึ่งถูกสเกลไปด้วย · ไม่ได้ข้ามเวลา แค่เดินเร็วขึ้น
    /// เส้นทางทุกอย่างจึงวิ่งครบเหมือนเล่นจริง
    ///
    /// **เขียนผ่าน `GamePause.ResumeScale` ไม่ใช่ `Time.timeScale`** · GamePause
    /// เป็นเจ้าของ timeScale คนเดียว และคืนค่าเป็น `_resumeScale` ทุกครั้งที่จอเลือก
    /// การ์ดปิด — เขียนตรงจึงถูกทับตั้งแต่เลเวลอัปครั้งแรก แล้วรันคลานไปด้วยความเร็ว
    /// ปกติทั้งที่ log บอกว่าเร่งไปแล้ว
    ///
    /// ═══ ต้องรีบเลือกการ์ดแทนคนด้วย ═══
    ///
    /// จอเลือกการ์ดหยุดเกม (timeScale 0) แล้วรอ `upgradePickSeconds` ซึ่งเป็น
    /// **เวลาจริง** 30 วินาที · headless ไม่มีใครกด รันหนึ่งจึงเสียเวลาจริง 30 วิ
    /// ต่อหนึ่งเลเวล · probe ย่นตัวจับเวลาให้สั้นลงแทนที่จะไปกดปุ่มเอง —
    /// ใช้เส้นทาง auto-pick ที่มีอยู่แล้ว ไม่ใช่เส้นทางใหม่ที่เทสต์เท่านั้นที่เดิน
    ///
    /// ═══ ไม่ใช่ของแทนการเล่นเอง ═══
    ///
    /// headless ไม่มีอินพุต ไม่มีคนเดินไปเก็บ orb · ตัวนี้ตอบได้แค่ว่า "ระบบที่ทำงาน
    /// เองตามเวลา ทำงานไหม" ส่วนอะไรที่ต้องมีคนกด ยังต้องเล่นเอง
    ///
    /// ═══ ต้องอุ้มผู้เล่นไว้ และนั่นทำให้ผลอ่านต่างไป ═══
    ///
    /// ไม่มีอินพุต = ยืนนิ่งให้ศัตรูรุม · รอบแรกตายที่ 0:31 ก่อนนัดหมายแรกจะถึง
    /// probe จึงเติมเลือดให้เต็มทุกเฟรม ซึ่งแปลว่า **ผลที่ได้ไม่ได้บอกอะไรเรื่องบาลานซ์
    /// เลย** — ไม่ใช่ว่ารันนี้ "รอดถึงนาที 3" แต่คือ "ถ้ารอดถึงนาที 3 ระบบจะทำงานถูก"
    /// </summary>
    public static class P3RRunProbe
    {
        private const string StateKey   = "p3r.runprobe.state";
        private const string MenuScene  = "Assets/GameScenes/MenuScene.unity";
        private const string MapPath    = "Assets/ScriptableObjects/Map/MapData_Arena01.asset";

        /// <summary>เดินนาฬิกาเกมถึงวินาทีที่เท่าไรถึงพอ — เลย 3 นาทีไปหน่อยให้นัดหมายได้ยิง</summary>
        private const float  TargetGameSeconds = 200f;
        private const float  Speed             = 20f;

        /// <summary>ย่นเวลารอเลือกการ์ดเหลือเท่านี้ — 0 แปลว่า "ไม่มีกำหนด" จึงใช้ไม่ได้</summary>
        private const float  PickSeconds       = 0.5f;

        [MenuItem("Tools/Clone Swarm/Run Probe (เล่นจริง 3 นาทีเกม)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Probe] อยู่ใน play mode อยู่แล้ว — ออกก่อนแล้วค่อยรันใหม่");
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

            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying) return;
                var go = new GameObject("~P3RRunProbe") { hideFlags = HideFlags.HideAndDontSave };
                go.AddComponent<Runner>();
            };
        }

        internal static void Finish(string report, bool ok)
        {
            SessionState.SetString(StateKey, "");
            Debug.Log(report);

            if (!Application.isBatchMode) { EditorApplication.isPlaying = false; return; }

            // เหตุผลเดียวกับ P3RSmokeTest — ต้องรอให้ออกจาก play mode **จริง** ก่อนปิด
            // ไม่งั้น Unity เขียนสถานะตอน play mode ลงไฟล์ซีน แล้วจอที่ซ่อนตัวเองใน
            // Awake ถูกบันทึกเป็น "ปิด" · เครื่องมือที่ทำลายซีนที่มันเพิ่งตรวจ คือเครื่องมือ
            // ที่ทำให้คนเลิกเชื่อผลของมัน
            void OnPlayModeChanged(PlayModeStateChange state)
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;

                try { EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single); }
                catch (System.Exception e) { Debug.LogWarning($"[Probe] คืนซีนไม่สำเร็จ: {e.Message}"); }

                EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = false;
        }

        // ═══════════════════════════════════════════════════════════════════
        private class Runner : MonoBehaviour
        {
            private readonly List<string> log    = new();
            private readonly List<string> errors = new();

            /// <summary>บรรทัดที่สนใจ — เก็บทุกบรรทัดที่ขึ้นต้นด้วย prefix เหล่านี้</summary>
            private static readonly string[] Watch =
            {
                "[GameTimeline]",
                "[WaveManager]",
                "[Session]",
                "[SharedEXP] Level Up",
                "[ObjectiveManager]",
                "[BossManager]",
            };

            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
                Application.logMessageReceived += OnLog;
                StartCoroutine(Drive());
            }

            private void OnDestroy() => Application.logMessageReceived -= OnLog;

            private void OnLog(string msg, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    bool foreign = stack != null &&
                                   (stack.Contains("Library/PackageCache") || stack.Contains("Synty"));
                    if (!foreign) errors.Add($"      [{type}] {msg.Split('\n')[0]}");
                    return;
                }

                string head = msg.Split('\n')[0];
                if (Watch.Any(head.StartsWith)) log.Add(head);
            }

            /// <summary>
            /// รอเงื่อนไข ไม่ใช่รอเวลา — ใช้ unscaledTime เพราะ timeScale ถูกเร่งไว้
            /// รอด้วยเวลาที่ถูกเร่งจะหมดเวลาเร็วกว่าที่ตั้งใจ 10 เท่า
            /// </summary>
            private IEnumerator Until(System.Func<bool> cond, float timeoutSec, string what)
            {
                float deadline = Time.unscaledTime + timeoutSec;
                while (!cond())
                {
                    if (Time.unscaledTime > deadline)
                    {
                        Report(false, $"หมดเวลารอ: {what} ({timeoutSec:0}s)");
                        yield break;
                    }
                    yield return null;
                }
            }

            private bool _stopped;
            private bool _keepAlive;

            /// <summary>
            /// เติมเลือดให้ทุกคนเต็มทุกเฟรม — probe ยืนนิ่ง ไม่งั้นตายก่อนถึงนัดหมายแรก
            ///
            /// เขียน NetworkVariable ตรงๆ ได้เพราะ probe เป็น host · ไม่ปลุกคนที่ตายไปแล้ว
            /// (isDead เป็นเรื่องของ Respawn) จึงต้องเริ่มเติมตั้งแต่ก่อนโดนตีนัดแรก
            /// </summary>
            private void LateUpdate()
            {
                if (!_keepAlive) return;

                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer) return;

                foreach (var c in nm.ConnectedClientsList)
                {
                    var pm = c.PlayerObject != null ? c.PlayerObject.GetComponent<playermove>() : null;
                    if (pm == null || pm.isDead.Value) continue;
                    pm.netHealth.Value = pm.netMaxHealth.Value;
                }
            }

            /// <summary>รันจบไปแล้วหรือยัง — รอต่อไปก็ไม่มีอะไรเกิดขึ้น เพราะนาฬิกาหยุดแล้ว</summary>
            private bool RunEnded => log.Any(l => l.Contains("LOSE") || l.Contains("WIN"));

            private IEnumerator Drive()
            {
                // ── 1. ขึ้น host ───────────────────────────────────────────
                yield return Until(() => NetworkManager.Singleton != null, 30f, "NetworkManager");
                if (_stopped) yield break;

                var nm = NetworkManager.Singleton;
                if (!nm.IsListening) nm.StartHost();

                yield return Until(() => nm.IsListening, 30f, "StartHost");
                if (_stopped) yield break;

                // ── 2. ตั้งแมพ **ก่อน** โหลดซีน ────────────────────────────
                //
                // GameTimeline.ResolveSchedule กับ WaveManager.ResolveScaling อ่าน
                // RunSetup.Map ตอน OnNetworkSpawn ซึ่งเกิดทันทีที่ซีนโหลดเสร็จ
                // ตั้งทีหลังคือตั้งไม่ทัน แล้วจะได้ค่าของซีนโดยที่ไม่มีอะไรผิดให้เห็น
                var map = AssetDatabase.LoadAssetAtPath<MapData>(MapPath);
                if (map == null) { Report(false, $"ไม่เจอ MapData ที่ {MapPath}"); yield break; }

                RunSetup.Set(map, DifficultyTier.Normal);
                log.Add($"      → ตั้ง RunSetup: {map.mapId} / {RunSetup.Difficulty}");

                // ── 3. เข้าเกมทางเดียวกับที่ล็อบบี้ใช้ ─────────────────────
                yield return Until(() => GameSessionManager.Instance != null, 30f, "GameSessionManager");
                if (_stopped) yield break;

                if (!GameSessionManager.Instance.StartGame("SampleScene"))
                { Report(false, "StartGame คืน false"); yield break; }

                // ── 4. รอให้นาฬิกาเกมเริ่มเดินจริง ─────────────────────────
                yield return Until(() => GameTimeline.Instance != null, 60f, "GameTimeline spawn");
                if (_stopped) yield break;

                _keepAlive = true;

                // ── ตรวจตารางแปลก่อนรอนาฬิกา ───────────────────────────────
                //
                // ทำตรงนี้เพราะหลักฐานเรื่องคำแปลไม่ควรขึ้นกับว่ารันไปถึงนาทีไหน ·
                // ถ้ารอให้บอสออกมาประกาศเองแล้วค่อยดู รันที่ตายก่อนจะไม่ได้คำตอบเลย
                ResolveAnnouncements();

                yield return Until(() => GameTimeline.Instance.hasStarted.Value, 90f, "นาฬิกาเริ่มเดิน");
                if (_stopped) yield break;

                // ── 5. เร่งเวลา + ย่นเวลารอเลือกการ์ด ──────────────────────
                GamePause.ResumeScale = Speed;

                var sem = SharedExperienceManager.Instance;
                if (sem != null) sem.upgradePickSeconds = PickSeconds;

                log.Add($"      → เร่งเวลา ×{Speed:0} · เวลาเลือกการ์ด {PickSeconds:0.#}s");

                // หยุดรอเมื่อรันจบด้วย — นาฬิกาหยุดเดินแล้ว รอต่อไปก็แค่หมดเวลาเปล่าๆ
                yield return Until(
                    () => GameTimeline.Instance == null || RunEnded ||
                          GameTimeline.Instance.GetGameTime() >= TargetGameSeconds,
                    600f, $"เดินนาฬิกาถึง {TargetGameSeconds:0}s");
                if (_stopped) yield break;

                float reached = GameTimeline.Instance != null ? GameTimeline.Instance.GetGameTime() : 0f;

                if (RunEnded && reached < TargetGameSeconds)
                { Report(false, $"รันจบเองที่ {reached / 60f:0.0} นาที — ยังไม่ถึงนัดหมายที่ตั้งใจดู"); yield break; }

                Report(true, $"เดินถึง {reached / 60f:0.0} นาทีเกม");
            }

            /// <summary>
            /// เรียก GameHUD.ResolveAnnouncement ทุก key ที่โค้ดใช้ แล้วบันทึกผล
            ///
            /// ═══ ทำไมไม่รอให้บอสประกาศเอง ═══
            ///
            /// ประกาศแต่ละอันผูกกับกลไกคนละตัว — tether ต้องมีบอสเฟสนั้น · floor hazard
            /// ต้องมีท่านั้นออก · หลายอันไม่มีวันเกิดใน headless ที่ไม่มีคนเล่น
            /// ถ้ารอดู จะได้หลักฐานแค่ไม่กี่อันแล้วเข้าใจเอาเองว่าที่เหลือก็คงใช้ได้
            ///
            /// เรียกตรงๆ ตอบได้ครบทุก key ว่า **lookup ทำงานไหมและได้ข้อความอะไร** ·
            /// สิ่งที่ยังตอบไม่ได้คือฟอนต์วาดออกมาหน้าตายังไง ซึ่งต้องดูภาพเท่านั้น
            ///
            /// locale ปัจจุบันติดมาในรายงานด้วย — ข้อความไทยที่ออกมาถูกต้องแต่ locale
            /// เป็น en แปลว่าตัวเลือกภาษาไม่ทำงาน ซึ่งเป็นคนละปัญหากับตารางว่าง
            /// </summary>
            private void ResolveAnnouncements()
            {
                var loc = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
                log.Add($"      -> locale ปัจจุบัน: {(loc != null ? loc.Identifier.Code : "(ไม่มี)")}");

                var keys = new List<string>();
                foreach (var file in System.IO.Directory.GetFiles("Assets/Script", "*.cs",
                                                                  System.IO.SearchOption.AllDirectories))
                    foreach (System.Text.RegularExpressions.Match m in
                             System.Text.RegularExpressions.Regex.Matches(
                                 System.IO.File.ReadAllText(file), KEYRE))
                    {
                        string k = m.Groups[1].Value;
                        if (!k.EndsWith(".") && !keys.Contains(k)) keys.Add(k);
                    }

                // key ที่ต่อไว้กับป้ายในซีน — ไม่ได้โผล่เป็น literal ในโค้ด เพราะอยู่ใน
                // ตัว builder ฝั่ง editor · แต่มันคือข้อความที่ผู้เล่นเห็นจริงบนจอ
                // ถ้าไม่ไล่ตรงนี้ จะเหลือแต่ของที่โค้ดเรียก แล้วเข้าใจว่าครอบหมดแล้ว
                foreach (var label in Object.FindObjectsByType<CloneSwarm.UI.P3R.P3RLocalizedText>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!string.IsNullOrEmpty(label.key) && !keys.Contains(label.key))
                        keys.Add(label.key);

                keys.Sort();
                int bad = 0;

                foreach (var k in keys)
                {
                    // เลือกตารางตามคำนำหน้า — key ของจอกับของประกาศอยู่คนละตาราง
                    // ส่งผิดตารางจะได้ "ไม่มี key" ทั้งที่มี ซึ่งชี้ไปแก้ผิดที่
                    string v = k.StartsWith("ui.")
                             ? CloneSwarm.UI.P3R.P3RStrings.Ui(k, 3)
                             : GameHUD.ResolveAnnouncement(k, 3);
                    bool ok = !string.IsNullOrEmpty(v) && v != k;
                    if (!ok) bad++;
                    log.Add($"      {(ok ? "  " : "X ")}{k} = {v}");
                }

                log.Add($"      -> key ทั้งหมด {keys.Count} · หาไม่เจอ {bad}");
                _announceKeys    = keys.Count;
                _announceMissing = bad;
            }

            /// <summary>
            /// รูปแบบ key — **ต้องอยู่ในอัญประกาศ**
            ///
            /// รอบแรกจับแบบไม่มีอัญประกาศ แล้วได้ของปลอมสองตัว: `ui.panel` ที่ตัดมาจาก
            /// `ui.panelRoot` ในคอมเมนต์ และ `ui.pause.title` ที่เป็นตัวอย่างใน Tooltip
            /// — รายงานว่า "หาไม่เจอ 1" ทั้งที่ทุก key จริงแปลออกครบ
            ///
            /// เทสต์ที่แดงด้วยเรื่องที่ไม่ใช่ปัญหา ทำให้คนเลิกอ่านผลเร็วพอๆ กับเทสต์ที่เขียวเสมอ
            ///
            /// ตัวที่ลงท้ายด้วยจุดคือ prefix ที่ต่อค่าตอนรัน ข้ามไป
            /// </summary>
            private const string KEYRE = "\"((?:announce|ui)\\.[a-z0-9_.]+)\"";

            private int _announceKeys, _announceMissing = -1;

            // ── รายงาน ────────────────────────────────────────────────────
            private void Report(bool reachedEnd, string note)
            {
                if (_stopped) return;
                _stopped = true;
                GamePause.ResumeScale = 1f;

                var sb = new StringBuilder();
                sb.AppendLine("╔══ RUN PROBE ══════════════════════════════════════════");
                sb.AppendLine($"   {note}");
                sb.AppendLine("   ── Console ──────────────────────────────────────────");

                foreach (var l in log) sb.AppendLine("   " + l);

                if (errors.Count > 0)
                {
                    sb.AppendLine("   ── error ระหว่างรัน ─────────────────────────────────");
                    foreach (var e in errors.Distinct().Take(15)) sb.AppendLine(e);
                }
                else sb.AppendLine("   ไม่มี error ระหว่างรัน");

                // ── สามบรรทัดที่ตั้งใจมาดู ────────────────────────────────
                sb.AppendLine("   ── สรุป ─────────────────────────────────────────────");
                Check(sb, "ตารางมาจากแมพ",  log.Any(l => l.Contains("ตารางจากแมพ")));
                Check(sb, "สเกลมาจากแมพ",   log.Any(l => l.Contains("สเกลศัตรูจากแมพ")));
                Check(sb, "นัดหมายยิงจริง", log.Any(l => l.Contains("นัดหมาย '")));
                Check(sb, "wave เลื่อนตามเวลา", log.Count(l => l.Contains("] Wave ")) > 1);
                if (_announceMissing >= 0)
                    Check(sb, $"ประกาศทุก key แปลออก ({_announceKeys} key · หาไม่เจอ {_announceMissing})",
                          _announceMissing == 0);

                sb.AppendLine("╚═══════════════════════════════════════════════════════");

                bool ok = reachedEnd && errors.Count == 0
                       && log.Any(l => l.Contains("ตารางจากแมพ"))
                       && log.Any(l => l.Contains("สเกลศัตรูจากแมพ"))
                       && log.Any(l => l.Contains("นัดหมาย '"));

                P3RRunProbe.Finish(sb.ToString(), ok);
            }

            private static void Check(StringBuilder sb, string what, bool ok)
                => sb.AppendLine($"   {(ok ? "ผ่าน" : "ไม่ผ่าน")}  {what}");
        }
    }
}
