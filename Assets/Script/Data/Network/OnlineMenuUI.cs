using System;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI panel สำหรับ Online mode
/// ใช้ SessionSettings.ToSessionOptions() เพื่อตั้งค่า Relay อัตโนมัติ
/// GameSessionManager (SessionObserver) จะรับ event ต่อจากนี้
/// </summary>

// ใน OnlineMenuUI.cs หรือที่ตั้ง Host/Server button


public class OnlineMenuUI : MonoBehaviour
{
    [Header("SessionSettings (ลาก ScriptableObject มาใส่)")]
    [SerializeField] SessionSettings sessionSettings;

    [Header("Host")]
    public Button          createSessionButton;
    public TextMeshProUGUI sessionCodeLabel;   // แสดง code หลัง create
    public Button          copyCodeButton;     // copy code ไปยัง clipboard

    [Header("Join")]
    public TMP_InputField  joinCodeInput;
    public Button          joinSessionButton;

    [Header("Shared")]
    public TextMeshProUGUI statusText;
    public Button          leaveButton;

    [Header("Lobby (หลัง create สำเร็จ)")]
    public Button          startGameButton;    // Host only
    public TextMeshProUGUI playerCountLabel;   // "Players: 1 / 4"
    public TMP_Dropdown    sceneDropdown;      // Host เลือก scene ที่จะโหลด

    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL host ไม่ได้ (Unity Transport ไม่รองรับ server บน browser)
        // ซ่อนปุ่ม Create Session — เหลือแค่ Join
        if (createSessionButton != null) createSessionButton.gameObject.SetActive(false);
        SetStatus("WebGL: Join as Client only (ใส่ Room Code แล้วกด Join)");
#endif
    }
    // ── Lifecycle ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        createSessionButton.onClick.AddListener(OnCreateClicked);
        joinSessionButton.onClick.AddListener(OnJoinClicked);
        if (leaveButton)      leaveButton.onClick.AddListener(OnLeaveClicked);
        if (copyCodeButton)   copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        if (startGameButton)  startGameButton.onClick.AddListener(OnStartGameClicked);

        sessionCodeLabel?.gameObject.SetActive(false);
        copyCodeButton?.gameObject.SetActive(false);
        startGameButton?.gameObject.SetActive(false);
        playerCountLabel?.gameObject.SetActive(false);
        sceneDropdown?.gameObject.SetActive(false);
        leaveButton?.gameObject.SetActive(false);
        SetButtons(false);

        // รอ MultiplayerService พร้อม (UnityServicesWithName prefab init ให้)
        WaitForServicesAsync();
    }

    void OnDisable()
    {
        createSessionButton.onClick.RemoveListener(OnCreateClicked);
        joinSessionButton.onClick.RemoveListener(OnJoinClicked);
        if (leaveButton)     leaveButton.onClick.RemoveListener(OnLeaveClicked);
        if (copyCodeButton)  copyCodeButton.onClick.RemoveListener(OnCopyCodeClicked);
        if (startGameButton) startGameButton.onClick.RemoveListener(OnStartGameClicked);

        GameSessionManager.OnPlayerJoined -= OnPlayerJoined;
        GameSessionManager.OnPlayerLeft   -= OnPlayerLeft;
    }

    // ── รอ Service พร้อม ─────────────────────────────────────────────────
    async void WaitForServicesAsync()
    {
        SetStatus("กำลังเชื่อมต่อ Services…");

        float elapsed = 0f;
        while (MultiplayerService.Instance == null && elapsed < 15f)
        {
            await Task.Delay(200);
            elapsed += 0.2f;
        }

        if (MultiplayerService.Instance == null)
        {
            SetStatus("⚠️ Services ไม่พร้อม — ตรวจสอบ UnityServicesWithName prefab");
            return;
        }

        SetStatus("พร้อมแล้ว ✓");
        SetButtons(true);
    }

    // ── Create Session (Host) ─────────────────────────────────────────────
    async void OnCreateClicked()
    {
        SetButtons(false);
        SetStatus("กำลังสร้าง Session…");

        try
        {
            // ใช้ SessionSettings.ToSessionOptions() — Relay + PlayerName ครบ
            SessionOptions options = sessionSettings != null
                ? sessionSettings.ToSessionOptions()
                : new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();

            ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // แสดง Room Code
            if (sessionCodeLabel != null)
            {
                sessionCodeLabel.text = $"Room Code:  {session.Code}";
                sessionCodeLabel.gameObject.SetActive(true);
            }

            SetStatus("สร้างสำเร็จ! รอผู้เล่นอื่นเข้าร่วม…");
            copyCodeButton?.gameObject.SetActive(true);
            leaveButton?.gameObject.SetActive(true);

            // แสดงปุ่ม Start Game + Scene Dropdown (Host only)
            if (startGameButton != null) startGameButton.gameObject.SetActive(true);
            ShowSceneDropdown();

            // แสดง player count และ subscribe update
            if (playerCountLabel != null) playerCountLabel.gameObject.SetActive(true);
            UpdatePlayerCount();
            GameSessionManager.OnPlayerJoined += OnPlayerJoined;
            GameSessionManager.OnPlayerLeft   += OnPlayerLeft;
        }
        catch (Exception e)
        {
            SetStatus($"❌ สร้างไม่สำเร็จ: {e.Message}");
            SetButtons(true);
        }
    }

    // ── Join Session (Client) ─────────────────────────────────────────────
    async void OnJoinClicked()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) { SetStatus("กรุณาใส่ Room Code"); return; }

        SetButtons(false);
        SetStatus("กำลังเข้าร่วม…");

        try
        {
            JoinSessionOptions options = sessionSettings != null
                ? sessionSettings.ToJoinSessionOptions()
                : new JoinSessionOptions();

            ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);

            SetStatus($"เข้าร่วมสำเร็จ! กำลังโหลดเกม…");
            leaveButton?.gameObject.SetActive(true);
            // GameSessionManager จะรับ OnSessionJoined — Client รอ Host โหลด scene
        }
        catch (Exception e)
        {
            SetStatus($"❌ Code ไม่ถูกต้อง: {e.Message}");
            SetButtons(true);
        }
    }

    // ── Leave ─────────────────────────────────────────────────────────────
    async void OnLeaveClicked()
    {
        leaveButton.interactable = false;
        SetStatus("กำลังออกจาก Session…");

        try
        {
            var session = GameSessionManager.Instance?.CurrentSession;
            if (session != null) await session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OnlineMenuUI] Leave: {e.Message}");
        }

        sessionCodeLabel?.gameObject.SetActive(false);
        copyCodeButton?.gameObject.SetActive(false);
        startGameButton?.gameObject.SetActive(false);
        playerCountLabel?.gameObject.SetActive(false);
        sceneDropdown?.gameObject.SetActive(false);
        leaveButton?.gameObject.SetActive(false);
        SetButtons(true);
        SetStatus("ออกแล้ว");
    }

    // ── Scene Dropdown ────────────────────────────────────────────────────
    void ShowSceneDropdown()
    {
        if (sceneDropdown == null || GameSessionManager.Instance == null) return;

        string[] scenes = GameSessionManager.Instance.availableScenes;
        if (scenes == null || scenes.Length == 0) return;

        sceneDropdown.ClearOptions();
        sceneDropdown.AddOptions(new System.Collections.Generic.List<string>(scenes));
        sceneDropdown.gameObject.SetActive(true);
    }

    // ── Start Game (Host only) ────────────────────────────────────────────
    void OnStartGameClicked()
    {
        startGameButton.interactable = false;
        SetStatus("กำลังเริ่มเกม…");

        // อ่าน scene ที่เลือกจาก dropdown (ถ้ามี) หรือใช้ default
        string selected = null;
        if (sceneDropdown != null && sceneDropdown.gameObject.activeSelf)
        {
            var scenes = GameSessionManager.Instance?.availableScenes;
            int idx = sceneDropdown.value;
            if (scenes != null && idx < scenes.Length)
                selected = scenes[idx];
        }

        GameSessionManager.Instance?.StartGame(selected);
    }

    // ── Player Count ──────────────────────────────────────────────────────
    void OnPlayerJoined(string _) => UpdatePlayerCount();
    void OnPlayerLeft(string _)   => UpdatePlayerCount();

    void UpdatePlayerCount()
    {
        if (playerCountLabel == null || GameSessionManager.Instance == null) return;
        int cur = GameSessionManager.Instance.PlayerCount;
        int max = GameSessionManager.Instance.MaxPlayers;
        playerCountLabel.text = $"Players: {cur} / {max}";
    }

    // ── Copy Code ─────────────────────────────────────────────────────────
    void OnCopyCodeClicked()
    {
        string code = GameSessionManager.Instance?.SessionCode;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;
        SetStatus($"คัดลอก Code แล้ว: {code}");

        // Flash ข้อความปุ่มชั่วคราว
        StartCoroutine(FlashCopyButton());
    }

    System.Collections.IEnumerator FlashCopyButton()
    {
        var label = copyCodeButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) yield break;

        string original = label.text;
        label.text = "✅ คัดลอกแล้ว!";
        yield return new WaitForSeconds(1.5f);
        label.text = original;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void SetButtons(bool on)
    {
        createSessionButton.interactable = on;
        joinSessionButton.interactable   = on;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
    }
}
