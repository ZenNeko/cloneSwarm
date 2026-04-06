using UnityEngine;

/// <summary>
/// Visual animation สำหรับ ObjectiveOrb
/// — ลอย (bob) + หมุน + pulse scale + แสงจุด
///
/// วิธีใช้: แนบ component นี้บน ObjectiveOrb prefab
/// (client-side เท่านั้น ไม่มี network sync)
/// </summary>
public class OrbVisual : MonoBehaviour
{
    [Header("Float")]
    public float floatAmplitude = 0.30f;
    public float floatSpeed     = 1.80f;

    [Header("Spin")]
    public float spinSpeed = 80f;           // องศา/วินาที

    [Header("Pulse Scale")]
    public float pulseAmplitude = 0.07f;
    public float pulseSpeed     = 3.20f;

    [Header("Glow Light")]
    public Color  lightColor     = new(1.00f, 0.80f, 0.15f);
    public float  lightIntensity = 2.50f;
    public float  lightRange     = 6.00f;

    [Header("Orb Color")]
    public Color orbColor = new(1.00f, 0.85f, 0.15f);

    // ── Internal ───────────────────────────────────────────────────────────
    Vector3  baseLocalPos;
    float    t;
    Light    orbLight;
    Renderer rend;

    void Start()
    {
        baseLocalPos = transform.localPosition;

        // ค้นหา renderer บน child (mesh)
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // สร้าง material instance เพื่อไม่ shared กับ prefab
            rend.material = new Material(rend.sharedMaterial ?? new Material(Shader.Find("Standard")));
            rend.material.color = orbColor;

            // Emission เพื่อให้ดูเรืองแสง
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", orbColor * 0.6f);
        }

        // Ambient light
        var lightGO = new GameObject("OrbLight");
        lightGO.transform.SetParent(transform, false);
        orbLight           = lightGO.AddComponent<Light>();
        orbLight.type      = LightType.Point;
        orbLight.color     = lightColor;
        orbLight.intensity = lightIntensity;
        orbLight.range     = lightRange;
        orbLight.shadows   = LightShadows.None;
    }

    void Update()
    {
        t += Time.deltaTime;

        // Float
        float floatOffset = Mathf.Sin(t * floatSpeed) * floatAmplitude;
        transform.localPosition = baseLocalPos + Vector3.up * floatOffset;

        // Spin
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Pulse scale
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmplitude;
        transform.localScale = Vector3.one * pulse;

        // Light flicker
        if (orbLight != null)
            orbLight.intensity = lightIntensity + Mathf.Sin(t * pulseSpeed * 1.3f) * 0.7f;
    }

    /// <summary>
    /// เรียกจาก ObjectiveOrb.cs ก่อน despawn เพื่อ play pickup VFX
    /// </summary>
    public void PlayCollectEffect()
    {
        VFXFactory.Play(VFXType.OrbPickup, transform.position);
    }
}
