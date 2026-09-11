using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MolotovProjectile — NetworkObject สำหรับระเบิดขวดไฟ
/// โยนโค้งไปยัง targetPos เมื่อถึงจะระเบิดทำดาเมจเริ่มแรก และสร้างพื้นที่ไฟเผาต่อเนื่อง (Damage Zone)
/// </summary>
public class MolotovProjectile : NetworkBehaviour
{
    [HideInInspector] public float   damage;
    [HideInInspector] public float   radius;
    [HideInInspector] public float   fuseTime;
    [HideInInspector] public Vector3 targetPos;
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public bool    isCrit;

    [Header("Zone Configuration")]
    public int   zoneTicks          = 5;
    public float zoneTickInterval   = 1.0f;
    public float zoneDamagePercent  = 0.4f; // ความแรงของแต่ละติ๊กเมื่อเทียบกับดาเมจหลัก
    [HideInInspector]
    public string zoneVfxKey        = "O_AoE_RadiantAura";
    [HideInInspector]
    public string explosionVfxKey   = "GrenadeExplosion";

    [Tooltip("หน่วงก่อนกองไฟโผล่ (วินาที) - 0 = พร้อมระเบิดเลย\nตั้งไว้ราว 0.3-0.4 ถ้า explosionVfxKey กับ zoneVfxKey เป็นวงคล้ายกัน\nไม่งั้นจะเห็นเป็นสองวงซ้อนกันตอนระเบิด")]
    public float  zoneVfxDelay      = 0f;

    [Header("Super (Napalm) Settings")]
    [Tooltip("จำนวนกองไฟย่อยที่กระจายออกรอบวง (0 = ไม่กระจายย่อย)")]
    public int   childPoolsCount    = 0;
    public float childPoolOffset    = 2.5f;

    [Header("Burn DOT (Napalm Only or Custom)")]
    [Tooltip("ระยะเวลาเผาไหม้ต่อหลังจากเดินออกจากวงไฟ (วินาที, 0 = ไม่มี DOT)")]
    public float burnDuration       = 0f;
    [Tooltip("ความแรงของแต่ละติ๊ก DOT (เทียบกับความแรงติ๊กในวงปกติ เช่น 0.5 = ดาเมจครึ่งหนึ่งของติ๊กวง)")]
    public float burnDamageRatio    = 0.5f;
    [Tooltip("ความถี่ของติ๊ก DOT (วินาที)")]
    public float burnTickInterval   = 1.0f;

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

        // ระเบิดหลังถึงที่หมายแล้วหน่วงอีก fuseTime — เดิมอ่านแค่ travelTime
        // fuseTime จึงเป็นฟิลด์ตายเหมือนกันกับใน GrenadeProjectile (บั๊กเดียวกันถูกคัดลอกมาสองที่)
        if (timer >= travelTime + fuseTime)
        {
            timer = float.MaxValue;
            Explode();
        }
    }

    void Explode()
    {
        // 1. ดาเมจแรกระเบิดลงพื้น
        // ใช้ OverlapEnemy แทน OverlapSphere ตรงๆ — กัน enemy ที่มีหลาย Collider (เช่น TargetDummy)
        // โดนดาเมจซ้ำจากการ query เจอ collider มากกว่าหนึ่งชิ้นของตัวเดียวกัน
        foreach (var c in PlayerWeaponManager.OverlapEnemy(targetPos, radius))
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

        // 2. สร้างพื้นที่เพลิงเผาไหม้ต่อเนื่อง (Damage Zone)
        if (weaponManager != null)
        {
            float tickDmg   = damage * zoneDamagePercent;
            float burnTickDmg = tickDmg * burnDamageRatio;
            weaponManager.SpawnDamageZone(targetPos, zoneTicks, zoneTickInterval, radius, tickDmg, isCrit, weaponName, zoneVfxKey, burnDuration, burnTickDmg, burnTickInterval, zoneVfxDelay);

            // กระจายกองไฟย่อยรอบจุดตก (เช่น สำหรับ Napalm Bomb)
            if (childPoolsCount > 0)
            {
                for (int i = 0; i < childPoolsCount; i++)
                {
                    float angle = (360f / childPoolsCount) * i;
                    Vector3 offsetDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    Vector3 childPos = targetPos + offsetDir * childPoolOffset;
                    // ความแรงและขนาดของกองย่อยเป็น 70% ของตัวแม่
                    weaponManager.SpawnDamageZone(childPos, zoneTicks, zoneTickInterval, radius * 0.7f, tickDmg * 0.7f, isCrit, weaponName, zoneVfxKey, burnDuration, burnTickDmg * 0.7f, burnTickInterval, zoneVfxDelay);
                }
            }
        }

        // 3. ปล่อย VFX ระเบิดขวดไฟ
        SpawnVfxClientRpc(targetPos, explosionVfxKey);

        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void SpawnVfxClientRpc(Vector3 pos, string vfxKey)
    {
        // เล่นเอฟเฟกต์ระเบิดขวดไฟตาม key ที่ระบุ (ถ้ามีใน Database) หรือเล่น GrenadeExplosion เป็น fallback
        string activeVfx = (NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.HasMapping(vfxKey))
            ? vfxKey
            : "GrenadeExplosion";

        VFXFactory.Play(activeVfx, pos);
    }
}
