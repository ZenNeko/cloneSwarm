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
    [Tooltip("ร้านอัปเกรดถาวร — มี TalentShopUI อยู่บนนี้")]
    public GameObject talentShopPanel;

    // ═══════════════════════════════════════════════════════════════════════
    // MAIN PANEL
    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Main Panel ─────────────────────────")]
    public Button          playSoloButton;
    public Button          onlineButton;
    public Button          settingsButton;
    public Button          quitButton;
    [Tooltip("เปิดร้าน Talent Shop")]
    public Button          talentShopButton;
    [Tooltip("ยอดทองที่โชว์บนหน้า Main — ปล่อยว่างได้")]
    public TextMeshProUGUI goldText;
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
        if (talentShopButton) talentShopButton.onClick.AddListener(OnTalentShopClicked);

        // Talent Shop — TalentShopUI ยิง OnBack เมื่อกดปุ่ม Back
        CloneSwarm.Meta.TalentShopUI.OnBack       += ShowMain;
        CloneSwarm.Meta.MetaProgression.OnGoldChanged += HandleGoldChanged;
        RefreshGold();

        // CharSelect back
        if (charSelectBackButton) charSelectBackButton.onClick.AddListener(ShowMain);
        CharacterSelectUI.OnCharacterConfirmed += OnCharacterConfirmed;

        // Online back — leave session (ถ้ามี) ก่อน navigate กลับ เพื่อกัน orphan session
        // และกัน bug "กลับเข้ามาแล้ว auto-create ซ้อนของเดิม"
        if (onlinePanelBackButton)
            onlinePanelBackButton.onClick.AddListener(OnOnlineBackClicked);

        // Settings — SettingsMenuUI fires OnBack เมื่อกดปุ่ม Back
        SettingsMenuUI.OnBack += ShowMain;

        ShowMain();
    }

    void OnDestroy()
    {
        CharacterSelectUI.OnCharacterConfirmed -= OnCharacterConfirmed;
        SettingsMenuUI.OnBack                  -= ShowMain;
        CloneSwarm.Meta.TalentShopUI.OnBack           -= ShowMain;
        CloneSwarm.Meta.MetaProgression.OnGoldChanged -= HandleGoldChanged;
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
        talentShopPanel?.SetActive(false);
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

    void OnSettingsClicked()   => ShowPanel(settingsPanel);

    void OnTalentShopClicked() => ShowPanel(talentShopPanel);

    /// <summary>
    /// Back ออกจาก Online panel — leave session ก่อน (ถ้ามี) แล้ว navigate กลับ
    /// — ใช้ async void เพราะ Button.onClick ไม่รองรับ Task
    /// — UI navigate ทันที (ไม่รอ leave สำเร็จ) เพื่อกัน user ค้าง
    /// — OnlineMenuUI.LeaveSessionIfActiveAsync() จัดการ leave ใน background
    /// </summary>
    async void OnOnlineBackClicked()
    {
        // นำทางออกก่อน → UX ดีขึ้น user ไม่ต้องรอ network
        ShowPanel(charSelectPanel);

        if (onlineMenuUI != null)
            await onlineMenuUI.LeaveSessionIfActiveAsync();
    }

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
                // Trigger auto-create หลัง char confirm + รอ AuthenticationService.IsSignedIn
                // (กัน error "Player is not authorized" ที่เกิดจาก auto-create ใน OnEnable
                //  ตอน auth ยัง sign-in ไม่เสร็จ)
                _ = TriggerOnlineCreateAsync();
                break;
            default:
                ShowMain();
                break;
        }
    }

    /// <summary>Helper: รอให้ Online panel enable เสร็จก่อนค่อยเรียก auto-create</summary>
    async System.Threading.Tasks.Task TriggerOnlineCreateAsync()
    {
        // รอ 1 frame ให้ OnEnable ของ OnlineMenuUI วิ่งก่อน (subscribe listeners + reset UI)
        await System.Threading.Tasks.Task.Yield();
        if (onlineMenuUI != null)
            await onlineMenuUI.BeginAutoCreateAfterCharSelectAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SOLO LAUNCH
    // ═══════════════════════════════════════════════════════════════════════
    IEnumerator LaunchSolo()
    {
        ShowPanel(loadingPanel);
        if (loadingText) loadingText.text = "Starting game...";
        yield return null;

        NetworkManager.Singleton.StartHost();

        if (loadingText) loadingText.text = "Loading scene...";
        yield return null;

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

}
