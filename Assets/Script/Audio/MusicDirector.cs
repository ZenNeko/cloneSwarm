using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัดสินว่าตอนนี้เพลงแต่ละชั้นควรดังเท่าไร แล้วสั่ง SoundManager — วางไว้ในซีนเกม (SampleScene)
/// ดูแบบทั้งหมดที่ docs/design-dynamic-bgm.md
///
/// ═══ ไม่ใช่ NetworkBehaviour — และไม่มี RPC ใหม่ ═══
///
/// ทุกเครื่องคำนวณเองจากสถานะที่ sync อยู่แล้ว (gameTime · isMainBossPhase · บอสเกิด/ตาย/เปลี่ยนเฟส ·
/// จอเลือกการ์ด · ชนะ/แพ้) · ถ้า server สั่ง "เปิดเบส" ผ่าน RPC คนที่เข้ามากลางเกมจะพลาดคำสั่ง
/// คำนวณจากสถานะแทน → เข้ามาเมื่อไรก็ได้ mix ถูกทันที
///
/// ═══ เก็บข้อเท็จจริง ไม่เก็บสถานะเป็นขั้น ═══
///
/// Recompute() สร้าง mix ใหม่ทั้งชุดจากข้อเท็จจริงทุกครั้ง · ช่วงเวลาเปลี่ยนระหว่างสู้มินิบอส
/// หรือปิดจอเลือกการ์ดกลางไฟต์บอส ไม่ต้องจำว่า "ก่อนหน้านี้คืออะไร" — สูตรเดิมให้คำตอบที่ถูกเอง
///
/// ลำดับ:  ชนะ/แพ้  >  บอสใหญ่  >  max(ช่วงเวลา, overlay มินิบอส)  แล้วใส่ override จอเลือกการ์ดทับ
/// </summary>
public class MusicDirector : MonoBehaviour
{
    /// <summary>ตัวที่ทำงานอยู่ในซีน — SceneBGMPlayer เช็คเพื่อถอยออกเมื่อมีเพลงซ้อนชั้นให้เล่น</summary>
    public static MusicDirector Active { get; private set; }

    [Tooltip("ใช้เมื่อแมพ/ระดับที่เลือกไม่ได้ตั้ง musicProfile · ว่างทั้งคู่ = ปล่อยให้ SceneBGMPlayer เล่นแบบเดิม")]
    public MusicProfile sceneProfile;

    [Tooltip("เช็คเวลาเกมทุกกี่วินาที — เพลงไม่ต้องการความแม่นระดับเฟรม")]
    [Min(0.1f)] public float pollInterval = 0.5f;

    public MusicProfile Profile { get; private set; }
    public bool HasProfile => Profile != null && Profile.track != null && Profile.track.StemCount > 0;

    /// <summary>สถานะที่ใช้อยู่ — สำหรับ DevTools/log</summary>
    public string StateLabel { get; private set; } = "-";

    enum Result { None, Won, Lost }

    // ── ข้อเท็จจริง ───────────────────────────────────────────────────────
    Result _result;
    readonly HashSet<BossController> _miniBosses = new();
    float  _miniReleaseAt = -1f;
    int    _bossPhase;
    bool   _cardPick;

    // ── ของที่คำนวณแล้วส่งไป ─────────────────────────────────────────────
    MusicMix _mix = new();
    float[] _sent    = new float[0];
    LayeredTrack _sentTrack;
    float  _sentDuck = 1f;
    bool   _sentCardPick, _sentOverlay;
    float  _pollTimer;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        Active = this;

        // RunSetup ตั้งบนทุกเครื่องก่อนเข้าซีน (client รับจาก LobbyState ใน LobbyUI)
        var tier = RunSetup.Map != null ? RunSetup.Map.GetTier(RunSetup.Difficulty) : null;
        Profile = tier != null && tier.musicProfile != null ? tier.musicProfile : sceneProfile;
    }

    void OnEnable()
    {
        BossController.OnAnyBossSpawned   += OnBossSpawned;
        BossController.OnAnyBossDespawned  += OnBossDespawned;
        BossController.OnAnyPhaseChanged   += OnPhaseChanged;
        SharedExperienceManager.OnUpgradePhaseStart += OnLevelUpStart;
        SharedExperienceManager.OnOrbPhaseStart     += OnOrbStart;
        SharedExperienceManager.OnUpgradePhaseEnd   += OnCardPickEnd;   // ใช้ร่วมกันทั้งเลเวลอัปและ orb
        GameTimeline.OnGameWon  += OnWon;
        GameTimeline.OnGameLost += OnLost;
    }

    void OnDisable()
    {
        BossController.OnAnyBossSpawned   -= OnBossSpawned;
        BossController.OnAnyBossDespawned -= OnBossDespawned;
        BossController.OnAnyPhaseChanged  -= OnPhaseChanged;
        SharedExperienceManager.OnUpgradePhaseStart -= OnLevelUpStart;
        SharedExperienceManager.OnOrbPhaseStart     -= OnOrbStart;
        SharedExperienceManager.OnUpgradePhaseEnd   -= OnCardPickEnd;
        GameTimeline.OnGameWon  -= OnWon;
        GameTimeline.OnGameLost -= OnLost;

        // ออกจากซีนกลางจอเลือกการ์ด (Quit · หลุดจาก host) — duck อยู่ใน SoundManager ซึ่งอยู่ข้ามซีน
        // ถ้าไม่คืน เพลงเมนูและรันถัดไปจะดังแค่ครึ่งเดียวไปตลอด
        // เช็ค Exists ก่อน — Instance สร้างตัวใหม่เองถ้าไม่มี ซึ่งตอนปิดเกมจะทิ้ง GameObject ค้าง
        if (!Mathf.Approximately(_sentDuck, 1f) && SoundManager.Exists)
        {
            SoundManager.Instance.SetMusicDuck(1f, 0f);
            _sentDuck = 1f;
        }

        if (Active == this) Active = null;
    }

    void Start()
    {
        if (!HasProfile) return;

        // บอสที่ spawn ไปแล้วก่อนเรา subscribe — ทางนี้คือสิ่งที่ทำให้เข้ากลางเกมได้ mix ถูก
        foreach (var bc in FindObjectsByType<BossController>())
            if (bc.IsSpawned) OnBossSpawned(bc);

        Recompute();
    }

    void Update()
    {
        if (!HasProfile || _result != Result.None) return;

        // unscaled — จอเลือกการ์ดตั้ง timeScale = 0 แต่ release hold ของมินิบอสยังต้องเดิน
        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer > 0f) return;
        _pollTimer = pollInterval;
        Recompute();
    }

    /// <summary>
    /// DevTools — สลับเพลงกลางรันเพื่อฟังทดสอบ โดยไม่ต้องแก้ MapData · สถานะเกม (บอส · จอการ์ด) คงเดิม
    /// </summary>
    public void DevUseProfile(MusicProfile profile)
    {
        if (profile == null || _result != Result.None) return;
        Profile      = profile;
        _mix         = new MusicMix();
        _sentTrack   = null;
        _sentOverlay = _sentCardPick = false;
        Recompute();
    }

    // ── Events ────────────────────────────────────────────────────────────
    void OnBossSpawned(BossController bc)
    {
        if (bc == null) return;
        if (bc.IsMainBoss.Value) _bossPhase = 0;
        else _miniBosses.Add(bc);
        Recompute();
    }

    void OnBossDespawned(BossController bc)
    {
        if (bc == null || !_miniBosses.Remove(bc)) return;
        if (_miniBosses.Count == 0 && Profile != null)
            _miniReleaseAt = Time.unscaledTime + Profile.miniBossReleaseHold;
        Recompute();
    }

    void OnPhaseChanged(BossController bc, int phase)
    {
        // มินิบอสก็มีเฟส — ไม่กรองแล้วเพลงบอสจะเปลี่ยนตามมินิบอส
        if (bc == null || !bc.IsMainBoss.Value) return;
        _bossPhase = phase;
        Recompute();
    }

    void OnLevelUpStart(int _)       { _cardPick = true;  Recompute(); }
    void OnOrbStart(OrbReward _)     { _cardPick = true;  Recompute(); }
    void OnCardPickEnd()             { _cardPick = false; Recompute(); }

    void OnWon(float t, int lvl)  => EndRun(Result.Won);
    void OnLost(float t, int lvl) => EndRun(Result.Lost);

    void EndRun(Result r)
    {
        if (_result != Result.None || !HasProfile) return;
        _result = r;
        StateLabel = r.ToString();

        var sm = SoundManager.Instance;
        sm.SetMusicDuck(1f, 0f);
        sm.StopMusic(Profile.endFade);
        sm.PlaySfx2D(r == Result.Won ? Profile.winStinger : Profile.loseStinger);
    }

    // ── Core ──────────────────────────────────────────────────────────────
    void Recompute()
    {
        if (!HasProfile || _result != Result.None) return;

        var tl = GameTimeline.Instance;
        var r  = _mix.Evaluate(Profile, new MusicMix.Facts
        {
            minutes       = tl != null ? tl.gameTime.Value / 60f : 0f,
            mainBossPhase = tl != null && tl.isMainBossPhase.Value,
            bossPhase     = _bossPhase,
            miniBoss      = _miniBosses.Count > 0 || Time.unscaledTime < _miniReleaseAt,
            cardPick      = _cardPick,
            sentOverlay   = _sentOverlay,
            sentCardPick  = _sentCardPick,
        });
        StateLabel = r.label;

        Send(r);
        _sentOverlay  = r.overlay;
        _sentCardPick = _cardPick;
    }

    void Send(in MusicMix.Result r)
    {
        var sm = SoundManager.Instance;
        var levels = _mix.Levels;

        if (!Mathf.Approximately(r.duck, _sentDuck))
        {
            sm.SetMusicDuck(r.duck, Profile.cardPickFade);
            _sentDuck = r.duck;
        }

        // track เปลี่ยน (เริ่มซีน · เข้าธีมบอส) หรือถูกหยุดไปจากที่อื่น → เริ่มใหม่ด้วย crossfade
        bool playing = sm.Music != null && sm.Music.CurrentTrack == r.track && sm.IsMusicPlaying;
        if (r.track != _sentTrack || !playing)
        {
            sm.PlayMusicTrack(r.track, Profile.defaultFade, levels);
            _sentTrack = r.track;
            Remember(levels);
            return;
        }

        if (SameAsSent(levels)) return;   // ส่งซ้ำจะรีเซ็ตความเร็ว fade ที่กำลังเดินอยู่
        sm.SetMusicLevels(levels, r.fade);
        Remember(levels);
    }

    void Remember(float[] levels)
    {
        if (_sent.Length != levels.Length) _sent = new float[levels.Length];
        System.Array.Copy(levels, _sent, levels.Length);
    }

    bool SameAsSent(float[] levels)
    {
        if (_sent.Length != levels.Length) return false;
        for (int i = 0; i < levels.Length; i++)
            if (!Mathf.Approximately(_sent[i], levels[i])) return false;
        return true;
    }
}
