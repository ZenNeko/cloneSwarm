using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Magic Missile — Homing Missile ที่ Slow enemy + Evo: โอกาส Freeze
///
/// กลไก:
///   • OnFire → สร้าง homing missiles (local visual + coroutine)
///   • Missile ติดตาม enemy ใกล้สุด → เมื่อถึงเป้า: FireMelee + ApplySlowDebuff
///   • Evo: เมื่อ hit → roll chance → ApplyFreeze
///
/// Level data แนะนำ:
///   Lv1: dmg=25,  cd=2.0s, count=2, range=10, speed=12
///   Lv2: dmg=30,  cd=1.8s, count=2, range=11, speed=13
///   Lv3: dmg=35,  cd=1.6s, count=3, range=12, speed=14
///   Lv4: dmg=45,  cd=1.4s, count=3, range=13, speed=15
///   Lv5: dmg=55,  cd=1.2s, count=4, range=14, speed=16
/// </summary>
public class MagicMissileWeapon : WeaponBase
{
    [Header("Magic Missile Settings")]
    [Tooltip("Prefab visual ของ missile (ต้องมี NetworkObject + MagicMissileProjectile script)")]
    public GameObject missilePrefab;

    [Tooltip("รัศมี AoE เมื่อ missile กระทบ")]
    public float impactRadius = 1.5f;

    [Header("Slow Debuff")]
    [Tooltip("ลดความเร็ว enemy กี่ % (0.5 = ลดเหลือ 50%)")]
    public float slowPercent = 0.5f;
    [Tooltip("Slow duration (วินาที)")]
    public float slowDuration = 2f;

    [Header("Evolution — Freeze")]
    [Tooltip("เปิด Freeze chance (evo)")]
    public bool evoEnabled = false;
    [Tooltip("โอกาส freeze per hit (0–1)")]
    [Range(0f, 1f)]
    public float freezeChance = 0.15f;
    [Tooltip("Freeze duration (วินาที)")]
    public float freezeDuration = 1.5f;

    protected override GameObject GetProjectilePrefab() => missilePrefab;

    protected override void OnFire(WeaponLevelData ld)
    {
        int count = Mathf.Max(1, ld.projectileCount);
        float dmg = RollDamage(ld.damage, out bool isCrit);
        float speed = ld.projectileSpeed > 0f ? ld.projectileSpeed : 12f;
        float range = ld.range;

        Vector3 dir = GetAimDirection();

        for (int i = 0; i < count; i++)
        {
            // หา enemy target ในระยะยิงของอาวุธ (รองรับเล็งสุ่มและล็อกเป้าตัวใกล้สุด)
            Transform target = FindTargetEnemy(range);
            Vector3  spawnPos = transform.position + Vector3.up * 0.8f;

            // สุ่ม spawn offset เล็กน้อยเพื่อความสมจริง (ไม่เกิดซ้อนทับตำแหน่งเดียวกันเป๊ะๆ)
            Vector2 offset = Random.insideUnitCircle * 0.5f;
            spawnPos += new Vector3(offset.x, 0f, offset.y);

            // ยิงกระสุนนำวิถีซิงก์เน็ตเวิร์กออกไป (ส่ง target ไปทำงานต่อบนเซิร์ฟเวอร์)
            FireProjectile(spawnPos, dir, dmg, speed, count: 1, spreadDeg: 0f, piercing: false, maxRange: range, isCrit: isCrit, target: target);
        }
    }
}
