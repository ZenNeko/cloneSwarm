using System.Linq;
using System.Text;
using CloneSwarm.UI.P3R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ต่อ <see cref="GameplayLoadingGate"/> ให้ซีนเกม
    /// เมนู: Tools > Clone Swarm > Wire Gameplay Loading Gate
    ///
    /// วาง component บน **`HUDCanvas`** ไม่ใช่บน `P3R_Loading`
    /// — component บน panel ที่ปิดอยู่ไม่มีวันทำงาน (`Awake`/`OnEnable` ไม่วิ่ง)
    /// ถ้าวางบนแผงที่เริ่มมาแบบปิด ม่านจะไม่มีวันกางเลยสักครั้ง
    /// บั๊กเดียวกับที่ `WinLoseUI` เพิ่งโดน · ตัวคุมทุกตัวในซีนนี้อยู่บน HUDCanvas
    ///
    /// รันซ้ำได้ — มีอยู่แล้วก็แค่เช็คสายให้
    /// </summary>
    public static class P3RLoadingGateWirer
    {
        private const string ScenePath  = "Assets/GameScenes/SampleScene.unity";
        private const string CanvasName = "HUDCanvas";
        private const string PanelName  = "P3R_Loading";

        [MenuItem("Tools/Clone Swarm/Wire Gameplay Loading Gate")]
        public static void Wire()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder("[LoadingGate] ต่อม่านรอผู้เล่นในซีนเกม\n");

            var canvas = Find(scene, CanvasName);
            var panel  = Find(scene, PanelName);

            if (canvas == null) { Debug.LogError($"[LoadingGate] หา {CanvasName} ไม่เจอ"); return; }
            if (panel  == null)
            {
                Debug.LogError($"[LoadingGate] หา {PanelName} ไม่เจอใน {ScenePath} — " +
                               "สร้างด้วย P3RLoadingSceneBuilder แล้วย้ายเข้ามาก่อน");
                return;
            }

            int n = 0;

            var gate = canvas.GetComponent<GameplayLoadingGate>();
            if (gate == null)
            {
                gate = canvas.AddComponent<GameplayLoadingGate>();
                n++;
                log.AppendLine($"  ใส่ GameplayLoadingGate บน {CanvasName}");
            }

            if (gate.loadingPanel != panel)
            {
                gate.loadingPanel = panel;
                n++;
                log.AppendLine($"  loadingPanel → {PanelName}");
            }

            var screen = panel.GetComponent<LoadingScreenUI>();
            if (screen != null && gate.screen != screen)
            {
                gate.screen = screen;
                n++;
                log.AppendLine("  screen → LoadingScreenUI");
            }

            // ม่านต้องวาดทับทุกอย่าง — uGUI วาดตามลำดับพี่น้อง ลูกคนหลังทับลูกคนก่อน
            // ระหว่างรอต่อ ไม่ควรมีอะไรโผล่ข้างหน้ามันได้เลยแม้แต่จอหยุดเกม
            var t = panel.transform;
            int last = t.parent.childCount - 1;
            if (t.GetSiblingIndex() != last)
            {
                t.SetSiblingIndex(last);
                n++;
                log.AppendLine($"  วางลำดับท้ายสุด ({last}) ให้วาดทับทุกจอ");
            }

            // เริ่มมาแบบปิด — `GameplayLoadingGate.Awake` กางให้เองตอนรัน
            // เปิดค้างไว้ในซีนแปลว่าเปิดฉากมาเจอม่านบังใน Editor ทุกครั้งที่เปิดซีน
            if (panel.activeSelf)
            {
                panel.SetActive(false);
                n++;
                log.AppendLine("  ปิดแผงไว้ในซีน (ตอนรัน gate กางให้เอง)");
            }

            if (n == 0) { Debug.Log(log.AppendLine("  ต่อไว้แล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorUtility.SetDirty(gate);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึก {ScenePath} ({n} รายการ)");
            Debug.Log(log.ToString());
        }

        private static GameObject Find(Scene scene, string name)
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == name)?.gameObject;
    }
}
