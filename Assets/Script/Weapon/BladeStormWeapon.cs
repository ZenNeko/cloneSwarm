using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blade Storm — Super version ของ Dual Slash
/// slash 120° arc × 3 ครั้ง สลับหน้า-หลัง-หน้า (rapid burst) ทำงานผ่าน Server Authority
/// </summary>
public class BladeStormWeapon : WeaponBase
{
    [Tooltip("จำนวนครั้งที่ slash ใน burst เดียว (สูงสุด 6)")]
    [Range(1, 6)]
    public int   burstCount    = 6;
    [Tooltip("หน่วงระหว่าง slash แต่ละครั้งใน burst (วินาที)")]
    public float burstInterval = 0.12f;

    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("มุม arc ของ slash แต่ละครั้ง (องศา)")]
    [Range(10f, 360f)]
    public float arcAngle = 120f;

    [Header("Slashes Configuration")]
    [Tooltip("การตั้งค่าการฟันด้านหน้า (จังหวะคี่: 1, 3, 5)")]
    public SlashConfig slash1 = new SlashConfig { forwardOffset = 0.4f, rightOffset = 0f, rotationY = 0f };
    [Tooltip("การตั้งค่าการฟันด้านหลัง (จังหวะคู่: 2, 4, 6)")]
    public SlashConfig slash2 = new SlashConfig { forwardOffset = 0.4f, rightOffset = 0f, rotationY = 0f };

    [Header("Alternate")]
    [Tooltip("สลับลำดับการฟันทุกครั้งที่ยิง (จากหน้าไปหลัง -> หลังมาหน้า)")]
    public bool alternatePerFire = true;

    private bool isBursting;
    private bool _swapState;

    protected override void OnFire(WeaponLevelData ld)
    {
        if (isBursting) return;
        StartCoroutine(BurstSequence(ld));
    }

    IEnumerator BurstSequence(WeaponLevelData ld)
    {
        isBursting = true;
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = ld.range;

        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   arc    = arcAngle;

        int count = Mathf.Clamp(burstCount, 1, 6);
        bool reverse = _swapState;
        if (alternatePerFire) _swapState = !_swapState;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos;
            Vector3 slashDir;
            Vector3 dmgDir;
            float rotZ = 0f;

            // ค้นหาทิศการเล็งล่าสุดแบบไดนามิกในทุกครั้งที่ปล่อยคมดาบ
            Vector3 dir = GetAimDirection();

            // สลับหน้า-หลัง
            bool isEven = (i % 2 == 0);
            if (reverse) isEven = !isEven;

            var cfg = isEven ? slash1 : slash2;

            if (cfg != null)
            {
                Vector3 baseDir = isEven ? dir : -dir;
                Vector3 right = Vector3.Cross(Vector3.up, baseDir).normalized;

                pos = center 
                    + baseDir * (radius * cfg.forwardOffset)
                    + right   * (radius * cfg.rightOffset);

                Quaternion rot = Quaternion.LookRotation(baseDir, Vector3.up)
                               * Quaternion.Euler(cfg.rotationX, cfg.rotationY, 0f);
                slashDir = rot * Vector3.forward;
                dmgDir   = baseDir; // ดาเมจตามทิศทางจริง (หน้า หรือ หลัง)
                rotZ     = cfg.rotationZ;
            }
            else
            {
                // Fallback
                slashDir = isEven ? dir : -dir;
                pos = center + slashDir * (radius * 0.4f);
                dmgDir = slashDir;
            }

            // ใช้ FireArcMelee เพื่อส่งคำสั่งทำดาเมจและลงทะเบียนสถิติไปทำบน Server (ServerRpc)
            FireArcMelee(pos, dmgDir, radius, arc, dmg, isCrit);

            string vfxKey = cfg != null && cfg.useSecondaryVfx ? ResolveSecondaryVfx("SlashHit") : ResolveHitVfx("SlashHit");
            ShowVfx(vfxKey, pos, radius, isCrit, isAttackHit: false, direction: slashDir, arcAngle: arc, roll: rotZ);

            if (i < count - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        isBursting = false;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (data == null) return;
        float radius = data.GetLevelData(currentLevel).range;
        Vector3 center = transform.position + Vector3.up * 0.5f;
        Vector3 forward = transform.forward;

        int count = Mathf.Clamp(burstCount, 1, 6);

        for (int i = 0; i < count; i++)
        {
            float t = count > 1 ? (float)i / (count - 1) : 0f;
            Color color = Color.Lerp(Color.cyan, Color.magenta, t);

            bool isEven = (i % 2 == 0);
            var cfg = isEven ? slash1 : slash2;
            Vector3 baseDir = isEven ? forward : -forward;

            DrawSlashGizmo(center, baseDir, radius, cfg, color, $"Slash {i + 1}");
        }
    }
}
