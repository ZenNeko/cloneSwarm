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

        GameTimeline.OnMiniBossTime  += SpawnMiniBoss;
        GameTimeline.OnMainBossTime  += SpawnMainBoss;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnMiniBossTime  -= SpawnMiniBoss;
        GameTimeline.OnMainBossTime  -= SpawnMainBoss;
    }

    // ── Dev API (เรียกจาก DevTools) ───────────────────────────────────────
    /// <summary>Force spawn mini boss (dev tool only — server only)</summary>
    public void DevSpawnMiniBoss() => SpawnMiniBoss();

    /// <summary>Force spawn main boss (dev tool only — server only)</summary>
    public void DevSpawnMainBoss() => SpawnMainBoss();

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnMiniBoss()
    {
        if (!IsServer) return;

        var prefab = PickRandomMiniBoss();
        if (prefab == null)
        {
            Debug.LogWarning("[BossManager] miniBossPrefabs is empty — assign at least one prefab!");
            return;
        }

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(prefab, pos, Quaternion.identity);

        if (activeMiniBossConfig != null)
        {
            var bc = go.GetComponent<BossController>();
            if (bc != null) bc.config = activeMiniBossConfig;
        }

        go.GetComponent<NetworkObject>()?.Spawn(true);

        // HP = baseHP × miniBossBaseHealthMult × waveHealthMult
        float waveHealthMult  = WaveManager.Instance?.CurrentHealthMultiplier ?? 1f;
        float finalHealthMult = miniBossBaseHealthMult * waveHealthMult;

        go.GetComponent<Enemy>()?.ApplyWaveScaling(finalHealthMult, miniBossSpeedMult);
        Debug.Log($"[BossManager] 🟡 Mini Boss [{prefab.name}] spawned — HP×{finalHealthMult:F2}");
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

        if (activeBossConfig != null)
        {
            var bc = go.GetComponent<BossController>();
            if (bc != null) bc.config = activeBossConfig;
        }

        go.GetComponent<NetworkObject>()?.Spawn(true);

        activeMainBossEnemy = go.GetComponent<Enemy>();
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
