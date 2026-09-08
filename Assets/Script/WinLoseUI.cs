using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// หน้าจอ Win / Lose
/// รับ event จาก GameTimeline.OnGameWon / OnGameLost
/// </summary>
public class WinLoseUI : MonoBehaviour
{
    public static WinLoseUI Instance { get; private set; }

    /// <summary>true เมื่อ panel ถูกเรียกแสดง (Win/Lose triggered) — UI อื่นใช้เช็คเพื่อซ่อนตัวเอง</summary>
    public static bool IsShowing { get; private set; }

    /// <summary>ยิงเมื่อ Win/Lose ถูก trigger — UI อื่น subscribe เพื่อซ่อนตัวเอง</summary>
    public static event System.Action OnAnyResultTriggered;

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Result")]
    public TextMeshProUGUI resultLabel;    // "VICTORY!" / "DEFEAT"
    public Color winColor  = new Color(1f, 0.9f, 0.1f);
    public Color loseColor = new Color(0.8f, 0.2f, 0.2f);

    [Header("Stats")]
    public TextMeshProUGUI timeLabel;      // "Time:  15:32"
    public TextMeshProUGUI levelLabel;     // "Level: 8"
    public TextMeshProUGUI waveLabel;      // "Wave:  12"

    [Header("Meta Reward")]
    public TextMeshProUGUI goldEarnedLabel;  // "+ 1,240 G"
    public TextMeshProUGUI goldTotalLabel;   // "รวม 8,430 G"

    [Header("Button")]
    public Button returnButton;
    public Button playAgainButton;

    [Header("Animation")]
    [Tooltip("วินาทีก่อน panel จะแสดง (เวลาระเบิด fade ฯลฯ)")]
    public float showDelay = 1.5f;
    [Tooltip("วินาทีที่ fade in")]
    public float fadeInDuration = 0.8f;

    private CanvasGroup canvasGroup;

    /// <summary>กดเริ่มรันใหม่ไปแล้ว — LoadScene เป็น async ปุ่มยังรับคลิกได้จนกว่าซีนจะสลับจริง
    /// เหตุผลเดียวกับที่ ReturnToMenu ปิดปุ่มทั้งสองไว้ระหว่าง await</summary>
    private bool restarting;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        IsShowing = false;

        canvasGroup = panelRoot?.GetComponent<CanvasGroup>();
        if (panelRoot) panelRoot.SetActive(false);

        if (returnButton)    returnButton.onClick.AddListener(ReturnToMenu);
        if (playAgainButton) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
    }

    void OnEnable()
    {
        GameTimeline.OnGameWon  += OnWin;
        GameTimeline.OnGameLost += OnLose;
        CloneSwarm.Meta.RunRewardTracker.OnRewardGranted += OnRewardGranted;
    }

    void OnDisable()
    {
        GameTimeline.OnGameWon  -= OnWin;
        GameTimeline.OnGameLost -= OnLose;
        CloneSwarm.Meta.RunRewardTracker.OnRewardGranted -= OnRewardGranted;
    }

    /// <summary>
    /// RunRewardTracker จ่ายทองมาแล้ว — อาจมาถึงก่อนหรือหลัง panel แสดง
    /// จึงเก็บค่าไว้แล้วเขียนทับทั้งสองทาง
    /// </summary>
    void OnRewardGranted(int goldEarned, int goldTotal)
    {
        pendingGoldEarned = goldEarned;
        pendingGoldTotal  = goldTotal;
        RefreshGoldLabels();
    }

    int pendingGoldEarned = -1;
    int pendingGoldTotal;

    void RefreshGoldLabels()
    {
        if (pendingGoldEarned < 0) return;
        if (goldEarnedLabel) goldEarnedLabel.text = $"+ {pendingGoldEarned:N0} G";
        if (goldTotalLabel)  goldTotalLabel.text  = $"รวม {pendingGoldTotal:N0} G";
    }

    // ── Event Handlers ────────────────────────────────────────────────────
    void OnWin(float gameTimeSec, int level)
    {
        string timeStr  = FormatTime(gameTimeSec);
        int    wave     = WaveManager.Instance?.GetCurrentWave() ?? 0;
        IsShowing = true;
        OnAnyResultTriggered?.Invoke();
        StartCoroutine(ShowPanel(true, timeStr, level, wave));
    }

    void OnLose(float gameTimeSec, int level)
    {
        string timeStr = FormatTime(gameTimeSec);
        int    wave    = WaveManager.Instance?.GetCurrentWave() ?? 0;
        IsShowing = true;
        OnAnyResultTriggered?.Invoke();
        StartCoroutine(ShowPanel(false, timeStr, level, wave));
    }

    // ── Show Panel ────────────────────────────────────────────────────────
    IEnumerator ShowPanel(bool isWin, string time, int level, int wave)
    {
        yield return new WaitForSecondsRealtime(showDelay);

        // ตั้งค่า text
        if (resultLabel)
        {
            resultLabel.text  = isWin ? "VICTORY!" : "DEFEAT";
            resultLabel.color = isWin ? winColor : loseColor;
        }
        if (timeLabel)  timeLabel.text  = $"Time    {time}";
        if (levelLabel) levelLabel.text = $"Level   {level}";
        if (waveLabel)  waveLabel.text  = $"Wave    {wave}";

        // รางวัลอาจมาถึงก่อน panel แสดง — เขียนค่าที่ค้างไว้ตรงนี้
        if (goldEarnedLabel && pendingGoldEarned < 0) goldEarnedLabel.text = "...";
        RefreshGoldLabels();

        // Fade in
        if (panelRoot) panelRoot.SetActive(true);

        if (playAgainButton != null)
        {
            bool isHost = GameSessionManager.Instance?.IsHost ?? true;
            playAgainButton.interactable = isHost;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // หยุดเกมหลัง fade in เสร็จ (ไม่ให้ enemy ยังวิ่ง)
        GamePause.Add(PauseReason.GameOver);
    }

    void OnPlayAgainClicked()
    {
        if (restarting) return;
        restarting = true;

        if (playAgainButton) playAgainButton.interactable = false;
        if (returnButton)    returnButton.interactable    = false;

        // ปลด timeScale ก่อนสั่งโหลด — NGO รอ scene event ด้วยเวลาที่ถูก scale บางเส้นทาง
        IsShowing = false;
        GamePause.ResetAll();

        string sceneName = RunSetup.Map != null ? RunSetup.Map.sceneName : "SampleScene";

        bool started;
        if (GameSessionManager.Instance != null)
        {
            started = GameSessionManager.Instance.StartGame(sceneName);
        }
        else if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            started = true;
        }
        else
        {
            Debug.LogWarning("[WinLose] Play Again: ไม่มีทั้ง GameSessionManager และ NetworkManager ฝั่ง server — เริ่มรันใหม่ไม่ได้");
            started = false;
        }

        // ไม่ได้โหลดซีนจริง — คืนสถานะเดิมทั้งหมด ไม่งั้นเหลือจอผลลัพธ์ที่กดอะไรไม่ได้
        // และเกมเดินต่อทั้งที่ผู้เล่นตายหมดแล้ว
        if (!started)
        {
            restarting = false;
            IsShowing  = true;
            GamePause.Add(PauseReason.GameOver);
            if (playAgainButton) playAgainButton.interactable = true;
            if (returnButton)    returnButton.interactable    = true;
        }
    }

    // ── Return to Menu ────────────────────────────────────────────────────
    async void ReturnToMenu()
    {
        IsShowing = false;
        GamePause.ResetAll();

        // กันกดซ้ำระหว่าง await — LoadScene ยังไม่เกิด ปุ่มยังรับคลิกได้อยู่
        if (returnButton)    returnButton.interactable    = false;
        if (playAgainButton) playAgainButton.interactable = false;

        // ไม่ทำข้อนี้ = GameSessionManager (DontDestroyOnLoad) ถือ session ตายข้ามซีน
        // แล้วเมนูจะโชว์รหัสห้องของห้องที่ไม่มีใครอยู่
        if (GameSessionManager.Instance != null)
            await GameSessionManager.Instance.LeaveSessionIfActiveAsync();

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
            nm.Shutdown();

        SceneManager.LoadScene("MenuScene");
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static string FormatTime(float s)
    {
        int m   = Mathf.FloorToInt(s / 60f);
        int sec = Mathf.FloorToInt(s % 60f);
        return $"{m:00}:{sec:00}";
    }
}
