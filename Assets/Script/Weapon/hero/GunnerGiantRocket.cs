using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gunner E Ability — Giant Rocket
/// ✦ ยิงออกจากตัวผู้เล่น ทิศตรงหาเมาส์ (XZ plane, ไม่ใช้มุมกล้อง)
/// ✦ Range = ระยะระหว่างผู้เล่นกับเมาส์บนพื้น (cap ที่ AbilityData.range)
/// ✦ ระเบิดเมื่อชน Enemy เท่านั้น — หมดระยะ = หายไปไม่ระเบิด
/// ✦ Damage = baseDamage × (1 + missingHP% × 2.0)
/// </summary>
public class GunnerGiantRocket : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    public Key activateKey = Key.E;

    [Header("Giant Rocket Config")]
    public float rocketSpeed        = 18f;
    public float explosionRadius    = 6f;
    public float maxBonusMultiplier = 2f;

    // ── Cooldown ──────────────────────────────────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey      => "E";   // Giant Rocket ของ Gunner อยู่ E เสมอ
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => false;
    public float  ActiveRemaining => 0f;
    public float  ActiveMax       => 0f;

    // ── Events ────────────────────────────────────────────────────────────
    public static event System.Action<GunnerGiantRocket, float> OnCooldownChanged;
    public static event System.Action<GunnerGiantRocket>        OnFired;

    void Update()
    {
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown) return;

        if (Keyboard.current != null && Keyboard.current[activateKey].wasPressedThisFrame)
            Fire();
    }

    void Fire()
    {
        var ld = data.GetLevelData(currentLevel);

        float cd = ld.cooldown * (manager.statManager != null
            ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnFired?.Invoke(this);

        float maxRange     = ld.range * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);
        float radius       = explosionRadius * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);
        float baseDmg      = RollDamage(ld.damage, out bool _);

        Vector3 playerPos  = transform.position;
        Vector3 spawnPos   = playerPos + Vector3.up * 0.5f;

        // ทิศและระยะจากผู้เล่น → เมาส์บนพื้น (ไม่ผ่านมุมกล้อง)
        Vector3 mouseGround = GetMouseOnGround(playerPos.y);
        Vector3 toMouse     = new Vector3(mouseGround.x - playerPos.x, 0f, mouseGround.z - playerPos.z);

        // range = ระยะผู้เล่น–เมาส์ cap ที่ maxRange
        float dynamicRange  = Mathf.Clamp(toMouse.magnitude, 0.5f, maxRange);
        Vector3 direction   = toMouse.sqrMagnitude > 0.001f ? toMouse.normalized : transform.forward;

        SpawnGiantRocket(spawnPos, direction, baseDmg, rocketSpeed, dynamicRange, radius);
        Debug.Log($"[GiantRocket] FIRED dir={direction:F2} range={dynamicRange:F1} dmg={baseDmg:F0}");
    }

    // ── หาจุดบนพื้น Y=groundY ที่เมาส์ชี้ ────────────────────────────────
    // ใช้ Plane แนวนอนที่ Y=groundY — ไม่พึ่งมุมหรือทิศกล้อง
    Vector3 GetMouseOnGround(float groundY)
    {
        var mouse = Mouse.current;
        if (mouse != null && Camera.main != null)
        {
            Ray ray   = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (plane.Raycast(ray, out float dist))
                return ray.GetPoint(dist);
        }
        // Fallback: 10 หน่วยไปข้างหน้า
        Vector3 fwd = transform.forward; fwd.y = 0f;
        return transform.position + (fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward) * 10f;
    }
}
