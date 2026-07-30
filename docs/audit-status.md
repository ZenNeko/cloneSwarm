# Audit — สแกนใหม่ทั้งโปรเจกต์ 2026-07-28

แทนที่ลิสต์ 21 ข้อเดิมของ Antigravity ซึ่งพลาดของจริงไปหลายอย่าง (AbilityBase leak,
VFX pool หา VisualEffect บน child ไม่เจอ, `GetHealthPercent` หารผิดตัว, Lance VFX graph หาย)
และตีกรอบเรื่อง authority แคบเกินไป

**บริบทที่ใช้จัดลำดับ**: เกม co-op PvE · **ยังไม่ตัดสินว่าจะเปิดห้องสาธารณะ แต่อยากเปิดในอนาคต**
→ หลักคือ *ทำสิ่งที่คุ้มไม่ว่าจะตัดสินใจทางไหน* และ *อย่าตัดสินใจที่ทำให้การเปิดห้องสาธารณะแพงขึ้น*

---

## ✅ ปิดไปแล้ว (PR #10)

| | |
|---|---|
| HP spoofing | server อ่าน `baseHealth` จาก `CharacterData` เอง |
| Material leak | `TelegraphZone` ใช้ `sharedMaterial` + `MaterialPropertyBlock` |
| Dead code | ลบ `ExperienceManager` |
| Burn DOT race · static event leak · ExpOrb double collect | มีอยู่ก่อนแล้ว ยืนยันแล้ว |
| `List` → `HashSet` · `InvokeRepeating` → coroutine · lose-check timer · VFX pool parenting | perf |
| `AbilityBase` MissingReferenceException | นอก audit เดิม |
| VFX pool หา `VisualEffect` บน child ไม่เจอ + Lance graph reference เสีย | นอก audit เดิม |
| `GetHealthPercent` หารด้วย field ที่ไม่ replicate | นอก audit เดิม |

---

---

# 🔍 รอบสอง — Sonnet อ่านลึก 85 ไฟล์ (2026-07-28)

รอบแรกใช้ grep สแกนรูปแบบ รอบนี้ให้ Sonnet 3 ตัวอ่านจริงใน `Weapon/` (44) ·
boss/objective/Elite · `Projectile/` + `Weapon/hero/` + orbs
**ทุกข้อด้านล่าง verify ด้วยตัวเองแล้ว** (agent เคยให้เลขบรรทัดคลาดเคลื่อนมาก่อน)

## ⚠️ รูปแบบร่วมที่สำคัญกว่าตัวบั๊ก — "แก้ไฟล์เดียว ลืมพี่น้อง"

โปรเจกต์นี้มีไฟล์ที่โครงสร้างเหมือนกันเป็นตระกูล และการแก้บั๊กมักลงแค่ไฟล์เดียว:

| แก้แล้ว | ยังไม่แก้ (โครงเดียวกัน) |
|---|---|
| `ExpOrb` มี `isCollected` guard | `HealingOrb` · `MagnetOrb` **ไม่มี** |
| `StormcallerWeapon` ส่ง `hitVfx: "None"` | `LightningChain` · `StormBunny` ส่ง `"HitEffect"` |
| `DeathFieldWeapon` มี distance gate | `ClusterBombWeapon` **ไม่มี** |
| `playermove.GetHealthPercent` แก้แล้ว (`f95b7ef4`) | `Enemy.GetHealthPercent` · `WorldHPBar` **ยังผิด** |

**เวลาแก้บั๊กต่อจากนี้ ให้ grep หาไฟล์ที่มีรูปแบบเดียวกันเสมอก่อนปิดงาน**

## 🔴 P0 ใหม่ — เห็นได้ / ใช้โกงได้ และแก้ไม่ยาก

### N1 `Enemy.maxHealth` ไม่ replicate → หลอดเลือดบอสผิดทุกจอ client

`Enemy.cs:56` เป็น `public float` ธรรมดา · `ApplyWaveScaling` (`:551`) มี `if (!IsServer) return;`
→ client ไม่เคยได้ค่าที่สเกลแล้ว แต่ `netHealth` (NetworkVariable) ได้

`WorldHPBar.cs:123` `Clamp01(netHealth.Value / _enemy.maxHealth)` และ `:136` แสดง `"{netHealth} / {maxHealth}"`

mini-boss สเกล ×5 จาก 30 → client เห็น `Clamp01(150/30)` = **หลอดเต็มค้าง** และตัวเลข **"150 / 30"**
หลอดจะเริ่มขยับตอนเลือดเหลือต่ำกว่า 30 เท่านั้น

**เทสต์ไม่เจอเพราะเล่นเป็น host** — ฝั่ง server ค่าถูกเสมอ
`EliteController.cs:95` `_enemy.maxHealth += def.shieldHP` ก็โดนแบบเดียวกัน

> **แก้แล้ว (Round 1 + verify)** — `Enemy.netMaxHealth` + `ServerSetMaxHealth()` setter ·
> `WorldHPBar` · **`MiniBossBarEntry.cs:71-72,85`** ← ตัวนี้ audit เดิมและแผน Round 1 **ตกทั้งคู่**
> mini-boss มีหลอด 2 ที่ (world bar + HUD list entry) เจอตอน grep ก่อนปิดงาน

### N2 `HealingOrb` / `MagnetOrb` เก็บซ้ำได้ → ฮีลสองเท่า

โครงเหมือน `ExpOrb` เป๊ะ — `Update()` เช็คระยะ → `Collect()` และ `OnTriggerEnter` → `Collect()`
เป็นสองทางเข้าอิสระ แต่**ไม่มี guard** (`ExpOrb` มีที่ `:89-95`)

`Despawn`/`Destroy` มีผลสิ้นเฟรม → ถ้าเข้าทั้งสองทางในเฟรมเดียว `pm.HealPercent()` ทำงาน **2 ครั้ง**

### N3 HitEffect ซ้อน — บั๊กที่ `CLAUDE.md` บอกว่าแก้แล้ว กลับมา 3 จุด

- `ValorWeapon.cs:137-139` — เรียก `FireMelee()` แล้ว `BroadcastVfxTypeServerRpc(..., "HitEffect")` ซ้ำ
  **ยิงที่ตำแหน่งผู้เล่นแม้ไม่โดนศัตรูเลย**
- `LightningChainWeapon.cs:28` · `StormBunnyWeapon.cs:114,145` ส่ง `hitVfx: "HitEffect"` เข้า
  `FireChainServerRpc` ซึ่ง `PlayerWeaponManager.cs:1344-1346` ทำ `EnemyTakeDamage` (HitEffect อัตโนมัติ)
  แล้ว `BroadcastBeamClientRpc` → `VFXFactory.cs:48` ยิงซ้ำ **ทุกข้อของ chain**

### N4 `ClusterBombWeapon.cs:95` ระเบิดจากศัตรูที่ตายทั่วแมพ

subscribe `Enemy.OnAnyEnemyDiedAt` (static ระดับโลก) แต่**ไม่มี distance gate**
→ ศัตรูตายที่ไหนก็ได้ ตายด้วยอาวุธเพื่อนก็ได้ = ระเบิด + ลูกย่อย 3 ลูกฟรี
`DeathFieldWeapon.cs:65-66` โครงเดียวกันแต่มี `if (dist > fieldRadius) return;`

## 🟠 P1 ใหม่

- **กลไกบอสมีชีวิตต่อหลังบอสตาย** — `BossController.OnDeath()` หยุดแค่ coroutine ตัวเอง
  ไม่ตามเก็บ `TelegraphZone`/`BossTether`/`FloorHazard` ที่ spawn ไปแล้ว (เป็น NetworkObject อิสระ
  ไม่มี back-reference กลับหาบอส) → **โดนดาเมจจากบอสที่ตายไปแล้ว**
- **`ZoneObjective.cs:172`** quest Survive ตั้ง `timeoutAt = float.MaxValue` → ถ้าคนออกจากโซนถาวร
  coroutine ไม่มีวันจบ → objective ไม่ despawn · `extraSpawnsPerTick` ค้างบูสต์ทั้งเกม · กินสล็อตถาวร
- **crit flag หายระหว่างทาง** — `Projectile.cs:77` เรียก `EnemyTakeDamage(damage)` ไม่ส่ง `isCrit`
  ทั้งที่อ่าน field เดียวกันที่ `:83` · เหมือนกันที่ `BoomerangProjectile.cs:87` ·
  `GunnerGiantRocket.cs:68` และ `HunterMissileAbility.cs:74` ทิ้งด้วย `out bool _`
  → ดาเมจคูณถูก แต่ **VFX crit ไม่ขึ้น**
- **`BossManager.cs:131`** `OnMainBossKilled` ไม่มี re-entrancy guard และ `EnemyTakeDamage` ไม่มีธง
  "ตายแล้ว" → 2 นัดในเฟรมเดียว = event ยิงซ้ำ

## 🔴 P0 เพิ่มจากรอบ UI + Data (ตรวจครบ ~170/171 ไฟล์แล้ว)

### N5 `Time.timeScale` ไม่มีใครเป็นเจ้าของ → softlock จากการกด ESC

**3 ระบบเขียน `Time.timeScale` อิสระกัน ไม่รู้จักกัน**:
`PauseMenuUI` (`:102`,`:112`) · `SharedExperienceManager` (`:225`,`:234`,`:291` ยิงผ่าน ClientRpc ทุก client) ·
`WinLoseUI` (`:121`)

`PauseMenuUI.Pause()` จำค่าเดิมด้วย `prevTimeScale = Time.timeScale` แล้ว `Resume()` เขียนกลับดื้อๆ

- **กด ESC ตอนเลือกการ์ด** (timeScale = 0 อยู่แล้ว) → จำ 0 → phase จบเซ็ตเป็น 1 → Resume เขียน 0 กลับ
  → **เกมค้างถาวร ไม่มี UI ขึ้น ต้องโหลดฉากใหม่**
- **กด ESC หลังจบเกม** → Resume เขียน 1 → **โลกเดินต่อหลังจอ win/lose**

`PauseMenuUI.Update():90-92` ไม่เช็ค `WinLoseUI.IsShowing` ทั้งที่เป็น public static ที่มีอยู่แล้ว
วิธีแก้ที่ถูกคือให้มีเจ้าของเดียว (counter/stack) ไม่ใช่ต่างคนต่างเขียน

### N6 `FlowFieldPathfinder.cs:113` bake บนทุก client

`Update()` มี gate `if (!nm.IsServer) return;` พร้อมคอมเมนต์ว่า client ไม่ใช้ flow field
แต่ `Start()` เรียก `BakeWalkable()` **ไม่มี gate** → `gridSize` 100×100 = `Physics.CheckSphere` **10,000 ครั้ง**
บนทุก client ตอนโหลดฉาก ทั้งที่ไม่มีใครใช้ผลลัพธ์ → กระตุกฟรี แก้บรรทัดเดียว

## 🟠 P1 เพิ่ม

- **`UpgradeManager.cs:80`** `OnForceAutoPick` ใช้เส้นทาง level-up เสมอ แม้อยู่ใน Orb Phase
  → `PlayerUpgradePickedServerRpc` มี `if (!isUpgradePhase) return;` เลย no-op เงียบๆ
  → ตัวนับ "รอผู้เล่น" ขาดไปหนึ่งคนทุกครั้งที่มีคน auto-pick ตอน orb timeout
- **`CrateSpawnManager:97` · `OrbDropManager:71` · `LootDropTable:88`** เช็ค `if (netObj != null) Spawn()`
  แต่**ไม่มี else log** → prefab ที่ลืมใส่ `NetworkObject` จะ spawn แค่ฝั่ง server client ไม่เห็นอะไร
  และไม่มีอะไรบอกว่าทำไม
- **`DevTools.cs:240`** canvas `DontDestroyOnLoad` แต่ script เป็น scene object → reload ฉากทีได้ canvas ค้างเพิ่ม
  **`DevTools.cs:92`** `OnKillAllEnemies` ไม่เช็ค `IsServer` ต่างจากทุก handler อื่นในไฟล์เดียวกัน
  *(เครื่องมือ dev ไม่ใช่โค้ดที่ผู้เล่นแตะ — กระทบตอนเทสต์เท่านั้น)*

## ✅ สิ่งที่ตรวจแล้วสะอาด (ไม่ต้องกลับมาดูอีก)

- **ไม่มีการเขียนทับ ScriptableObject ตอน runtime เลย** — การแยก data/presentation ตาม `CLAUDE.md` ทำได้จริง
  (มีแค่ `MiniBossConfig.OnValidate` ที่ migrate legacy ซึ่งเป็น Editor-only และตั้งใจ)
- **HUD ปลอดภัยกับ 4 ผู้เล่น** — ทุกตัวที่ต้องหา local player กรอง `pm.IsOwner` ถูกต้อง
  ไม่มีที่ไหนอ่าน state ของผู้เล่นคนอื่นผิด
- **static event pairing ถูกหมด** ใน `GameHUD` `LevelUpUI` `WinLoseUI` `PauseMenuUI` `BossHUDUI`
  `ObjectiveIndicatorUI` `FollowCamera` `UpgradeManager`
- **card pool ของ `UpgradeManager` ถูกต้อง** — `used` HashSet กันการ์ดซ้ำ · level indexing ตรงกับ `WeaponStatHUD` ·
  pool ว่างไม่ crash
- **`WaveConfig` weight มี `Mathf.Max(1, weight)`** กันหารศูนย์แล้ว
- data layer ~25 ไฟล์ (`WeaponData` `AbilityData` `CharacterData` `StatData` boss action ทั้งหมด
  `MenuManager` `OnlineMenuUI` `PlayerSpawnManager` `WaveManager`) ไม่พบปัญหา

## 🟡 P3 ใหม่ (เล็ก)

`GrenadeProjectile.cs:114` · `MineObject.cs:79` ใช้ `Instantiate` ตรงเมื่อ designer ใส่ prefab
(fallback ไป pool ถูกอยู่แล้วเมื่อเว้นว่าง) · `BouncingSpikeProjectile` ไม่มี hit-dedup ต่างจาก
`Boomerang` ที่มี `hitIds` · `BunnyHopWeapon.cs:115` ไม่เช็คตายระหว่าง dash ·
`SplitterBombWeapon.cs:26,39` ทิ้ง crit flag · `FenceWeapon.cs:54` copy cooldown loop ของ base มาทั้งดุ้น ·
projectile 3 ตัวไม่เช็ค `IsSpawned` ต่างจากพี่น้อง · `BossTether`/`FloorHazard` ไม่เรียก
`base.OnNetworkDespawn()` · `MainBoss.cs:50` สร้าง config graph บนทุก client ·
`FloorHazard.Activate()` ไม่มีใครเรียกใน C# · stub ตายค้าง `MinefieldWeapon.cs` · `PlasmaWhipWeapon.cs`

---

---

# 🔧 รอบสาม — ชั้น config (`.prefab` / `.asset` / scene) 2026-07-29

audit สองรอบแรกอ่าน **โค้ดล้วน** แต่บั๊กสองตัวที่เจอจากการเล่นจริงวันนี้
(`NetworkAnimator` NRE ท่วม 590 อัน · VFX pool spam) **ไม่มีตัวไหนอยู่ในโค้ดเลย**
รอบนี้เลยไล่ชั้นที่ไม่เคยมีใครแตะ

## 🔴 พบ

### C1 `NetworkAnimator` บน `Purple 1.prefab` ไม่ได้ assign Animator
`m_Animator: {fileID: 0}` ทั้งที่มี `Animator` (พร้อม controller + avatar) อยู่บน GameObject เดียวกัน
→ `CheckParametersChanged()` deref null ทุก `NetworkUpdate` tick = **1 error/เฟรม ตลอดที่บอสมีชีวิต**
`ParameterEntries: []` ว่าง และ **ไม่มีสคริปต์บอสตัวไหนแตะ Animator เลย** → component นี้ไม่ได้ sync อะไร
**ผู้ใช้เลื่อนไว้ก่อน** (ยังเป็น mock-up art) — ทางแก้: ถอด component ทิ้ง หรือ assign + ตั้ง ParameterEntries

### C2 `CLAUDE.md` ระบุ path ของ scene ผิด → **แก้แล้ว**
เขียนว่า `Assets/Scenes/` ซึ่ง**ไม่มีโฟลเดอร์นั้นอยู่จริง** ของจริงคือ `Assets/GameScenes/`
เป็นเอกสารที่ agent อ่านเป็นอันดับแรก — ผิดตรงนี้คือพา agent ไปหาไฟล์ผิดที่ทุกเซสชัน

## 🟠 พบ — ยังไม่แก้

### C3 build ใส่ scene ที่ไม่ใช่ของจริงไป 3 จาก 5
`EditorBuildSettings` เปิดไว้ 5 scene:

| scene | ขนาด | มีอะไร |
|---|---|---|
| `MenuScene.unity` | — | ของจริง |
| `SampleScene.unity` | 1.8M | **ของจริง** — GameTimeline · WaveManager · SharedExp · NetworkedVFXPool |
| `SampleScene black.unity` | 164K | stub เกือบว่าง มีแค่ EnemySpawner **ไม่มี NetworkedVFXPool** |
| `Scene 2.unity` | 164K | stub เกือบว่าง เหมือนกัน |
| `WeaponTestScene.unity` | 648K | dev harness |

ไม่กระทบ runtime เพราะโปรเจกต์โหลด scene **ด้วยชื่อ** ไม่ใช่ index (`WinLoseUI:134` · `PauseMenuUI.menuSceneName`)
→ index เลื่อนไม่พัง แต่เป็นขยะที่ติดไปกับ build

### C4 `DefaultNetworkPrefabs.asset` มี entry ตายค้าง 1 ตัว
guid `e651dbb3fbac04af2b8f5abf007ddc23` ไม่ตรงกับ `.prefab` ไหนในโปรเจกต์ (51/52 resolve)
→ NGO เตือนตอน startup · ไม่ทำให้พัง

## 🟡 ข้อมูลค้าง ไม่ใช่บั๊กที่ทำงานผิดตอนนี้

`weaponVfxType: -1` และ `secondaryVfxType: -1` บน `OrbitalCannonWeapon.prefab` · `StormBunnyWeapon.prefab`
เป็นค่า **int ค้างจากยุคที่ field เคยเป็น enum** ตอนนี้ field เป็น `string` (`WeaponBase.cs:36,43`)
→ deserialize ไม่ลง กลายเป็นค่าว่าง → `ResolveHitVfx()` ตกไปใช้ fallback
**ตรวจแล้วไม่พัง**: `OrbitalCannonWeapon.cs` ไม่เรียก VFX เลย · `StormBunnyWeapon.cs:68` fallback เป็น
`"MeteorAoE"` ซึ่งมีจริงใน database — แต่เป็น data rot ที่ควรล้าง

## ✅ ชั้น config ที่ตรวจแล้วสะอาด

- **NetworkObject prefab ครบทะเบียนทั้ง 47 ตัว** — ไม่มีตัวไหนตกจาก `DefaultNetworkPrefabs.asset`
  (กฎข้อ 7 ของ `CLAUDE.md` ที่ว่าลืมแล้ว NGO ไม่ยอม spawn)
- **ไม่มี missing script** (`m_Script: {fileID: 0}`) ใน `Assets/Prefab/` และ scene ทั้งหมด
- **`VFXDatabase` ไม่มี key ซ้ำ และไม่มี entry ไหน prefab เป็น null** — สองเคสนี้ `BuildPools` จะ skip เงียบๆ
- VFX key ที่โค้ดอ้างแต่ไม่มีใน database มี 4 ตัว (`LanceThrust` · `SlashAoE360` · `VortexSpawn` · `Default`)
  **ยืนยันแล้วว่าเป็น fallback ที่ไม่มีใครไปถึง** ตรงกับที่รอบก่อนบันทึกไว้

> ⚠️ **บทเรียนวิธีทำงาน**: ระหว่างไล่รอบนี้ผมได้ตัวเลขผิด 2 ครั้งจาก path ที่มีช่องว่าง
> (`Assets/Prefab/Art Asset/...`) ทำ `xargs` และ field splitting ของ `awk` เพี้ยน
> รอบหนึ่งได้ "16 entry ตาย" ซึ่งของจริงคือ **1**
> **เช็คของโปรเจกต์นี้ต้อง null-delimited หรือ tab-delimited เสมอ**

---

# 🔍 รอบสี่ — NetworkObject lifecycle 2026-07-29

ไล่หลังเจอบั๊กจากการเล่นจริง: **client ไม่เห็น Crate แต่ host เห็น**

## 🔴 L1 `CrateSpawnManager` spawn ใน `OnNetworkSpawn` — client ไม่เคยได้รับ

`CrateSpawnManager.OnNetworkSpawn()` ([:34-45](../Assets/Script/CrateSpawnManager.cs:34))
วน `spawnPoints` แล้ว spawn กล่องทุกจุด **ทันทีที่ manager network-spawn บน server**

**หลักฐาน — เป็น outlier ตัวเดียวในโปรเจกต์**

| ตัว spawn | spawn ตอนไหน | client เห็นไหม |
|---|---|---|
| **`CrateSpawnManager`** | **ใน `OnNetworkSpawn`** | ❌ **ไม่เห็น** |
| `OrbDropManager:74` | ตอนศัตรูตาย | ✅ เห็น |
| `ObjectiveManager:82` | ตาม timer | ✅ เห็น |
| `BossManager:81,123` | ตาม timeline | ✅ เห็น |
| `EnemySpawner` | รอ `WaveManager` เรียก | ✅ เห็น |

ทั้งหมดใช้ `Spawn(true)` **เหมือนกันเป๊ะ** ต่างกันแค่**จังหวะ** และตัวที่ต่างคือตัวที่พัง

`EnemySpawner.cs:38` มีคอมเมนต์เขียนไว้เองว่า *"WaveManager เรียก `StartSpawning()` เอง — ไม่ spawn ทันที"*
เหมือนเคยมีคนเจอปัญหานี้มาก่อนแล้วเลี่ยงไว้ แต่ไม่ได้เขียนบันทึกว่าทำไม

**คำอธิบายที่น่าจะเป็น** (ยังไม่พิสูจน์ในระดับ NGO internals): การ spawn ระหว่างที่
`NetworkManager` ยังทำ spawn pass ของตัวเองไม่จบ ทำให้ object ไม่ติดไปกับ payload
ที่ sync ให้ client — host เห็นเพราะ instantiate ในเครื่องตัวเอง

**ทางแก้ที่เสนอ**: เลื่อน initial spawn ออกจาก `OnNetworkSpawn` — คลาสนี้มี `Update()`
ที่ gate `IsServer` อยู่แล้ว ใส่ธง `_initialSpawnPending` แล้วทำใน `Update()` เฟรมแรกแทน
**ไม่ต้องเพิ่ม coroutine ไม่ต้องแตะ NGO event**

**เทสต์**: 2 instance → client ต้องเห็นกล่องตั้งแต่เข้าเกม

## ✅ ตรวจแล้วไม่ใช่บั๊ก — อย่าเสียเวลาอีก

- **`ZoneObjective` ไม่มี `OnNetworkDespawn` เลย** และ subscribe `OnValueChanged` 3 ตัว
  (`:118-120`) โดยตัวหนึ่งเป็น lambda ที่ถอนไม่ได้ — **ไม่ใช่บั๊ก**
  NetworkVariable ตายพร้อม object · `UpdateShader` มี null guard (`:519`) ·
  spawn boost ถูกคืนครบทุกเส้นทางรวมถึง `ExpireAndDespawn` (`:367`)
- **NetworkBehaviour อื่นทั้งหมด** subscribe/unsubscribe ระหว่าง `OnNetworkSpawn`/`OnNetworkDespawn` สมดุลหมด

## ✅ `MainBoss.GenerateLegacyConfig` — ปิดเคส **ไม่ใช่บั๊ก ห้ามแก้**

เลื่อนมา 2 รอบเพราะ "ดูเหมือน N6 แต่ยังวิเคราะห์ไม่ครบ" ตอนนี้ครบแล้ว

`OnNetworkSpawn` เรียก `GenerateLegacyConfig()` **โดยไม่มี `IsServer` gate** ซึ่งดูเหมือนบั๊ก
แบบเดียวกับ FlowField แต่**ต้องเป็นแบบนั้น** — `BossController.OnPhaseChangedClient(int)`
([:133-135](../Assets/Script/BossController.cs:133)) อ่าน `config.phases[phaseIndex]`
และรันฝั่ง client

→ gate เป็น server-only = client ได้ `config == null` → return ตั้งแต่ `:133`
→ **เสียเอฟเฟกต์ตอนบอสเปลี่ยน phase บนจอ client**

`phase2Threshold`/`phase3Threshold` มี fallback ไป `legacyPhase*` อยู่แล้วก็จริง
แต่ `OnPhaseChangedClient` **ไม่มี fallback** — นั่นคือส่วนที่ทำให้แก้ไม่ได้

## ⚠️ สิ่งที่ grep ทำไม่ได้ — ต้องใช้เครื่องมืออื่น

กวาดหา "`Instantiate` prefab ที่มี `NetworkObject` แต่ไม่มีใครเรียก `Spawn()`" **ทำด้วย grep ไม่ได้**
เพราะต้อง resolve ว่าตัวแปรนั้นชี้ไป prefab ไหน (อยู่ใน Inspector) และระยะห่างระหว่าง
`Instantiate` กับ `Spawn` ไม่คงที่ (`ObjectiveManager` ห่างกัน 23 บรรทัด)

→ ถ้าอยากปิดช่องนี้จริงต้องเขียน **Editor script** ที่เดินดู serialized field ทุกตัว
แล้วเช็คว่า prefab ปลายทางมี `NetworkObject` หรือไม่ · ยังไม่ได้ทำ

---

## P0 เดิม (จากรอบ grep) — ยังใช้อยู่

### P0.1 buff ชั่วคราวไม่มีผลกับ client  `playermove.cs`

`tempMoveSpeedBonus` ([:295](../Assets/Script/playermove.cs:295)) และ `tempHealthRegenBonus`
([:298](../Assets/Script/playermove.cs:298)) เป็น field ธรรมดา ไม่ replicate
แต่ถูกใช้ในโค้ดที่รัน **ฝั่ง server**:

- [:111-112](../Assets/Script/playermove.cs:111) `totalRegen = healthRegenPerSecond + tempHealthRegenBonus` แล้ว `if (IsServer ...)`
- [:155](../Assets/Script/playermove.cs:155) `effectiveSpeed = moveSpeed * ... * (1f + tempMoveSpeedBonus)`

weapon เป็นคนเซ็ตค่านี้บน **owner client** → สำเนาฝั่ง server ของผู้เล่นที่ไม่ใช่ host ยังเป็น 0
→ **Blade of Exile (speed) และ SupportArena (regen) ไม่ทำงานเลยสำหรับทุกคนที่ไม่ใช่ host**

ไม่ใช่เรื่อง anti-cheat — เป็นของที่ผู้เล่นจ่ายการ์ดไปแล้วไม่ได้ของ
**คุ้มทำทันทีไม่ว่าจะเปิดห้องสาธารณะหรือไม่**

---

## P1 — perf ที่ทุกคนรู้สึก

### P1.1 Hit RPC ยิงทุกครั้งที่โดนดาเมจ  `Enemy.cs`

[:369](../Assets/Script/Enemy.cs:369) `NotifyHitClientRpc` ยิงหาทุก client ทุกครั้งที่ enemy โดนตี
ในเกม bullet-heaven ที่โดนตีหลายสิบครั้งต่อวินาที นี่คือต้นทุน network ที่ใหญ่ที่สุดในเกม

**ไม่ใช่งานกล** — `CLAUDE.md` ระบุว่า `HitEffect`/`CritHitEffect` มาจาก RPC ตัวนี้
การ throttle เปลี่ยนสิ่งที่ผู้เล่นเห็น ต้องเลือกก่อนว่า batch, throttle ต่อ enemy,
หรือให้ฝั่งคนยิง predict เอง

---

## P2 — guard rail ราคาถูก ที่ลดต้นทุนตอนเปิดห้องสาธารณะ

ทำได้ **โดยไม่ต้องรื้อสถาปัตยกรรม** และไม่เสียเปล่าถ้าสุดท้ายไม่เปิดห้อง

### P2.1 ปิดการแจก progression ตามใจ ← คุ้มที่สุดในกลุ่มนี้

`PlayerWeaponManager` เปิด RPC เหล่านี้ให้เรียกเมื่อไหร่ก็ได้:
`AddWeaponServerRpc(string)` ([:116](../Assets/Script/PlayerWeaponManager.cs:116)) ·
`UpgradeWeaponServerRpc` ([:138](../Assets/Script/PlayerWeaponManager.cs:138)) ·
`ReplaceWeaponServerRpc` ([:160](../Assets/Script/PlayerWeaponManager.cs:160)) ·
`FuseWeaponsServerRpc` ([:184](../Assets/Script/PlayerWeaponManager.cs:184)) ·
`SpawnPassiveWeaponServerRpc` ([:210](../Assets/Script/PlayerWeaponManager.cs:210)) ·
`PlayerStatManager.ApplyStatServerRpc` ([:154](../Assets/Script/PlayerStatManager.cs:154))

→ เรียกเอาอาวุธอะไรก็ได้ อัปเกรดกี่ครั้งก็ได้ **ข้ามระบบการ์ดทั้งระบบ**
ร้ายแรงกว่าการปลอมดาเมจ เพราะเป็น progression ถาวร

**แก้ถูกๆ ได้โดยไม่ต้องมี stat sync**: `SharedExperienceManager` รู้อยู่แล้วว่าตอนนี้อยู่ใน
upgrade phase หรือไม่ และใครยังไม่ได้เลือก → ให้ server เช็คว่า
"ผู้เล่นคนนี้กำลังอยู่ใน upgrade phase และยังไม่ได้ใช้สิทธิ์" ก่อนอนุมัติ
ปิดช่องได้เกือบหมดด้วยเงื่อนไขเดียว **ไม่ต้องแตะ 1.4**

### P2.2 clamp บัฟที่ client กำหนดเอง

`AddShieldServerRpc(float amount)` ([:1038](../Assets/Script/PlayerWeaponManager.cs:1038)) ·
`ApplyExpMultiplierServerRpc(float)` ([SharedExp:129](../Assets/Script/SharedExperienceManager.cs:129) — clamp 1-5 แล้ว แต่ยังส่งค่าเองได้) ·
`ApplySuperBigAoEExplosionServerRpc(..., expMultiplier)` ([:1132](../Assets/Script/PlayerWeaponManager.cs:1132))

clamp เป็นแค่พลาสเตอร์ (พิสูจน์แล้วตอนแก้ HP spoofing) **แต่ราคาถูกมากและกันเคสสุดโต่งได้**
ทำเป็นมาตรการชั่วคราวจนกว่าจะตัดสินใจเรื่องห้องสาธารณะ

---

## P3 — เล็ก ทำเมื่อว่าง

- **4.1** `TelegraphZone.cs:295` ยัง `Instantiate(detonateVfxPrefab)` แทน `NetworkedVFXPool`
  — ต้องเพิ่ม entry ใน `VFXDatabase` ก่อน (งาน Editor + โค้ด)
  ยังมี `Instantiate` VFX ตรงๆ อีก 3 จุดที่ audit เดิมไม่ได้ลิสต์:
  `CharacterAnimationEvents.cs:50` · `MineObject.cs:79` · `ObjectiveOrb.cs:147`
- **3.5** `timeScale = 0` ไม่มี failsafe ฝั่ง client (server มี `ForceAutoPick` timer แล้ว)
- fallback string ที่ชี้ไป key ที่ไม่มีใน `VFXDatabase` — `LanceThrust` · `SlashAoE360` ·
  `VortexSpawn` · `Default` ตอนนี้ไม่มีตัวไหนถูกเรียกใช้จริง (prefab ตั้ง `weaponVfxType` ครบ)
  เป็นชื่อตายในโค้ด ไม่ใช่ระเบิดเวลา

---

## P4 — เลื่อน แต่ต้องไม่ทำให้แพงขึ้น

**การรื้อ authority ทั้งก้อน** — damage/crit คำนวณฝั่ง server, card validation, stat sync
(เดิมคือ 1.2 / 1.3 / 1.4 / 2.6 — ทั้งหมดติดคอขวดเดียวกันคือ server ไม่รู้ progression ของผู้เล่น)

รอการตัดสินใจเรื่องห้องสาธารณะ แต่ระหว่างนี้:

> **กฎกันหนี้บานปลาย** — เวลาเพิ่มอาวุธ/สกิลใหม่ อย่าเพิ่ม ServerRpc ที่รับค่า `damage`
> เป็นพารามิเตอร์เพิ่มอีก ทุกตัวที่เพิ่มวันนี้คือของที่ต้องรื้อวันหน้า
> ถ้าเลี่ยงไม่ได้ อย่างน้อยให้ RPC ส่ง "ชื่ออาวุธ + level" แทนตัวเลขดิบ
> จะได้ย้ายไปคำนวณฝั่ง server ทีหลังโดยไม่ต้องแก้ call site

---

## ❌ ไม่ทำ

**4.2 magic numbers** — ประโยชน์เป็นความสวยงาม แต่ต้องแตะหลายไฟล์และเสี่ยงเปลี่ยนค่าโดยไม่ตั้งใจ
ได้ไม่คุ้มเสีย

---

## สิ่งที่สแกนแล้วพบว่า **ไม่มีปัญหา** (ไม่ต้องเสียเวลาอีก)

- **ไม่มี `RequireOwnership = false` เลยสักตัว** → client ยุ่งกับผู้เล่นคนอื่นไม่ได้ โกงได้แค่ให้ตัวเอง
  นี่คือเหตุผลหลักที่ทำให้เลื่อน P4 ได้อย่างสบายใจ
- **Owner-write NetworkVariable มีตัวเดียว** — `PlayerVisual._netSpeed` ใช้ขับ animation ไม่ใช่ gameplay
- **static event ไม่รั่ว** — ไล่ทุกไฟล์ที่ subscribe แล้ว สมดุลหมด
  ยกเว้น `OnlineMenuUI` ที่ `-=` มากกว่า `+=` (14 vs 10) ซึ่งไม่เป็นอันตราย
  เคส `AbilityBase` เป็นเรื่องลำดับการทำลาย ไม่ใช่ลืม unsubscribe
