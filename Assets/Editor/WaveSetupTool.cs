using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Swarm > Setup Wave Configs
///
/// สร้าง WaveConfig ScriptableObject 5 ชุด ครอบคลุม 15 นาที (15 waves @ 60s/wave)
///
/// Wave timeline (WaveManager defaults: waveDuration=60, wavesPerConfig=3):
///   WaveConfig_Early    wave  1– 3  (  0:00 –  3:00)  Cube เต็ม
///   WaveConfig_Mid      wave  4– 6  (  3:00 –  6:00)  Cube+Skull มาผสม
///   WaveConfig_Late     wave  7– 9  (  6:00 –  9:00)  Skull ครอง
///   WaveConfig_PreBoss  wave 10–12  (  9:00 – 12:00)  Skull หนัก + spawn เร็ว
///   WaveConfig_Chaos    wave 13–15  ( 12:00 – 15:00)  Skull เกือบทั้งหมด + override rate 0.5s
///
/// Spawn rate จาก WaveManager formula:  rate = baseRate × 0.9^(wave-1)
///   wave  1 → 1.50s   wave  5 → 1.02s   wave 10 → 0.66s   wave 15 → 0.43s
///
/// หลังรัน:
///   → assign WaveConfig 5 ตัวตามลำดับใน WaveManager.waveConfigs[]
///   → ตั้ง WaveManager.wavesPerConfig = 3
/// </summary>
public static class WaveSetupTool
{
    const string OUT_DIR        = "Assets/ScriptableObjects/Waves";
    const string ENEMY_DIR      = "Assets/prefab/Enemy";
    const string CUBE_NAME      = "Cube";
    const string SKULL_NAME     = "SkullBonesBoss";

    // ── Wave bracket definition ────────────────────────────────────────────
    struct WaveDef
    {
        public string fileName;
        public string label;
        public int    cubeWeight;
        public int    skullWeight;
        public float  spawnOverride;   // 0 = ใช้ WaveManager formula
        public string notes;
    }

    static readonly WaveDef[] DEFS = new WaveDef[]
    {
        new() {
            fileName      = "WaveConfig_Early",
            label         = "Early (Wave 1–3)",
            cubeWeight    = 10,
            skullWeight   = 0,
            spawnOverride = 0f,
            notes         = "Cube เท่านั้น | HP×1.0–1.4 | rate ~1.5–1.2s\n" +
                            "ผู้เล่นรู้จัก mechanics และ unlocked weapon แรก"
        },
        new() {
            fileName      = "WaveConfig_Mid",
            label         = "Mid (Wave 4–6)",
            cubeWeight    = 7,
            skullWeight   = 3,
            spawnOverride = 0f,
            notes         = "Skull 30% | HP×1.6–2.0 | rate ~1.1–0.9s\n" +
                            "ผู้เล่นเจอ Mini Boss แรกที่ 5 นาที"
        },
        new() {
            fileName      = "WaveConfig_Late",
            label         = "Late (Wave 7–9)",
            cubeWeight    = 4,
            skullWeight   = 6,
            spawnOverride = 0f,
            notes         = "Skull 60% | HP×2.2–2.6 | rate ~0.8–0.7s\n" +
                            "Mini Boss ที่ 10 นาที — ความดุร้ายสูง"
        },
        new() {
            fileName      = "WaveConfig_PreBoss",
            label         = "Pre-Boss (Wave 10–12)",
            cubeWeight    = 2,
            skullWeight   = 8,
            spawnOverride = 0f,
            notes         = "Skull 80% | HP×2.8–3.2 | rate ~0.65–0.55s\n" +
                            "ระยะสุดท้ายก่อน Main Boss — ความดันสูงสุด"
        },
        new() {
            fileName      = "WaveConfig_Chaos",
            label         = "Chaos (Wave 13–15)",
            cubeWeight    = 1,
            skullWeight   = 9,
            spawnOverride = 0.5f,
            notes         = "Skull 90% | HP×3.4–3.8 | rate FORCED 0.5s\n" +
                            "Chaos phase — spawn เร็วกว่า formula\n" +
                            "Main Boss เริ่มที่ 15:00 → waves หยุดอัตโนมัติ"
        },
    };

    [MenuItem("Tools/Swarm/Setup Wave Configs")]
    public static void Setup()
    {
        // ── Load enemy prefabs ─────────────────────────────────────────────
        GameObject cubePrefab  = LoadPrefab(CUBE_NAME,  ENEMY_DIR);
        GameObject skullPrefab = LoadPrefab(SKULL_NAME, ENEMY_DIR);

        if (cubePrefab == null)
        {
            EditorUtility.DisplayDialog("Error",
                $"❌ ไม่พบ prefab '{CUBE_NAME}' ใน {ENEMY_DIR}\n" +
                "ตรวจสอบชื่อ prefab แล้วรันใหม่", "OK");
            return;
        }

        System.IO.Directory.CreateDirectory(OUT_DIR);

        int created = 0;
        string log  = "";

        foreach (var def in DEFS)
        {
            string path = $"{OUT_DIR}/{def.fileName}.asset";

            // ลบของเดิมถ้ามี
            AssetDatabase.DeleteAsset(path);

            var cfg = ScriptableObject.CreateInstance<WaveConfig>();
            cfg.spawnIntervalOverride = def.spawnOverride;

            // สร้าง EnemyEntry list
            var entries = new System.Collections.Generic.List<WaveConfig.EnemyEntry>();

            if (def.cubeWeight > 0 && cubePrefab != null)
                entries.Add(new WaveConfig.EnemyEntry { prefab = cubePrefab, weight = def.cubeWeight });

            if (def.skullWeight > 0 && skullPrefab != null)
                entries.Add(new WaveConfig.EnemyEntry { prefab = skullPrefab, weight = def.skullWeight });
            else if (def.skullWeight > 0 && skullPrefab == null)
                log += $"⚠ {def.fileName}: Skull prefab ไม่พบ — ใช้ Cube แทน\n";

            cfg.enemies = entries.ToArray();

            AssetDatabase.CreateAsset(cfg, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string warnBlock = log.Length > 0 ? $"\nคำเตือน:\n{log}" : "";
        EditorUtility.DisplayDialog(
            "Wave Configs Created",
            $"✅ สร้างสำเร็จ {created}/{DEFS.Length} configs\n" +
            $"📁 {OUT_DIR}/\n" +
            warnBlock +
            "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            "ขั้นตอนต่อไป — ตั้งค่า WaveManager:\n\n" +
            "  waveConfigs[0]  = WaveConfig_Early\n" +
            "  waveConfigs[1]  = WaveConfig_Mid\n" +
            "  waveConfigs[2]  = WaveConfig_Late\n" +
            "  waveConfigs[3]  = WaveConfig_PreBoss\n" +
            "  waveConfigs[4]  = WaveConfig_Chaos\n\n" +
            "  wavesPerConfig  = 3\n" +
            "  waveDuration    = 60 s\n" +
            "  healthMultPerWave = 0.20\n" +
            "  speedMultPerWave  = 0.05\n" +
            "  spawnRateAccel    = 0.10\n" +
            "  maxSpeedMult      = 2.0",
            "OK");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    static GameObject LoadPrefab(string nameContains, string dir)
    {
        string[] guids = AssetDatabase.FindAssets($"{nameContains} t:Prefab", new[] { dir });
        if (guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
