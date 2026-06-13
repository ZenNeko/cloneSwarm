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

    // ── Network Variables ─────────────────────────────────────────────────
    public NetworkVariable<float> gameTime       = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
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
        if (!mainBossSpawned && t >= nextMiniBossAt)
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
        if (!mainBossSpawned && Mathf.FloorToInt(t) % 2 == 0 && t > 3f)
            CheckLoseCondition();
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
    void CheckLoseCondition()
    {
        if (NetworkManager.Singleton == null) return;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pm = c.PlayerObject?.GetComponent<playermove>();
            if (pm != null && !pm.isDead.Value) return;   // มีคนรอดอยู่
        }

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
