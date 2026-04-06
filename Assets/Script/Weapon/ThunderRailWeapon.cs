using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thunder Rail — Fusion: Stormcaller + Railgun
/// Railgun pierce ยิงทะลุทุก enemy + chain lightning ออกจากทุกตัวที่โดน
///
/// Fusion tier — 1 level
///   dmg=150 (rail), chainDmg=60, chainTargets=3, cd=3.0s, range=50
/// </summary>
public class ThunderRailWeapon : WeaponBase
{
    [Tooltip("จำนวน chain targets จากแต่ละ enemy ที่โดน railgun")]
    public int   chainTargets       = 3;
    [Tooltip("ดาเมจ chain แต่ละตัว")]
    public float chainDamage        = 60f;
    [Tooltip("รัศมีหา chain target")]
    public float chainSearchRadius  = 8f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg = RollDamage(ld.damage);
        if (manager.statManager != null)
            dmg *= manager.statManager.GetPowerMultiplier();

        // Aim ไปหา nearest enemy
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 dir    = GetAimDirection();
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        dir = dir.normalized;

        // Raycast pierce — hit ทุก enemy ในแนว
        int mask = LayerMask.GetMask("Enemy");
        var hits = Physics.RaycastAll(origin, dir, ld.range, mask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        Vector3 endPoint = origin + dir * ld.range;

        // Railgun beam VFX
        manager.BroadcastBeamServerRpc(origin, endPoint, (int)VFXType.RailgunBeam);

        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy == null) continue;

            enemy.EnemyTakeDamage(dmg);

            // Chain lightning จาก enemy ที่โดน
            FireChainFrom(hit.point + Vector3.up * 0.8f, enemy.GetInstanceID(), chainDamage, chainTargets, mask);
        }
    }

    void FireChainFrom(Vector3 pos, int excludeId, float chainDmg, int remaining, int mask)
    {
        if (remaining <= 0) return;

        var cols   = Physics.OverlapSphere(pos, chainSearchRadius, mask);
        Enemy best = null;
        float minD = float.MaxValue;
        foreach (var c in cols)
        {
            var e = c.GetComponent<Enemy>();
            if (e == null || e.GetInstanceID() == excludeId) continue;
            float d = Vector3.Distance(pos, c.transform.position);
            if (d < minD) { minD = d; best = e; }
        }
        if (best == null) return;

        Vector3 targetPos = best.transform.position + Vector3.up * 0.8f;
        manager.BroadcastBeamServerRpc(pos, targetPos, (int)VFXType.RailgunBeam);
        best.EnemyTakeDamage(chainDmg);

        FireChainFrom(targetPos, best.GetInstanceID(), chainDmg * 0.7f, remaining - 1, mask);
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
        return dir;
    }
}
