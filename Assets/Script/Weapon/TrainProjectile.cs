using Unity.Netcode;
using UnityEngine;

/// <summary>
/// TrainProjectile — NetworkObject สำหรับสปอว์นรถไฟ (Final City Transit / FC Limited Express)
/// วิ่งตรงทะลวงผ่านศัตรู หากเป็นรุ่น Super (FC Limited Express) จะเกิดระเบิดตามทาง กระแทกศัตรูลอย (Knock Up) และศัตรูตายมีโอกาสดรอปเหรียญทอง (EXP 3 เท่า)
/// </summary>
public class TrainProjectile : NetworkBehaviour
{
    public float speed    = 10f;
    public float damage   = 80f;
    public float maxRange = 20f;
    public bool  piercing = true;

    [Header("Super (FC Limited Express) Settings")]
    public bool  isSuper;
    public float explosionRadius   = 2.0f;
    public float explosionInterval = 0.25f;
    public float knockUpForce      = 6f;
    public float goldDropChance    = 0.25f;

    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public bool   isCrit;
    [HideInInspector] public PlayerWeaponManager ownerManager;

    private Vector3 moveDirection;
    private Vector3 startPosition;
    private float   explosionTimer;

    public void Init(Vector3 direction)
    {
        moveDirection = direction.normalized;
        startPosition = transform.position;
        explosionTimer = 0f;
    }

    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        // เคลื่อนที่ตรงไปข้างหน้า
        transform.position += moveDirection * speed * Time.deltaTime;

        // ตรวจสอบระยะทางสิ้นสุด
        if ((transform.position - startPosition).sqrMagnitude >= maxRange * maxRange)
        {
            SafeDespawn();
            return;
        }

        // ปล่อยระเบิดตามหลังหากเป็นร่าง Super
        if (isSuper)
        {
            explosionTimer += Time.deltaTime;
            if (explosionTimer >= explosionInterval)
            {
                explosionTimer = 0f;
                TriggerWakeExplosion(transform.position);
            }
        }
    }

    void TriggerWakeExplosion(Vector3 position)
    {
        // 1. เล่นเอฟเฟกต์ระเบิดให้กับ Client ทุกคน
        if (ownerManager != null)
        {
            ownerManager.BroadcastVfxTypeClientRpc(position, "GrenadeExplosion", 1f);
        }

        // 2. คำนวณความเสียหายแบบ AoE บนเซิร์ฟเวอร์
        float expDmg = damage * 0.5f; // ดาเมจระเบิดตามหลังเป็น 50% ของดาเมจชนตรงๆ
        var mask = LayerMask.GetMask("Enemy");
        foreach (var c in Physics.OverlapSphere(position, explosionRadius, mask))
        {
            var enemy = c.GetComponent<Enemy>();
            if (enemy != null)
            {
                float hpBefore = enemy.GetCurrentHealth();
                enemy.EnemyTakeDamage(expDmg, isCrit);
                if (ownerManager != null)
                {
                    ownerManager.RegisterWeaponDamage(weaponName, expDmg);
                }

                // สุ่มดรอปทอง (EXP 3 เท่า) เมื่อศัตรูตาย
                if (hpBefore > 0f && enemy.GetCurrentHealth() <= 0f)
                {
                    RollGoldDrop(enemy);
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;
        if (!other.CompareTag("Enemy")) return;

        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            float hpBefore = enemy.GetCurrentHealth();
            enemy.EnemyTakeDamage(damage, isCrit);
            if (ownerManager != null)
            {
                ownerManager.RegisterWeaponDamage(weaponName, damage);
            }

            if (isSuper)
            {
                // กระแทกลอยขึ้นฟ้า (Knock Up)
                enemy.ApplyKnockUp(knockUpForce, 0.8f);

                // สุ่มดรอปทองเมื่อศัตรูตาย
                if (hpBefore > 0f && enemy.GetCurrentHealth() <= 0f)
                {
                    RollGoldDrop(enemy);
                }
            }
        }

        if (!piercing) SafeDespawn();
    }

    void RollGoldDrop(Enemy enemy)
    {
        if (Random.value <= goldDropChance)
        {
            if (enemy.expOrbPrefab != null)
            {
                // ดรอปเหรียญทอง (ใช้ expOrbPrefab สปอว์นแล้วคูณ EXP 3 เท่าเพื่อแทนค่าทอง)
                GameObject goldCoin = Instantiate(enemy.expOrbPrefab, enemy.transform.position, Quaternion.identity);
                goldCoin.GetComponent<NetworkObject>()?.Spawn(true);
                var expOrb = goldCoin.GetComponent<ExpOrb>();
                if (expOrb != null)
                {
                    expOrb.SetExpAmount(enemy.expReward * 3f);
                }
                Debug.Log($"[FC Limited Express] 💰 Gold drop triggered (Spawned 3x EXP Gold Orb) at {enemy.transform.position}");
            }
        }
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
