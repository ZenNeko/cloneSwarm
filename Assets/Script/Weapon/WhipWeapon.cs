using UnityEngine;

/// <summary>
/// Whip — Tentacle style: ตี AoE เป็นเส้นตรงข้างหน้า (forward line)
/// หา nearest enemy → ยิง line AoE (OverlapBox) ในทิศนั้น
///
/// Level data แนะนำ:
///   Lv1: dmg=35, cd=1.8s, range=2.5
///   Lv2: dmg=44, cd=1.6s, range=2.8
///   Lv3: dmg=55, cd=1.4s, range=3.2
///   Lv4: dmg=68, cd=1.2s, range=3.6
///   Lv5: dmg=85, cd=1.0s, range=4.0
///
/// Super: ChainsawWeapon (knockback + chain)
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class WhipWeapon : WeaponBase
{
    [Header("Tentacle Config")]
    [Tooltip("ความกว้างของ line AoE")]
    public float width = 1.5f;

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

        // Line AoE ข้างหน้า (Physics.OverlapBox ผ่าน server)
        manager.FireLineAoEServerRpc(origin, dir, dmg, range, width, isCrit);
        // VFX ที่จุดกลางของ line
        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(ResolveHitVfx("WhipSlash"), vfxPos, range, isCrit, isAttackHit: false, direction: dir);
    }

    /// <summary>หา nearest enemy แล้วหันหน้าไป — ใช้ใน subclass ได้</summary>
    protected Vector3 GetForwardDirection()
    {
        int   mask    = LayerMask.GetMask("Enemy");
        var   cols    = Physics.OverlapSphere(transform.position, 20f, mask);
        float minDist = float.MaxValue;
        Vector3 dir   = transform.forward;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist)
            {
                minDist = d;
                dir     = (c.transform.position - transform.position).normalized;
            }
        }
        dir.y = 0f;
        return dir == Vector3.zero ? transform.forward : dir;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        float range = data.GetLevelData(currentLevel).range;
        Vector3 dir    = transform.forward;
        Vector3 center = transform.position + dir * (range * 0.5f) + Vector3.up * 0.5f;

        Gizmos.color  = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(dir), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 2f, range));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
