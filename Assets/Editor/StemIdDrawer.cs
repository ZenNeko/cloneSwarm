using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ช่องชื่อ stem ใน MusicProfile — dropdown จากชั้นของ track ที่ mix นั้นอ้างถึง
    ///
    /// ═══ track ไหน ═══
    ///
    /// mainBossPhases อ้าง stem ของ **ธีมบอส** (mainBossTrack) · ช่องอื่นอ้าง stem ของ track หลัก
    /// cardPickOverrides ใช้ได้กับทั้งสอง track (จอเลือกการ์ดเปิดกลางไฟต์บอสได้) → รวมชื่อจากทั้งคู่
    ///
    /// ═══ ไม่แก้ค่าที่ผิดให้เอง ═══
    ///
    /// ชื่อที่ไม่มีใน track ยังโชว์อยู่พร้อมป้าย — แบบเดียวกับ RollIdDrawer · stem ถูกลบ/เปลี่ยนชื่อ
    /// แล้ว mix ที่อ้างถึงควรถูกเห็น ไม่ใช่เด้งไปเป็นชั้นแรกเงียบๆ
    /// </summary>
    [CustomPropertyDrawer(typeof(StemIdAttribute))]
    public class StemIdDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(ไม่ระบุ)";

        private static List<string> NamesFor(SerializedProperty property, out string source)
        {
            var names = new List<string>();
            source = null;

            var profile = property.serializedObject.targetObject as MusicProfile;
            if (profile == null) return names;

            string path = property.propertyPath;
            bool bossOnly = path.StartsWith("mainBossPhases");
            bool both     = path.StartsWith("cardPickOverrides");

            if (!bossOnly) Add(names, profile.track);
            if (bossOnly || both) Add(names, profile.mainBossTrack);

            source = bossOnly ? "mainBossTrack" : both ? "track / mainBossTrack" : "track";
            return names;
        }

        private static void Add(List<string> into, LayeredTrack t)
        {
            if (t?.stems == null) return;
            foreach (var s in t.stems)
                if (!string.IsNullOrEmpty(s.name) && !into.Contains(s.name)) into.Add(s.name);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;

            var names = NamesFor(property, out _);
            string cur = property.stringValue ?? "";
            bool warn = names.Count == 0 || (!string.IsNullOrEmpty(cur) && !names.Contains(cur));
            return warn ? line + gap + line * 2f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float gap  = EditorGUIUtility.standardVerticalSpacing;
            var row  = new Rect(position.x, position.y, position.width, line);
            var help = new Rect(position.x, position.y + line + gap, position.width, line * 2f);

            var names = NamesFor(property, out string source);

            // ยังไม่ได้ใส่ track — ปล่อยให้พิมพ์ แต่บอกว่าตรวจให้ไม่ได้
            if (names.Count == 0)
            {
                EditorGUI.PropertyField(row, property, label);
                EditorGUI.HelpBox(help, $"ยังไม่มี stem ใน {source ?? "track"} — ใส่ LayeredTrack ก่อนแล้วจะเลือกจากรายการได้",
                    MessageType.None);
                return;
            }

            string cur = property.stringValue ?? "";
            bool unknown = !string.IsNullOrEmpty(cur) && !names.Contains(cur);

            var shown = new List<string> { NoneLabel };
            foreach (var n in names) shown.Add(n.Replace("/", "∕"));   // '/' ใน Popup = เมนูซ้อน
            if (unknown) shown.Add(cur.Replace("/", "∕") + "  (ไม่มีใน track)");

            int idx = string.IsNullOrEmpty(cur) ? 0
                    : unknown ? shown.Count - 1
                    : names.IndexOf(cur) + 1;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(row, label.text, idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx)
            {
                property.stringValue = picked == 0 ? ""
                                     : picked <= names.Count ? names[picked - 1]
                                     : cur;   // ช่องท้ายตอนค่าผิดคือค่าเดิมของมันเอง
            }

            if (unknown)
                EditorGUI.HelpBox(help, $"'{cur}' ไม่มีใน {source} — ตอนรันชั้นนี้จะถูกข้าม", MessageType.Warning);
        }
    }
}
