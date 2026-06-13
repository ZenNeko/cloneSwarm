using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hunter R (Ultimate) â€” Funnel Storm
/// à¸à¸” R â†’ Starting weapon (LaserWeapon) à¸¢à¸´à¸‡à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™ (tempCooldownMult)
///         + spawn 5 Funnel à¸—à¸µà¹ˆà¹‚à¸„à¸ˆà¸£à¸£à¸­à¸šà¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™ à¸¢à¸´à¸‡ laser à¸«à¸² enemy à¸—à¸µà¹ˆà¹ƒà¸à¸¥à¹‰à¸—à¸µà¹ˆà¸ªà¸¸à¸”
///
/// AbilityData à¹à¸™à¸°à¸™à¸³:
///   Lv1: cooldown=60s, duration=12s, damage=80 (laser per funnel), range=20
/// </summary>
public class HunterUltimate : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    public Key activateKey = Key.R;

    [Header("Ultimate Config")]
    public int   funnelCount         = 5;
    public float funnelOrbitRadius   = 5f;
    public float funnelLaserCooldown = 0.6f;
    public float funnelAttackRange   = 20f;
    public float laserCooldownMult   = 0.35f;

    // â”€â”€ IHUDAbility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string HUDSlotKey      => "E";   // Ultimate à¸‚à¸­à¸‡ Hunter à¸­à¸¢à¸¹à¹ˆ E à¹€à¸ªà¸¡à¸­
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => IsActive;
    // ActiveRemaining / ActiveMax à¹ƒà¸Šà¹‰à¸£à¹ˆà¸§à¸¡à¸à¸±à¸š property à¸”à¹‰à¸²à¸™à¸¥à¹ˆà¸²à¸‡ âœ“

    // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool  IsActive          { get; private set; }
    public float ActiveRemaining   { get; private set; }
    public float ActiveMax         { get; private set; }

    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // â”€â”€ Events (UI à¸Ÿà¸±à¸‡) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static event System.Action<HunterUltimate, bool>  OnUltStateChanged;
    public static event System.Action<HunterUltimate, float> OnCooldownChanged;

    private LaserWeapon cachedLaser;

    // â”€â”€ Update â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Update()
    {
        if (IsActive)
        {
            ActiveRemaining = Mathf.Max(0f, ActiveRemaining - Time.deltaTime);
            if (ActiveRemaining <= 0f) EndUltimate();
        }

        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown || IsActive) return;

        if (Keyboard.current != null && Keyboard.current[activateKey].wasPressedThisFrame)
            Activate();
    }

    // â”€â”€ Activate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        float durationMult = manager.statManager != null
            ? manager.statManager.GetDurationMultiplier() : 1f;
        float duration = ld.duration * durationMult;

        IsActive        = true;
        ActiveMax       = duration;
        ActiveRemaining = duration;

        var laser = GetLaser();
        if (laser != null) laser.tempCooldownMult = laserCooldownMult;

        float funnelDmg   = ld.damage * (manager.statManager != null ? manager.statManager.GetPowerMultiplier() : 1f);
        float attackRange = funnelAttackRange * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);
        // beamCount: base 1 + bonus à¸ˆà¸²à¸ ProjectileCount stat (à¹€à¸«à¸¡à¸·à¸­à¸™ LaserWeapon)
        int   beamCount   = 1 + (manager.statManager != null ? manager.statManager.GetBonusProjectileCount() : 0);

        SpawnFunnels(
            transform.position, funnelCount, funnelOrbitRadius,
            funnelDmg, funnelLaserCooldown, attackRange, duration, manager.OwnerClientId,
            beamCount);

        OnUltStateChanged?.Invoke(this, true);
        Debug.Log($"[HunterUlt] âœ¨ ULTIMATE ACTIVE â€” {duration:F1}s, {funnelCount} funnels");
    }

    // â”€â”€ End â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void EndUltimate()
    {
        IsActive = false;

        var laser = GetLaser();
        if (laser != null) laser.tempCooldownMult = 1f;

        var   ld = data.GetLevelData(currentLevel);
        float cd = ld.cooldown * (manager.statManager != null ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnUltStateChanged?.Invoke(this, false);
        Debug.Log("[HunterUlt] Ultimate ended â€” cooldown started");
    }

    LaserWeapon GetLaser()
    {
        if (cachedLaser != null) return cachedLaser;
        cachedLaser = manager.GetComponentInChildren<LaserWeapon>();
        return cachedLaser;
    }
}
