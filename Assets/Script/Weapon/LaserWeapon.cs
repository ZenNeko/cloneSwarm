using UnityEngine;

/// <summary>
/// Laser â€” Hunter's starting weapon
/// AoE à¹€à¸ªà¹‰à¸™à¸•à¸£à¸‡à¹à¸šà¸šà¸¡à¸µà¸„à¸§à¸²à¸¡à¸à¸§à¹‰à¸²à¸‡ (Box AoE) â€” damage enemy à¸—à¸¸à¸à¸•à¸±à¸§à¹ƒà¸™à¹à¸™à¸§à¸¢à¸´à¸‡
/// à¹ƒà¸Šà¹‰ FireLineAoEServerRpc (Physics.OverlapBox) â€” à¹„à¸¡à¹ˆà¸¡à¸µ projectile
/// à¸¢à¸´à¸‡à¹€à¸ªà¹‰à¸™à¹€à¸”à¸µà¸¢à¸§à¹€à¸ªà¸¡à¸­ â€” à¹„à¸¡à¹ˆà¸£à¸±à¸šà¸œà¸¥à¸ˆà¸²à¸ projectileCount
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=35,  cd=1.8s, range=14
///   Lv2: dmg=45,  cd=1.6s, range=16
///   Lv3: dmg=58,  cd=1.4s, range=18
///   Lv4: dmg=72,  cd=1.2s, range=20
///   Lv5: dmg=90,  cd=1.0s, range=22
/// </summary>
public class LaserWeapon : WeaponBase
{
    [Header("Laser Config")]
    [Tooltip("à¸„à¸§à¸²à¸¡à¸à¸§à¹‰à¸²à¸‡à¸‚à¸­à¸‡ AoE (à¸«à¸™à¹ˆà¸§à¸¢ Unity) â€” à¸¢à¸´à¹ˆà¸‡à¸¡à¸²à¸ à¸¢à¸´à¹ˆà¸‡à¸à¸§à¹‰à¸²à¸‡")]
    public float width = 1.5f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position + Vector3.up * 0.5f;
        Vector3 dir = GetAimDirection();
        float   dmg = RollDamage(ld.damage, out bool isCrit);

        // à¸¢à¸´à¸‡à¹€à¸ªà¹‰à¸™à¹€à¸”à¸µà¸¢à¸§à¹€à¸ªà¸¡à¸­ â€” à¹„à¸¡à¹ˆà¸ªà¸™à¹ƒà¸ˆ projectileCount
        FireLineAoE(pos, dir, dmg, ld.range, width, isCrit, vfxKey: ResolveHitVfx("Beam_Laser"));
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
        // à¸§à¸²à¸” box à¹à¸šà¸š wireframe à¹à¸—à¸™ (Handles à¹„à¸¡à¹ˆà¸¡à¸µ DrawBox à¹‚à¸”à¸¢à¸•à¸£à¸‡ à¹ƒà¸Šà¹‰ matrix à¹à¸—à¸™)
        UnityEngine.Gizmos.color  = new Color(0.1f, 0.95f, 1f, 0.35f);
        UnityEngine.Gizmos.matrix = Matrix4x4.TRS(center,
            Quaternion.LookRotation(dir), Vector3.one);
        UnityEngine.Gizmos.DrawWireCube(Vector3.zero,
            new Vector3(width, 2.4f, range));
        UnityEngine.Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
