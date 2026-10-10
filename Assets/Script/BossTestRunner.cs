#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ปุ่ม "▶ ทดสอบในเกม เฟส N" ของ Boss Designer — ฝั่งที่ทำงานใน Play Mode (Editor เท่านั้น)
///
/// หน้าต่างเขียนคำขอลง SessionState แล้วเข้า Play Mode ใน **MenuScene** · ตัวนี้อ่านคำขอ
/// **ครั้งเดียวแล้วลบทิ้ง** (กด Play ปกติรอบถัดไปจะไม่มีบอสโผล่มาเอง) แล้วเดินทางเข้าเกมจริง:
///   1. กด PLAY ให้ (MenuManager.OnPlayClicked → host ออฟไลน์ + LobbyState)
///   2. ตั้ง RunSetup เป็นแมพ/ระดับที่ใช้ config นี้ (ถ้ามี) → GameSessionManager.StartGame
///      — ทางเดียวกับปุ่มเริ่มรันในล็อบบี้ player จึงถูก spawn หลังโหลดซีนตามปกติ
///   3. รอ GameTimeline เริ่มรัน → เรียกบอสใหญ่ด้วย config นี้ → ข้ามไปเฟส N ผ่านทางเปลี่ยนเฟสจริง
///   4. ผู้เล่นอมตะถ้าเลือกไว้
///
/// ═══ ทำไมไม่เปิด SampleScene ตรงๆ ═══
/// NetworkManager อยู่ใน MenuScene (DontDestroyOnLoad) · SampleScene ไม่มี · และ GameSessionManager
/// ปิด auto-spawn player ไว้แล้ว spawn เองหลัง NGO โหลดซีนเกม — เปิดซีนเกมตรงๆ จึงไม่มีทั้ง host และ player
///
/// ไม่ถูกคอมไพล์เข้า build (ครอบ UNITY_EDITOR ทั้งไฟล์ และไม่มี component ในซีน)
/// </summary>
public class BossTestRunner : MonoBehaviour
{
    public const string KeyConfig = "CloneSwarm.BossTest.ConfigGuid";
    public const string KeyPhase  = "CloneSwarm.BossTest.Phase";
    public const string KeyGod    = "CloneSwarm.BossTest.God";
    public const string KeyTier   = "CloneSwarm.BossTest.Tier";   // -1 = ระดับของแมพที่ใช้ config นี้
    public const string MenuScenePath = "Assets/GameScenes/MenuScene.unity";

    string _guid;
    int    _phase;
    bool   _god;
    int    _tier = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        string guid = SessionState.GetString(KeyConfig, "");
        if (string.IsNullOrEmpty(guid)) return;

        var runner = new GameObject("[BossTestRunner]").AddComponent<BossTestRunner>();
        DontDestroyOnLoad(runner.gameObject);   // ต้องรอดข้ามการโหลดซีนเกม
        runner._guid  = guid;
        runner._phase = SessionState.GetInt(KeyPhase, 0);
        runner._god   = SessionState.GetBool(KeyGod, true);
        runner._tier  = SessionState.GetInt(KeyTier, -1);

        // ใช้ครั้งเดียว
        SessionState.EraseString(KeyConfig);
        SessionState.EraseInt(KeyPhase);
        SessionState.EraseBool(KeyGod);
        SessionState.EraseInt(KeyTier);
    }

    IEnumerator Start()
    {
        var config = AssetDatabase.LoadAssetAtPath<BossEncounterConfig>(AssetDatabase.GUIDToAssetPath(_guid));
        if (config == null) { Debug.LogError("[BossTest] หา config ไม่เจอ — ยกเลิก"); yield break; }
        Debug.Log($"[BossTest] {config.name} · เฟส {_phase + 1} · อมตะ {(_god ? "เปิด" : "ปิด")}");

        // ── 1. host ออฟไลน์ผ่านเมนู ──
        // รอให้ Start ของทุกตัวในซีนวิ่งก่อน — GameSessionManager.Start ต้องปิด auto-spawn player
        // และผูก OnServerStarted *ก่อน* StartHost ไม่งั้น player เกิดในเมนูด้วยตัวละคร default
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline &&
               (NetworkManager.Singleton == null || GameSessionManager.Instance == null))
            yield return null;
        var nm = NetworkManager.Singleton;
        if (nm == null || GameSessionManager.Instance == null)
        {
            Debug.LogError("[BossTest] ไม่มี NetworkManager/GameSessionManager — ต้องเริ่มจาก MenuScene · ยกเลิก");
            yield break;
        }

        if (!nm.IsListening)
        {
            var menu = FindFirstObjectByType<MenuManager>();
            if (menu != null) menu.OnPlayClicked();
            else if (!nm.StartHost()) { Debug.LogError("[BossTest] StartHost ไม่สำเร็จ"); yield break; }
        }
        deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline && !(nm.IsListening && nm.IsServer)) yield return null;
        if (!nm.IsServer) { Debug.LogError("[BossTest] host ไม่เริ่ม — ยกเลิก"); yield break; }
        // LobbyState เกิดหลัง host หนึ่งเฟรม — รอสั้นๆ ไม่บังคับ (ไม่มีก็เล่นได้ด้วยตัวละคร default)
        deadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < deadline && LobbyState.Instance == null) yield return null;

        // ── 2. เลือกแมพที่ใช้ config นี้ แล้วเริ่มรันแบบเดียวกับปุ่มในล็อบบี้ ──
        var (map, tier) = FindMapFor(config);
        if (_tier >= 0) tier = (DifficultyTier)_tier;   // ระดับที่เลือกดูใน Boss Designer
        RunSetup.Set(map, tier);
        string scene = map != null && !string.IsNullOrEmpty(map.sceneName) ? map.sceneName : "SampleScene";
        if (!GameSessionManager.Instance.StartGame(scene)) { Debug.LogError("[BossTest] StartGame ไม่สำเร็จ — ดู log ของ [Session]"); yield break; }
        Debug.Log($"[BossTest] โหลด {scene} · แมพ {(map != null ? $"{map.mapId}/{tier}" : "(ไม่มี — ค่า default)")}");

        // ── 3. รอเกมเริ่มจริง (GameTimeline รอผู้เล่นโหลด) — กันบอสเกิดก่อนผู้เล่น ──
        deadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < deadline &&
               (GameTimeline.Instance == null || !GameTimeline.Instance.hasStarted.Value || BossManager.Instance == null))
            yield return null;
        if (BossManager.Instance == null) { Debug.LogError("[BossTest] ไม่มี BossManager ในซีนเกม — ยกเลิก"); yield break; }
        if (nm.LocalClient?.PlayerObject == null) Debug.LogWarning("[BossTest] ยังไม่เห็น player ของ host — บอสจะเกิดอยู่ดี แต่ตรวจ log ของ [Session]");

        playermove.DevInvulnerable = _god;
        BossManager.Instance.DevSpawnMainBoss(config);

        // รอบอส spawn แล้วค่อยข้ามเฟส (OnNetworkSpawn ต้องตั้ง enemy/phase ก่อน)
        BossController boss = null;
        deadline = Time.realtimeSinceStartup + 5f;
        while (boss == null && Time.realtimeSinceStartup < deadline)
        {
            boss = FindObjectsByType<BossController>().FirstOrDefault(b => b.IsSpawned && b.IsMainBoss.Value);
            yield return null;
        }
        if (boss == null) { Debug.LogError("[BossTest] บอสไม่เกิด — ดู log ของ BossManager"); yield break; }

        if (_phase > 0) boss.DevJumpToPhase(_phase);
        Debug.Log($"[BossTest] พร้อม — {config.name} เฟส {_phase + 1}");
    }

    /// <summary>แมพ/ระดับแรกที่ใช้ config นี้เป็นบอสใหญ่ — ไม่มีก็คืน null (เกมใช้ค่า default)</summary>
    static (MapData, DifficultyTier) FindMapFor(BossEncounterConfig config)
    {
        foreach (var g in AssetDatabase.FindAssets("t:MapData"))
        {
            var m = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(g));
            if (m?.tiers == null) continue;
            foreach (var t in m.tiers)
                if (t != null && t.mainBossConfig == config) return (m, t.tier);
        }
        return (null, DifficultyTier.Normal);
    }

    void OnDestroy() => playermove.DevInvulnerable = false;
}
#endif
