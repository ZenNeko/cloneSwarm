using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ย้าย `P3R_HUD` จาก `Proto_GameplayHUD2` เข้า `SampleScene`
    /// เมนู: Tools > Clone Swarm > Migrate Gameplay HUD v2 → SampleScene
    ///
    /// ═══ ต่างจาก P3RScreenMigrator ยังไง ═══
    ///
    /// ตัวนั้น **ลบ panel เดิมทิ้งทั้งอัน** แล้ววางตัวใหม่แทน ซึ่งใช้ได้กับจอเมนูเพราะ
    /// ตัวคุมอยู่บน panel เดียวกันและถูกสร้างใหม่ไปพร้อมกัน
    ///
    /// HUD ทำแบบนั้นไม่ได้ — ตัวคุมทุกตัว (`GameHUD` · `WeaponStatHUD` · `BossHUDUI` ·
    /// `ObjectiveTrackerHUD` · `StatusHUDUI` · `ObjectiveIndicatorUI` · `DamageFeedbackUI`)
    /// อยู่บน **`HUDCanvas`** ไม่ได้อยู่บน panel · ลบ panel = สายขาดหมดแต่ component ยังอยู่
    ///
    /// ตัวนี้จึงทำกลับด้าน: ก๊อป panel ใหม่เข้ามา แล้ว **คัดค่าจากตัวขนบน panel
    /// ไปใส่ตัวจริงบน HUDCanvas** แล้วลบตัวขนทิ้ง · ตัวจริงไม่เคยถูกแตะ
    ///
    /// ═══ ของเดิมไม่ถูกลบ ═══
    ///
    /// HUD ชุดเก่าถูก **ปิด** ไม่ใช่ลบ และเปลี่ยนชื่อขึ้นต้น `Legacy_` ให้เห็นชัด
    /// สาขานี้เคยเสียงานถาวรมาแล้วสองครั้งจากการลบ/เขียนทับที่กู้ไม่ได้ — ปิดไว้
    /// แปลว่าย้อนได้ด้วยการติ๊กกลับ และ git diff ยังอ่านออกว่าอะไรหายไปจากจอ
    ///
    /// รันซ้ำได้ — รอบสองจะเห็นว่ามี `P3R_HUD` อยู่แล้วและถามก่อนทับ
    /// </summary>
    public static class P3RGameplayHudV2Migrator
    {
        private const string ProtoPath  = "Assets/GameScenes/Proto_GameplayHUD2.unity";
        private const string TargetPath = "Assets/GameScenes/SampleScene.unity";
        private const string CanvasName = "HUDCanvas";
        private const string PanelName  = "P3R_HUD";

        /// <summary>
        /// กิ่ง HUD ชุดเก่าที่จอใหม่มาแทนที่ — **ปิด ไม่ลบ**
        ///
        /// `Objective_Panel` อยู่ในนี้ด้วยเพราะ `ObjectiveTrackerHUD` จะถูกชี้ไปที่แผงใหม่
        /// ถ้าปล่อยของเดิมเปิดไว้จะได้แผง objective ว่างๆ ค้างอยู่มุมจอตลอดเกม
        /// </summary>
        private static readonly string[] LegacyRoots =
        {
            "Timer_Panel", "BottomLeft_Panel", "WeaponRow", "StatRow",
            "Objective_Panel", "MiniBossHP_Panel", "Announcement",
            "RespawnOverlay",

            // **`DamageFeedbackPanel` จงใจไม่อยู่ในนี้** — v2 ไม่มีตัวแทนให้มัน และ
            // `DamageFeedbackUI` ขับด้วย `vignetteImage.color.a` ไม่ได้ `SetActive`
            // ปิด GameObject ทิ้ง = เอฟเฟกต์ตอนโดนตีหายไปเงียบๆ ทั้งเกม
            // มันเป็นชั้นเอฟเฟกต์เต็มจอ ไม่ใช่ชิ้นส่วนของเลย์เอาต์ จึงอยู่ต่อได้ตามเดิม
        };

        [MenuItem("Tools/Clone Swarm/Migrate Gameplay HUD v2 → SampleScene")]
        public static void Migrate()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "ย้าย HUD v2 เข้าซีนเกม",
                    "จะก๊อป P3R_HUD เข้า SampleScene แล้วชี้ตัวคุมบน HUDCanvas มาที่ของใหม่\n\n" +
                    "HUD ชุดเก่าถูก **ปิด ไม่ลบ** และเปลี่ยนชื่อขึ้นต้น Legacy_\n" +
                    "ย้อนได้ด้วย git · ควรมี working tree ที่สะอาดก่อนกด",
                    "ย้าย", "ยกเลิก"))
                return;

            var log = new StringBuilder("[HUD-ย้าย] P3R_HUD → SampleScene\n");

            // ── 1. หยิบ panel ต้นแบบออกมาก่อน ────────────────────────────────
            var proto = EditorSceneManager.OpenScene(ProtoPath, OpenSceneMode.Single);
            var source = FindInScene(proto, PanelName);
            if (source == null)
            {
                Debug.LogError($"[HUD-ย้าย] หา '{PanelName}' ใน {ProtoPath} ไม่เจอ — " +
                               "สร้างซีนต้นแบบก่อนด้วย Build P3R Gameplay HUD v2 Scene");
                return;
            }

            // ก๊อปข้ามซีนต้องผ่าน prefab ชั่วคราว — Instantiate ตรงๆ ได้ของที่อ้าง
            // object ในซีนต้นทางซึ่งจะกลายเป็น null ทันทีที่เปิดซีนปลายทาง
            string temp = "Assets/__P3R_HUD_migrate.prefab";
            PrefabUtility.SaveAsPrefabAsset(source, temp);

            try
            {
                var scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
                var canvas = FindInScene(scene, CanvasName);
                if (canvas == null)
                {
                    Debug.LogError($"[HUD-ย้าย] หา '{CanvasName}' ใน {TargetPath} ไม่เจอ");
                    return;
                }

                // ── 2. เอาตัวเก่าที่ย้ายมาแล้วออกก่อน (รันซ้ำได้) ──────────────
                var existing = FindInScene(scene, PanelName);
                if (existing != null)
                {
                    log.AppendLine($"   ลบ {PanelName} ของรอบก่อนออก แล้ววางใหม่");
                    Object.DestroyImmediate(existing);
                }

                // ── 3. วาง panel ใหม่ ────────────────────────────────────────
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(temp);
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas.transform);
                PrefabUtility.UnpackPrefabInstance(panel, PrefabUnpackMode.Completely,
                                                   InteractionMode.AutomatedAction);
                panel.name = PanelName;
                panel.transform.SetAsFirstSibling();   // อยู่หลังจอ LevelUp/Pause/WinLose
                log.AppendLine($"   วาง {PanelName} ใต้ {CanvasName} แล้ว");

                // ── 4. คัดสายจากตัวขนไปใส่ตัวจริง ───────────────────────────
                TransplantAll(canvas, panel, log);

                // ── 5. ปิดของเดิม — ไม่ลบ ────────────────────────────────────
                foreach (var name in LegacyRoots)
                {
                    var old = canvas.transform.Find(name);
                    if (old == null) { log.AppendLine($"   ไม่เจอของเดิม {name} (ข้าม)"); continue; }
                    old.gameObject.SetActive(false);
                    old.name = "Legacy_" + name;
                    log.AppendLine($"   ปิด + เปลี่ยนชื่อ {name} → Legacy_{name}");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                log.AppendLine();
                log.AppendLine("   ของเดิมถูก **ปิด ไม่ลบ** — ย้อนได้ด้วยการติ๊กกลับหรือ git");
                log.AppendLine("   ยังต้องกดเล่นจริงถึงจะรู้: 5-second test");
                Debug.Log(log.ToString());
            }
            finally
            {
                AssetDatabase.DeleteAsset(temp);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        /// <summary>
        /// คัดค่าทีละช่องจากตัวขนบน panel ไปใส่ตัวจริงบน HUDCanvas
        ///
        /// **คัดเฉพาะช่องที่ชี้ของในซีน** — ช่องที่ชี้ asset (`miniBossBarPrefab` ·
        /// `entryPrefab`) ต้องปล่อยไว้ ตัวจริงถือค่าที่ถูกอยู่แล้วและตัวขนไม่มีให้
        /// เขียนทับไปจะได้ null ที่ไม่มีอะไรฟ้องจนกว่าจะถึงเวลาต้องใช้จริง
        /// </summary>
        private static void TransplantAll(GameObject canvas, GameObject panel, StringBuilder log)
        {
            Transplant<GameHUD>(canvas, panel, log, (dst, src) =>
            {
                dst.timerLabel           = src.timerLabel;
                dst.hpText               = src.hpText;
                dst.hpFill               = src.hpFill;
                dst.shieldFill           = src.shieldFill;
                dst.shieldBarRoot        = src.shieldBarRoot;
                dst.expFill              = src.expFill;
                dst.levelText            = src.levelText;
                dst.announcementLabel    = src.announcementLabel;
                dst.announcementDuration = src.announcementDuration;
                dst.respawnPanel         = src.respawnPanel;
                dst.respawnCountdownText = src.respawnCountdownText;
                dst.chargeBarRoot        = src.chargeBarRoot;
                dst.chargeBarFill        = src.chargeBarFill;
                dst.chargeBarText        = src.chargeBarText;
                dst.qSlot                = src.qSlot;
                dst.eSlot                = src.eSlot;
                dst.abilityReadyColor    = src.abilityReadyColor;
                dst.abilityCooldownColor = src.abilityCooldownColor;
                dst.abilityActiveColor   = src.abilityActiveColor;
                dst.mainBossAnnouncementColor = src.mainBossAnnouncementColor;
            });

            Transplant<WeaponStatHUD>(canvas, panel, log, (dst, src) =>
            {
                // สีไม่คัด — `P3RGameplayHudRestyler` ตั้งไว้ที่ตัวจริงแล้ว
                dst.weaponSlots = src.weaponSlots;
                dst.statSlots   = src.statSlots;
            });

            Transplant<BossHUDUI>(canvas, panel, log, (dst, src) =>
            {
                dst.miniBossPanel     = src.miniBossPanel;
                dst.miniBossContainer = src.miniBossContainer;
                dst.castBarRoot       = src.castBarRoot;
                dst.castFill          = src.castFill;
                dst.castNameText      = src.castNameText;
                // miniBossBarPrefab ไม่คัด — เป็น asset ที่ตัวจริงถืออยู่แล้ว
            });

            Transplant<ObjectiveTrackerHUD>(canvas, panel, log, (dst, src) =>
            {
                dst.panelRoot      = src.panelRoot;
                dst.entryContainer = src.entryContainer;
                // entryPrefab ไม่คัด — เหตุผลเดียวกัน
            });
        }

        private static void Transplant<T>(GameObject canvas, GameObject panel,
                                          StringBuilder log, System.Action<T, T> copy)
            where T : MonoBehaviour
        {
            var carrier = panel.GetComponent<T>();
            if (carrier == null) { log.AppendLine($"   ไม่มีตัวขน {typeof(T).Name} บน panel"); return; }

            var real = canvas.GetComponent<T>();
            if (real == null)
            {
                // ตัวจริงไม่มีบน Canvas — ปล่อยตัวขนไว้ทำงานแทน ดีกว่าลบทิ้งแล้วไม่มีใครขับ
                log.AppendLine($"   ⚠ ไม่เจอ {typeof(T).Name} บน {CanvasName} — " +
                               "ปล่อยตัวขนบน panel ไว้ทำงานแทน");
                return;
            }

            Undo.RecordObject(real, "P3R HUD v2 migrate");
            copy(real, carrier);
            EditorUtility.SetDirty(real);
            Object.DestroyImmediate(carrier);
            log.AppendLine($"   คัดสาย {typeof(T).Name} → ตัวจริงบน {CanvasName} แล้วลบตัวขน");
        }

        // ═══════════════════════════════════════════════════════════════════
        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var hit = root.GetComponentsInChildren<Transform>(true)
                              .FirstOrDefault(t => t.name == name);
                if (hit != null) return hit.gameObject;
            }
            return null;
        }
    }
}
