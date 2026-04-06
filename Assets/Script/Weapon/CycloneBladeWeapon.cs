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
        float dmg    = RollDamage(ld.damage);
        float radius = ld.range;

        if (manager.statManager != null)
        {
            dmg    *= manager.statManager.GetPowerMultiplier();
            radius *= manager.statManager.GetAreaMultiplier();
        }

        Vector3 center = transform.position + Vector3.up * 0.5f;

        if (cycleIndex % 2 == 0)
        {
            // 360° spin
            manager.FireMeleeServerRpc(center, radius, dmg);
            manager.BroadcastVfxTypeServerRpc(center, (int)VFXType.WhipSlash);
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
                manager.FireMeleeServerRpc(pos, radius * 0.8f, fDmg);
                manager.BroadcastVfxTypeServerRpc(pos, (int)VFXType.WhipSlash);
            }
        }

        cycleIndex++;
    }

    Vector3 GetAimDirection()
    {
        int mask    = LayerMask.GetMask("Enemy");
        var cols    = Physics.OverlapSphere(transform.position, 20f, mask);
        float minD  = float.MaxValue;
        Vector3 dir = transform.forward;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minD) { minD = d; dir = (c.transform.position - transform.position).normalized; }
        }
        dir.y = 0f;
        return dir == Vector3.zero ? transform.forward : dir;
    }
}
