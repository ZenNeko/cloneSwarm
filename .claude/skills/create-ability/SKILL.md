---
name: create-ability
description: Scaffold a new player ability (Q/E slot) for the Clone Swarm Unity project — creates an AbilityBase subclass and provides setup instructions for the AbilityData ScriptableObject and prefab. Use when the user asks "create new ability", "add ability X", or "scaffold Q/E skill".
---

# Create New Ability

Scaffolds a new player active ability (Q or E slot) for Clone Swarm.

## Project conventions

**Ability vs Weapon — key differences:**
- Ability uses `AbilityBase.cs` + `AbilityData.cs` (NOT WeaponBase/WeaponData)
- **No automatic cooldown timer** — subclass handles its own input + timing
- Cast on button press (Q or E via `AbilityHUDUI` slot binding)
- Damage routed through `manager.<Action>ServerRpc(...)` like weapons
- SFX live on prefab via `AbilityBase` (castSfx[], hitSfx[])

**Ability lifecycle:**
1. `Init(AbilityData, level, manager)` — called by `PlayerWeaponManager`
2. `OnInit()` — find components, set state (override in subclass)
3. `Update()` — input poll, cooldown tick (must check `manager.IsOwner` manually)
4. On cast → `PlayCastSfx()` + server RPC for damage
5. `OnLevelUp()` — adjust to new level data

## Workflow

When invoked:

1. **Ask the user** (use `AskUserQuestion`):
   - Ability name (e.g., "BladeOfExile", "ValorShield")
   - Slot: `Q` or `E`
   - Cast type: `Instant` (dash, AoE burst) / `Toggle` (mode on/off) / `Channeled` (hold to maintain) / `Summon` (spawn entity)
   - Targeting: `Self` (buff/shield) / `Forward` (cone/line) / `Target` (auto-pick nearest) / `Cursor` (point-and-click)
   - Cooldown style: shared cooldown / charges / energy resource

2. **Read a similar ability** for reference:
   ```bash
   ls Assets/Script/Weapon/*.cs | grep -i -E "valor|exile|rocket|shield"
   ```
   Most abilities are weapon-style scripts named `<Name>Weapon.cs` but inherit `AbilityBase`.

3. **Create the ability script** at `Assets/Script/Weapon/<Name>Ability.cs` (or `<Name>Weapon.cs` if following project naming):

   ```csharp
   using UnityEngine;
   using UnityEngine.InputSystem;

   /// <summary>
   /// <Name> — <one-line description>
   /// Slot: <Q|E>  |  Cast: <Instant|Toggle|Channeled|Summon>
   ///
   /// LevelData:
   ///   Lv1: dmg=X, cd=Y, duration=Z
   /// </summary>
   public class <Name>Ability : AbilityBase
   {
       [Header("<Name>-specific")]
       public float exampleParam = 1f;

       float cooldownTimer;
       bool  isActive;

       protected override void OnInit()
       {
           cooldownTimer = 0f;
           isActive      = false;
       }

       void Update()
       {
           if (manager == null || !manager.IsOwner) return;
           if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

           cooldownTimer -= Time.deltaTime;

           var kb = Keyboard.current;
           if (kb == null) return;

           bool pressed = data.slotType == AbilitySlotType.Q
               ? kb.qKey.wasPressedThisFrame
               : kb.eKey.wasPressedThisFrame;

           if (pressed && cooldownTimer <= 0f)
               Activate();
       }

       void Activate()
       {
           var ld = data.GetLevelData(currentLevel);
           cooldownTimer = ld.cooldown;

           float dmg = RollDamage(ld.damage, out bool isCrit);
           PlayCastSfx();

           // Route damage through manager (server-authoritative)
           // e.g., manager.FireAoEServerRpc(transform.position, ld.range, dmg, isCrit);
       }
   }
   ```

4. **Print setup instructions**:

   ```
   📦 Unity Editor setup:

   1. Create prefab `Assets/prefab/Ability/<Name>Ability.prefab`:
      - Add <Name>Ability component
      - Add castSfx[] / hitSfx[] AudioClips
      - Tune castVolume, hitVolume, pitchVariance

   2. Create AbilityData asset:
      Right-click → Create → LoL Swarm → Ability Data
      - abilityName: "<Name>"
      - slotType: Q or E
      - prefab: drag the prefab above
      - levels[]: usually 1 entry (abilities rarely level up)
      - icon: ability icon sprite

   3. Wire to character:
      - Add AbilityData to character's ability pool in CharacterData
      - AbilityHUDUI auto-shows the icon + cooldown
   ```

5. **Verify** by reading the file.

## Pitfalls to avoid

- ❌ Don't inherit `WeaponBase` — abilities don't use the auto-cooldown loop
- ❌ Don't forget `manager.IsOwner` check in Update (otherwise fires on all clients)
- ❌ Don't put SFX in `AbilityData` — it lives on `AbilityBase` (prefab)
- ❌ Don't call `Instantiate` for damaging entities — use server RPC
- ✅ Always check `playermove.isDead.Value` before allowing cast
- ✅ Use `data.slotType` to bind Q vs E input (don't hardcode)
- ✅ For toggle abilities, expose a public bool the AbilityHUDUI can read

### The input gate every ability needs

Abilities read `Keyboard.current` in their own `Update` — there is no shared input path, so each
new one must add the gate itself. Put it with the other guards, before reading the key:

```csharp
if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
if (IsOnCooldown) return;
if (GamePause.LocalInputSuspended) return;   // host opened the pause menu in multiplayer

var kb = Keyboard.current;
```

`LocalInputSuspended` is not a time-scale pause. The host **is** the server, so freezing time on
the host freezes every client's enemies; multiplayer pause therefore only suspends the local
machine's input while the world keeps running. Miss this gate and the host keeps casting with the
menu open.

All six existing abilities carry it — `ValorWeapon`, `BladeOfExileWeapon`, `GunnerGiantRocket`,
`GunnerRocketMode`, `HunterMissileAbility`, `HunterUltimate`. Copy the shape from any of them.

### Also inherited from the weapon rules

- **No new ServerRpc taking `damage`** — send weapon name + level instead
- **Never discard the crit flag** — `RollDamage(dmg, out bool _)` silently downgrades every crit
  to a normal-looking hit. `HunterMissileAbility` and `GunnerGiantRocket` still do; they are left
  that way on purpose until the server owns crit rolling
- **Don't fire `HitEffect` yourself** — `Enemy.NotifyHitClientRpc` already does it on every client
- **Check the ability isn't mid-action when the player dies** — `BunnyHopWeapon` dashed to
  completion after its owner died because it only tested `isDead` before starting
