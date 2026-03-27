using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Valor — Riven's Q ability (Player-activated)
/// กด Q → Dash หาศัตรูที่ใกล้ที่สุด + AoE damage at landing
/// เมื่อ Blade of Exile active → Wind Slash radial เพิ่มเติม
/// </summary>
public class ValorWeapon : WeaponBase
{
    [Header("Input Key")]
    [Tooltip("ปุ่มที่กดเพื่อใช้สกิล")]
    public Key activateKey = Key.Q;

    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.12f;

    [Header("Wind Slash (during Exile)")]
    public int windSlashCount = 4;

    // ── Cooldown (manual — ไม่ใช้ base timer) ────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────
    /// <summary>UI ฟัง — normalized cooldown (0 = ready, 1 = just used)</summary>
    public static event System.Action<ValorWeapon, float> OnCooldownChanged;
    public static event System.Action<ValorWeapon>        OnActivated;

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;
    private bool          isDashing;

    // ── Init ──────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    // ── Override Update — input + cooldown tick ───────────────────────────
    protected override void Update()
    {
        // Cooldown countdown (runs on all clients for UI sync)
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        // Input — Owner only
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown || isDashing) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[activateKey].wasPressedThisFrame)
            Activate();
    }

    // ── Activate (called on key press) ────────────────────────────────────
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        float cd = ld.cooldown;
        if (manager.statManager != null) cd *= manager.statManager.GetCooldownMultiplier();
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnActivated?.Invoke(this);

        Transform target = FindNearestEnemy(ld.range * 1.5f);
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        float damage = RollDamage(ld.damage);
        StartCoroutine(DashAndBlast(dir.normalized, ld.range, damage, ld));
    }

    protected override void OnFire(WeaponLevelData ld) { /* ไม่ใช้ */ }

    // ── Dash coroutine ────────────────────────────────────────────────────
    IEnumerator DashAndBlast(Vector3 dir, float radius, float damage, WeaponLevelData ld)
    {
        isDashing = true;

        var rb = manager.playerMove?.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float speed   = dashDistance / dashDuration;
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                rb.velocity = new Vector3(dir.x * speed, rb.velocity.y, dir.z * speed);
                elapsed    += Time.deltaTime;
                yield return null;
            }
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }
        else yield return null;

        // AoE blast at landing
        manager.FireMeleeServerRpc(transform.position, radius, damage);

        // Wind Slash — เฉพาะตอน Blade of Exile active
        if (chargeManager != null && chargeManager.IsExileActive)
        {
            float windDmg = damage * 0.8f;
            for (int i = 0; i < windSlashCount; i++)
            {
                float   angle    = i * (360f / windSlashCount);
                Vector3 slashDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                manager.FireRaycastServerRpc(transform.position, slashDir, windDmg, ld.range * 2f);
            }
        }

        isDashing = false;
    }
}
