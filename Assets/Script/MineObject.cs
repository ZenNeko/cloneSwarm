using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MineObject — NetworkObject
/// วางบนพื้น ระเบิดเมื่อศัตรูเข้า triggerRadius หรือหมดอายุ
///
/// Prefab setup:
///   • NetworkObject component
///   • MineObject script
///   (ไม่ต้องมี Collider ใน scene — ใช้ OverlapSphere check)
/// </summary>
public class MineObject : NetworkBehaviour
{
    [HideInInspector] public float damage;
    [HideInInspector] public float triggerRadius = 1.5f;
    [HideInInspector] public string weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager weaponManager;
    [HideInInspector] public bool  isCrit;

    [Header("Settings")]
    public float lifetime   = 15f;   // หมดอายุ (วินาที)
    public float checkRate  = 0.2f;  // ตรวจทุก X วินาที

    [Header("Visual")]
    [Tooltip("ถ้าว่าง — ใช้ GrenadeExplosion จาก NetworkedVFXPool แทน")]
    public GameObject explodeVfxPrefab;

    private float lifeTimer;
    private float checkTimer;
    private bool  exploded;
    private static readonly Collider[] _overlapBuffer = new Collider[64];
    // reuse ตัวเดียวข้าม mine ทุกลูก + เคลียร์ก่อนใช้ทุกครั้ง — กัน enemy ที่มีหลาย Collider
    // (เช่น TargetDummy) โดนดาเมจซ้ำ โดยไม่เพิ่ม garbage ต่อเฟรมเหมือนที่ OverlapSphereNonAlloc ตั้งใจไว้เดิม
    private static readonly HashSet<Enemy> _hitEnemies = new HashSet<Enemy>();

    void Update()
    {
        if (!IsServer || exploded) return;

        lifeTimer  += Time.deltaTime;
        checkTimer += Time.deltaTime;

        if (lifeTimer >= lifetime)
        {
            Detonate(); return;
        }

        if (checkTimer < checkRate) return;
        checkTimer = 0f;

        var mask = LayerMask.GetMask("Enemy");
        int count = Physics.OverlapSphereNonAlloc(transform.position, triggerRadius, _overlapBuffer, mask);
        if (count > 0) Detonate();
    }

    void Detonate()
    {
        if (exploded) return;
        exploded = true;

        var mask = LayerMask.GetMask("Enemy");
        int count = Physics.OverlapSphereNonAlloc(transform.position, triggerRadius * 2f, _overlapBuffer, mask);

        // pass ดาเมจจริง — ต้อง de-dup ต่อ Enemy (ไม่ใช่ต่อ Collider) ก่อนตี
        _hitEnemies.Clear();
        for (int i = 0; i < count; i++)
        {
            var c = _overlapBuffer[i];
            if (c == null) continue;
            var enemy = c.GetComponent<Enemy>();
            if (enemy == null || !_hitEnemies.Add(enemy)) continue;

            enemy.EnemyTakeDamage(damage, isCrit);
            if (weaponManager != null)
            {
                weaponManager.RegisterWeaponDamage(weaponName, damage);
            }
        }

        ExplodeClientRpc(transform.position);
        if (NetworkObject.IsSpawned) NetworkObject.Despawn(true);
    }

    [ClientRpc]
    void ExplodeClientRpc(Vector3 pos)
    {
        if (explodeVfxPrefab != null)
            Destroy(Instantiate(explodeVfxPrefab, pos, Quaternion.identity), 3f);
        else
            VFXFactory.Play("GrenadeExplosion", pos);
    }
}
