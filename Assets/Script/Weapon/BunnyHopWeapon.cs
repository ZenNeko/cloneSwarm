using System.Collections;
using UnityEngine;

/// <summary>
/// Signature Weapon ของ Riven — ไม่ใช้ cooldown timer แต่ใช้ CHARGE จาก ChargeManager
/// เมื่อ ChargeManager เรียก FireOnCharge():
///   — Dash หาศัตรูที่ใกล้ที่สุด + AoE damage
///   — Wind Slash ยิง ทุกครั้ง (ตาม windSlashCount) — ตอน Exile: count ×2
///   — ทุก 2nd cast: AoE ใหญ่ขึ้น 50% + Wind Slash เป็น full radial
///   — Runic Blade: damage +0–15% ตามระยะก่อน Dash
///   — Shield = 25% ของ damage ที่ทำ
/// </summary>
public class BunnyHopWeapon : WeaponBase
{
    [Header("Dash")]
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;

    [Header("Wind Slash (ทุก charge fill)")]
    [Tooltip("จำนวน slash ต่อ fire (Exile → ×2)")]
    public int windSlashCount = 2;
    [Tooltip("ความเสียหาย Wind Slash เป็น % ของ damage หลัก")]
    [Range(0.3f, 1f)]
    public float windSlashDmgRatio = 0.6f;

    [Header("Runic Blade Passive")]
    [Tooltip("ระยะสูงสุดที่ให้ bonus damage เต็ม (15%)")]
    public float runicMaxRange = 12f;

    [Header("Shield")]
    [Tooltip("Shield = X% ของ damage ที่ทำ")]
    public float shieldPercent = 0.25f;

    protected override bool UsesCooldownTimer => false;

    private ChargeManager chargeManager;

    protected override void OnInit()
    {
        chargeManager = manager.GetComponent<ChargeManager>();
    }

    protected override void OnFire(WeaponLevelData ld) { /* ไม่ใช้ */ }

    // ── เรียกจาก ChargeManager เมื่อ CHARGE เต็ม ─────────────────────────
    public void FireOnCharge(int fireCount)
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        var   ld     = data.GetLevelData(currentLevel);
        var   sm     = manager.statManager;
        float damage = ld.damage * (sm != null ? sm.GetPowerMultiplier() : 1f);
        float range  = ld.range  * (sm != null ? sm.GetAreaMultiplier()  : 1f);

        // ทุก 2nd cast: AoE ใหญ่ขึ้น + Wind Slash รอบทิศ
        bool  bigSlash  = (fireCount % 2 == 0);
        float aoeRadius = bigSlash ? range * 1.5f : range;

        // Runic Blade: distance bonus 0–15%
        Transform nearest = FindNearestEnemy(aoeRadius * 2f);
        if (nearest != null)
        {
            float dist  = Vector3.Distance(transform.position, nearest.position);
            float bonus = Mathf.Clamp01(dist / runicMaxRange) * 0.15f;
            damage *= (1f + bonus);
        }

        damage = RollDamage(damage);

        Vector3 dashDir = nearest != null
            ? (nearest.position - transform.position).normalized
            : transform.forward;
        dashDir.y = 0f;

        StartCoroutine(DashAndFire(dashDir, aoeRadius, damage, bigSlash, range));
    }

    IEnumerator DashAndFire(Vector3 dir, float radius, float damage, bool bigSlash, float baseRange)
    {
        // Dash
        var rb = manager.playerMove?.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float speed = dashDistance / dashDuration;
            float elapsed = 0f;
            while (elapsed < dashDuration)
            {
                rb.velocity = new Vector3(dir.x * speed, rb.velocity.y, dir.z * speed);
                elapsed    += Time.deltaTime;
                yield return null;
            }
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        }
        else yield return null;

        // AoE at landing
        manager.FireMeleeServerRpc(transform.position, radius, damage);

        // Shield = 25% dmg × จำนวน enemy ที่โดน (approximate)
        float shieldAmount = damage * shieldPercent * Mathf.Max(1f, FindAllEnemiesInRange(radius).Length);
        manager.AddShieldServerRpc(shieldAmount);

        // ── Wind Slash — ทุก charge fill ─────────────────────────────────
        bool   exile      = chargeManager != null && chargeManager.IsExileActive;
        int    slashCount = exile ? windSlashCount * 2 : windSlashCount;
        float  slashDmg   = damage * windSlashDmgRatio;
        float  slashRange = baseRange * 2.5f;

        if (bigSlash)
        {
            // 2nd cast: ออกรอบทิศ (radial)
            for (int i = 0; i < slashCount; i++)
            {
                float   angle    = i * (360f / slashCount);
                Vector3 slashDir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                manager.FireRaycastServerRpc(transform.position, slashDir, slashDmg, slashRange);
            }
        }
        else
        {
            // 1st cast: ออกทางด้านหน้า + spread
            float spreadAngle = exile ? 60f : 40f;
            float step        = slashCount > 1 ? spreadAngle / (slashCount - 1) : 0f;
            float startAngle  = -(spreadAngle * 0.5f);
            for (int i = 0; i < slashCount; i++)
            {
                float   angle    = startAngle + step * i;
                Vector3 slashDir = Quaternion.Euler(0f, angle, 0f) * dir;
                manager.FireRaycastServerRpc(transform.position, slashDir, slashDmg, slashRange);
            }
        }
    }
}
