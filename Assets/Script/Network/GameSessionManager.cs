using System;
using Unity.Netcode;
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

    [Header("Session Type (ต้องตรงกับ SessionSettings)")]
    [SerializeField] private string sessionType = "default-session";

    [Header("Scenes")]
    [Tooltip("รายชื่อ Scene ที่ Host สามารถเลือกได้ (ต้องอยู่ใน Build Settings)")]
    public string[] availableScenes = { "SampleScene" };

    // ── Static Events สำหรับ game code subscribe ─────────────────────────
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
        currentSession = session;

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

    // ── Public API ────────────────────────────────────────────────────────
    public ISession CurrentSession  => currentSession;
    public bool     IsHost          => currentSession?.IsHost ?? false;
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
