using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
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
    public Button          settingsBackButton;
    [Tooltip("AudioMixer ที่มี exposed params: MasterVolume, MusicVolume, SFXVolume")]
    public AudioMixer      audioMixer;
    public Slider          masterSlider;
    public Slider          musicSlider;
    public Slider          sfxSlider;
    public TextMeshProUGUI masterValueText;
    public TextMeshProUGUI musicValueText;
    public TextMeshProUGUI sfxValueText;
    [Tooltip("Dropdown Quality Settings (optional)")]
    public TMP_Dropdown    qualityDropdown;

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

        // Settings back
        if (settingsBackButton) settingsBackButton.onClick.AddListener(ShowMain);
        InitSettings();

        ShowMain();
    }

    void OnDestroy()
    {
        CharacterSelectUI.OnCharacterConfirmed -= OnCharacterConfirmed;
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

        NetworkManager.Singleton.StartHost();

        if (loadingText) loadingText.text = "Loading scene...";
        yield return null;

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════════════════════════
    void InitSettings()
    {
        float master = PlayerPrefs.GetFloat("Vol_Master", 0.8f);
        float music  = PlayerPrefs.GetFloat("Vol_Music",  0.7f);
        float sfx    = PlayerPrefs.GetFloat("Vol_SFX",    1.0f);

        if (masterSlider) { masterSlider.value = master; masterSlider.onValueChanged.AddListener(OnMasterChanged); }
        if (musicSlider)  { musicSlider.value  = music;  musicSlider.onValueChanged.AddListener(OnMusicChanged);  }
        if (sfxSlider)    { sfxSlider.value    = sfx;    sfxSlider.onValueChanged.AddListener(OnSFXChanged);      }

        ApplyVolume("MasterVolume", master, masterValueText);
        ApplyVolume("MusicVolume",  music,  musicValueText);
        ApplyVolume("SFXVolume",    sfx,    sfxValueText);

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.value = QualitySettings.GetQualityLevel();
            qualityDropdown.onValueChanged.AddListener(lvl =>
            {
                QualitySettings.SetQualityLevel(lvl);
                PlayerPrefs.SetInt("QualityLevel", lvl);
            });
        }
    }

    void OnMasterChanged(float v) { PlayerPrefs.SetFloat("Vol_Master", v); ApplyVolume("MasterVolume", v, masterValueText); }
    void OnMusicChanged(float v)  { PlayerPrefs.SetFloat("Vol_Music",  v); ApplyVolume("MusicVolume",  v, musicValueText);  }
    void OnSFXChanged(float v)    { PlayerPrefs.SetFloat("Vol_SFX",    v); ApplyVolume("SFXVolume",    v, sfxValueText);    }

    void ApplyVolume(string param, float val, TextMeshProUGUI label)
    {
        if (audioMixer != null)
        {
            float db = val > 0.0001f ? Mathf.Log10(val) * 20f : -80f;
            audioMixer.SetFloat(param, db);
        }
        if (label) label.text = $"{Mathf.RoundToInt(val * 100)}%";
    }
}
