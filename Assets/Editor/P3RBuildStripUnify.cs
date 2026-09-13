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
    /// ยุบแถบ build ให้เหลือ **อันเดียวจริงๆ** ในซีนเกม
    /// เมนู: Tools > Clone Swarm > Unify BuildStrip (อันเดียวทั้งเกม)
    ///
    /// ═══ ทำไมเคยมีสองอัน และทำไมนั่นไม่ใช่เหตุผลที่ดี ═══
    ///
    /// จอ Level Up ปูแผ่นทึบ 98% (`InkDeep` alpha 0xFA) เต็มจอทับทุกอย่าง แถบที่อยู่บน
    /// `P3R_HUD` จึงถูกฝังอยู่ใต้แผ่นนั้น · ทางแก้เดิมคือ "ก็สร้างอีกอันไว้บนจอ Level Up สิ"
    /// ซึ่งแก้อาการ แต่ทิ้งของชุดเดียวกันไว้สองที่ให้ค่อยๆ เพี้ยนออกจากกัน — และมันเพี้ยนจริง
    ///
    /// **นี่เป็นปัญหาลำดับการวาด ไม่ใช่ปัญหาจำนวนวัตถุ** · uGUI วาดตามลำดับพี่น้องในลำดับชั้น
    /// ลูกคนหลังทับลูกคนก่อน · ย้ายแถบขึ้นมาเป็นลูกของ `HUDCanvas` แล้ววางไว้ **หลัง**
    /// `P3R_LevelUp` มันก็อยู่เหนือแผ่นทึบ โดยไม่ต้องมีสำเนา
    ///
    /// ═══ ลำดับที่เลือก และทำไม ═══
    ///
    ///   P3R_HUD → … → P3R_LevelUp → **BuildStrip_Shared** → P3R_Pause → P3R_WinLose
    ///
    /// อยู่ **หลัง** `P3R_LevelUp` เพราะตอนเลือกการ์ดคือตอนที่ต้องอ่านของในมือที่สุด
    /// อยู่ **ก่อน** `P3R_Pause` กับ `P3R_WinLose` เพราะสองจอนั้นคือ "เกมหยุดแล้ว"
    /// แถบของที่ถืออยู่ไม่ควรลอยทับเมนูหยุดเกมหรือจอสรุปผล
    ///
    /// การเอาไปไว้ท้ายสุดให้ทับทุกอย่างนั้นง่ายกว่า แต่ผิด — ลำดับต้องเลือก ไม่ใช่ดันให้สุด
    ///
    /// ═══ ของเดิมไม่ถูกลบ ═══
    ///
    /// แถบของจอ Level Up ถูก **ปิด** และเปลี่ยนชื่อขึ้นต้น `Legacy_` เท่านั้น
    /// ซีนต้นแบบ `Proto_LevelUp` ยังมีแถบของตัวเองเหมือนเดิม เพราะซีนนั้นไม่มี `HUDCanvas`
    /// ให้อ้าง — มันเป็นภาพอ้างอิงของจอเดียว ไม่ใช่ซีนเกม
    ///
    /// รันซ้ำได้ — รอบสองเห็นว่ายุบแล้วก็ไม่ทำอะไร
    /// </summary>
    public static class P3RBuildStripUnify
    {
        private const string ScenePath  = "Assets/GameScenes/SampleScene.unity";
        private const string CanvasName = "HUDCanvas";
        private const string HudPanel   = "P3R_HUD";
        private const string LevelUp    = "P3R_LevelUp";
        private const string SharedName = "BuildStrip_Shared";

        [MenuItem("Tools/Clone Swarm/Unify BuildStrip (อันเดียวทั้งเกม)")]
        public static void Unify()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "ยุบแถบ build ให้เหลืออันเดียว",
                    "จะย้ายแถบของ HUD ขึ้นมาเป็นลูกของ HUDCanvas วางไว้หลัง P3R_LevelUp\n" +
                    "แล้วปิดแถบของจอ Level Up (เปลี่ยนชื่อขึ้นต้น Legacy_ — ไม่ลบ)\n\n" +
                    "ควรมี working tree ที่สะอาดก่อนกด",
                    "ยุบ", "ยกเลิก"))
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var log = new StringBuilder("[ยุบแถบ] แถบ build อันเดียวทั้งเกม\n");

            var canvas = FindInScene(scene, CanvasName)?.transform as RectTransform;
            var hud    = FindInScene(scene, HudPanel)?.transform;
            if (canvas == null || hud == null)
            {
                Debug.LogError($"[ยุบแถบ] หา {CanvasName} หรือ {HudPanel} ไม่เจอใน {ScenePath}");
                return;
            }

            // ── 1. แถบของจอ Level Up คือตัวที่อยู่รอด ─────────────────────
            //
            // **ไม่ใช่ของ HUD** — ตัวของจอ Level Up อยู่ตำแหน่งตามแบบ (ล่างขวา -57,45)
            // และเป็นตัวที่ถูกจัดมาเรื่อยๆ · ส่วนของ HUD ถูกลากจน anchor เพี้ยนไปเป็น (1,0)
            // และ `SlotArea` ในแถวย้ายไป x=0 จนป้ายกับช่องสลับข้างกัน
            // เก็บตัวที่สภาพดีกว่าไว้ แล้วปิดอีกตัว ไม่ใช่กลับกัน
            var shared = canvas.Find(SharedName) as RectTransform;

            if (shared == null)
            {
                var levelUpUI = FindAll<LevelUpUI>(scene).FirstOrDefault(u => u.buildStrip != null);
                shared = levelUpUI != null
                       ? levelUpUI.buildStrip.transform as RectTransform
                       : null;
            }

            if (shared == null)
            {
                Debug.LogError("[ยุบแถบ] หาแถบของจอ Level Up ไม่เจอ — " +
                               "LevelUpUI.buildStrip ว่างอยู่?");
                return;
            }

            int moved = 0;
            if (shared.parent != canvas)
            {
                // worldPositionStays: true — เก็บตำแหน่งที่จัดไว้ด้วยมือ ไม่รีเซ็ตให้
                shared.SetParent(canvas, true);
                moved++;
                log.AppendLine($"  ย้าย '{shared.name}' ออกจากจอ Level Up → ลูกของ {CanvasName}");
            }

            if (shared.name != SharedName)
            {
                log.AppendLine($"  เปลี่ยนชื่อ '{shared.name}' → '{SharedName}'");
                shared.name = SharedName;
                moved++;
            }

            // ── 2. ลำดับการวาด — หลัง P3R_LevelUp ก่อน P3R_Pause ──────────
            var lvl = canvas.Find(LevelUp);
            if (lvl != null)
            {
                int want = lvl.GetSiblingIndex() + 1;
                if (shared.GetSiblingIndex() != want)
                {
                    shared.SetSiblingIndex(want);
                    moved++;
                    log.AppendLine($"  วางลำดับที่ {want} — หลัง {LevelUp} ก่อน P3R_Pause");
                }
            }
            else log.AppendLine($"  **หา {LevelUp} ไม่เจอ** — ลำดับยังไม่ถูกจัด แถบอาจถูกแผ่นทึบบัง");

            // ── 3. ต้องเดินจังหวะเอง ไม่มีใครสั่งมันแล้ว ─────────────────
            var strip = shared.GetComponent<BuildStripUI>();
            if (strip != null && strip.autoRefreshInterval <= 0f)
            {
                strip.autoRefreshInterval = 0.4f;
                EditorUtility.SetDirty(strip);
                moved++;
                log.AppendLine("  ตั้ง autoRefreshInterval = 0.4s");
            }

            // ── 4. ปิดแถบของ HUD ที่กลายเป็นตัวซ้ำ ─────────────────────────
            var hudStrip = hud.Find("BuildStrip") as RectTransform;
            if (hudStrip != null && hudStrip != shared)
            {
                hudStrip.gameObject.SetActive(false);
                hudStrip.name = "Legacy_HudBuildStrip";
                EditorUtility.SetDirty(hudStrip.gameObject);
                moved++;
                log.AppendLine($"  ปิดแถบของ {HudPanel} → {hudStrip.name}");
            }

            // ── 5. LevelUpUI ยังชี้ตัวร่วมได้เหมือนเดิม ───────────────────
            // ไม่ใช่สายบังคับ — ตัวร่วมเดินจังหวะเองอยู่แล้ว อันนี้แค่ให้รีเฟรชทันที
            // ตอนจอเปิด แทนที่จะช้าได้ถึง 0.4 วิ · หลุดไปก็ไม่พัง
            foreach (var ui in FindAll<LevelUpUI>(scene))
            {
                if (ui.buildStrip == strip) continue;
                ui.buildStrip = strip;
                EditorUtility.SetDirty(ui);
                moved++;
                log.AppendLine($"  ชี้ LevelUpUI.buildStrip → {SharedName}");
            }

            if (moved == 0) { Debug.Log(log.AppendLine("  ยุบไว้แล้ว ไม่มีอะไรต้องทำ").ToString()); return; }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึก {ScenePath} ({moved} รายการ)");
            Debug.Log(log.ToString());
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<T>(true))
                    .ToArray();

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
