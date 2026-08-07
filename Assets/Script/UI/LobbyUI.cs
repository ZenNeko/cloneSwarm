using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CloneSwarm.Meta;

public class LobbyUI : MonoBehaviour
{
    [Header("TabBar & Maps")]
    public TabBar tabBar;
    public List<MapData> maps = new();

    [Header("Party UI")]
    public Transform partyContainer;
    public GameObject partyRowTemplate;

    [Header("Controls")]
    public Button readyButton;
    public TextMeshProUGUI readyButtonText;
    public Button startRunButton;

    [Header("Selection Labels")]
    public TextMeshProUGUI mapNameLabel;
    public TextMeshProUGUI difficultyLabel;

    [Header("Invite & Network Controls")]
    public Button inviteButton;
    public GameObject busyOverlay;
    public TextMeshProUGUI busyText;

    [Header("Room & Join Controls")]
    public TextMeshProUGUI roomCodeLabel;
    public Button copyCodeButton;
    public TMP_InputField lobbyJoinCodeInput;
    public Button lobbyJoinButton;

    private readonly List<GameObject> spawnedPartyRows = new();
    private LobbyState lastLobbyState;

    private void Start()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        if (startRunButton != null)
        {
            startRunButton.onClick.AddListener(OnStartRunClicked);
        }

        if (inviteButton != null)
        {
            inviteButton.onClick.AddListener(OnInviteClicked);
        }

        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        }

        if (lobbyJoinButton != null)
        {
            lobbyJoinButton.onClick.AddListener(OnLobbyJoinClicked);
        }
    }

    private void OnEnable()
    {
        LobbyState.OnLobbyChanged += Refresh;
        CharacterSelectUI.OnCharacterConfirmed += OnCharacterPicked;

        // รหัสห้องอ่านจาก GameSessionManager.CurrentSession ไม่ใช่จาก LobbyState
        // จึงต้องฟังสถานะ session ตรงๆ ไม่งั้นสร้างห้องสำเร็จแล้ว UI ไม่วาดใหม่
        GameSessionManager.OnSessionJoined += OnSessionChanged;
        GameSessionManager.OnSessionLeft   += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        LobbyState.OnLobbyChanged -= Refresh;
        CharacterSelectUI.OnCharacterConfirmed -= OnCharacterPicked;

        GameSessionManager.OnSessionJoined -= OnSessionChanged;
        GameSessionManager.OnSessionLeft   -= Refresh;
    }

    private void OnSessionChanged(Unity.Services.Multiplayer.ISession s)
    {
        Debug.Log($"[DBG-lobby7] OnSessionJoined ยิงแล้ว — code='{s?.Code}'");
        Refresh();
    }

    private void OnCharacterPicked(CharacterData cd)
    {
        if (cd != null) SelectCharacter(cd.characterName);
    }

    public void SelectCharacter(string characterName)
    {
        if (LobbyState.Instance != null)
        {
            LobbyState.Instance.SetCharacterServerRpc(characterName);
        }
    }

    public void SelectMap(MapData map)
    {
        if (map == null) return;
        RunSetup.Set(map, RunSetup.Difficulty);
        if (LobbyState.Instance != null) LobbyState.Instance.SetMapServerRpc(map.mapId);
        Refresh();
    }

    public void SelectDifficulty(DifficultyTier tier)
    {
        RunSetup.Set(RunSetup.Map, tier);
        if (LobbyState.Instance != null) LobbyState.Instance.SetDifficultyServerRpc(tier);
        Refresh();
    }

    public void PushLocalSelectionsToLobby()
    {
        if (LobbyState.Instance == null) return;
        if (RunSetup.Map != null) LobbyState.Instance.SetMapServerRpc(RunSetup.Map.mapId);
        LobbyState.Instance.SetDifficultyServerRpc(RunSetup.Difficulty);
        if (CharacterSelectUI.SelectedCharacter != null)
        {
            LobbyState.Instance.SetCharacterServerRpc(CharacterSelectUI.SelectedCharacter.characterName);
        }
    }

    private void OnReadyButtonClicked()
    {
        if (LobbyState.Instance == null || NetworkManager.Singleton == null) return;

        ulong localId = NetworkManager.Singleton.LocalClientId;
        if (LobbyState.Instance.TryGetEntry(localId, out var entry))
        {
            LobbyState.Instance.SetReadyServerRpc(!entry.ready);
        }
    }

    private void OnStartRunClicked()
    {
        MapData selectedMap = RunSetup.Map;
        string sceneName = selectedMap != null ? selectedMap.sceneName : "SampleScene";
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.StartGame(sceneName);
        }
    }

    private void ShowBusy(string msg)
    {
        if (busyOverlay != null) busyOverlay.SetActive(true);
        if (busyText != null) busyText.text = msg;
    }

    private void HideBusy()
    {
        if (busyOverlay != null) busyOverlay.SetActive(false);
    }

    private async System.Threading.Tasks.Task<bool> RestartAndShutdownNetworkAsync(string statusMsg)
    {
        if (GameSessionManager.Instance == null || GameSessionManager.Instance.IsBusy)
        {
            Debug.Log($"[DBG-lobby7] restart ABORT — Instance null? {GameSessionManager.Instance == null} · IsBusy? {GameSessionManager.Instance?.IsBusy}");
            return false;
        }
        ShowBusy(statusMsg);

        await GameSessionManager.Instance.LeaveSessionIfActiveAsync();

        var nm = NetworkManager.Singleton;
        Debug.Log($"[DBG-lobby7] ก่อน Shutdown — IsListening={nm?.IsListening} IsServer={nm?.IsServer}");
        if (nm != null && nm.IsListening) nm.Shutdown();
        while (nm != null && nm.IsListening) await System.Threading.Tasks.Task.Yield();
        Debug.Log($"[DBG-lobby7] Shutdown เสร็จ — IsListening={nm?.IsListening}");

        return true;
    }

    private async void OnInviteClicked()
    {
        Debug.Log("[DBG-lobby7] === กด Invite ===");
        if (!await RestartAndShutdownNetworkAsync("กำลังเปิดห้อง…")) return;

        bool ok = await GameSessionManager.Instance.CreateSessionAsync();
        Debug.Log($"[DBG-lobby7] CreateSessionAsync คืน {ok} · CurrentSession null? {GameSessionManager.Instance.CurrentSession == null} · code='{GameSessionManager.Instance.SessionCode}'");
        HideBusy();
        if (!ok) return;

        Refresh();   // กันเหนียว เผื่อ OnSessionJoined ไม่ยิง
        Debug.Log("[DBG-lobby7] เรียก Refresh() ท้าย OnInviteClicked แล้ว");
    }

    private async void OnLobbyJoinClicked()
    {
        if (lobbyJoinCodeInput == null || string.IsNullOrWhiteSpace(lobbyJoinCodeInput.text)) return;
        string code = lobbyJoinCodeInput.text.Trim();

        if (!await RestartAndShutdownNetworkAsync("กำลังเข้าห้อง…")) return;

        bool ok = await GameSessionManager.Instance.JoinSessionAsync(code);
        HideBusy();
        if (!ok) return;
    }

    private void OnCopyCodeClicked()
    {
        string code = GameSessionManager.Instance?.SessionCode;
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
        }
    }

    public void Refresh()
    {
        bool isHost = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost) ||
                      (GameSessionManager.Instance != null && GameSessionManager.Instance.IsHost);

        if (tabBar != null)
        {
            tabBar.SetTabVisible("map", isHost);
        }

        bool hasSession = GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentSession != null;
        Debug.Log($"[DBG-lobby7] Refresh — hasSession={hasSession} · roomCodeLabel null? {roomCodeLabel == null} · copyCodeButton null? {copyCodeButton == null} · inviteButton null? {inviteButton == null}");

        if (hasSession)
        {
            if (roomCodeLabel != null)
            {
                roomCodeLabel.gameObject.SetActive(true);
                roomCodeLabel.text = $"Room: {GameSessionManager.Instance.SessionCode}";
            }
            if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(true);
            if (inviteButton != null) inviteButton.gameObject.SetActive(false);
        }
        else
        {
            if (roomCodeLabel != null) roomCodeLabel.gameObject.SetActive(false);
            if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(false);
            if (inviteButton != null) inviteButton.gameObject.SetActive(true);
        }

        if (LobbyState.Instance != null && LobbyState.Instance != lastLobbyState)
        {
            lastLobbyState = LobbyState.Instance;
            PushLocalSelectionsToLobby();
        }

        if (LobbyState.Instance == null)
        {
            lastLobbyState = null;
            return;
        }

        if (partyContainer != null && partyRowTemplate != null)
        {
            partyRowTemplate.SetActive(false);
            foreach (var row in spawnedPartyRows)
            {
                if (row != null) Destroy(row);
            }
            spawnedPartyRows.Clear();

            for (int i = 0; i < LobbyState.Instance.Players.Count; i++)
            {
                var entry = LobbyState.Instance.Players[i];
                var go = Instantiate(partyRowTemplate, partyContainer);
                go.SetActive(true);
                spawnedPartyRows.Add(go);

                string charName = entry.characterName.ToString();
                CharacterData charData = !string.IsNullOrEmpty(charName) && MetaDatabase.Instance != null
                    ? MetaDatabase.Instance.GetCharacter(charName)
                    : null;

                var textComp = go.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    string displayName = charData != null ? charData.characterName : (string.IsNullOrEmpty(charName) ? "Selecting..." : charName);
                    int slot = PlayerSlotRegistry.Instance != null ? PlayerSlotRegistry.Instance.GetSlot(entry.clientId) : -1;
                    string slotText = slot >= 0 ? (slot + 1).ToString() : "?";
                    textComp.text = $"Player {slotText}: {displayName} [{(entry.ready ? "READY" : "NOT READY")}]";
                }
            }
        }

        if (NetworkManager.Singleton != null && LobbyState.Instance.TryGetEntry(NetworkManager.Singleton.LocalClientId, out var myEntry))
        {
            if (readyButtonText != null)
            {
                readyButtonText.text = myEntry.ready ? "CANCEL READY" : "READY";
            }
        }

        string mapId = LobbyState.Instance.SelectedMapId.Value.ToString();
        MapData map = GetSelectedMap(mapId);
        if (mapNameLabel != null)
        {
            mapNameLabel.text = map != null ? map.displayName : (string.IsNullOrEmpty(mapId) ? "Select Map" : mapId);
        }
        if (difficultyLabel != null)
        {
            difficultyLabel.text = LobbyState.Instance.SelectedDifficulty.Value.ToString();
        }

        if (startRunButton != null)
        {
            startRunButton.interactable = isHost && LobbyState.Instance.AllReady();
        }
    }

    private MapData GetSelectedMap(string mapId)
    {
        if (maps == null || string.IsNullOrEmpty(mapId)) return null;
        return maps.Find(m => m != null && m.mapId == mapId);
    }
}
