using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawner ที่รับคำสั่งจาก WaveManager — ไม่มี logic wave ของตัวเอง
/// </summary>
public class EnemySpawner : NetworkBehaviour
{
    [Header("Prefabs")]
    public GameObject enemyPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Spawn interval เริ่มต้น (วินาที) — WaveManager จะลดลงทุก wave")]
    public float baseSpawnRate = 1.5f;
    [Tooltip("รัศมีที่ spawn รอบผู้เล่น")]
    public float spawnRadius   = 12f;

    // ── Wave-controlled params (Server only) ──────────────────────────────
    private float      currentHealthMult = 1f;
    private float      currentSpeedMult  = 1f;
    private float      currentExpMult    = 1f;
    private WaveConfig currentConfig;

    public override void OnNetworkSpawn()
    {
        // WaveManager เรียก StartSpawning() เอง — ไม่ spawn ทันที
    }

    public override void OnNetworkDespawn()
    {
        CancelInvoke(nameof(SpawnEnemy));
    }

    // ── API สำหรับ WaveManager ─────────────────────────────────────────────
    public void StartSpawning(float spawnRate, float healthMult, float speedMult, float expMult = 1f, WaveConfig config = null)
    {
        if (!IsServer) return;
        currentHealthMult = healthMult;
        currentSpeedMult  = speedMult;
        currentExpMult    = expMult;
        currentConfig     = config;

        CancelInvoke(nameof(SpawnEnemy));
        InvokeRepeating(nameof(SpawnEnemy), 0.5f, spawnRate);
        Debug.Log($"[EnemySpawner] Spawning started — rate:{spawnRate:F2}s HP×{healthMult:F2} SPD×{speedMult:F2} EXP×{expMult:F2}");
    }

    public void StopSpawning()
    {
        CancelInvoke(nameof(SpawnEnemy));
        Debug.Log("[EnemySpawner] Spawning stopped");
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnEnemy()
    {
        if (!IsServer) return;

        Transform spawnNear = GetRandomPlayerTransform();
        if (spawnNear == null) return;

        Vector2 rand = Random.insideUnitCircle.normalized;
        Vector3 pos  = spawnNear.position + new Vector3(rand.x, 0f, rand.y) * spawnRadius;

        // เลือก prefab จาก WaveConfig ถ้ามี ไม่งั้นใช้ default
        GameObject prefab = currentConfig?.PickRandomPrefab() ?? enemyPrefab;
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        go.GetComponent<NetworkObject>()?.Spawn(true);

        // Apply wave scaling หลัง Spawn (OnNetworkSpawn set base health แล้ว)
        go.GetComponent<Enemy>()?.ApplyWaveScaling(currentHealthMult, currentSpeedMult, currentExpMult);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetRandomPlayerTransform()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients.Count == 0) return null;
        return clients[Random.Range(0, clients.Count)].PlayerObject?.transform;
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.red;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, spawnRadius);
#endif
    }
}
