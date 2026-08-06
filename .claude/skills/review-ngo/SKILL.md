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
- [ ] 🔴 **No `NetworkObject.Spawn()` inside `OnNetworkSpawn`** — see below, this one is invisible on the host

#### Spawning inside `OnNetworkSpawn` — clients never receive the object

**Found 2026-07-29 as the cause of "clients can't see crates".** `CrateSpawnManager.OnNetworkSpawn`
looped its spawn points and spawned immediately. The host saw the crates (it instantiates locally);
no client ever did.

Every other spawner in the project waits for something — a death, a timer, the wave manager — and
every one of those replicates correctly. `EnemySpawner` even carries a comment saying it
deliberately does not spawn immediately, with no explanation, so this had been hit before and
worked around without being written down.

- **Symptom**: object exists on host, absent on all clients, no error anywhere
- **Fix**: defer to the first server `Update()` (set a flag), or hook a later event
- **Why it hides**: playing as host is the default during development, and the host is always correct

Treat any `Spawn()` reachable from `OnNetworkSpawn` as CRITICAL.

### 4b. Host is the server — "local-only" is a lie on the host

Anything that halts local simulation on the host halts it **for the entire room**.

- [ ] `Time.timeScale = 0` is never used as a "local" pause — on the host it freezes every client's enemies
- [ ] Local-only suspension uses an input flag (`GamePause.LocalInputSuspended`), never the time scale
- [ ] Comments claiming "local-only" are checked against the host case before being believed

**Found 2026-07-30**: `PauseMenuUI` carried the comment *"Time.timeScale stops local-only —
other players keep going"*. True on a client, false on the host, and only a two-machine test
exposed it. Round 8 split the paths: solo still freezes the world, multiplayer suspends only
the local machine's input.

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
- [ ] **No `NetworkObject` component on a VFX prefab** — VFX here is local-only and pooled; a
      `NetworkObject` that is never spawned makes NGO log *"can only be re-parented after being
      spawned"* on every pool return. Four beam prefabs carried one until 2026-07-30
- [ ] **Client-visible values are replicated, not read off a plain field** — `Enemy.maxHealth` is
      server-only and scaled server-side; anything a client draws must read `netMaxHealth`. Route
      writes through `ServerSetMaxHealth()` so a new call site can't set one without the other
- [ ] **Boss mechanics are registered with the boss** — telegraphs and tethers are independent
      NetworkObjects with no back-reference; `BossController.RegisterMechanic` exists so they
      despawn when the boss dies instead of landing an attack for a corpse
- [ ] **Nothing writes to a project asset at runtime** — no `sharedMaterial.EnableKeyword`, no
      mutating a ScriptableObject. The separation is real in this project; keep it that way

### 6b. Singleton getters that build their own GameObject

A lazy `Instance` getter that does `new GameObject()` + `AddComponent<T>()` **runs `Awake`
synchronously**. If `Awake` then reads the same `Instance` property, the assignment on the
previous line has not completed, so it builds another one — unbounded recursion on first use.

- [ ] `Awake` and `OnDestroy` touch the **backing field**, never the property
- [ ] `OnDestroy` especially — the getter would construct a GameObject during scene teardown

Hit in Round 7; would have stack-overflowed on the first enemy hit of any match. Compile is blind
to it.

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

### Verify the finding before reporting it — several "bugs" here were not

Every one of these looked like a real defect and survived until someone opened the file:

| Looked wrong | Actually |
|---|---|
| `ZoneObjective` has no `OnNetworkDespawn` and subscribes 3 `OnValueChanged`, one an unremovable lambda | Fine — the variables die with the object, the shader callback is null-guarded, the spawn boost is cleared on every exit path |
| 13 files override `OnNetworkDespawn` without calling `base` | Fine — `NetworkBehaviour.OnNetworkDespawn` is an **empty** virtual hook; cleanup lives in `InternalOnNetworkDespawn`. Only matters when an intermediate class has a body, and the one that does (`BossController`) is already called correctly |
| `MainBoss.GenerateLegacyConfig` runs on every client with no server gate | **Required** — `BossController.OnPhaseChangedClient` reads `config.phases` on the client and has no fallback. Gating it costs clients the phase-change effect |
| `BossController` / `EliteController` read the non-replicated `maxHealth` | Fine — both sit behind `if (!IsServer) return;` |

Two habits that catch this:
- **Read the enclosing method's guards**, not just the flagged line
- **Check the base class body** before claiming a missing `base.` call matters

### Grep hygiene in this repo

Asset paths contain spaces (`Assets/Prefab/Art Asset/…`, `Assets/Prefab/Exp orb/…`). Plain
`grep -rl | xargs` and whitespace-splitting `awk` mangle them **silently**. One sweep reported 16
dead prefab-registry entries when the real number was 1; it was caught only because the count
contradicted an earlier result.

Use `grep -rlZ … | while IFS= read -r -d ''`, `find -exec … +`, or tab-delimited output parsed
with `awk -F'\t'`. When a sweep produces a count, derive it a second way before reporting it.
