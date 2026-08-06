using Unity.Netcode;
using UnityEngine;

/// <summary>
/// วัตถุทำลายได้สำหรับ ZoneObjective quest "DestroyObjects"
/// (Crush the Statues ของ The Spell Brigade / Vase Destruction ของ LoL Swarm)
///
/// ทำไมต้องพ่วง <see cref="Enemy"/>:
///   ทุกอาวุธในเกมนี้เล็งด้วย LayerMask "Enemy" แล้วเรียก Enemy.EnemyTakeDamage()
///   ถ้าทำเป็น component ใหม่ที่ไม่ใช่ Enemy จะต้องแก้อาวุธ ~25 ตัว
///   → ใช้ Enemy ที่ปิด movement/contact damage แทน อาวุธทุกตัวยิงโดนฟรี
///
/// การนับ: ZoneObjective subscribe Enemy.OnEnemyDiedServer แล้วเช็คว่า
/// NetworkObject ที่ตายอยู่ใน set ที่ตัวเอง spawn ไว้ไหม — component นี้ไม่นับเอง
///
/// Setup prefab:
///   1. GameObject ใหม่ + layer = "Enemy" (จำเป็น — ไม่งั้นอาวุธไม่เห็น)
///   2. Enemy.cs + Rigidbody + Collider (ไม่ใช่ trigger) + NetworkObject
///   3. ใส่ script นี้
///   4. Enemy: expOrbPrefab = null (ไม่ให้ดรอป exp), expReward = 0
///   5. ลงทะเบียนใน Assets/DefaultNetworkPrefabs.asset
/// </summary>
[RequireComponent(typeof(Enemy))]
public class DestructibleObjective : NetworkBehaviour
{
    // ── Static Events (UI subscribe เพื่อแสดง indicator) ─────────────────
    public static event System.Action<DestructibleObjective> OnDestructibleSpawned;
    public static event System.Action<DestructibleObjective> OnDestructibleDespawned;

    [Header("Health")]
    [Tooltip("HP ของวัตถุ — ZoneObjective เขียนทับค่านี้ก่อนเรียก Spawn()")]
    public float health = 60f;

    [Header("Drops")]
    [Tooltip("ให้วัตถุดรอป exp orb ตอนแตกไหม (ปิด = ได้แค่รางวัลตอนจบ quest)")]
    public bool dropExpOrb = false;

    public override void OnNetworkSpawn()
    {
        var enemy = GetComponent<Enemy>();
        if (enemy != null)
        {
            // ปิดพฤติกรรม enemy ปกติ — วัตถุนี้อยู่กับที่และไม่ทำดาเมจ
            enemy.suppressDefaultMovement = true;
            enemy.speed         = 0f;
            enemy.contactDamage = 0f;
            enemy.damageRadius  = 0f;   // กัน Enemy.Update ยิง TakeDamage(0) ใส่ผู้เล่นทุก tick

            if (IsServer)
            {
                // เขียนทั้ง field และ NetworkVariable เพราะลำดับ OnNetworkSpawn
                // ระหว่าง component บน GameObject เดียวกันไม่การันตี:
                //   - Enemy รันทีหลัง → อ่าน maxHealth ที่เราเพิ่งตั้ง
                //   - Enemy รันไปแล้ว → NetworkVariable ที่เราเขียนทับกลับมาถูก
                enemy.maxHealth          = health;
                enemy.netMaxHealth.Value = health;
                enemy.netHealth.Value    = health;

                if (!dropExpOrb) enemy.expOrbPrefab = null;
            }
        }

        // Enemy ตั้ง isKinematic = false ใน OnNetworkSpawn ของตัวเอง แต่ไม่แตะ
        // constraints → freeze ตรงนี้ปลอดภัยไม่ว่าใครรันก่อน กัน enemy ดันวัตถุลอย
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.constraints = RigidbodyConstraints.FreezeAll;

        OnDestructibleSpawned?.Invoke(this);
    }

    public override void OnNetworkDespawn()
    {
        OnDestructibleDespawned?.Invoke(this);
    }
}
