using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เติมตาราง <c>UI</c> แล้วต่อป้ายในจอ Pause / WinLose เข้ากับ key
    /// เมนู: Tools > Clone Swarm > Localization > 4. Build UI Table
    ///
    /// ═══ รายชื่อ key กับการต่อสาย อยู่ที่เดียวกันโดยตั้งใจ ═══
    ///
    /// ถ้าแยกเป็นสองตาราง (ตัวเติมคำแปล กับ ตัวต่อสาย) วันหนึ่งจะเพิ่ม key แล้วลืม
    /// ต่อสาย หรือย้ายป้ายแล้วลืมแก้ path · ที่นี่ `Rows` ถือทั้ง path · key · คำแปล
    /// สองภาษา ครบในบรรทัดเดียว เพิ่มของใหม่จึงเพิ่มบรรทัดเดียว
    ///
    /// ═══ ของที่ไม่ต่อ และเหตุผล ═══
    ///
    /// **ResultLabel (VICTORY / DEFEAT)** — เจ้าของโปรเจกต์สั่งเว้นไว้
    ///
    /// **ป้ายที่โค้ดเขียนทับตอนรัน** — ชื่อตัวละคร · `P1 · HOST` · `Lv 14` ·
    /// `182 / 220` · เวลา · เลเวล · เวฟ · ตัวเลขทอง · บรรทัดเหตุผลรางวัล ·
    /// `100%` ของแถบเสียง · Subtitle · Order (01/02/03)
    /// ต่อไปก็ไม่มีผล เพราะโค้ดเขียนทับทีหลังทุกเฟรม — และการเห็นคอมโพเนนต์แปะอยู่
    /// บนป้ายที่ไม่มีผลจะหลอกคนอ่านซีนว่าตรงนั้นแปลแล้ว
    ///
    /// **PartyRow_Template และแถวตัวอย่าง** — เป็นแม่แบบกับของโชว์ในเอดิเตอร์
    ///
    /// ═══ MASTER / MUSIC / SFX เขียนเหมือนกันทั้งสองภาษา ═══
    ///
    /// ไม่ใช่การลืมแปล · คำพวกนี้เป็นศัพท์บนแผงเสียงที่คนไทยอ่านเป็นอังกฤษกันอยู่แล้ว
    /// และรูปแบบ mono ตัวใหญ่เป็นส่วนหนึ่งของภาษาภาพ P3R · มี key ไว้ให้เปลี่ยนได้
    /// โดยไม่ต้องแตะโค้ด ถ้าวันหนึ่งตัดสินใจเป็นอย่างอื่น
    /// </summary>
    public static class P3RUiTableBuilder
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";
        private const string TableName = "UI";

        private readonly struct Row
        {
            public readonly string Path, Key, En, Th;
            public Row(string path, string key, string en, string th)
            { Path = path; Key = key; En = en; Th = th; }
        }

        // path นับจาก **ตัวจอ** ไม่ใช่จากรากซีน — ย้ายทั้งจอไปไว้ใต้ของอื่นแล้วยังหาเจอ
        private static readonly Row[] Rows =
        {
            // ── P3R_Pause ────────────────────────────────────────────────
            new("PauseMenu/PausePanel/Title",                      "ui.pause.title",       "PAUSED",           "หยุดชั่วคราว"),
            new("PauseMenu/PausePanel/TitleHint",                  "ui.pause.hint",        "PRESS ESC TO RESUME", "ESC เพื่อกลับเข้าเกม"),
            new("PauseMenu/PausePanel/MenuList/Item_resume/Label", "ui.pause.resume",      "RESUME",           "กลับเข้าเกม"),
            new("PauseMenu/PausePanel/MenuList/Item_settings/Label","ui.pause.settings",   "SETTINGS",         "ตั้งค่า"),
            new("PauseMenu/PausePanel/MenuList/Item_quit/Label",   "ui.pause.quit",        "QUIT TO MENU",     "ออกไปเมนูหลัก"),
            new("PauseMenu/PausePanel/CoopBadge/Tag",              "ui.pause.coop.tag",    "CO-OP",            "CO-OP"),
            new("PauseMenu/PausePanel/CoopBadge/Text",             "ui.pause.coop.note",
                "You are in a room with others — the world keeps running and your character can still be hit",
                "อยู่ในห้องกับเพื่อน — โลกยังเดินอยู่ ตัวละครยังโดนตีได้"),
            new("PauseMenu/PausePanel/SettingsSubPanel/Header",       "ui.settings.audio",  "AUDIO SETTINGS", "ตั้งค่าเสียง"),
            new("PauseMenu/PausePanel/SettingsSubPanel/Label_MASTER", "ui.settings.master", "MASTER",         "MASTER"),
            new("PauseMenu/PausePanel/SettingsSubPanel/Label_MUSIC",  "ui.settings.music",  "MUSIC",          "MUSIC"),
            new("PauseMenu/PausePanel/SettingsSubPanel/Label_SFX",    "ui.settings.sfx",    "SFX",            "SFX"),

            // ── P3R_WinLose ──────────────────────────────────────────────
            new("Panel_WinLose/LeftColumn/StatRow/Stat_TIME/Label",  "ui.result.time",   "TIME",  "เวลา"),
            new("Panel_WinLose/LeftColumn/StatRow/Stat_LEVEL/Label", "ui.result.level",  "LEVEL", "เลเวล"),
            new("Panel_WinLose/LeftColumn/StatRow/Stat_WAVE/Label",  "ui.result.wave",   "WAVE",  "เวฟ"),
            new("Panel_WinLose/LeftColumn/PartyHeader",              "ui.result.party",  "PARTY", "ปาร์ตี้"),
            new("Panel_WinLose/RightColumn/RewardHeader",            "ui.result.rewards","REWARDS", "บัญชีรางวัล"),
            new("Panel_WinLose/RightColumn/TotalLabel",              "ui.result.total",  "TOTAL EARNED", "ได้รับรวม"),
            new("Panel_WinLose/RightColumn/VaultLabel",              "ui.result.vault",  "GOLD VAULT",   "ยอดทองในคลัง"),
            new("Panel_WinLose/Buttons/Btn_PlayAgain/Label",         "ui.result.play_again", "PLAY AGAIN", "เล่นอีกครั้ง"),
            new("Panel_WinLose/Buttons/Btn_Menu/Label",              "ui.result.menu",       "MAIN MENU",  "เมนูหลัก"),
            new("Panel_WinLose/Buttons/WaitingForHost",              "ui.result.waiting_host",
                "Waiting for the host to start a new run", "รอโฮสต์เริ่มรอบใหม่"),

            // ชื่อ GameObject เป็นภาษาไทย · `Transform.Find` รับได้ตามปกติ
            //
            // รอบแรกเลี่ยงไปหาด้วยชื่อลูก ("Label") เพราะคิดว่า path ภาษาไทยจะเปราะ —
            // แล้วมันไปเจอปุ่มเมนูที่ชื่อ "Label" เหมือนกัน **เขียนทับสายที่ต่อถูกไปแล้ว**
            // จอจึงขึ้น ui.settings.reset ตรงช่อง "กลับเข้าเกม" · เจอเพราะเรนเดอร์ดู
            new("PauseMenu/PausePanel/SettingsSubPanel/Btn_คืนค่าเริ่มต้น/Label",
                "ui.settings.reset", "RESET TO DEFAULT", "คืนค่าเริ่มต้น"),
            new("PauseMenu/PausePanel/SettingsSubPanel/Btn_ย้อนกลับ/Label",
                "ui.common.back",    "BACK",             "ย้อนกลับ"),
        };


        /// <summary>
        /// key ที่ **โค้ดเรียกเอง** ไม่มีป้ายในซีนให้ต่อสาย
        ///
        /// ข้อความพวกนี้ประกอบตอนรัน (ใส่ตัวเลข / เลือกตามผลแพ้ชนะ) จึงไม่มีป้ายนิ่งๆ
        /// ให้แปะคอมโพเนนต์ · แต่ยังต้องอยู่ในตารางเดียวกัน ไม่งั้นจะมีข้อความบนจอ
        /// เดียวกันที่แปลได้กับแปลไม่ได้ปนกัน ซึ่งคือสิ่งที่เจอในภาพจากตัวเกมจริง
        ///
        /// เหตุผลที่นับจำนวนได้มีสอง key — ดูคำอธิบายใน RewardLineUI.DefaultLabel
        /// </summary>
        private static readonly (string key, string en, string th)[] CodeRows =
        {
            ("ui.result.subtitle.win",  "ARENA 01 · CLEARED",     "ARENA 01 · เคลียร์แล้ว"),
            ("ui.result.subtitle.lose", "ARENA 01 · PARTY WIPED", "ARENA 01 · ปาร์ตี้ล้มทั้งทีม"),

            ("ui.reward.time_survived",    "Time survived",            "เวลาที่รอด"),
            ("ui.reward.time_survived_n",  "Survived to minute {0}",   "รอดถึงนาที {0}"),
            ("ui.reward.enemy_kills",      "Enemies defeated",         "ศัตรูที่กำจัด"),
            ("ui.reward.enemy_kills_n",    "{0} enemies defeated",     "ศัตรูที่กำจัด {0} ตัว"),
            ("ui.reward.objective_gold",   "Bonus gold during the run","ทองพิเศษระหว่างรอบ"),
            ("ui.reward.win_bonus",        "Main Boss defeated",       "ล้ม Main Boss สำเร็จ"),
            ("ui.reward.gold_find",        "Treasure hunter bonus",    "โบนัสนักล่าสมบัติ"),
            ("ui.reward.other",            "Other",                    "อื่นๆ"),
        };

        [MenuItem("Tools/Clone Swarm/Localization/4. Build UI Table")]
        public static void Build() => Run(overwriteExisting: false);

        [MenuItem("Tools/Clone Swarm/Localization/4b. Build UI Table (เขียนทับของเดิม)")]
        public static void BuildOverwrite()
        {
            if (EditorUtility.DisplayDialog("เขียนทับคำแปลเดิม",
                    "คำแปลที่คนแก้ไว้ในตาราง UI จะถูกเขียนทับด้วยค่าตั้งต้นในสคริปต์ ยืนยันไหม",
                    "เขียนทับ", "ยกเลิก"))
                Run(overwriteExisting: true);
        }

        private static void Run(bool overwriteExisting)
        {
            var log = new StringBuilder("[UI แปล]\n");

            int wrote = WriteTable(overwriteExisting, log);
            if (wrote < 0) return;

            WireScene(log);
            Debug.Log(log.ToString());
        }

        // ══ 1. เติมตาราง ═══════════════════════════════════════════════════
        private static int WriteTable(bool overwrite, StringBuilder log)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            if (collection == null)
            {
                Debug.LogError($"[UI แปล] ไม่เจอตาราง '{TableName}' — สร้างใน Window > Asset Management > Localization Tables ก่อน");
                return -1;
            }

            var en = collection.StringTables.FirstOrDefault(t => t.LocaleIdentifier.Code == "en");
            var th = collection.StringTables.FirstOrDefault(t => t.LocaleIdentifier.Code == "th-TH");

            if (en == null || th == null)
            {
                Debug.LogError($"[UI แปล] ตาราง '{TableName}' ไม่มีภาษาครบ (en / th-TH)");
                return -1;
            }

            var all = Rows.Select(r => (r.Key, r.En, r.Th))
                          .Concat(CodeRows.Select(c => (c.key, c.en, c.th)))
                          .ToArray();

            int wrote = 0, skipped = 0;
            foreach (var (key, e, t) in all)
            {
                collection.SharedData.AddKey(key);   // มีอยู่แล้วก็ไม่เป็นไร

                wrote   += SetValue(en, key, e, overwrite) ? 1 : 0;
                wrote   += SetValue(th, key, t, overwrite) ? 1 : 0;
                skipped += 2;
            }

            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(th);
            AssetDatabase.SaveAssets();

            log.AppendLine($"  ตาราง '{TableName}' — เขียนลง {wrote} ค่า · ข้ามที่มีอยู่แล้ว {skipped - wrote} · ทั้งหมด {all.Length} key");
            return wrote;
        }

        /// <summary>เขียนค่าถ้าช่องยังว่าง — ไม่ทับคำแปลที่คนแก้ไว้ กติกาเดียวกับ harvester</summary>
        private static bool SetValue(UnityEngine.Localization.Tables.StringTable table,
                                     string key, string value, bool overwrite)
        {
            var entry = table.GetEntry(key);
            if (entry != null && !overwrite && !string.IsNullOrEmpty(entry.LocalizedValue)) return false;

            table.AddEntry(key, value);
            return true;
        }

        // ══ 2. ต่อสายในซีน ═════════════════════════════════════════════════
        private static void WireScene(StringBuilder log)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var panels = new Dictionary<string, Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "P3R_Pause" || t.name == "P3R_WinLose")
                        panels[t.name] = t;

            int wired = 0;
            var missing  = new List<string>();
            var claimed  = new Dictionary<GameObject, string>();

            foreach (var r in Rows)
            {
                string panelName = r.Key.StartsWith("ui.result.") ? "P3R_WinLose" : "P3R_Pause";
                var target = panels.TryGetValue(panelName, out var p) ? p.Find(r.Path) : null;

                if (target == null) { missing.Add($"{panelName}/{r.Path}"); continue; }

                // ป้ายเดียวถูกจองสอง key = มี path ที่ชี้ของผิด · เงียบไม่ได้เด็ดขาด
                // เพราะอาการคือ "จอขึ้นข้อความของช่องอื่น" ซึ่งดูเหมือนคำแปลผิด
                // ไม่ได้ดูเหมือนสายผิด แล้วจะไปไล่หาผิดที่
                if (claimed.TryGetValue(target.gameObject, out var already) && already != r.Key)
                {
                    missing.Add($"ป้าย {panelName}/{r.Path} ถูกจองด้วย '{already}' ไปแล้ว — '{r.Key}' ชนกัน");
                    continue;
                }

                if (!Attach(target.gameObject, r.Key)) continue;
                claimed[target.gameObject] = r.Key;
                wired++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            log.AppendLine($"  ต่อสายในซีน {wired} ป้าย · บันทึก {ScenePath}");

            // หาป้ายไม่เจอ = คำแปลอยู่ในตารางแต่ไม่มีใครเอาไปใช้ ซึ่งเงียบสนิทบนจอ
            if (missing.Count > 0)
            {
                log.AppendLine($"  ⚠ หาป้ายไม่เจอ {missing.Count} จุด:");
                foreach (var m in missing) log.AppendLine($"      {m}");
            }
        }

        private static bool Attach(GameObject go, string key)
        {
            if (go.GetComponent<TMP_Text>() == null) return false;

            var loc = go.GetComponent<P3RLocalizedText>() ?? go.AddComponent<P3RLocalizedText>();
            loc.table = TableName;
            loc.key   = key;
            EditorUtility.SetDirty(go);
            return true;
        }
    }
}
