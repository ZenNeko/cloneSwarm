using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player damage feedback UI — red vignette flash เมื่อ owner โดนดาเมจ
///
/// Setup:
///   1. วาง full-screen Image สีแดงใต้ HUD Canvas (alpha=0)
///   2. ติด DamageFeedbackUI script ที่ Canvas root
///   3. assign vignetteImage reference
///
/// Usage:
///   DamageFeedbackUI.Instance?.ShowFlash(intensity: 0.4f);
///   intensity 0-1 — auto-clamp ไว้ที่ alpha สูงสุด maxAlpha
/// </summary>
public class DamageFeedbackUI : MonoBehaviour
{
    public static DamageFeedbackUI Instance { get; private set; }

    [Header("Refs")]
    [Tooltip("full-screen Image สีแดง — alpha จะถูก animate")]
    public Image vignetteImage;

    [Header("Settings")]
    [Tooltip("alpha สูงสุดที่ flash จะขึ้น (0-1)")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.5f;
    [Tooltip("ระยะเวลา fade in (วินาที)")]
    public float fadeInDuration  = 0.05f;
    [Tooltip("ระยะเวลา fade out (วินาที)")]
    public float fadeOutDuration = 0.4f;

    private Coroutine _activeFlash;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (vignetteImage != null)
        {
            var c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>เรียกตอน owner ของ player object โดนดาเมจ</summary>
    public void ShowFlash(float intensity = 1f)
    {
        if (vignetteImage == null) return;

        if (_activeFlash != null) StopCoroutine(_activeFlash);
        _activeFlash = StartCoroutine(FlashCoroutine(Mathf.Clamp01(intensity)));
    }

    IEnumerator FlashCoroutine(float intensity)
    {
        float targetA = maxAlpha * intensity;
        Color c = vignetteImage.color;

        // Fade in
        float t = 0f;
        float startA = c.a;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(startA, targetA, t / fadeInDuration);
            vignetteImage.color = c;
            yield return null;
        }
        c.a = targetA;
        vignetteImage.color = c;

        // Fade out
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(targetA, 0f, t / fadeOutDuration);
            vignetteImage.color = c;
            yield return null;
        }
        c.a = 0f;
        vignetteImage.color = c;

        _activeFlash = null;
    }
}
