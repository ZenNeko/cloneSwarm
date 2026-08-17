using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public class Projectile : NetworkBehaviour
{
    public float   speed    = 12f;
    public float   damage   = 20f;
    public float   maxRange = 20f;
    public bool    piercing = false;   // ถ้า true = ไม่ destroy เมื่อชน enemy

    [HideInInspector]
    [Tooltip("อาวุธเป็นคนตั้งตอนยิง · > 0 = ระเบิดเป็นวงตรงจุดที่ชน แทนการตีเฉพาะตัวที่ชน")]
    public float explosionRadius = 0f;

    [Header("VFX")]
    [Tooltip("VFX ที่แสดงเมื่อ projectile ชน\nกำหนด prefab ใน VFXDatabase")]
    [FormerlySerializedAs("hitVFX")]
    [HideInInspector]
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

        if ((transform.position - startPosition).sqrMagnitude >= maxRange * maxRange)
            SafeDespawn();
    }

    // ── Collision ─────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;
        if (!other.CompareTag("Enemy")) return;

        if (explosionRadius > 0f)
        {
            // ระเบิดแทนการตีตัวเดียว — ตัวที่ชนอยู่ในรัศมีอยู่แล้วจึงโดนด้วย
            // ไม่ตีตรงซ้ำเพื่อไม่ให้ตัวที่ชนกินดาเมจสองเด้ง
            ExplodeOnImpact();
        }
        else
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(damage, isCrit);
                if (ownerManager != null)
                {
                    ownerManager.RegisterWeaponDamage(weaponName, damage);
                }
            }
        }
        ShowHitVfxClientRpc(transform.position, isCrit, hitVFX);
        if (!piercing) SafeDespawn();
    }

    /// <summary>
    /// AoE ตอนกระสุนกระทบ — server เท่านั้น (OnTriggerEnter ถูก gate ไว้แล้ว)
    ///
    /// ใช้กับกระสุนที่ต้อง "ระเบิดตรงที่ชน" เช่น Blunderbuss · ต่างจากการโยนระเบิดโค้ง
    /// ตรงที่จุดระเบิดมาจากการชนจริง ไม่ใช่พิกัดที่คำนวณไว้ล่วงหน้า
    ///
    /// ใช้ OverlapEnemy ที่ de-dup ต่อ Enemy ให้แล้ว — ศัตรูที่มี collider หลายตัวจึงไม่กินสองเด้ง
    /// </summary>
    void ExplodeOnImpact()
    {
        Vector3 center = transform.position;
        foreach (var c in PlayerWeaponManager.OverlapEnemy(center, explosionRadius))
        {
            var e = c.GetComponent<Enemy>();
            if (e == null) continue;

            e.EnemyTakeDamage(damage, isCrit);
            if (ownerManager != null) ownerManager.RegisterWeaponDamage(weaponName, damage);
        }
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }

    [ClientRpc]
    void ShowHitVfxClientRpc(Vector3 pos, bool crit, string vfxKey)
    {
        // ถ้าเป็นคริติคอล หรือ hitVFX เป็นค่าเริ่มต้น (HitEffect)
        // จะถูกแสดงผ่านระบบ Enemy.NotifyHitClientRpc โดยอัตโนมัติอยู่แล้ว เพื่อหลีกเลี่ยงการทำซ้ำ
        if (crit || vfxKey == "HitEffect" || string.IsNullOrEmpty(vfxKey)) return;
        VFXFactory.Play(vfxKey, pos);
    }
}
