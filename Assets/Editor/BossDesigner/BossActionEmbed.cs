using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// "แปลงทุกคลิปเป็นสำเนาของบอสนี้" — action ที่ config ชี้ออกไปนอกไฟล์ถูกก๊อปเข้ามาเป็น sub-asset
    ///
    /// ═══ ทำไม ═══
    ///
    /// คลิปรุ่นแรก (Boss poc 1/AoE_Boss_*.asset) ชี้ไฟล์แยกตรงๆ · แก้ในการ์ดแก้ด่วนแล้วไฟล์นั้นเปลี่ยน
    /// ทุกบอสที่ใช้ไฟล์เดียวกันเปลี่ยนตามแบบเงียบๆ · ท่าที่ได้จากแผงท่าสำเร็จรูปเป็นสำเนาอยู่แล้ว
    /// แปลงของเก่าให้เป็นแบบเดียวกัน คนใช้จะได้มีกติกาเดียว: **ท่าในบอสเป็นของบอสนั้น**
    ///
    /// ก๊อปครั้งเดียวต่อ action (ที่อ้างซ้ำหลายคลิปชี้ตัวก๊อปเดียวกัน — การแชร์ภายในบอสยังอยู่)
    /// แล้วไล่ต่อในตัวก๊อป (ComboAction / RandomAttackAction / timeline ซ้อน) จนหมด
    /// ไฟล์ต้นทางไม่ถูกลบ — อาจมีบอสอื่นใช้อยู่
    /// </summary>
    public static class BossActionEmbed
    {
        /// <summary>จำนวน action นอกไฟล์ที่ config นี้ชี้ตรงๆ (ไม่ไล่ลึก) — ใช้โชว์ปุ่มบน toolbar</summary>
        public static int CountExternal(BossEncounterConfig cfg)
        {
            if (cfg == null) return 0;
            string path = AssetDatabase.GetAssetPath(cfg);
            var found = new HashSet<Object>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj == null) continue;
                var it = new SerializedObject(obj).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference &&
                        it.objectReferenceValue is BossAction a && AssetDatabase.GetAssetPath(a) != path)
                        found.Add(a);
            }
            return found.Count;
        }

        /// <returns>จำนวน action ที่ก๊อปเข้ามา</returns>
        public static int EmbedExternalActions(BossEncounterConfig cfg, StringBuilder log = null, bool undo = false)
        {
            if (cfg == null) return 0;
            string path = AssetDatabase.GetAssetPath(cfg);
            var copies = new Dictionary<Object, Object>();
            var queue = new Queue<Object>(AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != null));

            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                var so = new SerializedObject(obj);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue is not BossAction a) continue;
                    if (AssetDatabase.GetAssetPath(a) == path) continue;   // อยู่ในไฟล์นี้แล้ว

                    if (!copies.TryGetValue(a, out var copy))
                    {
                        copy = Object.Instantiate(a);
                        copy.name = a.name;
                        AssetDatabase.AddObjectToAsset(copy, cfg);
                        if (undo) Undo.RegisterCreatedObjectUndo(copy, "Embed Boss Actions");
                        copies[a] = copy;
                        queue.Enqueue(copy);
                        log?.AppendLine($"  ก๊อปเข้า: {a.name}  ← {AssetDatabase.GetAssetPath(a)}");
                    }
                    if (undo && !changed) Undo.RecordObject(obj, "Embed Boss Actions");
                    it.objectReferenceValue = copy;
                    changed = true;
                }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(obj);
                }
            }

            if (copies.Count > 0) AssetDatabase.SaveAssets();
            return copies.Count;
        }

        [MenuItem("Tools/Clone Swarm/Boss/แปลงทุกคลิปเป็นสำเนาของบอส (ทุก config)")]
        public static void EmbedAllConfigs()
        {
            var sb = new StringBuilder();
            int total = 0;
            foreach (var g in AssetDatabase.FindAssets("t:BossEncounterConfig"))
            {
                var cfg = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(AssetDatabase.GUIDToAssetPath(g));
                if (cfg == null) continue;
                var one = new StringBuilder();
                int n = EmbedExternalActions(cfg, one);
                total += n;
                sb.AppendLine($"{cfg.name}: {n} ท่า");
                sb.Append(one);
            }
            sb.Insert(0, $"[BossActionEmbed] ก๊อปเข้า config รวม {total} ท่า\n");
            Debug.Log(sb.ToString());
        }

        /// <summary>batchmode: -executeMethod CloneSwarm.EditorTools.BossActionEmbed.RunBatch</summary>
        public static void RunBatch() => EmbedAllConfigs();
    }
}
