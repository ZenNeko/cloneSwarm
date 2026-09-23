using System.Collections;
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

    // Elite ถูกถอดออกจาก spawner แล้ว (2026-09-24) — โค้ดกับ asset ยังเก็บไว้ที่ Script/Elite/
    // ไม่เคยเกิดจริง (eliteSpawnRate ในซีน = 0) และทางเดิมใส่ EliteController ซึ่งเป็น
    // NetworkBehaviour หลัง Spawn() ฝั่ง server อย่างเดียว — NGO ไม่รองรับ client จะไม่มี component นั้น
    // ถ้าจะทำใหม่: ใส่ EliteController ไว้ใน prefab ศัตรูตั้งแต่แรก แล้วตั้ง modifier ก่อน Spawn

    // ── Wave-controlled params (Server only) ──────────────────────────────
    private float      currentHealthMult = 1f;
    private float      currentSpeedMult  = 1f;
    private float      currentExpMult    = 1f;
    private WaveConfig currentConfig;
    private Coroutine  _spawnLoop;

    // ── เพดานจำนวน (Server only) ──────────────────────────────────────────
    // นับเฉพาะตัวที่ spawner นี้ปล่อย — บอส/มินิบอสมาจาก BossManager จึงไม่นับและไม่ถูกกัน
    // ตัวที่ตายออกจากลิสต์ผ่าน Enemy.OnEnemyDiedServer · ตัวที่ถูก despawn ทางอื่น (ล้างซีน ฯลฯ)
    // ถูกกวาดทิ้งตอนนับ — ลิสต์จึงไม่พึ่ง event อย่างเดียว
    private readonly System.Collections.Generic.List<Enemy> _alive = new();
    private static readonly System.Predicate<Enemy> IsGone = e => e == null || !e.IsSpawned;
    private EnemyScaling _capSource;
    private bool _capReachedLogged;

    /// <summary>ศัตรูปกติที่มีชีวิตอยู่ตอนนี้ (server) — สำหรับ DevTools / log</summary>
    public int AliveCount { get { _alive.RemoveAll(IsGone); return _alive.Count; } }

    /// <summary>เพดาน ณ ตอนนี้ตามจำนวนผู้เล่นที่ต่ออยู่ · 0 = ไม่จำกัด</summary>
    public int CurrentAliveCap =>
        _capSource == null || NetworkManager.Singleton == null ? 0
        : _capSource.AliveCapFor(NetworkManager.Singleton.ConnectedClientsList.Count);

    /// <summary>WaveManager ส่งสเกลที่ใช้จริงมาทุก wave (แมพหรือซีน)</summary>
    public void SetAliveCap(EnemyScaling source) => _capSource = source;

    public override void OnNetworkSpawn() => Enemy.OnEnemyDiedServer += OnEnemyDied;

    void OnEnemyDied(Enemy e, Vector3 _) => _alive.Remove(e);

    // WaveManager เรียก StartSpawning() เอง — ไม่ spawn ทันทีใน OnNetworkSpawn

    public override void OnNetworkDespawn()
    {
        Enemy.OnEnemyDiedServer -= OnEnemyDied;
        StopSpawning();
        _alive.Clear();
    }

    // ── API สำหรับ WaveManager ─────────────────────────────────────────────
    public void StartSpawning(float spawnRate, float healthMult, float speedMult, float expMult = 1f, WaveConfig config = null)
    {
        if (!IsServer) return;
        currentHealthMult = healthMult;
        currentSpeedMult  = speedMult;
        currentExpMult    = expMult;
        currentConfig     = config;

        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(SpawnLoop(spawnRate));
        Debug.Log($"[EnemySpawner] Spawning started — rate:{spawnRate:F2}s HP×{healthMult:F2} SPD×{speedMult:F2} EXP×{expMult:F2}");
    }

    public void StopSpawning()
    {
        if (_spawnLoop != null) { StopCoroutine(_spawnLoop); _spawnLoop = null; }
        Debug.Log("[EnemySpawner] Spawning stopped");
    }

    /// <summary>แทน InvokeRepeating — ยิงครั้งแรกหลัง 0.5s แล้วทุก rate วินาที (timing เดิมเป๊ะ)</summary>
    IEnumerator SpawnLoop(float rate)
    {
        yield return new WaitForSeconds(0.5f);

        var wait = new WaitForSeconds(rate);   // cache กัน GC alloc ทุกรอบ
        while (true)
        {
            SpawnEnemy();
            yield return wait;
        }
    }

    // ── Spawn boost API (ใช้โดย ZoneObjective Survive quest ฯลฯ) ─────────
    /// <summary>จำนวน enemy เพิ่มต่อ tick (1 = double, 2 = triple) — server only</summary>
    [HideInInspector] public int extraSpawnsPerTick = 0;

    /// <summary>เพิ่ม spawn count per tick ชั่วคราว (เรียก ClearSpawnBoost เพื่อยกเลิก)</summary>
    public void BoostSpawn(int extraPerTick)
    {
        if (!IsServer) return;
        extraSpawnsPerTick = Mathf.Max(0, extraPerTick);
        Debug.Log($"[EnemySpawner] 🚀 Spawn boost: +{extraSpawnsPerTick} per tick");
    }

    public void ClearSpawnBoost()
    {
        if (!IsServer) return;
        extraSpawnsPerTick = 0;
        Debug.Log("[EnemySpawner] Spawn boost cleared");
    }

    // ── Spawn ─────────────────────────────────────────────────────────────
    void SpawnEnemy()
    {
        if (!IsServer) return;

        // base spawn 1 ครั้ง + extra (boost)
        int total = 1 + extraSpawnsPerTick;

        // ถึงเพดาน = ข้ามรอบนี้ ไม่ฆ่าตัวเก่า (ผู้เล่นอาจกำลังตีอยู่) · ลูปยังเดินต่อ
        // พอฆ่าลดลงก็ปล่อยตัวใหม่ได้เองในรอบถัดไป
        int cap = CurrentAliveCap;
        if (cap > 0)
        {
            int room = cap - AliveCount;
            if (room <= 0)
            {
                if (!_capReachedLogged)
                {
                    _capReachedLogged = true;
                    Debug.Log($"[EnemySpawner] ถึงเพดาน {cap} ตัว — หยุดปล่อยจนกว่าจะลดลง");
                }
                return;
            }
            _capReachedLogged = false;
            total = Mathf.Min(total, room);
        }

        for (int i = 0; i < total; i++)
            DoSpawnOnce();
    }

    void DoSpawnOnce()
    {
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
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.ApplyWaveScaling(currentHealthMult, currentSpeedMult, currentExpMult);
            _alive.Add(enemy);
        }
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
