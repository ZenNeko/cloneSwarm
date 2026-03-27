using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blade of the Exile — Riven's E ability (Player-activated)
/// กด E → เปิด Exile Mode 15 วินาที
///   • +50% move speed (tempMoveSpeedBonus)
///   • Charge rate ×2 ผ่าน ChargeManager.IsExileActive
///   • ValorWeapon จะยิง Wind Slash เพิ่มเมื่อ active
/// หลังหมดเวลา → cooldown ก่อนใช้ได้อีก
/// </summary>
public class BladeOfExileWeapon : WeaponBase
{
    [Header("Input Key")]
    [Tooltip("ปุ่มที่กดเพื่อใช้สกิล")]
    public Key activateKey = Key.E;

    [Header("Exile Stats")]
    public float exileDuration  = 15f;
    public float moveSpeedBonus = 0.5f;    // +50%

    // ── State ──────────────────────────────────────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }
    public bool  IsExileActive     { get; private set; }
    public float ExileRemaining    { get; private set; }

    // ── Events (สำหรับ UI) ────────────────────────────────────────────────
    public static event System.Action<BladeOfExileWeapon, float> OnCooldownChanged;
    public static event System.Action<BladeOfExileWeapon, bool>  OnExileStateChanged;

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;

    // ── Init ──────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    // ── Override Update — input + cooldown tick ───────────────────────────
    protected override void Update()
    {
        // Exile countdown (ทุก client เห็น ExileRemaining สำหรับ UI)
        if (IsExileActive)
        {
            ExileRemaining = Mathf.Max(0f, ExileRemaining - Time.deltaTime);
            if (ExileRemaining <= 0f)
                EndExile();
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
        IsExileActive  = true;
        ExileRemaining = exileDuration;

        if (manager.playerMove != null)
            manager.playerMove.tempMoveSpeedBonus += moveSpeedBonus;
        if (chargeManager != null)
            chargeManager.IsExileActive = true;

        OnExileStateChanged?.Invoke(this, true);
        Debug.Log("[Blade of Exile] ⚔️ EXILE ACTIVE");
    }

    void EndExile()
    {
        IsExileActive = false;

        if (manager.playerMove != null)
            manager.playerMove.tempMoveSpeedBonus -= moveSpeedBonus;
        if (chargeManager != null)
            chargeManager.IsExileActive = false;

        OnExileStateChanged?.Invoke(this, false);

        // CD เริ่มนับหลัง exile หมด
        var   ld = data.GetLevelData(currentLevel);
        float cd = ld.cooldown * (manager.statManager != null ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        Debug.Log($"[Blade of Exile] Exile ended — cooldown {cd:F0}s");
    }

    protected override void OnFire(WeaponLevelData ld) { /* ไม่ใช้ — activated by input */ }
}
