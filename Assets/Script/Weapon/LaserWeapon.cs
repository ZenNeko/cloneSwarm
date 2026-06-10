using UnityEngine;

/// <summary>
/// Laser — Hunter's starting weapon
/// AoE เส้นตรงแบบมีความกว้าง (Box AoE) — damage enemy ทุกตัวในแนวยิง
/// ใช้ FireLineAoEServerRpc (Physics.OverlapBox) — ไม่มี projectile
/// ยิงเส้นเดียวเสมอ — ไม่รับผลจาก projectileCount
///
/// Level data แนะนำ:
///   Lv1: dmg=35,  cd=1.8s, range=14
///   Lv2: dmg=45,  cd=1.6s, range=16
///   Lv3: dmg=58,  cd=1.4s, range=18
///   Lv4: dmg=72,  cd=1.2s, range=20
///   Lv5: dmg=90,  cd=1.0s, range=22
/// </summary>
public class LaserWeapon : WeaponBase
{
    [Header("Laser Config")]
    [Tooltip("ความกว้างของ AoE (หน่วย Unity) — ยิ่งมาก ยิ่งกว้าง")]
    public float width = 1.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();
        float   dmg = RollDamage(ld.damage, out bool isCrit);

        // ยิงเส้นเดียวเสมอ — ไม่สนใจ projectileCount
        manager.FireLineAoEServerRpc(pos, dir, dmg, ld.range, width, isCrit, vfxKey: ResolveHitVfx("Beam_Laser"));
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (data == null) return;
        float range = data.GetLevelData(currentLevel).range;

        Vector3 dir    = transform.forward;
        Vector3 center = transform.position + dir * (range * 0.5f) + Vector3.up * 0.5f;

        UnityEditor.Handles.color = new Color(0.1f, 0.95f, 1f, 0.20f);
        // วาด box แบบ wireframe แทน (Handles ไม่มี DrawBox โดยตรง ใช้ matrix แทน)
        UnityEngine.Gizmos.color  = new Color(0.1f, 0.95f, 1f, 0.35f);
        UnityEngine.Gizmos.matrix = Matrix4x4.TRS(center,
            Quaternion.LookRotation(dir), Vector3.one);
        UnityEngine.Gizmos.DrawWireCube(Vector3.zero,
            new Vector3(width, 2.4f, range));
        UnityEngine.Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
