using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GrenadeProjectile — NetworkObject
/// บินไปยัง targetPos แล้วระเบิด AoE
/// ถ้า cluster = true → ยิง pellets รอบทิศทางหลังระเบิด
///
/// Prefab setup:
///   • NetworkObject component
///   • Rigidbody (kinematic=true)
///   • Sphere Collider (isTrigger optional)
///   • GrenadeProjectile script
/// </summary>
/// <summary>
/// ค่า cluster ที่ "อาวุธ" ส่งมาคุมลูกระเบิด — ให้จูนอาวุธได้จบในคอมโพเนนต์เดียว
/// ไม่ต้องเด้งไปแก้ prefab ลูกระเบิดอีกที
///
/// ยุบเป็น struct แทนการยืด signature ของ ThrowGrenadeServerRpc ที่ยาวอยู่แล้ว —
/// แบบเดียวกับ TelegraphInit ตามที่ CLAUDE.md กำหนดไว้ (พารามิเตอร์ float เรียงกันหลายตัว
/// สลับลำดับแล้ว compile ผ่าน หาไม่เจอจนกว่าจะเห็นของผิดในเกม)
///
/// `overrideProjectile = false` (ค่า default ของ struct) = ไม่แตะอะไร ใช้ค่าบน prefab ลูกตามเดิม
/// อาวุธที่ไม่ได้ส่งอะไรมาจึงทำงานเหมือนเดิมทุกประการ
///
/// **`clusterProjectilePrefab` ไม่อยู่ในนี้** — prefab reference ส่งข้ามเน็ตเวิร์กไม่ได้
/// ต้องตั้งบน prefab ลูกระเบิดเสมอ (เหตุผลเดียวกับที่ ADR-006 อธิบายว่าทำไม VFX ต้องเป็น id ไม่ใช่ prefab)
/// </summary>
[System.Serializable]
public struct GrenadeClusterSettings : INetworkSerializable
{
    [Tooltip("เปิด = อาวุธคุมค่าด้านล่างทั้งหมด · ปิด = ใช้ค่าที่ตั้งบน prefab ลูกระเบิด")]
    public bool  overrideProjectile;
    [Tooltip("จำนวนลูกที่ปล่อยตอนระเบิด")]
    public int   pellets;
    [Tooltip("ดาเมจลูกย่อยเป็น % ของดาเมจลูกแม่")]
    public float dmgPercent;
    [Tooltip("ความเร็วกระสุนย่อย — ใช้เฉพาะเมื่อ prefab ลูกเป็นตระกูล Projectile")]
    public float projSpeed;
    [Tooltip("รัศมีที่ลูกใหม่กระจายไปตกรอบจุดระเบิด")]
    public float spreadRadius;
    [Tooltip("รัศมีระเบิดของลูกใหม่")]
    public float childRadius;
    [Tooltip("เวลาหน่วงก่อนลูกใหม่ระเบิด")]
    public float childFuse;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref overrideProjectile);
        s.SerializeValue(ref pellets);
        s.SerializeValue(ref dmgPercent);
        s.SerializeValue(ref projSpeed);
        s.SerializeValue(ref spreadRadius);
        s.SerializeValue(ref childRadius);
        s.SerializeValue(ref childFuse);
    }
}

public class GrenadeProjectile : NetworkBehaviour
{
    // ── ค่าที่อาวุธเซ็ตให้ตอน spawn — ไม่ต้องตั้งบน prefab ────────────────
    [HideInInspector] public float   damage;
    [HideInInspector] public bool    cluster;
    [HideInInspector] public bool    isCrit;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public string weaponName = "Unknown";

    [Header("ตัวระเบิดนี้ทำอะไร — ตั้งบน prefab นี้")]
    [Tooltip("รัศมีระเบิด (ค่าฐาน) — อาวุธเป็นคนคูณ Area stat ให้ตอนยิง\n" +
             "อาวุธที่ยังส่งรัศมีของตัวเองมา (> 0) จะเขียนทับค่านี้")]
    public float radius = 3f;
    [Tooltip("เวลาหน่วงก่อนระเบิด (วินาที) — อาวุธที่ส่งค่าของตัวเองมา (> 0) จะเขียนทับ")]
    public float fuseTime = 1.2f;

    [Header("Visual")]
    [Tooltip("ข้ามพูล VFX — ปล่อยว่างไว้ดีกว่า จะได้ใช้ explosionVfxKey ผ่าน NetworkedVFXPool ตามกฎข้อ 2")]
    public GameObject explosionVfxPrefab;

    [Tooltip("คีย์ VFX ตอนระเบิด — ThrowGrenadeServerRpc จะเขียนทับด้วย weaponVfxType ของอาวุธที่ยิง\n" +
             "ถ้าอาวุธตั้งไว้ · ค่าตรงนี้เป็นค่าตั้งต้นเมื่ออาวุธไม่ได้ตั้งอะไร")]
    public string explosionVfxKey = "GrenadeExplosion";

    [Header("Cluster")]
    [Tooltip("สิ่งที่ปล่อยออกมาตอนระเบิด — ชนิดของ prefab เป็นตัวกำหนดพฤติกรรมเอง\n" +
             "• prefab ที่มี GrenadeProjectile → ปล่อย 'ระเบิดลูกใหม่' โค้งไปตกรอบจุดระเบิดแล้วระเบิดซ้ำ\n" +
             "• prefab ที่มี Projectile → ปล่อย 'กระสุนย่อย' พุ่งตรงออก 360 องศา\n" +
             "ปล่อยว่าง = ไม่ปล่อยอะไรเลย")]
    public GameObject clusterProjectilePrefab;
    [Tooltip("จำนวนลูกที่ปล่อย — ใช้ทั้งสองโหมด")]
    public int   clusterPellets    = 8;
    [Tooltip("ดาเมจลูกย่อยเป็น % ของดาเมจลูกแม่")]
    public float clusterDmgPercent = 0.4f;   // % ของ damage หลัก
    [Tooltip("ความเร็วกระสุนย่อย — ใช้เฉพาะโหมด Projectile")]
    public float clusterProjSpeed  = 14f;

    [Header("Cluster — โหมดระเบิดลูกใหม่")]
    [Tooltip("รัศมีที่ลูกใหม่กระจายไปตกรอบจุดระเบิด")]
    public float clusterSpreadRadius = 3f;
    [Tooltip("รัศมีระเบิดของลูกใหม่")]
    public float clusterChildRadius  = 2f;
    [Tooltip("เวลาหน่วงก่อนลูกใหม่ระเบิด")]
    public float clusterChildFuse    = 0.6f;

    private float   timer;
    private float   travelTime;
    private Vector3 startPos;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        startPos   = transform.position;
        travelTime = Mathf.Clamp(Vector3.Distance(startPos, targetPos) / 12f, 0.3f, 2f);
    }

    void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / travelTime);
        // arc trajectory
        float arcHeight = 1.5f;
        Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
        pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);
        transform.position = pos;

        // ระเบิดหลังถึงที่หมายแล้วหน่วงอีก fuseTime — ระหว่างหน่วง t ถูก clamp ไว้ที่ 1
        // ลูกระเบิดจึงนอนนิ่งอยู่ที่ targetPos ตามที่ควรเป็น
        //
        // เดิมตัดสินด้วย travelTime ล้วน **fuseTime ไม่เคยถูกอ่านเลยสักบรรทัด** ทั้งที่ทุกอาวุธ
        // ส่งค่ามาให้ (Shotgun 0.05 · SplitterBomb 1.2 · ClusterBomb 1.0/0.6) — ค่าพวกนั้น
        // ไม่เคยมีผลอะไร และ clusterChildFuse ที่เพิ่งเพิ่มก็ตายด้วยเหตุผลเดียวกัน
        if (timer >= travelTime + fuseTime)
        {
            timer = float.MaxValue;
            Explode();
        }
    }

    void Explode()
    {
        // AoE damage — ใช้ OverlapEnemy (dedupe ต่อ Enemy 1 ตัว) กัน collider ซ้อน (trigger + solid) โดนสองครั้ง
        var cols = PlayerWeaponManager.OverlapEnemy(targetPos, radius);
        foreach (var c in cols)
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                if (weaponManager != null)
                {
                    weaponManager.RegisterWeaponDamage(weaponName, damage);
                }
            }
        }

        // ── Cluster: ปล่อยลูกย่อย ─────────────────────────────────────────
        // ชนิดของ clusterProjectilePrefab เป็นตัวเลือกโหมดเอง ไม่มีสวิตช์แยก
        if (cluster && clusterProjectilePrefab != null)
        {
            if (clusterProjectilePrefab.GetComponent<GrenadeProjectile>() != null)
                SpawnChildGrenades();
            else
                SpawnClusterPellets();
        }

        // VFX (all clients)
        // scale ต้องคำนวณและส่งจากฝั่ง server — radius ถูกเซ็ตเฉพาะฝั่ง server (ไม่ใช่ NetworkVariable)
        // ถ้าไปคำนวณใน ClientRpc จะได้ radius = 0 แล้วภาพหดเป็นศูนย์เงียบๆ
        SpawnVfxClientRpc(targetPos, explosionVfxKey, ComputeExplosionVfxScale());

        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    /// <summary>
    /// ปล่อย "ระเบิดลูกใหม่" กระจายรอบจุดระเบิด — server เท่านั้น (Explode ถูก gate ไว้แล้ว)
    ///
    /// สร้างเองตรงนี้แทนการเรียก ThrowGrenadeServerRpc เพราะ RPC ตัวนั้น resolve prefab จาก
    /// projPrefabId ผ่าน NetworkedVFXPool แล้วถ้าหาไม่เจอจะ fallback ไปหยิบ projectilePrefab
    /// ของอาวุธที่ยิง — ซึ่งคือ "ลูกแม่" ไม่ใช่ลูกที่เราตั้งใจ · prefab ระเบิดทั้งสองตัวในโปรเจกต์นี้
    /// ไม่ได้อยู่ใน projectile registry ของ pool ด้วย (ตรวจแล้ว 2026-08-15) จึงไม่มีทางได้ id ที่ถูก
    /// ตรงนี้เราถือ reference อยู่ในมือแล้วและอยู่ฝั่ง server อยู่แล้ว สร้างตรงๆ จบกว่า
    ///
    /// **ลูกใหม่ตั้ง cluster = false เสมอ** — นี่คือตัวหยุดลูกโซ่ ไม่ใช่ทางเลือก
    /// ถ้าปล่อยให้ลูกใหม่ cluster ต่อได้ และ prefab ลูกชี้กลับมาหาลูกแม่ (ซึ่งข้อมูลตอนนี้เป็นแบบนั้นจริง)
    /// จะได้ระเบิดเพิ่มเป็นทวีคูณจนเกมค้าง
    /// </summary>
    void SpawnChildGrenades()
    {
        float childDmg = damage * clusterDmgPercent;

        for (int i = 0; i < clusterPellets; i++)
        {
            Vector2 rnd      = Random.insideUnitCircle * clusterSpreadRadius;
            Vector3 childPos = targetPos + new Vector3(rnd.x, 0f, rnd.y);

            var go = Instantiate(clusterProjectilePrefab, targetPos + Vector3.up * 0.3f, Quaternion.identity);
            var cg = go.GetComponent<GrenadeProjectile>();
            if (cg != null)
            {
                cg.damage        = childDmg;
                cg.radius        = clusterChildRadius;
                cg.fuseTime      = clusterChildFuse;
                cg.targetPos     = childPos;
                cg.weaponName    = weaponName;
                cg.weaponManager = weaponManager;
                cg.isCrit        = isCrit;   // สืบทอดคริตจากลูกแม่ — โรลเดียวกันทั้งชุด
                cg.cluster       = false;    // หยุดลูกโซ่ที่ชั้นเดียว ห้ามแก้เป็น true
                // สืบทอดคีย์ VFX ด้วย ไม่งั้นลูกใหม่จะกลับไปใช้ค่า default บน prefab ของตัวเอง
                // แล้วอาวุธที่ตั้ง weaponVfxType ไว้จะเห็นลูกแม่เป็นสีหนึ่ง ลูกย่อยเป็นอีกสีหนึ่ง
                cg.explosionVfxKey = explosionVfxKey;
            }

            go.GetComponent<NetworkObject>()?.Spawn(true);
        }
    }

    /// <summary>ปล่อย "กระสุนย่อย" พุ่งตรงออก 360 องศา — โหมดเดิม ใช้เมื่อ prefab ลูกเป็นตระกูล Projectile</summary>
    void SpawnClusterPellets()
    {
        if (weaponManager == null) return;

        int projId = NetworkedVFXPool.Instance != null
            ? NetworkedVFXPool.Instance.GetProjectileId(clusterProjectilePrefab)
            : -1;

        float pelletDmg = damage * clusterDmgPercent;
        for (int i = 0; i < clusterPellets; i++)
        {
            float   angle = (360f / clusterPellets) * i;
            Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            // pellets สืบทอด crit flag จากลูกแม่ — คริตทั้งลูกใหญ่ + ลูกย่อยตรงกันเป็น event เดียว
            weaponManager.FireProjectileServerRpc(
                targetPos + Vector3.up * 0.3f, dir,
                pelletDmg, clusterProjSpeed, 1, 0f,
                projPrefabId: projId, weaponName: weaponName, isCrit: isCrit);
        }
    }

    /// <summary>
    /// ตัวคูณขนาด VFX ให้ตรงกับรัศมีระเบิดจริง — สูตรเดียวกับ WeaponBase.ComputeVfxScale
    /// (radius จริง ÷ designedRadius ที่ prefab ถูกออกแบบมา)
    ///
    /// เดิมไม่ได้ส่ง scale เลย ภาพระเบิดจึงเท่ากันหมดไม่ว่ารัศมีจริงจะเป็นเท่าไหร่ —
    /// ลูกแม่รัศมี 3 ลูกย่อยรัศมี 2 และ Area stat ที่ขยายรัศมี ล้วนแสดงภาพขนาดเดียวกัน
    /// ผู้เล่นจึงอ่านไม่ออกว่าพื้นที่อันตรายกว้างแค่ไหน
    /// </summary>
    float ComputeExplosionVfxScale()
    {
        var pool = NetworkedVFXPool.Instance;
        if (pool == null || radius <= 0f) return 1f;
        float designed = pool.GetDesignedRadius(explosionVfxKey);
        return designed > 0f ? radius / designed : 1f;
    }

    [ClientRpc]
    void SpawnVfxClientRpc(Vector3 pos, string vfxKey, float scale)
    {
        if (explosionVfxPrefab != null)
        {
            // ทางเลี่ยงพูล — เก็บไว้เพื่อ backward compat แต่ prefab ในโปรเจกต์ตั้งเป็นว่างทั้งหมด
            // ถ้ามีคนมาลากใส่ ภาพจะไม่ผ่านพูลและไม่รับ scale ด้วย (ผิดกฎข้อ 2 ใน CLAUDE.md)
            Destroy(Instantiate(explosionVfxPrefab, pos, Quaternion.identity), 3f);
            return;
        }

        VFXFactory.Play(string.IsNullOrEmpty(vfxKey) ? "GrenadeExplosion" : vfxKey, pos, scale);
    }
}
