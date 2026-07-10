using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ควบคุม Wave System — อยู่ใน SampleScene
///
/// Wave เป็น internal logic เท่านั้น (enemy scaling / config)
/// ไม่มีการประกาศ wave ให้ผู้เล่นเห็น
/// HUD แสดงแค่ game clock นับขึ้นจาก 0:00 → 15:00
/// </summary>
public class WaveManager : NetworkBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Wave Timing")]
    [Tooltip("ดีเลย์ก่อน wave 1 จะเริ่ม (วินาที) — ให้ผู้เล่นได้ตั้งตัว")]
    public float startDelay   = 5f;
    [Tooltip("ระยะเวลาของแต่ละ wave (วินาที) ก่อนเลื่อนขึ้น scaling ถัดไป\n" +
             "60 = wave ละ 1 นาที | 90 = ช้าหน่อย")]
    public float waveDuration = 60f;

    [Header("Enemy Scaling per Wave")]
    [Tooltip("HP ของศัตรูเพิ่มขึ้นกี่ % ต่อ wave (เป็นทศนิยม)\n" +
             "0.20 = +20% ต่อ wave | wave 5 → HP × 1.80")]
    public float healthMultPerWave    = 0.20f;
    [Tooltip("ความเร็วศัตรูเพิ่มขึ้นกี่ % ต่อ wave\n" +
             "0.05 = +5% ต่อ wave | จำกัดด้วย maxSpeedMultiplier")]
    public float speedMultPerWave     = 0.05f;
    [Tooltip("EXP reward เพิ่มขึ้นกี่ % ต่อ wave\n" +
             "0.15 = +15% ต่อ wave (ชดเชย scaling ของศัตรู)")]
    public float expMultPerWave       = 0.15f;
    [Tooltip("เพดาน speed multiplier — ป้องกันศัตรูเร็วเกิน\n" +
             "2 = เร็วได้สุด 2 เท่าของ base")]
    public float maxSpeedMultiplier   = 2f;
    [Tooltip("Spawn rate เร็วขึ้นกี่ % ต่อ wave (เป็นทศนิยม)\n" +
             "0.10 = -10% ต่อ wave (interval สั้นลง)\n" +
             "wave 5 → interval × (1-0.10)^5 ≈ 0.59 ของ base")]
    public float spawnRateAccel       = 0.10f;

    [Header("Wave Configs (by wave bracket)")]
    [Tooltip("ลำดับ WaveConfig — เปลี่ยนทุก wavesPerConfig waves")]
    public WaveConfig[] waveConfigs;
    [Tooltip("กี่ wave ถึงจะเปลี่ยน config ถัดไป")]
    public int wavesPerConfig = 3;

    // ── Network Variables ─────────────────────────────────────────────────
    // Game clock อยู่ที่ GameTimeline.gameTime — ไม่ซ้ำที่นี่

    // internal only
    private NetworkVariable<int> currentWave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Server State ──────────────────────────────────────────────────────
    private EnemySpawner spawner;
    private bool         bossPaused;
    private Coroutine    waveCoroutine;

    /// <summary>Health multiplier ณ wave ปัจจุบัน — BossManager ใช้ scale mini boss HP</summary>
    public float CurrentHealthMultiplier { get; private set; } = 1f;
    /// <summary>EXP multiplier ณ wave ปัจจุบัน</summary>
    public float CurrentExpMultiplier    { get; private set; } = 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) { Debug.LogError("[WaveManager] ❌ EnemySpawner not found!"); return; }

        spawner.StopSpawning();
        waveCoroutine = StartCoroutine(WaveLoop());
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เรียกจาก BossManager เมื่อ Main Boss phase เริ่ม</summary>
    public void PauseForBoss()
    {
        if (!IsServer) return;
        bossPaused = true;
        spawner?.StopSpawning();
        if (waveCoroutine != null) { StopCoroutine(waveCoroutine); waveCoroutine = null; }
        Debug.Log("[WaveManager] ⏸ Paused for Main Boss");
    }

    // ── Wave Loop (Server-only logic, ไม่มี announcement) ────────────────
    IEnumerator WaveLoop()
    {
        // รอก่อนเริ่ม wave แรก
        yield return new WaitForSeconds(startDelay);

        while (!bossPaused)
        {
            currentWave.Value++;

            int   w           = currentWave.Value - 1;
            float healthMult  = 1f + w * healthMultPerWave;
            float speedMult   = Mathf.Min(1f + w * speedMultPerWave, maxSpeedMultiplier);
            float expMult     = 1f + w * expMultPerWave;
            float spawnRate   = Mathf.Max(
                0.3f,
                spawner.baseSpawnRate * Mathf.Pow(1f - spawnRateAccel, w)
            );

            CurrentHealthMultiplier = healthMult;   // เก็บไว้ให้ BossManager อ่าน
            CurrentExpMultiplier    = expMult;

            WaveConfig config = GetConfigForWave(currentWave.Value);
            spawner.StartSpawning(spawnRate, healthMult, speedMult, expMult, config);
            Debug.Log($"[WaveManager] Wave {currentWave.Value} — HP×{healthMult:F2} SPD×{speedMult:F2} EXP×{expMult:F2} rate:{spawnRate:F2}s");

            yield return new WaitForSeconds(waveDuration);
            if (bossPaused) yield break;

            // ไม่หยุด spawn — loop ต่อทันที แค่อัปเดต scaling ใหม่
        }
    }

    WaveConfig GetConfigForWave(int wave)
    {
        if (waveConfigs == null || waveConfigs.Length == 0) return null;
        int idx = Mathf.Min((wave - 1) / Mathf.Max(1, wavesPerConfig), waveConfigs.Length - 1);
        return waveConfigs[idx];
    }

    // ── Getters ───────────────────────────────────────────────────────────
    public int GetCurrentWave() => currentWave.Value;
}
