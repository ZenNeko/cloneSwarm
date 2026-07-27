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
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;
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

    protected override void Update()
    {
        base.Update();
        if (manager == null || !manager.IsOwner) return;
        orbitAngle += rotSpeed * Time.deltaTime;
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        float radius = data.GetLevelData(currentLevel).range * areaMult;
        for (int i = 0; i < orbs.Count; i++)
        {
            if (orbs[i] == null) continue;
            float a = orbitAngle + (360f / orbs.Count) * i;
            float r = Mathf.Deg2Rad * a;
            orbs[i].localPosition = new Vector3(Mathf.Cos(r) * radius, 0.5f, Mathf.Sin(r) * radius);
            orbs[i].localScale = Vector3.one * (orbSize * areaMult);
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

            // Melee AoE ที่ตำแหน่ง orb
            float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
            FireMelee(orbPos, 0.8f * areaMult, meleeDmg, isCrit);

            // ยิง projectile หาศัตรูจาก orb (รองรับเล็งสุ่มและล็อกเป้าตัวใกล้สุด)
            var enemy = FindTargetEnemy(ld.range * 2f);
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
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(orbPrefab, transform);
            go.transform.localScale = Vector3.one * (orbSize * areaMult);
            orbs.Add(go.transform);
        }
    }

    void OnDestroy()
    {
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
    }
}
