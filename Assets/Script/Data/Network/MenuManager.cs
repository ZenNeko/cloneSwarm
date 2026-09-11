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
    [Tooltip("จอ Title — ปล่อยว่างได้ ถ้าว่างเกมจะเปิดมาที่ Main เลยเหมือนเดิม")]
    public GameObject titlePanel;
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject loadingPanel;
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
        CloneSwarm.Meta.MetaProgression.OnGoldChanged += HandleGoldChanged;
        SettingsMenuUI.OnBack                         += ShowMain;
        LobbyUI.OnBack                                += ShowMain;
        JoinRoomPanel.OnJoined                         += HandleJoined;
        JoinRoomPanel.OnJoinFailed                     += HandleJoinFailed;
        GameSessionManager.OnSessionJoined             += HandleSessionJoined;
    }

    void OnDisable()
    {
        CloneSwarm.Meta.MetaProgression.OnGoldChanged -= HandleGoldChanged;
        SettingsMenuUI.OnBack                         -= ShowMain;
        LobbyUI.OnBack                                -= ShowMain;
        JoinRoomPanel.OnJoined                         -= HandleJoined;
        JoinRoomPanel.OnJoinFailed                     -= HandleJoinFailed;
        GameSessionManager.OnSessionJoined             -= HandleSessionJoined;
    }

    void Start()
    {
        // Main panel buttons
        if (playSoloButton) playSoloButton.onClick.AddListener(OnPlayClicked);
        if (joinRoomButton) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton)     quitButton.onClick.AddListener(OnQuitClicked);
        if (talentShopButton) talentShopButton.onClick.AddListener(OnTalentShopClicked);

        RefreshGold();

        // จอแรกคือ Title ถ้ามี — ไม่มีก็เข้า Main เลย
        // panel ถูกปล่อยให้เปิดค้างไว้ในซีนเพื่อให้ Awake ของลูกๆ วิ่งตอนโหลด
        // ShowPanel ตัวแรกนี้คือคนที่ปิดตัวที่ไม่ใช่จอแรกทิ้ง
        if (titlePanel != null) ShowTitle();
        else                    ShowMain();
    }

    void HandleSessionJoined(ISession _)
    {
        EnsureLobbyStateSpawned();
    }

    void HandleJoined()
    {
        ShowPanel(lobbyPanel);

        // ต้องตั้งโหมดด้วย ไม่ใช่แค่เปิดแผง — ร้าน Talent เป็นแท็บใน lobbyPanel ตัวเดียวกัน
        // ถ้าเพิ่งเข้าร้านมา mode ยังค้างเป็น Shop แผงจะเปิดมาเป็นหน้าร้าน
        // ไม่มีแท็บล็อบบี้และไม่มี bottomBar (เทียบ OnPlayClicked ที่ตั้งให้อยู่แล้ว)
        if (lobbyUI != null) lobbyUI.SetMode(HubMode.Lobby);
    }

    void HandleJoinFailed()
    {
        if (lobbyPanel != null && lobbyPanel.activeSelf)
            StartCoroutine(StartOfflineHostThenSpawn());
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
    /// <summary>เปิดจอ Title — ทางกลับมาหลังจบรอบก็ใช้ตัวนี้ได้</summary>
    public void ShowTitle()
    {
        ShowPanel(titlePanel);
    }

    /// <summary>กลับหน้าแรก — public เพราะจอ P3R เรียกผ่าน P3RMenuBridge ไม่ได้ผ่านปุ่ม</summary>
    public void ShowMain()
    {
        ShowPanel(mainPanel);
    }

    /// <summary>
    /// เปิดหนึ่ง ปิดที่เหลือ
    ///
    /// **panel ใหม่ทุกตัวต้องมาเข้าแถวนี้** ไม่งั้นจะไม่มีใครปิดมันได้เลย
    /// `titlePanel` เคยขาดไปและเป็นเหตุที่จอไตเติลคลุมเมนูถาวร — อาการคือจอเปิดค้าง
    /// โดยไม่มี error สักบรรทัด เพราะ `onAdvanceEvent → ShowMain()` ทำงานถูกทุกอย่าง
    /// ยกเว้นไม่มีบรรทัดไหนแตะตัวมันเอง
    /// </summary>
    void ShowPanel(GameObject target)
    {
        titlePanel?.SetActive(false);
        mainPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        target?.SetActive(true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════
    public void OnPlayClicked()
    {
        ShowPanel(lobbyPanel);
        if (lobbyUI != null) lobbyUI.SetMode(HubMode.Lobby);
        StartCoroutine(StartOfflineHostThenSpawn());
    }

    IEnumerator StartOfflineHostThenSpawn()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsListening) nm.StartHost();
        yield return null;                 // ให้ NGO ตั้งตัวหนึ่งเฟรม
        EnsureLobbyStateSpawned();
    }

    public void OnJoinRoomClicked()
    {
        if (JoinRoomPanel.Instance != null) JoinRoomPanel.Instance.Open();
    }

    public void OnSettingsClicked()   => ShowPanel(settingsPanel);

    /// ร้านเป็นแท็บใน Hub — ไม่แตะ network เลย
    public void OnTalentShopClicked()
    {
        ShowPanel(lobbyPanel);
        if (lobbyUI != null) lobbyUI.SetMode(HubMode.Shop);
    }

    public void OnQuitClicked()
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
