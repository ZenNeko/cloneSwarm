---
name: review-ngo
description: Code-review a NetworkBehaviour for Unity Netcode for GameObjects (NGO) patterns — server/client authority, NetworkVariable usage, RPC correctness, race conditions, and Clone Swarm project conventions. Use when the user asks "review this NGO code", "check NetworkBehaviour", "audit multiplayer code", or after editing a file with NetworkBehaviour/NetworkVariable/ServerRpc/ClientRpc.
---

# NGO Code Review (Clone Swarm)

Reviews Unity Netcode for GameObjects code for correctness, authority leaks, and project conventions.

## What to review

When invoked on a file or PR diff, check against ALL of these categories. Report findings as **CRITICAL** / **WARNING** / **NIT** with file:line refs.

### 1. Authority & ownership

- [ ] All write-side logic to `NetworkVariable.Value` is gated by `if (!IsServer) return;`
- [ ] All write-side logic to gameplay state (HP, position, spawn) is server-only
- [ ] Owner-only logic (input, local prediction) gated by `if (!IsOwner) return;`
- [ ] No `Instantiate(prefab)` of NetworkObjects from client — must go through ServerRpc
- [ ] `NetworkObject.Spawn(true)` called only on server
- [ ] No client-side mutation of `playermove.isDead` / `Enemy.netHealth` — both Server-write

### 2. RPC correctness

- [ ] `ServerRpc` methods end with `ServerRpc` suffix (compile-time required)
- [ ] `ClientRpc` methods end with `ClientRpc` suffix
- [ ] `[ServerRpc(RequireOwnership = false)]` set explicitly if non-owner clients call it
- [ ] ServerRpc parameters are NGO-serializable (no `Transform`, no `GameObject` direct — use `NetworkObjectReference` instead)
- [ ] No infinite RPC loops (ServerRpc → ClientRpc → ServerRpc)
- [ ] `BroadcastVfxTypeClientRpc` / similar — fire once per event, not per-frame
- [ ] ClientRpc that runs effects checks for owner-only filters if needed

### 3. NetworkVariable usage

- [ ] Declared with correct `ReadPermission` / `WritePermission`
- [ ] `OnValueChanged += handler` subscribed in `OnNetworkSpawn` (or `OnEnable`) and unsubscribed in `OnNetworkDespawn` / `OnDisable`
- [ ] No subscribe in `Awake/Start` (NetworkVariable isn't ready yet)
- [ ] Initial value set on server in `OnNetworkSpawn` (after `IsServer` check)
- [ ] Not mutated inside `OnValueChanged` handler (causes recursive callbacks)

### 4. Lifecycle hooks

- [ ] `OnNetworkSpawn` — used instead of `Start` for NGO state init
- [ ] `OnNetworkDespawn` — cleanup events, coroutines, timers
- [ ] Static event handlers (e.g. `MainBoss.OnAnyBossSpawned`) unsubscribed in `OnDisable` to prevent leaks across scene loads
- [ ] No `DontDestroyOnLoad` on networked objects (NGO manages lifetime)

### 5. Coroutines & timing

- [ ] Server-only coroutines started inside `if (IsServer)` block
- [ ] Coroutines stopped or guarded against despawn (`if (!NetworkObject.IsSpawned) yield break;`)
- [ ] No `WaitForSeconds` with very small intervals causing GC churn — pool `WaitForSeconds` instances
- [ ] Coroutines don't survive scene unload (use `StopAllCoroutines` in despawn)

### 6. Clone Swarm project-specific conventions

- [ ] Weapon damage to enemies routes through `PlayerWeaponManager.<XxxServerRpc>` (not direct `Enemy.EnemyTakeDamage` on client)
- [ ] VFX spawned via `NetworkedVFXPool.Instance.PlayByType(VFXType.X, pos)` (NOT direct Instantiate)
- [ ] HitEffect / CritHitEffect is fired by `Enemy.NotifyHitClientRpc` — weapons should NOT also spawn it (causes duplicates, see Laser/Raycast fix in PR #5)
- [ ] SFX uses `SoundManager.Instance.PlaySfx(...)` / `PlayRandomSfx(clips, ...)` — never direct AudioSource
- [ ] WeaponBase/AbilityBase subclasses: VFX/SFX fields on prefab (not WeaponData/AbilityData)
- [ ] Static events fired on ALL clients (in `OnNetworkSpawn` before `IsServer` check) for HUD subscribers
- [ ] `NetworkedVFXPool` recursion guard not bypassed (depth counter exists for stack-overflow safety)
- [ ] `serverInvincible` flag respected when modifying enemy HP (used during boss phase transitions)

### 7. Race conditions & nullables

- [ ] Null check `NetworkManager.Singleton` before use
- [ ] Null check `PlayerObject` before dereferencing
- [ ] Null check `playermove`, `statManager` (optional refs)
- [ ] No assumption that ClientRpc arrives before next ServerRpc (network reordering possible)
- [ ] `NetworkObject.IsSpawned` checked before late operations

## Workflow

When asked to review:

1. **Identify scope**:
   - Single file? → read it fully
   - PR/branch? → `git diff main...HEAD --name-only`, focus on `.cs` files with NetworkBehaviour
   - Recent edits? → check `git diff` of working tree

2. **Read the file(s) in full** — NGO bugs often span ownership across multiple methods

3. **For each finding, output**:
   ```
   [SEVERITY] file.cs:line  — Brief title
     Issue: <what's wrong>
     Why:   <why it matters in multiplayer>
     Fix:   <concrete code change or pattern>
   ```

4. **Severity rubric**:
   - **CRITICAL** — desyncs, cheats possible, crashes, infinite loops
   - **WARNING** — works in happy path but breaks in edge cases (disconnect, late join, scene reload)
   - **NIT** — convention violation, naming, performance hint

5. **End with a summary**: counts per severity + top 3 issues to fix first

## Pitfalls reviewers themselves make

- ❌ Don't flag missing `IsServer` check on a method that's already ServerRpc-only — it's redundant but not a bug
- ❌ Don't suggest `[ClientRpc]` for purely-local UI updates — use C# events or NetworkVariable.OnValueChanged
- ❌ Don't recommend `Instantiate` over object pools without checking project's existing pool (NetworkedVFXPool, ObjectPool<T>)
- ✅ Always quote the exact file:line in findings
- ✅ Distinguish "missing convention" from "actual bug" — both worth flagging, but rank differently
