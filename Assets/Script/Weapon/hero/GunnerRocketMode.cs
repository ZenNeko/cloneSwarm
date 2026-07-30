using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gunner Q Ability — Rocket Mode
/// กด Q → เปิด Rocket Mode เป็นเวลา duration (ค่าจาก AbilityData.levels[n].duration)
/// ขณะ active: GunnerWeapon ยิง StickyRocket แทน normal projectile
/// หลังหมดเวลา → cooldown ก่อนใช้ได้อีก
///
/// AbilityData แนะนำ:
///   Lv1: cooldown=20s, duration=5s
///   Lv2: cooldown=18s, duration=6s
///   Lv3: cooldown=16s, duration=7s
/// </summary>
public class GunnerRocketMode : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    public Key activateKey = Key.Q;

    // ── State ─────────────────────────────────────────────────────────────
    public bool  IsRocketModeActive { get; private set; }
    public float ModeRemaining      { get; private set; }
    public float ModeMax            { get; private set; }   // duration เต็ม — ใช้โดย HUD

    public bool  IsOnCooldown       { get; private set; }
    public float CooldownRemaining  { get; private set; }
    public float CooldownMax        { get; private set; }

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey      => "Q";   // Rocket Mode ของ Gunner อยู่ Q เสมอ
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => IsRocketModeActive;
    public float  ActiveRemaining => ModeRemaining;
    public float  ActiveMax       => ModeMax;

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<GunnerRocketMode, bool>  OnRocketModeStateChanged;
    public static event System.Action<GunnerRocketMode, float> OnCooldownChanged;

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        // Rocket Mode countdown
        if (IsRocketModeActive)
        {
            ModeRemaining = Mathf.Max(0f, ModeRemaining - Time.deltaTime);
            if (ModeRemaining <= 0f) EndRocketMode();
        }

        // Cooldown countdown
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        // Input — Owner only
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown || IsRocketModeActive) return;
        if (GamePause.LocalInputSuspended) return;   // host เปิดเมนู pause ใน multiplayer

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[activateKey].wasPressedThisFrame)
            Activate();
    }

    // ── Activate ──────────────────────────────────────────────────────────
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        float durationMult = manager.statManager != null
            ? manager.statManager.GetDurationMultiplier() : 1f;
        float duration     = ld.duration * durationMult;

        IsRocketModeActive = true;
        ModeMax            = duration;
        ModeRemaining      = duration;

        OnRocketModeStateChanged?.Invoke(this, true);
        Debug.Log($"[GunnerRocketMode] 🚀 ROCKET MODE ACTIVE — {duration:F1}s");
    }

    // ── End Mode ──────────────────────────────────────────────────────────
    void EndRocketMode()
    {
        IsRocketModeActive = false;
        OnRocketModeStateChanged?.Invoke(this, false);

        // Cooldown เริ่มหลัง mode หมด
        var   ld = data.GetLevelData(currentLevel);
        float cd = ld.cooldown * (manager.statManager != null
            ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        Debug.Log($"[GunnerRocketMode] Rocket Mode ended — cooldown {cd:F0}s");
    }
}
