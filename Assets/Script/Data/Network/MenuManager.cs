using System.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main Menu Controller — จัดการทุก Panel ใน MenuScene
///
/// Flow:
///   Main → PLAY → Lobby (StartHost offline)
///   Main → Join Room → JoinSessionAsync → Lobby
///   Main → Settings
///   Main → Talent Shop
///   Main → Quit
/// </summary>
public class MenuManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    // PANELS
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Panels ──────────────────────────────")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject loadingPanel;
    [Tooltip("ร้านอัปเกรดถาวร — มี TalentShopUI อยู่บนนี้")]
    public GameObject talentShopPanel;
    public GameObject lobbyPanel;
    public LobbyUI    lobbyUI;
    public GameObject lobbyStatePrefab;

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN PANEL
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Main Panel ─────────────────────────")]
    public Button          playSoloButton;
    public Button          settingsButton;
    public Button          quitButton;
    [Tooltip("เปิดร้าน Talent Shop")]
    public Button          talentShopButton;
    [Tooltip("ยอดทองที่โชว์บนหน้า Main — ปล่อยว่างได้")]
    public TextMeshProUGUI goldText;
    [Tooltip("ข้อความ version ล่างจอ เช่น v0.1.0-alpha")]
    public TextMeshProUGUI versionText;

    [Header("── Join Room (Main Panel) ────────────")]
    public Button          joinRoomButton;
    public TMP_InputField  joinCodeInput;
    public TextMeshProUGUI joinStatusText;

    // ═══════════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Settings ───────────────────────────")]
    [Tooltip("Component บน settingsPanel — จัดการ volume / quality เอง\n" +
             "MenuManager subscribe SettingsMenuUI.OnBack เพื่อกลับ Main")]
    public SettingsMenuUI settingsMenuUI;

    // ═══════════════════════════════════════════════════════════════════════
    // LOADING / SCENE
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Loading ─────────────────────────────")]
    public TextMeshProUGUI loadingText;
    [Tooltip("ชื่อ Game Scene ใน Build Settings")]
    public string          gameSceneName = "SampleScene";

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════
    void Awake()
    {
        if (versionText) versionText.text = $"v{Application.version}";
    }

    void OnEnable()
    {
        GameSessionManager.OnStatus += HandleStatus;
    }

    void OnDisable()
    {
        GameSessionManager.OnStatus -= HandleStatus;
    }

    void Start()
    {
        // Main panel buttons
        if (playSoloButton) playSoloButton.onClick.AddListener(OnPlayClicked);
        if (joinRoomButton) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton)     quitButton.onClick.AddListener(OnQuitClicked);
        if (talentShopButton) talentShopButton.onClick.AddListener(OnTalentShopClicked);

        // Talent Shop — TalentShopUI ยิง OnBack เมื่อกดปุ่ม Back
        CloneSwarm.Meta.TalentShopUI.OnBack       += ShowMain;
        CloneSwarm.Meta.MetaProgression.OnGoldChanged += HandleGoldChanged;
        RefreshGold();

        // Settings — SettingsMenuUI fires OnBack เมื่อกดปุ่ม Back
        SettingsMenuUI.OnBack += ShowMain;

        // Session joined -> EnsureLobbyStateSpawned
        GameSessionManager.OnSessionJoined += HandleSessionJoined;

        ShowMain();
    }

    void OnDestroy()
    {
        GameSessionManager.OnSessionJoined   -= HandleSessionJoined;
        SettingsMenuUI.OnBack                  -= ShowMain;
        CloneSwarm.Meta.TalentShopUI.OnBack           -= ShowMain;
        CloneSwarm.Meta.MetaProgression.OnGoldChanged -= HandleGoldChanged;
    }

    void HandleStatus(string msg)
    {
        if (joinStatusText != null) joinStatusText.text = msg;
    }

    void HandleSessionJoined(ISession _)
    {
        EnsureLobbyStateSpawned();
    }

    void HandleGoldChanged(int _) => RefreshGold();

    void RefreshGold()
    {
        if (goldText != null)
            goldText.text = $"{CloneSwarm.Meta.MetaProgression.Gold:N0} G";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════
    void ShowMain()
    {
        ShowPanel(mainPanel);
    }

    void ShowPanel(GameObject target)
    {
        mainPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        talentShopPanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        target?.SetActive(true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════
    void OnPlayClicked()
    {
        ShowPanel(lobbyPanel);
        StartCoroutine(StartOfflineHostThenSpawn());
    }

    IEnumerator StartOfflineHostThenSpawn()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsListening) nm.StartHost();
        yield return null;                 // ให้ NGO ตั้งตัวหนึ่งเฟรม
        EnsureLobbyStateSpawned();
    }

    async void OnJoinRoomClicked()
    {
        if (joinCodeInput == null || string.IsNullOrWhiteSpace(joinCodeInput.text)) return;
        bool ok = await GameSessionManager.Instance.JoinSessionAsync(joinCodeInput.text.Trim());
        if (ok) ShowPanel(lobbyPanel);
    }

    void OnSettingsClicked()   => ShowPanel(settingsPanel);

    void OnTalentShopClicked() => ShowPanel(talentShopPanel);

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnsureLobbyStateSpawned()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (LobbyState.Instance != null) return;
        if (lobbyStatePrefab == null) return;
        var go = Instantiate(lobbyStatePrefab);
        go.GetComponent<NetworkObject>().Spawn();
    }
}
