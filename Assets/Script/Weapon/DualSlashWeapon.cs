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
    // ── Slash Config ────────────────────────────────────────────────────
    [System.Serializable]
    public class SlashConfig
    {
        [Tooltip("Offset ไปข้างหน้า (คูณ radius)")]
        [Range(-1f, 1f)]
        public float forwardOffset = 0.6f;

        [Tooltip("Offset ไปทางขวา (คูณ radius) — ค่าลบ = ซ้าย")]
        [Range(-1f, 1f)]
        public float rightOffset = -0.3f;

        [Tooltip("หมุนรอบแกน X (ก้ม/เงย)")]
        [Range(0f, 360f)]
        public float rotationX = 0f;

        [Tooltip("หมุนรอบแกน Y (ซ้าย/ขวา) — 90 = ขวา, 180 = หลัง, 270 = ซ้าย")]
        [Range(0f, 360f)]
        public float rotationY = 0f;

        [Tooltip("หมุนรอบแกน Z (เอียง/หมุนตัว)")]
        [Range(0f, 360f)]
        public float rotationZ = 0f;
    }

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
    Vector3 _lastMoveDir = Vector3.forward;
    bool    _swapState;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = ld.range;

        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        Vector3 forward = GetAimDirection();
        float   arc     = data != null ? data.arcAngle : 360f;

        // สลับลำดับ slash ทุก fire
        SlashConfig first  = _swapState ? slash2 : slash1;
        SlashConfig second = _swapState ? slash1 : slash2;
        if (alternatePerFire) _swapState = !_swapState;

        StartCoroutine(SlashSequence(forward, radius, arc, dmg, isCrit, first, second));
    }

    IEnumerator SlashSequence(Vector3 forward, float radius, float arc,
                              float dmg, bool isCrit,
                              SlashConfig first, SlashConfig second)
    {
        // ── Slash 1 ─────────────────────────────────────────────────
        FireSlash(forward, radius, arc, dmg, isCrit, first);

        if (slashDelay > 0f)
            yield return new WaitForSeconds(slashDelay);

        // ── Slash 2 ─────────────────────────────────────────────────
        FireSlash(forward, radius, arc, dmg, isCrit, second);
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
        manager.FireArcMeleeServerRpc(pos, forward, radius, arc, dmg, isCrit);
        ShowVfx(VFXType.SlashHit, pos, radius, isCrit,
                isAttackHit: false, direction: dir, arcAngle: arc, roll: cfg.rotationZ);
    }

    Vector3 GetAimDirection()
    {
        if (manager?.playerMove != null)
        {
            Vector3 move = manager.playerMove.MoveDirection;
            if (move.sqrMagnitude > 0.01f)
                _lastMoveDir = move.normalized;
        }
        return _lastMoveDir;
    }
}
