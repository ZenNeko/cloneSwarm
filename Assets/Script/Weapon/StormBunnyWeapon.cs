using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Storm Bunny — Fusion: BunnyHop Super + Stormcaller
/// Dash + 360° AoE + chain lightning ที่จุดลงจอด
/// เมื่อ Exile active → projectile + wind slash ยิงออกไป มีผล lightning chain ด้วย
///
/// Fusion tier — 1 level
/// WeaponData: superWeaponA = BunnyHop Super, superWeaponB = Stormcaller
/// </summary>
public class StormBunnyWeapon : BunnyHopWeapon
{
    [Header("Storm Bunny — Lightning Chain")]
    [Tooltip("จำนวน chain lightning หลัง dash")]
    public int   chainCount        = 4;
    [Tooltip("รัศมีหา chain target")]
    public float chainRadius       = 8f;
    [Tooltip("ดาเมจลดลงต่อ chain")]
    [Range(0.3f, 1f)]
    public float chainDamageMult   = 0.85f;

    [Header("Exile Lightning Bonus")]
    [Tooltip("จำนวน mini-chain จาก projectile/wind slash hit")]
    public int   exileMiniChain    = 2;
    [Tooltip("delay ก่อน mini-chain (วินาที)")]
    public float exileChainDelay   = 0.3f;
    [Tooltip("รัศมี mini-chain")]
    public float exileMiniRadius   = 6f;
    [Tooltip("ดาเมจ mini-chain เป็น % ของ damage หลัก")]
    [Range(0.1f, 1f)]
    public float exileMiniDmgRatio = 0.4f;

    // Override DashAndFire เพื่อเพิ่ม chain lightning หลัง AoE
    protected override IEnumerator DashAndFire(Vector3 dir, float radius, float damage,
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

            ShowVfx(dashTrailVfxType, startPos, isAttackHit: false);

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

        Vector3 center      = transform.position;
        int     aoeHitCount = (IsSuper && bigSlash) ? 2 : 1;

        // ── AoE 360° ─────────────────────────────────────────────────────
        for (int i = 0; i < aoeHitCount; i++)
        {
            manager.FireMeleeServerRpc(center, radius, damage);
            ShowVfx(VFXType.MeteorAoE, center, radius);
        }

        // ── Shield ────────────────────────────────────────────────────────
        float shieldAmount = damage * shieldPercent
                           * Mathf.Max(1f, FindAllEnemiesInRange(radius).Length);
        manager.AddShieldServerRpc(shieldAmount);

        // ── Chain Lightning ที่จุดลงจอด ───────────────────────────────────
        StartCoroutine(ChainLightningFromLanding(center, damage));

        // ── Exile Bonus: Projectile + Wind Slash + Lightning ─────────────
        if (exileActive)
        {
            var rawLd          = data != null ? data.GetLevelData(currentLevel) : new WeaponLevelData();
            var projLd         = BuildEffectiveLevelData(rawLd);
            float baseRange    = projLd.range;
            float projMaxRange = baseRange * exileProjectileRangeMult;
            float projSpeed    = projLd.projectileSpeed;
            int   pCount       = Mathf.Max(1, projLd.projectileCount);
            float angleStep    = 360f / pCount;
            for (int i = 0; i < pCount; i++)
            {
                float   angle   = i * angleStep;
                Vector3 projDir = Quaternion.Euler(0f, angle, 0f) * dir;
                FireProjectile(center, projDir, damage, projSpeed,
                               piercing: projLd.piercing, maxRange: projMaxRange,
                               isCrit: isCrit);
            }

            // Wind Slash radial 360°
            float slashDmg = damage * windSlashDmgRatio;
            for (int i = 0; i < windSlashCount; i++)
            {
                float   angle    = i * (360f / windSlashCount);
                Vector3 slashDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                manager.FireRaycastServerRpc(center, slashDir, slashDmg, windSlashRange,
                                             vfxTypeInt: (int)VFXType.SlashHit,
                                             isCrit: isCrit);
            }

            // Mini chain lightning จาก projectile + wind slash hits (delayed)
            StartCoroutine(ExileLightningBonus(center, damage, radius));
        }
    }

    // ── Chain Lightning ที่จุดลงจอด ───────────────────────────────────────
    IEnumerator ChainLightningFromLanding(Vector3 landingPos, float damage)
    {
        yield return null; // 1 frame delay เพื่อให้ AoE ทำงานก่อน

        int   mask   = LayerMask.GetMask("Enemy");
        var   hitSet = new HashSet<int>();
        float curDmg = damage * 0.6f; // chain damage = 60% of main

        Vector3 prevPos = landingPos + Vector3.up * 0.5f;

        for (int i = 0; i < chainCount; i++)
        {
            Enemy next = FindNearestUnhit(prevPos, chainRadius, mask, hitSet);
            if (next == null) break;

            Vector3 nextPos = next.transform.position + Vector3.up * 0.5f;
            next.EnemyTakeDamage(curDmg);
            hitSet.Add(next.GetInstanceID());

            manager.BroadcastBeamServerRpc(prevPos, nextPos, (int)VFXType.None);

            prevPos  = nextPos;
            curDmg  *= chainDamageMult;
        }
    }

    // ── Exile Bonus: mini chain lightning จาก AoE radius (delayed) ────────
    IEnumerator ExileLightningBonus(Vector3 center, float damage, float radius)
    {
        yield return new WaitForSeconds(exileChainDelay);

        int   mask    = LayerMask.GetMask("Enemy");
        float miniDmg = damage * exileMiniDmgRatio;

        // หา enemy ทั้งหมดที่ยังอยู่ใน radius (ที่โดน projectile/wind slash)
        var cols   = Physics.OverlapSphere(center, radius + 4f, mask);
        var hitSet = new HashSet<int>();

        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || hitSet.Contains(e.GetInstanceID())) continue;
            hitSet.Add(e.GetInstanceID());

            // mini chain จาก enemy ตัวนี้
            Vector3 prevPos = e.transform.position + Vector3.up * 0.5f;
            float   curDmg  = miniDmg;

            for (int j = 0; j < exileMiniChain; j++)
            {
                Enemy next = FindNearestUnhit(prevPos, exileMiniRadius, mask, hitSet);
                if (next == null) break;

                Vector3 nextPos = next.transform.position + Vector3.up * 0.5f;
                next.EnemyTakeDamage(curDmg);
                hitSet.Add(next.GetInstanceID());

                manager.BroadcastBeamServerRpc(prevPos, nextPos, (int)VFXType.None);

                prevPos  = nextPos;
                curDmg  *= chainDamageMult;
            }
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────
    Enemy FindNearestUnhit(Vector3 center, float radius, int mask, HashSet<int> exclude)
    {
        var   cols = Physics.OverlapSphere(center, radius, mask);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || exclude.Contains(e.GetInstanceID())) continue;
            float d = Vector3.Distance(center, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        return best;
    }
}
