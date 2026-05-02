using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Centralized sound system — singleton ที่ดูแล:
///   • SFX pool (3D positional, ไม่ต้องสร้าง GameObject ทุกครั้ง)
///   • Music source (loop background)
///   • Volume control (Master / Music / SFX) → บันทึกใน PlayerPrefs
///   • AudioMixer integration (optional — assign ใน Inspector ของ SoundManager prefab)
///
/// **Auto-singleton** — ถ้าไม่มี GameObject ในซีน จะสร้างเองเมื่อมีคนเรียก Instance ครั้งแรก
/// (ในโหมดนี้ mixer = null → ใช้ per-call volume scaling แทน)
///
/// **Setup ที่แนะนำ (สำหรับใช้ AudioMixer):**
///   1. สร้าง AudioMixer asset ที่มี groups: Master, Music, SFX
///   2. Expose volume parameters ชื่อ "MasterVol", "MusicVol", "SfxVol"
///   3. สร้าง prefab SoundManager → assign mixer + child AudioSource สำหรับ music
///   4. วางใน bootstrap scene
///
/// **API:**
///   SoundManager.Instance.PlaySfx(clip, position, volume, pitchVariance);
///   SoundManager.Instance.PlaySfx2D(clip, volume);
///   SoundManager.Instance.PlayMusic(clip, loop);
///   SoundManager.Instance.SetMasterVolume(0.8f);   // 0..1
/// </summary>
public class SoundManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────
    static SoundManager _instance;
    public static SoundManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            // ลองหาในซีนก่อน (ถ้ามี prefab ที่ user วางไว้)
            _instance = FindAnyObjectByType<SoundManager>();
            if (_instance != null) return _instance;

            // ไม่มี → auto-create
            var go = new GameObject("SoundManager (auto)");
            _instance = go.AddComponent<SoundManager>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Mixer (Optional — ถ้ามี จะ control ผ่าน exposed parameters)")]
    [Tooltip("AudioMixer ที่มี exposed parameters: MasterVol, MusicVol, SfxVol")]
    public AudioMixer mixer;
    [Tooltip("AudioMixerGroup สำหรับ Music source")]
    public AudioMixerGroup musicGroup;
    [Tooltip("AudioMixerGroup สำหรับ SFX pool")]
    public AudioMixerGroup sfxGroup;

    [Header("SFX Pool")]
    [Tooltip("จำนวน AudioSource ใน pool — เพิ่มถ้ามี SFX overlap เยอะ")]
    public int sfxPoolSize = 16;

    [Header("Default Volumes (ใช้ตอนยังไม่มีค่าเก็บใน PlayerPrefs)")]
    [Range(0f, 1f)] public float defaultMaster = 1f;
    [Range(0f, 1f)] public float defaultMusic  = 0.7f;
    [Range(0f, 1f)] public float defaultSfx    = 1f;

    // ── PlayerPrefs keys ──────────────────────────────────────────────────
    const string KEY_MASTER = "vol_master";
    const string KEY_MUSIC  = "vol_music";
    const string KEY_SFX    = "vol_sfx";

    // ── Mixer parameter names (must match exposed names ใน AudioMixer) ───
    const string PARAM_MASTER = "MasterVol";
    const string PARAM_MUSIC  = "MusicVol";
    const string PARAM_SFX    = "SfxVol";

    // ── Runtime state ─────────────────────────────────────────────────────
    AudioSource[] sfxPool;
    int           poolIdx;
    AudioSource   musicSource;

    public float MasterVolume { get; private set; }
    public float MusicVolume  { get; private set; }
    public float SfxVolume    { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSfxPool();
        BuildMusicSource();
        LoadVolumes();
    }

    void BuildSfxPool()
    {
        sfxPool = new AudioSource[Mathf.Max(1, sfxPoolSize)];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            var go = new GameObject($"SfxPool_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake          = false;
            src.spatialBlend         = 1f;
            src.outputAudioMixerGroup = sfxGroup;
            sfxPool[i] = src;
        }
    }

    void BuildMusicSource()
    {
        var go = new GameObject("MusicSource");
        go.transform.SetParent(transform);
        musicSource = go.AddComponent<AudioSource>();
        musicSource.playOnAwake          = false;
        musicSource.loop                 = true;
        musicSource.spatialBlend         = 0f;   // 2D
        musicSource.outputAudioMixerGroup = musicGroup;
    }

    // ── Public API: SFX ───────────────────────────────────────────────────
    /// <summary>เล่น SFX 3D positional ที่ pos — ใช้ pool, no GC alloc</summary>
    public void PlaySfx(AudioClip clip, Vector3 pos, float volume = 1f, float pitchVariance = 0f)
    {
        if (clip == null) return;
        var src = sfxPool[poolIdx];
        poolIdx = (poolIdx + 1) % sfxPool.Length;

        src.transform.position = pos;
        src.spatialBlend       = 1f;
        src.clip               = clip;
        src.pitch              = pitchVariance > 0f ? 1f + Random.Range(-pitchVariance, pitchVariance) : 1f;
        src.volume             = volume * GetSfxScale();
        src.Play();
    }

    /// <summary>เล่น SFX 2D (UI clicks, hits, ฯลฯ) — ไม่มี positional</summary>
    public void PlaySfx2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        var src = sfxPool[poolIdx];
        poolIdx = (poolIdx + 1) % sfxPool.Length;

        src.spatialBlend = 0f;
        src.clip         = clip;
        src.pitch        = 1f;
        src.volume       = volume * GetSfxScale();
        src.Play();
    }

    /// <summary>หา random clip จาก array แล้วเล่น (ใช้คู่กับ data.sfxArray)</summary>
    public void PlayRandomSfx(AudioClip[] clips, Vector3 pos, float volume = 1f, float pitchVariance = 0f)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySfx(clips[Random.Range(0, clips.Length)], pos, volume, pitchVariance);
    }

    // ── Public API: Music ─────────────────────────────────────────────────
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip   = clip;
        musicSource.loop   = loop;
        musicSource.volume = GetMusicScale();
        musicSource.Play();
    }

    public void StopMusic() => musicSource.Stop();
    public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

    // ── Public API: Volume ────────────────────────────────────────────────
    public void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); ApplyMixerVolume(PARAM_MASTER, MasterVolume); RescalePlaying(); SaveVolumes(); }
    public void SetMusicVolume (float v) { MusicVolume  = Mathf.Clamp01(v); ApplyMixerVolume(PARAM_MUSIC,  MusicVolume);  RescalePlaying(); SaveVolumes(); }
    public void SetSfxVolume   (float v) { SfxVolume    = Mathf.Clamp01(v); ApplyMixerVolume(PARAM_SFX,    SfxVolume);    SaveVolumes(); }

    // ── Internals ─────────────────────────────────────────────────────────
    /// <summary>Linear 0..1 → dB (-80..0). Mixer ใช้ dB ไม่ใช่ linear</summary>
    void ApplyMixerVolume(string param, float v01)
    {
        if (mixer == null) return;
        float db = v01 > 0.0001f ? Mathf.Log10(v01) * 20f : -80f;
        mixer.SetFloat(param, db);
    }

    /// <summary>เมื่อไม่มี mixer ต้องคูณ volume ตอน Play — return scale factor</summary>
    float GetSfxScale()   => mixer == null ? SfxVolume   * MasterVolume : 1f;
    float GetMusicScale() => mixer == null ? MusicVolume * MasterVolume : 1f;

    /// <summary>เมื่อไม่มี mixer และเพลงเล่นอยู่ ต้อง update volume ทันที</summary>
    void RescalePlaying()
    {
        if (mixer != null) return;
        if (musicSource != null && musicSource.isPlaying)
            musicSource.volume = GetMusicScale();
    }

    void SaveVolumes()
    {
        PlayerPrefs.SetFloat(KEY_MASTER, MasterVolume);
        PlayerPrefs.SetFloat(KEY_MUSIC,  MusicVolume);
        PlayerPrefs.SetFloat(KEY_SFX,    SfxVolume);
        PlayerPrefs.Save();
    }

    void LoadVolumes()
    {
        SetMasterVolume(PlayerPrefs.GetFloat(KEY_MASTER, defaultMaster));
        SetMusicVolume (PlayerPrefs.GetFloat(KEY_MUSIC,  defaultMusic));
        SetSfxVolume   (PlayerPrefs.GetFloat(KEY_SFX,    defaultSfx));
    }
}
