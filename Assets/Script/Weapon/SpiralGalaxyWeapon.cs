using UnityEngine;

/// <summary>
/// Spiral Galaxy — Super version of Vortex
/// 8 base streams (4 Fire + 4 Ice, alternating) + bonus projectiles, rotating at double speed.
/// Ice streams slow enemies and have a chance to freeze them.
/// </summary>
public class SpiralGalaxyWeapon : VortexWeapon
{
    [Header("Ice Stream Settings")]
    [Tooltip("โอกาส freeze ของพ่นไฟน้ำแข็ง (0-1)")]
    public float freezeChance = 0.15f;
    [Tooltip("ระยะเวลา freeze (วินาที)")]
    public float freezeDuration = 1.5f;
    [Tooltip("อัตราการ slow (0-1, ค่าต่ำ = ช้ามาก)")]
    public float slowPercent = 0.5f;
    [Tooltip("ระยะเวลา slow (วินาที)")]
    public float slowDuration = 2f;


    protected override bool IsIceStream(int index)
    {
        // Alternating fire (even) and ice (odd) streams
        return (index % 2 == 1);
    }

    protected override void OnInit()
    {
        base.OnInit();
    }

    protected override void SweepNozzleDamage(int index, float dmg, float range, bool isCrit)
    {
        if (nozzles[index] == null) return;

        Vector3 dir = nozzles[index].forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        dir = dir.normalized;

        // Damage origin starts at the nozzle's world position, height locked to gameplay plane (0.5f above player root)
        Vector3 origin = nozzles[index].position;
        origin.y = transform.position.y + 0.5f;

        if (IsIceStream(index))
        {
            // Ice stream: Deals damage, slows, and has a chance to freeze enemies
            FireLineAoE(origin, dir, dmg, range, streamWidth, isCrit, knockbackForce: 0f, vfxKey: "None",
                        slowPercent, slowDuration, freezeChance, freezeDuration);
        }
        else
        {
            // Fire stream: Deals standard damage
            FireLineAoE(origin, dir, dmg, range, streamWidth, isCrit, knockbackForce: 0f, vfxKey: "None");
        }
    }
}
