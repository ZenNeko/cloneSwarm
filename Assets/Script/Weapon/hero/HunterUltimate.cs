using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hunter R (Ultimate) — Funnel Storm
/// กด R → Starting weapon (LaserWeapon) ยิงเร็วขึ้น (tempCooldownMult)
///         + spawn 5 Funnel ที่โคจรรอบผู้เล่น ยิง laser หา enemy ที่ใกล้ที่สุด
///
/// AbilityData แนะนำ:
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

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey      => "E";   // Ultimate ของ Hunter อยู่ E เสมอ
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => IsActive;
    // ActiveRemaining / ActiveMax ใช้ร่วมกับ property ด้านล่าง ✓

    // ── State ─────────────────────────────────────────────────────────────
    public bool  IsActive          { get; private set; }
    public float ActiveRemaining   { get; private set; }
    public float ActiveMax         { get; private set; }

    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<HunterUltimate, bool>  OnUltStateChanged;
    public static event System.Action<HunterUltimate, float> OnCooldownChanged;

    private LaserWeapon cachedLaser;

    // ── Update ────────────────────────────────────────────────────────────
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
        if (GamePause.LocalInputSuspended) return;   // host เปิดเมนู pause ใน multiplayer

        if (Keyboard.current != null && Keyboard.current[activateKey].wasPressedThisFrame)
            Activate();
    }

    // ── Activate ──────────────────────────────────────────────────────────
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
        // beamCount: base 1 + bonus จาก ProjectileCount stat (เหมือน LaserWeapon)
        int   beamCount   = 1 + (manager.statManager != null ? manager.statManager.GetBonusProjectileCount() : 0);

        SpawnFunnels(
            transform.position, funnelCount, funnelOrbitRadius,
            funnelDmg, funnelLaserCooldown, attackRange, duration, manager.OwnerClientId,
            beamCount);

        OnUltStateChanged?.Invoke(this, true);
        Debug.Log($"[HunterUlt] ✨ ULTIMATE ACTIVE — {duration:F1}s, {funnelCount} funnels");
    }

    // ── End ───────────────────────────────────────────────────────────────
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
        Debug.Log("[HunterUlt] Ultimate ended — cooldown started");
    }

    LaserWeapon GetLaser()
    {
        if (cachedLaser != null) return cachedLaser;
        cachedLaser = manager.GetComponentInChildren<LaserWeapon>();
        return cachedLaser;
    }

    void OnDestroy()
    {
        if (IsActive)
        {
            var laser = GetLaser();
            if (laser != null) laser.tempCooldownMult = 1f;
            IsActive = false;
        }
    }
}
