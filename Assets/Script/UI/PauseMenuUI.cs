using System.Collections.Generic;
using CloneSwarm.UI.P3R;
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
///   • รายการเมนู 3 แถวสไตล์ P3R: กลับเข้าเกม / ตั้งค่า / ออกไปเมนูหลัก
///   • Slider Master / Music / SFX → ขับ SoundManager (อยู่ในแผงย่อยที่เปิดจากแถว "ตั้งค่า")
///   • แถวปาร์ตี้ + ป้ายเตือน CO-OP / SOLO
///
/// Setup:
///   1. ใน game scene สร้าง Canvas + Panel hierarchy ตามที่ออกแบบ
///      (หรือให้ Tools > Clone Swarm > Build P3R Pause Scene สร้างซีนต้นแบบให้ทั้งซีน)
///   2. วาง GameObject ที่มี PauseMenuUI component (panelRoot ชี้ไป Panel)
///   3. ลาก Slider / Text / Button refs เข้า Inspector
///   4. กำหนด menuSceneName = "MenuScene"
///
/// หมายเหตุ:
///   • Solo: Time.timeScale หยุด / Multiplayer: LocalInputSuspended ระงับ input ของเครื่องตัวเอง
///   • Quit = NetworkManager.Shutdown + load MenuScene
///
/// ═══════════════════════════════════════════════════════════════════════
/// **ทำไมสไลเดอร์เสียงยังอยู่ในไฟล์นี้ ทั้งที่ handoff บอกให้ย้ายไปจอ Config**
///
/// handoff เขียนว่า "สไลเดอร์เสียงกับปุ่ม reset ย้ายไปใช้ที่จอ Config" — แต่ตรวจแล้วพบว่า
/// `SettingsMenuUI` ถูกวางไว้ใน `MenuScene.unity` เท่านั้น **ไม่มีอยู่ใน `SampleScene.unity`**
/// (ตรวจด้วย guid ของสคริปต์: SampleScene = 0 อ้างอิง · MenuScene = 1)
/// แปลว่าในซีนเกม "จอ Config" ไม่มีอยู่จริง — ลบสไลเดอร์ทิ้งตามตัวอักษรของ handoff
/// จะได้จอ pause ที่ไม่มีทางปรับเสียงเลยระหว่างเล่น และสายที่ต่อไว้แล้วใน SampleScene
/// (masterSlider / musicSlider / sfxSlider / label 3 ตัว / resetDefaultsButton — ต่อครบทุกช่อง)
/// จะหลุดเงียบๆ ตอนรัน ไม่ใช่ตอนคอมไพล์
///
/// ทางประนีประนอมที่เลือก: **เก็บ field เดิมไว้ทุกตัว** แล้วย้ายของพวกนี้ไปอยู่ในแผงย่อย
/// <see cref="settingsSubPanel"/> ที่เปิดจากแถว `02 ตั้งค่า` แทน — ตัวจอ pause หลักสะอาดตาม
/// แบบ (เหลือแค่ 3 แถว + ปาร์ตี้ + ป้ายเตือน) โดยไม่ทำสายที่ต่อไว้ขาด
/// เมื่อไหร่ที่ย้าย `SettingsMenuUI` เข้าซีนเกมได้จริง ค่อยเปลี่ยน <see cref="OnMenuConfirm"/>
/// ให้ไปเปิดจอนั้นแทน แล้วค่อยเลิกใช้แผงย่อยนี้ทีเดียว
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════
    // id ของรายการเมนู — ตรงกับ P3RMenuItem.id ที่ builder ใส่ให้
    // ใช้ string ไม่ใช่ index ตามแพตเทิร์นของ P3RMenuList (สลับลำดับแล้วไม่พัง)
    // ═══════════════════════════════════════════════════════════════════
    public const string MenuIdResume   = "resume";
    public const string MenuIdSettings = "settings";
    public const string MenuIdQuit     = "quit";

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

    // ═══════════════════════════════════════════════════════════════════
    // P3R LAYOUT — ทั้งบล็อกนี้เป็นของใหม่ · ปล่อยว่างได้ทุกช่อง
    // ซีนเก่าที่ยังไม่ได้ต่อสายจะทำงานเหมือนเดิมทุกอย่าง (ทุกจุดมี null guard)
    // ═══════════════════════════════════════════════════════════════════
    [Header("P3R — Menu List")]
    [Tooltip("รายการ 3 แถว · id ต้องเป็น resume / settings / quit\n" +
             "ปล่อยว่างได้ถ้าซีนยังใช้ปุ่มแบบเก่า")]
    public P3RMenuList menuList;

    [Header("P3R — Settings Sub-Panel")]
    [Tooltip("แผงย่อยที่เก็บสไลเดอร์เสียง — เปิดจากแถว `02 ตั้งค่า`\n" +
             "ทางประนีประนอม: ซีนเกมยังไม่มีจอ Config จริง (ดูคำอธิบายหัวไฟล์)")]
    public GameObject settingsSubPanel;
    public Button settingsBackButton;

    [Header("P3R — Party Rows")]
    [Tooltip("ที่วางแถวปาร์ตี้ — pivot ล่าง แถวงอกขึ้นบน")]
    public RectTransform partyRowContainer;
    [Tooltip("แถวต้นแบบที่ถูกโคลน · ต้อง SetActive(false) ไว้ · builder สร้างให้เป็นลูกของ container")]
    public PausePartyRowUI partyRowTemplate;
    [Tooltip("สูงต่อแถว (px ที่กรอบ 1920×1080)")]
    public float partyRowHeight = 90f;
    [Tooltip("ระยะห่างระหว่างแถว")]
    public float partyRowSpacing = 12f;

    [Header("P3R — CO-OP / SOLO Badge")]
    public GameObject coopBadgeRoot;
    [Tooltip("พื้นป้าย — co-op ใช้แดงจาง solo ใช้โทนกลาง")]
    public Image coopBadgeBackground;
    [Tooltip("เส้นเน้นซ้าย 6px")]
    public Image coopBadgeAccent;
    [Tooltip("ป้ายหัว mono — `CO-OP` / `SOLO`")]
    public TextMeshProUGUI coopBadgeTag;
    [Tooltip("ข้อความอธิบายใต้ป้ายหัว")]
    public TextMeshProUGUI coopBadgeText;

    // ── Internal state ────────────────────────────────────────────────────
    bool isPaused;
    float partyTickTimer;

    /// <summary>แถวที่โคลนออกมาแล้ว — ใช้ซ้ำ ไม่ Destroy ทิ้งทุกรอบ</summary>
    readonly List<PausePartyRowUI> partyRows = new();

    /// <summary>buffer ของ playermove ที่เจอในรอบนี้ — reuse กัน alloc ทุก 0.25 วิ</summary>
    readonly List<playermove> playerBuffer = new();

    const float PartyTickInterval = 0.25f;

    // ── สีป้ายเตือน (ค่าตรงจาก design handoff) ───────────────────────────
    static readonly Color CoopBg     = new Color(216f / 255f, 32f / 255f, 32f / 255f, 0.10f);
    static readonly Color CoopAccent = new Color32(0xD8, 0x20, 0x20, 0xFF);
    static readonly Color CoopTag    = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
    // solo: "โทนกลาง ไม่ใช้แดง" — ใช้ขาวจางบนพื้นการ์ด ไม่ยืมสีอื่นในพาเลตต์มาสร้างความหมายใหม่
    static readonly Color SoloBg     = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color SoloAccent = new Color(1f, 1f, 1f, 0.30f);
    static readonly Color SoloTag    = new Color(1f, 1f, 1f, 0.70f);

    const string CoopBadgeText = "อยู่ในห้องกับเพื่อน — โลกยังเดินอยู่ ตัวละครยังโดนตีได้";
    const string SoloBadgeText = "เกมหยุดจริง";

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        // ปิด panel ตอนเริ่ม — open ผ่าน ESC เท่านั้น
        if (panelRoot != null) panelRoot.SetActive(false);
        if (settingsSubPanel != null) settingsSubPanel.SetActive(false);
        if (partyRowTemplate != null) partyRowTemplate.gameObject.SetActive(false);

        // ESC เป็นของไฟล์นี้คนเดียว — ถ้าปล่อยให้ P3RMenuList ฟังด้วย จะโดนสองเด้งในเฟรมเดียว
        // (list ยิง OnCancel + Update() ที่นี่เรียก Toggle) แล้วเมนูจะกะพริบเปิด-ปิดทันที
        if (menuList != null) menuList.listenEscape = false;
    }

    void OnEnable()
    {
        if (masterSlider) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        if (resumeButton)        resumeButton.onClick.AddListener(Resume);
        if (quitButton)          quitButton.onClick.AddListener(OnQuitClicked);
        if (resetDefaultsButton) resetDefaultsButton.onClick.AddListener(OnResetDefaults);
        if (settingsBackButton)  settingsBackButton.onClick.AddListener(CloseSettings);

        if (menuList != null) menuList.OnConfirm += OnMenuConfirm;

        // Multiplayer: โลกยังเดินตอนเมนู pause เปิด → overlay priority สูงกว่าเด้งทับได้
        // ปิดเมนูให้เองแทนที่จะปล่อยให้ซ้อนกัน
        SharedExperienceManager.OnUpgradePhaseStart += HandleUpgradePhaseStart;
        SharedExperienceManager.OnOrbPhaseStart     += ForceResume;
        WinLoseUI.OnAnyResultTriggered              += ForceResume;
    }

    void OnDisable()
    {
        if (masterSlider) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (musicSlider)  musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider)    sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);

        if (resumeButton)        resumeButton.onClick.RemoveListener(Resume);
        if (quitButton)          quitButton.onClick.RemoveListener(OnQuitClicked);
        if (resetDefaultsButton) resetDefaultsButton.onClick.RemoveListener(OnResetDefaults);
        if (settingsBackButton)  settingsBackButton.onClick.RemoveListener(CloseSettings);

        if (menuList != null) menuList.OnConfirm -= OnMenuConfirm;

        SharedExperienceManager.OnUpgradePhaseStart -= HandleUpgradePhaseStart;
        SharedExperienceManager.OnOrbPhaseStart     -= ForceResume;
        WinLoseUI.OnAnyResultTriggered              -= ForceResume;

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
        {
            // ESC ตอนแผงย่อยเปิดอยู่ = ถอยกลับหนึ่งชั้น ไม่ใช่ปิดเมนูทั้งจอ
            if (isPaused && IsSettingsOpen) CloseSettings();
            else                            Toggle();
            return;
        }

        if (!isPaused) return;

        // co-op โลกยังเดินอยู่ HP เปลี่ยนได้ตลอดเวลาที่เมนูค้าง → ต้อง tick ซ้ำ
        // unscaledDeltaTime เพราะ solo timeScale = 0 (scaled จะค้างที่ 0 ตลอดกาล)
        partyTickTimer += Time.unscaledDeltaTime;
        if (partyTickTimer >= PartyTickInterval)
        {
            partyTickTimer = 0f;
            RefreshParty();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    public bool IsSettingsOpen => settingsSubPanel != null && settingsSubPanel.activeSelf;

    public void Toggle()
    {
        if (WinLoseUI.IsShowing) return;
        if (GamePause.Has(PauseReason.PhaseSelect)) return;
        if (isPaused) Resume(); else Pause();
    }

    public void Pause()
    {
        if (isPaused) return;

        // PauseReason เรียง priority ไว้แล้ว (PauseMenu = ต่ำสุด) — บังคับใช้ที่ชั้น UI ด้วย
        // ไม่งั้นเมนูจะเปิดทับแผงเลือกการ์ด / หน้าจบเกม แล้วกดอะไรไม่ได้จนต้องปิดเกมทิ้ง
        if (IsHigherPriorityOverlayOpen()) return;

        isPaused = true;

        bool isMultiplayer = IsCoop();

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

        // ต้องอยู่ **หลัง** SetActive(true) เสมอ — SetActive เรียก Awake/OnEnable ของ
        // P3RMenuList ทันทีแบบ synchronous ซึ่ง Awake ตัวนั้นเรียก P3RMenuItem.Apply()
        // ที่บังคับ label ชิดขวา · ทับทีหลังจึงเป็นทางเดียวที่ได้ผลแน่นอน
        ApplyLeftAlignedRows();

        CloseSettings();
        partyTickTimer = 0f;
        RefreshParty();
    }

    public void Resume()
    {
        if (!isPaused) return;
        isPaused = false;
        GamePause.Remove(PauseReason.PauseMenu);
        GamePause.SuspendLocalInput(false);
        CloseSettings();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Menu list ─────────────────────────────────────────────────────────
    void OnMenuConfirm(string id)
    {
        switch (id)
        {
            case MenuIdResume:   Resume();       break;
            case MenuIdSettings: OpenSettings(); break;
            case MenuIdQuit:     OnQuitClicked(); break;
            default:
                Debug.LogWarning($"[PauseMenuUI] ไม่รู้จักรายการเมนู id = '{id}' — " +
                                 $"ต้องเป็น {MenuIdResume} / {MenuIdSettings} / {MenuIdQuit}");
                break;
        }
    }

    /// <summary>
    /// จอ pause ชิดซ้าย แต่ <see cref="P3RMenuItem.Apply"/> ฮาร์ดโค้ด
    /// <c>TextAlignmentOptions.Right</c> ไว้ (เมนูหลักชิดขวา) และถูกเรียกจาก
    /// <c>P3RMenuList.Awake()</c> ทุกครั้งที่ panel ถูกเปิด
    ///
    /// **ทำไมไม่แก้ P3RMenuItem ให้รองรับชิดซ้าย:** ไฟล์นั้นเมนูหลักใช้ร่วมอยู่
    /// การเพิ่มโหมด alignment เข้าไปแปลว่าต้องเพิ่มฟิลด์ใน P3RTheme ด้วย (theme เป็นแหล่งเดียว
    /// ของทุกค่าที่ตาเห็น) แล้วเมนูหลักจะได้ฟิลด์ที่ไม่มีวันใช้ติดมาถาวร
    /// ที่นี่แลกด้วยการทับค่าเดียว (alignment) หลัง Apply — สั้นกว่า และไม่แตะไฟล์ที่คนอื่นใช้อยู่
    ///
    /// ค่าที่เหลือทั้งหมด (ขนาดฟอนต์ 62 · แถบ 760×58 · ยื่นซ้าย -300) เดินทางมาทาง
    /// P3RTheme_Pause.asset ตามปกติ — ดูคำอธิบายการแม็ปค่าใน P3RPauseSceneBuilder
    /// </summary>
    void ApplyLeftAlignedRows()
    {
        if (menuList == null) return;
        foreach (var item in menuList.items)
        {
            if (item == null || item.label == null) continue;
            item.label.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    // ── Settings sub-panel ────────────────────────────────────────────────
    public void OpenSettings()
    {
        if (settingsSubPanel == null) return;
        SyncFromSoundManager();
        settingsSubPanel.SetActive(true);
        // ปิดเฉพาะ component ไม่ใช่ทั้ง GameObject — แถวเมนูยังต้องเห็นอยู่ข้างหลังแผงย่อย
        // แต่ต้องไม่กินลูกศร/Enter ไปเลื่อนรายการที่มองไม่เห็นว่ากำลังเลื่อน
        if (menuList != null) menuList.enabled = false;
    }

    public void CloseSettings()
    {
        if (settingsSubPanel != null) settingsSubPanel.SetActive(false);
        if (menuList != null) menuList.enabled = true;
    }

    // ── Overlay priority ──────────────────────────────────────────────────
    /// <summary>
    /// มี overlay เต็มจอที่ priority สูงกว่าเมนู pause ถืออยู่หรือไม่
    /// (เลือกการ์ดตอนเลเวลอัป / orb phase / หน้าจบเกม)
    /// </summary>
    static bool IsHigherPriorityOverlayOpen()
        => WinLoseUI.IsShowing
        || GamePause.Has(PauseReason.PhaseSelect)
        || GamePause.Has(PauseReason.GameOver);

    /// <summary>ปิดเมนูเงียบๆ เมื่อ overlay priority สูงกว่าเปิดขึ้นมาทับ</summary>
    void ForceResume() => Resume();

    void HandleUpgradePhaseStart(int _) => ForceResume();

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

    // ═══════════════════════════════════════════════════════════════════
    // PARTY ROWS
    //
    // **ที่มาของข้อมูลแต่ละช่อง** (ทั้งหมดอ่านได้โดยไม่ต้องแก้ไฟล์อื่น):
    //   ชื่อ      · PlayerVisual.GetCharacterData(CharacterIndex).DisplayName
    //             (ห้ามใช้ characterName ตรงๆ — นั่นคือ id ที่ห้ามแปล)
    //   พอร์เทรต · CharacterData.portrait — ตอนนี้ยังว่างเกือบทุกตัว จึงต้องรองรับ null
    //   ช่องผู้เล่น · PlayerSlotRegistry.Instance.GetSlot(clientId) — CLAUDE.md ข้อ 11
    //             ห้ามใช้ clientId % 4 เด็ดขาด
    //   HP       · playermove.netHealth / netMaxHealth (NetworkVariable · Everyone read)
    //   ตาย      · playermove.isDead
    //   เลเวล    · SharedExperienceManager.GetCurrentLevel() — XP โปรเจกต์นี้เป็น shared pool
    //             เลเวลจึง **เท่ากันทุกคนโดยดีไซน์** ไม่ใช่ค่าต่อคน (แบบเขียน Lv 14 ต่อแถว
    //             ซึ่งอ่านได้ทั้งสองแบบ — ที่นี่เลือกตามความจริงของระบบ)
    // ═══════════════════════════════════════════════════════════════════
    void RefreshParty()
    {
        UpdateCoopBadge();

        if (partyRowContainer == null || partyRowTemplate == null) return;

        CollectPlayers();

        // ให้มีแถวพอ — โคลนจากต้นแบบในซีน (ไม่ใช้ prefab เพราะแถวนี้เป็นของจอนี้จอเดียว
        // สร้างเป็น prefab แยกแล้วต้องคอยตามซิงก์เวลาปรับดีไซน์)
        while (partyRows.Count < playerBuffer.Count)
        {
            var row = Instantiate(partyRowTemplate, partyRowContainer);
            row.name = $"PartyRow_{partyRows.Count}";
            partyRows.Add(row);
        }

        int level = SharedExperienceManager.Instance != null
            ? SharedExperienceManager.Instance.GetCurrentLevel()
            : 1;

        for (int i = 0; i < partyRows.Count; i++)
        {
            var row = partyRows[i];
            if (row == null) continue;

            if (i >= playerBuffer.Count)
            {
                row.gameObject.SetActive(false);
                continue;
            }

            var pm = playerBuffer[i];
            row.gameObject.SetActive(true);

            var rt = (RectTransform)row.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * (partyRowHeight + partyRowSpacing));

            bool isHost = NetworkManager.Singleton != null &&
                          pm.OwnerClientId == NetworkManager.ServerClientId;

            string displayName = "?";
            Sprite portrait    = null;
            if (pm.TryGetComponent(out PlayerVisual visual))
            {
                var cd = visual.GetCharacterData(visual.CharacterIndex);
                if (cd != null)
                {
                    displayName = cd.DisplayName;
                    portrait    = cd.portrait;
                }
            }

            int slot = PlayerSlotRegistry.Instance != null
                ? PlayerSlotRegistry.Instance.GetSlot(pm.OwnerClientId)
                : -1;
            string slotLabel = slot >= 0 ? $"P{slot + 1}" : "P?";
            string subLabel  = isHost ? $"{slotLabel} · HOST" : slotLabel;

            row.SetIdentity(displayName, subLabel, portrait, pm.IsOwner || isHost);
            row.SetVitals(level, pm.netHealth.Value, pm.netMaxHealth.Value, pm.isDead.Value);
        }

        // container pivot อยู่ล่าง → เพิ่มความสูงแล้วแถวงอกขึ้นบน ป้าย CO-OP ที่อยู่ใต้ลงไปไม่ขยับ
        int n = playerBuffer.Count;
        float h = n > 0 ? n * partyRowHeight + (n - 1) * partyRowSpacing : 0f;
        partyRowContainer.sizeDelta = new Vector2(partyRowContainer.sizeDelta.x, h);
    }

    /// <summary>เก็บผู้เล่นในห้องจริง เรียงตามช่องที่ลงทะเบียนไว้ (ไม่ใช่ลำดับที่ Unity คืนมา)</summary>
    void CollectPlayers()
    {
        playerBuffer.Clear();
        var found = FindObjectsByType<playermove>(FindObjectsSortMode.None);
        if (found == null) return;

        foreach (var pm in found)
            if (pm != null) playerBuffer.Add(pm);

        var reg = PlayerSlotRegistry.Instance;
        playerBuffer.Sort((a, b) =>
        {
            int sa = reg != null ? reg.GetSlot(a.OwnerClientId) : -1;
            int sb = reg != null ? reg.GetSlot(b.OwnerClientId) : -1;
            // ยังไม่ได้ช่อง (-1) ให้ไปท้ายแถว แล้วค่อยเรียงด้วย clientId เป็นตัวตัดสิน
            if (sa < 0) sa = int.MaxValue;
            if (sb < 0) sb = int.MaxValue;
            int c = sa.CompareTo(sb);
            return c != 0 ? c : a.OwnerClientId.CompareTo(b.OwnerClientId);
        });
    }

    // ── CO-OP / SOLO badge ────────────────────────────────────────────────
    /// <summary>
    /// solo กับ co-op ต่างกันที่ "โลกหยุดจริงไหม" ไม่ใช่แค่จำนวนคน — เงื่อนไขตรงนี้
    /// จึงต้องเป็นตัวเดียวกับที่ <see cref="Pause"/> ใช้ตัดสินว่าจะ Add(PauseMenu) หรือ
    /// SuspendLocalInput ไม่งั้นป้ายจะโกหกผู้เล่น
    /// </summary>
    static bool IsCoop()
        => NetworkManager.Singleton != null &&
           NetworkManager.Singleton.IsListening &&
           NetworkManager.Singleton.ConnectedClients.Count > 1;

    void UpdateCoopBadge()
    {
        if (coopBadgeRoot == null) return;
        coopBadgeRoot.SetActive(true);

        bool coop = IsCoop();

        if (coopBadgeBackground) coopBadgeBackground.color = coop ? CoopBg     : SoloBg;
        if (coopBadgeAccent)     coopBadgeAccent.color     = coop ? CoopAccent : SoloAccent;
        if (coopBadgeTag)
        {
            coopBadgeTag.text  = coop ? "CO-OP" : "SOLO";
            coopBadgeTag.color = coop ? CoopTag : SoloTag;
        }
        if (coopBadgeText) coopBadgeText.text = coop ? CoopBadgeText : SoloBadgeText;
    }

    // ── Sync sliders from SoundManager (เมื่อเปิด menu) ──────────────────
    void SyncFromSoundManager()
    {
        var sm = SoundManager.Instance;
        if (sm == null) return;
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
