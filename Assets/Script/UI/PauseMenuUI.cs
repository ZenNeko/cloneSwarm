using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game Pause Menu — ใช้ Inspector references ทั้งหมด (designer-built Canvas)
///
/// Features:
///   • กด ESC → toggle pause (ผ่าน GamePause — ห้ามเขียน Time.timeScale ตรง)
///   • Slider Master / Music / SFX → ขับ SoundManager
///   • Reset Defaults / Resume / Quit to Main Menu
///
/// Setup:
///   1. ใน game scene สร้าง Canvas + Panel hierarchy ตามที่ออกแบบ
///   2. วาง GameObject ที่มี PauseMenuUI component (panelRoot ชี้ไป Panel)
///   3. ลาก Slider / Text / Button refs เข้า Inspector
///   4. กำหนด menuSceneName = "MenuScene"
///
/// หมายเหตุ:
///   • Solo: Time.timeScale หยุด / Multiplayer: LocalInputSuspended ระงับ input ของเครื่องตัวเอง
///   • Quit = NetworkManager.Shutdown + load MenuScene
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("ชื่อ Menu Scene ใน Build Settings — กดออกจะโหลด scene นี้")]
    public string menuSceneName = "MenuScene";

    [Header("Panel Root")]
    [Tooltip("Panel parent GameObject — auto SetActive(true/false) ตอน pause/resume")]
    public GameObject panelRoot;

    [Header("Audio Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Audio Value Labels (% display)")]
    public TextMeshProUGUI masterValueText;
    public TextMeshProUGUI musicValueText;
    public TextMeshProUGUI sfxValueText;

    [Header("Buttons")]
    public Button resumeButton;
    public Button quitButton;
    public Button resetDefaultsButton;
    // หมายเหตุ: ค่า Default ใช้จาก SoundManager (Default Master/Music/Sfx) — ไม่ต้องตั้งซ้ำ

    // ── Internal state ────────────────────────────────────────────────────
    bool isPaused;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        // ปิด panel ตอนเริ่ม — open ผ่าน ESC เท่านั้น
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        if (resumeButton)        resumeButton.onClick.AddListener(Resume);
        if (quitButton)          quitButton.onClick.AddListener(OnQuitClicked);
        if (resetDefaultsButton) resetDefaultsButton.onClick.AddListener(OnResetDefaults);
    }

    void OnDisable()
    {
        if (masterSlider) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);

        if (resumeButton)        resumeButton.onClick.RemoveListener(Resume);
        if (quitButton)          quitButton.onClick.RemoveListener(OnQuitClicked);
        if (resetDefaultsButton) resetDefaultsButton.onClick.RemoveListener(OnResetDefaults);

        // กัน scene unload ทิ้ง pause state → restore
        if (isPaused)
        {
            GamePause.Remove(PauseReason.PauseMenu);
            GamePause.SuspendLocalInput(false);
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            Toggle();
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void Toggle() { if (isPaused) Resume(); else Pause(); }

    public void Pause()
    {
        if (isPaused) return;
        isPaused = true;

        bool isMultiplayer = NetworkManager.Singleton != null &&
                             NetworkManager.Singleton.IsListening &&
                             NetworkManager.Singleton.ConnectedClients.Count > 1;

        if (isMultiplayer)
        {
            GamePause.SuspendLocalInput(true);
        }
        else
        {
            GamePause.Add(PauseReason.PauseMenu);
        }

        SyncFromSoundManager();
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        GamePause.Remove(PauseReason.PauseMenu);
        GamePause.SuspendLocalInput(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnQuitClicked()
    {
        // คืน timeScale ก่อน load MenuScene (กัน menu freeze)
        GamePause.ResetAll();
        isPaused = false;

        // Shutdown network ก่อนกลับ menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(menuSceneName);
    }

    void OnResetDefaults()
    {
        SoundManager.Instance.ResetToDefaults();
        SyncFromSoundManager();
    }

    // ── Sync sliders from SoundManager (เมื่อเปิด menu) ──────────────────
    void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (masterSlider) masterSlider.SetValueWithoutNotify(sm.MasterVolume);
        if (musicSlider)  musicSlider.SetValueWithoutNotify(sm.MusicVolume);
        if (sfxSlider)    sfxSlider.SetValueWithoutNotify(sm.SfxVolume);
        UpdateLabel(masterValueText, sm.MasterVolume);
        UpdateLabel(musicValueText,  sm.MusicVolume);
        UpdateLabel(sfxValueText,    sm.SfxVolume);
    }

    // ── Slider callbacks ──────────────────────────────────────────────────
    void OnMasterChanged(float v) { SoundManager.Instance.SetMasterVolume(v); UpdateLabel(masterValueText, v); }
    void OnMusicChanged (float v) { SoundManager.Instance.SetMusicVolume(v);  UpdateLabel(musicValueText,  v); }
    void OnSfxChanged   (float v) { SoundManager.Instance.SetSfxVolume(v);    UpdateLabel(sfxValueText,    v); }

    static void UpdateLabel(TextMeshProUGUI label, float v01)
    {
        if (label) label.text = $"{Mathf.RoundToInt(v01 * 100)}%";
    }
}
