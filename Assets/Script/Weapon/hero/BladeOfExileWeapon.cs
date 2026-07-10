using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blade of the Exile — Riven's E ability  (AbilityBase — ไม่ใช่ WeaponBase)
/// กด E → เปิด Exile Mode
///   • +50% move speed (tempMoveSpeedBonus)
///   • Charge rate ×2 ผ่าน ChargeManager.IsExileActive
///   • ValorWeapon จะยิง Wind Slash เพิ่มเมื่อ active
/// หลังหมดเวลา → cooldown ก่อนใช้ได้อีก
///
/// AbilityData.levels[n].cooldown = cooldown หลัง exile หมด
/// AbilityData.levels[n].duration = exile duration (ก่อน scale)
/// </summary>
public class BladeOfExileWeapon : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    [Tooltip("ปุ่มที่กดเพื่อใช้สกิล")]
    public Key activateKey = Key.E;

    [Header("Exile Stats")]
    [Tooltip("move speed bonus ขณะ exile active (+0.5 = +50%)")]
    public float moveSpeedBonus = 0.5f;

    // ── State ──────────────────────────────────────────────────────────────
    public bool  IsOnCooldown        { get; private set; }
    public float CooldownRemaining   { get; private set; }
    public float CooldownMax         { get; private set; }
    public bool  IsExileActive       { get; private set; }
    public float ExileRemaining      { get; private set; }
    /// <summary>Exile duration จริง (หลัง scale ด้วย Duration stat) — ใช้โดย HUD</summary>
    public float ActiveExileDuration { get; private set; }

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<BladeOfExileWeapon, float> OnCooldownChanged;
    public static event System.Action<BladeOfExileWeapon, bool>  OnExileStateChanged;

    private ChargeManager chargeManager;

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey      => "E";   // Blade of Exile ของ Riven อยู่ E เสมอ
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => IsExileActive;
    public float  ActiveRemaining => ExileRemaining;
    public float  ActiveMax       => ActiveExileDuration;

    // ── Init ──────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    // ── Update — input + cooldown tick ────────────────────────────────────
    void Update()
    {
        // Exile countdown (ทุก client — เพื่อ UI)
        if (IsExileActive)
        {
            ExileRemaining = Mathf.Max(0f, ExileRemaining - Time.deltaTime);
            if (ExileRemaining <= 0f) EndExile();
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
        if (IsOnCooldown || IsExileActive) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[activateKey].wasPressedThisFrame)
            Activate();
    }

    // ── Activate ──────────────────────────────────────────────────────────
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        // duration scale ตาม Duration stat
        float durationMult   = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        ActiveExileDuration  = ld.duration * durationMult;
        IsExileActive        = true;
        ExileRemaining       = ActiveExileDuration;

        if (manager.playerMove != null)
            manager.playerMove.tempMoveSpeedBonus += moveSpeedBonus;
        if (chargeManager != null)
            chargeManager.IsExileActive = true;

        OnExileStateChanged?.Invoke(this, true);
        Debug.Log($"[Blade of Exile] ⚔️ EXILE ACTIVE — {ActiveExileDuration:F1}s");
    }

    // ── End Exile ─────────────────────────────────────────────────────────
    void EndExile()
    {
        IsExileActive = false;

        if (manager.playerMove != null)
            manager.playerMove.tempMoveSpeedBonus -= moveSpeedBonus;
        if (chargeManager != null)
            chargeManager.IsExileActive = false;

        OnExileStateChanged?.Invoke(this, false);

        // Cooldown เริ่มหลัง exile หมด — scale ตาม Ability Haste
        var   ld = data.GetLevelData(currentLevel);
        float cd = ld.cooldown * (manager.statManager != null ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        Debug.Log($"[Blade of Exile] Exile ended — cooldown {cd:F0}s");
    }

    void OnDestroy()
    {
        if (IsExileActive)
        {
            if (manager != null && manager.playerMove != null)
                manager.playerMove.tempMoveSpeedBonus -= moveSpeedBonus;
            if (chargeManager != null)
                chargeManager.IsExileActive = false;
            IsExileActive = false;
        }
    }
}
