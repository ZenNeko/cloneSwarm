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
    [Tooltip("Prefab ที่มี Enemy.cs (health/movement) + MainBoss.cs (attacks)")]
    public GameObject mainBossPrefab;
    [Tooltip("Mini Boss Type A — Chase AoE mechanic (ต้องมี MiniBossAI.cs + MiniBossConfig ChaseAoE)")]
    public GameObject miniBossPrefab;
    [Tooltip("Mini Boss Type B — Floor Hazard mechanic (ต้องมี MiniBossAI.cs + MiniBossConfig FloorHazard)")]
    public GameObject miniBossPrefab_B;

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
    private Enemy activeMainBossEnemy;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameTimeline.OnMiniBossTime  += SpawnMiniBoss;
        GameTimeline.OnMainBossTime  += SpawnMainBoss;
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnMiniBossTime  -= SpawnMiniBoss;
        GameTimeline.OnMainBossTime  -= SpawnMainBoss;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnMiniBoss()
    {
        if (!IsServer) return;

        // สลับสุ่มระหว่าง Type A (ChaseAoE) และ Type B (FloorHazard)
        GameObject prefab = PickMiniBossPrefab();
        if (prefab == null) return;

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.GetComponent<NetworkObject>()?.Spawn(true);

        // HP = baseHP × miniBossBaseHealthMult × waveHealthMult
        float waveHealthMult  = WaveManager.Instance?.CurrentHealthMultiplier ?? 1f;
        float finalHealthMult = miniBossBaseHealthMult * waveHealthMult;

        go.GetComponent<Enemy>()?.ApplyWaveScaling(finalHealthMult, miniBossSpeedMult);
        Debug.Log($"[BossManager] 🟡 Mini Boss [{prefab.name}] spawned — HP×{finalHealthMult:F2}");
    }

    GameObject PickMiniBossPrefab()
    {
        bool hasA = miniBossPrefab   != null;
        bool hasB = miniBossPrefab_B != null;

        if (hasA && hasB) return Random.value < 0.5f ? miniBossPrefab : miniBossPrefab_B;
        if (hasA) return miniBossPrefab;
        if (hasB) return miniBossPrefab_B;

        Debug.LogWarning("[BossManager] No mini boss prefab assigned!");
        return null;
    }

    void SpawnMainBoss()
    {
        if (!IsServer || mainBossPrefab == null) return;

        // หยุด wave
        WaveManager.Instance?.PauseForBoss();

        Vector3 pos = GetSpawnPosition();
        var go = Instantiate(mainBossPrefab, pos, Quaternion.identity);
        go.GetComponent<NetworkObject>()?.Spawn(true);

        activeMainBossEnemy = go.GetComponent<Enemy>();
        if (activeMainBossEnemy != null)
            activeMainBossEnemy.onDeath.AddListener(OnMainBossKilled);

        MainBossSpawnedClientRpc();
        Debug.Log($"[BossManager] 🔴 MAIN BOSS spawned at {pos}");
    }

    // ── Boss Death ────────────────────────────────────────────────────────
    void OnMainBossKilled()
    {
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
