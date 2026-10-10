using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ตัวเล่นเพลงแบบซ้อนชั้น — SoundManager สร้างและถือไว้ (ข้ามฉาก) · อย่าเรียกตรง ให้ผ่าน SoundManager
///
/// ═══ หน้าที่ ═══
///
///   • เล่นทุก stem ของ track พร้อมกันด้วย PlayScheduled เวลาเดียวกัน — Play() ทีละตัว
///     เหลื่อมกันหลายมิลลิวินาทีและได้ยินเป็นเสียงก้อง
///   • fade ระดับของแต่ละชั้นไปหาค่าเป้าหมาย · ชั้นที่เงียบยังเล่นต่อ ห้าม Stop
///     เพราะเปิดกลับมาจะหลุดจังหวะ
///   • สลับ track ด้วย crossfade สองชุด (deck) — ใช้ตอนเข้าธีมบอส และตอนเปลี่ยนซีน
///
/// ═══ volume ที่ AudioSource ได้จริง ═══
///
///   stem level × deck gain × duck × output scale (Music × Master จาก SoundManager)
///
/// คำนวณจากสี่ค่านี้ทุกเฟรมเสมอ · ไม่มีใคร set AudioSource.volume ตรงจากข้างนอก —
/// ของเดิม RescalePlaying() เขียน volume ทับ ถ้าปล่อยไว้ ขยับ slider แล้วชั้นที่ปิดอยู่จะดังเต็ม
///
/// ═══ เวลา ═══
///
/// เดินด้วย Time.unscaledDeltaTime — จอเลือกการ์ดตั้ง timeScale = 0 ถ้าใช้เวลาปกติ fade จะค้างครึ่งทาง
/// </summary>
public class LayeredMusicPlayer : MonoBehaviour
{
    // หน่วงเริ่มเล่นเล็กน้อย ให้ทุก source ทันรับคำสั่ง PlayScheduled ในรอบ audio เดียวกัน
    const double ScheduleLead = 0.1;

    class Deck
    {
        public LayeredTrack  track;
        public AudioSource[] src    = new AudioSource[0];
        public float[]       cur    = new float[0];
        public float[]       target = new float[0];
        public float[]       rate   = new float[0];
        public int           count;
        public float         gain, gainTarget, gainRate;
        public bool          releaseWhenSilent;

        public void EnsureCapacity(int n)
        {
            if (src.Length >= n) return;
            System.Array.Resize(ref src,    n);
            System.Array.Resize(ref cur,    n);
            System.Array.Resize(ref target, n);
            System.Array.Resize(ref rate,   n);
        }
    }

    Deck _live = new();
    Deck _out  = new();

    // source คืนมาแล้วเก็บไว้ใช้ใหม่ — จองตาม track ที่มีชั้นมากสุดที่เคยเจอ ขยายได้ ไม่หด
    readonly List<AudioSource> _free = new();
    int _sourceSerial;

    AudioMixerGroup _group;
    float _outputScale = 1f;
    float _duck = 1f, _duckTarget = 1f, _duckRate;

    readonly HashSet<LayeredTrack> _validated = new();

    // ── Setup ─────────────────────────────────────────────────────────────
    public void Init(AudioMixerGroup group) => _group = group;

    // ── Query ─────────────────────────────────────────────────────────────
    public LayeredTrack CurrentTrack => _live.track;
    public int   StemCount           => _live.count;
    public float Duck                => _duck;
    public float StemLevel(int i)    => i >= 0 && i < _live.count ? _live.cur[i] : 0f;

    public bool IsPlaying
    {
        get
        {
            for (int i = 0; i < _live.count; i++)
                if (_live.src[i] != null && _live.src[i].isPlaying) return true;
            return false;
        }
    }

    // ── Control ───────────────────────────────────────────────────────────

    /// <summary>
    /// เล่น track นี้ · ถ้ามี track อื่นเล่นอยู่ crossfade ภายใน fade วินาที · 0 = ตัดทันที
    /// levels = ระดับเริ่มต้นของแต่ละชั้น (ขาด = 0) · track เดิมที่เล่นอยู่แล้ว = ไม่ทำอะไร
    /// </summary>
    public void Play(LayeredTrack track, float fade, float[] levels, bool loop = true)
    {
        if (track == null || track.StemCount == 0) return;
        if (_live.track == track && IsPlaying) return;

        WarnIfInvalid(track);

        // deck ที่กำลังดังกลายเป็นตัวที่ fade ออก · ตัวที่ยัง fade ไม่จบจากรอบก่อนตัดทิ้งเลย
        Release(_out);
        (_live, _out) = (_out, _live);
        FadeOutAndRelease(_out, fade);

        int n = track.StemCount;
        _live.track = track;
        _live.count = n;
        _live.EnsureCapacity(n);
        _live.releaseWhenSilent = false;
        _live.gain       = fade > 0f ? 0f : 1f;
        _live.gainTarget = 1f;
        _live.gainRate   = fade > 0f ? 1f / fade : 0f;

        double startAt = AudioSettings.dspTime + ScheduleLead;
        for (int i = 0; i < n; i++)
        {
            float lv = levels != null && i < levels.Length ? levels[i] : 0f;
            _live.cur[i] = _live.target[i] = lv;
            _live.rate[i] = 0f;

            var s = Acquire();
            s.clip = track.stems[i].clip;
            s.loop = loop;
            s.volume = 0f;
            if (s.clip != null) s.PlayScheduled(startAt);
            _live.src[i] = s;
        }
        ApplyVolumes();
    }

    /// <summary>ตั้งระดับเป้าหมายของแต่ละชั้นใน track ปัจจุบัน · ขาด = 0 · fade 0 = ทันที</summary>
    public void SetLevels(float[] levels, float fade)
    {
        for (int i = 0; i < _live.count; i++)
        {
            float t = levels != null && i < levels.Length ? levels[i] : 0f;
            _live.target[i] = t;
            if (fade <= 0f) { _live.cur[i] = t; _live.rate[i] = 0f; }
            else _live.rate[i] = Mathf.Abs(t - _live.cur[i]) / fade;
        }
        ApplyVolumes();
    }

    /// <summary>ลดเสียงทั้งเพลง (1 = ปกติ) · ไม่แตะระดับของชั้น</summary>
    public void SetDuck(float mult, float fade)
    {
        _duckTarget = Mathf.Clamp01(mult);
        if (fade <= 0f) { _duck = _duckTarget; _duckRate = 0f; }
        else _duckRate = Mathf.Abs(_duckTarget - _duck) / fade;
        ApplyVolumes();
    }

    /// <summary>Music × Master จาก SoundManager — เรียกทุกครั้งที่ slider ขยับ</summary>
    public void SetOutputScale(float scale)
    {
        _outputScale = Mathf.Max(0f, scale);
        ApplyVolumes();
    }

    /// <summary>fade ทุกอย่างลงแล้วคืน source · 0 = หยุดทันที</summary>
    public void Stop(float fade)
    {
        Release(_out);
        (_live, _out) = (_out, _live);
        FadeOutAndRelease(_out, fade);
    }

    // ── Loop ──────────────────────────────────────────────────────────────
    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        Step(_live, dt);
        Step(_out,  dt);
        if (_out.track != null && _out.releaseWhenSilent && _out.gain <= 0f)
            Release(_out);

        if (_duck != _duckTarget)
            _duck = Mathf.MoveTowards(_duck, _duckTarget, _duckRate * dt);

        ApplyVolumes();
    }

    static void Step(Deck d, float dt)
    {
        if (d.track == null) return;
        for (int i = 0; i < d.count; i++)
            if (d.cur[i] != d.target[i])
                d.cur[i] = Mathf.MoveTowards(d.cur[i], d.target[i], d.rate[i] * dt);
        if (d.gain != d.gainTarget)
            d.gain = Mathf.MoveTowards(d.gain, d.gainTarget, d.gainRate * dt);
    }

    void ApplyVolumes()
    {
        Apply(_live);
        Apply(_out);
    }

    void Apply(Deck d)
    {
        if (d.track == null) return;
        float k = d.gain * _duck * _outputScale;
        for (int i = 0; i < d.count; i++)
            if (d.src[i] != null) d.src[i].volume = d.cur[i] * k;
    }

    // ── Deck / source bookkeeping ─────────────────────────────────────────
    void FadeOutAndRelease(Deck d, float fade)
    {
        if (d.track == null) return;
        if (fade <= 0f) { Release(d); return; }
        d.gainTarget = 0f;
        d.gainRate   = d.gain / fade;
        d.releaseWhenSilent = true;
    }

    void Release(Deck d)
    {
        for (int i = 0; i < d.count; i++)
        {
            var s = d.src[i];
            if (s == null) continue;
            s.Stop();
            s.clip = null;
            _free.Add(s);
            d.src[i] = null;
        }
        d.track = null;
        d.count = 0;
        d.gain = d.gainTarget = 0f;
        d.releaseWhenSilent = false;
    }

    AudioSource Acquire()
    {
        int last = _free.Count - 1;
        if (last >= 0)
        {
            var s = _free[last];
            _free.RemoveAt(last);
            if (s != null) return s;
        }

        var go = new GameObject($"MusicStem_{_sourceSerial++}");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake           = false;
        src.spatialBlend          = 0f;   // 2D
        src.outputAudioMixerGroup = _group;
        return src;
    }

    void WarnIfInvalid(LayeredTrack track)
    {
        if (!_validated.Add(track)) return;   // เตือนครั้งเดียวต่อ track
        string problem = track.Validate();
        if (problem != null)
            Debug.LogWarning($"[Music] track '{track.name}': {problem}", track);
    }
}
