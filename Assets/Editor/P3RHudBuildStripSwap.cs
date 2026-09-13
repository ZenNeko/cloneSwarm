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
    /// เปลี่ยนแถวอาวุธ/พาสซีฟบน HUD ตอนเล่น ให้เป็นแถบเดียวกับจอ Level Up
    /// เมนู: Tools > Clone Swarm > Swap HUD Rows → BuildStrip
    ///
    /// ═══ ทำอะไร ═══
    ///
    /// สร้าง <see cref="BuildStripUI"/> ด้วย <see cref="P3RBuildStripBuilder"/> ตัวเดียว
    /// กับที่จอ Level Up ใช้ วางล่างกลางของ `P3R_HUD` แล้ว **ปิด** แถวเดิมของ
    /// `WeaponStatHUD` พร้อมเปลี่ยนชื่อขึ้นต้น `Legacy_`
    ///
    /// ═══ ทำไมไม่ rebuild ซีนใหม่ทั้งอัน ═══
    ///
    /// `P3RGameplayHudV2SceneBuilder` + `P3RGameplayHudV2Migrator` ทำงานเป็นคู่ และ
    /// migrator คัดค่าจากตัวขนบน panel ไปทับตัวคุมจริงบน `HUDCanvas` ทั้งชุด
    /// รันใหม่เพื่อเปลี่ยนสองแถว = เอาทั้ง HUD ไปเสี่ยงกับสายอีกหลายสิบเส้นที่ไม่ได้แก้
    /// สาขานี้เคยเสียงานถาวรมาแล้วสองครั้งจากการเขียนทับที่กู้ไม่ได้
    ///
    /// ตัวนี้จึงผ่าเฉพาะจุด และเรียก builder ตัวเดียวกับที่ scene builder เรียก
    /// ผลที่ได้จึงตรงกับการ rebuild ทุกประการ
    ///
    /// ═══ ของเดิมไม่ถูกลบ ═══
    ///
    /// แถวเดิมถูกปิด ไม่ลบ · `WeaponStatHUD` กับอาเรย์ของมันไม่ถูกแตะเลย
    /// อยากกลับไปใช้แบบเดิมก็ปิดแถบใหม่แล้วเปิด `Legacy_*` กลับ — ไม่ต้อง revert อะไร
    ///
    /// รันซ้ำได้ — รอบสองเห็นว่ามี `BuildStrip` อยู่แล้วจะข้ามให้
    /// </summary>
    public static class P3RHudBuildStripSwap
    {
        private static readonly string[] Scenes =
        {
            "Assets/GameScenes/Proto_GameplayHUD2.unity",
            "Assets/GameScenes/SampleScene.unity",
        };

        private const string PanelName = "P3R_HUD";
        private const string StripName = "BuildStrip";

        /// <summary>
        /// แถวเดิมที่แถบใหม่มาแทน — ปิด ไม่ลบ · คีย์คือชื่อเดิม ค่าคือชื่อใหม่
        ///
        /// ขึ้นต้น `Legacy_Hud` ไม่ใช่ `Legacy_` เฉยๆ เพราะ **ใต้ `HUDCanvas` มี
        /// `Legacy_WeaponRow` กับ `Legacy_StatRow` อยู่แล้ว** จากตอนย้าย HUD v2
        /// (นั่นคือแถวของ HUD รุ่นก่อน P3R) ชื่อซ้ำกันสองคู่คนละพ่อ ทำให้ทุกที่ที่
        /// ค้นด้วยชื่อ — `P3RGameplayHudV2Migrator.FindInScene` เป็นต้น — หยิบผิดตัวได้
        /// </summary>
        private static readonly (string from, string to)[] LegacyRows =
        {
            ("WeaponRow", "Legacy_HudWeaponRow"),
            ("StatRow",   "Legacy_HudStatRow"),
        };

        /// <summary>ล่างกลาง สูงจากขอบล่างเท่าจอ Level Up เพื่อให้สองจออ่านเหมือนกัน</summary>
        private const float BottomY = 45f;

        /// <summary>เดินจังหวะเท่า `WeaponStatHUD` เดิม — ไม่ใช่ตัวเลขใหม่</summary>
        private const float RefreshInterval = 0.4f;

        [MenuItem("Tools/Clone Swarm/Swap HUD Rows → BuildStrip")]
        public static void Swap()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "เปลี่ยนแถวบน HUD เป็นแถบเดียวกับจอ Level Up",
                    "จะสร้าง BuildStrip ล่างกลางของ P3R_HUD แล้วปิดแถวเดิม\n" +
                    "(เปลี่ยนชื่อขึ้นต้น Legacy_ — ไม่ลบ)\n\n" +
                    "ควรมี working tree ที่สะอาดก่อนกด",
                    "เปลี่ยน", "ยกเลิก"))
                return;

            var (mono, display) = LoadFonts();
            var log = new StringBuilder("[HUD-แถบ] เปลี่ยนแถวอาวุธ/พาสซีฟเป็น BuildStrip\n");

            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var panel = FindInScene(scene, PanelName);
                if (panel == null)
                {
                    log.AppendLine($"  {path} — หา {PanelName} ไม่เจอ ข้าม");
                    continue;
                }

                if (!SwapPanel((RectTransform)panel.transform, mono, display, log))
                {
                    log.AppendLine($"  {path} — มี {StripName} อยู่แล้ว ไม่มีอะไรต้องทำ");
                    continue;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine($"  บันทึก {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static bool SwapPanel(RectTransform panel, TMP_FontAsset mono,
                                      TMP_FontAsset display, StringBuilder log)
        {
            if (panel.Find(StripName) != null) return false;

            var strip = P3RBuildStripBuilder.Build(panel, mono, display, StripName);
            P3RBuildStripBuilder.PlaceBottomCenter(strip, BottomY);

            // HUD ไม่มีใครสั่ง refresh ให้ (จอ Level Up มี LevelUpUI.Show()) จึงต้องเดินเอง
            strip.autoRefreshInterval = RefreshInterval;

            // เติมช่องว่างให้เห็นในซีน — ตอนรัน SpawnRow ล้างของค้างก่อนสร้างใหม่อยู่แล้ว
            strip.SetEntries(new BuildStripUI.Entry[0], new BuildStripUI.Entry[0]);

            log.AppendLine($"  สร้าง {StripName} ล่างกลาง y={BottomY} · " +
                           $"refresh ทุก {RefreshInterval}s");

            foreach (var (from, to) in LegacyRows)
            {
                var row = panel.Find(from);
                if (row == null) { log.AppendLine($"    ไม่เจอแถวเดิม '{from}'"); continue; }

                row.gameObject.SetActive(false);
                row.name = to;
                log.AppendLine($"    ปิด + เปลี่ยนชื่อ → {row.name}");
            }

            return true;
        }

        /// <summary>
        /// หยิบฟอนต์ชุดเดียวกับที่ builder จอ Level Up ใช้
        /// หาไม่เจอก็ยังสร้างได้ TMP จะใช้ฟอนต์ปริยาย — แค่หน้าตาไม่ตรงแบบ จึงเตือนไว้
        /// </summary>
        private const string DisplayFontPath =
            "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-ExtraBold SDF.asset";
        private const string MonoFontPath =
            "Assets/Prefab/Art Asset/Fnot/Sarabun/Sarabun-SemiBold SDF.asset";

        private static (TMP_FontAsset mono, TMP_FontAsset display) LoadFonts()
        {
            var display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            var mono    = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath) ?? display;

            if (display == null)
                Debug.LogWarning($"[HUD-แถบ] หาฟอนต์ไม่เจอที่ {DisplayFontPath} — " +
                                 "แถบจะใช้ฟอนต์ปริยายของ TMP ซึ่งไม่มีสระไทย");

            return (mono, display);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
