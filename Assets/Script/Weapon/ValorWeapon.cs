using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Valor — Riven's Q ability  (AbilityBase — ไม่ใช่ WeaponBase)
/// กด Q → Dash หาศัตรูที่ใกล้ที่สุด + AoE damage at landing
/// เมื่อ Blade of Exile active → Wind Slash radial เพิ่มเติม
///
/// AbilityData (cooldown, damage, range) อยู่ใน AbilityData asset
/// </summary>
public class ValorWeapon : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    [Tooltip("ปุ่มที่กดเพื่อใช้สกิล")]
    public Key activateKey = Key.Q;

    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.12f;

    [Header("Wind Slash (during Exile)")]
    public int windSlashCount = 4;

    // ── Cooldown state ────────────────────────────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<ValorWeapon, float> OnCooldownChanged;
    public static event System.Action<ValorWeapon>        OnActivated;

    private ChargeManager chargeManager;
    private bool          isDashing;

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey       => "Q";   // Valor ของ Riven อยู่ Q เสมอ
    public string HUDKeyLabel      => activateKey.ToString();
    public bool   IsActiveMode     => false;
    public float  ActiveRemaining  => 0f;
    public float  ActiveMax        => 0f;

    // ── Init ──────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    // ── Update — input + cooldown tick ────────────────────────────────────
    void Update()
    {
        // Cooldown countdown (ทุก client — เพื่อ UI sync)
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

    // ── Activate ──────────────────────────────────────────────────────────
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        // Cooldown scale ตาม Ability Haste
        float cd = ld.cooldown;
        if (manager.statManager != null) cd *= manager.statManager.GetCooldownMultiplier();
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnActivated?.Invoke(this);

        // ทิศ dash = ทิศที่ผู้เล่นกด input อยู่
        // fallback → ทิศหาศัตรูที่ใกล้สุด → transform.forward
        Vector3 dir = manager.playerMove?.MoveDirection ?? Vector3.zero;
        if (dir.sqrMagnitude < 0.001f)
        {
            Transform nearest = FindNearestEnemy(ld.range * 1.5f);
            dir = nearest != null
                ? (nearest.position - transform.position)
                : transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
            dir = dir.normalized;
        }

        float damage = RollDamage(ld.damage, out bool isCrit);
        StartCoroutine(DashAndBlast(dir, ld.range, damage, isCrit));
    }

    // ── Dash coroutine ────────────────────────────────────────────────────
    IEnumerator DashAndBlast(Vector3 dir, float radius, float damage, bool isCrit)
    {
        isDashing = true;

        var pm = manager.playerMove;
        var rb = pm?.GetComponent<Rigidbody>();
        if (rb != null && pm != null)
        {
            pm.isDashing      = true;
            rb.linearVelocity  = Vector3.zero;

            Vector3 startPos  = rb.position;
            Vector3 endPos    = startPos + dir * dashDistance;
            float   elapsed   = 0f;

            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                float t  = Mathf.SmoothStep(0f, 1f, elapsed / dashDuration);
                rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
                yield return new WaitForFixedUpdate();
            }

            rb.MovePosition(endPos);
            rb.linearVelocity  = Vector3.zero;
            pm.isDashing = false;
        }
        else yield return null;

        FireMelee(transform.position, radius, damage, isCrit);
        string baseHit = isCrit ? "CritHitEffect" : "HitEffect";
        manager.BroadcastVfxTypeServerRpc(transform.position, baseHit);

        // Wind Slash — เฉพาะตอน Blade of Exile active
        if (chargeManager != null && chargeManager.IsExileActive)
        {
            float windDmg  = damage * 0.8f;
            float maxRange = data.GetLevelData(currentLevel).range * 2f;
            for (int i = 0; i < windSlashCount; i++)
            {
                float   angle    = i * (360f / windSlashCount);
                Vector3 slashDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                FireRaycast(transform.position, slashDir, windDmg, maxRange,
                                             isCrit: isCrit);
            }
        }

        isDashing = false;
    }
}
