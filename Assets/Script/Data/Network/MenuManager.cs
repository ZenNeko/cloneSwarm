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
    [Tooltip("จอโหลดสไตล์ P3R บน loadingPanel — ปล่อยว่างได้ จะเหลือแค่ loadingText แบบเดิม\n" +
             "P3RScreenWirer เติมช่องนี้ให้เองเพราะ LoadingScreenUI อยู่ในรายชื่อตัวคุมจอ")]
    public CloneSwarm.UI.P3R.LoadingScreenUI loadingScreenUI;

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// true = เข้า MenuScene รอบนี้เพราะเพิ่งเล่นจบ ไม่ใช่เพราะเพิ่งเปิดเกม
    ///
    /// static เพราะต้องข้ามซีน — ตัวตั้งค่าคือ `WinLoseUI.ReturnToMenu` ซึ่งอยู่ใน
    /// SampleScene แล้วตายไปพร้อมซีนก่อนที่ `MenuManager` จะเกิด · จะส่งต่อด้วย
    /// reference ไม่ได้ และไม่คุ้มที่จะทำ object DontDestroyOnLoad เพิ่มเพื่อ bool ตัวเดียว
    ///
    /// `MenuManager.Start` ล้างทิ้งทันทีหลังใช้ — เปิดเกมรอบหน้าต้องเห็นจอไตเติลตามปกติ
    /// </summary>
    public static bool ReturningFromRun;

    void Awake()
    {
        if (versionText) versionText.text = $"v{Application.version}";
    }

    void OnEnable()
    {
        CloneSwarm.Meta.MetaProgression.OnGoldChanged += HandleGoldChanged;
        SettingsMenuUI.OnBack                         += ShowMain;
        LobbyUI.OnBack                                += ShowMain;
        LobbyUI.OnRunStarting                          += ShowLoading;
        JoinRoomPanel.OnJoined                         += HandleJoined;
        JoinRoomPanel.OnJoinFailed                     += HandleJoinFailed;
        GameSessionManager.OnSessionJoined             += HandleSessionJoined;
    }

    void OnDisable()
    {
        CloneSwarm.Meta.MetaProgression.OnGoldChanged -= HandleGoldChanged;
        SettingsMenuUI.OnBack                         -= ShowMain;
        LobbyUI.OnBack                                -= ShowMain;
        LobbyUI.OnRunStarting                          -= ShowLoading;
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
        //
        // **ยกเว้นตอนกลับมาจากเกม** — จอไตเติลเป็นพิธีเปิดของการเปิดเกม ไม่ใช่ของ
        // การจบรอบ · คนที่เพิ่งเล่นจบแล้วกด "กลับเมนู" ต้องการเมนูหลัก ไม่ใช่ถูกส่งกลับ
        // ไปกดผ่านจอไตเติลอีกรอบทุกครั้งที่เล่นจบ
        if (titlePanel != null && !ReturningFromRun) ShowTitle();
        else                                        ShowMain();

        ReturningFromRun = false;   // ใช้ครั้งเดียวแล้วล้าง — เปิดเกมรอบหน้าต้องเห็นไตเติล
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
    /// คลุมช่วงรอยต่อระหว่างกด START RUN กับซีนเกมโผล่
    ///
    /// **จอนี้เคยถูกสร้าง ย้าย และต่อสายครบ แต่ไม่มีบรรทัดไหนเปิดมันเลย** — `loadingPanel`
    /// ถูกอ้างถึงสามที่และทั้งสามที่คือการปิด · ผู้เล่นจึงมองล็อบบี้ค้างอยู่ตลอดช่วงโหลด
    /// ซึ่งแยกไม่ออกจาก "กดแล้วไม่มีอะไรเกิดขึ้น"
    ///
    /// `NetworkManager.SceneManager.LoadScene` ใช้ `LoadSceneMode.Single` ทั้ง MenuScene
    /// จะถูกทิ้งอยู่แล้ว จอนี้จึงไม่ต้องมีใครสั่งปิด — มันหายไปพร้อมซีน
    /// แถบความคืบหน้าเป็นแบบกวาด เพราะ NGO รายงานเป็นช่วงๆ ไม่มีค่าต่อเนื่องให้ดึง
    /// (ดูเหตุผลเต็มใน <see cref="CloneSwarm.UI.P3R.LoadingScreenUI"/>)
    /// </summary>
    public void ShowLoading(MapData map, DifficultyTier tier)
    {
        ShowPanel(loadingPanel);

        if (loadingScreenUI != null)
        {
            loadingScreenUI.SetContext(map != null ? map.DisplayName : "", tier.ToString());
            loadingScreenUI.SetArt(map != null ? map.previewImage : null);
        }

        // ป้ายเดิมของ MenuManager — ซีนที่ยังไม่ได้ต่อ LoadingScreenUI ยังได้ข้อความบอกสถานะ
        if (loadingText != null)
            loadingText.text = map != null ? $"กำลังโหลด {map.DisplayName}…" : "กำลังโหลด…";
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

    /// <summary>
    /// ปิดเกม — เก็บกวาดเองตามลำดับก่อน แล้วค่อยสั่งปิด
    ///
    /// ═══ อาการที่แก้ ═══
    ///
    /// กด QUIT แล้ว Windows ขึ้น "CloneSwarm.exe is not responding" · ไม่ใช่แค่ช้า
    /// แต่ main thread ค้าง
    ///
    /// ของเดิมเรียก `Application.Quit()` ตรงๆ ทั้งที่ยังมี session ของ Relay เปิดอยู่
    /// และ NGO ยัง listening · Unity จึงไปเก็บกวาดทั้งสองอย่างให้เองตอนโปรเซสกำลังปิด
    /// ซึ่งเป็นจังหวะที่ SDK ต้องยิงเน็ตออกไปในขณะที่ระบบรอบตัวถูกรื้อไปแล้วครึ่งหนึ่ง
    ///
    /// ทางออกอื่นของเกมทุกทางเรียก `LeaveSessionIfActiveAsync` หมด (JoinRoomPanel ·
    /// LobbyUI · WinLoseUI) — **ทางปิดเกมเป็นทางเดียวที่ไม่เรียก** จึงเป็นทางเดียว
    /// ที่ทิ้งห้องค้างไว้ให้หมดอายุเอง คนอื่นยังเห็นห้องผีในลิสต์ด้วย
    ///
    /// ═══ ทำไมใช้ coroutine ไม่ใช่ async ═══
    ///
    /// `async void` บนปุ่มปิดเกมคือการรอบนบริบทที่กำลังจะหายไป · ถ้า await ไม่คืน
    /// ก็ไม่มีใครมาปลุกต่อ เพราะ synchronization context ถูกรื้อไปแล้ว
    /// coroutine เดินบนลูปของ Unity ตรงๆ และเราคุมเพดานเวลาได้เอง
    ///
    /// ═══ เพดานเวลา ═══
    ///
    /// การปิดเกมต้องไม่ขึ้นกับว่าเน็ตตอบไหม · ครบเวลาแล้วปิดเลย ยอมทิ้งห้องค้าง
    /// ดีกว่าค้างหน้าจอให้ผู้เล่นต้องไปฆ่าโปรเซสเอง
    /// </summary>
    public void OnQuitClicked()
    {
        if (_quitting) return;      // กดรัวไม่ทำให้เก็บกวาดซ้อนกัน
        _quitting = true;
        StartCoroutine(QuitRoutine());
    }

    private bool _quitting;

    /// <summary>วินาทีที่ยอมรอให้ออกจากห้องสำเร็จ ก่อนจะปิดทิ้งไปเลย</summary>
    private const float LeaveTimeout = 2f;

    private IEnumerator QuitRoutine()
    {
        // ── 1. ออกจากห้อง ────────────────────────────────────────────────
        var gsm = GameSessionManager.Instance;
        if (gsm != null)
        {
            var leave = gsm.LeaveSessionIfActiveAsync();
            float deadline = Time.realtimeSinceStartup + LeaveTimeout;

            while (!leave.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!leave.IsCompleted)
                Debug.LogWarning($"[Menu] ออกจากห้องไม่ทันใน {LeaveTimeout:0.#}s — ปิดเกมต่อเลย");
        }

        // ── 2. ปิด NGO เอง ให้เสร็จก่อนโปรเซสเริ่มรื้อตัวเอง ──────────────
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
            yield return null;      // ให้ Shutdown เดินจบหนึ่งเฟรม
        }

        // ── 3. ค่อยปิด ───────────────────────────────────────────────────
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
