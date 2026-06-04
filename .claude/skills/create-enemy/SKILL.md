---
name: create-enemy
description: Scaffold a new enemy or mini-boss for Clone Swarm — sets up Enemy.cs config, NetworkObject requirements, and (for mini-bosses) creates a MiniBossConfig ScriptableObject template. Use when the user asks "add new enemy", "create mini boss", or "scaffold boss prefab".
---

# Create New Enemy / Boss

Scaffolds enemy variants (regular grunt, ranged, tank, mini-boss) for the Clone Swarm NGO multiplayer.

## Project conventions

**Enemy types:**
- **Regular grunt** — just `Enemy.cs` + `NetworkObject` (uses default contact damage AI)
- **Ranged** — `Enemy.cs` + `EnemyRanged.cs` (shoots at players)
- **Tank** — `Enemy.cs` with high HP + slow speed, often custom mechanic
- **Mini-boss** — `Enemy.cs` + `MiniBossAI.cs` + `MiniBossConfig` SO (selectable mechanics)
- **Main boss** — `Enemy.cs` + `MainBoss.cs` (3-phase, fixed mechanics)

**Critical conventions:**
- All enemies need `NetworkObject` + `Enemy.cs` (server-authoritative damage)
- `Enemy.netHealth` is a `NetworkVariable<float>` — Server writes, all clients read
- Movement runs only on **server** (`if (!IsServer) return;` in Update)
- Wall sliding is built into `Enemy.cs` — no need to reimplement
- Death VFX/SFX auto via `NotifyDeathClientRpc` (uses `VFXType.EnemyDeath`)
- Register prefab in `DefaultNetworkPrefabs.asset` for NGO spawning
- For boss UI: subscribe to `MainBoss.OnAnyBossSpawned` or `MiniBossAI.OnAnyMiniBossSpawned`

**MiniBossConfig mechanics** (mix and match in the list):
- `CircleLine` — alternates Circle AoE ↔ Line AoE
- `Tether` — pillar/co-op tether the player must run from
- `Chase` — red zone that follows a player

## Workflow

When invoked:

1. **Ask the user** (use `AskUserQuestion`):
   - Enemy type: `Grunt` / `Ranged` / `Tank` / `MiniBoss` / `MainBoss variant`
   - Display name (e.g., "Frost Wraith")
   - Stats: maxHealth, speed, contactDamage, expReward
   - (Mini-boss) mechanics list: `CircleLine` / `Tether` / `Chase` / multiple
   - (Mini-boss) bonus drops (ExpOrb count, ObjectiveOrb, special items)

2. **For Grunt / Ranged / Tank** — usually NO new script needed, just config the existing prefab. Print setup:

   ```
   📦 Unity Editor setup:

   1. Duplicate an existing enemy prefab (Assets/prefab/Enemy/EnemyBasic.prefab)
   2. Rename to <Name>.prefab
   3. On Enemy component, set:
      - maxHealth, speed, contactDamage, damageCooldown
      - expReward, expOrbPrefab
   4. Add to DefaultNetworkPrefabs.asset (drag prefab in)
   5. Add to EnemySpawner pool / WaveConfig
   ```

3. **For MiniBoss** — read `MiniBossAI.cs` + `MiniBossConfig.cs` for reference, then:

   a. **Create the MiniBossConfig template** as a C# helper comment block (the asset itself must be created in Editor):

      ```csharp
      // Create via: Right-click → Create → Game → MiniBossConfig
      // Configure:
      //   bossName        = "<Name>"
      //   mechanics       = [<chosen list>]
      //   attackInterval  = <seconds>
      //   firstAttackDelay = <seconds>
      //   <mechanic-specific tunables>
      //   extraExpOrbs    = <count>
      //   bonusDrops[]    = <ObjectiveOrb prefab if relevant>
      ```

   b. **If new mechanic is needed**, add to `MiniBossConfig.Mechanic` enum AND `MiniBossAI.AttackLoop()` switch — but this is a bigger refactor, confirm with user first.

4. **For MainBoss variant** — create a subclass or new script following `MainBoss.cs` pattern. Bigger task, ask user to confirm scope.

5. **Print Editor setup** for all cases:

   ```
   📦 Unity Editor setup:

   1. Create prefab `Assets/prefab/Enemy/<Name>.prefab` with:
      - NetworkObject
      - Enemy.cs (set maxHealth, speed, contactDamage, expReward, expOrbPrefab)
      - Rigidbody (auto-configured by Enemy.OnNetworkSpawn)
      - Collider (sphere/capsule for hit detection)
      - (MiniBoss) MiniBossAI.cs → assign config + telegraphZonePrefab + tetherPrefab
      - (Optional) WorldHPBar.cs + child WorldHPCanvas for floating HP bar

   2. Register in DefaultNetworkPrefabs.asset
      (Assets/DefaultNetworkPrefabs.asset → drag prefab into list)

   3. Add to spawn pool:
      - Regular: EnemySpawner.enemyPrefabs[] or WaveConfig
      - MiniBoss: BossManager.miniBossPrefabs[]

   4. (MiniBoss only) Create MiniBossConfig asset:
      Right-click → Create → Game → MiniBossConfig
      <fill the config based on user's chosen mechanics>
   ```

## Pitfalls to avoid

- ❌ Don't forget `NetworkObject` component — enemy won't spawn via NGO without it
- ❌ Don't drive enemy movement from the client — server is authoritative
- ❌ Don't modify `netHealth.Value` from client code — Write Permission = Server only
- ❌ Don't forget to register in `DefaultNetworkPrefabs.asset` (will throw NetworkObject not found)
- ❌ Don't put `MiniBossConfig` fields in `MiniBossAI` directly — keep data-driven via SO
- ✅ Use `enemy.serverInvincible` flag for phase transition iframes (built into Enemy.cs)
- ✅ For mini-boss with multiple variants, use the same prefab + different MiniBossConfig
- ✅ Add `bossName` to MiniBossAI Inspector for HP bar display (falls back to gameObject.name)
