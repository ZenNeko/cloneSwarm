using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chainsaw — Super version ของ Whip (Tentacle style)
/// Line AoE ข้างหน้า + knockback + chain ไป enemy ถัดไป
///
/// Super tier — 1 level
///   dmg=40, cd=0.25s, range=5.0
///
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class ChainsawWeapon : WhipWeapon
{
    [Header("Chainsaw Bonus")]
    [Tooltip("แรงผลัก enemy ที่โดน")]
    public float knockbackForce = 3f;
    [Tooltip("จำนวน chain ต่อจาก line hit")]
    public int   chainCount     = 2;
    [Tooltip("ดาเมจลดลงต่อ chain")]
    [Range(0.3f, 1f)]
    public float chainDamageMult = 0.7f;
    [Tooltip("รัศมีหา chain target ถัดไป")]
    public float chainSearchRadius = 8f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = GetForwardDirection();
        float   dmg    = RollDamage(ld.damage, out bool isCrit);
        float   range  = ld.range;

        if (manager.statManager != null)
            range *= manager.statManager.GetAreaMultiplier();

        // ── Main line hit (manual OverlapBox เพื่อ knockback) ─────────────
        Vector3 center     = origin + dir * (range * 0.5f);
        Vector3 halfExtent = new Vector3(width * 0.5f, 1f, range * 0.5f);
        Quaternion rot     = Quaternion.LookRotation(dir);
        int   mask         = LayerMask.GetMask("Enemy");
        var   cols         = Physics.OverlapBox(center, halfExtent, rot, mask);
        var   hitSet       = new HashSet<int>();
        Enemy lastHit      = null;

        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || hitSet.Contains(e.GetInstanceID())) continue;

            e.EnemyTakeDamage(dmg);
            hitSet.Add(e.GetInstanceID());

            // Knockback
            Vector3 pushDir = (e.transform.position - transform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.001f)
                e.transform.position += pushDir.normalized * knockbackForce;

            lastHit = e;
        }

        // VFX ที่จุดกลางของ line
        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(VFXType.SlashHit, vfxPos, range, isCrit, direction: dir);

        // ── Chain จาก enemy ตัวสุดท้ายที่โดน ──────────────────────────────
        if (lastHit != null)
        {
            Vector3 prevPos = lastHit.transform.position + Vector3.up * 0.5f;
            float   curDmg  = dmg;

            for (int i = 0; i < chainCount; i++)
            {
                curDmg *= chainDamageMult;
                Enemy next = FindNearestUnhitEnemy(prevPos, chainSearchRadius, mask, hitSet);
                if (next == null) break;

                Vector3 nextPos = next.transform.position + Vector3.up * 0.5f;
                next.EnemyTakeDamage(curDmg);
                hitSet.Add(next.GetInstanceID());

                // Knockback
                Vector3 pushDir = (next.transform.position - transform.position);
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude > 0.001f)
                    next.transform.position += pushDir.normalized * knockbackForce * 0.5f;

                // VFX beam chain
                manager.BroadcastBeamServerRpc(prevPos, nextPos, (int)VFXType.None);
                prevPos = nextPos;
            }
        }
    }

    Enemy FindNearestUnhitEnemy(Vector3 center, float radius, int mask, HashSet<int> exclude)
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
