using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static CloneSwarm.EditorTools.P3RBuilderKit;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ทา **สีกับฟอนต์** ของ HUD ประจำจอใน `SampleScene` ให้เป็นชุด P3R
    /// เมนู: Tools > Clone Swarm > Restyle Gameplay HUD (P3R)
    ///
    /// ═══ ทำไมถึงเป็นตัวรีสกิล ไม่ใช่ builder ═══
    ///
    /// อีกสิบสองจอสร้างใหม่ทั้งจอได้เพราะไม่มีใครถือของในนั้นอยู่ · HUD ตรงข้าม —
    /// `GameHUD` · `WeaponStatHUD` · `StatusHUDUI` ต่อสายไว้กับ object พวกนี้เป็นสิบช่อง
    /// และผูกกับ NetworkManager กับผู้เล่นที่ spawn แล้ว การสร้างใหม่คือทำสายขาดทั้งหมด
    /// ตัวนี้จึงเดินเข้าไปแก้ค่าเฉพาะช่องสีกับช่องฟอนต์ **ไม่แตะโครงสร้าง ไม่แตะตำแหน่ง**
    ///
    /// ═══ ข้อจำกัดโซน C ที่ต้องเคารพ ═══
    ///
    /// เอาได้แค่ **สี · ฟอนต์ · มุมบากเฉียง** — ตัวนี้ทำแค่สองอย่างแรก ไม่แตะรูปทรงเลย
    ///   ห้ามเฉือนแถบ HP — ความยาวแถบคือข้อมูล เอียงแล้วอ่านค่าผิด
    ///   ห้ามลายทับตัวเลข — ตัวเลขต้องอ่านออกตอนศัตรูเต็มจอ
    ///   ห้ามโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด
    /// ก่อน/หลังต้องผ่าน **5-second test** ซึ่งต้องกดเล่นจริง ตัวนี้ทำแทนไม่ได้
    ///
    /// ═══ กับดักที่ตัวนี้จงใจเลี่ยง: ของที่โค้ดเขียนทับตอนรัน ═══
    ///
    /// ทาสีลงช่องที่มีเจ้าของตอนรันอยู่แล้ว = ได้สองคนเขียนสีเดียวกัน ค่าที่ทาไว้จะถูกลบ
    /// ในเฟรมแรกโดยไม่มีอะไรฟ้อง แล้วคนแก้ก็จะงงว่าทำไมรีสกิลแล้วไม่เปลี่ยน
    /// รายชื่อที่เลี่ยงและเหตุผลอยู่ที่ <see cref="RuntimeOwned"/> — แก้ที่ **ต้นทาง** แทน
    /// </summary>
    public static class P3RGameplayHudRestyler
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";
        private const string CanvasName = "HUDCanvas";

        private const string FontHeavy = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-ExtraBold SDF.asset";
        private const string FontMono  = "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-SemiBold SDF.asset";

        // แถบพื้นหลังของ bar — เข้มกว่า Panel เล็กน้อยเพื่อให้ส่วนที่เติมอ่านออกชัด
        private static readonly Color BarBack = new Color32(0x1A, 0x1F, 0x33, 0xFF);

        /// <summary>
        /// ช่องสีที่ **โค้ดเขียนทับทุกเฟรม/ทุกรอบ refresh** — ห้ามทาในซีน
        /// เก็บไว้เป็นรายชื่อจริงเพื่อให้รายงานบอกได้ว่า "ข้ามเพราะอะไร" ไม่ใช่เงียบหายไป
        /// </summary>
        private static readonly Dictionary<string, string> RuntimeOwned = new()
        {
            ["Q_BG"] = "GameHUD.ApplySlotColor() — แก้ที่ abilityReady/Cooldown/ActiveColor แทน",
            ["E_BG"] = "GameHUD.ApplySlotColor() — แก้ที่ abilityReady/Cooldown/ActiveColor แทน",
            ["Charge_Fill"] = "GameHUD.UpdatePassiveBar() อ่านจาก IHUDPassiveBar.BarColor — " +
                              "แก้ที่ ChargeManager / GunnerPassiveWeapon / HunterPassiveWeapon",
            ["Icon"] = "รูปของศิลปิน — ต้องเป็น Color.white เสมอ สีคูณเข้าพิกเซลของ sprite",
        };

        // ═══════════════════════════════════════════════════════════════════
        // ตารางสี — path เทียบจาก HUDCanvas
        //
        // ค่าที่เห็นในคอลัมน์ "เดิม" คือสิ่งที่อยู่ในซีนตอนเขียนตารางนี้ เก็บไว้เป็นคำอธิบาย
        // ว่าทำไมถึงเปลี่ยน ไม่ได้เอาไปเทียบตอนรัน (ตัวนี้ทาทับให้ตรงเป้าเสมอ จึงรันซ้ำได้)
        // ═══════════════════════════════════════════════════════════════════
        private static (string path, Color to, string why)[] ImageTargets() => new[]
        {
            // แถบเวลา — แบบวาดเป็นสแลบน้ำเงินทึบ ไม่ใช่แผ่นดำโปร่ง
            ("Timer_Panel", Primary, "เดิม #0A0A0F a.82 — แบบให้เป็นสแลบน้ำเงิน"),

            ("BottomLeft_Panel", A(Panel, 0.82f), "เดิม #0A0A0F a.82 — สีนอกชุด"),
            ("WeaponRow",        A(Panel, 0.82f), "เดิม #0A0A0F a.82 — สีนอกชุด"),
            ("StatRow",          A(Panel, 0.82f), "เดิม #0A0A0F a.82 — สีนอกชุด"),

            // HP — แบบใช้ teal ไม่ใช่เขียวสด · พื้นแถบเดิมเป็นเทากลางซึ่งกลืนกับสนาม
            ("BottomLeft_Panel/HPRow/HP_BarBG",              BarBack, "เดิม #1E1E1E เทากลาง"),
            ("BottomLeft_Panel/HPRow/HP_BarBG/HP_Fill",      Teal,    "เดิม #2DCC47 เขียวสดนอกชุด"),
            ("BottomLeft_Panel/HPRow/HP_BarBG/Shield_Fill",  BlueSlot,"เดิม #4CA5FF ฟ้านอกชุด"),

            // EXP — เดิมเป็นม่วง ซึ่งไม่มีในชุดสีเลย
            ("BottomLeft_Panel/EXP_BarBG",          BarBack,  "เดิม #19142D ม่วงเข้มนอกชุด"),
            ("BottomLeft_Panel/EXP_BarBG/EXP_Fill", BlueSlot, "เดิม #723FD8 ม่วงนอกชุด"),

            // ช่องสกิล — พื้นช่อง (ไม่ใช่ Q_BG/E_BG ที่โค้ดถืออยู่)
            ("BottomLeft_Panel/Q_Slot", Card, "เดิม #333347 เทาอมน้ำเงินนอกชุด"),
            ("BottomLeft_Panel/E_Slot", Card, "เดิม #333347 เทาอมน้ำเงินนอกชุด"),
            ("BottomLeft_Panel/Q_Slot/CooldownFill", A(InkDeep, 0.72f), "เดิมดำสนิท a.72"),
            ("BottomLeft_Panel/E_Slot/CooldownFill", A(InkDeep, 0.72f), "เดิมดำสนิท a.72"),
            ("BottomLeft_Panel/Q_Slot/ExileGlow (1)", A(Orange, 0.85f), "เดิม #FF8C19 ส้มนอกชุด"),
            ("BottomLeft_Panel/E_Slot/ExileGlow",     A(Orange, 0.85f), "เดิม #FF8C19 ส้มนอกชุด"),

            // แถบ charge — ตัว fill มีเจ้าของตอนรัน ทาได้แค่กรอบกับราง
            ("BottomLeft_Panel/ChargeBar_Panel",          A(Panel, 0.90f), "เดิม #141419 นอกชุด"),
            ("BottomLeft_Panel/ChargeBar_Panel/ChargeBG", BarBack,         "เดิม #0F0F14 นอกชุด"),

            ("DamageFeedbackPanel", A(Red, 0.50f),     "เดิมแดงสด #FF0000 a.50"),
            ("RespawnOverlay",      A(InkDeep, 0.72f), "เดิมดำสนิท a.72"),
        };

        // ═══════════════════════════════════════════════════════════════════
        // ตารางฟอนต์
        //
        // ป้ายที่เป็น **LiberationSans** คือป้ายที่ไม่เคยถูกกำหนดฟอนต์เลย TMP เลยถอย
        // ไปใช้ตัวสำรองของตัวเอง · มันไม่มีกลิฟไทย วันที่ป้ายพวกนี้ถูกแปลจะกลายเป็นกล่อง
        // ═══════════════════════════════════════════════════════════════════
        private static (string path, bool heavy)[] FontTargets() => new[]
        {
            // ตัวเลขใหญ่ที่ต้องอ่านทันระหว่างศัตรูเต็มจอ — แบบระบุน้ำหนัก 800
            ("Timer_Panel/TimerText", true),
            ("BottomLeft_Panel/HPRow/HP_BarBG/HP_Text", true),
            ("BottomLeft_Panel/EXP_BarBG/Level_Text", true),
            ("BottomLeft_Panel/Q_Slot/CDText", true),
            ("BottomLeft_Panel/E_Slot/CDText", true),
            ("BottomLeft_Panel/ChargeBar_Panel/ChargeBG/ChargeText", true),
            ("Announcement", true),
            ("RespawnOverlay/RespawnText", true),

            // ป้ายกำกับตัวเล็ก — น้ำหนักกลาง
            ("BottomLeft_Panel/HPRow/HP_Label", false),
            ("BottomLeft_Panel/ChargeBar_Panel/ChargeLabel", false),
            ("BottomLeft_Panel/Q_Slot/KeyHint", false),
            ("BottomLeft_Panel/E_Slot/KeyHint", false),
            ("WeaponRow/WeaponLabel", false),
            ("StatRow/StatLabel", false),
        };

        // ═══════════════════════════════════════════════════════════════════
        // แถว objective — อยู่บน **prefab** ไม่ใช่ในซีน เพราะถูก Instantiate หลายใบตอนรัน
        // (`ObjectiveTrackerHUD.entryPrefab`) · เส้นแบ่งเดียวกับการ์ด/แถวของอีกสิบสองจอ
        //
        // ลูกชื่อ `Image` **ไม่อยู่ในตาราง** — มันมี sprite จริงขนาด 24×32 คือไอคอน
        // สีของ Image คูณเข้าพิกเซลของ sprite ย้อมเมื่อไรภาพก็ไม่ตรงกับที่คนวาดส่งมา
        // ═══════════════════════════════════════════════════════════════════
        private const string ObjectiveEntryPrefab = "Assets/Prefab/UI/ObjectiveTrackerEntry.prefab";

        private static (string path, Color to, string why)[] PrefabImageTargets() => new[]
        {
            ("", A(Panel, 0.82f), "เดิม #0A0A0F a.82 — สีนอกชุด ชุดเดียวกับแผง HUD อื่น"),
        };

        private static (string path, bool heavy)[] PrefabFontTargets() => new[]
        {
            ("TimerText",    true),    // ตัวเลขนับถอยหลัง — ต้องอ่านทัน
            ("progressText", true),    // 4 / 6
            ("headerText",   false),
            ("taskText",     false),
        };

        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Clone Swarm/Restyle Gameplay HUD (P3R)")]
        public static void Restyle()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "รีสกิล HUD ในซีนเกม",
                    "จะแก้ **สีกับฟอนต์** ของ HUD ประจำจอใน SampleScene แล้วเซฟทับซีน\n\n" +
                    "ไม่แตะโครงสร้างและตำแหน่ง · รันซ้ำได้ผลเหมือนเดิม\n" +
                    "ควรมี git ที่สะอาดไว้ก่อน เผื่ออยากย้อน",
                    "ลุย", "ยกเลิก"))
                return;

            var log = new StringBuilder("[HUD] รีสกิล HUD ประจำจอ\n");
            int changed = 0, same = 0, missing = 0;

            // ── prefab ต้องมาก่อนเปิดซีน ────────────────────────────────────
            // `PrefabUtility.LoadPrefabContents` เปิด preview scene ซ้อนขึ้นมาอีกชั้น
            // ทำตอน SampleScene เปิดค้างอยู่แล้วเคยได้ซีนที่ P3R_LevelUp / P3R_Pause /
            // P3R_WinLose ถูกบันทึกกลับเป็นปิด ซึ่ง **ฆ่า singleton ทั้งสามตัว**
            // (Awake ไม่วิ่งบน GameObject ที่ปิดอยู่) และไม่มีอะไรฟ้องจนกว่าจะกดเล่น
            // แยกสองจังหวะออกจากกันแล้วโอกาสนั้นหายไปทั้งหมด
            var heavyEarly = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontHeavy);
            var monoEarly  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMono);
            if (heavyEarly == null || monoEarly == null)
            {
                Debug.LogError("[HUD] โหลดฟอนต์ Sarabun ไม่ได้ — ยกเลิกทั้งรอบ " +
                               "(ถ้าปล่อยผ่านจะได้ซีนที่สีเปลี่ยนแต่ฟอนต์ไม่เปลี่ยน ซึ่งไล่ยากกว่า)");
                return;
            }
            changed += RestyleObjectiveEntry(heavyEarly, monoEarly, log, ref same, ref missing);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = FindRoot(scene, CanvasName);
            if (canvas == null)
            {
                Debug.LogError($"[HUD] หา '{CanvasName}' ใน {ScenePath} ไม่เจอ — ไม่ได้แก้อะไรเลย");
                return;
            }

            var heavy = heavyEarly;
            var mono  = monoEarly;

            foreach (var (path, to, why) in ImageTargets())
            {
                var t = canvas.transform.Find(path);
                if (t == null) { log.AppendLine($"   หาไม่เจอ  {path}"); missing++; continue; }

                string leaf = t.name;
                if (RuntimeOwned.TryGetValue(leaf, out var owner))
                {
                    log.AppendLine($"   ข้าม     {path} — {owner}");
                    continue;
                }

                var img = t.GetComponent<Image>();
                if (img == null) { log.AppendLine($"   ไม่มี Image  {path}"); missing++; continue; }

                if (Same(img.color, to)) { same++; continue; }

                log.AppendLine($"   สี       {path}  {Hex(img.color)} → {Hex(to)}   ({why})");
                Undo.RecordObject(img, "P3R HUD restyle");
                img.color = to;
                EditorUtility.SetDirty(img);
                changed++;
            }

            foreach (var (path, isHeavy) in FontTargets())
            {
                var t = canvas.transform.Find(path);
                if (t == null) { log.AppendLine($"   หาไม่เจอ  {path}"); missing++; continue; }

                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp == null) { log.AppendLine($"   ไม่มี TMP  {path}"); missing++; continue; }

                var want = isHeavy ? heavy : mono;
                if (tmp.font == want) { same++; continue; }

                string from = tmp.font != null ? tmp.font.name : "(ไม่มี)";
                log.AppendLine($"   ฟอนต์    {path}  {from} → {want.name}");
                Undo.RecordObject(tmp, "P3R HUD restyle");
                tmp.font = want;
                EditorUtility.SetDirty(tmp);
                changed++;
            }

            changed += RestyleComponents(canvas, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);


            log.AppendLine();
            log.AppendLine($"   เปลี่ยน {changed} ช่อง · ตรงอยู่แล้ว {same} ช่อง · หาไม่เจอ {missing} ช่อง");
            log.AppendLine("   ยังเหลือที่ต้องกดเล่นจริงถึงจะรู้: 5-second test — แคปจอตอนศัตรูเต็มจอ");
            log.AppendLine("   โชว์ 5 วิ ปิด แล้วถามว่า HP เหลือกี่ % สกิลไหนพร้อม");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// แถว objective อยู่บน prefab — แก้ผ่าน <c>PrefabUtility.LoadPrefabContents</c>
        /// ไม่ใช่แก้ instance ในซีน เพราะแถวจริงถูกสร้างตอนรันจากไฟล์นี้ ไม่ได้อยู่ในซีนเลย
        ///
        /// ไฟล์นี้ **ไม่ได้อยู่ใต้ P3RBuilderKit.SavePrefab** จึงไม่มีตัวกันแฮชคุ้มกัน —
        /// ที่ปลอดภัยได้เพราะแก้เฉพาะช่องสี/ฟอนต์ที่ระบุชื่อไว้ ไม่ได้สร้างใหม่ทั้งไฟล์
        /// </summary>
        private static int RestyleObjectiveEntry(TMP_FontAsset heavy, TMP_FontAsset mono,
                                                 StringBuilder log, ref int same, ref int missing)
        {
            var root = PrefabUtility.LoadPrefabContents(ObjectiveEntryPrefab);
            if (root == null)
            {
                log.AppendLine($"   หาไม่เจอ  {ObjectiveEntryPrefab}");
                missing++;
                return 0;
            }

            int n = 0;
            try
            {
                foreach (var (path, to, why) in PrefabImageTargets())
                {
                    var t = string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);
                    var img = t != null ? t.GetComponent<Image>() : null;
                    if (img == null) { log.AppendLine($"   ไม่มี Image  แถว objective/{path}"); missing++; continue; }
                    if (Same(img.color, to)) { same++; continue; }

                    log.AppendLine($"   สี       แถว objective/{(path == "" ? "(root)" : path)}  " +
                                   $"{Hex(img.color)} → {Hex(to)}   ({why})");
                    img.color = to;
                    n++;
                }

                foreach (var (path, isHeavy) in PrefabFontTargets())
                {
                    var t = root.transform.Find(path);
                    var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
                    if (tmp == null) { log.AppendLine($"   ไม่มี TMP  แถว objective/{path}"); missing++; continue; }

                    var want = isHeavy ? heavy : mono;
                    if (tmp.font == want) { same++; continue; }

                    log.AppendLine($"   ฟอนต์    แถว objective/{path}  " +
                                   $"{(tmp.font != null ? tmp.font.name : "(ไม่มี)")} → {want.name}");
                    tmp.font = want;
                    n++;
                }

                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, ObjectiveEntryPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return n;
        }

        /// <summary>
        /// สีที่อยู่บน **ช่องของ component** ไม่ใช่บน Image ในซีน
        /// พวกนี้คือต้นทางจริงของสีที่ถูกเขียนทับตอนรัน — ทาที่ Image ไปก็ถูกลบทิ้ง
        /// </summary>
        private static int RestyleComponents(GameObject canvas, StringBuilder log)
        {
            int n = 0;

            var hud = canvas.GetComponent<GameHUD>();
            if (hud != null)
            {
                // แบบวาดช่องที่พร้อมใช้เป็นสแลบน้ำเงิน · ช่องที่ติดคูลดาวน์เป็นพื้นการ์ดเข้ม
                // · ช่องที่กำลัง active เป็นทอง — ต่างกันที่ **ความสว่าง** ด้วย ไม่ใช่แค่เฉดสี
                // คนตาบอดสีแดง-เขียวจึงยังแยกออกตอนศัตรูเต็มจอ
                n += SetColor(hud, ref hud.abilityReadyColor,    Primary, "GameHUD.abilityReadyColor",    log);
                n += SetColor(hud, ref hud.abilityCooldownColor, Card,    "GameHUD.abilityCooldownColor", log);
                n += SetColor(hud, ref hud.abilityActiveColor,   Gold,    "GameHUD.abilityActiveColor",   log);
            }
            else log.AppendLine("   หาไม่เจอ  GameHUD บน HUDCanvas");

            var weapons = canvas.GetComponent<WeaponStatHUD>();
            if (weapons != null)
            {
                // ช่องอาวุธ/สเตตัสถูก WeaponStatHUD ทาสีใหม่ทุกรอบ refresh จากช่องพวกนี้
                // ค่าแดงสด/เขียวสดที่เห็นค้างในซีนเป็นแค่ค่าที่เหลือจากตอนวาง ไม่เคยถูกใช้จริง
                n += SetColor(weapons, ref weapons.colorNormal, A(BlueSlot, 0.85f), "WeaponStatHUD.colorNormal", log);
                n += SetColor(weapons, ref weapons.colorSuper,  A(Gold,     0.85f), "WeaponStatHUD.colorSuper",  log);
                n += SetColor(weapons, ref weapons.colorFusion, A(Amber,    0.85f), "WeaponStatHUD.colorFusion", log);
                n += SetColor(weapons, ref weapons.colorStat,   A(Green,    0.85f), "WeaponStatHUD.colorStat",   log);
                n += SetColor(weapons, ref weapons.colorEmpty,  A(Card,     0.70f), "WeaponStatHUD.colorEmpty",  log);
            }
            else log.AppendLine("   หาไม่เจอ  WeaponStatHUD บน HUDCanvas");

            return n;
        }

        private static int SetColor(Object owner, ref Color field, Color to, string label, StringBuilder log)
        {
            if (Same(field, to)) return 0;
            log.AppendLine($"   สี       {label}  {Hex(field)} → {Hex(to)}");
            Undo.RecordObject(owner, "P3R HUD restyle");
            field = to;
            EditorUtility.SetDirty(owner);
            return 1;
        }

        // ── helpers ────────────────────────────────────────────────────────
        private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f
            && Mathf.Abs(a.b - b.b) < 0.002f && Mathf.Abs(a.a - b.a) < 0.002f;

        private static string Hex(Color c)
            => $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}"
             + (c.a > 0.99f ? "" : $" a{c.a:0.00}");

        private static GameObject FindRoot(Scene scene, string name)
            => scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
    }
}
