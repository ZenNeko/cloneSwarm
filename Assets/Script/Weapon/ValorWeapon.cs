using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Valor â€” Riven's Q ability  (AbilityBase â€” à¹„à¸¡à¹ˆà¹ƒà¸Šà¹ˆ WeaponBase)
/// à¸à¸” Q â†’ Dash à¸«à¸²à¸¨à¸±à¸•à¸£à¸¹à¸—à¸µà¹ˆà¹ƒà¸à¸¥à¹‰à¸—à¸µà¹ˆà¸ªà¸¸à¸” + AoE damage at landing
/// à¹€à¸¡à¸·à¹ˆà¸­ Blade of Exile active â†’ Wind Slash radial à¹€à¸žà¸´à¹ˆà¸¡à¹€à¸•à¸´à¸¡
///
/// AbilityData (cooldown, damage, range) à¸­à¸¢à¸¹à¹ˆà¹ƒà¸™ AbilityData asset
/// </summary>
public class ValorWeapon : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    [Tooltip("à¸›à¸¸à¹ˆà¸¡à¸—à¸µà¹ˆà¸à¸”à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸Šà¹‰à¸ªà¸à¸´à¸¥")]
    public Key activateKey = Key.Q;

    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.12f;

    [Header("Wind Slash (during Exile)")]
    public int windSlashCount = 4;

    // â”€â”€ Cooldown state â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // â”€â”€ Events (UI à¸Ÿà¸±à¸‡) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static event System.Action<ValorWeapon, float> OnCooldownChanged;
    public static event System.Action<ValorWeapon>        OnActivated;

    private ChargeManager chargeManager;
    private bool          isDashing;

    // â”€â”€ IHUDAbility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string HUDSlotKey       => "Q";   // Valor à¸‚à¸­à¸‡ Riven à¸­à¸¢à¸¹à¹ˆ Q à¹€à¸ªà¸¡à¸­
    public string HUDKeyLabel      => activateKey.ToString();
    public bool   IsActiveMode     => false;
    public float  ActiveRemaining  => 0f;
    public float  ActiveMax        => 0f;

    // â”€â”€ Init â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    // â”€â”€ Update â€” input + cooldown tick â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Update()
    {
        // Cooldown countdown (à¸—à¸¸à¸ client â€” à¹€à¸žà¸·à¹ˆà¸­ UI sync)
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        // Input â€” Owner only
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown || isDashing) return;

        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb[activateKey].wasPressedThisFrame)
            Activate();
    }

    // â”€â”€ Activate â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Activate()
    {
        var ld = data.GetLevelData(currentLevel);

        // Cooldown scale à¸•à¸²à¸¡ Ability Haste
        float cd = ld.cooldown;
        if (manager.statManager != null) cd *= manager.statManager.GetCooldownMultiplier();
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnActivated?.Invoke(this);

        // à¸—à¸´à¸¨ dash = à¸—à¸´à¸¨à¸—à¸µà¹ˆà¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™à¸à¸” input à¸­à¸¢à¸¹à¹ˆ
        // fallback â†’ à¸—à¸´à¸¨à¸«à¸²à¸¨à¸±à¸•à¸£à¸¹à¸—à¸µà¹ˆà¹ƒà¸à¸¥à¹‰à¸ªà¸¸à¸” â†’ transform.forward
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

    // â”€â”€ Dash coroutine â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    IEnumerator DashAndBlast(Vector3 dir, float radius, float damage, bool isCrit)
    {
        isDashing = true;

        var pm = manager.playerMove;
        var rb = pm?.GetComponent<Rigidbody>();
        if (rb != null && pm != null)
        {
            pm.isDashing      = true;
            rb.velocity       = Vector3.zero;

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
            rb.velocity  = Vector3.zero;
            pm.isDashing = false;
        }
        else yield return null;

        // AoE blast at landing
        FireMelee(transform.position, radius, damage);
        string baseHit = isCrit ? "CritHitEffect" : "HitEffect";
        manager.BroadcastVfxTypeServerRpc(transform.position, baseHit);

        // Wind Slash â€” à¹€à¸‰à¸žà¸²à¸°à¸•à¸­à¸™ Blade of Exile active
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
