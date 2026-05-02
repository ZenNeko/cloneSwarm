using System.Collections;
using UnityEngine;

/// <summary>
/// Camera shake singleton — เรียกจากที่ไหนก็ได้
///
/// Setup:
///   วาง CameraShake GameObject ใน scene (ติด script นี้)
///   จะหา Camera.main อัตโนมัติ
///
/// Usage:
///   CameraShake.Instance?.Shake(duration: 0.3f, magnitude: 0.4f);
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("ลด magnitude ให้ smooth — 0=hard cutoff, 1=full magnitude ตลอดเวลา")]
    [Range(0f, 1f)]
    public float dampening = 0.85f;

    private Transform _cam;
    private Vector3   _originalLocalPos;
    private Coroutine _activeShake;
    private float     _seedX;
    private float     _seedY;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _seedX = Random.value * 100f;
        _seedY = Random.value * 100f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>เรียกเพื่อทำ camera shake — ถ้ามี shake อยู่จะ overwrite</summary>
    public void Shake(float duration, float magnitude)
    {
        if (_cam == null) AttachCamera();
        if (_cam == null) return;

        if (_activeShake != null) StopCoroutine(_activeShake);
        _activeShake = StartCoroutine(ShakeCoroutine(duration, magnitude));
    }

    void AttachCamera()
    {
        var camComp = Camera.main;
        if (camComp == null) return;
        _cam = camComp.transform;
        _originalLocalPos = _cam.localPosition;
    }

    IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        float currentMag = magnitude;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Perlin noise — smoother กว่า Random.value
            float x = (Mathf.PerlinNoise(_seedX + Time.time * 25f, 0f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0f, _seedY + Time.time * 25f) - 0.5f) * 2f;

            // dampening: magnitude ลดลงตามเวลา
            currentMag = magnitude * Mathf.Pow(dampening, t * 10f);

            _cam.localPosition = _originalLocalPos + new Vector3(x, y, 0f) * currentMag;
            elapsed += Time.deltaTime;
            yield return null;
        }

        _cam.localPosition = _originalLocalPos;
        _activeShake = null;
    }
}
