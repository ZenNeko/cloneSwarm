/// <summary>
/// ประเภท VFX — ใช้เป็น key เพื่อ lookup prefab จาก NetworkedVFXPool
///
/// None = -1  ใช้เป็นค่า default ใน WeaponData.hitVfxType (= ไม่มี VFX)
/// ค่าอื่น >= 0 — ตรงกับ vfxTypeMappings[] ใน NetworkedVFXPool Inspector
/// </summary>
public enum VFXType
{
    None            = -1,   // ไม่มี VFX

    // ── Generic hit effects (ใช้ใน WeaponData.hitVfxType + Projectile.hitVFX) ──
    HitEffect       =  0,   // generic hit impact (ทุก weapon ใช้ร่วมกัน)
    CritHitEffect   =  1,   // critical hit

    // ── Beam / Area effects (ใช้โดย weapon scripts โดยตรง) ───────────────────
    // slot 2 reserved (BeamHit removed — ใช้ LineRenderer + HitEffect แทน)
    GrenadeExplosion=  3,   // Grenade / Minefield
    // slot 4 reserved (MineExplosion removed)
    OrbiterHit      =  5,   // Orbiter / RadiantAura
    OrbPickup       =  6,   // Objective Orb เก็บ
    EnemyDeath      =  7,   // ศัตรูตาย
    // slot 8 reserved (RailgunBeam removed — ใช้ BeamHit แทน)
    // slot 9 reserved
    WhipSlash       = 10,   // Whip tentacle — arc shader
    SlashHit        = 11,   // DualSlash / BladeStorm / Chainsaw — arc shader (30°-240°)
    SlashAoE360     = 18,   // CycloneBlade / RadiantAura — full circle shader (360°)
    // slot 12 reserved (LightningHit removed — ใช้ BeamHit แทน)
    // slot 13 reserved (BoomerangHit removed — ใช้ HitEffect แทน)
    // slot 14 reserved (ChainsawSlash removed — ใช้ HitEffect แทน)
    VortexSpawn     = 15,   // Vortex / SpiralGalaxy
    DashTrail       = 16,   // BunnyHop / StormBunny dash trail
    MeteorAoE       = 17,   // BunnyHop / StormBunny landing AoE
}
