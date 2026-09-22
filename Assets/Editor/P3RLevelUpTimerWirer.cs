using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ต่อ <c>LevelUpUI.timerGroup</c> ให้ชี้ก้อนนาฬิกาทั้งก้อนในซีนเกม
    /// เมนู: Tools > Clone Swarm > Wire LevelUp Timer Group
    ///
    /// ═══ ทำไมต้องมีปุ่มแยก แทนที่จะสร้างจอใหม่ ═══
    ///
    /// ช่องนี้ <see cref="P3RLevelUpSceneBuilder"/> ต่อให้อยู่แล้วตอนสร้าง — แต่การสร้างใหม่
    /// แปลว่าทั้งจอถูกเขียนทับ **รวมถึงของที่จัดด้วยมือในซีน** ซึ่งแพงเกินไปสำหรับสายเส้นเดียว
    ///
    /// ตัวนี้แตะช่องเดียว ไม่แตะทรง ไม่แตะข้อความ ไม่แตะลำดับ
    ///
    /// ═══ ทำไมไม่ให้โค้ดตอนรันหาเอง ═══
    ///
    /// <c>LevelUpUI</c> หาลูกชื่อ "Timer" เองก็ได้ แต่วันที่มีใครเปลี่ยนชื่อหรือย้ายมัน
    /// ไปอยู่ใต้ของอื่น มันจะซ่อนของผิดชิ้น (หรือไม่ซ่อนอะไรเลย) โดยไม่มี error
    /// — สายที่มองเห็นใน Inspector ผิดแล้วเห็น ส่วนการเดาด้วยชื่อผิดแล้วเงียบ
    ///
    /// รันซ้ำได้ — ต่อไว้แล้วก็บอกว่าไม่มีอะไรต้องทำ
    /// </summary>
    public static class P3RLevelUpTimerWirer
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";
        private const string TimerName = "Timer";

        [MenuItem("Tools/Clone Swarm/Wire LevelUp Timer Group")]
        public static void Wire()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder("[นาฬิกา] ต่อก้อนนาฬิกาของจอเลเวลอัป\n");
            int n = 0;

            var uis = scene.GetRootGameObjects()
                           .SelectMany(r => r.GetComponentsInChildren<LevelUpUI>(true))
                           .ToArray();

            if (uis.Length == 0)
            {
                Debug.LogError($"[นาฬิกา] หา LevelUpUI ไม่เจอใน {ScenePath}");
                return;
            }

            foreach (var ui in uis)
            {
                // **หาเฉพาะใต้จอตัวเอง** ไม่ใช่ทั้งซีน — ชื่อ "Timer" ธรรมดาเกินกว่าจะ
                // เชื่อได้ว่ามีตัวเดียว · จอหยุดเกมหรือ HUD มีของชื่อเดียวกันได้สบายๆ
                var timer = ui.GetComponentsInChildren<Transform>(true)
                              .FirstOrDefault(t => t.name == TimerName && t != ui.transform);

                if (timer == null)
                {
                    log.AppendLine($"  '{Path(ui.transform)}' ไม่มีลูกชื่อ '{TimerName}' — ข้าม");
                    continue;
                }

                if (ui.timerGroup == timer.gameObject)
                {
                    log.AppendLine($"  '{Path(ui.transform)}' ต่อไว้แล้ว");
                    continue;
                }

                ui.timerGroup = timer.gameObject;
                EditorUtility.SetDirty(ui);
                n++;
                log.AppendLine($"  timerGroup → {Path(timer)}");
            }

            if (n == 0) { Debug.Log(log.AppendLine("  ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึก {ScenePath} ({n} จุด)");
            Debug.Log(log.ToString());
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
