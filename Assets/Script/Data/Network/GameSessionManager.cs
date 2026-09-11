using System;
using System.Collections.Generic;
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

    [Header("Player Spawn")]
    [Tooltip("ชื่อซีนเมนู/ล็อบบี้ — ซีนนี้จะไม่ spawn player เพราะยังเลือกตัวละครไม่เสร็จ")]
    public string menuSceneName = "MenuScene";

    [Tooltip("จุดเกิดของผู้เล่น เรียงตาม slot ของ PlayerSlotRegistry\nว่างไว้ = กระจายเป็นวงรอบตำแหน่งเดิมของ playerPrefab เอง")]
    public Transform[] spawnPoints;

    [Tooltip("รัศมีวงกระจาย ตอนไม่ได้ตั้ง spawnPoints (หน่วย Unity)")]
    public float spawnRingRadius = 2.5f;

    /// <summary>ตัวนับสำรอง ใช้เมื่อ PlayerSlotRegistry ยังไม่พร้อมตอน spawn</summary>
    private int _spawnFallbackIndex;

    /// <summary>จุดเกิดที่จองไว้ต่อ client — คิดครั้งเดียวแล้วใช้ซ้ำตอน respawn
    /// ไม่งั้น fallback index จะเดินหน้าทุกครั้งที่ถาม แล้วได้คนละจุด</summary>
    private readonly Dictionary<ulong, Vector3> _spawnPositions = new();

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

        // ต้องทำก่อน StartHost ครั้งแรก — เรียกที่นี่เพราะ Start ของซีนเมนูวิ่งก่อนกด PLAY เสมอ
        DisableAutoPlayerSpawn();

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted += OnServerStarted;
            nm.OnServerStopped += OnServerStopped;
        }
    }

    void OnDestroy()
    {
        UnsubscribeSession();

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnServerStarted -= OnServerStarted;
            nm.OnServerStopped -= OnServerStopped;
        }
        UnhookPlayerSpawnEvents();

        if (sessionObserver != null)
        {
            sessionObserver.SessionAdded        -= OnSessionAdded;
            sessionObserver.AddingSessionFailed -= OnAddingFailed;
            sessionObserver.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Player spawn — เลื่อนจาก "ตอน connect" มาเป็น "ตอนซีนเกมโหลดเสร็จ"
    //
    // NGO spawn PlayerPrefab ทันทีที่ host/client เชื่อมต่อ ซึ่งเกิดใน MenuScene ตอนกด PLAY
    // (MenuManager.OnPlayClicked → StartHost) คือ *ก่อน* ผู้เล่นเลือกตัวละคร
    // PlayerVisual.OnNetworkSpawn / PlayerWeaponManager.OnNetworkSpawn จึงอ่าน
    // CharacterSelectUI.SelectedCharacter ได้แต่ค่า default แล้วล็อกค้างไปตลอด —
    // player object เป็น dynamic NetworkObject ไม่ถูกทำลายตอนเปลี่ยนซีน `_charIndex`
    // เลยติดค่าเดิมข้ามไปถึงซีนเกม เลือกตัวละครทีหลังก็ไม่มีผล
    //
    // แก้ที่ต้นเหตุ: ปิด auto-spawn แล้ว spawn เองหลังซีนเกมโหลดเสร็จ ตอนนั้น
    // SelectedCharacter เป็นค่าจริงแล้ว ทั้งโมเดล อาวุธ และ base stats จึงถูกตั้งแต่แรก
    // ไม่ต้องมีทางรื้อ-แล้ว-ใส่ใหม่
    // ═══════════════════════════════════════════════════════════════════════
    private GameObject playerPrefab;      // cache ไว้ก่อนถอดออกจาก NetworkConfig
    private bool       spawnHooked;

    void DisableAutoPlayerSpawn()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.NetworkConfig == null)
        {
            Debug.LogError("[Session] ไม่พบ NetworkManager ตอน Start — ปิด auto-spawn player ไม่ได้");
            return;
        }

        if (nm.NetworkConfig.PlayerPrefab == null)
        {
            // ปิดไปแล้วรอบก่อน (NetworkManager เป็น DontDestroyOnLoad อยู่ข้ามซีน)
            // แต่ถ้า playerPrefab หลุดไปด้วยจะไม่มีอะไร spawn เลย — ดังกว่าเงียบ
            if (playerPrefab == null)
                Debug.LogError("[Session] NetworkConfig.PlayerPrefab ว่างและไม่มี cache — " +
                               "จะไม่มี player ถูก spawn เลย · ตรวจ NetworkManager prefab");
            return;
        }

        playerPrefab = nm.NetworkConfig.PlayerPrefab;
        nm.NetworkConfig.PlayerPrefab = null;
        Debug.Log($"[Session] ปิด NGO auto-spawn player แล้ว — จะ spawn '{playerPrefab.name}' เองหลังซีนเกมโหลดเสร็จ");
    }

    void OnServerStarted()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null || spawnHooked) return;

        spawnHooked = true;
        nm.SceneManager.OnLoadEventCompleted  += OnSceneLoadEventCompleted;
        nm.SceneManager.OnSynchronizeComplete += OnClientSynchronizeComplete;

        // ครอบเส้นทาง "เปิดซีนเกมตรงๆ แล้ว StartHost" ซึ่งสองอีเวนต์ข้างบนไม่ยิงเลย
        // เพราะซีนเปิดอยู่แล้ว ไม่ได้ถูก NGO โหลด — เกิดกับ WeaponTestScene ที่มี AutoHost
        // และกับใครก็ตามที่กด Play ในซีนเกมโดยไม่ผ่านเมนู
        nm.OnClientConnectedCallback += OnClientConnectedInGameplayScene;
    }

    /// <summary>
    /// client เชื่อมต่อขณะที่อยู่ในซีนเกมอยู่แล้ว → spawn ให้เลย
    /// ในซีนเมนูจะถูกข้าม ซึ่งเป็นหัวใจของการเลื่อน spawn ไปหลังเลือกตัวละคร
    /// </summary>
    void OnClientConnectedInGameplayScene(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        if (SceneManager.GetActiveScene().name == menuSceneName) return;
        SpawnPlayerIfMissing(clientId);
    }

    void OnServerStopped(bool _) => UnhookPlayerSpawnEvents();

    void UnhookPlayerSpawnEvents()
    {
        if (!spawnHooked) return;
        spawnHooked = false;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback -= OnClientConnectedInGameplayScene;

        if (nm.SceneManager == null) return;   // ตายไปพร้อม SceneManager แล้ว
        nm.SceneManager.OnLoadEventCompleted  -= OnSceneLoadEventCompleted;
        nm.SceneManager.OnSynchronizeComplete -= OnClientSynchronizeComplete;
    }

    /// <summary>ทุกคนโหลดซีนเสร็จพร้อมกัน — เส้นทางปกติของการเริ่มรัน (server อยู่ใน list ด้วย)</summary>
    void OnSceneLoadEventCompleted(string sceneName, LoadSceneMode mode,
                                   List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == menuSceneName) return;

        foreach (var id in clientsCompleted)
            SpawnPlayerIfMissing(id);
    }

    /// <summary>
    /// late join — client เข้ามาหลังรันเริ่มแล้ว จึง sync ซีนเสร็จคนละจังหวะกับ
    /// OnLoadEventCompleted ที่ผ่านไปนานแล้ว ต้อง spawn ให้แยก
    /// </summary>
    void OnClientSynchronizeComplete(ulong clientId)
    {
        if (SceneManager.GetActiveScene().name == menuSceneName) return;
        SpawnPlayerIfMissing(clientId);
    }

    void SpawnPlayerIfMissing(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        if (playerPrefab == null)
        {
            Debug.LogError($"[Session] ไม่มี playerPrefab — spawn ให้ client {clientId} ไม่ได้");
            return;
        }

        // reconnect / เรียกซ้ำจากสองเส้นทาง — NGO อาจคืน player object เดิมมาแล้ว
        if (nm.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
            return;

        var go = Instantiate(playerPrefab, GetSpawnPosition(clientId), Quaternion.identity);
        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError($"[Session] '{playerPrefab.name}' ไม่มี NetworkObject — spawn player ไม่ได้");
            Destroy(go);
            return;
        }

        no.SpawnAsPlayerObject(clientId);
        Debug.Log($"[Session] spawn player ให้ client {clientId} แล้ว ที่ {go.transform.position}");
    }

    /// <summary>
    /// จุดเกิดแยกต่อผู้เล่น — ของเดิม Instantiate เปล่าๆ ทุกคนจึงเกิดที่พิกัดเดียวกัน
    /// ของ playerPrefab แล้วซ้อนทับกันหมด
    ///
    /// index มาจาก PlayerSlotRegistry ตาม CLAUDE.md ข้อ 11 (ห้ามใช้ clientId % 4
    /// เพราะ clientId ไม่ได้เรียงต่อกัน — log จริงมี client 0 กับ client 6)
    /// </summary>
    /// <summary>จุดเกิดของ client นี้ — คงที่ตลอดเกม เรียกซ้ำได้ (playermove ใช้ตอน respawn)</summary>
    public Vector3 GetSpawnPosition(ulong clientId)
    {
        if (_spawnPositions.TryGetValue(clientId, out var cached)) return cached;

        var pos = ComputeSpawnPosition(clientId);
        _spawnPositions[clientId] = pos;
        return pos;
    }

    Vector3 ComputeSpawnPosition(ulong clientId)
    {
        int slot = PlayerSlotRegistry.Instance != null
                 ? PlayerSlotRegistry.Instance.GetSlot(clientId) : -1;

        // registry ยังไม่ทันลงทะเบียน — ใช้ลำดับการ spawn แทน ดีกว่าทับกัน
        if (slot < 0) slot = _spawnFallbackIndex++;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var t = spawnPoints[slot % spawnPoints.Length];
            if (t != null) return t.position;
        }

        // ไม่ได้ตั้งจุดเกิดไว้ — กระจายเป็นวงรอบตำแหน่งของ prefab (4 คน = 4 ทิศ)
        float ang = slot * Mathf.PI * 0.5f;
        return playerPrefab.transform.position
             + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * spawnRingRadius;
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
    /// <returns>false = ไม่ได้เริ่มโหลดซีน (ไม่ใช่ host หรือ NGO ยังไม่ start)
    /// ผู้เรียกเอาไปคืนสถานะปุ่มได้ ไม่งั้นจะค้างจอที่กดอะไรไม่ได้</returns>
    public bool StartGame(string sceneName = null)
    {
        if (!IsHost)
        {
            Debug.LogWarning("[Session] StartGame: ต้องเป็น Host เท่านั้น");
            return false;
        }

        var nm = NetworkManager.Singleton;

        // NGO ยังไม่ start → ไม่มี SceneManager
        if (nm == null || !nm.IsListening)
        {
            Debug.LogError("[Session] StartGame: NetworkManager ยังไม่ได้ start — ตรวจสอบ SessionSettings → createNetworkSession = true");
            return false;
        }

        // ── ล้าง player ของรันก่อนหน้าก่อนโหลดซีน ────────────────────────────
        // SpawnAsPlayerObject ใช้ destroyWithScene = false (ค่า default ของ NGO) player object
        // จึงรอดข้าม LoadSceneMode.Single แล้ว SpawnPlayerIfMissing เห็น PlayerObject != null
        // เลย return ทิ้ง — รันใหม่ได้ตัวเดิมทั้งดุ้น ทั้ง isDead ที่ยังเป็น true (ตอนแพ้ไม่มีใคร
        // เรียก Respawn ให้), อาวุธกับสเตตัสของรันเก่า และตำแหน่งที่ตาย เพราะ GetSpawnPosition
        // ไม่เคยถูกเรียก · อาการคือกด Play Again แล้วเกิดมาตายอยู่ ขยับไม่ได้
        //
        // วางไว้ตรงนี้เพื่อครอบทุกเส้นทางที่เริ่มรัน — กดจากล็อบบี้ยังไม่มี player ก็เป็น no-op
        // และ RespawnCoroutine ที่ค้างอยู่จะตายไปพร้อม component ไม่ไปวาร์ปคนในรันใหม่
        var stale = new List<NetworkObject>();
        foreach (var c in nm.ConnectedClientsList)
            if (c.PlayerObject != null) stale.Add(c.PlayerObject);

        foreach (var po in stale)
            if (po != null && po.IsSpawned) po.Despawn(true);

        if (stale.Count > 0)
            Debug.Log($"[Session] ล้าง player ของรันก่อนหน้า {stale.Count} ตัว ก่อนโหลดซีนใหม่");

        string target = sceneName ?? (availableScenes.Length > 0 ? availableScenes[0] : "SampleScene");
        Debug.Log($"[Session] StartGame → โหลด '{target}'");
        nm.SceneManager.LoadScene(target, LoadSceneMode.Single);
        return true;
    }
}
