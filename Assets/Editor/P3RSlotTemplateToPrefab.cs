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
    /// ชี้ <c>BuildStripUI.slotTemplate</c> ไปที่ **prefab asset** แทน object ในซีน
    /// เมนู: Tools > Clone Swarm > Slot Template → Prefab
    ///
    /// ═══ ทำไมต้องย้าย ═══
    ///
    /// ช่องของแถบ build ถูก `Instantiate` ซ้ำสิบกว่าครั้งตอนรัน ตามกติกาของโปรเจกต์
    /// (skill `game-ui` §"prefab หรือ builder") ของแบบนี้ต้องเป็น prefab
    ///
    /// **พูดตรงๆ: กรณีนี้ยังไม่ใช่บั๊กที่รออยู่** — `BuildStripUI` อยู่บนตัวแถบเอง
    /// แม่แบบเป็นลูกของมัน สองอย่างนี้เกิดและตายพร้อมกันเสมอ สายจึงไม่ขาดกลางทาง
    /// เหมือนกรณีที่ตัวคุมอยู่บน Canvas แล้ว panel ถูกสร้างใหม่ (ซึ่งเป็นที่มาของกติกา)
    ///
    /// ที่ได้จริงจากการย้ายคือ:
    ///   • นิยามหน้าตาช่องอยู่ที่เดียว แก้ใน Inspector ได้โดยไม่ต้องเปิดซีน
    ///   • ซีนเล็กลง — โครงช่อง ~11 object เลิกถูกก๊อปเข้าไปนอนในไฟล์ซีน
    ///   • ตรงกับกติกาที่เหลือของโปรเจกต์ ไม่ต้องจำว่าอันนี้เป็นข้อยกเว้น
    ///
    /// prefab asset อ้างด้วย guid — ย้ายซีนกี่รอบก็ไม่หลุด ซึ่งกันไว้เผื่ออนาคต
    /// ที่วันหนึ่งแถบกับตัวคุมอาจไม่ได้อยู่ด้วยกันแล้ว
    ///
    /// ═══ ทำอะไร ═══
    ///
    ///   1. ชี้ `slotTemplate` ของทุก `BuildStripUI` ในซีนไปที่ prefab
    ///   2. **ปิด** แม่แบบตัวเก่าที่เป็นลูกของแถบ แล้วเปลี่ยนชื่อขึ้นต้น `Legacy_`
    ///
    /// ไม่ลบของเก่า — ถ้าย้อนต้องการก็ชี้กลับได้ · รันซ้ำได้
    ///
    /// **ต้องสร้าง prefab ก่อน** ด้วย builder ของจอที่เกี่ยวข้อง
    /// (`P3RBuildStripBuilder` เซฟให้ตอน build) — ตัวนี้ไม่สร้างเอง
    /// เพราะถ้าสร้างเองจะกลายเป็นแหล่งที่สองที่นิยามหน้าตาของช่อง
    /// </summary>
    public static class P3RSlotTemplateToPrefab
    {
        private static readonly string[] Scenes =
        {
            "Assets/GameScenes/SampleScene.unity",
            "Assets/GameScenes/Proto_LevelUp.unity",
            "Assets/GameScenes/Proto_GameplayHUD2.unity",
        };

        [MenuItem("Tools/Clone Swarm/Slot Template → Prefab")]
        public static void Convert()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                P3RBuildStripBuilder.SlotPrefabPath);

            if (prefab == null)
            {
                Debug.LogError(
                    $"[Slot→Prefab] ยังไม่มี {P3RBuildStripBuilder.SlotPrefabPath}\n" +
                    "สร้างก่อนด้วย Tools > Clone Swarm > Build P3R Level Up Scene " +
                    "(builder เป็นคนเซฟ prefab ตัวนี้)");
                return;
            }

            var slotPrefab = prefab.GetComponent<BuildStripSlot>();
            if (slotPrefab == null)
            {
                Debug.LogError($"[Slot→Prefab] {P3RBuildStripBuilder.SlotPrefabPath} " +
                               "ไม่มี component BuildStripSlot");
                return;
            }

            var log = new StringBuilder("[Slot→Prefab] ชี้แม่แบบไปที่ prefab asset\n");

            foreach (var path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    log.AppendLine($"  {path} — ไม่มีไฟล์ ข้าม");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = 0;

                foreach (var strip in FindAll<BuildStripUI>(scene))
                {
                    var old = strip.slotTemplate;

                    // ชี้อยู่ที่ prefab แล้ว — asset ไม่มี scene ที่ valid
                    if (old != null && !old.gameObject.scene.IsValid()) continue;

                    strip.slotTemplate = slotPrefab;
                    EditorUtility.SetDirty(strip);
                    n++;
                    log.AppendLine($"  {Path(strip.transform)} → prefab");

                    if (old != null)
                    {
                        old.gameObject.SetActive(false);
                        if (!old.name.StartsWith("Legacy_")) old.name = "Legacy_" + old.name;
                        EditorUtility.SetDirty(old.gameObject);
                        log.AppendLine($"    ปิดแม่แบบเดิม → {old.name}");
                    }
                }

                if (n == 0) { log.AppendLine($"  {path} — ไม่มีอะไรต้องทำ"); continue; }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine($"  บันทึก {path} ({n} แถบ)");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<T>(true))
                    .ToArray();

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
