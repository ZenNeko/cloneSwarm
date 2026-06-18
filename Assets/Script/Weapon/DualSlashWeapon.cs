using UnityEngine;
using System.Collections;

/// <summary>
/// Dual Slash — slash 2 ครั้ง ตั้งค่า offset / rotation / timing แยกแต่ละ slash
///
/// Level data แนะนำ:
///   Lv1: dmg=30, cd=1.5s, range=2.0
///   Lv2: dmg=38, cd=1.3s, range=2.2
///   Lv3: dmg=47, cd=1.1s, range=2.5
///   Lv4: dmg=56, cd=1.0s, range=2.8
///   Lv5: dmg=65, cd=0.9s, range=3.0
///
/// Super: BladeStormWeapon (slash 360° 3 ครั้ง rapid)
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class DualSlashWeapon : WeaponBase
{
    [Header("Slash 1")]
    public SlashConfig slash1 = new SlashConfig
    {
        forwardOffset = 0.6f, rightOffset = -0.3f,
        rotationY = 30f
    };

    [Header("Slash 2")]
    public SlashConfig slash2 = new SlashConfig
    {
        forwardOffset = 0.6f, rightOffset = 0.3f,
        rotationY = 330f, rotationZ = 180f
    };

    [Header("Timing")]
    [Tooltip("ดีเลย์ระหว่าง slash 1 กับ 2 (วินาที) — 0 = พร้อมกัน")]
    [Range(0f, 0.5f)]
    public float slashDelay = 0.15f;

    [Header("Alternate")]
    [Tooltip("สลับลำดับ slash ทุกครั้งที่ยิง (1→2, 2→1, 1→2, ...)")]
    public bool alternatePerFire = true;

    // ── Internal ────────────────────────────────────────────────────────
    bool    _swapState;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = ld.range;

        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        float   arc     = data != null ? data.arcAngle : 360f;

        // สลับลำดับ slash ทุก fire
        SlashConfig first  = _swapState ? slash2 : slash1;
        SlashConfig second = _swapState ? slash1 : slash2;
        if (alternatePerFire) _swapState = !_swapState;

        StartCoroutine(SlashSequence(radius, arc, dmg, isCrit, first, second));
    }

    IEnumerator SlashSequence(float radius, float arc,
                              float dmg, bool isCrit,
                              SlashConfig first, SlashConfig second)
    {
        // ── Slash 1 ─────────────────────────────────────────────────
        Vector3 forward1 = GetAimDirection();
        FireSlash(forward1, radius, arc, dmg, isCrit, first);

        if (slashDelay > 0f)
            yield return new WaitForSeconds(slashDelay);

        // ── Slash 2 ─────────────────────────────────────────────────
        Vector3 forward2 = GetAimDirection();
        FireSlash(forward2, radius, arc, dmg, isCrit, second);
    }

    void FireSlash(Vector3 forward, float radius, float arc,
                   float dmg, bool isCrit, SlashConfig cfg)
    {
        Vector3 right  = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // ── ตำแหน่ง slash ─────────────────────────────────────────
        Vector3 pos = origin
                    + forward * (radius * cfg.forwardOffset)
                    + right   * (radius * cfg.rightOffset);

        // ── ทิศ VFX (rotation XYZ) ────────────────────────────────
        // X,Y → เปลี่ยนทิศ direction | Z → roll (เอียง VFX)
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up)
                       * Quaternion.Euler(cfg.rotationX, cfg.rotationY, 0f);
        Vector3 dir = rot * Vector3.forward;

        // ── Damage + VFX ──────────────────────────────────────────
        FireArcMelee(pos, forward, radius, arc, dmg, isCrit);

        string vfxKey = cfg.useSecondaryVfx ? ResolveSecondaryVfx("SlashHit") : ResolveHitVfx("SlashHit");
        ShowVfx(vfxKey, pos, radius, isCrit,
                isAttackHit: false, direction: dir, arcAngle: arc, roll: cfg.rotationZ);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (data == null) return;
        float radius = data.GetLevelData(currentLevel).range;
        Vector3 center = transform.position + Vector3.up * 0.5f;
        Vector3 forward = transform.forward;

        DrawSlashGizmo(center, forward, radius, slash1, Color.green, "Slash 1");
        DrawSlashGizmo(center, forward, radius, slash2, Color.yellow, "Slash 2");
    }
}
