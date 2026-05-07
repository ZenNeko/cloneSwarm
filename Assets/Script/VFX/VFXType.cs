/// <summary>
/// ประเภท VFX — ใช้เป็น key เพื่อ lookup prefab จาก NetworkedVFXPool
///
/// None = -1  ใช้เป็นค่า default ใน WeaponBase.weaponVfxType / Projectile.hitVFX (= ไม่มี VFX)
/// ค่าอื่น >= 0 — ตรงกับ vfxTypeMappings[] ใน NetworkedVFXPool Inspector
/// </summary>
public enum VFXType
{
    None            = -1,   // ไม่มี VFX

    // ── Generic impact effects (Enemy.cs spawn เองตอน NotifyHitClientRpc) ────
    HitEffect       =  0,   // generic hit impact (ทุก weapon ใช้ร่วมกัน)
    CritHitEffect   =  1,   // critical hit

    // ── Beam / Area effects (ใช้โดย weapon scripts โดยตรง) ───────────────────
    TelegraphCircle =  2,   // Boss Circle/Chase telegraph — flat ring + sparks
    GrenadeExplosion=  3,   // Grenade / Minefield
    TelegraphLine   =  4,   // Boss Line telegraph — rectangular decal
    OrbiterHit      =  5,   // Orbiter / RadiantAura
    OrbPickup       =  6,   // Objective Orb เก็บ
    EnemyDeath      =  7,   // ศัตรูตาย
    TelegraphCross  =  8,   // Boss Cross telegraph — 2 ขีดไขว้
    TelegraphSpread =  9,   // Boss Spread telegraph — fan ของ N เส้น
    LanceThrust     = 10,   // Lance / Spear — forward pierce VFX
    SlashHit        = 11,   // DualSlash / BladeStorm / Chainsaw — arc shader (30°-240°)
    TelegraphDonut  = 12,   // Boss Donut telegraph — annulus shader
    TelegraphCone   = 13,   // Boss Cone telegraph — cone segment shader
    TelegraphChase  = 14,   // Boss Chase telegraph — magenta tracking circle
    VortexSpawn     = 15,   // Vortex / SpiralGalaxy
    DashTrail       = 16,   // BunnyHop / StormBunny dash trail
    MeteorAoE       = 17,   // BunnyHop / StormBunny landing AoE
    SlashAoE360     = 18,   // CycloneBlade / RadiantAura — full circle shader (360°)
    WhipSlash       = 19,   // Whip / WhipPlasma — tentacle arc VFX
    PhaseShockwave  = 20,   // Boss phase transition — large radial burst
}
