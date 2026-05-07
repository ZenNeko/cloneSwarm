using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI panel สำหรับ Settings menu — ใช้ Inspector references ทั้งหมด
/// ทุก slider/dropdown ขับ SoundManager / QualitySettings ผ่าน inline callbacks
///
/// Sections:
///   • Audio    — Master / Music / SFX sliders + % labels
///   • Graphics — Quality preset dropdown
///   • Footer   — Reset Defaults / Back buttons
///
/// MenuManager subscribe `OnBack` static event เพื่อกลับ Main panel
///
/// Layout ที่ user ต้องสร้างใน Hierarchy:
///   SettingsPanel  (มี SettingsMenuUI component)
///   ├── Title  "⚙ SETTINGS"
///   ├── Audio   ── Master / Music / SFX rows  (Label + Slider + ValueText)
///   ├── Graphics ── Quality row  (Label + Dropdown)
///   └── Footer  ── Reset / Back buttons
/// </summary>
public class SettingsMenuUI : MonoBehaviour
{
    // ── Static event (MenuManager subscribe เพื่อกลับ Main) ───────────────
    public static event System.Action OnBack;

    [Header("── Audio Sliders ───────────────────────")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("── Audio Value Labels (% display) ──────")]
    public TextMeshProUGUI masterValueText;
    public TextMeshProUGUI musicValueText;
    public TextMeshProUGUI sfxValueText;

    [Header("── Graphics ────────────────────────────")]
    public TMP_Dropdown qualityDropdown;

    [Header("── Footer ──────────────────────────────")]
    public Button resetDefaultsButton;
    public Button backButton;
    // หมายเหตุ: ค่า Default ใช้จาก SoundManager (Default Master/Music/Sfx) — ไม่ต้องตั้งซ้ำ

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        // Sliders
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        // Quality dropdown — populate options ครั้งแรกเท่านั้น
        if (qualityDropdown != null)
        {
            if (qualityDropdown.options.Count == 0)
            {
                qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            }
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        }

        // Buttons
        if (resetDefaultsButton) resetDefaultsButton.onClick.AddListener(OnResetDefaults);
        if (backButton)          backButton.onClick.AddListener(OnBackClicked);

        SyncFromSoundManager();
    }

    void OnDisable()
    {
        if (masterSlider)        masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (musicSlider)         musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider)           sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (qualityDropdown)     qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
        if (resetDefaultsButton) resetDefaultsButton.onClick.RemoveListener(OnResetDefaults);
        if (backButton)          backButton.onClick.RemoveListener(OnBackClicked);
    }

    // ── Sync ค่า slider/dropdown จาก SoundManager + QualitySettings ──────
    void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (masterSlider) masterSlider.SetValueWithoutNotify(sm.MasterVolume);
        if (musicSlider)  musicSlider.SetValueWithoutNotify(sm.MusicVolume);
        if (sfxSlider)    sfxSlider.SetValueWithoutNotify(sm.SfxVolume);
        UpdateLabel(masterValueText, sm.MasterVolume);
        UpdateLabel(musicValueText,  sm.MusicVolume);
        UpdateLabel(sfxValueText,    sm.SfxVolume);

        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(QualitySettings.GetQualityLevel());
    }

    // ── Callbacks ─────────────────────────────────────────────────────────
    void OnMasterChanged(float v) { SoundManager.Instance.SetMasterVolume(v); UpdateLabel(masterValueText, v); }
    void OnMusicChanged (float v) { SoundManager.Instance.SetMusicVolume(v);  UpdateLabel(musicValueText,  v); }
    void OnSfxChanged   (float v) { SoundManager.Instance.SetSfxVolume(v);    UpdateLabel(sfxValueText,    v); }

    void OnQualityChanged(int level)
    {
        QualitySettings.SetQualityLevel(level);
        PlayerPrefs.SetInt("QualityLevel", level);
        PlayerPrefs.Save();
    }

    void OnResetDefaults()
    {
        SoundManager.Instance.ResetToDefaults();
        SyncFromSoundManager();
    }

    void OnBackClicked() => OnBack?.Invoke();

    // ── Helper ────────────────────────────────────────────────────────────
    static void UpdateLabel(TextMeshProUGUI label, float v01)
    {
        if (label) label.text = $"{Mathf.RoundToInt(v01 * 100)}%";
    }
}
