using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CloneSwarm.Meta;

/// <summary>Hub ใช้ shell เดียวสองโหมด — เข้าจาก PLAY หรือจากปุ่มร้าน</summary>
public enum HubMode { Lobby, Shop }

public class LobbyUI : MonoBehaviour
{
    /// <summary>ยิงเมื่อออกจากล็อบบี้เรียบร้อยแล้ว — MenuManager subscribe เพื่อกลับหน้า Main</summary>
    public static event System.Action OnBack;

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
    public Button backButton;

    [Header("Selection Labels")]
    public TextMeshProUGUI mapNameLabel;
    public TextMeshProUGUI difficultyLabel;

    [Header("Selection Preview (ในแท็บ Lobby)")]
    [Tooltip("ปุ่มพาไปแท็บ MAP — ปิดการกดอัตโนมัติถ้าไม่ใช่ host เพราะ SetMapServerRpc รับเฉพาะ host")]
    public Button mapButton;
    [Tooltip("ภาพพรีวิวแมพที่เลือกอยู่ — อ่านจาก LobbyState ไม่ใช่จาก RunSetup เพื่อให้ตรงกับที่ server ถืออยู่")]
    public Image mapImage;
    [Tooltip("ภาพตัวละครของผู้เล่นเครื่องนี้ — ใช้ portrait ถ้ามี ไม่มีก็ icon")]
    public Image characterImage;

    [Header("Invite & Network Controls")]
    public Button inviteButton;
    [Tooltip("ป้ายบนปุ่ม Invite — หลังสร้างห้องจะกลายเป็นรหัสห้อง กดแล้วคัดลอก")]
    public TextMeshProUGUI inviteButtonText;
    public GameObject busyOverlay;
    public TextMeshProUGUI busyText;

    [Header("Room & Join Controls")]
    public Button lobbyJoinButton;

    [Header("Hub Mode")]
    [Tooltip("แถบล่างที่มี Ready / Start Run — ซ่อนตอนอยู่โหมดร้าน")]
    public GameObject bottomBar;

    private readonly List<GameObject> spawnedPartyRows = new();
    private LobbyState lastLobbyState;
    private HubMode mode = HubMode.Lobby;
    private Coroutine copyFeedbackRoutine;

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

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        if (inviteButton != null)
        {
            inviteButton.onClick.AddListener(OnInviteButtonClicked);
        }

        if (lobbyJoinButton != null)
        {
            lobbyJoinButton.onClick.AddListener(OnLobbyJoinClicked);
        }

        if (mapButton != null)
        {
            mapButton.onClick.AddListener(OnMapButtonClicked);
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

    /// <summary>MenuManager เรียกทันทีหลัง ShowPanel(lobbyPanel)</summary>
    public void SetMode(HubMode m)
    {
        mode = m;
        if (bottomBar != null) bottomBar.SetActive(m == HubMode.Lobby);

        // Refresh ตั้งความมองเห็นของแท็บก่อน แล้วค่อยเลือก — สลับลำดับไม่ได้
        // เพราะ TabBar.Select() ปฏิเสธแท็บที่ visible = false
        Refresh();
        if (tabBar != null) tabBar.Select(m == HubMode.Lobby ? "lobby" : "shop");
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

    private async void OnBackClicked()
    {
        var gsm = GameSessionManager.Instance;
        if (gsm != null && gsm.IsBusy) return;

        ShowBusy("กำลังออกจากห้อง…");

        // LeaveSessionIfActiveAsync คืนทันทีถ้าไม่มี session — solo จึงเรียกได้ตรงๆ
        if (gsm != null) await gsm.LeaveSessionIfActiveAsync();

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) nm.Shutdown();
        while (nm != null && nm.IsListening) await System.Threading.Tasks.Task.Yield();

        HideBusy();
        OnBack?.Invoke();
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

    /// <summary>ปุ่มแมพในแท็บ Lobby — พาไปแท็บ MAP ที่มี carousel เลือกแมพจริง</summary>
    private void OnMapButtonClicked()
    {
        // TabBar.Select ปฏิเสธแท็บที่ visible = false อยู่แล้ว ซึ่งเป็นกรณีของ client
        // (Refresh ตั้ง SetTabVisible("map", lobbyMode && isHost)) จึงไม่ต้องกันซ้ำตรงนี้
        if (tabBar != null) tabBar.Select("map");
    }

    private void OnLobbyJoinClicked()
    {
        if (JoinRoomPanel.Instance != null) JoinRoomPanel.Instance.Open();
    }

    /// listener ตัวเดียวแยกสาขาตอนกด — ไม่สลับ listener เพราะผูกซ้ำเงียบได้ง่าย
    private void OnInviteButtonClicked()
    {
        bool hasSession = GameSessionManager.Instance != null
                       && GameSessionManager.Instance.CurrentSession != null;

        if (hasSession) CopyRoomCode();
        else            OnInviteClicked();
    }

    private void CopyRoomCode()
    {
        string code = GameSessionManager.Instance?.SessionCode;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;

        if (copyFeedbackRoutine != null) StopCoroutine(copyFeedbackRoutine);
        copyFeedbackRoutine = StartCoroutine(ShowCopiedThenRestore());
    }

    private IEnumerator ShowCopiedThenRestore()
    {
        if (inviteButtonText != null) inviteButtonText.text = "คัดลอกแล้ว";
        yield return new WaitForSecondsRealtime(1.2f);
        copyFeedbackRoutine = null;
        Refresh();
    }

    public void Refresh()
    {
        bool isHost = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost) ||
                      (GameSessionManager.Instance != null && GameSessionManager.Instance.IsHost);

        if (tabBar != null)
        {
            bool lobbyMode = mode == HubMode.Lobby;
            tabBar.SetTabVisible("lobby",     lobbyMode);
            tabBar.SetTabVisible("map",       lobbyMode && isHost);
            tabBar.SetTabVisible("character", true);
            tabBar.SetTabVisible("shop",      true);
        }

        bool hasSession = GameSessionManager.Instance != null && GameSessionManager.Instance.CurrentSession != null;
        Debug.Log($"[DBG-lobby7] Refresh — hasSession={hasSession} · inviteButton null? {inviteButton == null}");

        if (inviteButton != null) inviteButton.gameObject.SetActive(true);

        // เช็ค copyFeedbackRoutine ด้วย ไม่งั้น Refresh ที่ถูกยิงจาก OnLobbyChanged
        // ระหว่าง 1.2 วินาทีจะลบข้อความ "คัดลอกแล้ว" ทิ้งก่อนผู้เล่นทันเห็น
        if (inviteButtonText != null && copyFeedbackRoutine == null)
        {
            inviteButtonText.text = hasSession
                ? $"รหัส {GameSessionManager.Instance.SessionCode} · คัดลอก"
                : "เชิญเพื่อน";
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

        if (mapImage != null)
        {
            var spr = map != null ? map.previewImage : null;
            mapImage.sprite  = spr;
            mapImage.enabled = spr != null;
        }

        // เฉพาะ host เปลี่ยนแมพได้ — ไม่ซ่อนแต่ปิดการกด เพื่อให้ client ยังเห็นว่าจะเล่นแมพไหน
        if (mapButton != null) mapButton.interactable = isHost;

        // ตัวละครของเครื่องนี้ อ่านจาก LobbyState ไม่ใช่ static ของ CharacterSelectUI
        // เพราะอันนี้คือค่าที่ server ถืออยู่จริง ถ้าสองอันไม่ตรงกันจะได้เห็นทันที
        if (characterImage != null)
        {
            CharacterData myChar = null;
            if (NetworkManager.Singleton != null && MetaDatabase.Instance != null
                && LobbyState.Instance.TryGetEntry(NetworkManager.Singleton.LocalClientId, out var meEntry))
            {
                myChar = MetaDatabase.Instance.GetCharacter(meEntry.characterName.ToString());
            }

            var spr = myChar != null ? (myChar.portrait != null ? myChar.portrait : myChar.icon) : null;
            characterImage.sprite  = spr;
            characterImage.enabled = spr != null;
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
