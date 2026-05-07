using UnityEngine;

/// <summary>
/// Auto-play BGM ของ scene ผ่าน SoundManager
/// — วาง component บน GameObject ใน scene นั้นๆ + assign clip ใน Inspector
/// — SoundManager เก็บ music source ไว้ DontDestroyOnLoad → cross-scene continuous play
///
/// ใช้กับ:
///   • MenuScene  → BGM_Menu
///   • SampleScene → BGM_Game
/// </summary>
public class SceneBGMPlayer : MonoBehaviour
{
    [Tooltip("BGM ของ scene นี้ — assign AudioClip ใน Inspector")]
    public AudioClip bgm;

    [Tooltip("Loop ตลอดทั้ง scene (true) หรือเล่นครั้งเดียว (false)")]
    public bool loop = true;

    [Tooltip("delay ก่อน start (วินาที) — เผื่อ scene transition fade")]
    public float startDelay = 0f;

    void Start()
    {
        if (bgm == null)
        {
            Debug.LogWarning("[SceneBGMPlayer] bgm not assigned");
            return;
        }

        if (startDelay > 0f)
            Invoke(nameof(PlayBgm), startDelay);
        else
            PlayBgm();
    }

    void PlayBgm()
    {
        SoundManager.Instance.PlayMusic(bgm, loop);
    }
}
