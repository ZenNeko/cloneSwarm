using UnityEngine;

/// <summary>
/// Spiral Galaxy — Super version of Vortex
/// 4 streams (cross shape) rotating around the player at double speed.
/// </summary>
public class SpiralGalaxyWeapon : VortexWeapon
{
    protected override int GetStreamCount() => 4; // Super version has 4 streams in a cross shape

    protected override void OnInit()
    {
        // Double rotation speed for the super version
        rotSpeed *= 2.0f;
        base.OnInit();
    }
}

