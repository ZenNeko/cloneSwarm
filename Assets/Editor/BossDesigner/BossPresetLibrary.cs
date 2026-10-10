using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างชุดท่าสำเร็จรูปเริ่มต้นใต้ <see cref="BossPalette.PresetsDir"/> — รันซ้ำได้ ไฟล์ที่มีแล้วไม่แตะ
    ///
    /// ค่าในแต่ละท่าตั้งให้ "อ่านออกทันทีว่าเป็นท่าอะไร" บนสนามขนาดมาตรฐาน (รัศมี ~20m) ไม่ใช่ค่าบาลานซ์
    /// สุดท้าย — ใช้แล้วได้สำเนาในบอส จูนในการ์ดแก้ด่วนต่อ
    ///
    /// ตั้งค่าผ่าน SerializedObject ตามชื่อฟิลด์ (ไม่ใช่ assign ตรง) — ฟิลด์ไหนเปลี่ยนชื่อไปจะ log เตือน
    /// แทนที่จะคอมไพล์ไม่ผ่านทั้งเครื่องมือ
    ///
    /// batchmode:  -executeMethod CloneSwarm.EditorTools.BossPresetLibrary.RunBatch
    /// </summary>
    public static class BossPresetLibrary
    {
        struct Spec
        {
            public string folder, name;
            public System.Type type;
            public (string field, object value)[] values;
        }

        static readonly Spec[] Specs =
        {
            // ── 1 หลบออก ─────────────────────────────────────────────
            S("1 หลบออก", "วงใต้ทุกคน", typeof(CircleAoEAction),
              ("targetingMode", "AllPlayers"), ("radius", 4f), ("warningDuration", 2.5f), ("damage", 30f)),
            S("1 หลบออก", "วงไล่ตามคนใกล้สุด", typeof(CircleAoEAction),
              ("targetingMode", "NearestPlayer"), ("isChasing", true), ("radius", 3f), ("warningDuration", 3f), ("damage", 25f)),
            S("1 หลบออก", "วงใหญ่รอบบอส", typeof(CircleAoEAction),
              ("targetingMode", "BossPosition"), ("radius", 9f), ("warningDuration", 3f), ("damage", 45f)),
            S("1 หลบออก", "กากบาทกลางสนาม", typeof(CrossAoEAction),
              ("targetingMode", "ArenaAnchor"), ("arenaAnchor", "Center"), ("lineLength", 40f), ("lineWidth", 5f),
              ("warningDuration", 3f), ("damage", 40f)),
            S("1 หลบออก", "เส้นใต้คนใกล้สุด", typeof(LineAoEAction),
              ("targetingMode", "NearestPlayer"), ("lineLength", 30f), ("lineWidth", 4f), ("warningDuration", 2.5f), ("damage", 35f)),
            S("1 หลบออก", "เส้นกวาดรอบบอส", typeof(LineAoEAction),
              ("targetingMode", "BossPosition"), ("lineLength", 22f), ("lineWidth", 3f), ("sweepDegreesPerSecond", 45f),
              ("warningDuration", 3f), ("damage", 35f)),
            S("1 หลบออก", "พัดหน้าบอส", typeof(ConeAoEAction),
              ("targetingMode", "BossPosition"), ("radius", 15f), ("coneAngle", 90f), ("warningDuration", 2.5f), ("damage", 40f)),

            // ── 2 เข้าใกล้ ───────────────────────────────────────────
            S("2 เข้าใกล้", "โดนัทรอบบอส", typeof(DonutAoEAction),
              ("targetingMode", "BossPosition"), ("radius", 20f), ("innerRadius", 5f), ("warningDuration", 3f), ("damage", 40f)),

            // ── 3 ยืนรวม ─────────────────────────────────────────────
            S("3 ยืนรวม", "จุดรวมดาเมจ (stack)", typeof(CircleAoEAction),
              ("targetingMode", "RandomPlayer"), ("isStackMarker", true), ("radius", 4f), ("warningDuration", 4f), ("damage", 60f)),

            // ── 4 แยกกัน ─────────────────────────────────────────────
            S("4 แยกกัน", "วงใหญ่ใต้ทุกคน (spread)", typeof(CircleAoEAction),
              ("targetingMode", "AllPlayers"), ("radius", 6f), ("warningDuration", 4f), ("damage", 35f)),

            // ── 5 หันหลัง ────────────────────────────────────────────
            S("5 หันหลัง", "จ้องตา (gaze)", typeof(CircleAoEAction),
              ("targetingMode", "BossPosition"), ("isGaze", true), ("radius", 30f), ("warningDuration", 3f), ("damage", 50f)),

            // ── 6 ต้องเดิน ───────────────────────────────────────────
            S("6 ต้องเดิน", "ห้ามหยุดเดิน", typeof(KeepMovingAction)),

            // ── 7 โซ่ ────────────────────────────────────────────────
            S("7 โซ่", "โซ่ต้องวิ่งออก (Far)",     typeof(TetherAction), ("mode", "Far")),
            S("7 โซ่", "โซ่ต้องเข้าใกล้ (Close)",   typeof(TetherAction), ("mode", "Close")),
            S("7 โซ่", "โซ่ห้ามออกไกล (Leash)",    typeof(TetherAction), ("mode", "Leash")),
            S("7 โซ่", "โซ่ส่งต่อได้ (Transferable)", typeof(TetherAction), ("mode", "Transferable")),

            // ── 8 ท่าชุด ─────────────────────────────────────────────
            S("8 ท่าชุด", "Limit Cut (ตามลำดับเลข)", typeof(LimitCutAction)),
            S("8 ท่าชุด", "จับคู่สี", typeof(ColorMatchAoEAction)),
        };

        static Spec S(string folder, string name, System.Type type, params (string, object)[] values)
            => new Spec { folder = folder, name = name, type = type, values = values };

        [MenuItem("Tools/Clone Swarm/Boss/Create Default Presets")]
        public static void RunFromMenu()
        {
            var log = CreateDefaults();
            EditorUtility.DisplayDialog("Boss Presets", string.Join("\n", log), "OK");
        }

        public static void RunBatch()
        {
            try
            {
                foreach (var line in CreateDefaults()) Debug.Log("[BossPresetLibrary] " + line);
                Debug.Log("[BossPresetLibrary] DONE");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[BossPresetLibrary] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static List<string> CreateDefaults()
        {
            var log = new List<string>();
            foreach (var spec in Specs)
            {
                string dir  = $"{BossPalette.PresetsDir}/{spec.folder}";
                string path = $"{dir}/{spec.name}.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) { log.Add($"มีแล้ว {path}"); continue; }

                BossPalette.CreateFolderDeep(dir);
                var action = ScriptableObject.CreateInstance(spec.type) as BossAction;
                AssetDatabase.CreateAsset(action, path);

                var so = new SerializedObject(action);
                foreach (var (field, value) in spec.values ?? new (string, object)[0])
                {
                    var p = so.FindProperty(field);
                    if (p == null) { log.Add($"⚠ {spec.name}: ไม่มีฟิลด์ '{field}' ใน {spec.type.Name}"); continue; }
                    switch (value)
                    {
                        case float f: p.floatValue = f; break;
                        case bool b:  p.boolValue  = b; break;
                        case string s when p.propertyType == SerializedPropertyType.Enum:
                            int idx = System.Array.IndexOf(p.enumNames, s);
                            if (idx < 0) log.Add($"⚠ {spec.name}: '{s}' ไม่มีใน {field}");
                            else p.enumValueIndex = idx;
                            break;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                log.Add($"สร้าง {path}");
            }
            AssetDatabase.SaveAssets();
            return log;
        }
    }
}
