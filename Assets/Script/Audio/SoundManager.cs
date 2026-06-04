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

    [Header("Default Master Volume (ใช้ตอนยังไม่มีค่าเก็บใน PlayerPrefs)")]
    [Tooltip("ค่าเริ่มต้นของ Master volume — Music และ SFX default ใช้ 1 (เต็ม) เสมอ")]
    [Range(0f, 1f)] public float defaultMaster = 1f;
    [Range(0f, 1f)] public float defaultMusic = 1f;
    [Range(0f, 1f)] public float defaultSfx = 1f;
    

    // Music/SFX default = 1 (full) — ผู้เล่นปรับได้ผ่าน slider 

    // ── PlayerPrefs keys (ต้องตรงกับ MenuManager) ────────────────────────
    const string KEY_MASTER = "Vol_Master";
    const string KEY_MUSIC  = "Vol_Music";
    const string KEY_SFX    = "Vol_SFX";

    // ── Mixer parameter names (ต้องตรงกับ exposed params ใน AudioMixer) ──
    const string PARAM_MASTER = "MasterVolume";
    const string PARAM_MUSIC  = "MusicVolume";
    const string PARAM_SFX    = "SFXVolume";

    // ── Runtime state ─────────────────────────────────────────────────────
    AudioSource[] sfxPool;
    int           poolIdx;
    AudioSource   musicSource;

    // Mixer params ที่ exposed จริง ใน assigned mixer — set ใน Awake (ValidateMixerParams)
    // ถ้า user ไม่ได้ expose ตามชื่อมาตรฐาน → param หายไปจาก set → ApplyMixerVolume skip
    readonly System.Collections.Generic.HashSet<string> _validMixerParams = new();

    public float MasterVolume { get; private set; }
    public float MusicVolume  { get; private set; }
    public float SfxVolume    { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        ValidateMixerParams();   // ตรวจว่า mixer มี exposed params ไหม → กัน warning spam
        ResetToDefaults();   // apply default values ก่อน load (ถ้าไม่มีค่าใน PlayerPrefs)
        BuildSfxPool();
        BuildMusicSource();
        LoadVolumes();
    }

    /// <summary>
    /// ตรวจว่า mixer ที่ assign มี exposed params (MasterVolume, MusicVolume, SFXVolume) ครบไหม
    /// — `GetFloat` คืน true ถ้า exposed name มี (ไม่ log warning เหมือน SetFloat)
    /// → จำเฉพาะที่ใช้ได้ใน `_validMixerParams` → `ApplyMixerVolume` skip params ที่ไม่มี
    /// </summary>
    void ValidateMixerParams()
    {
        _validMixerParams.Clear();
        if (mixer == null) return;

        foreach (string p in new[] { PARAM_MASTER, PARAM_MUSIC, PARAM_SFX })
        {
            if (mixer.GetFloat(p, out _))
                _validMixerParams.Add(p);
        }

        if (_validMixerParams.Count == 0)
        {
            Debug.LogWarning($"[SoundManager] Mixer assigned แต่ไม่มี exposed params " +
                             $"({PARAM_MASTER}/{PARAM_MUSIC}/{PARAM_SFX}) — " +
                             "ใช้ direct volume scaling อย่างเดียว " +
                             "(ถ้าอยากใช้ mixer: เปิด AudioMixer → คลิกขวา param slider → Expose Parameter to script)");
        }
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
    public void SetSfxVolume   (float v) { SfxVolume    = Mathf.Clamp01(v); ApplyMixerVolume(PARAM_SFX,    SfxVolume);    RescalePlaying(); SaveVolumes(); }

    /// <summary>
    /// คืนค่าเสียงเป็น default ที่ตั้งไว้ใน Inspector ของ SoundManager
    /// — เรียกจาก Reset Defaults button ใน Settings/Pause menu
    /// — overwrite PlayerPrefs ด้วย defaults
    /// </summary>
    public void ResetToDefaults()
    {
        SetMasterVolume(defaultMaster);
        SetMusicVolume(defaultMusic);
        SetSfxVolume(defaultSfx);
    }

    // ── Internals ─────────────────────────────────────────────────────────
    // Direct scaling ทำงานเสมอ — ไม่ depend on mixer setup
    // ถ้า mixer + group ครบ AudioMixer ก็ apply เพิ่มขึ้นไป (compound)
    // ถ้า exposed param ชื่อไม่ตรง mixer.SetFloat fail → direct scaling save ไว้

    /// <summary>Linear 0..1 → dB (-80..0). Mixer ใช้ dB ไม่ใช่ linear</summary>
    void ApplyMixerVolume(string param, float v01)
    {
        if (mixer == null) return;
        // Skip params ที่ไม่ได้ expose ใน mixer — กัน "Exposed name does not exist" warning spam
        if (!_validMixerParams.Contains(param)) return;

        float db = v01 > 0.0001f ? Mathf.Log10(v01) * 20f : -80f;
        mixer.SetFloat(param, db);
    }

    /// <summary>คืน scale factor ที่จะคูณกับ AudioSource.volume — ทำงานเสมอ</summary>
    float GetSfxScale()   => SfxVolume   * MasterVolume;
    float GetMusicScale() => MusicVolume * MasterVolume;

    /// <summary>
    /// อัปเดต volume ของ source ที่กำลังเล่นอยู่ — ทำงานเสมอ (direct scaling)
    /// </summary>
    void RescalePlaying()
    {
        // Music: update ทุกครั้ง
        if (musicSource != null && musicSource.isPlaying)
            musicSource.volume = GetMusicScale();

        // SFX pool: update sources ที่ playing (สำหรับ long beam SFX)
        if (sfxPool != null)
        {
            float sfxScale = GetSfxScale();
            foreach (var src in sfxPool)
            {
                if (src == null || !src.isPlaying) continue;
                src.volume = sfxScale;
            }
        }
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

    /// <summary>
    /// ล้าง PlayerPrefs ที่เก็บ volume — ครั้งหน้าจะใช้ default จาก Inspector
    /// คลิกขวาที่ SoundManager component → "Clear Saved Volumes" ใน Editor
    /// </summary>
    [ContextMenu("Clear Saved Volumes (Force Defaults Next Run)")]
    public void ClearSavedVolumes()
    {
        PlayerPrefs.DeleteKey(KEY_MASTER);
        PlayerPrefs.DeleteKey(KEY_MUSIC);
        PlayerPrefs.DeleteKey(KEY_SFX);
        PlayerPrefs.Save();
        Debug.Log("[SoundManager] PlayerPrefs cleared — restart to apply defaults");

        // ถ้ารันอยู่ ก็ apply defaults ทันที
        if (Application.isPlaying) ResetToDefaults();
    }
}
