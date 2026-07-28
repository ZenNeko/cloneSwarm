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

    [Header("Button")]
    public Button returnButton;

    [Header("Animation")]
    [Tooltip("วินาทีก่อน panel จะแสดง (เวลาระเบิด fade ฯลฯ)")]
    public float showDelay = 1.5f;
    [Tooltip("วินาทีที่ fade in")]
    public float fadeInDuration = 0.8f;

    private CanvasGroup canvasGroup;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        IsShowing = false;

        canvasGroup = panelRoot?.GetComponent<CanvasGroup>();
        if (panelRoot) panelRoot.SetActive(false);

        if (returnButton) returnButton.onClick.AddListener(ReturnToMenu);
    }

    void OnEnable()
    {
        GameTimeline.OnGameWon  += OnWin;
        GameTimeline.OnGameLost += OnLose;
    }

    void OnDisable()
    {
        GameTimeline.OnGameWon  -= OnWin;
        GameTimeline.OnGameLost -= OnLose;
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

        // Fade in
        if (panelRoot) panelRoot.SetActive(true);

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

    // ── Return to Menu ────────────────────────────────────────────────────
    void ReturnToMenu()
    {
        IsShowing = false;
        GamePause.ResetAll();

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
