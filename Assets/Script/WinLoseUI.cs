using System.Collections;
using System.Collections.Generic;
using CloneSwarm.Meta;
using CloneSwarm.UI.P3R;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// หน้าจอ Win / Lose
/// รับ event จาก GameTimeline.OnGameWon / OnGameLost
///
/// **ฟิลด์เก่าทุกตัวยังอยู่ครบ** — ซีนเดิมต่อสายไว้แล้ว ลบทิ้งคือพังเงียบตอนรัน
/// ของใหม่ที่ handoff ต้องการ (ป้ายแยกจากค่า, บัญชีรางวัลแยกเหตุผล, แถวปาร์ตี้)
/// เพิ่มเป็นฟิลด์ใหม่ที่ "ปล่อยว่างได้" ทั้งหมด ซีนเก่าจึงทำงานเหมือนเดิมไม่มีอะไรเปลี่ยน
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

    [Tooltip("แผ่นทแยงฝั่งซ้าย — เปลี่ยนสีตามผล · ปล่อยว่างได้ (ซีนเก่าไม่มี)\n" +
             "handoff ระบุว่าสีแผ่นนี้เป็นหนึ่งในสี่อย่างที่ต่างกันระหว่างจอชนะกับจอแพ้")]
    public Image diagonalPanel;
    public Color winDiagonalColor  = new Color32(0x18, 0x24, 0xD8, 0xFF);
    public Color loseDiagonalColor = new Color32(0x5A, 0x0E, 0x0E, 0xFF);

    [Tooltip("ป้ายรองเหนือคำผล — ปล่อยว่างได้ (ซีนเก่าไม่มี)")]
    public TextMeshProUGUI subtitleLabel;
    public string winSubtitle  = "ARENA 01 · เคลียร์แล้ว";
    public string loseSubtitle = "ARENA 01 · ปาร์ตี้ล้มทั้งทีม";

    [Header("Stats (ฟิลด์เดิม — โค้ดยัด prefix ให้เอง)")]
    public TextMeshProUGUI timeLabel;      // "Time    15:32"
    public TextMeshProUGUI levelLabel;     // "Level   8"
    public TextMeshProUGUI waveLabel;      // "Wave    12"

    [Header("Stats (ค่าล้วน — แบบใหม่ที่ป้ายเป็น TMP คนละตัว)")]
    [Tooltip("ค่าเวลาอย่างเดียว ไม่มีคำว่า Time — คู่กับป้าย TIME mono 15px ที่วางแยกในซีน")]
    public TextMeshProUGUI timeValueLabel;
    public TextMeshProUGUI levelValueLabel;
    public TextMeshProUGUI waveValueLabel;

    [Header("Meta Reward")]
    public TextMeshProUGUI goldEarnedLabel;  // "+ 1,240 G"
    public TextMeshProUGUI goldTotalLabel;   // "รวม 8,430 G"  (ฟิลด์เดิม มี prefix)

    [Tooltip("ยอดคลังแบบค่าล้วน '8,430 G' — คู่กับป้าย 'ยอดทองในคลัง' ที่วางแยกในซีน")]
    public TextMeshProUGUI goldTotalValueLabel;

    [Header("Reward Breakdown (บัญชีรางวัลแยกเหตุผล)")]
    [Tooltip("prefab ของแถวหนึ่งบรรทัด — ปล่อยว่าง = ไม่สร้างบัญชีแยก")]
    public RewardLineUI rewardLinePrefab;
    [Tooltip("ที่วางแถวบัญชี (ควรมี VerticalLayoutGroup)")]
    public RectTransform rewardLinesParent;

    [Header("Party")]
    public ResultPartyRowUI partyRowPrefab;
    public RectTransform    partyRowsParent;

    [Header("Button")]
    public Button returnButton;
    public Button playAgainButton;

    [Tooltip("CanvasGroup ของปุ่มเล่นอีกครั้ง — ใช้หรี่เป็น .45 ตอนเป็น client\nปล่อยว่าง = หาบนตัวปุ่มเอง (ใส่ให้ถ้ายังไม่มี)")]
    public CanvasGroup playAgainGroup;

    [Tooltip("ข้อความ 'รอโฮสต์เริ่มรอบใหม่' ใต้ปุ่ม — เปิดเฉพาะตอนเป็น client")]
    public GameObject waitingForHostLabel;

    [Range(0f, 1f)] public float disabledButtonAlpha = 0.45f;

    [Header("Animation")]
    [Tooltip("วินาทีก่อน panel จะแสดง (เวลาระเบิด fade ฯลฯ)")]
    public float showDelay = 1.5f;
    [Tooltip("วินาทีที่ fade in")]
    public float fadeInDuration = 0.8f;
    [Tooltip("วินาทีที่ตัวเลขทองไหลขึ้นจาก 0 — handoff: ~0.6s")]
    public float goldCountUpDuration = 0.6f;

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
        RunRewardTracker.OnRewardBreakdownGranted += OnRewardBreakdownGranted;
    }

    void OnDisable()
    {
        GameTimeline.OnGameWon  -= OnWin;
        GameTimeline.OnGameLost -= OnLose;
        RunRewardTracker.OnRewardBreakdownGranted -= OnRewardBreakdownGranted;
    }

    // ── Reward ────────────────────────────────────────────────────────────
    // RunRewardTracker จ่ายทองมาแล้ว — อาจมาถึงก่อนหรือหลัง panel แสดง
    // จึงเก็บค่าไว้แล้วเขียนทับทั้งสองทาง
    int  pendingGoldEarned = -1;
    int  pendingGoldTotal;
    RunRewardBreakdown pendingBreakdown;
    bool hasBreakdown;
    bool panelVisible;
    Coroutine goldTween;

    /// <summary>
    /// รับบัญชีที่ server คำนวณมาแล้ว — client ไม่คิดเลขเอง แค่แสดง
    /// (ตัวเลขทุกตัวใน breakdown เป็นทองของ "เครื่องนี้" หลังคูณ GoldFind แล้ว)
    /// </summary>
    void OnRewardBreakdownGranted(RunRewardBreakdown b, int vaultGold)
    {
        pendingBreakdown  = b;
        hasBreakdown      = true;
        pendingGoldEarned = b.totalGold;
        pendingGoldTotal  = vaultGold;

        BuildRewardLines();
        RefreshGoldLabels();
    }

    /// <summary>ยอดที่ยังไม่ไหล = เขียนค่าจริงลงไปเลย · ถ้า panel เปิดอยู่แล้วให้ไหลขึ้นแทน</summary>
    void RefreshGoldLabels()
    {
        if (pendingGoldEarned < 0) return;

        if (panelVisible && goldCountUpDuration > 0f && isActiveAndEnabled)
        {
            if (goldTween != null) StopCoroutine(goldTween);
            goldTween = StartCoroutine(CountUpGold(pendingGoldEarned, pendingGoldTotal));
        }
        else
        {
            WriteGoldLabels(pendingGoldEarned, pendingGoldTotal);
        }
    }

    void WriteGoldLabels(int earned, int vault)
    {
        if (goldEarnedLabel)     goldEarnedLabel.text     = $"+{earned:N0} G";
        if (goldTotalLabel)      goldTotalLabel.text      = $"รวม {vault:N0} G";
        if (goldTotalValueLabel) goldTotalValueLabel.text = $"{vault:N0} G";
    }

    /// <summary>
    /// ทองไหลขึ้นจาก 0 → ค่าจริง และคลังไหลตามจากยอดก่อนหน้า
    /// ใช้ <see cref="Time.unscaledDeltaTime"/> เพราะจอนี้ขึ้นตอนเกมหยุด (GamePause ตั้ง timeScale = 0)
    /// </summary>
    IEnumerator CountUpGold(int earned, int vaultAfter)
    {
        int vaultBefore = Mathf.Max(0, vaultAfter - earned);
        float t = 0f;

        while (t < goldCountUpDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / goldCountUpDuration);
            k = 1f - (1f - k) * (1f - k);   // ease-out quad — เร็วตอนต้น ค่อยหยุด ไม่ใช่วิ่งเรียบ

            WriteGoldLabels(Mathf.RoundToInt(earned * k),
                            Mathf.RoundToInt(Mathf.Lerp(vaultBefore, vaultAfter, k)));
            yield return null;
        }

        WriteGoldLabels(earned, vaultAfter);
        goldTween = null;
    }

    void BuildRewardLines()
    {
        if (rewardLinePrefab == null || rewardLinesParent == null || !hasBreakdown) return;

        for (int i = rewardLinesParent.childCount - 1; i >= 0; i--)
            Destroy(rewardLinesParent.GetChild(i).gameObject);

        var lines = pendingBreakdown.ToLines();
        for (int i = 0; i < lines.Count; i++)
        {
            var row = Instantiate(rewardLinePrefab, rewardLinesParent);
            // prefab/แม่แบบอาจถูกปิดไว้ในซีน — clone จะปิดตามแล้วแถวหายเงียบๆ
            row.gameObject.SetActive(true);
            row.Bind(lines[i].reason, lines[i].gold, lines[i].count);
            // บรรทัดสุดท้ายไม่ต้องมีเส้นคั่น — เส้นรวมด้านล่างทำหน้าที่นั้นอยู่แล้ว
            row.SetDividerVisible(i < lines.Count - 1);
        }
    }

    // ── Party ─────────────────────────────────────────────────────────────
    List<PartyMemberInfo> partyOverride;

    /// <summary>
    /// ป้อนรายชื่อปาร์ตี้จากภายนอก — ถ้าไม่เรียก จอจะไปอ่านเองจาก
    /// PlayerSlotRegistry + player object ที่ spawn อยู่ (<see cref="ResultPartyRowUI.CollectFromNetwork"/>)
    /// </summary>
    public void SetParty(List<PartyMemberInfo> members)
    {
        partyOverride = members;
        BuildPartyRows();
    }

    void BuildPartyRows()
    {
        if (partyRowPrefab == null || partyRowsParent == null) return;

        var members = partyOverride ?? ResultPartyRowUI.CollectFromNetwork();

        for (int i = partyRowsParent.childCount - 1; i >= 0; i--)
            Destroy(partyRowsParent.GetChild(i).gameObject);

        foreach (var m in members)
            Instantiate(partyRowPrefab, partyRowsParent).Bind(m);
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
        if (subtitleLabel)  subtitleLabel.text  = isWin ? winSubtitle : loseSubtitle;
        if (diagonalPanel)  diagonalPanel.color = isWin ? winDiagonalColor : loseDiagonalColor;

        // ฟิลด์เดิม (ป้าย+ค่ารวมกัน) กับฟิลด์ใหม่ (ค่าล้วน) เขียนทั้งคู่
        // ซีนไหนต่อแบบไหนไว้ก็ได้ค่าถูกต้องเหมือนกัน
        if (timeLabel)  timeLabel.text  = $"Time    {time}";
        if (levelLabel) levelLabel.text = $"Level   {level}";
        if (waveLabel)  waveLabel.text  = $"Wave    {wave}";

        if (timeValueLabel)  timeValueLabel.text  = time;
        if (levelValueLabel) levelValueLabel.text = level.ToString();
        if (waveValueLabel)  waveValueLabel.text  = wave.ToString();

        BuildPartyRows();
        BuildRewardLines();

        // รางวัลอาจมาถึงก่อน panel แสดง — เขียนค่าที่ค้างไว้ตรงนี้
        if (goldEarnedLabel && pendingGoldEarned < 0) goldEarnedLabel.text = "...";

        // Fade in
        if (panelRoot) panelRoot.SetActive(true);
        panelVisible = true;

        // ทองต้องเริ่มไหลหลัง panel เปิดแล้ว ไม่งั้นผู้เล่นพลาดอนิเมชันไปทั้งท่อน
        RefreshGoldLabels();

        ApplyHostOnlyState();

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

    /// <summary>
    /// เฉพาะ host กด "เล่นอีกครั้ง" ได้ — client ต้องเห็นว่าปุ่มกดไม่ได้ **ด้วยตา**
    /// ไม่ใช่แค่กดแล้วไม่มีอะไรเกิดขึ้น (interactable อย่างเดียวยังดูเหมือนปุ่มปกติในธีมนี้
    /// เพราะปุ่มเป็น Image สีทึบไม่มี transition สี)
    /// </summary>
    void ApplyHostOnlyState()
    {
        bool isHost = GameSessionManager.Instance?.IsHost ?? true;

        if (playAgainButton != null)
        {
            playAgainButton.interactable = isHost;

            var g = playAgainGroup;
            if (g == null) g = playAgainButton.GetComponent<CanvasGroup>();
            if (g == null) g = playAgainButton.gameObject.AddComponent<CanvasGroup>();

            g.alpha          = isHost ? 1f : disabledButtonAlpha;
            g.interactable   = isHost;
            g.blocksRaycasts = isHost;
        }

        if (waitingForHostLabel) waitingForHostLabel.SetActive(!isHost);
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
            if (returnButton) returnButton.interactable = true;
            ApplyHostOnlyState();
        }
    }

    // ── Return to Menu ────────────────────────────────────────────────────
    async void ReturnToMenu()
    {
        IsShowing = false;
        panelVisible = false;
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
