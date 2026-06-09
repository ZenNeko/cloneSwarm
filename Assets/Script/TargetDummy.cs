using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Stationary test dummy สำหรับ weapon test scene
/// — เพิ่มต่อยอด `Enemy.cs` แต่:
///   • ไม่ขยับ (suppressDefaultMovement = true)
///   • ไม่ทำดาเมจ (contactDamage = 0)
///   • HP สูงมาก (default 999999)
///   • Auto-respawn เมื่อตาย (configurable)
///   • แสดง floating damage text (optional)
///
/// **Setup:**
///   1. duplicate enemy prefab → ตั้ง TargetDummy.cs (require Enemy.cs)
///   2. ตั้ง maxHealth สูงๆ (เช่น 999999)
///   3. วางใน WeaponTestScene
/// </summary>
[RequireComponent(typeof(Enemy))]
public class TargetDummy : NetworkBehaviour
{
    [Header("Respawn")]
    [Tooltip("Respawn dummy หลังตายกี่วินาที — 0 = ไม่ respawn")]
    public float respawnDelay = 2f;

    [Header("Damage Display (optional)")]
    [Tooltip("Floating damage text prefab — แสดง dmg ทุกครั้งโดน (ปล่อยว่างได้)")]
    public GameObject damageTextPrefab;

    [Header("DPS Counter (write to test manager)")]
    [Tooltip("ลงทะเบียน damage taken ใน WeaponTestManager.TotalDamage")]
    public bool registerDamageToManager = true;

    Enemy _enemy;
    Vector3 _spawnPos;
    Quaternion _spawnRot;
    float _spawnMaxHealth;

    void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _enemy.suppressDefaultMovement = true;
        _enemy.contactDamage = 0f;
        _spawnPos = transform.position;
        _spawnRot = transform.rotation;
        _spawnMaxHealth = _enemy.maxHealth;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        _enemy.netHealth.OnValueChanged += OnHealthChanged;
        _enemy.onDeath.AddListener(HandleDeath);
    }

    public override void OnNetworkDespawn()
    {
        if (_enemy != null)
        {
            _enemy.netHealth.OnValueChanged -= OnHealthChanged;
            _enemy.onDeath.RemoveListener(HandleDeath);
        }
    }

    void OnHealthChanged(float oldHp, float newHp)
    {
        float dmg = oldHp - newHp;
        if (dmg <= 0f) return;

        // Notify DPS counter
        if (registerDamageToManager && WeaponTestManager.Instance != null)
            WeaponTestManager.Instance.AddDamage(dmg);

        // Floating text (visual — runs on all clients via RPC ในอนาคต ถ้าต้องการ)
        if (damageTextPrefab != null)
        {
            var t = Instantiate(damageTextPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
            var label = t.GetComponentInChildren<TMPro.TextMeshPro>();
            if (label != null) label.text = Mathf.CeilToInt(dmg).ToString();
            Destroy(t, 1f);
        }
    }

    void HandleDeath()
    {
        if (!IsServer) return;
        if (respawnDelay <= 0f) return;
        StartCoroutine(RespawnAfter(respawnDelay));
    }

    IEnumerator RespawnAfter(float t)
    {
        // Wait while object still spawned — death calls Despawn so we need
        // to re-spawn a new instance at the same position. ใช้ NetworkObject pooling
        // หรือ Instantiate ใหม่ขึ้นกับ project setup
        yield return new WaitForSeconds(t);

        // Simple approach: เก็บ prefab ref ใน manager + spawn ที่ตำแหน่งเดิม
        WeaponTestManager.Instance?.RespawnDummy(_spawnPos, _spawnRot, _spawnMaxHealth);
    }
}
