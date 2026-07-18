using Unity.Netcode;
using UnityEngine;

/// <summary>
/// BouncingSpikeProjectile — NetworkObject กระสุนหนามแหลมทะลวง
/// บินตรง เด้งกับกำแพง (wallLayer) ได้ N ครั้ง และทะลวงผ่านศัตรู (piercing)
/// ถ้าเป็นรุ่น Super เมื่อชนกำแพงครั้งแรกจะสปลิต (แยก) ออกเป็น 2 ลูก (+30° / -30°)
/// </summary>
public class BouncingSpikeProjectile : NetworkBehaviour
{
    public float     speed       = 14f;
    public float     damage      = 25f;
    public float     maxRange    = 25f;
    public bool      piercing    = true;
    public LayerMask wallLayer   = ~0; // default: ทุก layer
    public int       maxBounces  = 3;

    [Header("Super Split Settings")]
    public bool       isSuper;
    [Tooltip("Prefab ของตัวเอง เพื่อใช้สร้างกระสุนแยกร่างเมื่อชนกำแพง")]
    public GameObject selfPrefab;
    [HideInInspector] public bool hasSplit; // ป้องกันการแยกร่างซ้ำซ้อนจนเกิดสไปค์ล้นจอ

    [HideInInspector]
    public string hitVfxKey = "HitEffect";

    [HideInInspector] public bool   isCrit;
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager ownerManager;

    private Vector3 moveDirection;
    private Vector3 startPosition;
    private int     bounceCount;

    public void Init(Vector3 direction)
    {
        moveDirection = direction.normalized;
        startPosition = transform.position;
        bounceCount   = 0;
        
        // หมุนตัวให้ชี้ไปตามทิศทางเคลื่อนที่
        if (moveDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDirection);
    }

    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        float moveDist = speed * Time.deltaTime;
        Vector3 nextPos = transform.position + moveDirection * moveDist;

        // ตรวจสอบสิ่งกีดขวาง/กำแพง ข้างหน้า
        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDist + 0.1f, wallLayer, QueryTriggerInteraction.Ignore))
        {
            // เด้งกลับทิศทาง
            moveDirection = Vector3.Reflect(moveDirection, hit.normal);
            moveDirection.y = 0f;
            moveDirection.Normalize();

            // ปรับตำแหน่งและองศาให้หมุนตามทิศใหม่
            transform.position = hit.point + moveDirection * 0.1f;
            if (moveDirection != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(moveDirection);

            bounceCount++;

            // ตรรกะแยกตัว (Super Version)
            if (isSuper && !hasSplit && selfPrefab != null && ownerManager != null)
            {
                // แยกตัวเป็น 2 ลูก ทำมุมเฉียง +30 และ -30 องศาจากแนวเด้งใหม่
                SpawnSplitSpike(moveDirection, +30f, hit.point);
                SpawnSplitSpike(moveDirection, -30f, hit.point);

                // Despawn ตัวแม่
                SafeDespawn();
                return;
            }

            if (bounceCount >= maxBounces)
            {
                SafeDespawn();
                return;
            }
        }
        else
        {
            transform.position = nextPos;
        }

        // ตรวจสอบระยะยิงสูงสุด
        if ((transform.position - startPosition).sqrMagnitude >= maxRange * maxRange)
        {
            SafeDespawn();
        }
    }

    void SpawnSplitSpike(Vector3 baseDir, float angleOffset, Vector3 spawnPoint)
    {
        Vector3 splitDir = Quaternion.Euler(0f, angleOffset, 0f) * baseDir;
        var go = Instantiate(selfPrefab, spawnPoint + splitDir * 0.2f, Quaternion.LookRotation(splitDir));
        
        var proj = go.GetComponent<BouncingSpikeProjectile>();
        if (proj != null)
        {
            proj.damage        = damage * 0.8f; // ลดความแรงตัวแยกลงเล็กน้อยเพื่อความสมดุล
            proj.speed         = speed;
            proj.maxRange      = maxRange * 0.8f;
            proj.piercing      = piercing;
            proj.wallLayer     = wallLayer;
            proj.maxBounces    = maxBounces - bounceCount; // กระสุนแยกจะเด้งได้เท่ากับจำนวนครั้งที่เหลือ
            proj.isSuper       = isSuper;
            proj.hasSplit      = true; // ตัวลูกจะไม่แยกร่างซ้ำอีก
            proj.ownerManager  = ownerManager;
            proj.isCrit        = isCrit;
            proj.weaponName    = weaponName;
            proj.hitVfxKey     = hitVfxKey;
        }

        var no = go.GetComponent<NetworkObject>();
        if (no != null)
        {
            no.Spawn(true);
            proj.Init(splitDir);
        }
        else
        {
            Destroy(go);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;
        if (!other.CompareTag("Enemy")) return;

        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage, isCrit);
            if (ownerManager != null)
            {
                ownerManager.RegisterWeaponDamage(weaponName, damage);
            }
        }

        // เอฟเฟกต์ชนศัตรู (ส่งไปวาดที่ Client ทุกเครื่อง)
        ShowHitVfxClientRpc(transform.position, isCrit, hitVfxKey);

        if (!piercing)
        {
            SafeDespawn();
        }
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void ShowHitVfxClientRpc(Vector3 pos, bool crit, string vfxKey)
    {
        // หากเป็นคริติคอล หรือใช้เอฟเฟกต์เริ่มต้น HitEffect จะถูกเล่นโดย Enemy.cs อัตโนมัติ เพื่อป้องกันการวาดซ้ำ
        if (crit || vfxKey == "HitEffect" || string.IsNullOrEmpty(vfxKey)) return;
        VFXFactory.Play(vfxKey, pos);
    }
}
