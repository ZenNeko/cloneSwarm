using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// หาช่องข้อความที่ **ถ่างระยะตัวอักษรไว้ แล้วถูกสคริปต์เขียนทับตอนรัน**
    /// เมนู: Tools > Clone Swarm > Audit Thai Risk
    ///
    /// `P3RText` กันภาษาไทยได้เฉพาะตอน **สร้างซีน** — มันอ่านข้อความที่มีอยู่ ณ ตอนนั้น
    /// แล้วตัดสินว่าจะถ่างหรือไม่ถ่าง · ช่องที่ตอนสร้างเป็นภาษาอังกฤษจะได้ระยะถ่างติดไป
    /// แล้วพอตอนรันมีคนเขียนชื่อที่แปลแล้วลงไป ระยะถ่างยังค้างอยู่
    ///
    /// ตัวนี้ไม่รู้ว่า field ไหนรับข้อความที่แปลแล้ว — มันแค่ชี้ว่า "ช่องนี้ถ่างระยะอยู่
    /// และมีสคริปต์ถือมันไว้" ซึ่งเป็นเงื่อนไขที่จำเป็นของอาการนี้ · คนอ่านตัดสินเอง
    /// ว่าช่องไหนรับข้อมูลที่แปลได้บ้าง
    ///
    /// ช่องที่ไม่มีสคริปต์ถือ = ข้อความค้างตามที่ builder ใส่ ไม่มีทางเปลี่ยน ไม่ต้องสน
    /// </summary>
    public static class P3RThaiRiskAudit
    {
        private static readonly string[] Scenes =
        {
            "Assets/GameScenes/MenuScene.unity",
            "Assets/GameScenes/SampleScene.unity",
        };

        /// <summary>
        /// ใส่ <see cref="CloneSwarm.UI.P3R.P3RThaiTracking"/> ให้ทุกช่องที่ถ่างระยะอยู่
        /// และมีสคริปต์ของโปรเจกต์ถือไว้ — ซึ่งคือชุดเดียวกับที่ <see cref="Run"/> รายงาน
        ///
        /// ช่องที่ไม่มีสคริปต์ถือ ข้อความไม่มีทางเปลี่ยน จึงไม่ต้องเฝ้า
        /// </summary>
        [MenuItem("Tools/Clone Swarm/Fix Thai Risk (ใส่ตัวเฝ้าระยะถ่าง)")]
        public static void Fix()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int total = 0;
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int added = 0;

                foreach (var tmp in CollectRiskyLabels(scene))
                {
                    if (tmp.GetComponent<CloneSwarm.UI.P3R.P3RThaiTracking>() != null) continue;
                    var guard = tmp.gameObject.AddComponent<CloneSwarm.UI.P3R.P3RThaiTracking>();
                    guard.latinSpacing = tmp.characterSpacing;
                    added++;
                }

                if (added > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"[ThaiRisk] {System.IO.Path.GetFileNameWithoutExtension(path)}: " +
                          $"ใส่ตัวเฝ้า {added} ช่อง");
                total += added;
            }
            Debug.Log($"[ThaiRisk] รวม {total} ช่อง — ตอนนี้ระยะถ่างจะถูกคิดใหม่ทุกครั้งที่ข้อความเปลี่ยน");
        }

        [MenuItem("Tools/Clone Swarm/Audit Thai Risk")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var sb = new StringBuilder("[ThaiRisk] ช่องที่ถ่างระยะอยู่ และมีสคริปต์ถือไว้\n");

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine($"── {System.IO.Path.GetFileNameWithoutExtension(path)}");

                var rows = Scan(scene)
                    .Select(h => $"      {h.owner.GetType().Name}.{h.field,-22} " +
                                 $"spacing={h.label.characterSpacing,-7:0.##} " +
                                 (h.label.GetComponent<CloneSwarm.UI.P3R.P3RThaiTracking>() != null
                                      ? "[มีตัวเฝ้า] " : "[ยังไม่มีตัวเฝ้า] ") +
                                 $"\"{Trim(h.label.text)}\"")
                    .Distinct().OrderBy(x => x).ToList();

                if (rows.Count == 0) sb.AppendLine("      ไม่มี");
                else foreach (var r in rows) sb.AppendLine(r);
            }

            sb.AppendLine();
            sb.AppendLine("ช่องไหนรับ DisplayName / Description / ชื่อแมพ ที่แปลได้ ต้องเป็น spacing 0");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ช่องที่ **ถ่างระยะอยู่** และ **มีสคริปต์ของโปรเจกต์ถือไว้**
        /// สองเงื่อนไขนี้คือสิ่งที่ทำให้อาการเกิดได้ — ถ่างแต่ไม่มีใครถือ ข้อความก็ไม่มีทางเปลี่ยน
        /// </summary>
        private static List<(MonoBehaviour owner, string field, TMP_Text label)> Scan(Scene scene)
        {
            var hits = new List<(MonoBehaviour, string, TMP_Text)>();

            foreach (var mb in scene.GetRootGameObjects()
                                    .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                                    .Where(m => m != null))
            {
                // ข้าม component ของ Unity/TMP เอง — สนใจเฉพาะสคริปต์ที่เราเขียน
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") ||
                    ns.StartsWith("Unity.")      || ns.StartsWith("PhEngine")) continue;

                var it = new SerializedObject(mb).GetIterator();
                bool enter = true;
                while (it.NextVisible(enter))
                {
                    enter = false;
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue is not TMP_Text tmp) continue;
                    if (Mathf.Abs(tmp.characterSpacing) < 0.01f) continue;
                    hits.Add((mb, it.name, tmp));
                }
            }
            return hits;
        }

        private static IEnumerable<TMP_Text> CollectRiskyLabels(Scene scene)
            => Scan(scene).Select(h => h.label).Distinct();

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= 28 ? s : s.Substring(0, 28) + "…";
        }
    }
}
