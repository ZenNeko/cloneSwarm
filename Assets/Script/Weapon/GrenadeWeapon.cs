using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grenade — โยน grenade ไปสุ่มตำแหน่งรอบ player → ระเบิด AoE
/// ใช้ local coroutine + visual GameObject (ไม่ต้องการ NetworkObject prefab)
/// Damage ผ่าน FireMeleeServerRpc เมื่อถึง fuseTime
/// </summary>
public class GrenadeWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิด (scale ตาม AreaSize stat)")]
    public float explosionRadius = 3f;
    [Tooltip("เวลาบินก่อนระเบิด (วินาที)")]
    public float fuseTime        = 1.5f;
    [Tooltip("Prefab visual ของ grenade (ต้องมี NetworkObject + GrenadeProjectile script)")]
    public GameObject grenadePrefab;

    protected override GameObject GetProjectilePrefab() => grenadePrefab;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector2 rnd       = Random.insideUnitCircle.normalized * (ld.range * Random.Range(0.4f, 1f));
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            
            // โยนระเบิดผ่านเซิร์ฟเวอร์แบบซิงก์เน็ตเวิร์ก (จะไปเกิดเป็น GrenadeProjectile ตัวจริง)
            ThrowGrenade(spawnPos, targetPos, dmg, radius, fuseTime, cluster: false, isCrit: isCrit);
        }
    }
}
