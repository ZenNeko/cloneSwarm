using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class Projectile : NetworkBehaviour
{
    public float   speed    = 12f;
    public float   damage   = 20f;
    public float   maxRange = 20f;
    public bool    piercing = false;   // ถ้า true = ไม่ destroy เมื่อชน enemy

    [Header("VFX")]
    [Tooltip("VFX ที่แสดงเมื่อ projectile ชน\nกำหนด prefab ใน VFXDatabase")]
    [FormerlySerializedAs("hitVFX")]
    [VFXKey]
    public string hitVFX = "HitEffect";

    [HideInInspector]
    public bool isCrit;   // set by weapon → ถ้า true จะแสดง CritHitEffect แทน

    [HideInInspector]
    public string weaponName = "Unknown";
    [HideInInspector]
    public PlayerWeaponManager ownerManager;

    private Transform target;
    private Vector3   moveDirection;
    private Vector3   startPosition;

    // ── Init ──────────────────────────────────────────────────────────────
    /// <summary>Homing mode — ติดตาม target</summary>
    public void Init(Transform t)
    {
        target        = t;
        moveDirection = Vector3.zero;
        startPosition = transform.position;
    }

    /// <summary>Direction mode — บินตรง ไม่ homing</summary>
    public void InitDirection(Vector3 direction)
    {
        moveDirection = direction.normalized;
        target        = null;
        startPosition = transform.position;
    }

    // ── Update: Server only ───────────────────────────────────────────────
    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection * speed * Time.deltaTime;
        }
        else
        {
            if (target == null) { SafeDespawn(); return; }
            // ล็อค Y ให้อยู่ระดับเดิม — ป้องกัน homing ดิ่งลงพื้นเมื่อ enemy อยู่ต่ำกว่า spawnPos
            Vector3 targetFlat = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetFlat, speed * Time.deltaTime);
        }

        if (Vector3.Distance(startPosition, transform.position) >= maxRange)
            SafeDespawn();
    }

    // ── Collision ─────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;
        if (!other.CompareTag("Enemy")) return;

        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.EnemyTakeDamage(damage);
            if (ownerManager != null)
            {
                ownerManager.RegisterWeaponDamage(weaponName, damage);
            }
        }
        ShowHitVfxClientRpc(transform.position, isCrit);
        if (!piercing) SafeDespawn();
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowHitVfxClientRpc(Vector3 pos, bool crit)
        => VFXFactory.Play(crit ? "CritHitEffect" : hitVFX, pos);
}
