using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ช่อง variant ของนัดหมาย — เปลี่ยนจากช่องพิมพ์เปล่าเป็นรายการให้เลือก
    ///
    /// ═══ ปัญหาที่แก้ ═══
    ///
    /// ช่องเดิมเป็น text field · พิมพ์ "augmnet" แล้วเกม **ยังเล่นได้ปกติ** —
    /// ObjectiveManager หาแบบไม่เจอ บ่นหนึ่งบรรทัดใน Console แล้วสุ่มแทน
    /// จังหวะที่ออกแบบไว้หายไปโดยไม่มีอาการอื่น และคนตั้งตารางไม่มีทางรู้
    ///
    /// ═══ ไม่แก้ค่าที่พิมพ์ผิดให้เอง ═══
    ///
    /// ค่าที่ไม่ตรงกับของจริงยังโชว์อยู่ในรายการ พร้อมป้ายว่าไม่มีอยู่จริง ·
    /// การเด้งไปเป็นค่าแรกให้เองจะกลืนความตั้งใจของคนเขียนทิ้ง แล้วเขาจะไม่รู้ว่า
    /// เคยตั้งอะไรไว้ · หน้าที่ของเครื่องมือคือบอกว่าผิด ไม่ใช่ตัดสินใจแทน
    ///
    /// ═══ ครอบได้แค่ทางที่ผ่าน Inspector ═══
    ///
    /// ค่าที่มาจากสคริปต์ จากการ merge หรือจากซีนเก่ายังหลุดได้อยู่ ·
    /// สโมกเทสต์ (CheckTimelineCues) จึงยังต้องอยู่ ไม่ใช่ของซ้ำซ้อน
    /// </summary>
    [CustomPropertyDrawer(typeof(VariantIdAttribute))]
    public class VariantIdDrawer : PropertyDrawer
    {
        private const string RandomLabel = "(สุ่มตามน้ำหนัก)";

        // ── แคชรายชื่อ ────────────────────────────────────────────────────
        //
        // OnGUI วิ่งหลายครั้งต่อเฟรม (Layout + Repaint + ทุก event) · การ
        // FindAnyObjectByType ทุกครั้งคือการไล่ทั้งซีนสิบกว่ารอบต่อวินาที
        // เพื่อได้คำตอบเดิม — สแกนวินาทีละครั้งพอ
        private static double   _nextScan;
        private static string[] _zoneIds = new string[0];
        private static string[] _bossIds = new string[0];
        private static bool     _managersInScene;

        private static void Scan()
        {
            if (EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + 1.0;

            var om = Object.FindAnyObjectByType<ObjectiveManager>(FindObjectsInactive.Include);
            var bm = Object.FindAnyObjectByType<BossManager>(FindObjectsInactive.Include);
            _managersInScene = om != null || bm != null;

            _zoneIds = om != null && om.zoneVariants != null
                     ? om.zoneVariants.Where(v => v.prefab != null && !string.IsNullOrEmpty(v.id))
                                      .Select(v => v.id).Distinct().ToArray()
                     : new string[0];

            _bossIds = bm != null && bm.miniBossPrefabs != null
                     ? bm.miniBossPrefabs.Where(p => p != null)
                                         .Select(p => p.name).Distinct().ToArray()
                     : new string[0];
        }

        /// <summary>
        /// ชนิดของนัดหมายเดียวกัน — โซนกับมินิบอสคนละรายชื่อ
        /// path ลงท้ายด้วย "variant" เสมอ จึงสลับท้ายเป็น "kind" ได้ตรงๆ
        /// ใช้ได้ทั้งบน component ในซีนและบน MapData ที่ซ้อนลึกกว่า
        /// </summary>
        private static SerializedProperty KindSibling(SerializedProperty p)
        {
            string path = p.propertyPath;
            if (!path.EndsWith("variant")) return null;
            return p.serializedObject.FindProperty(path.Substring(0, path.Length - 7) + "kind");
        }

        private static bool IsZone(SerializedProperty property)
        {
            var kind = KindSibling(property);
            return kind == null || kind.enumValueIndex == (int)TimelineCueKind.ZoneObjective;
        }

        private static string[] IdsFor(SerializedProperty property)
            => IsZone(property) ? _zoneIds : _bossIds;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Scan();
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;

            if (!_managersInScene) return line + gap + line * 2f;

            string cur = property.stringValue ?? "";
            bool unknown = !string.IsNullOrEmpty(cur) && !IdsFor(property).Contains(cur);
            return unknown ? line + gap + line * 2f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Scan();
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;
            var row  = new Rect(position.x, position.y, position.width, line);
            var note = new Rect(position.x, position.y + line + gap, position.width, line * 2f);

            // ── ไม่มีแมเนเจอร์ให้ถาม ───────────────────────────────────────
            //
            // แก้ MapData ตอนเปิดซีนเมนูอยู่ก็เจอกรณีนี้ · ปิดช่องไปเลยจะแย่กว่า
            // เพราะคนแก้ทำอะไรไม่ได้เลย — ปล่อยให้พิมพ์ได้ แต่บอกว่าตรวจให้ไม่ได้
            if (!_managersInScene)
            {
                EditorGUI.PropertyField(row, property, label);
                EditorGUI.HelpBox(note,
                    "ยังตรวจชื่อให้ไม่ได้ — เปิด SampleScene แล้วจะเลือกจากรายการได้",
                    MessageType.None);
                return;
            }

            var ids = IdsFor(property);
            string cur = property.stringValue ?? "";
            bool unknown = !string.IsNullOrEmpty(cur) && !ids.Contains(cur);

            var shown = new List<string> { RandomLabel };
            shown.AddRange(ids.Select(Safe));
            if (unknown) shown.Add(Safe(cur) + "  (ไม่มีอยู่จริง)");

            int idx = string.IsNullOrEmpty(cur) ? 0
                    : unknown ? shown.Count - 1
                    : System.Array.IndexOf(ids, cur) + 1;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(row, label.text, idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx)
            {
                // ช่องสุดท้ายตอนค่าผิดคือค่าเดิมของมันเอง — เลือกแล้วไม่เปลี่ยนอะไร
                property.stringValue = picked == 0 ? ""
                                     : picked <= ids.Length ? ids[picked - 1]
                                     : cur;
            }

            if (!unknown) return;

            string where = IsZone(property) ? "ObjectiveManager.zoneVariants"
                                            : "BossManager.miniBossPrefabs";
            EditorGUI.HelpBox(note,
                $"'{cur}' ไม่มีใน {where} — นัดหมายนี้จะสุ่มแทน ไม่ใช่แบบที่ตั้งไว้",
                MessageType.Warning);
        }

        // '/' ใน label ของ Popup จะกลายเป็นเมนูซ้อน — แตะแค่ตอนแสดง ค่าจริงไม่เปลี่ยน
        private static string Safe(string s) => s.Replace("/", "∕");
    }
}
