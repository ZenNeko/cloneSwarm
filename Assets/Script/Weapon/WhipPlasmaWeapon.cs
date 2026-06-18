using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WhipPlasma — Super version ของ Whip (Tentacle style)
///
/// กลไก Tentacle:
///   1. เหวี่ยง tentacle หลักไปข้างหน้า (line AoE + knockback)
///   2. ปลาย tentacle "กระดอน" ไปหา enemy ใกล้เคียง N ตัว (chain beam)
///   3. แต่ละ chain hit ทำ damage ลดลงทีละขั้น
///   4. ทุก enemy ที่โดน — ผลัก (knockback) ออกจากผู้เล่น
///
/// Super tier — 1 level
///   dmg=55, cd=0.3s, range=5.5, count=3 (chain targets)
///
/// Fusion: PlasmaWhipWeapon (Railgun + WhipPlasma)
/// </summary>
public class WhipPlasmaWeapon : WhipWeapon
{
    [Header("Tentacle Chain")]
    [Tooltip("จำนวน chain ต่อจาก main hit")]
    public int   chainCount          = 3;
    [Tooltip("ดาเมจลดลงต่อ chain (0.7 = -30%)")]
    [Range(0.3f, 1f)]
    public float chainDamageMult     = 0.7f;
    [Tooltip("รัศมีหา chain target ถัดไป")]
    public float chainSearchRadius   = 8f;

    [Header("Knockback")]
    [Tooltip("แรงผลัก main hit")]
    public float knockbackForce      = 4f;
    [Tooltip("แรงผลัก chain hit (เบากว่า main)")]
    public float chainKnockbackForce = 2f;

    [Header("Tentacle Timing")]
    [Tooltip("delay ระหว่าง chain แต่ละ bounce (วินาที)")]
    public float chainDelay          = 0.06f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir    = GetAimDirection();
        float   dmg    = RollDamage(ld.damage, out bool isCrit);
        float   range  = ld.range;

        if (manager.statManager != null)
        {
            dmg   *= manager.statManager.GetPowerMultiplier();
            range *= manager.statManager.GetAreaMultiplier();
        }

        // ── Main Tentacle Strike (narrow line) ──────────────────────────
        int        mask       = LayerMask.GetMask("Enemy");
        Vector3    boxCenter  = origin + dir * (range * 0.5f);
        Vector3    halfExtent = new Vector3(width * 0.5f, 1f, range * 0.5f);
        Quaternion rot        = Quaternion.LookRotation(dir);
        var        cols       = Physics.OverlapBox(boxCenter, halfExtent, rot, mask);
        var        hitSet     = new HashSet<int>();
        var        mainHits   = new List<Enemy>();

        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || hitSet.Contains(e.GetInstanceID())) continue;

            Vector3 kbDir = e.transform.position - transform.position;
            FireMelee(e.transform.position, 0.3f, dmg, isCrit, knockbackForce, kbDir);
            hitSet.Add(e.GetInstanceID());
            mainHits.Add(e);
        }

        // VFX: WhipSlash arc ที่จุดกลาง
        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(ResolveHitVfx("WhipSlash"), vfxPos, range, isCrit, isAttackHit: false, direction: dir);

        // ── Chain Tentacle Bounce ────────────────────────────────────────
        // เริ่ม chain จาก enemy ที่อยู่ไกลสุดใน main hit (ปลาย tentacle)
        Enemy chainStart = GetFarthestEnemy(mainHits, origin);
        if (chainStart != null)
            StartCoroutine(ChainBounce(chainStart, dmg, isCrit, hitSet, mask));
    }

    IEnumerator ChainBounce(Enemy startEnemy, float baseDmg, bool isCrit,
                            HashSet<int> hitSet, int mask)
    {
        Vector3 prevPos = startEnemy.transform.position + Vector3.up * 0.5f;
        float   curDmg  = baseDmg * chainDamageMult;

        for (int i = 0; i < chainCount; i++)
        {
            if (chainDelay > 0f) yield return new WaitForSeconds(chainDelay);

            Enemy next = FindNearestUnhit(prevPos, chainSearchRadius, mask, hitSet);
            if (next == null) yield break;

            Vector3 nextPos = next.transform.position + Vector3.up * 0.5f;

            Vector3 kbDir = next.transform.position - transform.position;
            FireMelee(next.transform.position, 0.3f, curDmg, isCrit, chainKnockbackForce, kbDir);
            hitSet.Add(next.GetInstanceID());

            // Beam VFX ระหว่าง bounce (tentacle line)
            manager.BroadcastBeamServerRpc(prevPos, nextPos, ResolveSecondaryVfx("Default"), "HitEffect");

            prevPos  = nextPos;
            curDmg  *= chainDamageMult;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    Enemy GetFarthestEnemy(List<Enemy> list, Vector3 origin)
    {
        Enemy best = null;
        float maxD = -1f;
        foreach (var e in list)
        {
            if (e == null) continue;
            float d = Vector3.Distance(origin, e.transform.position);
            if (d > maxD) { maxD = d; best = e; }
        }
        return best;
    }

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
