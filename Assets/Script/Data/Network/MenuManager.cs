using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Main Menu Controller — จัดการทุก Panel ใน MenuScene
///
/// Flow:
///   Main → CharSelect → Solo  → StartHost → LoadScene
///                     → Online → OnlinePanel (room code)
///   Main → Settings (volume / quality)
///   Main → Quit
/// </summary>
public class MenuManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    // PANELS
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Panels ──────────────────────────────")]
    public GameObject mainPanel;
    public GameObject charSelectPanel;
    public GameObject onlinePanel;
    public GameObject settingsPanel;
    public GameObject loadingPanel;

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN PANEL
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Main Panel ─────────────────────────")]
    public Button          playSoloButton;
    public Button          onlineButton;
    public Button          settingsButton;
    public Button          quitButton;
    [Tooltip("ข้อความ version ล่างจอ เช่น v0.1.0-alpha")]
    public TextMeshProUGUI versionText;

    // ═══════════════════════════════════════════════════════════════════════
    // CHARACTER SELECT
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Character Select ────────────────────")]
    public CharacterSelectUI characterSelectUI;
    public Button            charSelectBackButton;

    // ═══════════════════════════════════════════════════════════════════════
    // ONLINE PANEL
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Online Panel ────────────────────────")]
    public OnlineMenuUI onlineMenuUI;
    public Button       onlinePanelBackButton;

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

    // ── Internal ──────────────────────────────────────────────────────────
    private enum MenuMode { None, Solo, Online }
    private MenuMode pendingMode = MenuMode.None;

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════
    void Awake()
    {
        if (versionText) versionText.text = $"v{Application.version}";
    }

    void Start()
    {
        // Main panel buttons
        if (playSoloButton) playSoloButton.onClick.AddListener(OnPlaySoloClicked);
        if (onlineButton)   onlineButton.onClick.AddListener(OnOnlineClicked);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettingsClicked);
        if (quitButton)     quitButton.onClick.AddListener(OnQuitClicked);

        // CharSelect back
        if (charSelectBackButton) charSelectBackButton.onClick.AddListener(ShowMain);
        CharacterSelectUI.OnCharacterConfirmed += OnCharacterConfirmed;

        // Online back
        if (onlinePanelBackButton)
            onlinePanelBackButton.onClick.AddListener(() => ShowPanel(charSelectPanel));

        // Settings — SettingsMenuUI fires OnBack เมื่อกดปุ่ม Back
        SettingsMenuUI.OnBack += ShowMain;

        ShowMain();
    }

    void OnDestroy()
    {
        CharacterSelectUI.OnCharacterConfirmed -= OnCharacterConfirmed;
        SettingsMenuUI.OnBack                  -= ShowMain;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NAVIGATION
    // ═══════════════════════════════════════════════════════════════════════
    void ShowMain()
    {
        pendingMode = MenuMode.None;
        ShowPanel(mainPanel);
    }

    void ShowPanel(GameObject target)
    {
        mainPanel?.SetActive(false);
        charSelectPanel?.SetActive(false);
        onlinePanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        target?.SetActive(true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ═══════════════════════════════════════════════════════════════════════
    void OnPlaySoloClicked()
    {
        pendingMode = MenuMode.Solo;
        ShowPanel(charSelectPanel);
    }

    void OnOnlineClicked()
    {
        pendingMode = MenuMode.Online;
        ShowPanel(charSelectPanel);
    }

    void OnSettingsClicked() => ShowPanel(settingsPanel);

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHARACTER CONFIRMED → route by pendingMode
    // ═══════════════════════════════════════════════════════════════════════
    void OnCharacterConfirmed(CharacterData cd)
    {
        switch (pendingMode)
        {
            case MenuMode.Solo:
                StartCoroutine(LaunchSolo());
                break;
            case MenuMode.Online:
                ShowPanel(onlinePanel);
                break;
            default:
                ShowMain();
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SOLO LAUNCH
    // ═══════════════════════════════════════════════════════════════════════
    IEnumerator LaunchSolo()
    {
        ShowPanel(loadingPanel);
        if (loadingText) loadingText.text = "Starting game...";
        yield return null;

        // WebGL ไม่รองรับ UnityTransport เป็น server — swap เป็น OfflineTransport แทน
        // OfflineTransport ไม่สร้าง socket จริง ทำงานได้ทุก platform
        var nm = NetworkManager.Singleton;
        if (nm.NetworkConfig.NetworkTransport is not OfflineTransport)
        {
            // ลบ transport เดิม (UnityTransport) แล้ว add OfflineTransport
            var oldTransport = nm.NetworkConfig.NetworkTransport as UnityEngine.Component;
            if (oldTransport != null) Destroy(oldTransport);

            var offline = nm.gameObject.AddComponent<OfflineTransport>();
            nm.NetworkConfig.NetworkTransport = offline;
        }

        nm.StartHost();

        if (loadingText) loadingText.text = "Loading scene...";
        yield return null;

        nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

}
