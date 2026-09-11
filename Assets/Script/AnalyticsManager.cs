using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Analytics;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Persistent Singleton managing Unity Services / Analytics initialization and custom events telemetry.
/// Auto-instantiated before the first scene loads using RuntimeInitializeOnLoadMethod.
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    private playermove _localPlayer;
    private bool _initialized;
    private bool _sessionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        var go = new GameObject("AnalyticsManager");
        go.AddComponent<AnalyticsManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private async void Start()
    {
        // subscribe ก่อน await — ถ้ารอ InitializeAsync ให้เสร็จก่อนค่อยผูก
        // player ที่ spawn ระหว่างนั้นจะหลุดไปเลย แล้วไม่มี game_session_start ทั้งรัน
        playermove.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;

        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[Analytics] Unity Services Initialized successfully.");

            // Start data collection for Unity Analytics
            AnalyticsService.Instance.StartDataCollection();
            _initialized = true;
            Debug.Log("[Analytics] Data collection started.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Analytics] Initialization failed: {e.Message}");
        }

        // player อาจ spawn ไปแล้วระหว่างรอ init — ยิง session start ย้อนหลังให้
        if (_initialized && _localPlayer != null) StartSession();
    }

    private void OnDestroy()
    {
        playermove.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        if (_localPlayer != null)
        {
            _localPlayer.isDead.OnValueChanged -= HandleLocalPlayerDeadChanged;
        }
    }

    /// <summary>
    /// ชื่อตัวละครของ local player — ต้องอ่านจาก PlayerVisual.CharacterIndex เป็นหลัก
    ///
    /// PlayerWeaponManager.characterData เป็น field ที่ตั้งใน Inspector เท่านั้น
    /// ไม่มีโค้ดไหนเขียนตอนรัน (player.prefab เป็น fileID: 0) ทุก event เลยได้ "Unknown"
    /// ตลอด — เห็นใน log จริง: "game_session_start: character=Unknown"
    ///
    /// PlayerVisual.CharacterIndex เป็น NetworkVariable จึงเชื่อถือได้ทั้ง host และ client
    /// (แหล่งเดียวกับที่ TempPartyHUD ใช้โชว์ชื่อบนหัวจอ)
    /// </summary>
    private string GetLocalCharacterName()
    {
        if (_localPlayer == null) return "Unknown";

        var visual = _localPlayer.GetComponent<PlayerVisual>();
        if (visual != null)
        {
            var cd = visual.GetCharacterData(visual.CharacterIndex);
            if (cd != null && !string.IsNullOrEmpty(cd.characterName)) return cd.characterName;
        }

        var pwm = _localPlayer.GetComponent<PlayerWeaponManager>();
        if (pwm != null && pwm.characterData != null) return pwm.characterData.characterName;

        var selected = CharacterSelectUI.SelectedCharacter;
        if (selected != null && !string.IsNullOrEmpty(selected.characterName)) return selected.characterName;

        Debug.LogWarning("[Analytics] หาชื่อตัวละครไม่ได้ — event จะถูกส่งเป็น 'Unknown'");
        return "Unknown";
    }

    private void HandleLocalPlayerSpawned(Transform playerTransform)
    {
        // ปลด subscription ของ player ตัวก่อนเสมอ — เปลี่ยนซีน/รันใหม่จะได้ object ใหม่
        if (_localPlayer != null) _localPlayer.isDead.OnValueChanged -= HandleLocalPlayerDeadChanged;

        _localPlayer = playerTransform.GetComponent<playermove>();
        if (_localPlayer != null)
        {
            _localPlayer.isDead.OnValueChanged += HandleLocalPlayerDeadChanged;

            // Trigger session start event
            StartSession();
        }
    }

    private void StartSession()
    {
        if (!_initialized || _sessionStarted) return;
        _sessionStarted = true;

        string charName = GetLocalCharacterName();

        int playerCount = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : 1;

        CustomEvent sessionStartEvent = new CustomEvent("game_session_start");
        sessionStartEvent["character_name"] = charName;
        sessionStartEvent["player_count"] = playerCount;

        try
        {
            AnalyticsService.Instance.RecordEvent(sessionStartEvent);
            Debug.Log($"[Analytics] Sent game_session_start: character={charName}, players={playerCount}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Failed to send session start event: {e.Message}");
        }
    }

    private void HandleLocalPlayerDeadChanged(bool previous, bool current)
    {
        if (current) // Died
        {
            float timeOfDeath = GameTimeline.Instance != null ? GameTimeline.Instance.gameTime.Value : 0f;
            int finalLevel = SharedExperienceManager.Instance != null ? SharedExperienceManager.Instance.sharedLevel.Value : 1;

            string charName = GetLocalCharacterName();

            CustomEvent deathEvent = new CustomEvent("player_death");
            deathEvent["time_of_death"] = timeOfDeath;
            deathEvent["character_name"] = charName;
            deathEvent["final_level"] = finalLevel;

            try
            {
                AnalyticsService.Instance.RecordEvent(deathEvent);
                Debug.Log($"[Analytics] Sent player_death: time={timeOfDeath}, lvl={finalLevel}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Failed to send player death event: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Triggered by PlayerWeaponManager ClientRpc when the game ends.
    /// Calculates DPS and submits session end stats.
    /// </summary>
    public void SendSessionEndAnalytics(string[] weaponNames, float[] damages, float finalTime, int finalLevel, bool isWin)
    {
        if (!_initialized) return;

        string charName = GetLocalCharacterName();

        // Calculate DPS per weapon
        float duration = Mathf.Max(1f, finalTime);
        var weaponDpsList = new List<string>();
        
        for (int i = 0; i < Mathf.Min(weaponNames.Length, damages.Length); i++)
        {
            float dps = damages[i] / duration;
            weaponDpsList.Add($"{weaponNames[i]}:{dps:F2}");
        }

        string dpsJoined = string.Join(", ", weaponDpsList);

        CustomEvent sessionEndEvent = new CustomEvent("game_session_end");
        sessionEndEvent["is_win"] = isWin;
        sessionEndEvent["final_time"] = finalTime;
        sessionEndEvent["final_level"] = finalLevel;
        sessionEndEvent["character_name"] = charName;
        sessionEndEvent["weapon_dps"] = dpsJoined;

        try
        {
            AnalyticsService.Instance.RecordEvent(sessionEndEvent);
            Debug.Log($"[Analytics] Sent game_session_end: win={isWin}, time={finalTime}, dps={dpsJoined}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Failed to send session end event: {e.Message}");
        }

        // Analytics บัฟเฟอร์ไว้แล้วอัปโหลดเป็นรอบ (~60s) — จบเกมแล้วผู้เล่นมักปิดเกมทันที
        // ไม่ Flush จะหายทั้งก้อน
        try { AnalyticsService.Instance.Flush(); }
        catch (Exception e) { Debug.LogWarning($"[Analytics] Flush ไม่สำเร็จ: {e.Message}"); }

        // Reset session state for next run
        _sessionStarted = false;
    }
}
