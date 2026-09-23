using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// คัดลอกตารางเวลาจาก <c>GameTimeline</c> และสเกลศัตรูจาก <c>WaveManager</c>
    /// ในซีนเกม ไปลง <c>MapData</c>
    /// เมนู: Tools > Clone Swarm > Copy Scene Timeline → MapData
    ///
    /// ═══ หลังคัดลอกแล้ว ตารางของซีนจะไม่ถูกใช้ในรันจริงอีก ═══
    ///
    /// `GameTimeline.ResolveSchedule` เลือกแมพก่อนเสมอ · แมพที่มีนัดหมายจะ
    /// **แทนที่ลิสต์ของซีนทั้งก้อน** ลิสต์ในซีนยังเห็นอยู่ใน Inspector แต่กลายเป็น
    /// ของที่แก้แล้วไม่มีผลเมื่อเข้าเกมผ่านล็อบบี้ — กับดักที่ดูเหมือนบั๊ก
    ///
    /// ลิสต์ในซีนยังมีหน้าที่อยู่: มันคือสิ่งที่เกิดเมื่อกด Play ที่ SampleScene ตรงๆ
    /// ซึ่งตอนนั้น `RunSetup.Map` เป็น null · คิดว่ามันเป็น "ค่าตอนทดสอบซีนเดี่ยว"
    /// ไม่ใช่ "ตารางของเกม"
    ///
    /// ดูได้จาก log ตอนเริ่มรันว่าตารางไหนชนะ:
    ///   [GameTimeline] เริ่ม — ตารางจากแมพ arena01 / Normal · ...
    ///
    /// ═══ ของที่คัดลอกไม่ได้ ═══
    ///
    /// `objectiveExpReward` · `objectiveHealAmount` · `waitForAllPlayers` ·
    /// `startWaitTimeout` ไม่มีใน `TimelineSchedule` เพราะไม่ใช่ **ตาราง** —
    /// มันคือกติกาของซีน ไม่ใช่จังหวะของแมพ · ถ้าวันหนึ่งอยากให้แมพตั้งรางวัลเองได้
    /// ค่อยเพิ่มเข้า schedule แล้วตัวนี้จะคัดลอกให้เอง
    ///
    /// ═══ สเกลศัตรูถูกเปิดสวิตช์ให้เลย ═══
    ///
    /// คัดลอกแล้วตั้ง `enabled = true` เพราะค่าที่คัดมา**เท่ากับของซีนทุกตัว** —
    /// พฤติกรรมจึงไม่เปลี่ยนแม้แต่นิดเดียว เปลี่ยนแค่ว่าใครเป็นเจ้าของค่า
    ///
    /// ทางเลือกอีกทางคือคัดค่ามาแต่ปิดสวิตช์ไว้ ซึ่งจะได้ตัวเลขที่เห็นอยู่ใน
    /// Inspector แต่ไม่มีผลอะไรเลย — กับดักแบบเดียวกับที่เพิ่งเตือนเรื่องลิสต์ในซีน
    /// </summary>
    public static class MapScheduleImporter
    {
        private const string ScenePath   = "Assets/GameScenes/SampleScene.unity";
        private const string DefaultMap  = "Assets/ScriptableObjects/Map/MapData_Arena01.asset";

        [MenuItem("Tools/Clone Swarm/Copy Scene Timeline → MapData")]
        public static void CopyToSelection()
        {
            // เก็บ **path** ไม่ใช่ตัว object — ดูเหตุผลที่ Run()
            var paths = Selection.objects.OfType<MapData>()
                                 .Select(AssetDatabase.GetAssetPath)
                                 .Where(p => !string.IsNullOrEmpty(p))
                                 .ToArray();

            if (paths.Length == 0)
            {
                Debug.LogError("[คัดลอกตาราง] เลือก MapData asset ใน Project ก่อน " +
                               "แล้วค่อยกดเมนูนี้");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run(paths);
        }

        /// <summary>
        /// ทางเรียกจาก batchmode — รับ -mapAsset &lt;path&gt; ได้ (ไม่ใส่ = MapData_Arena01_Normal)
        /// </summary>
        public static void CopyFromCommandLine()
        {
            Run(new[] { ArgValue("-mapAsset") ?? DefaultMap });
        }

        private static string ArgValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }

        // ── งานจริง ───────────────────────────────────────────────────────
        /// <summary>
        /// รับ **path** ไม่ใช่ตัว MapData
        ///
        /// ═══ ทำไมโหลด asset หลังเปิดซีน ═══
        ///
        /// `OpenScene` ปลด asset ที่ไม่มีใครอ้างถึงทิ้ง · MapData ที่โหลดไว้ก่อนหน้า
        /// จึงถูกทำลาย แล้วการแตะมันบรรทัดถัดมาได้ MissingReferenceException
        /// ("has been destroyed but you are still trying to access it")
        ///
        /// path เป็นสตริง ไม่มีอะไรทำลายได้ — โหลดใหม่หลังซีนนิ่งแล้วจึงปลอดภัย
        /// </summary>
        private static void Run(string[] mapPaths)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gt = scene.GetRootGameObjects()
                          .SelectMany(r => r.GetComponentsInChildren<GameTimeline>(true))
                          .FirstOrDefault();

            if (gt == null) { Debug.LogError($"[คัดลอกตาราง] หา GameTimeline ไม่เจอใน {ScenePath}"); return; }

            var log = new StringBuilder("[คัดลอกตาราง]\n");
            var wm = scene.GetRootGameObjects()
                           .SelectMany(r => r.GetComponentsInChildren<WaveManager>(true))
                           .FirstOrDefault();

            log.AppendLine($"  ต้นทาง: {ScenePath} · นัดหมาย {CountTimes(gt.cues)} ครั้ง · " +
                           $"บอสใหญ่ {gt.mainBossTimeMin} นาที");

            if (wm == null)
                log.AppendLine("  ไม่เจอ WaveManager ในซีน — ไม่ได้คัดลอกสเกลศัตรู");

            foreach (var mapPath in mapPaths)
            {
                var map = AssetDatabase.LoadAssetAtPath<MapData>(mapPath);
                if (map == null)
                {
                    Debug.LogError($"[คัดลอกตาราง] ไม่เจอ MapData ที่ {mapPath}");
                    continue;
                }

                if (map.tiers == null || map.tiers.Length == 0)
                {
                    Debug.LogWarning($"[คัดลอกตาราง] {map.name} ไม่มี tier เลย — ข้าม");
                    continue;
                }

                foreach (var tier in map.tiers)
                {
                    if (tier == null) continue;
                    if (tier.schedule == null) tier.schedule = new TimelineSchedule();

                    // ของเดิมที่จะโดนทับ — เงียบไม่ได้ นี่คือการลบงานของคนอื่น
                    int had = CountTimes(tier.schedule.cues);
                    if (had > 0)
                        log.AppendLine($"  ⚠ {map.name} / {tier.tier} มีนัดหมายอยู่แล้ว {had} ครั้ง — เขียนทับ");

                    tier.schedule.cues            = CloneCues(gt.cues);
                    tier.schedule.mainBossMinutes = gt.mainBossTimeMin;

                    log.AppendLine($"  {map.name} / {tier.tier} ← นัดหมาย {CountTimes(tier.schedule.cues)} ครั้ง · " +
                                   $"บอสใหญ่ {tier.schedule.mainBossMinutes} นาที");

                    if (wm == null) continue;

                    if (tier.enemyScaling != null && tier.enemyScaling.enabled)
                        log.AppendLine($"  ⚠ {map.name} / {tier.tier} เปิดสเกลของตัวเองอยู่แล้ว — เขียนทับ");

                    tier.enemyScaling = new EnemyScaling
                    {
                        enabled            = true,
                        healthMultPerWave  = wm.healthMultPerWave,
                        speedMultPerWave   = wm.speedMultPerWave,
                        expMultPerWave     = wm.expMultPerWave,
                        maxSpeedMultiplier = wm.maxSpeedMultiplier,
                        spawnRateAccel     = wm.spawnRateAccel,
                        maxAliveEnemies        = wm.maxAliveEnemies,
                        maxAlivePerExtraPlayer = wm.maxAlivePerExtraPlayer,
                    };

                    log.AppendLine($"  {map.name} / {tier.tier} ← สเกลศัตรู {tier.enemyScaling}");
                }

                EditorUtility.SetDirty(map);
            }

            AssetDatabase.SaveAssets();

            log.AppendLine("  ── ตารางของซีนจะไม่ถูกใช้ในรันที่เข้าผ่านล็อบบี้อีก ──");
            log.AppendLine("  แก้จังหวะเกมที่ MapData ตั้งแต่นี้ไป · ลิสต์ในซีนเหลือไว้สำหรับกด Play ที่ SampleScene ตรงๆ");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// สำเนาใหม่ทุกใบ ไม่ใช่การชี้ไปที่ object เดิม
        ///
        /// `TimelineCue` เป็น class · ถ้าแชร์ instance กัน แก้ที่ซีนแล้วแมพเปลี่ยนตาม
        /// (หรือกลับกัน) ทั้งที่ทั้งสองที่ควรเป็นอิสระต่อกันแล้ว — และ Unity จะ
        /// serialize ซ้ำสองชุดอยู่ดี ทำให้ที่เห็นใน Inspector กับที่อยู่ในไฟล์ไม่ตรงกัน
        /// </summary>
        private static TimelineCue[] CloneCues(TimelineCue[] src)
        {
            if (src == null) return new TimelineCue[0];

            return src.Where(c => c != null).Select(c => new TimelineCue
            {
                label     = c.label,
                kind      = c.kind,
                atMinutes = c.atMinutes != null ? (float[])c.atMinutes.Clone() : new float[0],
                variant   = c.variant,
            }).ToArray();
        }

        private static int CountTimes(TimelineCue[] cues)
            => cues == null ? 0 : cues.Where(c => c != null).Sum(c => c.TimeCount);
    }
}
