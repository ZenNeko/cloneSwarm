using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// นาฬิกาเกม + จัดตาราง event ทั้งหมด
///
/// Schedule:
///   ครั้งแรก: objectiveStartMin / miniBossStartMin (วินาทีที่จะ spawn ครั้งแรก)
///   ต่อๆ ไป: objectiveIntervalMin / miniBossIntervalMin (ทุกๆ N นาที)
///   ตอน mainBossTimeMin → Main Boss (wave หยุด)
///   Kill Main Boss → WIN | ผู้เล่นทุกคนตาย → LOSE
/// </summary>
public class GameTimeline : NetworkBehaviour
{
    public static GameTimeline Instance { get; private set; }

    [Header("Zone Objective Schedule (minutes)")]
    [Tooltip("เวลาที่ objective ตัวแรกจะ spawn (นาทีจากเริ่มเกม)")]
    public float objectiveStartMin    = 2f;
    [Tooltip("ระยะห่างระหว่าง objective แต่ละครั้ง (นาที)")]
    public float objectiveIntervalMin = 2f;

    [Header("Mini Boss Schedule (minutes)")]
    [Tooltip("เวลาที่ mini boss ตัวแรกจะ spawn (นาทีจากเริ่มเกม)")]
    public float miniBossStartMin     = 5f;
    [Tooltip("ระยะห่างระหว่าง mini boss แต่ละครั้ง (นาที)")]
    public float miniBossIntervalMin  = 5f;

    [Header("Main Boss")]
    [Tooltip("เวลาที่ Main Boss spawn (นาที) — wave จะหยุด")]
    public float mainBossTimeMin      = 15f;

    [Header("Rewards (Zone Objective)")]
    [Tooltip("EXP โบนัสที่ให้เมื่อเสร็จ objective")]
    public float objectiveExpReward   = 80f;
    [Tooltip("HP ที่ฟื้นให้ผู้เล่นทุกคน")]
    public float objectiveHealAmount  = 20f;

    [Header("Start Gate")]
    [Tooltip("รอให้ทุก client ที่ต่ออยู่มี player object ก่อน จึงเริ่มนับเวลาและปล่อย wave")]
    public bool  waitForAllPlayers = true;
    [Tooltip("รอนานสุดกี่วินาทีก่อนเริ่มเองแม้ยังไม่ครบ — กันเกมค้างถ้ามีใครโหลดไม่จบหรือหลุดกลางทาง")]
    public float startWaitTimeout  = 20f;

    // ── Network Variables ─────────────────────────────────────────────────
    public NetworkVariable<float> gameTime       = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>รันเริ่มจริงแล้วหรือยัง — WaveManager รอตัวนี้ก่อนปล่อยศัตรู
    /// เป็น NetworkVariable เพื่อให้ HUD ฝั่ง client รู้ด้วยว่ายังอยู่ช่วงรอโหลด</summary>
    public NetworkVariable<bool>  hasStarted      = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isMainBossPhase = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events ─────────────────────────────────────────────────────
    /// <summary>ถึงเวลา spawn Zone Objective</summary>
    public static event Action            OnObjectiveTime;
    /// <summary>ถึงเวลา spawn Mini Boss</summary>
    public static event Action            OnMiniBossTime;
    /// <summary>ถึงเวลา spawn Main Boss — wave จะหยุด</summary>
    public static event Action            OnMainBossTime;
    /// <summary>ชนะ — (gameTimeSec, level)</summary>
    public static event Action<float,int> OnGameWon;
    /// <summary>แพ้ — (gameTimeSec, level)</summary>
    public static event Action<float,int> OnGameLost;

    // ── Server State ──────────────────────────────────────────────────────
    private float nextObjectiveAt;
    private float nextMiniBossAt;
    private bool  mainBossSpawned;
    private bool  gameEnded;
    private float _loseCheckTimer;
    private float _startWaitTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        nextObjectiveAt = objectiveStartMin * 60f;
        nextMiniBossAt  = miniBossStartMin  * 60f;
        Debug.Log($"[GameTimeline] เริ่ม — Objective: first {objectiveStartMin}m every {objectiveIntervalMin}m | " +
                  $"MiniBoss: first {miniBossStartMin}m every {miniBossIntervalMin}m | MainBoss: {mainBossTimeMin}m");
    }

    // ── Server Update ─────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || gameEnded) return;

        // ยังไม่เริ่ม = ไม่นับเวลา · ของเดิมนาฬิกาเดินตั้งแต่ OnNetworkSpawn ของตัวเอง
        // ซึ่งเกิดก่อนที่ GameSessionManager จะ spawn player เสร็จ ทำให้ศัตรูออกมาก่อน
        // ผู้เล่นโผล่ และ CheckLoseCondition ก็ประกาศแพ้ตั้งแต่ยังไม่ได้เล่น
        if (!hasStarted.Value) { TryBeginRun(); return; }

        gameTime.Value += Time.deltaTime;
        float t = gameTime.Value;

        // Zone Objective
        if (t >= nextObjectiveAt)
        {
            nextObjectiveAt = t + objectiveIntervalMin * 60f;
            TriggerObjectiveClientRpc();
            Debug.Log($"[GameTimeline] Zone Objective — t={FormatTime(t)}");
        }

        // Mini Boss
        if (t >= nextMiniBossAt)
        {
            nextMiniBossAt = t + miniBossIntervalMin * 60f;
            TriggerMiniBossClientRpc();
            Debug.Log($"[GameTimeline] Mini Boss — t={FormatTime(t)}");
        }

        // Main Boss
        if (!mainBossSpawned && t >= mainBossTimeMin * 60f)
        {
            mainBossSpawned       = true;
            isMainBossPhase.Value = true;
            TriggerMainBossClientRpc();
            Debug.Log($"[GameTimeline] MAIN BOSS — t={FormatTime(t)}");
        }

        // Lose Check (all PlayerObjects null = all dead) — ทุก 2 วิ
        // เดิมใช้ FloorToInt(t) % 2 == 0 ซึ่งเป็นจริงทุกเฟรมตลอดวินาทีคู่ → รัน ~60-120 ครั้งแทนที่จะเป็น 1
        if (t > 3f)
        {
            _loseCheckTimer += Time.deltaTime;
            if (_loseCheckTimer >= 2f)
            {
                _loseCheckTimer = 0f;
                CheckLoseCondition();
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เรียกจาก BossManager เมื่อ Main Boss ตาย → WIN</summary>
    public void TriggerWin()
    {
        if (!IsServer || gameEnded) return;
        gameEnded             = true;
        isMainBossPhase.Value = false;
        SendAllFinalStats(true);
        GameWonClientRpc(gameTime.Value, GetLevel());
        Debug.Log($"[GameTimeline] ✅ WIN — t={FormatTime(gameTime.Value)}");
    }

    // ── Server Helpers ────────────────────────────────────────────────────
    /// <summary>
    /// ประตูเริ่มรัน — เปิดเมื่อทุก client ที่ต่ออยู่มี player object แล้ว
    ///
    /// มี timeout กันค้าง เพราะถ้ามีใครโหลดไม่จบหรือหลุดระหว่าง sync
    /// การรอแบบไม่มีที่สิ้นสุดจะทำให้คนที่เหลือติดอยู่ในจอเปล่า
    ///
    /// เช็คเฉพาะตอนยังไม่เริ่ม — คน join ทีหลังจึงไม่ทำให้เกมหยุดนับเวลาใหม่
    /// </summary>
    void TryBeginRun()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        int connected = 0, ready = 0;
        foreach (var c in nm.ConnectedClientsList)
        {
            connected++;
            if (c.PlayerObject != null) ready++;
        }

        _startWaitTimer += Time.deltaTime;

        bool enough = waitForAllPlayers ? (connected > 0 && ready >= connected)
                                        : ready > 0;

        if (!enough)
        {
            if (_startWaitTimer < startWaitTimeout) return;
            Debug.LogWarning($"[GameTimeline] รอครบ {startWaitTimeout:F0}s แล้วยังพร้อมแค่ {ready}/{connected} คน — เริ่มไปก่อน");
        }

        hasStarted.Value = true;
        Debug.Log($"[GameTimeline] ▶ เริ่มนับเวลา — ผู้เล่นพร้อม {ready}/{connected} คน (รอไป {_startWaitTimer:F1}s)");
    }

    void CheckLoseCondition()
    {
        if (NetworkManager.Singleton == null) return;

        // ต้องแยก "ยังไม่เกิด" ออกจาก "เกิดแล้วตาย" — ของเดิมเหมารวมเป็นแพ้ทั้งคู่
        // GameSessionManager spawn player หลังซีนโหลดเสร็จ ซึ่งช้ากว่านาฬิกาเกมเริ่มเดิน
        // เช็คแรกเกิดที่ t≈4s จึงเจอ PlayerObject เป็น null ทั้งหมด แล้วประกาศแพ้
        // ทั้งที่ผู้เล่นยังไม่ทันโผล่ (เห็นใน log: LOSE t=00:04 ก่อน spawn 2 วินาที)
        int spawned = 0;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = c.PlayerObject;
            if (po == null) continue;

            var pm = po.GetComponent<playermove>();
            if (pm == null) continue;

            spawned++;
            if (!pm.isDead.Value) return;   // มีคนรอดอยู่
        }

        if (spawned == 0) return;           // ยังไม่มีใครเกิดเลย ไม่ใช่ตายหมด

        gameEnded = true;
        SendAllFinalStats(false);
        GameLostClientRpc(gameTime.Value, GetLevel());
        Debug.Log($"[GameTimeline] ❌ LOSE — t={FormatTime(gameTime.Value)}");
    }

    void SendAllFinalStats(bool isWin)
    {
        if (NetworkManager.Singleton == null) return;
        float finalTime = gameTime.Value;
        int finalLevel = GetLevel();
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pwm = c.PlayerObject?.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.SendFinalStats(finalTime, finalLevel, isWin);
            }
        }
    }

    int GetLevel() => SharedExperienceManager.Instance?.GetCurrentLevel() ?? 0;

    static string FormatTime(float s)
    {
        int m = Mathf.FloorToInt(s / 60f);
        int sec = Mathf.FloorToInt(s % 60f);
        return $"{m:00}:{sec:00}";
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    [ClientRpc] void TriggerObjectiveClientRpc()         => OnObjectiveTime?.Invoke();
    [ClientRpc] void TriggerMiniBossClientRpc()          => OnMiniBossTime?.Invoke();
    [ClientRpc] void TriggerMainBossClientRpc()          => OnMainBossTime?.Invoke();
    [ClientRpc] void GameWonClientRpc(float t, int lvl)  => OnGameWon?.Invoke(t, lvl);
    [ClientRpc] void GameLostClientRpc(float t, int lvl) => OnGameLost?.Invoke(t, lvl);

    // ── Getters ───────────────────────────────────────────────────────────
    public float GetGameTime()      => gameTime.Value;
    public bool  IsMainBossPhase()  => isMainBossPhase.Value;
    public string GetFormattedTime() => FormatTime(gameTime.Value);
}
