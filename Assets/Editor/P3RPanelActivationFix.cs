using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// เปิด panel ที่ถือ singleton กลับมาให้ active ในซีนเกม
    /// เมนู: Tools > Clone Swarm > Fix Panel Activation
    ///
    /// ═══ ทำไมต้องมีเครื่องมือสำหรับเรื่องแค่นี้ ═══
    ///
    /// `LevelUpUI` · `WinLoseUI` · `PauseMenuUI` ตั้ง `Instance` ของตัวเองใน `Awake()`
    /// และ **`Awake` ไม่วิ่งบน GameObject ที่ปิดอยู่** · panel ที่ถูกเซฟมาในสภาพปิด
    /// จึงทำให้ `Instance` เป็น null ตลอดเกม — จอเลเวลอัปไม่เด้ง จอจบเกมไม่ขึ้น
    /// โดยไม่มี error สักบรรทัด เพราะทุกที่เรียกผ่าน `?.` กันหมด
    ///
    /// panel พวกนี้ซ่อนตัวเองใน `Awake`/`Start` อยู่แล้ว — **active ไม่ได้แปลว่ามองเห็น**
    ///
    /// เรื่องนี้กัดมาแล้วสองรอบจากคนละต้นเหตุ: รอบแรกสโมกเทสต์เขียนสถานะ play mode
    /// ทับซีนตอนออกจาก play · รอบสองคนเปิด Editor แล้วซีนถูกเซฟทั้งที่ panel ปิดอยู่
    /// ต้นเหตุคนละอย่างแต่ปลายทางเหมือนกัน จึงคุ้มที่จะมีปุ่มซ่อมแยก
    ///
    /// สโมกเทสต์เป็นตัว **จับ** (`LevelUpUI.Instance ไม่เป็น null`) ตัวนี้เป็นตัว **ซ่อม**
    /// รันซ้ำได้ ไม่มีอะไรต้องทำก็บอกว่าไม่มี
    /// </summary>
    public static class P3RPanelActivationFix
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";

        /// <summary>
        /// panel ที่ต้อง active เสมอ — เพิ่มชื่อที่นี่เมื่อมีจอใหม่ที่ตั้ง singleton ใน Awake
        ///
        /// public เพราะ <see cref="P3RScreenMigrator"/> ต้องอ่านด้วย — มันปิดแผงที่ย้ายเข้ามา
        /// ทุกครั้งโดยตั้งใจ (กันชนกับแผงเดิมก่อนต่อสาย) ซึ่งถูกสำหรับจอเมนู
        /// แต่ **ผิดสำหรับจอที่ถือ singleton** เพราะมันจะไม่มีใครเปิดกลับให้เลย
        ///
        /// มีลิสต์เดียวสองคนอ่าน — ก๊อปไปสองที่คือรับประกันว่าวันหนึ่งจะไม่ตรงกัน
        /// </summary>
        public static readonly string[] MustBeActive =
        {
            "P3R_LevelUp", "P3R_WinLose", "P3R_Pause",
        };

        [MenuItem("Tools/Clone Swarm/Fix Panel Activation")]
        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder($"[เปิด panel] {ScenePath}\n");
            int n = 0;

            foreach (var name in MustBeActive)
            {
                var go = FindInScene(scene, name);
                if (go == null) { log.AppendLine($"  ไม่เจอ '{name}'"); continue; }

                if (go.activeSelf) { log.AppendLine($"  '{name}' เปิดอยู่แล้ว"); continue; }

                go.SetActive(true);
                EditorUtility.SetDirty(go);
                log.AppendLine($"  เปิด '{name}' กลับ");
                n++;
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine($"  บันทึกแล้ว ({n} panel)");
            }
            else log.AppendLine("  ไม่มีอะไรต้องทำ");

            Debug.Log(log.ToString());
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
