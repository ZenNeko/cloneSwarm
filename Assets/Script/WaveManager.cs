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

    [Header("Wave Configs — ช่วงตามนาที (แนะนำ)")]
    // ตั้งแล้วจะแทน waveConfigs + wavesPerConfig ข้างล่างทั้งคู่
    // แมพก็ตั้งทับได้อีกที ผ่าน MapData.TierContent.schedule.wavePhases
    [Tooltip("ช่วงของ WaveConfig ตามนาที · ว่าง = ใช้แบบแบ่งตาม wave ข้างล่าง")]
    public WavePhase[] wavePhases = new WavePhase[0];

    [Header("Wave Configs (แบบเดิม — แบ่งตามจำนวน wave)")]
    [Tooltip("ลำดับ WaveConfig — เปลี่ยนทุก wavesPerConfig waves · ใช้เมื่อ wavePhases ว่าง")]
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

    // นาฬิกาสำรองสำหรับซีนที่ไม่มี GameTimeline (เช่น WeaponTestScene) เท่านั้น
    // เดินเฉพาะเมื่อไม่มีนาฬิกาจริงให้อ่าน — ไม่ใช่เรือนที่สองที่เดินคู่กันไป
    private float _fallbackClock;

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

        // จบรันแล้วต้องหยุดปล่อยศัตรู — สมัครก่อนหา spawner เพื่อให้เข้าคู่กับ
        // OnNetworkDespawn เสมอ ไม่ว่าจะออกทางไหน
        GameTimeline.OnGameWon  += OnRunEnded;
        GameTimeline.OnGameLost += OnRunEnded;

        spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null) { Debug.LogError("[WaveManager] ❌ EnemySpawner not found!"); return; }

        spawner.StopSpawning();

        var phases = ActivePhases();
        Debug.Log(phases != null && phases.Length > 0
            ? $"[WaveManager] config ตามนาที {phases.Length} ช่วง"
            : $"[WaveManager] config แบ่งตาม wave — ทุก {wavesPerConfig} wave");

        waveCoroutine = StartCoroutine(WaveLoop());
    }

    public override void OnNetworkDespawn()
    {
        GameTimeline.OnGameWon  -= OnRunEnded;
        GameTimeline.OnGameLost -= OnRunEnded;
    }

    /// <summary>
    /// รันจบแล้ว — ทั้งชนะและแพ้ใช้ทางเดียวกัน
    ///
    /// ═══ ขาแพ้เคยไม่มีใครหยุด wave ═══
    ///
    /// จุดเดียวที่เคยหยุดคือ `PauseForBoss` ซึ่ง BossManager เรียกตอนบอสใหญ่ออก ·
    /// ขาชนะจึงปลอดภัยโดยบังเอิญ (wave ถูกหยุดไปตั้งแต่บอสออกแล้ว) แต่ขาแพ้
    /// `GameTimeline` แค่ตั้ง `gameEnded` แล้วหยุด Update ของตัวเอง ส่วน WaveManager
    /// ไม่เคยรู้เรื่อง — ศัตรูเกิดเพิ่มเรื่อยๆ หลังจอผลลัพธ์จนกว่าจะมีคนกดออก
    /// และ `EnemySpawner` ไม่มีเพดานจำนวน
    ///
    /// event ยิงบนทุก client ผ่าน ClientRpc · `StopWaves` กัน IsServer ไว้แล้ว
    /// </summary>
    void OnRunEnded(float gameTimeSec, int level) => StopWaves("รันจบแล้ว");

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เรียกจาก BossManager เมื่อ Main Boss phase เริ่ม</summary>
    public void PauseForBoss() => StopWaves("บอสใหญ่ออก");

    /// <summary>
    /// หยุดปล่อยศัตรูสำหรับรันนี้ — เรียกซ้ำได้ ไม่มีทางกลับไปเดินต่อ
    ///
    /// ไม่ล้างศัตรูที่เกิดไปแล้ว · ตัวที่อยู่ในสนามยังอยู่ต่อจนกว่าจะเปลี่ยนซีน
    /// ซึ่งถูกแล้วสำหรับขาชนะ (ยังมีไฟต์บอสใหญ่อยู่) และไม่สำคัญสำหรับขาแพ้
    /// </summary>
    public void StopWaves(string reason)
    {
        if (!IsServer || bossPaused) return;

        bossPaused = true;
        spawner?.StopSpawning();
        if (waveCoroutine != null) { StopCoroutine(waveCoroutine); waveCoroutine = null; }
        Debug.Log($"[WaveManager] ⏸ หยุด wave — {reason}");
    }

    // ── Wave Loop (Server-only logic, ไม่มี announcement) ────────────────
    /// <summary>
    /// เลข wave เป็น **ฟังก์ชันของนาฬิกาเกม** ไม่ใช่ตัวนับของตัวเอง
    ///
    /// ═══ ของเดิมเดินนาฬิกาเรือนที่สอง ═══
    ///
    /// loop เก่าสะสมเวลาด้วย `WaitForSeconds(waveDuration)` แยกจาก
    /// `GameTimeline.gameTime` — สองเรือนตอบคำถามเดียวกันว่า "รันไปถึงไหนแล้ว"
    /// เริ่มพร้อมกันเพราะทั้งคู่รอ `hasStarted` แต่หลังจากนั้นไม่มีอะไรผูกให้ตรงกัน
    ///
    /// ราคาที่แพงกว่าการคลาดเคลื่อนคือ **ตารางของ wave มองไม่เห็น** · ตอนนี้
    /// wave N คือช่วงเวลาที่คำนวณกลับไปมาได้ ไม่ใช่ผลของลำดับ yield ที่ผ่านมา
    /// </summary>
    IEnumerator WaveLoop()
    {
        // ไม่ปล่อยศัตรูจนกว่านาฬิกาเกมจะเริ่มจริง (GameTimeline รอให้ทุกคน spawn เสร็จก่อน)
        // ของเดิม wave เริ่มนับจาก OnNetworkSpawn ของตัวเอง ศัตรูจึงออกมาก่อนผู้เล่นโหลดเสร็จ
        // เช็ค Instance != null ด้วย เพราะซีนอย่าง WeaponTestScene ไม่มี GameTimeline
        while (GameTimeline.Instance != null && !GameTimeline.Instance.hasStarted.Value)
            yield return null;

        int applied = 0;

        while (!bossPaused)
        {
            float t = TickRunTime();
            int   w = WaveAt(t);

            if (w != applied && w > 0)
            {
                applied           = w;
                currentWave.Value = w;
                ApplyWave(w, t);
            }

            yield return null;
        }
    }

    /// <summary>
    /// เวลาของรันตอนนี้ — เรียก **เฟรมละครั้ง** เท่านั้น (มันเดินนาฬิกาสำรองด้วย)
    /// ซีนที่ไม่มี GameTimeline จึงยังมี wave ได้ แทนที่จะค้างอยู่ที่ wave 0
    /// </summary>
    float TickRunTime()
    {
        var gt = GameTimeline.Instance;
        if (gt != null) return gt.GetGameTime();

        _fallbackClock += Time.deltaTime;
        return _fallbackClock;
    }

    /// <summary>
    /// wave ที่ควรอยู่ ณ เวลานี้ — 0 = ยังไม่ถึง wave แรก
    ///
    /// กัน waveDuration ที่เป็น 0 หรือติดลบไว้ด้วย · ของเดิมค่านั้นทำให้
    /// `WaitForSeconds(0)` วน wave ขึ้นทุกเฟรมจนสุดเกม โดยไม่มีอะไรบอกว่าเกิดขึ้น
    /// </summary>
    int WaveAt(float t)
    {
        if (t < startDelay) return 0;
        return Mathf.FloorToInt((t - startDelay) / Mathf.Max(0.1f, waveDuration)) + 1;
    }

    void ApplyWave(int wave, float t)
    {
        int   w          = wave - 1;
        float healthMult = 1f + w * healthMultPerWave;
        float speedMult  = Mathf.Min(1f + w * speedMultPerWave, maxSpeedMultiplier);
        float expMult    = 1f + w * expMultPerWave;
        float spawnRate  = Mathf.Max(
            0.3f,
            spawner.baseSpawnRate * Mathf.Pow(1f - spawnRateAccel, w)
        );

        CurrentHealthMultiplier = healthMult;   // เก็บไว้ให้ BossManager อ่าน
        CurrentExpMultiplier    = expMult;

        spawner.StartSpawning(spawnRate, healthMult, speedMult, expMult, GetConfigFor(wave, t));
        Debug.Log($"[WaveManager] Wave {wave} @ {t / 60f:0.0}m — " +
                  $"HP×{healthMult:F2} SPD×{speedMult:F2} EXP×{expMult:F2} rate:{spawnRate:F2}s");
    }

    // ── เลือก WaveConfig ─────────────────────────────────────────────────
    /// <summary>ช่วงตามนาทีที่ใช้จริง — แมพก่อน แล้วค่อยซีน · ว่าง = ใช้แบบแบ่งตาม wave</summary>
    WavePhase[] ActivePhases()
    {
        var sched = RunSetup.Map != null ? RunSetup.Map.GetSchedule(RunSetup.Difficulty) : null;
        if (sched != null && sched.wavePhases != null && sched.wavePhases.Length > 0)
            return sched.wavePhases;
        return wavePhases;
    }

    /// <summary>
    /// ช่วงที่เวลาน้อยสุด **ครอบตั้งแต่เริ่มเกมเสมอ** ไม่ว่าจะตั้งนาทีไว้เท่าไร
    ///
    /// ถ้าปล่อยให้เวลาก่อนช่วงแรกไม่มี config จะเกิดช่องว่างที่ต้องถอยไปใช้กลไก
    /// อีกแบบ แล้วรันเดียวกันจะมีสองกติกาทำงานคนละช่วง ซึ่งอธิบายยากกว่ากติกาเดียว
    /// ที่ขอบเขตชัด
    /// </summary>
    WaveConfig GetConfigFor(int wave, float timeSec)
    {
        var phases = ActivePhases();
        if (phases != null && phases.Length > 0)
        {
            float minutes = timeSec / 60f;

            WaveConfig current = null, earliest = null;
            float bestAt = float.NegativeInfinity, earliestAt = float.PositiveInfinity;

            foreach (var p in phases)
            {
                if (p.config == null) continue;

                if (p.atMinutes < earliestAt) { earliestAt = p.atMinutes; earliest = p.config; }
                if (p.atMinutes <= minutes && p.atMinutes >= bestAt) { bestAt = p.atMinutes; current = p.config; }
            }

            if (current != null) return current;
            if (earliest != null) return earliest;

            // ทุกช่องว่าง config — บอกออกมา ไม่ใช่คืน null เงียบๆ แล้วศัตรูไม่ออก
            Debug.LogWarning("[WaveManager] wavePhases มีแต่ช่องที่ยังไม่ใส่ config → ใช้แบบแบ่งตาม wave แทน");
        }

        return GetConfigForWave(wave);
    }

    WaveConfig GetConfigForWave(int wave)
    {
        WaveConfig[] activeConfigs = waveConfigs;

        if (RunSetup.Map != null)
        {
            var tierContent = RunSetup.Map.GetTier(RunSetup.Difficulty);
            if (tierContent != null && tierContent.wavesByPhase != null && tierContent.wavesByPhase.Length > 0)
            {
                activeConfigs = tierContent.wavesByPhase;
            }
        }

        if (activeConfigs == null || activeConfigs.Length == 0) return null;
        int idx = Mathf.Min((wave - 1) / Mathf.Max(1, wavesPerConfig), activeConfigs.Length - 1);
        return activeConfigs[idx];
    }

    // ── Getters ───────────────────────────────────────────────────────────
    public int GetCurrentWave() => currentWave.Value;
}
