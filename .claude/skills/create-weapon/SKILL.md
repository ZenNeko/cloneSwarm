---
name: create-weapon
description: Scaffold a new weapon for the Clone Swarm Unity project — creates a WeaponBase subclass + provides instructions for the WeaponData ScriptableObject and prefab setup. Use when the user asks "create a new weapon", "add weapon X", or "scaffold weapon".
---

# Create New Weapon

Scaffolds a new weapon for the Clone Swarm multiplayer game.

## Project conventions

**Weapon architecture** (Netcode for GameObjects):
- `WeaponBase.cs` — base class (Owner-only Update, fires via `PlayerWeaponManager` ServerRpc)
- `WeaponData.cs` — ScriptableObject (shared stats, levels, identity)
- Concrete subclass — implements `OnFire(WeaponLevelData ld)`
- Prefab — has `WeaponBase` subclass component + VFX/SFX config

**Critical conventions:**
- VFX/SFX live on the **prefab** (not in `WeaponData`) — set in `WeaponBase` Inspector
- `OnFire` runs on Owner client → spawning enemy hits goes through `manager.<Action>ServerRpc(...)`
- Use `RollDamage(baseDamage, out bool isCrit)` for crit handling
- Use `PlayFireSfx()` / `PlayHitSfx(pos)` from base class for audio
- Use `GetAimDirection()` (already handles `AimMode.AutoNearest` / `MouseAim`)
- 5 levels for Normal tier, 1 level for Super/Fusion

## Workflow

When invoked:

1. **Ask the user** (use `AskUserQuestion` if not provided):
   - Weapon name (e.g., "FrostSpear")
   - Tier: `Normal` / `Super` / `Fusion`
   - Fire pattern: `Projectile` / `Hitscan/Raycast` / `Melee/Arc` / `AoE` / `Beam` / `Charge`
   - Aim mode: `AutoNearest` (default for most) or `MouseAim` (Gunner pistol-style)
   - Any unique mechanic (burst, chain, pierce, homing, etc.)

2. **Read a similar weapon** for reference based on fire pattern:
   - Projectile burst → `PistolWeapon.cs`
   - Hitscan → `LaserWeapon.cs`, `RailgunWeapon.cs`
   - Melee arc → `DualSlashWeapon.cs`, `WhipWeapon.cs`
   - AoE → `RadiantAuraWeapon.cs`, `GrenadeWeapon.cs`
   - Chain → `LightningChainWeapon.cs`

3. **Create the weapon script** at `Assets/Script/Weapon/<Name>Weapon.cs`:

   ```csharp
   using UnityEngine;

   /// <summary>
   /// <Name> — <one-line desc>
   ///
   /// LevelData example:
   ///   Lv1: dmg=X, cd=Y, ...
   /// </summary>
   public class <Name>Weapon : WeaponBase
   {
       [Header("<Name>-specific tunables")]
       public float exampleParam = 1f;

       protected override void OnFire(WeaponLevelData ld)
       {
           Vector3 pos = transform.position + Vector3.up * 0.5f;
           Vector3 dir = GetAimDirection();
           float   dmg = RollDamage(ld.damage, out bool isCrit);

           // For projectile:
           FireProjectile(pos, dir, dmg, ld.projectileSpeed, isCrit: isCrit);

           // For hitscan/AoE/melee — call manager.FireXxxServerRpc(...)

           PlayFireSfx();
       }
   }
   ```

4. **Print setup instructions** (do NOT try to create the ScriptableObject or prefab from CLI — Unity asset files are GUID-based and must be created in the Editor):

   ```
   📦 Unity Editor setup required:

   1. Create prefab `Assets/prefab/Weapon/<Name>Weapon.prefab`:
      - Add <Name>Weapon component
      - Set weaponVfxType, secondaryVfxType (from VFXType enum)
      - Add fireSfx[] / hitSfx[] AudioClips
      - Tune fireVolume, hitVolume, pitchVariance

   2. Create WeaponData asset:
      Right-click in Assets/Script/Data/WeaponData → Create → LoL Swarm → Weapon Data
      - weaponName: "<Name>"
      - tier: <Normal|Super|Fusion>
      - prefab: drag the prefab above
      - aimMode: <chosen>
      - levels[]: fill 5 (Normal) or 1 (Super/Fusion) entries

   3. Register in card pool (if Normal):
      - Set weight (Common≈100, Uncommon≈60, Rare≈25, Epic≈8)
      - For Super: assign to base weapon's `superVersion` field
   ```

5. **Verify** by reading the created file to confirm syntax.

## Pitfalls to avoid

- ❌ Don't put `[SerializeField]` VFX/SFX on `WeaponData` — those live on `WeaponBase`
- ❌ Don't call `Instantiate` directly for enemy-damaging projectiles — go through `manager.FireProjectileServerRpc` (server-authoritative)
- ❌ Don't access `manager.statManager` without null check (it's optional)
- ❌ Don't update on non-owner clients — base class handles `IsOwner` check but custom Update must too
- ✅ Always call `PlayFireSfx()` / `PlayHitSfx(pos)` for audio (centralized via SoundManager)
- ✅ Use `RollDamage(dmg, out isCrit)` overload when you need crit flag for VFX
