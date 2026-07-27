using System.Collections;
using UnityEngine;

/// <summary>
/// SuperOrbitalStrikeWeapon — ร่างพัฒนา (Super Version) ของ OrbitalStrikeWeapon
/// ผสม Orbital Barrage + Gravity Well:
///   1. ระหว่าง warning → ดูดศัตรูเข้าหาศูนย์กลาง (Gravity Well)
///   2. ระเบิดหลักทำดาเมจปกติ
///   3. หลังระเบิดหลัก → ยิง aftershock ลูกเล็กอีก N ลูกกระจายรอบจุดเดิม
/// </summary>
public class SuperOrbitalStrikeWeapon : OrbitalStrikeWeapon
{
    [Header("Super: Gravity Well")]
    [Tooltip("ความแรงในการดูดศัตรูต่อ tick (ใช้คูณ deltaTime)")]
    public float pullForce = 3f;

    [Header("Super: Aftershock Barrage")]
    [Tooltip("จำนวนลูกระเบิด aftershock หลังระเบิดหลัก")]
    public int aftershockCount = 4;

    [Tooltip("delay ระหว่างลูก aftershock แต่ละลูก (วินาที)")]
    public float aftershockDelay = 0.1f;

    [Tooltip("ตัวคูณรัศมีของ aftershock เทียบกับ main explosion")]
    public float aftershockRadiusMult = 0.5f;

    [Tooltip("ตัวคูณดาเมจของ aftershock เทียบกับ main hit")]
    public float aftershockDamageMult = 0.4f;

    [Tooltip("ระยะกระจาย of aftershock จากศูนย์กลาง")]
    public float aftershockSpread = 3f;

    protected override IEnumerator StrikeCoroutine(Vector3 pos, float dmg, bool isCrit, float wRadius, float eRadius)
    {
        // ── Broadcast Warning Indicator to other clients ──────────────────
        if (manager != null)
        {
            manager.SpawnOrbitalWarningClientRpc(pos, wRadius, strikeDelay);
        }

        // ── Warning Indicator (Local for Owner) ───────────────────────────
        GameObject warning = SpawnWarningObject(pos, wRadius);
        if (warning != null) _activeVisuals.Add(warning);

        Coroutine anim = StartCoroutine(AnimateWarning(warning, wRadius, strikeDelay));

        // ── Phase 1: Gravity Well — ดูดศัตรูเข้าหาศูนย์กลางตลอด strikeDelay ──
        float elapsed = 0f;
        float actualPullRadius = wRadius;

        while (elapsed < strikeDelay)
        {
            elapsed += Time.deltaTime;
            float pullThisTick = pullForce * Time.deltaTime;
            if (manager != null)
            {
                manager.PullEnemiesServerRpc(pos, actualPullRadius, pullThisTick);
            }
            yield return null;
        }

        if (anim != null) StopCoroutine(anim);
        if (warning != null)
        {
            _activeVisuals.Remove(warning);
            Destroy(warning);
        }

        // ── Phase 2: Main Explosion ────────────────────────────────────────
        FireMelee(pos + Vector3.up * 0.5f, eRadius, dmg, isCrit);
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), pos, eRadius, isCrit, isAttackHit: false);

        // ── Phase 3: Aftershock Barrage — ลูกเล็กกระจายรอบจุดระเบิดเดิม ──
        float afterRadius = eRadius * aftershockRadiusMult;
        float afterDmg    = dmg * aftershockDamageMult;

        for (int i = 0; i < aftershockCount; i++)
        {
            yield return new WaitForSeconds(aftershockDelay);

            // สุ่มตำแหน่งรอบจุดระเบิดเดิม
            Vector2 offset = Random.insideUnitCircle * aftershockSpread;
            Vector3 afterPos = pos + new Vector3(offset.x, 0f, offset.y);

            FireMelee(afterPos + Vector3.up * 0.5f, afterRadius, afterDmg, isCrit);
            ShowVfx(ResolveHitVfx("GrenadeExplosion"), afterPos, afterRadius, isCrit, isAttackHit: false);
        }
    }
}
