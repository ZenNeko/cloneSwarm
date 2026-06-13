using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gunner E Ability â€” Giant Rocket
/// âœ¦ à¸¢à¸´à¸‡à¸­à¸­à¸à¸ˆà¸²à¸à¸•à¸±à¸§à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™ à¸—à¸´à¸¨à¸•à¸£à¸‡à¸«à¸²à¹€à¸¡à¸²à¸ªà¹Œ (XZ plane, à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰à¸¡à¸¸à¸¡à¸à¸¥à¹‰à¸­à¸‡)
/// âœ¦ Range = à¸£à¸°à¸¢à¸°à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™à¸à¸±à¸šà¹€à¸¡à¸²à¸ªà¹Œà¸šà¸™à¸žà¸·à¹‰à¸™ (cap à¸—à¸µà¹ˆ AbilityData.range)
/// âœ¦ à¸£à¸°à¹€à¸šà¸´à¸”à¹€à¸¡à¸·à¹ˆà¸­à¸Šà¸™ Enemy à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™ â€” à¸«à¸¡à¸”à¸£à¸°à¸¢à¸° = à¸«à¸²à¸¢à¹„à¸›à¹„à¸¡à¹ˆà¸£à¸°à¹€à¸šà¸´à¸”
/// âœ¦ Damage = baseDamage Ã— (1 + missingHP% Ã— 2.0)
/// </summary>
public class GunnerGiantRocket : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    public Key activateKey = Key.E;

    [Header("Giant Rocket Config")]
    public float rocketSpeed        = 18f;
    public float explosionRadius    = 6f;
    public float maxBonusMultiplier = 2f;

    // â”€â”€ Cooldown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // â”€â”€ IHUDAbility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string HUDSlotKey      => "E";   // Giant Rocket à¸‚à¸­à¸‡ Gunner à¸­à¸¢à¸¹à¹ˆ E à¹€à¸ªà¸¡à¸­
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => false;
    public float  ActiveRemaining => 0f;
    public float  ActiveMax       => 0f;

    // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // à¸—à¸´à¸¨à¹à¸¥à¸°à¸£à¸°à¸¢à¸°à¸ˆà¸²à¸à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™ â†’ à¹€à¸¡à¸²à¸ªà¹Œà¸šà¸™à¸žà¸·à¹‰à¸™ (à¹„à¸¡à¹ˆà¸œà¹ˆà¸²à¸™à¸¡à¸¸à¸¡à¸à¸¥à¹‰à¸­à¸‡)
        Vector3 mouseGround = GetMouseOnGround(playerPos.y);
        Vector3 toMouse     = new Vector3(mouseGround.x - playerPos.x, 0f, mouseGround.z - playerPos.z);

        // range = à¸£à¸°à¸¢à¸°à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™â€“à¹€à¸¡à¸²à¸ªà¹Œ cap à¸—à¸µà¹ˆ maxRange
        float dynamicRange  = Mathf.Clamp(toMouse.magnitude, 0.5f, maxRange);
        Vector3 direction   = toMouse.sqrMagnitude > 0.001f ? toMouse.normalized : transform.forward;

        SpawnGiantRocket(spawnPos, direction, baseDmg, rocketSpeed, dynamicRange, radius);
        Debug.Log($"[GiantRocket] FIRED dir={direction:F2} range={dynamicRange:F1} dmg={baseDmg:F0}");
    }

    // â”€â”€ à¸«à¸²à¸ˆà¸¸à¸”à¸šà¸™à¸žà¸·à¹‰à¸™ Y=groundY à¸—à¸µà¹ˆà¹€à¸¡à¸²à¸ªà¹Œà¸Šà¸µà¹‰ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // à¹ƒà¸Šà¹‰ Plane à¹à¸™à¸§à¸™à¸­à¸™à¸—à¸µà¹ˆ Y=groundY â€” à¹„à¸¡à¹ˆà¸žà¸¶à¹ˆà¸‡à¸¡à¸¸à¸¡à¸«à¸£à¸·à¸­à¸—à¸´à¸¨à¸à¸¥à¹‰à¸­à¸‡
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
        // Fallback: 10 à¸«à¸™à¹ˆà¸§à¸¢à¹„à¸›à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸²
        Vector3 fwd = transform.forward; fwd.y = 0f;
        return transform.position + (fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward) * 10f;
    }
}
