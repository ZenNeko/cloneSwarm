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

        // Subscribe to player spawn
        playermove.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;
    }

    private void OnDestroy()
    {
        playermove.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        if (_localPlayer != null)
        {
            _localPlayer.isDead.OnValueChanged -= HandleLocalPlayerDeadChanged;
        }
    }

    private void HandleLocalPlayerSpawned(Transform playerTransform)
    {
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

        string charName = "Unknown";
        var pwm = _localPlayer.GetComponent<PlayerWeaponManager>();
        if (pwm != null && pwm.characterData != null)
        {
            charName = pwm.characterData.characterName;
        }

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

            string charName = "Unknown";
            var pwm = _localPlayer.GetComponent<PlayerWeaponManager>();
            if (pwm != null && pwm.characterData != null)
            {
                charName = pwm.characterData.characterName;
            }

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

        string charName = "Unknown";
        if (_localPlayer != null)
        {
            var pwm = _localPlayer.GetComponent<PlayerWeaponManager>();
            if (pwm != null && pwm.characterData != null)
            {
                charName = pwm.characterData.characterName;
            }
        }

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

        // Reset session state for next run
        _sessionStarted = false;
    }
}
