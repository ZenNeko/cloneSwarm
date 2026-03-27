using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbital Cannon — FUSION: Magnum (Super Pistol) + Star Ring (Super Orbiter)
///
/// กลไก:
///   • Orb วนรอบตัวผู้เล่น (เหมือน Orbiter)
///   • แต่ละ Orb ยิง Projectile หาศัตรูใกล้สุดทุก cooldown
///   • Orb ยังคง deal melee damage เมื่อชนศัตรูด้วย
///
/// Level data (Fusion tier, 1 level):
///   dmg=80 (projectile), cd=0.8s, count=4 (orb จำนวน), range=5
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
        aimMode = AimMode.AutoNearest;
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
        float projDmg  = RollDamage(ld.damage);
        float meleeDmg = projDmg * meleeDmgMult;

        foreach (var orb in orbs)
        {
            if (orb == null) continue;
            Vector3 orbPos = orb.position;

            // Melee AoE ที่ตำแหน่ง orb
            manager.FireMeleeServerRpc(orbPos, 0.8f, meleeDmg);

            // ยิง projectile หาศัตรูใกล้สุดจาก orb
            var enemy = FindNearestEnemy(ld.range * 2f);
            if (enemy != null)
            {
                Vector3 dir = (enemy.position - orbPos);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    manager.FireProjectileServerRpc(
                        orbPos, dir.normalized, projDmg,
                        ld.projectileSpeed, 1, 0f, piercing: false);
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
