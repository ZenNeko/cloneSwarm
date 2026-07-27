using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// EXP และ Level เป็นของกลาง — ทุก player share กัน
///
/// Upgrade Phase Flow:
///   Server: Level Up → BeginUpgradePhaseClientRpc → รอ PlayerUpgradePickedServerRpc ทุกคน
///   Client: pause (timeScale=0) → แสดง card UI → เลือก → call ServerRpc → รอ EndUpgradePhaseClientRpc
///   Timer:  ถ้าหมดเวลา → ForceAutoPickClientRpc → auto-pick → EndUpgradePhase
/// </summary>
public class SharedExperienceManager : NetworkBehaviour
{
    public static SharedExperienceManager Instance { get; private set; }

    [Header("Level Settings")]
    public int   maxLevel       = 30;
    public float baseExpToLevel = 100f;
    public float expGrowthRate  = 1.25f;

    [Header("Upgrade Phase")]
    [Tooltip("วินาทีที่ให้แต่ละคนเลือก card (0 = ไม่มีกำหนด)")]
    public float upgradePickSeconds = 30f;

    // ── Network Variables ─────────────────────────────────────────────────
    public NetworkVariable<float> sharedExp       = new(0f,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   sharedLevel     = new(1,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> sharedExpToNext = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> expMultiplier   = new(1f,   NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events ─────────────────────────────────────────────────────
    /// <summary>EXP bar UI — ยิงบนทุก client เมื่อ EXP เปลี่ยน</summary>
    public static event Action<float, float> OnSharedExpChanged;
    /// <summary>Level counter UI — ยิงบนทุก client เมื่อ level เปลี่ยน</summary>
    public static event Action<int>          OnSharedLevelChanged;

    // ── Upgrade Phase Events (ยิงบนทุก client) ────────────────────────────
    /// <summary>เริ่ม upgrade phase — UpgradeManager แสดง card, หยุดเกม</summary>
    public static event Action<int>      OnUpgradePhaseStart;    // (newLevel)
    /// <summary>ทุกคนเลือกครบ / หมดเวลา — กลับเข้าเกม</summary>
    public static event Action           OnUpgradePhaseEnd;
    /// <summary>Tick ทุก 1 วินาที → แสดง countdown</summary>
    public static event Action<float>    OnTimerTick;            // (remainingSeconds)
    /// <summary>หมดเวลา → UpgradeManager ต้อง auto-pick ทันที</summary>
    public static event Action           OnForceAutoPick;
    /// <summary>อัปเดตจำนวนคนที่เลือกแล้ว → UI "X / Y players"</summary>
    public static event Action<int, int> OnPickedCountChanged;   // (picked, total)

    // ── Orb Reward Events ─────────────────────────────────────────────────
    /// <summary>ผู้เล่นเก็บ Objective Orb — ทุกคนได้ level-up card 1 ใบจาก weapon/stat ที่ตัวเองมี</summary>
    public static event Action OnOrbPhaseStart;

    // ── Server-side State ─────────────────────────────────────────────────
    private Queue<int>     pendingLevels    = new();
    private Queue<ulong>   pendingOrbs      = new();
    private bool           isUpgradePhase   = false;
    private bool           isOrbPhase       = false;
    private HashSet<ulong> pickedPlayers    = new();
    private Coroutine      timerCoroutine;
    private ulong          orbCollectorId   = ulong.MaxValue; // client ที่เก็บ orb

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            sharedLevel.Value     = 1;
            sharedExpToNext.Value = CalcExpToNext(1);
            sharedExp.Value       = 0f;
            expMultiplier.Value   = 1f;
        }

        // Subscribe NetworkVariable callbacks
        sharedExp.OnValueChanged       += HandleExpChanged;
        sharedExpToNext.OnValueChanged += HandleExpToNextChanged;
        sharedLevel.OnValueChanged     += HandleLevelChanged;

        // ยิงค่าเริ่มต้นให้ UI
        OnSharedExpChanged?.Invoke(sharedExp.Value, sharedExpToNext.Value);
    }

    public override void OnNetworkDespawn()
    {
        sharedExp.OnValueChanged       -= HandleExpChanged;
        sharedExpToNext.OnValueChanged -= HandleExpToNextChanged;
        sharedLevel.OnValueChanged     -= HandleLevelChanged;
    }

    void HandleExpChanged(float _, float v)       => OnSharedExpChanged?.Invoke(v, sharedExpToNext.Value);
    void HandleExpToNextChanged(float _, float v) => OnSharedExpChanged?.Invoke(sharedExp.Value, v);
    void HandleLevelChanged(int _, int v)         => OnSharedLevelChanged?.Invoke(v);

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Server only — เรียกจาก ExpOrb</summary>
    public void AddExp(float amount)
    {
        if (!IsServer)         return;
        if (isUpgradePhase || isOrbPhase) return;   // ระหว่าง upgrade / orb phase ไม่รับ EXP
        if (sharedLevel.Value >= maxLevel) return;

        float exp = sharedExp.Value + amount * expMultiplier.Value;

        while (exp >= sharedExpToNext.Value && sharedLevel.Value < maxLevel)
        {
            exp -= sharedExpToNext.Value;
            sharedLevel.Value++;
            sharedExpToNext.Value = CalcExpToNext(sharedLevel.Value);
            pendingLevels.Enqueue(sharedLevel.Value);
            Debug.Log($"[SharedEXP] Level Up! → {sharedLevel.Value}");
        }

        sharedExp.Value = exp;

        ProcessNextPhase();
    }

    /// <summary>UpgradeManager เรียกหลังเลือก ExpBonus card</summary>
    [Rpc(SendTo.Server)]
    public void ApplyExpMultiplierServerRpc(float newMultiplier)
    {
        // Guard against client-side EXP multiplier injection (1.0x to 5.0x max)
        expMultiplier.Value = Mathf.Clamp(newMultiplier, 1f, 5f);
        Debug.Log($"[SharedEXP] ExpMultiplier → {expMultiplier.Value:F2}x");
    }

    /// <summary>UpgradeManager เรียกหลังเลือก card เสร็จ — ส่ง Server รู้ว่า player นี้พร้อมแล้ว</summary>
    [Rpc(SendTo.Server)]
    public void PlayerUpgradePickedServerRpc(RpcParams rpcParams = default)
    {
        if (!isUpgradePhase) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        pickedPlayers.Add(sender);

        int total = NetworkManager.ConnectedClients.Count;
        NotifyPickedCountClientRpc(pickedPlayers.Count, total);
        Debug.Log($"[SharedEXP] Player {sender} เลือกแล้ว → {pickedPlayers.Count}/{total}");

        if (pickedPlayers.Count >= total)
            CompleteUpgradePhase();
    }

    // ── Upgrade Phase: Server Logic ───────────────────────────────────────

    void ProcessNextPhase()
    {
        if (isUpgradePhase || isOrbPhase) return;

        if (pendingLevels.Count > 0)
        {
            StartNextUpgradePhase();
        }
        else if (pendingOrbs.Count > 0)
        {
            StartNextOrbPhase();
        }
    }

    void StartNextUpgradePhase()
    {
        if (pendingLevels.Count == 0) return;

        int level = pendingLevels.Dequeue();
        isUpgradePhase = true;
        pickedPlayers.Clear();

        int total = NetworkManager.ConnectedClients.Count;
        BeginUpgradePhaseClientRpc(level, total);

        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(UpgradeTimerCoroutine());
    }

    void CompleteUpgradePhase()
    {
        if (timerCoroutine != null) { StopCoroutine(timerCoroutine); timerCoroutine = null; }
        isUpgradePhase = false;
        pickedPlayers.Clear();
        EndUpgradePhaseClientRpc();

        // ถ้ายังมี level หรือ orb ที่รอ → เริ่ม phase ถัดไป
        if (pendingLevels.Count > 0 || pendingOrbs.Count > 0)
            StartCoroutine(DelayNextPhase());
    }

    IEnumerator DelayNextPhase()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        ProcessNextPhase();
    }

    IEnumerator UpgradeTimerCoroutine()
    {
        if (upgradePickSeconds <= 0f) yield break;   // ไม่มีกำหนดเวลา

        float remaining = upgradePickSeconds;
        while (remaining > 0f)
        {
            UpdateTimerClientRpc(remaining);
            yield return new WaitForSecondsRealtime(1f);   // ใช้ realtime เพราะ timeScale = 0
            remaining -= 1f;
        }

        UpdateTimerClientRpc(0f);
        ForceAutoPickClientRpc();                          // แจ้งทุก client auto-pick
        yield return new WaitForSecondsRealtime(0.5f);    // รอให้ client จัดการก่อน
        CompleteUpgradePhase();
    }

    // ── ClientRpc ────────────────────────────────────────────────────────

    [ClientRpc]
    void BeginUpgradePhaseClientRpc(int level, int totalPlayers)
    {
        Time.timeScale = 0f;
        OnPickedCountChanged?.Invoke(0, totalPlayers);
        OnUpgradePhaseStart?.Invoke(level);
        Debug.Log($"[SharedEXP] Upgrade Phase เริ่ม — Level {level} | รอ {totalPlayers} ผู้เล่น");
    }

    [ClientRpc]
    void EndUpgradePhaseClientRpc()
    {
        Time.timeScale = 1f;
        OnUpgradePhaseEnd?.Invoke();
        Debug.Log("[SharedEXP] Upgrade Phase สิ้นสุด — เกมกลับมา");
    }

    [ClientRpc]
    void UpdateTimerClientRpc(float remaining)
    {
        OnTimerTick?.Invoke(remaining);
    }

    [ClientRpc]
    void ForceAutoPickClientRpc()
    {
        OnForceAutoPick?.Invoke();
        Debug.Log("[SharedEXP] หมดเวลา — auto-pick!");
    }

    [ClientRpc]
    void NotifyPickedCountClientRpc(int picked, int total)
    {
        OnPickedCountChanged?.Invoke(picked, total);
    }

    // ── Orb Phase ─────────────────────────────────────────────────────────

    /// <summary>
    /// เรียกจาก ObjectiveOrb (server-side) — แสดง card เฉพาะ collector
    /// </summary>
    /// <summary>เรียกจาก ObjectiveOrb (server-side) — ทุกคนได้ card พร้อมกัน</summary>
    public void StartOrbPhaseForPlayer(ulong collectorClientId)
    {
        if (!IsServer) return;
        pendingOrbs.Enqueue(collectorClientId);
        ProcessNextPhase();
    }

    void StartNextOrbPhase()
    {
        if (pendingOrbs.Count == 0) return;

        ulong collectorClientId = pendingOrbs.Dequeue();
        isOrbPhase     = true;
        orbCollectorId = collectorClientId;   // เก็บไว้ log เท่านั้น
        pickedPlayers.Clear();

        int total = NetworkManager.ConnectedClients.Count;
        BeginOrbPhaseClientRpc(total);   // broadcast ทุกคน

        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        timerCoroutine = StartCoroutine(OrbTimerCoroutine());
        Debug.Log($"[OrbPhase] Client {collectorClientId} เริ่ม Orb Phase — ทุกคนได้ card");
    }

    [ClientRpc]
    void BeginOrbPhaseClientRpc(int totalPlayers)
    {
        Time.timeScale = 0f;
        OnPickedCountChanged?.Invoke(0, totalPlayers);
        OnOrbPhaseStart?.Invoke();
    }

    /// <summary>UpgradeManager เรียกหลังเลือก card — รอทุกคนเลือกครบ</summary>
    [Rpc(SendTo.Server)]
    public void PlayerOrbPickedServerRpc(RpcParams rpcParams = default)
    {
        if (!isOrbPhase) return;
        pickedPlayers.Add(rpcParams.Receive.SenderClientId);
        int total = NetworkManager.ConnectedClients.Count;
        NotifyPickedCountClientRpc(pickedPlayers.Count, total);
        if (pickedPlayers.Count >= total) CompleteOrbPhase();
    }

    void CompleteOrbPhase()
    {
        if (timerCoroutine != null) { StopCoroutine(timerCoroutine); timerCoroutine = null; }
        isOrbPhase     = false;
        orbCollectorId = ulong.MaxValue;
        pickedPlayers.Clear();
        EndUpgradePhaseClientRpc();   // reuse same "resume" ClientRpc (broadcast ทุกคน)

        // ถ้ายังมี level หรือ orb ที่รอ → เริ่ม phase ถัดไป
        if (pendingLevels.Count > 0 || pendingOrbs.Count > 0)
            StartCoroutine(DelayNextPhase());
    }

    IEnumerator OrbTimerCoroutine()
    {
        if (upgradePickSeconds <= 0f) yield break;
        float remaining = upgradePickSeconds;
        while (remaining > 0f)
        {
            UpdateTimerClientRpc(remaining);
            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
        }
        UpdateTimerClientRpc(0f);
        ForceAutoPickClientRpc();
        yield return new WaitForSecondsRealtime(0.5f);
        CompleteOrbPhase();
    }

    // ── Getters ───────────────────────────────────────────────────────────
    public float GetExpPercent()   => sharedExpToNext.Value > 0 ? sharedExp.Value / sharedExpToNext.Value : 1f;
    public float GetCurrentExp()   => sharedExp.Value;
    public float GetExpToNext()    => sharedExpToNext.Value;
    public int   GetCurrentLevel() => sharedLevel.Value;

    float CalcExpToNext(int level) =>
        Mathf.Floor(baseExpToLevel * Mathf.Pow(expGrowthRate, level - 1));
}
