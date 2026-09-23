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
    float[] _work    = new float[0];
    float[] _sent    = new float[0];
    float[] _preBoss = new float[0];   // mix ล่าสุดของเพลงหลักก่อนบอสออก — ใช้ตอนไม่มีธีมบอส
    LayeredTrack _sentTrack;
    float  _sentDuck = 1f;
    bool   _sentCardPick, _sentOverlay;
    float  _pollTimer;

    // ชื่อ stem → index แปลงครั้งเดียวต่อ (รายการ, track) · ไม่เทียบสตริงทุกรอบ
    readonly Dictionary<(StemLevel[], LayeredTrack), int[]> _indexCache = new();
    readonly HashSet<string> _warnedNames = new();

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

        var  tl        = GameTimeline.Instance;
        bool mainBoss  = tl != null && tl.isMainBossPhase.Value;
        bool bossTheme = mainBoss && Profile.mainBossTrack != null && Profile.mainBossTrack.StemCount > 0;
        var  track     = bossTheme ? Profile.mainBossTrack : Profile.track;

        EnsureSize(ref _work, track.StemCount);
        System.Array.Clear(_work, 0, _work.Length);

        bool  overlay = false;
        float fade;

        if (bossTheme)
        {
            var mix = Profile.PhaseMix(_bossPhase);
            Fill(_work, mix, track);
            fade = Profile.FadeOf(mix);
            StateLabel = $"MainBoss (ธีม) เฟส {_bossPhase + 1}";
        }
        else if (mainBoss)
        {
            // ไม่มีธีมบอส → เพลงเดิมเล่นต่อ ค้าง mix ล่าสุดก่อนบอสออก ไม่ปรับอะไร (ตัดสินแล้ว 2026-09-23)
            // เข้ามากลางไฟต์บอสจะไม่มี mix ก่อนหน้า → ใช้ช่วงเวลาล่าสุดแทน ดีกว่าเงียบทั้งไฟต์
            if (_preBoss.Length == 0 && tl != null)
            {
                int last = Profile.BandIndexAt(tl.gameTime.Value / 60f);
                EnsureSize(ref _preBoss, _work.Length);
                if (last >= 0) Fill(_preBoss, Profile.timeBands[last].mix, track);
            }
            System.Array.Copy(_preBoss, _work, Mathf.Min(_preBoss.Length, _work.Length));
            fade = Profile.defaultFade;
            StateLabel = "MainBoss (เพลงเดิม)";
        }
        else
        {
            float minutes = tl != null ? tl.gameTime.Value / 60f : 0f;
            int band = Profile.BandIndexAt(minutes);
            StemMix bandMix = band >= 0 ? Profile.timeBands[band].mix : null;
            Fill(_work, bandMix, track);
            fade = Profile.FadeOf(bandMix);
            StateLabel = band >= 0 ? $"ช่วง {band + 1} (นาที {Profile.timeBands[band].atMinutes:0.#})" : "ไม่มีช่วงเวลา";

            overlay = _miniBosses.Count > 0 || Time.unscaledTime < _miniReleaseAt;
            if (overlay)
            {
                MaxInto(_work, Profile.miniBossOverlay, track);
                StateLabel += " + มินิบอส";
            }
            if (overlay != _sentOverlay) fade = Profile.FadeOf(Profile.miniBossOverlay);

            EnsureSize(ref _preBoss, _work.Length);
            System.Array.Copy(_work, _preBoss, _work.Length);
        }

        // จอเลือกการ์ด — ใส่ท้ายสุด ปิดจอแล้วสูตรข้างบนคืน mix ที่ถูกให้เอง
        if (_cardPick)
        {
            Override(_work, Profile.cardPickOverrides, track);
            StateLabel += " · เลือกการ์ด";
        }
        if (_cardPick != _sentCardPick) fade = Profile.cardPickFade;

        Send(track, fade);
        _sentOverlay  = overlay;
        _sentCardPick = _cardPick;
    }

    void Send(LayeredTrack track, float fade)
    {
        var sm = SoundManager.Instance;

        float duck = _cardPick ? Profile.cardPickDuck : 1f;
        if (!Mathf.Approximately(duck, _sentDuck))
        {
            sm.SetMusicDuck(duck, Profile.cardPickFade);
            _sentDuck = duck;
        }

        // track เปลี่ยน (เริ่มซีน · เข้าธีมบอส) หรือถูกหยุดไปจากที่อื่น → เริ่มใหม่ด้วย crossfade
        bool playing = sm.Music != null && sm.Music.CurrentTrack == track && sm.IsMusicPlaying;
        if (track != _sentTrack || !playing)
        {
            sm.PlayMusicTrack(track, Profile.defaultFade, _work);
            _sentTrack = track;
            Remember();
            return;
        }

        if (SameAsSent()) return;   // ส่งซ้ำจะรีเซ็ตความเร็ว fade ที่กำลังเดินอยู่
        sm.SetMusicLevels(_work, fade);
        Remember();
    }

    void Remember()
    {
        EnsureSize(ref _sent, _work.Length);
        System.Array.Copy(_work, _sent, _work.Length);
    }

    bool SameAsSent()
    {
        if (_sent.Length != _work.Length) return false;
        for (int i = 0; i < _work.Length; i++)
            if (!Mathf.Approximately(_sent[i], _work[i])) return false;
        return true;
    }

    // ── Mix helpers ───────────────────────────────────────────────────────
    void Fill(float[] into, StemMix mix, LayeredTrack track)
    {
        if (mix?.levels == null) return;
        var idx = Indices(mix.levels, track);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = mix.levels[i].level;
    }

    void MaxInto(float[] into, StemMix mix, LayeredTrack track)
    {
        if (mix?.levels == null) return;
        var idx = Indices(mix.levels, track);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = Mathf.Max(into[idx[i]], mix.levels[i].level);
    }

    void Override(float[] into, StemLevel[] levels, LayeredTrack track)
    {
        if (levels == null) return;
        var idx = Indices(levels, track);
        for (int i = 0; i < idx.Length; i++)
            if (idx[i] >= 0) into[idx[i]] = levels[i].level;
    }

    int[] Indices(StemLevel[] levels, LayeredTrack track)
    {
        var key = (levels, track);
        if (_indexCache.TryGetValue(key, out var idx) && idx.Length == levels.Length) return idx;

        idx = new int[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            idx[i] = track.IndexOf(levels[i].stem);
            if (idx[i] < 0 && !string.IsNullOrEmpty(levels[i].stem) &&
                _warnedNames.Add($"{track.name}/{levels[i].stem}"))
                Debug.LogWarning($"[Music] '{Profile.name}' อ้าง stem '{levels[i].stem}' ที่ไม่มีใน track '{track.name}' — ข้ามชั้นนี้", Profile);
        }
        _indexCache[key] = idx;
        return idx;
    }

    static void EnsureSize(ref float[] arr, int n)
    {
        if (arr.Length != n) arr = new float[n];
    }
}
