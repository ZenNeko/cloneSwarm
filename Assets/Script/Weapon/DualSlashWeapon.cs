using UnityEngine;

/// <summary>
/// Dual Slash — slash 2 ครั้งพร้อมกัน ซ้าย/ขวาของทิศหน้า
///
/// Level data แนะนำ:
///   Lv1: dmg=30, cd=1.5s, range=2.0
///   Lv2: dmg=38, cd=1.3s, range=2.2
///   Lv3: dmg=47, cd=1.1s, range=2.5
///   Lv4: dmg=56, cd=1.0s, range=2.8
///   Lv5: dmg=65, cd=0.9s, range=3.0
///
/// Super: BladeStormWeapon (slash 360° 3 ครั้ง rapid)
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class DualSlashWeapon : WeaponBase
{
    [Tooltip("ระยะ offset ซ้าย/ขวา (เมตร) ของ slash แต่ละครั้ง")]
    public float slashOffset = 0.6f;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage);
        float radius = ld.range;

        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        // หาทิศหน้าจาก nearest enemy หรือ forward
        Vector3 forward = GetAimDirection();
        Vector3 right   = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 origin  = transform.position + Vector3.up * 0.5f;

        // Slash ซ้าย
        Vector3 leftPos  = origin + forward * (radius * 0.6f) - right * slashOffset;
        // Slash ขวา
        Vector3 rightPos = origin + forward * (radius * 0.6f) + right * slashOffset;

        manager.FireMeleeServerRpc(leftPos,  radius, dmg);
        manager.FireMeleeServerRpc(rightPos, radius, dmg);

        manager.BroadcastVfxTypeServerRpc(leftPos,  (int)VFXType.WhipSlash);
        manager.BroadcastVfxTypeServerRpc(rightPos, (int)VFXType.WhipSlash);
    }

    Vector3 GetAimDirection()
    {
        // หา nearest enemy แล้วหันหน้าไป
        int mask = LayerMask.GetMask("Enemy");
        var cols = Physics.OverlapSphere(transform.position, 20f, mask);
        float   minDist = float.MaxValue;
        Vector3 dir     = transform.forward;
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
}
