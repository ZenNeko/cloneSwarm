using System;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Services.Authentication;
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

    // ── Internal state ────────────────────────────────────────────────────
    // กัน auto-create ซ้อน + กันสร้าง session อีกครั้งระหว่างที่ panel re-enabled
    bool isCreatingSession;

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

        // Subscribe OnSessionLeft เพื่อ reset UI อัตโนมัติเมื่อ session หาย
        // (host dropped, network lost, หรือ pending leave จาก back button)
        GameSessionManager.OnSessionLeft -= OnSessionLeftExternal;
        GameSessionManager.OnSessionLeft += OnSessionLeftExternal;

        // Reset UI ก่อน — ถ้ามี existing session WaitForServicesAsync() จะ restore กลับเอง
        sessionCodeLabel?.gameObject.SetActive(false);
        copyCodeButton?.gameObject.SetActive(false);
        startGameButton?.gameObject.SetActive(false);
        playerCountLabel?.gameObject.SetActive(false);
        sceneDropdown?.gameObject.SetActive(false);
        leaveButton?.gameObject.SetActive(false);

        // Default: โชว์ Create/Join — ถ้ามี session แล้ว WaitForServices/RestoreFromExistingSession
        // จะซ่อนให้อีกที (กันค้างซ่อนจาก previous panel cycle)
        SetCreateVisible(true);
        SetButtons(false);

        // รอ MultiplayerService พร้อม (UnityServicesWithName prefab init ให้)
        // จากนั้น: ถ้ามี session อยู่แล้ว → restore UI / ไม่มี → auto-create
        WaitForServicesAsync();
    }

    void OnDisable()
    {
        createSessionButton.onClick.RemoveListener(OnCreateClicked);
        joinSessionButton.onClick.RemoveListener(OnJoinClicked);
        if (leaveButton)     leaveButton.onClick.RemoveListener(OnLeaveClicked);
        if (copyCodeButton)  copyCodeButton.onClick.RemoveListener(OnCopyCodeClicked);
        if (startGameButton) startGameButton.onClick.RemoveListener(OnStartGameClicked);

        GameSessionManager.OnPlayerJoined  -= OnPlayerJoined;
        GameSessionManager.OnPlayerLeft    -= OnPlayerLeft;
        GameSessionManager.OnSessionJoined -= OnSessionJoinedRefresh;
        GameSessionManager.OnSessionLeft   -= OnSessionLeftExternal;
    }

    /// <summary>เมื่อ SessionObserver fire OnSessionJoined → refresh player count
    /// (กรณีที่ตอนแรก CreateSession เสร็จแต่ observer ยังไม่ทันใส่ session ลง singleton)</summary>
    void OnSessionJoinedRefresh(ISession _) => UpdatePlayerCount();

    /// <summary>fired by GameSessionManager เมื่อ session หายระหว่างที่ panel เปิดอยู่
    /// (เช่น Host หาย, network drop, หรือ Leave async เพิ่งจบ) → reset UI กลับเป็น "no session"</summary>
    void OnSessionLeftExternal()
    {
        if (!isActiveAndEnabled) return;
        ResetSessionUI();
        SetStatus("Session หายแล้ว");
    }

    // ── รอ Service พร้อม + restore (ไม่ auto-create ตรงนี้) ──────────────
    /// <summary>
    /// เรียกใน OnEnable → รอ services พร้อม
    /// — ถ้ามี session อยู่แล้ว (กลับมาจากที่อื่น) → restore UI
    /// — ถ้าไม่มี → enable manual buttons, รอ MenuManager เรียก
    ///   BeginAutoCreateAfterCharSelectAsync() หลัง user confirm character
    /// </summary>
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

        // กลับเข้ามาในขณะที่ session ยังอยู่ → restore UI ไม่ต้องสร้างใหม่
        var existing = GameSessionManager.Instance?.CurrentSession;
        if (existing != null)
        {
            RestoreFromExistingSession(existing);
            return;
        }

        // ไม่ auto-create ที่นี่ — MenuManager จะเรียก BeginAutoCreateAfterCharSelectAsync()
        // หลัง user confirm character เสร็จ (กัน "Player is not authorized" ตอน auth ยังไม่ sign-in)
        SetStatus("พร้อมแล้ว ✓");
        SetButtons(true);
    }

    /// <summary>
    /// Public — MenuManager เรียกหลัง user confirm character (Online mode)
    /// → รอ AuthenticationService.IsSignedIn พร้อม → auto-create session
    ///
    /// เหตุผลที่แยกจาก WaitForServicesAsync:
    ///   • MultiplayerService.Instance ready ≠ Auth signed-in
    ///   • Auth sign-in อาจ delay หลัง services ready ทำให้ CreateSession เจอ "Player is not authorized"
    ///   • Trigger หลัง char confirm สอดคล้องกับ design (user ยืนยันแล้วค่อยจอง slot ใน lobby)
    /// </summary>
    public async Task BeginAutoCreateAfterCharSelectAsync()
    {
        // ถ้ามี session อยู่แล้ว (อาจ restored จาก previous visit) → ไม่สร้างซ้อน
        if (GameSessionManager.Instance?.CurrentSession != null) return;
        if (isCreatingSession) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: ห้าม host → skip
        return;
#endif

        // รอ MultiplayerService ready (เผื่อ OnEnable ยังรอ services อยู่)
        SetStatus("กำลังเชื่อมต่อ Services…");
        float elapsed = 0f;
        while (MultiplayerService.Instance == null && elapsed < 15f)
        {
            await Task.Delay(200);
            elapsed += 0.2f;
        }
        if (MultiplayerService.Instance == null)
        {
            SetStatus("⚠️ Services ไม่พร้อม");
            return;
        }

        // รอ Authentication sign-in สำเร็จ — สาเหตุของ error "Player is not authorized"
        SetStatus("กำลังเข้าสู่ระบบ…");
        elapsed = 0f;
        while (!AuthenticationService.Instance.IsSignedIn && elapsed < 15f)
        {
            await Task.Delay(200);
            elapsed += 0.2f;
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetStatus("⚠️ Sign-in ไม่สำเร็จ — ลองกด Create เอง");
            SetButtons(true);   // เปิดให้ user retry manual
            return;
        }

        // Panel ปิดไปก่อน auth พร้อม → ยกเลิก (เช่น user กด Back กลับไปแล้ว)
        if (this == null || !isActiveAndEnabled) return;

        await CreateSessionInternalAsync(autoMode: true);
    }

    /// <summary>เมื่อเข้ามาแล้วพบว่ามี session อยู่แล้ว (กลับมาจากหน้า Char Select)
    /// → restore UI โดยไม่สร้าง session ใหม่</summary>
    void RestoreFromExistingSession(ISession session)
    {
        SetStatus($"✓ Session active — Room: {session.Code}");

        if (sessionCodeLabel != null)
        {
            sessionCodeLabel.text = $"Room Code:  {session.Code}";
            sessionCodeLabel.gameObject.SetActive(true);
        }
        else Debug.LogWarning("[OnlineMenuUI] sessionCodeLabel ไม่ได้ assign — Room Code จะไม่แสดง");

        copyCodeButton?.gameObject.SetActive(true);

        if (leaveButton)
        {
            leaveButton.gameObject.SetActive(true);
            leaveButton.interactable = true;
        }

        if (session.IsHost)
        {
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = true;
            }
            ShowSceneDropdown();
        }

        if (playerCountLabel != null) playerCountLabel.gameObject.SetActive(true);
        UpdatePlayerCount(session);   // ส่ง local ref กัน timing issue

        // ซ่อน Create — Join ยัง interactable เพื่อให้ swap ไป session อื่นได้
        SetInSessionButtons();

        // กัน double-subscribe เวลา OnEnable วน
        GameSessionManager.OnPlayerJoined  -= OnPlayerJoined;
        GameSessionManager.OnPlayerLeft    -= OnPlayerLeft;
        GameSessionManager.OnSessionJoined -= OnSessionJoinedRefresh;
        GameSessionManager.OnPlayerJoined  += OnPlayerJoined;
        GameSessionManager.OnPlayerLeft    += OnPlayerLeft;
        GameSessionManager.OnSessionJoined += OnSessionJoinedRefresh;
    }

    // ── Create Session (Host) — manual button หรือ auto ────────────────────
    async void OnCreateClicked()
    {
        // กันกดซ้ำขณะกำลังสร้าง หรือมี session อยู่แล้ว
        if (isCreatingSession) return;
        if (GameSessionManager.Instance?.CurrentSession != null)
        {
            SetStatus("Session อยู่แล้ว — กด Leave ก่อนสร้างใหม่");
            return;
        }

        await CreateSessionInternalAsync(autoMode: false);
    }

    /// <summary>Logic หลักของการสร้าง session — ใช้ทั้ง auto + manual</summary>
    async Task CreateSessionInternalAsync(bool autoMode)
    {
        isCreatingSession = true;
        SetButtons(false);
        SetStatus(autoMode ? "กำลังสร้าง Session อัตโนมัติ…" : "กำลังสร้าง Session…");

        try
        {
            // ใช้ SessionSettings.ToSessionOptions() — Relay + PlayerName ครบ
            SessionOptions options = sessionSettings != null
                ? sessionSettings.ToSessionOptions()
                : new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();

            ISession session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Edge case: user กด Back ระหว่างที่ create อยู่ → panel ปิดไปแล้ว
            // ต้อง leave session ทันที กัน orphan
            if (this == null || !isActiveAndEnabled)
            {
                try { await session.LeaveAsync(); }
                catch (Exception leaveEx) { Debug.LogWarning($"[OnlineMenuUI] orphan leave failed: {leaveEx.Message}"); }
                return;
            }

            // แสดง Room Code
            if (sessionCodeLabel != null)
            {
                sessionCodeLabel.text = $"Room Code:  {session.Code}";
                sessionCodeLabel.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("[OnlineMenuUI] sessionCodeLabel ไม่ได้ assign ใน Inspector " +
                                 "— Room Code จะไม่แสดง (Code: " + session.Code + ")");
            }

            SetStatus("สร้างสำเร็จ! รอผู้เล่นอื่นเข้าร่วม…");
            copyCodeButton?.gameObject.SetActive(true);

            if (leaveButton)
            {
                leaveButton.gameObject.SetActive(true);
                leaveButton.interactable = true;
            }

            // แสดงปุ่ม Start Game + Scene Dropdown (Host only)
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.interactable = true;
            }
            ShowSceneDropdown();

            // แสดง player count — ใช้ local `session` ref เพราะ GameSessionManager.CurrentSession
            // อาจยังเป็น null (SessionObserver event ยังไม่ได้ trigger ตอนนี้)
            if (playerCountLabel != null) playerCountLabel.gameObject.SetActive(true);
            UpdatePlayerCount(session);

            // Subscribe events — dedupe กัน double-fire เมื่อ OnEnable วนซ้ำ
            GameSessionManager.OnPlayerJoined  -= OnPlayerJoined;
            GameSessionManager.OnPlayerLeft    -= OnPlayerLeft;
            GameSessionManager.OnSessionJoined -= OnSessionJoinedRefresh;
            GameSessionManager.OnPlayerJoined  += OnPlayerJoined;
            GameSessionManager.OnPlayerLeft    += OnPlayerLeft;
            GameSessionManager.OnSessionJoined += OnSessionJoinedRefresh;

            // ซ่อน Create — Join ยังกดได้เผื่อ user กระโดดไป session อื่น
            SetInSessionButtons();
        }
        catch (Exception e)
        {
            SetStatus($"❌ สร้างไม่สำเร็จ: {e.Message}");
            SetButtons(true);   // เปิด Create/Join ให้ retry ได้
            if (leaveButton)
            {
                // Edge case: ถ้าผู้ใช้ค้างหน้า + create fail → ยังต้องมีปุ่ม leave ใช้ออกได้
                leaveButton.gameObject.SetActive(true);
                leaveButton.interactable = true;
            }
        }
        finally
        {
            isCreatingSession = false;
        }
    }

    // ── Join Session (Client) ─────────────────────────────────────────────
    async void OnJoinClicked()
    {
        // Guard: กัน double-click ระหว่าง pending create
        if (isCreatingSession) return;

        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) { SetStatus("กรุณาใส่ Room Code"); return; }

        SetButtons(false);

        // ถ้ามี session ปัจจุบัน (auto-created หลังเลือก char) → leave ก่อนเข้า session ของเพื่อน
        // เพราะ Unity Multiplayer Service ไม่ยอมให้อยู่ 2 session พร้อมกัน
        if (GameSessionManager.Instance?.CurrentSession != null)
        {
            SetStatus("กำลังออกจาก session เดิม…");
            await LeaveSessionIfActiveAsync();
            // รอ event propagate (SessionObserver fire OnSessionLeft → reset UI)
            await Task.Delay(200);

            // Panel ถูกปิดระหว่างนี้ → ยกเลิก
            if (!isActiveAndEnabled) return;
        }

        SetStatus("กำลังเข้าร่วม…");
        SetButtons(false);   // ResetSessionUI() อาจถูกเรียกระหว่าง leave → re-disable

        try
        {
            JoinSessionOptions options = sessionSettings != null
                ? sessionSettings.ToJoinSessionOptions()
                : new JoinSessionOptions();

            ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);

            SetStatus($"เข้าร่วมสำเร็จ! กำลังโหลดเกม…");
            if (leaveButton != null)
            {
                leaveButton.gameObject.SetActive(true);
                leaveButton.interactable = true;
            }

            // แสดง Room Code + player count ของ session ที่ join เข้าไป
            if (sessionCodeLabel != null)
            {
                sessionCodeLabel.text = $"Room Code:  {session.Code}";
                sessionCodeLabel.gameObject.SetActive(true);
            }
            copyCodeButton?.gameObject.SetActive(true);
            if (playerCountLabel != null) playerCountLabel.gameObject.SetActive(true);
            UpdatePlayerCount(session);

            // ซ่อน Create — Join ยัง interactable (user อาจอยาก swap session อีก)
            SetInSessionButtons();

            // Subscribe player count update (dedupe)
            GameSessionManager.OnPlayerJoined  -= OnPlayerJoined;
            GameSessionManager.OnPlayerLeft    -= OnPlayerLeft;
            GameSessionManager.OnSessionJoined -= OnSessionJoinedRefresh;
            GameSessionManager.OnPlayerJoined  += OnPlayerJoined;
            GameSessionManager.OnPlayerLeft    += OnPlayerLeft;
            GameSessionManager.OnSessionJoined += OnSessionJoinedRefresh;
            // GameSessionManager จะรับ OnSessionJoined — Client รอ Host โหลด scene
        }
        catch (Exception e)
        {
            SetStatus($"❌ Join ไม่สำเร็จ: {e.Message}");
            SetButtons(true);   // เปิดให้ retry ใส่ code ใหม่
            // หมายเหตุ: user อาจไม่มี session ตอนนี้ (เพราะ leave ไปก่อนหน้า)
            // → Create button มองเห็นได้จาก ResetSessionUI flow → ใช้สร้างใหม่ได้
        }
    }

    // ── Leave ─────────────────────────────────────────────────────────────
    /// <summary>Manual Leave button — เคลียร์ session แต่ค้างหน้านี้ไว้</summary>
    async void OnLeaveClicked()
    {
        if (leaveButton) leaveButton.interactable = false;
        await LeaveSessionIfActiveAsync();
        ResetSessionUI();
        SetStatus("ออกจาก Session แล้ว");
    }

    /// <summary>
    /// Public — MenuManager เรียกตอนกด Back button เพื่อ leave ก่อน navigate ออก
    /// safe to call แม้ไม่มี session — ไม่ throw
    /// </summary>
    public async Task LeaveSessionIfActiveAsync()
    {
        var session = GameSessionManager.Instance?.CurrentSession;
        if (session == null) return;

        try
        {
            SetStatus("กำลังออกจาก Session…");
            await session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OnlineMenuUI] Leave: {e.Message}");
        }
    }

    /// <summary>Reset UI elements กลับสู่สถานะ "ไม่มี session"</summary>
    void ResetSessionUI()
    {
        sessionCodeLabel?.gameObject.SetActive(false);
        copyCodeButton?.gameObject.SetActive(false);
        startGameButton?.gameObject.SetActive(false);
        playerCountLabel?.gameObject.SetActive(false);
        sceneDropdown?.gameObject.SetActive(false);
        if (leaveButton) leaveButton.gameObject.SetActive(false);

        // โชว์ Create/Join group กลับ (สถานะ "พร้อมสร้าง/เข้าร่วม session ใหม่")
        SetCreateVisible(true);
        SetButtons(true);
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

    /// <summary>
    /// Refresh "Players: N/M" — รับ session override สำหรับกรณีที่
    /// GameSessionManager.Instance.CurrentSession ยังไม่ถูก set (observer delay)
    /// </summary>
    void UpdatePlayerCount(ISession sessionOverride = null)
    {
        if (playerCountLabel == null) return;

        var s = sessionOverride ?? GameSessionManager.Instance?.CurrentSession;
        if (s == null)
        {
            playerCountLabel.text = "Players: – / –";
            return;
        }
        playerCountLabel.text = $"Players: {s.Players.Count} / {s.MaxPlayers}";
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
    /// <summary>เปิด/ปิด interactable ของ Create+Join (ใช้ขณะ pending operation)</summary>
    void SetButtons(bool on)
    {
        createSessionButton.interactable = on;
        joinSessionButton.interactable   = on;
    }

    /// <summary>
    /// แสดง/ซ่อนปุ่ม Create — Join button + input field คงอยู่เสมอ (UI consistency)
    /// — Defensive guard ใน OnJoinClicked กันคลิกผิดเมื่อมี session อยู่แล้ว
    /// </summary>
    void SetCreateVisible(bool show)
    {
        if (createSessionButton != null) createSessionButton.gameObject.SetActive(show);

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: host ไม่ได้ — Create ซ่อนเสมอ
        if (createSessionButton != null) createSessionButton.gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// State "อยู่ใน session แล้ว" — ซ่อน Create, แต่ปุ่ม Join ยังกดได้ (เพื่อ swap session)
    /// แทนที่ SetButtons(false) ในกรณีที่เข้า session สำเร็จ
    /// </summary>
    void SetInSessionButtons()
    {
        SetCreateVisible(false);
        // Create disable (กันกดซ้ำ) แต่ Join ยัง interactable
        if (createSessionButton != null) createSessionButton.interactable = false;
        if (joinSessionButton   != null) joinSessionButton.interactable   = true;
    }

    void SetStatus(string msg)
    {
        if (statusText) statusText.text = msg;
    }
}
