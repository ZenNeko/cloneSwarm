using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Hunter Q — Homing Missile
/// Server-side movement: เคลื่อนที่หา target (ulong NetworkObjectId)
/// เมื่อถึง target หรือ target ตาย → ระเบิด AoE
///
/// Visual offset: ตั้ง rotation บน prefab ได้เลย (เช่น x=90 สำหรับ mesh ตั้งตรง)
/// _flyDir แยกจาก transform.forward — ไม่ถูกปนเปื้อนจาก prefab offset
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MissileProjectile : NetworkBehaviour
{
    [Header("Missile Config")]
    public float moveSpeed       = 14f;
    public float turnSpeed       = 180f;  // องศา/วินาที
    public float arrivalDistance = 0.8f;

    // ── Runtime ───────────────────────────────────────────────────────────
    [HideInInspector] public float damage;
    [HideInInspector] public float explosionRadius;
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager weaponManager;

    private ulong     targetNetId;
    private Transform targetTransform;
    private bool      hasExploded;
    private float     maxLifetime = 8f;
    private float     lifeTimer;

    // ทิศบินอิสระ — ไม่ขึ้นกับ transform.forward (ซึ่งถูกปนด้วย prefab x-offset)
    private Vector3    _flyDir;
    // prefab visual offset — ใส่บน root หรือ child mesh ก็ได้
    private Quaternion _prefabRotOffset;

    void Awake()
    {
        // บันทึก visual offset ก่อนใครแตะ rotation
        _prefabRotOffset = transform.localRotation;
        // เริ่มบินไปข้างหน้าก่อน (จะ home หา target ทันทีที่ Update รัน)
        _flyDir = Vector3.forward;
    }

    // ── Init ──────────────────────────────────────────────────────────────
    public void Init(ulong netId, float dmg, float radius)
    {
        targetNetId     = netId;
        damage          = dmg;
        explosionRadius = radius;
    }

    // ── Update (Server only) ──────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || hasExploded) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetime) { Explode(transform.position); return; }

        // Lazy-find target
        if (targetTransform == null)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(targetNetId, out var netObj))
                targetTransform = netObj.transform;
        }

        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            Explode(transform.position);
            return;
        }

        Vector3 toTarget = targetTransform.position - transform.position;

        if (toTarget.sqrMagnitude <= arrivalDistance * arrivalDistance)
        {
            Explode(targetTransform.position);
            return;
        }

        // ── Homing — ใช้ _flyDir ไม่ใช้ transform.forward ──────────────────
        Vector3 desiredDir = toTarget.normalized;
        _flyDir = Vector3.RotateTowards(_flyDir, desiredDir,
                      turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        // Visual: LookRotation ตาม _flyDir แล้วคูณ prefab offset
        // → mesh หันถูกทิศ + ยัง offset ได้ตามที่ตั้งใน prefab
        transform.rotation  = Quaternion.LookRotation(_flyDir) * _prefabRotOffset;

        // Movement: _flyDir โดยตรง (ไม่ใช้ transform.forward ที่ถูกปนด้วย offset)
        transform.position += _flyDir * moveSpeed * Time.deltaTime;
    }

    // ── Explode ───────────────────────────────────────────────────────────
    void Explode(Vector3 center)
    {
        if (hasExploded) return;
        hasExploded = true;

        var cols = PlayerWeaponManager.OverlapEnemy(center, explosionRadius);
        foreach (var c in cols)
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage);
                if (weaponManager != null)
                {
                    weaponManager.RegisterWeaponDamage(weaponName, damage);
                }
            }
        }

        ShowExplosionClientRpc(center);
        GetComponent<NetworkObject>()?.Despawn(true);
    }

    [ClientRpc]
    void ShowExplosionClientRpc(Vector3 pos)
    {
        // ถอด HitEffect ซ้ำซ้อนออก เหลือเพียงระเบิดลูกใหญ่
        VFXFactory.Play("GrenadeExplosion", pos);
    }
}
