using System;
using Blocks.Sessions.Common;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridge ระหว่าง Building Block Session และ NetworkManager
///
/// Building Block จัดการ:  Auth → Relay → สร้าง/เข้าร่วม Session → StartHost/StartClient
/// GameSessionManager จัดการ: รับ event จาก Session → แจ้ง game code
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("Session Settings (ลากจาก Assets/Blocks/MultiplayerSession/Settings)")]
    [SerializeField] private SessionSettings sessionSettings;

    [Header("Session Type (ต้องตรงกับ SessionSettings)")]
    [SerializeField] private string sessionType = "default-session";

    [Header("Scenes")]
    [Tooltip("รายชื่อ Scene ที่ Host สามารถเลือกได้ (ต้องอยู่ใน Build Settings)")]
    public string[] availableScenes = { "SampleScene" };

    // ── Static Events สำหรับ game code subscribe ─────────────────────────
    /// <summary>ข้อความสถานะ — UI subscribe แทนที่จะให้ manager ไปยุ่งกับ Text</summary>
    public static event Action<string> OnStatus;
    /// <summary>เมื่อเข้า session สำเร็จ (ทั้ง Host และ Client)</summary>
    public static event Action<ISession> OnSessionJoined;
    /// <summary>เมื่อออกจาก session หรือ session ถูกลบ</summary>
    public static event Action OnSessionLeft;
    /// <summary>เมื่อมีผู้เล่นเข้ามาใหม่</summary>
    public static event Action<string> OnPlayerJoined;
    /// <summary>เมื่อผู้เล่นออก</summary>
    public static event Action<string> OnPlayerLeft;

    // ── Internal State ────────────────────────────────────────────────────
    private SessionObserver sessionObserver;
    private ISession        currentSession;
    private bool            isBusy;    // กันกดซ้ำระหว่างสร้าง/เข้า/ออก

    public bool IsBusy => isBusy;

    private void Status(string msg) => OnStatus?.Invoke(msg);

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        sessionObserver = new SessionObserver(sessionType);
        sessionObserver.SessionAdded        += OnSessionAdded;
        sessionObserver.AddingSessionFailed += OnAddingFailed;

        // ถ้า session มีอยู่แล้วก่อน Start (เช่น reload scene)
        if (sessionObserver.Session != null)
            OnSessionAdded(sessionObserver.Session);
    }

    void OnDestroy()
    {
        UnsubscribeSession();

        if (sessionObserver != null)
        {
            sessionObserver.SessionAdded        -= OnSessionAdded;
            sessionObserver.AddingSessionFailed -= OnAddingFailed;
            sessionObserver.Dispose();
        }
    }

    // ── Session Observer Events ───────────────────────────────────────────
    void OnSessionAdded(ISession session)
    {
        // เรียกได้จากสองทาง: SessionObserver และจาก Create/Join ที่ลงทะเบียนเอง
        // guard กันผูก event ซ้ำถ้าทั้งสองทางยิงมาที่ session เดียวกัน
        if (session == null || currentSession == session)
        {
            Debug.Log($"[DBG-lobby7] OnSessionAdded ข้าม — null? {session == null} · ซ้ำ? {currentSession == session}");
            return;
        }

        currentSession = session;
        Debug.Log($"[DBG-lobby7] OnSessionAdded ลงทะเบียนแล้ว — code='{session.Code}' IsHost={session.IsHost}");

        session.Changed                 += OnSessionChanged;
        session.RemovedFromSession      += HandleLeftSession;
        session.Deleted                 += HandleLeftSession;
        session.PlayerJoined            += HandlePlayerJoined;
        session.PlayerHasLeft           += HandlePlayerLeft;

        Debug.Log($"[Session] เข้าร่วมสำเร็จ | Name: {session.Name} | Code: {session.Code} | IsHost: {session.IsHost} | Players: {session.Players.Count}/{session.MaxPlayers}");

        OnSessionJoined?.Invoke(session);
    }

    void OnSessionChanged()
    {
        if (currentSession == null) return;
        Debug.Log($"[Session] State → {currentSession.State} | Players: {currentSession.Players.Count}/{currentSession.MaxPlayers}");
    }

    void HandleLeftSession()
    {
        Debug.Log("[Session] ออกจาก session แล้ว");

        UnsubscribeSession();
        currentSession = null;

        // ปิด NetworkManager ด้วยถ้ายังเปิดอยู่
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        OnSessionLeft?.Invoke();
    }

    void HandlePlayerJoined(string playerId)
    {
        Debug.Log($"[Session] Player เข้า: {playerId} | รวม {currentSession?.Players.Count}/{currentSession?.MaxPlayers}");
        OnPlayerJoined?.Invoke(playerId);
    }

    void HandlePlayerLeft(string playerId)
    {
        Debug.Log($"[Session] Player ออก: {playerId}");
        OnPlayerLeft?.Invoke(playerId);
    }

    void OnAddingFailed(AddingSessionOptions options, SessionException ex)
    {
        Debug.LogError($"[Session] เข้า/สร้าง session ไม่สำเร็จ: {ex.Message}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    void UnsubscribeSession()
    {
        if (currentSession == null) return;
        currentSession.Changed            -= OnSessionChanged;
        currentSession.RemovedFromSession -= HandleLeftSession;
        currentSession.Deleted            -= HandleLeftSession;
        currentSession.PlayerJoined       -= HandlePlayerJoined;
        currentSession.PlayerHasLeft      -= HandlePlayerLeft;
    }

    // ── Session Flow API ──────────────────────────────────────────────────
    public async System.Threading.Tasks.Task<bool> CreateSessionAsync()
    {
        if (isBusy) return false;

#if UNITY_WEBGL && !UNITY_EDITOR
        Status("WebGL: Host ไม่ได้");
        return false;
#endif

        isBusy = true;
        Status("กำลังเชื่อมต่อ Services…");

        ISession created = null;
        try
        {
            float elapsed = 0f;
            while (MultiplayerService.Instance == null && elapsed < 15f)
            {
                await System.Threading.Tasks.Task.Delay(200);
                elapsed += 0.2f;
            }

            if (MultiplayerService.Instance == null)
            {
                Status("⚠️ Services ไม่พร้อม");
                return false;
            }

            Status("กำลังเข้าสู่ระบบ…");
            elapsed = 0f;
            while (!AuthenticationService.Instance.IsSignedIn && elapsed < 15f)
            {
                await System.Threading.Tasks.Task.Delay(200);
                elapsed += 0.2f;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Status("⚠️ Sign-in ไม่สำเร็จ");
                return false;
            }

            Status("กำลังสร้าง Session…");

            SessionOptions options = sessionSettings != null
                ? sessionSettings.ToSessionOptions()
                : new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();

            created = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[DBG-lobby7] MultiplayerService สร้างเสร็จ — code='{created?.Code}'");
        }
        catch (Exception e)
        {
            Status($"❌ สร้างไม่สำเร็จ: {e.Message}");
            return false;
        }
        finally
        {
            isBusy = false;
        }

        // ลงทะเบียน "นอก try" โดยตั้งใจ — ถ้า OnSessionAdded throw (เช่น session.Players ยังไม่พร้อม)
        // เราไม่ต้องการให้ catch ข้างบนรายงานว่าสร้างไม่สำเร็จ ทั้งที่ห้องเปิดไปแล้วจริง
        if (created == null) return false;
        OnSessionAdded(created);

        Status("สร้างห้องสำเร็จ");
        return true;
    }

    public async System.Threading.Tasks.Task<bool> JoinSessionAsync(string code)
    {
        if (isBusy) return false;

        if (string.IsNullOrWhiteSpace(code))
        {
            Status("กรุณาใส่ Room Code");
            return false;
        }

        isBusy = true;
        Status("กำลังเชื่อมต่อ Services…");

        ISession joined = null;
        try
        {
            float elapsed = 0f;
            while (MultiplayerService.Instance == null && elapsed < 15f)
            {
                await System.Threading.Tasks.Task.Delay(200);
                elapsed += 0.2f;
            }

            if (MultiplayerService.Instance == null)
            {
                Status("⚠️ Services ไม่พร้อม");
                return false;
            }

            Status("กำลังเข้าสู่ระบบ…");
            elapsed = 0f;
            while (!AuthenticationService.Instance.IsSignedIn && elapsed < 15f)
            {
                await System.Threading.Tasks.Task.Delay(200);
                elapsed += 0.2f;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Status("⚠️ Sign-in ไม่สำเร็จ");
                return false;
            }

            Status("กำลังเข้าร่วม…");

            JoinSessionOptions options = sessionSettings != null
                ? sessionSettings.ToJoinSessionOptions()
                : new JoinSessionOptions();

            joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);
            Debug.Log($"[DBG-lobby7] MultiplayerService เข้าห้องเสร็จ — code='{joined?.Code}'");
        }
        catch (Exception e)
        {
            Status($"❌ Join ไม่สำเร็จ: {e.Message}");
            return false;
        }
        finally
        {
            isBusy = false;
        }

        // ลงทะเบียนนอก try ด้วยเหตุผลเดียวกับ CreateSessionAsync
        if (joined == null) return false;
        OnSessionAdded(joined);

        Status("เข้าห้องสำเร็จ");
        return true;
    }

    public async System.Threading.Tasks.Task LeaveSessionIfActiveAsync()
    {
        if (currentSession == null) return;

        try
        {
            Status("กำลังออกจาก Session…");
            await currentSession.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameSessionManager] Leave: {e.Message}");
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    public ISession CurrentSession  => currentSession;

    /// เจ้าของรัน — NGO server มาก่อน เพราะ solo เป็น host ที่ไม่มี session
    public bool IsHost => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                          || (currentSession?.IsHost ?? false);
    public string   SessionCode     => currentSession?.Code ?? "";
    public int      PlayerCount     => currentSession?.Players.Count ?? 0;
    public int      MaxPlayers      => currentSession?.MaxPlayers ?? 0;

    /// <summary>Host กด Start Game → โหลด scene ให้ทุก client อัตโนมัติ</summary>
    /// <param name="sceneName">ชื่อ scene ที่จะโหลด — ถ้าไม่ส่งจะใช้ availableScenes[0]</param>
    public void StartGame(string sceneName = null)
    {
        if (!IsHost)
        {
            Debug.LogWarning("[Session] StartGame: ต้องเป็น Host เท่านั้น");
            return;
        }

        var nm = NetworkManager.Singleton;

        // NGO ยังไม่ start → ไม่มี SceneManager
        if (nm == null || !nm.IsListening)
        {
            Debug.LogError("[Session] StartGame: NetworkManager ยังไม่ได้ start — ตรวจสอบ SessionSettings → createNetworkSession = true");
            return;
        }

        string target = sceneName ?? (availableScenes.Length > 0 ? availableScenes[0] : "SampleScene");
        Debug.Log($"[Session] StartGame → โหลด '{target}'");
        nm.SceneManager.LoadScene(target, LoadSceneMode.Single);
    }
}
