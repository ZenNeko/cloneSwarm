using System.Collections;
using UnityEngine;

/// <summary>
/// Signature Weapon à¸‚à¸­à¸‡ Riven â€” à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ cooldown timer à¹à¸•à¹ˆà¹ƒà¸Šà¹‰ CHARGE à¸ˆà¸²à¸ ChargeManager
///
/// à¸—à¸¸à¸ cast  : Dash + AoE radial 360Â° à¸£à¸­à¸šà¸•à¸±à¸§
///   cast 1  : radius à¸›à¸à¸•à¸´
///   cast 1 = cast 2 : radius à¹€à¸—à¹ˆà¸²à¸à¸±à¸™
///
/// à¸‚à¸“à¸° AD_BladeOfExile active (à¹€à¸žà¸´à¹ˆà¸¡à¹€à¸•à¸´à¸¡à¸šà¸™ AoE à¸›à¸à¸•à¸´):
///   + à¸¢à¸´à¸‡ Projectile à¹ƒà¸™à¸—à¸´à¸¨ dash
///   + AoE radius Ã—exileAoeMult
///   (Wind Slash à¸–à¸¹à¸à¸–à¸­à¸”à¸­à¸­à¸ â€” à¸£à¸­ design à¹ƒà¸«à¸¡à¹ˆ)
///
/// Super BunnyHop (IsSuper):
///   cast 2 à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™: AoE à¸•à¸µ 2 à¸„à¸£à¸±à¹‰à¸‡ (double hit)
///
/// Runic Blade: à¸¢à¸´à¹ˆà¸‡à¸­à¸¢à¸¹à¹ˆà¹„à¸à¸¥à¸¨à¸±à¸•à¸£à¸¹à¸à¹ˆà¸­à¸™ dash â†’ damage +0â€“15%
/// Shield: 25% à¸‚à¸­à¸‡ damage Ã— à¸ˆà¸³à¸™à¸§à¸™à¸¨à¸±à¸•à¸£à¸¹à¹‚à¸”à¸™
/// </summary>
public class BunnyHopWeapon : WeaponBase
{
    [Header("Dash")]
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("AoE")]
    [Tooltip("Projectile maxRange à¸‚à¸“à¸° Exile = base range Ã— à¸„à¹ˆà¸²à¸™à¸µà¹‰")]
    public float exileProjectileRangeMult = 2f;

    [Header("Runic Blade Passive")]
    [Tooltip("à¸£à¸°à¸¢à¸°à¸ªà¸¹à¸‡à¸ªà¸¸à¸”à¸—à¸µà¹ˆà¹ƒà¸«à¹‰ bonus damage à¹€à¸•à¹‡à¸¡ (15%)")]
    public float runicMaxRange = 12f;

    [Header("Shield")]
    [Tooltip("Shield = X% à¸‚à¸­à¸‡ damage à¸—à¸µà¹ˆà¸—à¸³")]
    public float shieldPercent = 0.25f;

    [Header("Super BunnyHop")]
    [Tooltip("à¹€à¸›à¸´à¸”à¸”à¹‰à¸§à¸¢ ActivateSuper() â€” cast 2 à¸£à¸°à¹€à¸šà¸´à¸” AoE 2 à¸„à¸£à¸±à¹‰à¸‡")]
    [SerializeField, HideInInspector]
    private bool _isSuper;
    public bool IsSuper => _isSuper;
    public void ActivateSuper()   { _isSuper = true;  Debug.Log("[BunnyHop] â­ Super ACTIVATED"); }
    public void DeactivateSuper() { _isSuper = false; Debug.Log("[BunnyHop] Super deactivated"); }

    [Header("Blade of Exile Bonus")]
    [Tooltip("à¸„à¸¹à¸“ AoE radius à¹€à¸žà¸´à¹ˆà¸¡à¹€à¸•à¸´à¸¡à¸‚à¸“à¸° Exile active")]
    public float exileAoeMult = 1.5f;
    // Projectile speed + count à¸­à¹ˆà¸²à¸™à¸ˆà¸²à¸ WeaponData â†’ levels â†’ projectileSpeed / projectileCount

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;

    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    protected override void OnFire(WeaponLevelData ld) { /* à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ */ }

    // â”€â”€ à¹€à¸£à¸µà¸¢à¸à¸ˆà¸²à¸ ChargeManager à¹€à¸¡à¸·à¹ˆà¸­ CHARGE à¹€à¸•à¹‡à¸¡ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public virtual void FireOnCharge(int fireCount)
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        var   ld     = data.GetLevelData(currentLevel);
        var   sm     = manager.statManager;
        float damage = ld.damage * (sm != null ? sm.GetPowerMultiplier() : 1f);
        float range  = ld.range  * (sm != null ? sm.GetAreaMultiplier()  : 1f);

        // cast 1 = cast 2: radius à¹€à¸—à¹ˆà¸²à¸à¸±à¸™ (bigSlash à¹ƒà¸Šà¹‰à¹€à¸‰à¸žà¸²à¸° Super double hit)
        bool  bigSlash  = (fireCount % 2 == 0);
        float aoeRadius = range;

        // Exile â†’ radius à¹ƒà¸«à¸à¹ˆà¸‚à¸¶à¹‰à¸™à¸­à¸µà¸
        bool exileActive = GetExileActive();
        if (exileActive) aoeRadius *= exileAoeMult;

        // Runic Blade: à¸¢à¸´à¹ˆà¸‡à¹„à¸à¸¥à¸¨à¸±à¸•à¸£à¸¹ â†’ damage +0â€“15%
        Transform nearest = FindNearestEnemy(aoeRadius * 2f);
        if (nearest != null)
        {
            float dist  = Vector3.Distance(transform.position, nearest.position);
            float bonus = Mathf.Clamp01(dist / runicMaxRange) * 0.15f;
            damage *= (1f + bonus);
        }

        damage = RollDamage(damage, out bool isCrit);

        // à¸—à¸´à¸¨ dash = à¸—à¸´à¸¨à¸—à¸µà¹ˆà¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™à¸à¸” input à¸­à¸¢à¸¹à¹ˆ
        Vector3 dashDir = manager.playerMove?.MoveDirection ?? Vector3.zero;
        if (dashDir.sqrMagnitude < 0.001f)
        {
            dashDir = nearest != null
                ? (nearest.position - transform.position)
                : transform.forward;
            dashDir.y = 0f;
            if (dashDir.sqrMagnitude > 0.001f) dashDir = dashDir.normalized;
            else dashDir = transform.forward;
        }

        StartCoroutine(DashAndFire(dashDir, aoeRadius, damage, bigSlash, exileActive, isCrit));
    }

    protected virtual IEnumerator DashAndFire(Vector3 dir, float radius, float damage,
                            bool bigSlash, bool exileActive, bool isCrit = false)
    {
        var pm = manager.playerMove;
        var rb = pm?.GetComponent<Rigidbody>();

        // â”€â”€ Dash â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (rb != null && pm != null)
        {
            pm.isDashing = true;
            rb.velocity  = Vector3.zero;

            Vector3 startPos = rb.position;
            Vector3 endPos   = startPos + dir * dashDistance;
            float   elapsed  = 0f;

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

        Vector3 center     = transform.position;
        int     aoeHitCount = (_isSuper && bigSlash) ? 2 : 1;

        // â”€â”€ AoE radial 360Â° à¸£à¸­à¸šà¸•à¸±à¸§ â€” à¸—à¸¸à¸ cast â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Damage à¹€à¸£à¸µà¸¢à¸à¸•à¸²à¸¡ aoeHitCount (Super double hit) à¹à¸•à¹ˆ VFX à¹à¸ªà¸”à¸‡ 1 à¸„à¸£à¸±à¹‰à¸‡à¸žà¸­
        for (int i = 0; i < aoeHitCount; i++)
            FireMelee(center, radius, damage);
        // isAttackHit:false â†’ à¹„à¸¡à¹ˆ spawn HitEffect overlay (Enemy.EnemyTakeDamage à¸ˆà¸±à¸”à¹ƒà¸«à¹‰à¹à¸¥à¹‰à¸§)
        ShowVfx(ResolveHitVfx("MeteorAoE"), center, radius, isAttackHit: false);

        // â”€â”€ Shield â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float shieldAmount = damage * shieldPercent
                           * Mathf.Max(1f, FindAllEnemiesInRange(radius).Length);
        manager.AddShieldServerRpc(shieldAmount);

        // â”€â”€ Exile Bonus: Projectile radial 360Â° (à¸—à¸¸à¸ cast à¹€à¸¡à¸·à¹ˆà¸­ Exile active) â”€â”€
        if (exileActive)
        {
            // Projectile à¸à¸£à¸°à¸ˆà¸²à¸¢ 360Â°/count â€” à¸œà¹ˆà¸²à¸™ BuildEffectiveLevelData à¹€à¸žà¸·à¹ˆà¸­à¸£à¸±à¸š stat bonus
            // (projectileCount + GetBonusProjectileCount, range Ã— GetAreaMultiplier)
            var rawLd          = data != null ? data.GetLevelData(currentLevel) : new WeaponLevelData();
            var   projLd       = BuildEffectiveLevelData(rawLd);
            float baseRange    = projLd.range;
            float projMaxRange = baseRange * exileProjectileRangeMult;
            float projSpeed    = projLd.projectileSpeed;
            int   pCount       = Mathf.Max(1, projLd.projectileCount);
            float angleStep    = 360f / pCount;
            for (int i = 0; i < pCount; i++)
            {
                float   angle   = i * angleStep;
                Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * dir; // dir = à¸—à¸´à¸¨à¸—à¸µà¹ˆà¸à¸³à¸¥à¸±à¸‡à¹„à¸›
                FireProjectile(center, projDir, damage, projSpeed,
                               piercing: projLd.piercing, maxRange: projMaxRange,
                               isCrit: isCrit);
            }
        }
    }

    protected bool GetExileActive()
    {
        var exile = manager.GetComponentInChildren<BladeOfExileWeapon>();
        return exile != null && exile.IsExileActive;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        float baseRange = data != null ? data.GetLevelData(currentLevel).range : 3f;
        float aoeR      = baseRange;
        float projR     = baseRange * exileProjectileRangeMult;

        Vector3 pos = transform.position;

        // â”€â”€ AoE cast 1 & 2 (à¹€à¸‚à¸µà¸¢à¸§ â€” à¸‚à¸™à¸²à¸”à¹€à¸—à¹ˆà¸²à¸à¸±à¸™) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.up, aoeR);
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, aoeR);

        // â”€â”€ Exile: Projectile radial (à¸ªà¹‰à¸¡) â€” à¸­à¸±à¸™à¹à¸£à¸à¸ˆà¸²à¸ forward â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        int pCount = Mathf.Max(1, data != null ? data.GetLevelData(currentLevel).projectileCount : 4);
        for (int i = 0; i < pCount; i++)
        {
            float   angle   = i * (360f / pCount);
            Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * transform.forward; // forward à¹à¸—à¸™ dir à¸ˆà¸£à¸´à¸‡à¹ƒà¸™ editor
            Gizmos.DrawRay(pos, projDir * projR);
            // à¸ˆà¸¸à¸”à¸›à¸¥à¸²à¸¢
            Gizmos.DrawWireSphere(pos + projDir * projR, i == 0 ? 0.2f : 0.12f);
        }

        // â”€â”€ Label â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(pos + Vector3.right * aoeR,  $"aoe r={aoeR:F1}");
        UnityEditor.Handles.Label(pos + Vector3.forward * projR + Vector3.up * 0.3f,
                                                                   $"proj range={projR:F1}");
    }
#endif

}
