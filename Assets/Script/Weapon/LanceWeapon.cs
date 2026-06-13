using UnityEngine;

/// <summary>
/// Tentacle (Lance) â€” à¹€à¸ªà¸·à¸­à¸à¸«à¸™à¸§à¸” tendril à¹€à¸”à¸µà¸¢à¸§à¸žà¸¸à¹ˆà¸‡à¸•à¸£à¸‡à¹„à¸›à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸²
/// Holocure-style Summon Tentacle: narrow forward line AoE
///
/// Level progression (5 levels):
///   Lv1: dmg=45,  cd=1.6s, range=6.0   (single tendril)
///   Lv2: dmg=58,  cd=1.5s, range=6.5   (+damage)
///   Lv3: dmg=73,  cd=1.3s, range=7.5   (-cooldown)
///   Lv4: dmg=90,  cd=1.2s, range=8.5   (+area)
///   Lv5: dmg=110, cd=1.0s, range=9.5   (+damage + KNOCKBACK unlock)
///
/// Super: TendrilStormWeapon (forward + 3 random tentacles)
/// </summary>
public class LanceWeapon : WeaponBase
{
    [Header("Lance Config")]
    [Tooltip("à¸„à¸§à¸²à¸¡à¸à¸§à¹‰à¸²à¸‡à¸‚à¸­à¸‡ pierce (à¹à¸„à¸š = single tendril)")]
    public float pierceWidth = 0.8f;
    [Tooltip("à¸ˆà¸³à¸™à¸§à¸™à¸„à¸£à¸±à¹‰à¸‡à¸—à¸µà¹ˆà¹à¸—à¸‡ â€” 1=single tendril, 2+=rapid (à¸¡à¸±à¸à¹Œà¹ƒà¸Šà¹‰à¹€à¸›à¹‡à¸™ 1)")]
    public int   thrustCount = 1;
    [Tooltip("à¸«à¸™à¹ˆà¸§à¸‡à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡ thrust à¸«à¸¥à¸²à¸¢à¸„à¸£à¸±à¹‰à¸‡ (à¸§à¸´à¸™à¸²à¸—à¸µ)")]
    public float thrustDelay = 0.12f;

    [Header("Knockback (unlocks at level)")]
    [Tooltip("Level à¸—à¸µà¹ˆà¸›à¸¥à¸”à¸¥à¹‡à¸­à¸ knockback")]
    public int   knockbackUnlockLevel = 5;
    [Tooltip("à¹à¸£à¸‡à¸œà¸¥à¸±à¸ enemy (à¹€à¸¡à¸•à¸£) à¸•à¸²à¸¡à¹à¸™à¸§à¸žà¸¸à¹ˆà¸‡")]
    public float knockbackForce       = 1.5f;

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

        if (thrustCount <= 1)
        {
            DoThrust(origin, dir, dmg, range, isCrit);
        }
        else
        {
            StartCoroutine(MultiThrust(origin, dir, dmg, range, isCrit));
        }
    }

    /// <summary>à¸¢à¸´à¸‡ tentacle 1 à¸„à¸£à¸±à¹‰à¸‡ (forward line AoE + VFX) â€” à¹ƒà¸Šà¹‰à¹ƒà¸™ subclass à¹„à¸”à¹‰</summary>
    protected void DoThrust(Vector3 origin, Vector3 dir, float dmg, float range, bool isCrit)
    {
        float kb = (currentLevel >= knockbackUnlockLevel) ? knockbackForce : 0f;

        FireLineAoE(origin, dir, dmg, range, pierceWidth, isCrit, kb);

        Vector3 vfxPos = origin + dir * (range * 0.5f);
        ShowVfx(ResolveHitVfx("LanceThrust"), vfxPos, isCrit: isCrit, isAttackHit: false, direction: dir);
    }

    System.Collections.IEnumerator MultiThrust(Vector3 origin, Vector3 dir, float dmg, float range, bool isCrit)
    {
        float dmgPerThrust = dmg / thrustCount;
        for (int i = 0; i < thrustCount; i++)
        {
            Vector3 pos = transform.position + Vector3.up * 0.5f;
            DoThrust(pos, dir, dmgPerThrust, range, isCrit);
            yield return new WaitForSeconds(thrustDelay);
        }
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (data == null) return;
        float   range  = data.GetLevelData(currentLevel).range;
        Vector3 dir    = transform.forward;
        Vector3 center = transform.position + dir * (range * 0.5f) + Vector3.up * 0.5f;

        Gizmos.color  = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(dir), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(pierceWidth, 1.2f, range));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
