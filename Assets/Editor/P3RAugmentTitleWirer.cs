using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้าง <c>Augment_Title</c> ในซีนเกม แล้วต่อเข้า <see cref="LevelUpUI"/>
    /// เมนู: Tools > Clone Swarm > Wire Augment Title
    ///
    /// ═══ ก๊อปของที่จัดไว้แล้ว ไม่สร้างใหม่ ═══
    ///
    /// `P3RLevelUpSceneBuilder` สร้างหัวเรื่องได้ก็จริง แต่มันสร้างจากพิกัดในโค้ด
    /// ซึ่ง **ไม่ตรงกับที่จัดด้วยมือในซีนแล้ว** (ขนาด · มุมเอียง · ตำแหน่ง ถูกขยับไปหมด)
    /// สร้างใหม่จะได้ก้อนที่หน้าตาไม่เข้าพวกกับของข้างๆ
    ///
    /// ก๊อปก้อนเดิมแล้วเปลี่ยนคำ จึงได้ทรงเดียวกันเป๊ะโดยไม่ต้องรู้ว่าเขาจัดไว้ยังไง
    /// `Instantiate` ผูก reference ภายในก้อนที่ก๊อปให้เอง — `P3RLayeredText` ของสำเนา
    /// จึงชี้ลูกของตัวเอง ไม่ใช่ชี้ข้ามไปที่ใบต้นฉบับ
    ///
    /// ═══ ทำไมต้องมิเรอร์ข้อความเอง ═══
    ///
    /// `P3RLayeredText` ทำงานใน `LateUpdate` ซึ่งไม่วิ่งใน edit mode — ตั้งคำที่ใบ
    /// `source` ใบเดียวแล้วอีกสองใบจะค้างคำเก่าจนกว่าจะกด Play · ตั้งให้ครบทุกใบตรงนี้
    ///
    /// รันซ้ำได้ — มีอยู่แล้วก็แค่เช็คสายให้
    /// </summary>
    public static class P3RAugmentTitleWirer
    {
        private const string ScenePath   = "Assets/GameScenes/SampleScene.unity";
        private const string SourceName  = "TitleGroup";
        private const string AugmentName = "Augment_Title";
        private const string AugmentText = "AUGMENT";

        [MenuItem("Tools/Clone Swarm/Wire Augment Title")]
        public static void Wire()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder($"[หัวเรื่อง augment] {ScenePath}\n");
            int n = 0;

            var ui = All<LevelUpUI>(scene).FirstOrDefault();
            if (ui == null) { Debug.LogError("[หัวเรื่อง augment] หา LevelUpUI ไม่เจอ"); return; }

            // หาก้อนเดิม — ใต้จอ Level Up เท่านั้น ชื่อ "TitleGroup" ธรรมดาเกินกว่าจะ
            // เชื่อได้ว่ามีใบเดียวทั้งซีน
            var source = ui.GetComponentsInChildren<Transform>(true)
                           .FirstOrDefault(t => t.name == SourceName);
            if (source == null)
            {
                Debug.LogError($"[หัวเรื่อง augment] หา '{SourceName}' ใต้ LevelUpUI ไม่เจอ");
                return;
            }

            if (ui.titleDefault != source.gameObject)
            {
                ui.titleDefault = source.gameObject;
                EditorUtility.SetDirty(ui);
                n++;
                log.AppendLine($"  titleDefault → {SourceName}");
            }

            var existing = ui.GetComponentsInChildren<Transform>(true)
                             .FirstOrDefault(t => t.name == AugmentName);

            if (existing == null)
            {
                var clone = Object.Instantiate(source.gameObject, source.parent);
                clone.name = AugmentName;
                clone.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);

                // ทรงเหมือนต้นฉบับทุกอย่าง — Instantiate ไม่ยกค่าของ RectTransform
                // มาให้ครบเมื่อ parent ต่างกัน แต่ที่นี่ parent เดียวกัน จึงตรงอยู่แล้ว
                foreach (var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.text = AugmentText;

                clone.SetActive(false);   // LevelUpUI เปิดเองตอนแจก augment
                existing = clone.transform;
                n++;
                log.AppendLine($"  ก๊อป '{SourceName}' → '{AugmentName}' (คำว่า {AugmentText})");
            }
            else log.AppendLine($"  '{AugmentName}' มีอยู่แล้ว");

            if (ui.titleAugment != existing.gameObject)
            {
                ui.titleAugment = existing.gameObject;
                EditorUtility.SetDirty(ui);
                n++;
                log.AppendLine($"  titleAugment → {AugmentName}");
            }

            if (n == 0) { Debug.Log(log.AppendLine("  ต่อครบแล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึกแล้ว ({n} รายการ)");
            Debug.Log(log.ToString());
        }

        private static T[] All<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<T>(true))
                    .ToArray();
    }
}
