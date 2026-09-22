using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// สร้างโซนเควสต์อีกแบบที่ให้ **augment orb** แล้วลงทะเบียนเข้า <c>ObjectiveManager</c>
    /// เมนู: Tools > Clone Swarm > Wire Augment Objective Zone
    ///
    /// ═══ เป็น prefab variant ไม่ใช่ prefab ใหม่ ═══
    ///
    /// กติกาของโซน (เวลาผนึก · จำนวนของที่ต้องส่ง · ศัตรูที่เพิ่ม · รางวัล EXP ·
    /// ระยะเวลา fail) เหมือนกันทุกอย่างกับโซนปกติ — ต่างกัน **ช่องเดียว** คือ
    /// `orbPrefab` ที่ให้ orb ธรรมดา หรือ orb ที่ตั้ง reward = Augment
    ///
    /// variant ทำให้การจูนกติกาที่ตัวแม่ไหลลงมาที่แบบนี้เอง · ถ้าก๊อปเป็น prefab
    /// เต็มใบ วันหนึ่งจะจูนโซนปกติแล้วลืมจูนโซน augment แล้วสองแบบเล่นไม่เหมือนกัน
    /// โดยไม่มีใครตั้งใจ — บั๊กคลาสเดียวกับแถบ build ที่เคยมีสองอัน
    ///
    /// ═══ น้ำหนักที่ตั้งให้ และทำไม ═══
    ///
    /// ปกติ 3 : augment 1 — โซน augment ออกราวหนึ่งในสี่ครั้ง
    ///
    /// **ไม่ใช่ตัวเลขที่พิสูจน์แล้ว** เป็นจุดตั้งต้นให้จูนต่อ · augment มีทางได้อยู่แล้ว
    /// จากเลเวลที่กำหนด (3/7/12/18) ทางนี้เป็นของแถม ไม่ควรกลายเป็นทางหลัก
    ///
    /// รันซ้ำได้ — มีอยู่แล้วก็แค่เช็คสายให้ ไม่ทับน้ำหนักที่จูนไว้
    /// </summary>
    public static class AugmentZoneWirer
    {
        private const string ScenePath   = "Assets/GameScenes/SampleScene.unity";
        private const string BaseZone    = "Assets/Prefab/ZoneObjective.prefab";
        private const string AugZone     = "Assets/Prefab/ZoneObjective_Augment.prefab";
        private const string AugOrbPath  = "Assets/Prefab/Exp orb/AugOrb.prefab";

        private const float NormalWeight  = 3f;
        private const float AugmentWeight = 1f;
        private const string AugmentId    = "augment";

        /// <summary>
        /// นาทีที่นัดโซน augment ไว้ — ค่าตั้งต้นตามที่เจ้าของโปรเจกต์ระบุ
        /// ปรับต่อได้ที่ `GameTimeline.cues` ใน Inspector โดยไม่ต้องรันตัวนี้อีก
        /// </summary>
        private static readonly float[] AugmentMinutes = { 3f, 8f, 12f };

        [MenuItem("Tools/Clone Swarm/Wire Augment Objective Zone")]
        public static void Wire()
        {
            var log = new StringBuilder("[โซน augment]\n");

            var augZone = EnsureVariant(log);
            if (augZone == null) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var om = scene.GetRootGameObjects()
                          .SelectMany(r => r.GetComponentsInChildren<ObjectiveManager>(true))
                          .FirstOrDefault();

            if (om == null) { Debug.LogError("[โซน augment] หา ObjectiveManager ไม่เจอในซีน"); return; }

            var baseZone = om.zoneObjectivePrefab
                        ?? AssetDatabase.LoadAssetAtPath<GameObject>(BaseZone);

            // ลงทะเบียนแล้วก็ยังต้องไปตั้งนัดหมายต่อ — **ห้าม return ตรงนี้**
            // รอบแรกเขียนไว้ให้ออกเลย แล้วนัดหมายไม่เคยถูกตั้งเพราะ variant
            // ถูกลงทะเบียนไปตั้งแต่รอบก่อนหน้า · "ทำครบแล้ว" ของแต่ละส่วนไม่เท่ากัน
            bool registered = om.zoneVariants != null &&
                              om.zoneVariants.Any(v => v.prefab == augZone);

            if (registered) log.AppendLine("  zoneVariants ลงทะเบียนไว้แล้ว");
            else
            {
                om.zoneVariants = new[]
                {
                    new ObjectiveManager.ZoneVariant { id = "normal",   prefab = baseZone, weight = NormalWeight },
                    new ObjectiveManager.ZoneVariant { id = AugmentId,  prefab = augZone,  weight = AugmentWeight },
                };
                EditorUtility.SetDirty(om);
                log.AppendLine($"  zoneVariants → normal {NormalWeight} : {AugmentId} {AugmentWeight}");
            }

            // id อาจยังว่างถ้า variant ถูกลงทะเบียนตั้งแต่ก่อนมีช่อง id
            EnsureIds(om, augZone, baseZone, log);
            EnsureCue(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  บันทึก {ScenePath}");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// เติม id ให้ช่องที่ยังว่าง — นัดหมายอ้างแบบด้วย id ไม่ใช่ด้วย prefab
        /// ช่อง id เพิ่งมีทีหลัง ลิสต์ที่ลงทะเบียนไว้ก่อนหน้าจึงเป็นสตริงว่างทั้งคู่
        /// แล้วนัดหมายจะหาแบบไม่เจอ (บ่นออกมา แต่ยังสุ่มแทน)
        /// </summary>
        private static void EnsureIds(ObjectiveManager om, GameObject augZone,
                                      GameObject baseZone, StringBuilder log)
        {
            if (om.zoneVariants == null) return;

            bool changed = false;
            for (int i = 0; i < om.zoneVariants.Length; i++)
            {
                var v = om.zoneVariants[i];
                if (!string.IsNullOrEmpty(v.id)) continue;

                if (v.prefab == augZone)      v.id = AugmentId;
                else if (v.prefab == baseZone) v.id = "normal";
                else continue;

                om.zoneVariants[i] = v;
                changed = true;
                log.AppendLine($"  เติม id '{v.id}' ให้ {v.prefab.name}");
            }
            if (changed) EditorUtility.SetDirty(om);
        }

        /// <summary>
        /// นัดโซน augment ไว้ที่นาทีที่กำหนด
        ///
        /// **น้ำหนักกับนัดหมายทำงานคนละหน้าที่** — น้ำหนักตอบว่า "ถ้าสุ่ม จะออกบ่อยแค่ไหน"
        /// นัดหมายตอบว่า "นาทีนี้ต้องออกแน่นอน" · มีทั้งคู่ได้ และควรมีทั้งคู่:
        /// นัดหมายรับประกันจังหวะที่ออกแบบไว้ ส่วนน้ำหนักทำให้รอบอื่นยังมีลุ้น
        ///
        /// ไม่ทับ cue ที่มีอยู่แล้ว — ตั้งเวลาเองไว้แล้วรันซ้ำก็ไม่หาย
        /// </summary>
        private static void EnsureCue(Scene scene, StringBuilder log)
        {
            var gt = scene.GetRootGameObjects()
                          .SelectMany(r => r.GetComponentsInChildren<GameTimeline>(true))
                          .FirstOrDefault();
            if (gt == null) { log.AppendLine("  ไม่เจอ GameTimeline — ข้ามการตั้งนัดหมาย"); return; }

            if (gt.cues != null && gt.cues.Any(c => c != null && c.variant == AugmentId))
            {
                log.AppendLine("  นัดหมายโซน augment มีอยู่แล้ว ไม่ทับ");
                return;
            }

            var cue = new TimelineCue
            {
                label     = "โซน augment",
                kind      = TimelineCueKind.ZoneObjective,
                atMinutes = (float[])AugmentMinutes.Clone(),
                variant   = AugmentId,
            };

            var list = gt.cues != null ? gt.cues.ToList() : new System.Collections.Generic.List<TimelineCue>();
            list.Add(cue);
            gt.cues = list.ToArray();
            EditorUtility.SetDirty(gt);

            log.AppendLine($"  นัดหมายโซน augment → นาที {string.Join(", ", AugmentMinutes)}");
        }

        /// <summary>
        /// สร้าง variant ถ้ายังไม่มี แล้วชี้ orbPrefab ไปที่ AugOrb
        ///
        /// **ไม่ทับของที่มีอยู่** — ถ้า variant มีแล้วก็แค่เช็คว่า orb ถูกใบไหม
        /// </summary>
        private static GameObject EnsureVariant(StringBuilder log)
        {
            var orb = AssetDatabase.LoadAssetAtPath<GameObject>(AugOrbPath);
            if (orb == null)
            {
                Debug.LogError($"[โซน augment] ไม่เจอ {AugOrbPath} — " +
                               "รัน Tools > Clone Swarm > Wire Augment Orb ก่อน");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AugZone);
            if (existing == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(BaseZone);
                if (src == null)
                {
                    Debug.LogError($"[โซน augment] ไม่เจอโซนต้นแบบ {BaseZone}");
                    return null;
                }

                // variant ไม่ใช่สำเนา — ของที่ไม่ได้ override จะตามตัวแม่ตลอดไป
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                existing = PrefabUtility.SaveAsPrefabAsset(inst, AugZone);
                Object.DestroyImmediate(inst);
                log.AppendLine($"  สร้าง variant {AugZone}");
            }

            var zone = existing.GetComponent<ZoneObjective>();
            if (zone == null)
            {
                Debug.LogError("[โซน augment] variant ไม่มี ZoneObjective component");
                return null;
            }

            if (zone.orbPrefab != orb)
            {
                var root = PrefabUtility.LoadPrefabContents(AugZone);
                try
                {
                    var z = root.GetComponent<ZoneObjective>();
                    z.orbPrefab = orb;
                    PrefabUtility.SaveAsPrefabAsset(root, AugZone);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }

                log.AppendLine("  orbPrefab → AugOrb.prefab");
                existing = AssetDatabase.LoadAssetAtPath<GameObject>(AugZone);
            }

            return existing;
        }
    }
}
