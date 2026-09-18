using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ถอด <c>WeaponStatHUD</c> ที่ปลดระวางแล้วออกจาก **ซีนเกม**
    /// เมนู: Tools > Clone Swarm > Retire WeaponStatHUD (SampleScene)
    ///
    /// ═══ ทำไมถึงปลดระวาง ═══
    ///
    /// ของที่ผู้เล่นถืออยู่เคยโชว์สองที่: แถวช่องของ `WeaponStatHUD` กับแถบ
    /// `BuildStripUI` · สองที่อ่านข้อมูลคนละทางและค่อยๆ เพี้ยนออกจากกัน
    /// ตอนนี้แถบ build เป็นที่เดียว — `WeaponStatHUD` จึงเหลือแต่ component ที่ชี้
    /// ช่องซึ่งถูกลบไปแล้ว (`bg`/`icon` เป็น null ทั้ง 5 ช่อง)
    ///
    /// component ที่ชี้ของที่ไม่มีแล้วไม่ throw — มันแค่ **ไม่ทำอะไร** ซึ่งแย่กว่า
    /// เพราะคนอ่านซีนรอบหน้าจะคิดว่ามันยังทำงานอยู่
    ///
    /// ═══ ลบเฉพาะซีนเกม ไม่แตะที่อื่น ═══
    ///
    /// `WeaponTestScene` (ฮาร์เนสทดสอบอาวุธ) กับ `Proto_GameplayHUD2` (ซีนต้นแบบ)
    /// ยังใช้ของเดิมอยู่และไม่ใช่ซีนที่ผู้เล่นเข้า — ปล่อยไว้
    /// **คลาสก็ยังอยู่** เพราะเครื่องมือฝั่ง Editor อีกห้าตัวอ้างถึงมัน
    /// (Restyler · V2Migrator · V2SceneBuilder · SlotTrimmer · SmokeTest)
    /// การปลดระวางคือ "ไม่ได้ใช้ในเกมแล้ว" ไม่ใช่ "ลบทิ้งทั้งโปรเจกต์"
    ///
    /// รันซ้ำได้ — ถอดไปแล้วก็บอกว่าไม่มีอะไรต้องทำ
    /// </summary>
    public static class P3RRetireWeaponStatHUD
    {
        private const string ScenePath = "Assets/GameScenes/SampleScene.unity";

        [MenuItem("Tools/Clone Swarm/Retire WeaponStatHUD (SampleScene)")]
        public static void Retire()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder($"[ปลดระวาง] WeaponStatHUD ใน {ScenePath}\n");

            var found = scene.GetRootGameObjects()
                             .SelectMany(r => r.GetComponentsInChildren<WeaponStatHUD>(true))
                             .ToArray();

            if (found.Length == 0)
            {
                Debug.Log(log.AppendLine("  ถอดไปแล้ว ไม่มีอะไรต้องทำ").ToString());
                return;
            }

            // เช็คว่ายังมีแถบ build อยู่จริงก่อนถอดของเก่า — ถ้าถอดแล้วไม่เหลือที่ไหน
            // โชว์ของที่ถืออยู่เลย นั่นไม่ใช่การปลดระวาง นั่นคือการทำฟีเจอร์หาย
            var strip = scene.GetRootGameObjects()
                             .SelectMany(r => r.GetComponentsInChildren<CloneSwarm.UI.P3R.BuildStripUI>(true))
                             .FirstOrDefault(s => s.gameObject.activeInHierarchy);

            if (strip == null)
            {
                Debug.LogError("[ปลดระวาง] ไม่เจอแถบ build ที่เปิดอยู่ในซีน — " +
                               "ถอด WeaponStatHUD ตอนนี้จะไม่เหลือที่โชว์ของที่ถืออยู่เลย · ยกเลิก");
                return;
            }

            foreach (var hud in found)
            {
                log.AppendLine($"  ถอดออกจาก '{Path(hud.transform)}'");
                Object.DestroyImmediate(hud, allowDestroyingAssets: false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึกแล้ว ({found.Length} component) · " +
                           $"แถบ build ที่รับช่วงต่อ: {Path(strip.transform)}");
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
