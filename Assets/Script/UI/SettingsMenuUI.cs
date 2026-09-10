using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// UGUI panel สำหรับ Settings menu — ใช้ Inspector references ทั้งหมด
/// ทุก slider/dropdown ขับ SoundManager / QualitySettings ผ่าน inline callbacks
///
/// Sections:
///   • Audio    — Master / Music / SFX sliders + % labels
///   • Graphics — Quality preset dropdown
///   • Language — Locale dropdown (Unity Localization)
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

    [Header("── Language ────────────────────────────")]
    [Tooltip("รายการภาษาเติมเองจาก Locale ที่มีใน Localization Settings — ไม่ต้องกรอก options")]
    public TMP_Dropdown languageDropdown;

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

        // Language — รายการ locale โหลดแบบ async ตอนเกมเริ่ม
        // ถ้าเปิดหน้านี้ก่อน init เสร็จ (เช่นเปิดจากซีนแรกสุด) รายการจะว่าง
        // จึงรอ InitializationOperation ให้จบก่อนค่อยเติม
        if (languageDropdown != null)
        {
            var init = LocalizationSettings.InitializationOperation;
            if (init.IsDone) PopulateLanguages();
            else             init.Completed += _ => PopulateLanguages();

            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
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
        if (languageDropdown)    languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
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

    // ── Language ──────────────────────────────────────────────────────────
    /// <summary>locale ที่เรียงตรงกับ index ใน dropdown — เก็บไว้เพราะ dropdown คืนมาแค่ index</summary>
    readonly List<Locale> _locales = new();

    void PopulateLanguages()
    {
        if (languageDropdown == null) return;

        _locales.Clear();
        var available = LocalizationSettings.AvailableLocales;
        if (available != null) _locales.AddRange(available.Locales);

        if (_locales.Count == 0)
        {
            Debug.LogWarning("[Settings] ไม่มี Locale ใน Localization Settings — ช่องเลือกภาษาจะว่าง " +
                             "สร้างที่ Edit > Project Settings > Localization > Locale Generator");
            languageDropdown.ClearOptions();
            return;
        }

        // ชื่อที่โชว์ใช้ LocaleName ของแต่ละภาษา (เช่น "English", "Thai (Thailand)")
        // จงใจไม่แปลชื่อภาษา — คนที่เผลอตั้งภาษาที่อ่านไม่ออกต้องหาทางกลับได้
        var names = new List<string>(_locales.Count);
        foreach (var l in _locales) names.Add(l.LocaleName);

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(names);

        int current = _locales.IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.SetValueWithoutNotify(current >= 0 ? current : 0);
        languageDropdown.RefreshShownValue();
    }

    /// <summary>
    /// เปลี่ยน locale ทันที ไม่ต้องรีสตาร์ต
    ///
    /// ข้อความที่ผูกผ่าน LocalizeStringEvent กับ LocalizedString อัปเดตตามเอง
    /// แต่แผงที่วาดค้างไว้แล้วและอ่านค่าครั้งเดียวตอนเปิด (เช่น CharacterSelectUI)
    /// จะยังเป็นภาษาเดิมจนกว่าจะเปิดใหม่ — ถ้าต้องการให้รีเฟรชทันที
    /// ให้แผงนั้น subscribe LocalizationSettings.SelectedLocaleChanged
    ///
    /// การจำภาษาข้ามรอบเล่นเป็นหน้าที่ของ PlayerPrefLocaleSelector ใน Localization Settings
    /// ไม่ได้เขียน PlayerPrefs เองที่นี่ เพราะตัวนั้นทำตอนเปิดเกมได้ด้วย ซึ่ง UI ทำไม่ได้
    /// </summary>
    void OnLanguageChanged(int index)
    {
        if (index < 0 || index >= _locales.Count) return;
        LocalizationSettings.SelectedLocale = _locales[index];
    }

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
