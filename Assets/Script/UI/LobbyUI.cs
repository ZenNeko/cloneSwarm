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

    // ── แถวปาร์ตี้แบบ P3R (ของใหม่ · ไม่บังคับ) ────────────────────────────
    // ทั้งหมดนี้ "ต่อก็ใช้ ไม่ต่อก็ได้" — ซีน MenuScene เดิมต่อไว้กับ partyRowTemplate
    // ที่เป็น TMP บรรทัดเดียว ถ้าเปลี่ยนไปเลยสายในซีนจะขาดเงียบๆ โดยคอมไพเลอร์ไม่ฟ้อง
    // Refresh() จึงเลือกทางเองตามว่ามีอะไรต่ออยู่
    [Header("── P3R Party Rows (ปล่อยว่างได้) ──────")]
    [Tooltip("แถวเต็มรูปแบบ P3R — ต่อแล้วจะถูกใช้แทน partyRowTemplate")]
    public CloneSwarm.UI.P3R.LobbyPartyRowUI partyRowPrefab;

    [Tooltip("จำนวนช่องที่โชว์ทั้งหมด · ช่องที่เกินจำนวนผู้เล่นจะเป็นช่องว่างเส้นประ")]
    public int partySlotCount = 4;

    [Tooltip("ระยะห่างระหว่างแถว (px) — container ไม่มี LayoutGroup แถวถูกวางด้วยโค้ด")]
    public float partyRowSpacing = 10f;

    [Tooltip("สีขอบซ้ายของแต่ละช่อง — index ตรงกับ PlayerSlotRegistry.GetSlot() ไม่ใช่ clientId")]
    public Color[] slotColors =
    {
        new Color32(0x40, 0x73, 0xD9, 0xFF),
        new Color32(0x2C, 0xC5, 0xA0, 0xFF),
        new Color32(0xFF, 0xE6, 0x33, 0xFF),
        new Color32(0xF0, 0x86, 0x54, 0xFF),
    };

    [Header("── P3R Header (ปล่อยว่างได้) ───────────")]
    [Tooltip("ยอดทองบนแถบบน — MetaProgression.OnGoldChanged มีอยู่แล้ว แค่ยังไม่มีที่โชว์")]
    public TextMeshProUGUI goldText;
    [Tooltip("รหัสห้องแยกออกมาเป็นป้ายของตัวเอง — เดิมไปปนอยู่บนป้ายปุ่ม Invite")]
    public TextMeshProUGUI roomCodeLabel;
    public Button copyCodeButton;
    [Tooltip("PARTY · 3 / 4")]
    public TextMeshProUGUI partyCountLabel;
    [Tooltip("2 READY")]
    public TextMeshProUGUI readyCountLabel;

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

    /// <summary>host เท่านั้นที่เปลี่ยนแมพ/ความยากได้ — LobbyState ทิ้ง ServerRpc ของคนอื่นเงียบๆ
    /// อ่านสองแหล่งเพราะช่วงกำลังต่อ session ตัวใดตัวหนึ่งยังไม่พร้อม</summary>
    public bool IsHost =>
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost) ||
        (GameSessionManager.Instance != null && GameSessionManager.Instance.IsHost);

    /// <summary>true = มีล็อบบี้บนเน็ตเวิร์กอยู่ และเครื่องนี้ไม่ใช่ host → เปลี่ยนแมพ/ความยากไม่ได้
    ///
    /// ต้องเช็ค LobbyState ด้วย ไม่ใช่ !IsHost เฉยๆ — MapSelectUI อยู่บน Canvas ที่ root
    /// Start() ของมันจึงรันตั้งแต่ซีนโหลด ก่อนมี session ใดๆ ตอนนั้น IsHost เป็น false เสมอ
    /// ถ้าบล็อกด้วย !IsHost ค่าเริ่มต้นที่ Start() ตั้งให้จะไม่ถูกเขียนลง RunSetup
    /// แล้ว PushLocalSelectionsToLobby จะไม่มีอะไรส่งขึ้น server → SelectedMapId ว่าง
    /// → GetSelectedMap คืน null → Map_Image ถูกปิดทิ้งใน Refresh()</summary>
    public bool MapLocked => LobbyState.Instance != null && !IsHost;

    /// <summary>แมพที่ server ถืออยู่จริง — ไม่ใช่ตัวที่ผู้เล่นเครื่องนี้เพิ่งเลื่อนผ่าน</summary>
    public MapData NetworkSelectedMap =>
        LobbyState.Instance != null
            ? GetSelectedMap(LobbyState.Instance.SelectedMapId.Value.ToString())
            : null;

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

        // SetMapServerRpc ทิ้ง request ที่ไม่ใช่ host เงียบๆ — ถ้าเขียน RunSetup ก่อนส่ง
        // เครื่อง client จะเห็นแมพเปลี่ยนในจอตัวเอง แล้วเข้าเกมได้แมพของ host แทน
        // ให้ host เป็นคนเดียวที่เขียน · client รับแมพจริงจาก LobbyState ใน Refresh()
        if (MapLocked)
        {
            Refresh();
            return;
        }

        RunSetup.Set(map, RunSetup.Difficulty);
        if (LobbyState.Instance != null) LobbyState.Instance.SetMapServerRpc(map.mapId);
        Refresh();
    }

    public void SelectDifficulty(DifficultyTier tier)
    {
        // SetDifficultyServerRpc รับเฉพาะ host เหมือนกัน — เหตุผลเดียวกับ SelectMap
        if (MapLocked)
        {
            Refresh();
            return;
        }

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
        bool isHost = IsHost;

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

        RefreshP3RHeader();

        if (partyContainer != null && partyRowPrefab != null)
        {
            // ทางใหม่ — แถวเต็มรูปแบบ P3R · แม่แบบเก่าถ้ามีก็ปิดทิ้ง
            if (partyRowTemplate != null) partyRowTemplate.SetActive(false);
            RefreshP3RPartyRows();
        }
        else if (partyContainer != null && partyRowTemplate != null)
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
                    // charName เป็น ID ที่มาจากเน็ตเวิร์ก — ใช้เป็น fallback ได้ แต่ถ้าหา CharacterData เจอ
                    // ต้องโชว์ DisplayName ไม่ใช่ characterName
                    string displayName = charData != null ? charData.DisplayName : (string.IsNullOrEmpty(charName) ? "Selecting..." : charName);
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

        // client ไม่ได้เขียน RunSetup เองแล้ว (ดู SelectMap) จึงต้องรับค่าที่ server ถืออยู่มาใส่
        // ไม่งั้น RunSetup.Map ฝั่ง client ค้าง null ทั้งรัน แล้ว WinLoseUI/WaveManager อ่านผิด
        // host ไม่แตะตรงนี้ — RunSetup ของ host คือต้นทางอยู่แล้ว การเขียนทับจะเปิดช่องค่าเก่า
        if (!isHost && map != null)
        {
            RunSetup.Set(map, LobbyState.Instance.SelectedDifficulty.Value);
        }
        if (mapNameLabel != null)
        {
            mapNameLabel.text = map != null ? map.DisplayName : (string.IsNullOrEmpty(mapId) ? "Select Map" : mapId);
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

    // ═══════════════════════════════════════════════════════════════════════
    // P3R HEADER / PARTY — ทั้งบล็อกนี้ทำงานเฉพาะเมื่อมีการต่อสายไว้
    // ═══════════════════════════════════════════════════════════════════════
    private readonly List<CloneSwarm.UI.P3R.LobbyPartyRowUI> p3rRows = new();

    private void RefreshP3RHeader()
    {
        if (goldText != null)
            goldText.text = $"{CloneSwarm.Meta.MetaProgression.Gold:N0} G";

        bool hasSession = GameSessionManager.Instance != null
                       && GameSessionManager.Instance.CurrentSession != null;

        if (roomCodeLabel != null)
            roomCodeLabel.text = hasSession ? GameSessionManager.Instance.SessionCode : "—";

        // ไม่มีห้อง = ไม่มีรหัสให้คัดลอก · ซ่อนดีกว่าให้กดแล้วไม่เกิดอะไร
        if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(hasSession);

        if (LobbyState.Instance == null) return;

        int count = LobbyState.Instance.Players.Count;
        int ready = 0;
        for (int i = 0; i < count; i++)
            if (LobbyState.Instance.Players[i].ready) ready++;

        if (partyCountLabel != null) partyCountLabel.text = $"PARTY · {count} / {partySlotCount}";
        if (readyCountLabel != null) readyCountLabel.text = $"{ready} READY";
    }

    /// <summary>
    /// สร้างแถวให้ครบ <see cref="partySlotCount"/> ช่องเสมอ — ช่องที่ยังไม่มีคนเป็นเส้นประ
    /// แบบวาดไว้แบบนี้เพื่อให้เห็นว่าห้องยังรับได้อีกกี่คน ไม่ใช่แค่ว่ามีใครอยู่บ้าง
    /// </summary>
    private void RefreshP3RPartyRows()
    {
        if (partyRowPrefab == null || partyContainer == null) return;

        // ล้าง **ทุกลูก** ไม่ใช่แค่ที่ตัวเองสร้าง — builder วางแถวตัวอย่างไว้ในซีนให้เห็น
        // หน้าตาตอนยังไม่กด Play แถวพวกนั้นไม่ได้อยู่ใน p3rRows
        // ล้างแค่ของตัวเองแล้วตอนรันจะได้แถวสองชุดซ้อนกันจนอ่านไม่ออก
        for (int i = partyContainer.childCount - 1; i >= 0; i--)
            Destroy(partyContainer.GetChild(i).gameObject);
        p3rRows.Clear();

        var  players    = LobbyState.Instance != null ? LobbyState.Instance.Players : null;
        int  playerCount = players != null ? players.Count : 0;
        ulong localId   = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
        int  total      = Mathf.Max(partySlotCount, playerCount);

        for (int i = 0; i < total; i++)
        {
            var row = Instantiate(partyRowPrefab, partyContainer);
            row.gameObject.SetActive(true);
            p3rRows.Add(row);

            // ต้องวางตำแหน่งเอง — ไม่มี LayoutGroup บน container
            // ไม่วาง = ทุกแถวไปกองอยู่ที่เดียวกันตามตำแหน่งใน prefab
            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * (rt.sizeDelta.y + partyRowSpacing));

            if (i >= playerCount) { row.SetEmpty(); continue; }

            var entry    = players[i];
            var charName = entry.characterName.ToString();
            var charData = !string.IsNullOrEmpty(charName) && MetaDatabase.Instance != null
                         ? MetaDatabase.Instance.GetCharacter(charName)
                         : null;

            // charName เป็น ID บนเน็ตเวิร์ก — ที่โชว์ต้องเป็น DisplayName เสมอ
            string display = charData != null
                           ? charData.DisplayName
                           : (string.IsNullOrEmpty(charName) ? "กำลังเลือก…" : charName);

            // สีประจำช่องต้องมาจาก PlayerSlotRegistry ไม่ใช่ clientId % 4 (CLAUDE.md ข้อ 11)
            int slot = PlayerSlotRegistry.Instance != null
                     ? PlayerSlotRegistry.Instance.GetSlot(entry.clientId)
                     : i;

            Color accent = slotColors != null && slotColors.Length > 0
                         ? slotColors[Mathf.Abs(slot < 0 ? i : slot) % slotColors.Length]
                         : Color.white;

            // HP ยังไม่มีให้อ่านตอนอยู่ล็อบบี้ — ตัวละครยังไม่ถูก spawn
            // โชว์ค่าฐานจาก CharacterData แทน ซึ่งเป็นค่าที่จะได้ตอนเกิดจริง
            string detail = charData != null
                          ? $"HP {Mathf.RoundToInt(charData.baseHealth)}"
                          : "";

            row.Bind(slot, display, isHost: entry.clientId == 0,
                     isYou: entry.clientId == localId,
                     ready: entry.ready, detail: detail, accent: accent);
        }
    }
}
