using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloneSwarm.EditorTools
{
    /// <summary>
    /// ตั้งค่าเริ่มของระบบความยาก (docs/design-miniboss-roster-and-difficulty.md §10) — รันซ้ำได้ ไม่ทับของเดิม
    ///
    ///   1. DifficultyProfile 5 ไฟล์ใต้ Resources/Difficulty/ — ค่าเริ่มสำหรับเพลย์เทส ไม่ใช่ค่าที่วัดมา
    ///      HP ตามจำนวนคนใช้ตาราง Rabbit and Steel (0.9 / 1.7 / 2.4 / 3.0)
    ///   2. MapData ที่ยังไม่มีรายชื่อมินิบอส: เติมจาก BossManager.miniBossPrefabs ในซีนเกม
    ///      id = ชื่อ prefab — ตรงกับ cue.variant แบบเดิม ตารางเก่าจึงใช้ได้ต่อโดยไม่ต้องแปลง
    ///      config ว่าง = ท่าบน prefab เหมือนเดิม
    /// </summary>
    public static class DifficultySetup
    {
        const string Dir = "Assets/ScriptableObjects/Resources/Difficulty";
        const string GameScene = "Assets/GameScenes/SampleScene.unity";

        //                     tier                     hp    dmg   warn  intv  enrage? enrageT enemyHp exp
        static readonly (DifficultyTier t, float hp, float dmg, float warn, float intv, bool en, float enT, float eHp, float exp)[] Rows =
        {
            (DifficultyTier.Easy,   0.7f, 0.6f, 1.25f, 1.2f,  false, 1.3f,  0.8f, 1.0f),
            (DifficultyTier.Normal, 1.0f, 1.0f, 1.0f,  1.0f,  true,  1.0f,  1.0f, 1.0f),
            (DifficultyTier.Hard,   1.25f,1.3f, 0.9f,  0.9f,  true,  0.85f, 1.2f, 0.9f),
            (DifficultyTier.Savage, 1.5f, 1.5f, 0.85f, 0.85f, true,  0.8f,  1.4f, 0.8f),
            (DifficultyTier.Epic,   1.8f, 2.0f, 0.8f,  0.8f,  true,  0.75f, 1.6f, 0.7f),
        };

        [MenuItem("Tools/Clone Swarm/Difficulty/Create Profiles + Map Rosters")]
        public static void Run()
        {
            var log = new StringBuilder("[DifficultySetup]\n");
            CreateProfiles(log);
            FillRosters(log);
            AssetDatabase.SaveAssets();
            DifficultyProfile.ClearCache();
            Debug.Log(log.ToString());
        }

        /// <summary>batchmode: -executeMethod CloneSwarm.EditorTools.DifficultySetup.RunBatch</summary>
        public static void RunBatch() => Run();

        static void CreateProfiles(StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects/Resources", "Difficulty");

            var existing = AssetDatabase.FindAssets("t:DifficultyProfile", new[] { Dir })
                .Select(g => AssetDatabase.LoadAssetAtPath<DifficultyProfile>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null).Select(p => p.tier).ToHashSet();

            foreach (var r in Rows)
            {
                if (existing.Contains(r.t)) { log.AppendLine($"  มีแล้ว: {r.t}"); continue; }
                var p = ScriptableObject.CreateInstance<DifficultyProfile>();
                p.tier = r.t;
                p.bossHpMult = r.hp;
                p.bossDamageMult = r.dmg;
                p.bossWarningMult = r.warn;
                p.bossIntervalMult = r.intv;
                p.enrageEnabled = r.en;
                p.enrageTimeMult = r.enT;
                p.enemyHpMult = r.eHp;
                p.expMult = r.exp;
                AssetDatabase.CreateAsset(p, $"{Dir}/DifficultyProfile_{r.t}.asset");
                log.AppendLine($"  สร้าง: {r.t}  HP×{r.hp} ดาเมจ×{r.dmg} เตือน×{r.warn} ห่าง×{r.intv} enrage {(r.en ? $"×{r.enT}" : "ปิด")}");
            }
        }

        static void FillRosters(StringBuilder log)
        {
            var prefabs = SceneMiniBossPrefabs();
            if (prefabs.Length == 0) { log.AppendLine("  ไม่เจอ BossManager.miniBossPrefabs ในซีนเกม — ข้ามการเติมรายชื่อ"); return; }

            foreach (var g in AssetDatabase.FindAssets("t:MapData"))
            {
                var map = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(g));
                if (map == null) continue;
                if (map.miniBosses != null && map.miniBosses.Any(m => m != null && m.prefab != null))
                {
                    log.AppendLine($"  {map.mapId}: มีรายชื่อแล้ว");
                    continue;
                }
                Undo.RecordObject(map, "Fill Mini Boss Roster");
                map.miniBosses = prefabs.Select(p => new MapData.MiniBossEntry { id = p.name, prefab = p }).ToArray();
                EditorUtility.SetDirty(map);
                log.AppendLine($"  {map.mapId}: เติมรายชื่อ {string.Join(", ", prefabs.Select(p => p.name))}");
            }
        }

        /// <summary>อ่านจากซีนที่เปิดอยู่ · ไม่มี = เปิดซีนเกมแบบ additive อ่านแล้วปิด (ไม่แตะซีนที่ผู้ใช้เปิด)</summary>
        static GameObject[] SceneMiniBossPrefabs()
        {
            var bm = Object.FindAnyObjectByType<BossManager>(FindObjectsInactive.Include);
            if (bm != null) return bm.miniBossPrefabs.Where(p => p != null).ToArray();

            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
            try
            {
                bm = scene.GetRootGameObjects()
                          .Select(r => r.GetComponentInChildren<BossManager>(true))
                          .FirstOrDefault(b => b != null);
                return bm != null ? bm.miniBossPrefabs.Where(p => p != null).ToArray() : new GameObject[0];
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
