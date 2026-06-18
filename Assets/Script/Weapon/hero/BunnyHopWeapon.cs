using System.Collections;
using UnityEngine;

/// <summary>
/// Signature Weapon ของ Riven — ไม่ใช้ cooldown timer แต่ใช้ CHARGE จาก ChargeManager
///
/// ทุก cast  : Dash + AoE radial 360° รอบตัว
///   cast 1  : radius ปกติ
///   cast 1 = cast 2 : radius เท่ากัน
///
/// ขณะ AD_BladeOfExile active (เพิ่มเติมบน AoE ปกติ):
///   + ยิง Projectile ในทิศ dash
///   + AoE radius ×exileAoeMult
///   (Wind Slash ถูกถอดออก — รอ design ใหม่)
///
/// Super BunnyHop (IsSuper):
///   cast 2 เท่านั้น: AoE ตี 2 ครั้ง (double hit)
///
/// Runic Blade: ยิ่งอยู่ไกลศัตรูก่อน dash → damage +0–15%
/// Shield: 25% ของ damage × จำนวนศัตรูโดน
/// </summary>
public class BunnyHopWeapon : WeaponBase
{
    [Header("Dash")]
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("AoE")]
    [Tooltip("Projectile maxRange ขณะ Exile = base range × ค่านี้")]
    public float exileProjectileRangeMult = 2f;

    [Header("Runic Blade Passive")]
    [Tooltip("ระยะสูงสุดที่ให้ bonus damage เต็ม (15%)")]
    public float runicMaxRange = 12f;

    [Header("Shield")]
    [Tooltip("Shield = X% ของ damage ที่ทำ")]
    public float shieldPercent = 0.25f;

    [Header("Super BunnyHop")]
    [Tooltip("เปิดด้วย ActivateSuper() — cast 2 ระเบิด AoE 2 ครั้ง")]
    [SerializeField, HideInInspector]
    private bool _isSuper;
    public bool IsSuper => _isSuper;
    public void ActivateSuper()   { _isSuper = true;  Debug.Log("[BunnyHop] ⭐ Super ACTIVATED"); }
    public void DeactivateSuper() { _isSuper = false; Debug.Log("[BunnyHop] Super deactivated"); }

    [Header("Blade of Exile Bonus")]
    [Tooltip("คูณ AoE radius เพิ่มเติมขณะ Exile active")]
    public float exileAoeMult = 1.5f;
    // Projectile speed + count อ่านจาก WeaponData → levels → projectileSpeed / projectileCount

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;

    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    protected override void OnFire(WeaponLevelData ld) { /* ไม่ใช้ */ }

    // ── เรียกจาก ChargeManager เมื่อ CHARGE เต็ม ─────────────────────────
    public virtual void FireOnCharge(int fireCount)
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        var   ld     = data.GetLevelData(currentLevel);
        var   sm     = manager.statManager;
        float damage = ld.damage * (sm != null ? sm.GetPowerMultiplier() : 1f);
        float range  = ld.range  * (sm != null ? sm.GetAreaMultiplier()  : 1f);

        // cast 1 = cast 2: radius เท่ากัน (bigSlash ใช้เฉพาะ Super double hit)
        bool  bigSlash  = (fireCount % 2 == 0);
        float aoeRadius = range;

        // Exile → radius ใหญ่ขึ้นอีก
        bool exileActive = GetExileActive();
        if (exileActive) aoeRadius *= exileAoeMult;

        // Runic Blade: ยิ่งไกลศัตรู → damage +0–15%
        Transform nearest = FindNearestEnemy(aoeRadius * 2f);
        if (nearest != null)
        {
            float dist  = Vector3.Distance(transform.position, nearest.position);
            float bonus = Mathf.Clamp01(dist / runicMaxRange) * 0.15f;
            damage *= (1f + bonus);
        }

        damage = RollDamage(damage, out bool isCrit);

        // ทิศ dash = ทิศที่ผู้เล่นกด input อยู่
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

        // ── Dash ──────────────────────────────────────────────────────────
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

        // ── AoE radial 360° รอบตัว — ทุก cast ────────────────────────────
        // Damage เรียกตาม aoeHitCount (Super double hit) แต่ VFX แสดง 1 ครั้งพอ
        for (int i = 0; i < aoeHitCount; i++)
            FireMelee(center, radius, damage);
        // isAttackHit:false → ไม่ spawn HitEffect overlay (Enemy.EnemyTakeDamage จัดให้แล้ว)
        ShowVfx(ResolveHitVfx("MeteorAoE"), center, radius, isAttackHit: false);

        // ── Shield ────────────────────────────────────────────────────────
        float shieldAmount = damage * shieldPercent
                           * Mathf.Max(1f, FindAllEnemiesInRange(radius).Length);
        manager.AddShieldServerRpc(shieldAmount);

        // ── Exile Bonus: Projectile radial 360° (ทุก cast เมื่อ Exile active) ──
        if (exileActive)
        {
            // Projectile กระจาย 360°/count — ผ่าน BuildEffectiveLevelData เพื่อรับ stat bonus
            // (projectileCount + GetBonusProjectileCount, range × GetAreaMultiplier)
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
                Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * dir; // dir = ทิศที่กำลังไป
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

        // ── AoE cast 1 & 2 (เขียว — ขนาดเท่ากัน) ─────────────────────────
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.25f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.up, aoeR);
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, aoeR);

        // ── Exile: Projectile radial (ส้ม) — อันแรกจาก forward ────────────
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        int pCount = Mathf.Max(1, data != null ? data.GetLevelData(currentLevel).projectileCount : 4);
        for (int i = 0; i < pCount; i++)
        {
            float   angle   = i * (360f / pCount);
            Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * transform.forward; // forward แทน dir จริงใน editor
            Gizmos.DrawRay(pos, projDir * projR);
            // จุดปลาย
            Gizmos.DrawWireSphere(pos + projDir * projR, i == 0 ? 0.2f : 0.12f);
        }

        // ── Label ─────────────────────────────────────────────────────────
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(pos + Vector3.right * aoeR,  $"aoe r={aoeR:F1}");
        UnityEditor.Handles.Label(pos + Vector3.forward * projR + Vector3.up * 0.3f,
                                                                   $"proj range={projR:F1}");
    }
#endif

}
