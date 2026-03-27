using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbiter — วน Orb รอบตัวผู้เล่น
/// ใช้สำหรับ Normal (Orbiter) และ Super (Star Ring)
///
/// Orbs เป็น Local GameObject (ไม่ใช่ NetworkObject) — visual only บน Owner
/// Damage ส่งผ่าน FireMeleeServerRpc ที่ตำแหน่ง orb ทุก cooldown
///
/// StarRing ต่างกัน: orbCount มากขึ้น, aoeOnHit = true
///
/// Level data แนะนำ:
///   Lv1: dmg=18, cd=1.5s, count=2, range=3 (orbit radius)
///   Lv2: dmg=22, cd=1.4s, count=2, range=3
///   Lv3: dmg=28, cd=1.3s, count=3, range=3.5
///   Lv4: dmg=34, cd=1.2s, count=3, range=4
///   Lv5: dmg=42, cd=1.0s, count=4, range=4.5
/// </summary>
public class OrbiterWeapon : WeaponBase
{
    [Header("Orb Visual")]
    public GameObject orbPrefab;      // Simple sphere mesh (no NetworkObject)
    public float      orbSize  = 0.4f;
    public float      rotSpeed = 90f; // องศา/วินาที

    [Header("Star Ring Super")]
    public bool  aoeOnHit  = false;
    public float aoeRadius = 1.5f;   // radius ของ AoE เมื่อ orb ชนศัตรู

    private List<Transform> orbs      = new();
    private float           orbitAngle;

    protected override void OnInit()
    {
        aimMode = AimMode.AutoNearest;
        SpawnOrbs(data.GetLevelData(currentLevel).projectileCount);
    }

    protected override void OnLevelUp()
    {
        int needed = data.GetLevelData(currentLevel).projectileCount;
        if (orbs.Count < needed) SpawnOrbs(needed - orbs.Count);
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
        // ส่ง damage ที่ตำแหน่ง orb แต่ละลูก
        float dmg    = RollDamage(ld.damage);
        float radius = aoeOnHit ? aoeRadius : 0.6f;   // hit radius รอบ orb

        foreach (var orb in orbs)
        {
            if (orb == null) continue;
            Vector3 worldPos = orb.position;
            manager.FireMeleeServerRpc(worldPos, radius, dmg);
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
        foreach (var o in orbs)
            if (o != null) Destroy(o.gameObject);
    }
}
