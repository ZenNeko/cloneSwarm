using System.Collections;
using UnityEngine;

/// <summary>
/// Cyclone Blade — Fusion: Blade Storm + Chainsaw
/// 360° spin AoE + double forward slash สลับกัน ทุก cooldown
///
/// Fusion tier — 1 level
///   dmg=100 (spin), forwardDmg=80 (slash), cd=0.8s, range=4.5
/// </summary>
public class CycloneBladeWeapon : WeaponBase
{
    [Tooltip("จำนวน slash ไปข้างหน้าต่อ cycle")]
    public int   forwardSlashCount  = 2;
    [Tooltip("ดาเมจ slash ข้างหน้า relative กับ ld.damage")]
    public float forwardDamageMult  = 0.8f;
    public float slashOffset        = 0.7f;

    private int cycleIndex;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = ld.range;

        Vector3 center = transform.position + Vector3.up * 0.5f;

        if (cycleIndex % 2 == 0)
        {
            // 360° spin — main VFX
            FireMelee(center, radius, dmg, isCrit);
            ShowVfx(ResolveHitVfx("SlashAoE360"), center, radius, isCrit, isAttackHit: false);
        }
        else
        {
            // Forward double slash
            Vector3 forward = GetAimDirection();
            Vector3 right   = Vector3.Cross(Vector3.up, forward).normalized;
            float   fDmg    = dmg * forwardDamageMult;

            for (int i = 0; i < forwardSlashCount; i++)
            {
                float   side   = (i % 2 == 0) ? -1f : 1f;
                Vector3 pos    = center + forward * (radius * 0.6f) + right * slashOffset * side;
                FireMelee(pos, radius * 0.8f, fDmg, isCrit);
                ShowVfx(ResolveSecondaryVfx("SlashHit"), pos, radius * 0.8f, isCrit, isAttackHit: false, direction: forward);
            }
        }

        cycleIndex++;
    }
}
