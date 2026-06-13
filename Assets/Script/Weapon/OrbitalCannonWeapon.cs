using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbital Cannon â€” FUSION: Magnum (Super Pistol) + Star Ring (Super Orbiter)
///
/// à¸à¸¥à¹„à¸:
///   â€¢ Orb à¸§à¸™à¸£à¸­à¸šà¸•à¸±à¸§à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™ (à¹€à¸«à¸¡à¸·à¸­à¸™ Orbiter)
///   â€¢ à¹à¸•à¹ˆà¸¥à¸° Orb à¸¢à¸´à¸‡ Projectile à¸«à¸²à¸¨à¸±à¸•à¸£à¸¹à¹ƒà¸à¸¥à¹‰à¸ªà¸¸à¸”à¸—à¸¸à¸ cooldown
///   â€¢ Orb à¸¢à¸±à¸‡à¸„à¸‡ deal melee damage à¹€à¸¡à¸·à¹ˆà¸­à¸Šà¸™à¸¨à¸±à¸•à¸£à¸¹à¸”à¹‰à¸§à¸¢
///
/// Level data (Fusion tier, 1 level):
///   dmg=80 (projectile), cd=0.8s, count=4 (orb à¸ˆà¸³à¸™à¸§à¸™), range=5
///   projSpeed=18
/// </summary>
public class OrbitalCannonWeapon : WeaponBase
{
    [Header("Orb Visual")]
    public GameObject orbPrefab;
    public float      orbSize    = 0.5f;
    public float      rotSpeed   = 120f;
    public float      meleeDmgMult = 0.4f;  // melee damage = projectile dmg * mult

    private List<Transform> orbs      = new();
    private float           orbitAngle;

    protected override void OnInit()
    {
        SpawnOrbs(data.GetLevelData(currentLevel).projectileCount);
    }

    void Update()
    {
        if (manager == null || !manager.IsOwner) return;
        orbitAngle += rotSpeed * Time.deltaTime;
        float radius = data.GetLevelData(currentLevel).range;
        for (int i = 0; i < orbs.Count; i++)
        {
            if (orbs[i] == null) continue;
            float a = orbitAngle + (360f / orbs.Count) * i;
            float r = Mathf.Deg2Rad * a;
            orbs[i].localPosition = new Vector3(Mathf.Cos(r) * radius, 0.5f, Mathf.Sin(r) * radius);
        }
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        float projDmg  = RollDamage(ld.damage, out bool isCrit);
        float meleeDmg = projDmg * meleeDmgMult;

        foreach (var orb in orbs)
        {
            if (orb == null) continue;
            Vector3 orbPos = orb.position;

            // Melee AoE à¸—à¸µà¹ˆà¸•à¸³à¹à¸«à¸™à¹ˆà¸‡ orb
            FireMelee(orbPos, 0.8f, meleeDmg);

            // à¸¢à¸´à¸‡ projectile à¸«à¸²à¸¨à¸±à¸•à¸£à¸¹à¹ƒà¸à¸¥à¹‰à¸ªà¸¸à¸”à¸ˆà¸²à¸ orb
            var enemy = FindNearestEnemy(ld.range * 2f);
            if (enemy != null)
            {
                Vector3 dir = (enemy.position - orbPos);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    FireProjectile(orbPos, dir.normalized, projDmg, ld.projectileSpeed, isCrit: isCrit);
            }
        }
    }

    void SpawnOrbs(int count)
    {
        if (orbPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(orbPrefab, transform);
            go.transform.localScale = Vector3.one * orbSize;
            orbs.Add(go.transform);
        }
    }

    void OnDestroy()
    {
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
    }
}
