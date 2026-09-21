using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ช่อง Roll ของท่าบอส — เปลี่ยนจากช่องพิมพ์เปล่าเป็นรายการให้เลือก
    ///
    /// ═══ ปัญหาที่แก้ ═══
    ///
    /// ระบบ roll มีครบทั้ง RollContext / RollTransform / anchorChoices / excludePrevious
    /// แต่ **ไม่มี content ตัวไหนในเกมใช้เลยสักตัว** — BossConfig_01 นิยาม roll ไว้ 5 ตัว
    /// (mirror · spin · quadrant · victim · order) แล้วทุก action asset มี rollName ว่าง
    ///
    /// ไม่ใช่เพราะไม่อยากใช้ แต่เพราะช่องนี้เป็น text field ที่ต้องจำชื่อเอาเอง แล้วพิมพ์ผิด
    /// ก็ไม่มีอาการ: RollContext หา definition ไม่เจอจะถือเป็น Variant optionCount 2
    /// ซึ่งเป็นค่าที่ valid ระบบจึงไม่มีทางรู้ว่าผิด · ท่าที่ควรหมุนก็แค่หยุดหมุนเงียบๆ
    ///
    /// ═══ บอกชนิดมาด้วย ไม่ใช่แค่ชื่อ ═══
    ///
    /// ชื่อ roll อย่างเดียวไม่บอกว่าท่าจะทำอะไร — "spin" อาจเป็น SnapAngle หรือ Variant ก็ได้
    /// รายการจึงเขียนชนิดกับจำนวนตัวเลือกต่อท้าย คนเลือกจะได้รู้ผลทันทีโดยไม่ต้องเปิด config
    ///
    /// ═══ ไม่แก้ค่าที่พิมพ์ผิดให้เอง ═══
    ///
    /// ค่าที่ไม่ตรงยังโชว์อยู่พร้อมป้ายว่าไม่มีนิยาม — เหตุผลเดียวกับ VariantIdDrawer
    /// หน้าที่ของเครื่องมือคือบอกว่าผิด ไม่ใช่ตัดสินใจแทน
    ///
    /// ═══ ครอบได้แค่ทางที่ผ่าน Inspector ═══
    ///
    /// ค่าที่มาจากสคริปต์หรือ merge ยังหลุดได้ · RollContext.Roll จึงยัง warn ตอนรันด้วย
    /// </summary>
    [CustomPropertyDrawer(typeof(RollIdAttribute))]
    public class RollIdDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(ไม่ใช้ roll)";

        private struct RollInfo
        {
            public RollKind kind;
            public int      optionCount;
            public bool     kindConflict;   // นิยามคนละชนิดใน config คนละตัว
        }

        // ── แคช ───────────────────────────────────────────────────────────
        //
        // OnGUI วิ่งหลายครั้งต่อเฟรม · FindAssets ทุกครั้งคือการไล่ทั้งโปรเจกต์
        // สิบกว่ารอบต่อวินาทีเพื่อได้คำตอบเดิม — สแกนวินาทีละครั้งพอ
        private static double _nextScan;
        private static readonly Dictionary<string, RollInfo> _all = new();
        private static int _configCount;

        private static void ScanProject()
        {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 1.0;

            _all.Clear();
            _configCount = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:BossEncounterConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (cfg == null) continue;
                _configCount++;
                Absorb(_all, cfg);
            }
        }

        private static void Absorb(Dictionary<string, RollInfo> into, BossEncounterConfig cfg)
        {
            if (cfg?.rolls == null) return;
            foreach (var def in cfg.rolls)
            {
                if (def == null || string.IsNullOrEmpty(def.rollName)) continue;

                if (into.TryGetValue(def.rollName, out var seen))
                {
                    if (seen.kind != def.kind) seen.kindConflict = true;
                    seen.optionCount = Mathf.Max(seen.optionCount, def.optionCount);
                    into[def.rollName] = seen;
                }
                else
                {
                    into[def.rollName] = new RollInfo
                    {
                        kind = def.kind,
                        optionCount = Mathf.Max(1, def.optionCount),
                    };
                }
            }
        }

        /// <summary>
        /// config ที่เป็นเจ้าของ action นี้ — ถ้าหาเจอ ให้ใช้ roll ของมันตัวเดียว
        ///
        /// ท่าที่เป็น sub-asset ของ config ใช้ roll ของ config นั้นเท่านั้นตอนรัน
        /// เอารายชื่อทั้งโปรเจกต์มาโชว์จะชวนให้เลือกชื่อที่บอสตัวนี้ไม่มี
        /// </summary>
        private static BossEncounterConfig OwningConfig(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target == null) return null;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path)) return null;

            return AssetDatabase.LoadMainAssetAtPath(path) as BossEncounterConfig;
        }

        private static Dictionary<string, RollInfo> TableFor(SerializedProperty property, out bool scoped)
        {
            ScanProject();

            var owner = OwningConfig(property);
            scoped = owner != null;
            if (!scoped) return _all;

            var only = new Dictionary<string, RollInfo>();
            Absorb(only, owner);
            return only;
        }

        private static string Describe(string name, RollInfo info)
        {
            string kind = info.kindConflict ? $"{info.kind}?" : info.kind.ToString();
            // '/' ใน label ของ Popup จะกลายเป็นเมนูซ้อน — แตะแค่ตอนแสดง ค่าจริงไม่เปลี่ยน
            return $"{name.Replace("/", "∕")}  ·  {kind} ×{info.optionCount}";
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;

            var table = TableFor(property, out _);
            if (_configCount == 0) return line + gap + line * 2f;

            string cur = property.stringValue ?? "";
            bool unknown = !string.IsNullOrEmpty(cur) && !table.ContainsKey(cur);
            return unknown ? line + gap + line * 2.5f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;
            var row  = new Rect(position.x, position.y, position.width, line);

            var table = TableFor(property, out bool scoped);

            // ── ไม่มี config ให้ถามเลย ─────────────────────────────────────
            // ปิดช่องไปเลยจะแย่กว่า เพราะคนแก้ทำอะไรไม่ได้ — ปล่อยให้พิมพ์ แต่บอกว่าตรวจให้ไม่ได้
            if (_configCount == 0)
            {
                EditorGUI.PropertyField(row, property, label);
                EditorGUI.HelpBox(new Rect(position.x, position.y + line + gap, position.width, line * 2f),
                    "ไม่พบ BossEncounterConfig ในโปรเจกต์ — ยังตรวจชื่อ roll ให้ไม่ได้",
                    MessageType.None);
                return;
            }

            var names = table.Keys.OrderBy(n => n).ToList();
            string cur = property.stringValue ?? "";
            bool unknown = !string.IsNullOrEmpty(cur) && !table.ContainsKey(cur);

            var shown = new List<string> { NoneLabel };
            shown.AddRange(names.Select(n => Describe(n, table[n])));
            if (unknown) shown.Add(cur.Replace("/", "∕") + "  (ไม่มีนิยาม)");

            int idx = string.IsNullOrEmpty(cur) ? 0
                    : unknown ? shown.Count - 1
                    : names.IndexOf(cur) + 1;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(row, label.text, idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx)
            {
                // ช่องสุดท้ายตอนค่าผิดคือค่าเดิมของมันเอง — เลือกแล้วไม่เปลี่ยนอะไร
                property.stringValue = picked == 0 ? ""
                                     : picked <= names.Count ? names[picked - 1]
                                     : cur;
            }

            if (!unknown) return;

            string where = scoped
                ? "Roll Definitions ของ config ตัวนี้"
                : "Roll Definitions ของ config ตัวไหนเลยในโปรเจกต์";
            EditorGUI.HelpBox(new Rect(position.x, position.y + line + gap, position.width, line * 2.5f),
                $"'{cur}' ไม่มีใน {where}\n" +
                "ตอนรันจะถูกมองเป็น Variant 2 ตัวเลือก — roll เชิงพื้นที่ (SnapAngle / Mirror / Anchor) จะไม่ทำงาน",
                MessageType.Warning);
        }
    }
}
