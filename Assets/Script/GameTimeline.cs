using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// นาฬิกาเกม + จัดตาราง event ทั้งหมด
///
/// Schedule:
///   Zone Objective / Mini Boss → นัดหมายใน TimelineSchedule (นาทีที่กำหนดเอง)
///   ตอน MainBossMinutes → Main Boss (wave หยุด)
///   Kill Main Boss → WIN | ผู้เล่นทุกคนตาย → LOSE
///
/// ตารางมาจากแมพก่อน (MapData.TierContent.schedule) ถ้าแมพไม่ได้กำหนดจึงใช้ของซีน
/// </summary>
public class GameTimeline : NetworkBehaviour
{
    public static GameTimeline Instance { get; private set; }

    // ══════════════════════════════════════════════════════════════════════
    // ตารางเวลาเป็นนัดหมายล้วน — ไม่มี start+interval แล้ว
    //
    // ตารางแบบ "เริ่มนาทีที่ X แล้วทุก Y นาที" ตอบได้แค่จังหวะที่ห่างเท่ากันตลอด
    // มันบอกไม่ได้ว่า "เอาอันนี้ที่นาที 3, 8 และ 12" ซึ่งเป็นสิ่งที่การออกแบบจังหวะ
    // เกมต้องการจริง — จุดพีคกับจุดพักไม่ได้ห่างเท่ากันเสมอ
    //
    // สองแบบอยู่ด้วยกันได้ แต่แปลว่าจังหวะของรันถูกนิยามสองที่ แล้วคนอ่าน Inspector
    // ต้องประกอบเองในหัวว่าที่จริงอะไรออกนาทีไหนบ้าง · ตอนนี้เหลือทางเดียว
    // เวลาทุกช่องที่จะมีอะไรเกิดขึ้น มองเห็นได้หมดในลิสต์เดียว
    // ══════════════════════════════════════════════════════════════════════

    [Header("Timeline — ตารางของซีนนี้")]
    [Tooltip("นัดหมายของซีน · ใช้เมื่อแมพที่เลือกไม่ได้กำหนดตารางมาเอง")]
    public TimelineCue[] cues = new TimelineCue[0];

    [Header("Main Boss")]
    [Tooltip("เวลาที่ Main Boss spawn (นาที) — wave จะหยุด · แมพ override ได้")]
    public float mainBossTimeMin      = 15f;

    [Header("Rewards (Zone Objective)")]
    [Tooltip("EXP โบนัสที่ให้เมื่อเสร็จ objective")]
    public float objectiveExpReward   = 80f;
    [Tooltip("HP ที่ฟื้นให้ผู้เล่นทุกคน")]
    public float objectiveHealAmount  = 20f;

    [Header("Start Gate")]
    [Tooltip("รอให้ทุก client ที่ต่ออยู่มี player object ก่อน จึงเริ่มนับเวลาและปล่อย wave")]
    public bool  waitForAllPlayers = true;
    [Tooltip("รอนานสุดกี่วินาทีก่อนเริ่มเองแม้ยังไม่ครบ — กันเกมค้างถ้ามีใครโหลดไม่จบหรือหลุดกลางทาง")]
    public float startWaitTimeout  = 20f;

    // ── Network Variables ─────────────────────────────────────────────────
    public NetworkVariable<float> gameTime       = new(0f,    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    /// <summary>รันเริ่มจริงแล้วหรือยัง — WaveManager รอตัวนี้ก่อนปล่อยศัตรู
    /// เป็น NetworkVariable เพื่อให้ HUD ฝั่ง client รู้ด้วยว่ายังอยู่ช่วงรอโหลด</summary>
    public NetworkVariable<bool>  hasStarted      = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool>  isMainBossPhase = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Static Events ─────────────────────────────────────────────────────
    /// <summary>
    /// ถึงเวลา spawn Zone Objective — พารามิเตอร์คือ **ชื่อแบบที่ต้องการ**
    ///
    /// ว่าง = ให้ ObjectiveManager สุ่มตามน้ำหนักเหมือนเดิม
    /// มีค่า = นัดหมายเจาะจงว่าต้องเป็นแบบนี้ (เช่น โซนที่ให้ augment)
    ///
    /// **ต้องส่งมากับ event** — ถ้าให้ผู้รับไปเดาเอาจากเวลาปัจจุบัน สองฝั่งจะมี
    /// ตารางคนละชุดที่ต้องตรงกันตลอดไป ซึ่งไม่มีทางอยู่ตรงกันได้นาน
    /// </summary>
    public static event Action<string>     OnObjectiveTime;
    /// <summary>ถึงเวลา spawn Mini Boss — พารามิเตอร์คือชื่อ prefab ที่ต้องการ (ว่าง = สุ่ม)</summary>
    public static event Action<string>     OnMiniBossTime;
    /// <summary>ถึงเวลา spawn Main Boss — wave จะหยุด</summary>
    public static event Action            OnMainBossTime;
    /// <summary>ชนะ — (gameTimeSec, level)</summary>
    public static event Action<float,int> OnGameWon;
    /// <summary>แพ้ — (gameTimeSec, level)</summary>
    public static event Action<float,int> OnGameLost;

    // ── Server State ──────────────────────────────────────────────────────
    private bool  mainBossSpawned;

    // ── ตารางที่ใช้จริงในรันนี้ ──────────────────────────────────────────
    private TimelineCue[] _activeCues;
    private bool[][]      _fired;          // ยิงไปแล้วหรือยัง — ขนานกับ _activeCues
    private float         _mainBossMin;
    private bool  gameEnded;
    private float _loseCheckTimer;
    private float _startWaitTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // ตั้งค่าซีนไว้ก่อน — ใครถาม MainBossMinutes ระหว่างรอ spawn จะได้ไม่เจอ 0
        _activeCues  = cues;
        _mainBossMin = mainBossTimeMin;
    }

    /// <summary>
    /// เลือกตารางที่จะใช้ในรันนี้ — แมพก่อน แล้วค่อยซีน
    ///
    /// ═══ แมพแทนที่ทั้งชุด ไม่ผสม ═══
    ///
    /// ถ้าแมพมีนัดหมาย ลิสต์ของซีนถูกมองข้ามทั้งก้อน · การผสมสองลิสต์แปลว่า
    /// แมพ **ลบ** นัดหมายของซีนไม่ได้เลย ทำได้แค่เพิ่ม แล้วแมพที่ออกแบบมาให้เงียบ
    /// ช่วงต้นเกมก็ยังโดนโซนของซีนแทรกอยู่ดี — จังหวะที่ตั้งใจจะไม่มีวันได้ออก
    ///
    /// ความยาวรอบแยกเงื่อนไขกัน — แมพตั้งแค่ความยาวโดยไม่แตะนัดหมายก็ได้
    /// </summary>
    void ResolveSchedule()
    {
        _activeCues  = cues;
        _mainBossMin = mainBossTimeMin;
        string source = "ซีน";

        var tier = RunSetup.Map != null ? RunSetup.Map.GetTier(RunSetup.Difficulty) : null;
        var sched = tier != null ? tier.schedule : null;

        if (sched != null)
        {
            if (sched.HasCues)
            {
                _activeCues = sched.cues;
                source = $"แมพ {RunSetup.Map.mapId} / {RunSetup.Difficulty}";
            }
            if (sched.mainBossMinutes > 0f) _mainBossMin = sched.mainBossMinutes;
        }

        _fired = new bool[_activeCues != null ? _activeCues.Length : 0][];
        for (int i = 0; i < _fired.Length; i++)
            _fired[i] = new bool[_activeCues[i] != null ? _activeCues[i].TimeCount : 0];

        if (!IsServer) return;

        WarnOnEmptySchedule(source);
    }

    /// <summary>
    /// ตารางที่ขาดทั้งชนิด = ระบบนั้นไม่ทำงานทั้งรัน โดยไม่มีอาการอื่นเลย
    ///
    /// ของเดิมมีตาราง start+interval เป็นค่าเริ่มต้น ระบบจึง **รับประกันว่าทำงาน**
    /// แม้ไม่มีใครตั้งอะไร · ตอนนี้ทุกอย่างมาจากนัดหมายล้วน ลิสต์ว่างจึงถูกต้อง
    /// ตามกฎแต่ไม่ใช่สิ่งที่ใครตั้งใจ — ต้องบ่น ไม่ใช่เงียบ
    /// </summary>
    void WarnOnEmptySchedule(string source)
    {
        int zones = 0, minis = 0;
        if (_activeCues != null)
            foreach (var c in _activeCues)
            {
                if (c == null) continue;
                if (c.kind == TimelineCueKind.ZoneObjective) zones += c.TimeCount;
                else                                        minis += c.TimeCount;
            }

        if (zones == 0)
            Debug.LogWarning("[GameTimeline] ตารางไม่มีนัดหมาย Zone Objective เลย — " +
                             "จะไม่มีเควสต์โซนออกทั้งรัน");
        if (minis == 0)
            Debug.LogWarning("[GameTimeline] ตารางไม่มีนัดหมาย Mini Boss เลย — " +
                             "จะไม่มีมินิบอสออกทั้งรัน");

        Debug.Log($"[GameTimeline] เริ่ม — ตารางจาก{source} · " +
                  $"โซน {zones} ครั้ง · มินิบอส {minis} ครั้ง · บอสใหญ่ {_mainBossMin} นาที");
    }

    public override void OnNetworkSpawn()
    {
        // แก้ตารางให้ทุกฝั่ง ไม่ใช่เฉพาะ server — client อ่าน MainBossMinutes ไปทำ HUD
        // (BossHUDUI นับถอยหลัง enrage จากค่านี้) ถ้าฝั่ง client ยังถือ 15 อยู่ทั้งที่
        // แมพตั้ง 12 นาฬิกาบนจอจะเดินคนละเรื่องกับสิ่งที่เกิดจริง
        ResolveSchedule();
    }

    // ── Server Update ─────────────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || gameEnded) return;

        // ยังไม่เริ่ม = ไม่นับเวลา · ของเดิมนาฬิกาเดินตั้งแต่ OnNetworkSpawn ของตัวเอง
        // ซึ่งเกิดก่อนที่ GameSessionManager จะ spawn player เสร็จ ทำให้ศัตรูออกมาก่อน
        // ผู้เล่นโผล่ และ CheckLoseCondition ก็ประกาศแพ้ตั้งแต่ยังไม่ได้เล่น
        if (!hasStarted.Value) { TryBeginRun(); return; }

        gameTime.Value += Time.deltaTime;
        float t = gameTime.Value;

        // Zone Objective / Mini Boss ทั้งหมดเดินทางนี้ทางเดียว
        TickCues(t);

        // Main Boss
        if (!mainBossSpawned && t >= _mainBossMin * 60f)
        {
            mainBossSpawned       = true;
            isMainBossPhase.Value = true;
            TriggerMainBossClientRpc();
            Debug.Log($"[GameTimeline] MAIN BOSS — t={FormatTime(t)}");
        }

        // Lose Check (all PlayerObjects null = all dead) — ทุก 2 วิ
        // เดิมใช้ FloorToInt(t) % 2 == 0 ซึ่งเป็นจริงทุกเฟรมตลอดวินาทีคู่ → รัน ~60-120 ครั้งแทนที่จะเป็น 1
        if (t > 3f)
        {
            _loseCheckTimer += Time.deltaTime;
            if (_loseCheckTimer >= 2f)
            {
                _loseCheckTimer = 0f;
                CheckLoseCondition();
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────
    /// <summary>เรียกจาก BossManager เมื่อ Main Boss ตาย → WIN</summary>
    public void TriggerWin()
    {
        if (!IsServer || gameEnded) return;
        gameEnded             = true;
        isMainBossPhase.Value = false;
        SendAllFinalStats(true);
        GameWonClientRpc(gameTime.Value, GetLevel());
        Debug.Log($"[GameTimeline] ✅ WIN — t={FormatTime(gameTime.Value)}");
    }

    // ── Server Helpers ────────────────────────────────────────────────────
    /// <summary>
    /// ประตูเริ่มรัน — เปิดเมื่อทุก client ที่ต่ออยู่มี player object แล้ว
    ///
    /// มี timeout กันค้าง เพราะถ้ามีใครโหลดไม่จบหรือหลุดระหว่าง sync
    /// การรอแบบไม่มีที่สิ้นสุดจะทำให้คนที่เหลือติดอยู่ในจอเปล่า
    ///
    /// เช็คเฉพาะตอนยังไม่เริ่ม — คน join ทีหลังจึงไม่ทำให้เกมหยุดนับเวลาใหม่
    /// </summary>
    void TryBeginRun()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        int connected = 0, ready = 0;
        foreach (var c in nm.ConnectedClientsList)
        {
            connected++;
            if (c.PlayerObject != null) ready++;
        }

        _startWaitTimer += Time.deltaTime;

        bool enough = waitForAllPlayers ? (connected > 0 && ready >= connected)
                                        : ready > 0;

        if (!enough)
        {
            if (_startWaitTimer < startWaitTimeout) return;
            Debug.LogWarning($"[GameTimeline] รอครบ {startWaitTimeout:F0}s แล้วยังพร้อมแค่ {ready}/{connected} คน — เริ่มไปก่อน");
        }

        hasStarted.Value = true;
        Debug.Log($"[GameTimeline] ▶ เริ่มนับเวลา — ผู้เล่นพร้อม {ready}/{connected} คน (รอไป {_startWaitTimer:F1}s)");
    }

    void CheckLoseCondition()
    {
        if (NetworkManager.Singleton == null) return;

        // ต้องแยก "ยังไม่เกิด" ออกจาก "เกิดแล้วตาย" — ของเดิมเหมารวมเป็นแพ้ทั้งคู่
        // GameSessionManager spawn player หลังซีนโหลดเสร็จ ซึ่งช้ากว่านาฬิกาเกมเริ่มเดิน
        // เช็คแรกเกิดที่ t≈4s จึงเจอ PlayerObject เป็น null ทั้งหมด แล้วประกาศแพ้
        // ทั้งที่ผู้เล่นยังไม่ทันโผล่ (เห็นใน log: LOSE t=00:04 ก่อน spawn 2 วินาที)
        int spawned = 0;
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var po = c.PlayerObject;
            if (po == null) continue;

            var pm = po.GetComponent<playermove>();
            if (pm == null) continue;

            spawned++;
            if (!pm.isDead.Value) return;   // มีคนรอดอยู่
        }

        if (spawned == 0) return;           // ยังไม่มีใครเกิดเลย ไม่ใช่ตายหมด

        gameEnded = true;
        SendAllFinalStats(false);
        GameLostClientRpc(gameTime.Value, GetLevel());
        Debug.Log($"[GameTimeline] ❌ LOSE — t={FormatTime(gameTime.Value)}");
    }

    void SendAllFinalStats(bool isWin)
    {
        if (NetworkManager.Singleton == null) return;
        float finalTime = gameTime.Value;
        int finalLevel = GetLevel();
        foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
        {
            var pwm = c.PlayerObject?.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.SendFinalStats(finalTime, finalLevel, isWin);
            }
        }
    }

    int GetLevel() => SharedExperienceManager.Instance?.GetCurrentLevel() ?? 0;

    static string FormatTime(float s)
    {
        int m = Mathf.FloorToInt(s / 60f);
        int sec = Mathf.FloorToInt(s % 60f);
        return $"{m:00}:{sec:00}";
    }

    // ── ClientRpc ─────────────────────────────────────────────────────────
    /// <summary>
    /// ยิงนัดหมายที่ถึงเวลาแล้ว — แต่ละเวลายิงครั้งเดียวตลอดรัน
    ///
    /// ═══ ทำไมต้องจำว่ายิงไปแล้ว ═══
    ///
    /// เงื่อนไขคือ `t >= เวลาที่นัด` ซึ่งเป็นจริง **ตลอดไป** หลังผ่านนาทีนั้น
    /// ไม่จำก็จะยิงทุกเฟรมจนจบเกม — บั๊กเดียวกับที่ `_loseCheckTimer` เคยเป็น
    /// (ของเดิมเช็ค `FloorToInt(t) % 2 == 0` แล้วรัน 60-120 ครั้งต่อวินาทีคู่)
    ///
    /// ธงอยู่ที่ `_fired` ของ component ไม่ใช่บนตัวนัดหมาย เพราะนัดหมายอาจมาจาก
    /// MapData ซึ่งเป็น asset ที่อยู่ข้ามรัน — เขียนธงลงไปจะติดค้างถึงรันถัดไป
    ///
    /// ═══ เลยเวลาแล้วยังยิง ═══
    ///
    /// ใช้ &gt;= ไม่ใช่ช่วงแคบๆ รอบนาทีนั้น · เฟรมเดียวที่ตรงเป๊ะไม่มีจริง และถ้า
    /// เกมกระตุกข้ามนาทีที่นัดไป นัดนั้นต้องยังเกิด ไม่ใช่หายไปเงียบๆ
    /// </summary>
    void TickCues(float t)
    {
        if (_activeCues == null) return;

        for (int c = 0; c < _activeCues.Length; c++)
        {
            var cue = _activeCues[c];
            if (cue == null || cue.TimeCount == 0) continue;

            // ตารางมาจาก asset ได้ — ธงจึงอยู่ที่นี่ ไม่ใช่บนตัวนัดหมาย
            if (_fired[c] == null || _fired[c].Length != cue.TimeCount)
                _fired[c] = new bool[cue.TimeCount];

            for (int i = 0; i < cue.atMinutes.Length; i++)
            {
                if (_fired[c][i]) continue;
                if (t < cue.atMinutes[i] * 60f) continue;

                _fired[c][i] = true;
                FireCue(cue, t);
            }
        }
    }

    void FireCue(TimelineCue cue, float t)
    {
        switch (cue.kind)
        {
            case TimelineCueKind.ZoneObjective: TriggerObjectiveClientRpc(cue.variant); break;
            case TimelineCueKind.MiniBoss:      TriggerMiniBossClientRpc(cue.variant);  break;
        }

        Debug.Log($"[GameTimeline] นัดหมาย '{cue.DisplayName}' ({cue.kind}" +
                  (string.IsNullOrEmpty(cue.variant) ? "" : $" · {cue.variant}") +
                  $") — t={FormatTime(t)}");
    }

    [ClientRpc] void TriggerObjectiveClientRpc(string variant) => OnObjectiveTime?.Invoke(variant);
    [ClientRpc] void TriggerMiniBossClientRpc(string variant)  => OnMiniBossTime?.Invoke(variant);
    [ClientRpc] void TriggerMainBossClientRpc()          => OnMainBossTime?.Invoke();
    [ClientRpc] void GameWonClientRpc(float t, int lvl)  => OnGameWon?.Invoke(t, lvl);
    [ClientRpc] void GameLostClientRpc(float t, int lvl) => OnGameLost?.Invoke(t, lvl);

    // ── Getters ───────────────────────────────────────────────────────────
    public float GetGameTime()      => gameTime.Value;
    /// <summary>ความยาวรอบที่ใช้จริง — แมพ override ได้ อย่าอ่าน mainBossTimeMin ตรงๆ</summary>
    public float MainBossMinutes    => _mainBossMin;
    public bool  IsMainBossPhase()  => isMainBossPhase.Value;
    public string GetFormattedTime() => FormatTime(gameTime.Value);
}
