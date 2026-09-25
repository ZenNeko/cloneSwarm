using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// รับ event จาก GameTimeline แล้ว spawn Mini Boss / Main Boss
/// Server only — อยู่ใน SampleScene
/// </summary>
public class BossManager : NetworkBehaviour
{
    public static BossManager Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Prefab ที่มี Enemy.cs (health/movement) + BossController.cs (attacks)")]
    public GameObject mainBossPrefab;
    [Tooltip("Mini Boss prefabs — สุ่มเลือก 1 ตัวต่อครั้งที่ spawn\n" +
             "ใส่ได้หลายตัว (แต่ละตัวต้องมี BossController.cs + BossEncounterConfig)")]
    public GameObject[] miniBossPrefabs = new GameObject[0];

    [Header("Mini Boss Scaling")]
    [Tooltip("ค่า x ใน:  bossHP = enemyBaseHP  ×  x  ×  waveHealthMult")]
    public float miniBossBaseHealthMult = 5f;
    public float miniBossSpeedMult      = 1.2f;
    public float miniBossExpMult        = 4f;

    [Header("Spawn")]
    public float spawnOffsetRadius = 8f;   // spawn ห่างจาก player เท่าไร

    [Header("Events")]
    public UnityEvent onMainBossSpawned;
    public UnityEvent onMainBossKilled;

    // ── State ─────────────────────────────────────────────────────────────
    private Enemy               activeMainBossEnemy;
    private BossEncounterConfig activeBossConfig;
    private BossEncounterConfig activeMiniBossConfig;

    // กัน onDeath ยิงซ้ำ — EnemyTakeDamage ไม่มีธง "ตายแล้ว" 2 นัดในเฟรมเดียวเข้าได้ทั้งคู่
    bool _mainBossDeathHandled;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (RunSetup.Map != null)
        {
            var tierContent = RunSetup.Map.GetTier(RunSetup.Difficulty);
            if (tierContent != null)
            {
                activeBossConfig     = tierContent.mainBossConfig;
                activeMiniBossConfig = tierContent.miniBossConfig;
            }
        }

        GameTimeline.OnMiniBossTime  += SpawnMiniBoss;   // Action<string> — ชื่อ prefab ที่นัดไว้
        GameTimeline.OnMainBossTime  += SpawnMainBoss;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnMiniBossTime  -= SpawnMiniBoss;
        GameTimeline.OnMainBossTime  -= SpawnMainBoss;
    }

    // ── Dev API (เรียกจาก DevTools) ───────────────────────────────────────
    /// <summary>Force spawn mini boss (dev tool only — server only)</summary>
    public void DevSpawnMiniBoss() => SpawnMiniBoss("");

    /// <summary>Force spawn main boss (dev tool only — server only)</summary>
    public void DevSpawnMainBoss() => SpawnMainBoss();

    /// <summary>
    /// เรียกบอสใหญ่ด้วย config ที่ระบุ — ปุ่ม "ทดสอบในเกม" ของ Boss Designer (dev only · server only)
    /// ทับ config ของแมพ/prefab เฉพาะรอบนี้
    /// </summary>
    public void DevSpawnMainBoss(BossEncounterConfig config)
    {
        if (config != null) activeBossConfig = config;
        SpawnMainBoss();
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnMiniBoss(string variantId)
    {
        if (!IsServer) return;

        // รายชื่อของแมพก่อน (MapData.miniBosses) · แมพที่ไม่มีรายชื่อ = ลิสต์ในซีนแบบเดิม
        var entry = PickFromMap(variantId);
        var prefab = entry != null ? entry.prefab : PickMiniBoss(variantId);
        if (prefab == null)
        {
            Debug.LogWarning("[BossManager] miniBossPrefabs is empty — assign at least one prefab!");
            return;
        }

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(prefab, pos, Quaternion.identity);

        // ท่า: override ของระดับ > ของรายชื่อแมพ > (แบบเก่า) miniBossConfig ของระดับ > ของบน prefab
        var tierContent = RunSetup.Map != null ? RunSetup.Map.GetTier(RunSetup.Difficulty) : null;
        var config = entry != null
            ? (tierContent?.OverrideFor(entry.id) ?? entry.config ?? activeMiniBossConfig)
            : activeMiniBossConfig;
        var bc = go.GetComponent<BossController>();
        if (bc != null && config != null) bc.config = config;

        go.GetComponent<NetworkObject>()?.Spawn(true);

        // HP = baseHP × miniBossBaseHealthMult × waveHealthMult × ระดับ × จำนวนคน
        var tuning = DifficultyProfile.Current;
        float waveHealthMult  = WaveManager.Instance?.CurrentHealthMultiplier ?? 1f;
        float finalHealthMult = miniBossBaseHealthMult * waveHealthMult
                              * tuning.bossHpMult * tuning.HpForPlayers(PlayerCount());

        go.GetComponent<Enemy>()?.ApplyWaveScaling(finalHealthMult, miniBossSpeedMult);
        Debug.Log($"[BossManager] 🟡 Mini Boss [{prefab.name}] spawned — HP×{finalHealthMult:F2}");
    }

    /// <summary>
    /// เลือก mini boss — ตามชื่อถ้านัดไว้ ไม่งั้นสุ่ม
    ///
    /// เทียบด้วย **ชื่อ prefab** เพราะ `miniBossPrefabs` เป็นอาเรย์ของ prefab ล้วน
    /// ไม่มีช่อง id ให้ตั้ง · ถ้าวันหนึ่งมีบอสเยอะจนชื่อชนกัน ค่อยเปลี่ยนเป็น struct
    /// ที่มี id เหมือน `ObjectiveManager.ZoneVariant` — ตอนนี้ยังไม่คุ้มค่าโครงสร้าง
    ///
    /// หาไม่เจอแล้ว **บ่น** ไม่ใช่เงียบแล้วสุ่มแทน — ชื่อพิมพ์ผิดกับดวงไม่ดีหน้าตาเหมือนกัน
    /// </summary>
    /// <summary>
    /// มินิบอสจากรายชื่อของแมพ · id ว่าง = สุ่มจากรายชื่อ · id ไม่มีในรายชื่อ = บ่นแล้วสุ่ม
    /// แมพไม่มีรายชื่อ = null (ใช้ลิสต์ในซีน)
    /// </summary>
    MapData.MiniBossEntry PickFromMap(string variantId)
    {
        var roster = RunSetup.Map?.miniBosses;
        if (roster == null) return null;
        var valid = System.Array.FindAll(roster, m => m != null && m.prefab != null);
        if (valid.Length == 0) return null;

        if (!string.IsNullOrEmpty(variantId))
        {
            var hit = RunSetup.Map.FindMiniBoss(variantId);
            if (hit != null) return hit;
            Debug.LogWarning($"[BossManager] นัดหมายขอมินิบอส '{variantId}' แต่ไม่มีใน {RunSetup.Map.mapId}.miniBosses → สุ่มแทน");
        }
        return valid[Random.Range(0, valid.Length)];
    }

    static int PlayerCount()
    {
        var nm = NetworkManager.Singleton;
        return nm != null ? Mathf.Max(1, nm.ConnectedClientsList.Count) : 1;
    }

    GameObject PickMiniBoss(string variantId)
    {
        if (!string.IsNullOrEmpty(variantId))
        {
            if (miniBossPrefabs != null)
                foreach (var p in miniBossPrefabs)
                    if (p != null && p.name == variantId) return p;

            Debug.LogWarning($"[BossManager] นัดหมายขอบอส '{variantId}' " +
                             "แต่ไม่มีใน miniBossPrefabs → สุ่มแทน");
        }
        return PickRandomMiniBoss();
    }

    /// <summary>สุ่ม prefab จาก miniBossPrefabs (กรอง null) — null ถ้าไม่มีตัวให้สุ่ม</summary>
    GameObject PickRandomMiniBoss()
    {
        if (miniBossPrefabs == null || miniBossPrefabs.Length == 0) return null;

        // กรอง null ออก
        int validCount = 0;
        for (int i = 0; i < miniBossPrefabs.Length; i++)
            if (miniBossPrefabs[i] != null) validCount++;
        if (validCount == 0) return null;

        // สุ่มแบบ skip null
        int target = Random.Range(0, validCount);
        int seen   = 0;
        for (int i = 0; i < miniBossPrefabs.Length; i++)
        {
            if (miniBossPrefabs[i] == null) continue;
            if (seen == target) return miniBossPrefabs[i];
            seen++;
        }
        return null;
    }

    void SpawnMainBoss()
    {
        if (!IsServer || mainBossPrefab == null) return;

        // หยุด wave
        WaveManager.Instance?.PauseForBoss();

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(mainBossPrefab, pos, Quaternion.identity);

        var bc = go.GetComponent<BossController>();
        if (bc != null)
        {
            if (activeBossConfig != null) bc.config = activeBossConfig;
            bc.IsMainBoss.Value = true;   // ก่อน Spawn — ให้ค่าไปพร้อม spawn payload
        }

        go.GetComponent<NetworkObject>()?.Spawn(true);

        activeMainBossEnemy = go.GetComponent<Enemy>();

        // HP ตามระดับ × จำนวนคนตอนเกิด (ไม่ปรับกลางไฟต์ — คนหลุดแล้วหลอดกระโดดไม่ได้)
        // เดิมบอสใหญ่ไม่ถูกสเกลเลย: ทุกระดับ ทุกจำนวนคน HP เท่ากัน
        var tuning = DifficultyProfile.Current;
        float hpMult = tuning.bossHpMult * tuning.HpForPlayers(PlayerCount());
        if (activeMainBossEnemy != null && !Mathf.Approximately(hpMult, 1f))
            activeMainBossEnemy.ApplyWaveScaling(hpMult, 1f);
        if (activeMainBossEnemy != null)
        {
            _mainBossDeathHandled = false;
            activeMainBossEnemy.onDeath.AddListener(OnMainBossKilled);
        }

        MainBossSpawnedClientRpc();
        Debug.Log($"[BossManager] 🔴 MAIN BOSS spawned at {pos}");
    }

    // ── Boss Death ────────────────────────────────────────────────────────
    void OnMainBossKilled()
    {
        if (_mainBossDeathHandled) return;
        _mainBossDeathHandled = true;

        Debug.Log("[BossManager] ✅ Main Boss killed!");

        onMainBossKilled.Invoke();
        GameTimeline.Instance?.TriggerWin();
        MainBossKilledClientRpc();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Vector3 GetSpawnPosition()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count == 0) return new Vector3(20f, 0f, 20f);

        int idx = Random.Range(0, clients.Count);
        var playerPos = clients[idx].PlayerObject?.transform.position ?? Vector3.zero;

        Vector2 rand = Random.insideUnitCircle.normalized;
        return playerPos + new Vector3(rand.x, 0f, rand.y) * spawnOffsetRadius;
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc] void MainBossSpawnedClientRpc() => onMainBossSpawned.Invoke();
    [ClientRpc] void MainBossKilledClientRpc()  { /* WaveHUD / WinLoseUI จะรับจาก GameTimeline */ }
}
