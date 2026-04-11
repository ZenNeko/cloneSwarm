# Game Design Document — Swarm Survivors

> **Genre:** Top-down Co-op Bullet Heaven / Horde Survival
> **Platform:** PC (Unity 6, Netcode for GameObjects)
> **Players:** 1-4 Online Co-op
> **Inspiration:** LoL Swarm, Vampire Survivors, Brotato

---

## 1. Game Overview

### 1.1 Concept
ผู้เล่น 1-4 คน เลือก Hero แล้วเอาตัวรอดจาก horde ของศัตรูที่ยากขึ้นเรื่อยๆ เป็นเวลา **15 นาที** โดยสะสม weapon, stat, และ upgrade ระหว่างทาง เป้าหมายคือ **ฆ่า Main Boss** ที่ spawn ตอนนาทีที่ 15

### 1.2 Win / Lose Condition
| Condition | Trigger |
|---|---|
| **WIN** | ฆ่า Main Boss สำเร็จ |
| **LOSE** | ผู้เล่นทุกคนตายพร้อมกัน |

### 1.3 Core Loop
```
เริ่มเกม → ฆ่า enemy → เก็บ EXP Orb → Level Up → เลือก Upgrade Card
  → ทำ Zone Objective → ได้ Super/Fusion → ฆ่า Mini Boss → ... → Main Boss → WIN
```

---

## 2. Heroes

แต่ละ Hero มี:
- **Starting Weapon** — อาวุธหลักที่ยิงอัตโนมัติ
- **Passive** — ability ที่ trigger อัตโนมัติตามเงื่อนไข (ไม่นับ weapon slot)
- **Q Ability** — active skill กด Q
- **E Ability** — active skill กด E (ultimate)

### 2.1 Riven
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | (กำหนดใน CharacterData) | - |
| **Passive** | Charge System | เดินสะสม Charge → เต็ม 100 = weapon ยิงพิเศษ |
| **Q — Valor** | Dash + AoE | กด Q → dash หาศัตรูใกล้สุด + AoE damage at landing, Exile active → Wind Slash radial เพิ่ม |
| **E — Blade of Exile** | Exile Mode | กด E → +50% move speed, charge rate x2, Valor ยิง Wind Slash เพิ่ม, จำกัดเวลา |

### 2.2 Gunner
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | (กำหนดใน CharacterData) | - |
| **Passive** | Kill Streak | ทุก 30 kills → buff +50% move speed, +50 Ability Haste เป็นเวลา 10s |
| **Q — Rocket Mode** | Mode Switch | กด Q → เปิด Rocket Mode, starting weapon ยิง Sticky Rocket แทน projectile ปกติ, จำกัดเวลา |
| **E — Giant Rocket** | Big AoE | กด E → ยิง Giant Rocket ระเบิดพื้นที่กว้าง |

### 2.3 Hunter
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | (กำหนดใน CharacterData) | - |
| **Passive** | Rapid Fire | ทุก 50 hits → force-fire ทุก weapon ที่ equipped ทันที (scale กับ Ability Haste) |
| **Q — Homing Missiles** | Lock-on AoE | กด Q → ยิง homing missile 5 ลูกหา enemy ที่ใกล้สุด (unique targets), แต่ละลูกระเบิด AoE |
| **E — Funnel Storm** | Ultimate | กด R → starting weapon ยิงเร็วขึ้น + spawn 5 Funnel โคจรรอบผู้เล่น ยิง laser หา enemy |

---

## 3. Weapon System

### 3.1 Weapon Tiers

| Tier | Max Level | ได้จาก | หมายเหตุ |
|---|---|---|---|
| **Normal** | Lv 1-5 | Level Up card | อาวุธพื้นฐาน ทุก hero สุ่มได้ (ยกเว้น exclusive) |
| **Super** | Lv 1 | Zone Objective Orb | Normal Lv5 + เงื่อนไข → upgrade เป็น Super |
| **Fusion** | Lv 1 | Auto (มี Super A + Super B) | รวม Super 2 ตัวตาม recipe |

### 3.2 Super Upgrade Conditions
Normal weapon ต้องถึง Lv5 + เงื่อนไขเพิ่มเติม (ทุกข้อต้องครบ):
- `StatAtLevel` — ต้องมี stat X ถึง level Y
- `WeaponAtLevel` — ต้องมี weapon X ถึง level Y
- `PlayerLevel` — shared level ถึง X

### 3.3 Weapon List

#### Projectile Weapons
| Weapon | Mechanic | Aim Mode |
|---|---|---|
| **Pistol** | ยิงกระสุนเดี่ยว | Auto/Mouse |
| **Shotgun** | ยิงกระสุนหลายลูก spread | Auto/Mouse |
| **Orbital Cannon** | ยิง projectile ลงจากบน | Auto |
| **Boomerang** | ขว้างบูมเมอแรง กลับมา | Auto |
| **TriRang** | ขว้าง 3 ทิศ | Auto |
| **Vortex** | spawn vortex ที่ดูดศัตรู + ยิง projectile | Auto |
| **Spiral Galaxy** | vortex variant | Auto |

#### Melee / AoE Weapons
| Weapon | Mechanic |
|---|---|
| **Dual Slash** | ฟันซ้าย-ขวา AoE |
| **Blade Storm** | หมุนฟัน 360 องศา |
| **Chainsaw** | ฟันต่อเนื่อง + chain beam |
| **Cyclone Blade** | หมุนฟันรอบตัว |
| **Whip** | ฟาดแส้ทิศเดียว + line AoE |

#### Beam / Raycast Weapons
| Weapon | Mechanic |
|---|---|
| **Laser** | ยิง beam เส้นตรง (line AoE) |
| **Railgun** | ยิง raycast ทะลุ pierce |
| **Lightning Chain** | สายฟ้าโซ่กระโดดหาศัตรูถัดไป |
| **Plasma Whip** | แส้ + raycast combo |

#### Orbital / Persistent Weapons
| Weapon | Mechanic |
|---|---|
| **Orbiter** | ลูกบอลโคจรรอบตัว damage ศัตรูที่ชน |
| **Radiant Aura** | AoE damage รอบตัวต่อเนื่อง |
| **Death Field** | persistent AoE + ระเบิดตอนตาย |

#### Explosive Weapons
| Weapon | Mechanic |
|---|---|
| **Grenade** | โยนระเบิด AoE |
| **Cluster Bomb** | ระเบิดแตกหลายลูก |
| **Minefield** | วางกับระเบิดบนพื้น |

#### Movement Weapons
| Weapon | Mechanic |
|---|---|
| **BunnyHop** | dash + ยิง projectile/AoE ขณะ dash |
| **Storm Bunny** | BunnyHop + lightning chain variant |

### 3.4 Fusion Recipes
Super Weapon A + Super Weapon B = Fusion Weapon

| Super A | Super B | Fusion Result |
|---|---|---|
| Stormcaller | (Thunder variant) | **Thunder Rail** |
| (อื่นๆ กำหนดใน WeaponFusionRecipe assets) | | |

### 3.5 Weapon Stats Per Level
แต่ละ level กำหนด:
- `damage` — base damage
- `cooldown` — วินาทีระหว่าง fire
- `projectileCount` — จำนวน projectile ต่อ fire
- `range` — ระยะ/รัศมี AoE
- `projectileSpeed` — ความเร็ว projectile
- `piercing` — ทะลุหรือไม่

### 3.6 Aim Modes
- **AutoNearest** — ล็อกศัตรูที่ใกล้ที่สุดอัตโนมัติ
- **MouseAim** — เล็งตามตำแหน่งเมาส์

---

## 4. VFX System

### 4.1 VFX Types (9 active)
| ID | Type | Usage |
|---|---|---|
| 0 | **HitEffect** | base hit impact — ทุก weapon แสดงเมื่อโดน enemy |
| 1 | **CritHitEffect** | critical hit — แสดงแทน HitEffect เมื่อ crit |
| 3 | **GrenadeExplosion** | Grenade / Rocket / Mine explosion |
| 5 | **OrbiterHit** | Orbiter / RadiantAura / DeathField impact |
| 6 | **OrbPickup** | เก็บ Objective Orb |
| 7 | **EnemyDeath** | ศัตรูตาย |
| 10 | **WhipSlash** | Whip tentacle slash |
| 11 | **SlashHit** | DualSlash / BladeStorm / Cyclone / Chainsaw |
| 15 | **VortexSpawn** | Vortex spawn indicator |
| 16 | **DashTrail** | BunnyHop dash trail |
| 17 | **MeteorAoE** | BunnyHop landing AoE |

### 4.2 Hit VFX Flow
```
Weapon fires → RollDamage(baseDmg, out isCrit) → isCrit?
  → true:  CritHitEffect (slot 1)
  → false: HitEffect (slot 0)
  
If weapon has extra VFX (e.g. SlashHit):
  → ShowVfx() plays extra VFX + base HitEffect/CritHitEffect overlay
```

### 4.3 Beam VFX
Beam weapons (Lightning Chain, Railgun, Laser) วาด **LineRenderer** จาก A→B
Hit particle ใช้ HitEffect/CritHitEffect ที่ `ShowBaseHitVfx` จัดการ (ไม่มี endpoint particle แยก)

### 4.4 VFX Scale System
- แต่ละ VFXType มี `designedRadius` (radius ที่ prefab ถูกออกแบบมา)
- `ComputeVfxScale(type, actualRange)` = actualRange / designedRadius
- Weapon ส่ง actual range (หลัง stat scaling) → VFX scale ตามอัตโนมัติ

---

## 5. Stat System

### 5.1 Player Stats (max 6 slots, Lv1-5 each)

#### Combat Stats
| Stat | Per Level | Effect |
|---|---|---|
| **Damage** | +10% | damage ทุก weapon |
| **Ability Haste** | +10 flat | ลด cooldown (formula: 100/(100+haste)) |
| **Critical Chance** | +8% | โอกาส crit (x2 damage) |
| **Area Size** | +11% | range / AoE radius ทุก weapon |
| **Projectile Count** | 1/1/2/2/3 | extra projectile ทุก weapon |
| **Duration** | +12% | projectile lifetime / range |

#### Survival Stats
| Stat | Per Level | Effect |
|---|---|---|
| **Max Health** | +150 flat | HP สูงสุด |
| **Armor** | +8 flat | ลด damage ที่รับ |
| **Health Regen** | +4 flat | HP ฟื้นต่อวินาที |

#### Utility Stats
| Stat | Per Level | Effect |
|---|---|---|
| **Move Speed** | +9% | ความเร็วเคลื่อนที่ |
| **Pickup Radius** | +35% | รัศมีดูด EXP orb |
| **EXP Bonus** | +10% | EXP ที่ได้รับ |

#### Full Build Bonuses (at max level only)
| Stat | Effect |
|---|---|
| **Gain Gold** | +25 gold |
| **Heal on Full Build** | heal 25% HP |

### 5.2 Stat Card Pool
- Weighted random: Common ~100, Uncommon ~70, Rare ~40, Epic ~15

---

## 6. Progression & Upgrade System

### 6.1 Shared Experience
- **EXP เป็นของกลาง** — ทุก player share level เดียวกัน
- Enemy drop EXP Orb → player เก็บ → บวก shared EXP pool
- Max Level: 30
- EXP to next level: `baseExpToLevel * expGrowthRate^level` (base=100, growth=1.25x)
- EXP Orb ดูดอัตโนมัติตาม Pickup Radius stat

### 6.2 Level Up Phase
```
Level Up → pause (timeScale=0) → ทุก player เห็น 3 Upgrade Cards → เลือก 1
  → Card = Weapon (new/level up) หรือ Stat (new/level up)
  → เลือกครบทุกคน (หรือหมดเวลา 30s) → resume
```

- Card pool: Weighted random จาก allWeapons + allStats
- Character-exclusive weapons: ออกเฉพาะ hero ที่กำหนด
- หมดเวลา → auto-pick สุ่ม

### 6.3 Zone Objective (Orb Reward)
- ทุก 2 นาที → spawn Zone Objective ที่จุดสุ่ม (ห่างจาก player)
- ผู้เล่นเข้าไปยืนใน zone → capture progress
- เสร็จ → ทุกคนได้ 1 Orb Reward card (upgrade weapon/stat ที่มีอยู่แล้ว)
- ให้ EXP bonus + heal ทุกคน

### 6.4 Super Upgrade
- Normal weapon Lv5 + เงื่อนไข SuperCondition ครบ → Zone Objective ให้โอกาส Super
- Super weapon มี 1 level, stat ที่แรงกว่า Normal Lv5

### 6.5 Fusion
- มี Super A + Super B ตาม recipe → auto-fuse เป็น Fusion weapon
- Fusion weapon มี 1 level, stat สูงสุด

---

## 7. Enemy System

### 7.1 Enemy Types

| Type | Behavior | Script |
|---|---|---|
| **Normal** | เดินตรงเข้าหา player, contact damage | `Enemy.cs` |
| **Charge** | เดินตรง + ชาร์จพุ่งเป็นระยะ (x4 speed, 0.4s) | `Enemy.cs` + `EnemyCharge.cs` |
| **Ranged** | ยืนห่าง + ยิง bullet-hell pattern (multi-shot spread) | `Enemy.cs` + `EnemyRanged.cs` |

### 7.2 Enemy Stats
- `speed` — ความเร็วเคลื่อนที่
- `contactDamage` — damage เมื่อชน player
- `maxHealth` — HP
- `expReward` — EXP ที่ drop เมื่อตาย
- Re-target player ใกล้สุดทุก ~1 วินาที (ข้าม player ที่ตายแล้ว)

### 7.3 Wave Scaling
- ทุก `waveDuration` (60s) → wave ถัดไป
- **HP**: +20% per wave
- **Speed**: +5% per wave (cap 2x)
- **EXP reward**: +15% per wave
- **Spawn rate**: เร็วขึ้น 10% per wave (min 0.3s)
- Wave config เปลี่ยน enemy composition ทุก 3 waves

---

## 8. Boss System

### 8.1 Mini Boss
- Spawn ทุก **5 นาที**
- ใช้ enemy prefab ธรรมดา scale stats ขึ้น:
  - HP = baseHP x 5 x waveHealthMult
  - Speed x 1.2
  - EXP x 4

### 8.2 Main Boss (นาทีที่ 15)
- Spawn ที่ 15:00 → **wave หยุด** (ไม่ spawn enemy ปกติ)
- มี 3 Phases ตาม %HP:

#### Phase 1 (100-60% HP) — Interval 4s
- **Circle AoE** ที่ตำแหน่ง player

#### Phase 2 (60-30% HP) — Interval 3s
- สลับ **Circle** ↔ **Cross** AoE (เส้นตัดกัน 4 ทิศ)

#### Phase 3 (30-0% HP) — Interval 2s (Enrage)
- วนรอบ 4 pattern:
  1. **Circle** — วงกลม AoE
  2. **Spread** — 5 เส้น spread fan shape
  3. **Donut** — วงแหวน (safe zone ตรงกลาง)
  4. **Cone** — พัด 90 องศาหน้า player ที่ใกล้สุด

### 8.3 Telegraph Zone System
- ทุก AoE มี **warning phase** (2.5s) → แสดงพื้นที่อันตราย → blink → damage
- AoE Types: `Circle`, `Line`, `Cross`, `Spread`, `Donut`, `Cone`
- Donut มี safe zone สีเขียว (teal) ตรงกลาง ไม่ blink
- Cone หันหน้าหา player ที่ใกล้สุด

---

## 9. Game Timeline

```
 0:00  เริ่มเกม, wave 1 spawn (5s delay)
 1:00  wave 2 (scaling ขึ้น)
 2:00  Zone Objective #1 spawn
 4:00  Zone Objective #2
 5:00  Mini Boss #1
 6:00  Zone Objective #3
 8:00  Zone Objective #4
10:00  Mini Boss #2
10:00  Zone Objective #5
12:00  Zone Objective #6
14:00  Zone Objective #7
15:00  MAIN BOSS spawn → wave หยุด
       Kill Boss → WIN
       All dead → LOSE
```

---

## 10. Multiplayer Architecture

### 10.1 Network Model
- **Authoritative Server** — Unity Netcode for GameObjects (NGO)
- Host = Server + Client
- Server จัดการ: enemy spawn, damage, health, boss AI, wave scaling
- Client จัดการ: input, VFX (local pool), UI

### 10.2 Key RPCs
| Direction | RPC | Purpose |
|---|---|---|
| Client→Server | `FireProjectileServerRpc` | spawn projectile NetworkObject |
| Client→Server | `FireMeleeServerRpc` | melee AoE damage |
| Client→Server | `FireRaycastServerRpc` | raycast pierce damage |
| Client→Server | `FireLineAoEServerRpc` | line AoE damage |
| Server→Client | `BroadcastVfxTypeServerRpc` | broadcast VFX to all clients |
| Server→Client | `BroadcastBeamServerRpc` | broadcast beam VFX |
| Server→Client | `BeginUpgradePhaseClientRpc` | start level up pause |

### 10.3 VFX Pool (Client-side)
- ทุก client pre-allocate object pool ของ VFX prefab
- ไม่ Instantiate/Destroy ขณะเล่น → ไม่มี GC spike
- Beam ใช้ LineRenderer pool แยก

---

## 11. UI / HUD

### 11.1 In-Game HUD
- **Game Clock** — นับจาก 0:00 → 15:00
- **EXP Bar** — shared EXP + level
- **Weapon Slots** — แสดง weapon ที่ equipped + cooldown
- **Ability HUD** — Q/E ability + cooldown/active timer
- **Passive Bar** — Charge bar (Riven) / Kill counter (Gunner) / Hit counter (Hunter)
- **HP Bar** — player health

### 11.2 Level Up UI
- 3 Upgrade Cards แสดงพร้อมกัน
- แต่ละ card แสดง: icon, name, description, level
- Countdown timer (30s)
- "X / Y players picked" status

### 11.3 End Screen
- WIN / LOSE
- Game time + Level reached

---

## 12. Technical Architecture

### 12.1 Key Managers (Singleton)
| Manager | Scope | Responsibility |
|---|---|---|
| `GameTimeline` | Server | Game clock, event schedule |
| `WaveManager` | Server | Wave scaling, enemy config |
| `EnemySpawner` | Server | Enemy instantiation |
| `BossManager` | Server | Mini/Main boss spawn |
| `ObjectiveManager` | Server | Zone objective spawn |
| `SharedExperienceManager` | Server+Client | Shared EXP/level, upgrade phase |

### 12.2 Per-Player Components
| Component | Responsibility |
|---|---|
| `playermove` | Movement, HP, death/respawn |
| `PlayerWeaponManager` | Weapon slots, fire RPCs, projectile spawn |
| `PlayerStatManager` | Stat levels, multiplier getters |
| `PlayerAbilityManager` | Ability slots (Q/E) |
| `UpgradeManager` | Card generation, apply upgrades |
| `ExperienceManager` | Local EXP collection → shared pool |
| `ChargeManager` | Riven charge system |

### 12.3 Weapon Architecture
```
WeaponBase (abstract)
  ├── PistolWeapon, ShotgunWeapon, ...  (Normal weapons)
  ├── StormcallerWeapon, ThunderRailWeapon, ...  (Super/Fusion)
  ├── BunnyHopWeapon, StormBunnyWeapon  (Movement weapons)
  ├── GunnerPassiveWeapon, HunterPassiveWeapon  (Passive, no slot)
  └── OrbiterWeapon  (Orbital persistent)

AbilityBase (abstract)
  ├── ValorWeapon  (Riven Q)
  ├── BladeOfExileWeapon  (Riven E)
  ├── GunnerRocketMode  (Gunner Q)
  ├── GunnerGiantRocket  (Gunner E)
  ├── HunterMissileAbility  (Hunter Q)
  └── HunterUltimate  (Hunter E/R)
```

### 12.4 Data Architecture (ScriptableObjects)
```
CharacterData  → hero identity, base stats, starting weapon, abilities
WeaponData     → weapon identity, tier, levels, super conditions
AbilityData    → ability identity, levels
StatData       → stat identity, values per level
WaveConfig     → enemy composition per wave bracket
WeaponFusionRecipe  → Super A + Super B = Fusion
WeaponUpgradeData   → legacy upgrade (deprecated)
```
