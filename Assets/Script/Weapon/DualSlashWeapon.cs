using UnityEngine;
using System.Collections;

/// <summary>
/// Dual Slash â€” slash 2 à¸„à¸£à¸±à¹‰à¸‡ à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² offset / rotation / timing à¹à¸¢à¸à¹à¸•à¹ˆà¸¥à¸° slash
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=30, cd=1.5s, range=2.0
///   Lv2: dmg=38, cd=1.3s, range=2.2
///   Lv3: dmg=47, cd=1.1s, range=2.5
///   Lv4: dmg=56, cd=1.0s, range=2.8
///   Lv5: dmg=65, cd=0.9s, range=3.0
///
/// Super: BladeStormWeapon (slash 360Â° 3 à¸„à¸£à¸±à¹‰à¸‡ rapid)
/// Fusion: Blade Storm + Chainsaw = CycloneBladeWeapon
/// </summary>
public class DualSlashWeapon : WeaponBase
{
    // â”€â”€ Slash Config â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [System.Serializable]
    public class SlashConfig
    {
        [Tooltip("Offset à¹„à¸›à¸‚à¹‰à¸²à¸‡à¸«à¸™à¹‰à¸² (à¸„à¸¹à¸“ radius)")]
        [Range(-1f, 1f)]
        public float forwardOffset = 0.6f;

        [Tooltip("Offset à¹„à¸›à¸—à¸²à¸‡à¸‚à¸§à¸² (à¸„à¸¹à¸“ radius) â€” à¸„à¹ˆà¸²à¸¥à¸š = à¸‹à¹‰à¸²à¸¢")]
        [Range(-1f, 1f)]
        public float rightOffset = -0.3f;

        [Tooltip("à¸«à¸¡à¸¸à¸™à¸£à¸­à¸šà¹à¸à¸™ X (à¸à¹‰à¸¡/à¹€à¸‡à¸¢)")]
        [Range(0f, 360f)]
        public float rotationX = 0f;

        [Tooltip("à¸«à¸¡à¸¸à¸™à¸£à¸­à¸šà¹à¸à¸™ Y (à¸‹à¹‰à¸²à¸¢/à¸‚à¸§à¸²) â€” 90 = à¸‚à¸§à¸², 180 = à¸«à¸¥à¸±à¸‡, 270 = à¸‹à¹‰à¸²à¸¢")]
        [Range(0f, 360f)]
        public float rotationY = 0f;

        [Tooltip("à¸«à¸¡à¸¸à¸™à¸£à¸­à¸šà¹à¸à¸™ Z (à¹€à¸­à¸µà¸¢à¸‡/à¸«à¸¡à¸¸à¸™à¸•à¸±à¸§)")]
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
    [Tooltip("à¸”à¸µà¹€à¸¥à¸¢à¹Œà¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡ slash 1 à¸à¸±à¸š 2 (à¸§à¸´à¸™à¸²à¸—à¸µ) â€” 0 = à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™")]
    [Range(0f, 0.5f)]
    public float slashDelay = 0.15f;

    [Header("Alternate")]
    [Tooltip("à¸ªà¸¥à¸±à¸šà¸¥à¸³à¸”à¸±à¸š slash à¸—à¸¸à¸à¸„à¸£à¸±à¹‰à¸‡à¸—à¸µà¹ˆà¸¢à¸´à¸‡ (1â†’2, 2â†’1, 1â†’2, ...)")]
    public bool alternatePerFire = true;

    // â”€â”€ Internal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // à¸ªà¸¥à¸±à¸šà¸¥à¸³à¸”à¸±à¸š slash à¸—à¸¸à¸ fire
        SlashConfig first  = _swapState ? slash2 : slash1;
        SlashConfig second = _swapState ? slash1 : slash2;
        if (alternatePerFire) _swapState = !_swapState;

        StartCoroutine(SlashSequence(forward, radius, arc, dmg, isCrit, first, second));
    }

    IEnumerator SlashSequence(Vector3 forward, float radius, float arc,
                              float dmg, bool isCrit,
                              SlashConfig first, SlashConfig second)
    {
        // â”€â”€ Slash 1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        FireSlash(forward, radius, arc, dmg, isCrit, first);

        if (slashDelay > 0f)
            yield return new WaitForSeconds(slashDelay);

        // â”€â”€ Slash 2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        FireSlash(forward, radius, arc, dmg, isCrit, second);
    }

    void FireSlash(Vector3 forward, float radius, float arc,
                   float dmg, bool isCrit, SlashConfig cfg)
    {
        Vector3 right  = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // â”€â”€ à¸•à¸³à¹à¸«à¸™à¹ˆà¸‡ slash â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Vector3 pos = origin
                    + forward * (radius * cfg.forwardOffset)
                    + right   * (radius * cfg.rightOffset);

        // â”€â”€ à¸—à¸´à¸¨ VFX (rotation XYZ) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // X,Y â†’ à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™à¸—à¸´à¸¨ direction | Z â†’ roll (à¹€à¸­à¸µà¸¢à¸‡ VFX)
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up)
                       * Quaternion.Euler(cfg.rotationX, cfg.rotationY, 0f);
        Vector3 dir = rot * Vector3.forward;

        // â”€â”€ Damage + VFX â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        FireArcMelee(pos, forward, radius, arc, dmg, isCrit);
        ShowVfx(ResolveHitVfx("SlashHit"), pos, radius, isCrit,
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
