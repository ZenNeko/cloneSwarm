# Game Design Document — Swarm Survivors

> **Genre:** Top-down Co-op Bullet Heaven / Horde Survival
> **Platform:** PC (Unity 6 `6000.7.0b1`, Netcode for GameObjects 2.13)
> **Players:** 1-4 Online Co-op (เล่นคนเดียวแบบออฟไลน์ได้)
> **Inspiration:** LoL Swarm, Vampire Survivors, Rabbit and Steel, FFXIV (กลไกบอส)
>
> **อัปเดตล่าสุด:** 2026-09-23 · เวอร์ชันเกม 0.3.5 · ถ้าเอกสารนี้ขัดกับโค้ดหรือ asset ให้ถือโค้ดเป็นหลัก

---

## 1. Game Overview

### 1.1 Concept
ผู้เล่น 1-4 คน เลือก Hero แล้วเอาตัวรอดจาก horde ของศัตรูที่ยากขึ้นเรื่อยๆ เป็นเวลา **15 นาที** (ค่าเริ่มต้น · แต่ละแมพตั้งเองได้) โดยสะสม weapon, stat, augment และ upgrade ระหว่างทาง เป้าหมายคือ **ฆ่า Main Boss** ที่ spawn ตอนจบเวลา

> [!NOTE]
> **สถานะโครงการ (Project Status):** ยังเป็น **Proof of Concept / Mock-up** เน้นทดสอบระบบเครือข่าย (Multiplayer Synchronization) และกลไกการต่อสู้ โมเดล 3D ของตัวละคร ศัตรู และฉากยังเป็น **Placeholder Art** ที่จะทำใหม่ทั้งหมดในเวอร์ชันจำหน่าย
> ส่วนที่ถือว่าเป็นของจริงแล้ว: ระบบ VFX · ระบบ Telegraph ของบอส · ชุด UI สไตล์ **Persona 3 Reload (P3R)** ที่ทำครบทุกจอแล้ว (ข้อ 11)

### 1.2 Win / Lose Condition
| Condition | Trigger |
|---|---|
| **WIN** | ฆ่า Main Boss สำเร็จ |
| **LOSE** | ผู้เล่นทุกคนตายพร้อมกัน |

### 1.3 Core Loop
```
เริ่มเกม → ฆ่า enemy → เก็บ EXP Orb → Level Up → เลือก Upgrade Card (weapon / stat)
  → ทำ Zone Objective → Orb ให้การ์ดรางวัล หรือ Augment (ขึ้นกับแบบของโซน)
  → Normal Lv5 + เงื่อนไขครบ → Super → Super สองตัวครบสูตร → Fusion
  → ฆ่า Mini Boss → ... → Main Boss → WIN
หลังจบรัน (แพ้หรือชนะ) → ได้ทอง → ซื้อ Talent ถาวร / ปลดล็อกตัวละคร (ข้อ 14)
```

---

## 2. Heroes

มี 3 ตัว (`Assets/ScriptableObjects/Characters/`) แต่ละ Hero มี:
- **Starting Weapon** — อาวุธประจำตัว (exclusive · มี Super ของตัวเอง) ยิงอัตโนมัติ
- **Passive** — ทำงานอัตโนมัติตามเงื่อนไข ไม่กินช่องอาวุธ
- **ช่อง Q** — active skill · **คลิกซ้าย** (gamepad: West)
- **ช่อง E** — ultimate · **คลิกขวา** (gamepad: North)

> ปุ่มทั้งหมดอยู่ใน `AbilityInputActions.inputactions` ตัวเดียว ชื่อช่องยังเป็น Q/E/R ในฐานะ "ตัวตนของช่อง" แต่ปุ่มจริงย้ายไปเมาส์แล้ว (2026-09-04) HUD แสดงปุ่มจริงผ่าน `HUDKeyLabel`
> มีช่อง **R** (ปุ่ม R / gamepad East) ในระบบ input แต่ยังไม่มี ability ตัวไหนใช้

### 2.1 Riven
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | BunnyHop → Super: **RabbitHop** | dash ตัดผ่านศัตรูพร้อมฟัน · ได้ Move Speed ถาวรตามเลเวลอาวุธ 5/10/15/20/25% · **Runic Blade: ยิ่งใกล้ยิ่งแรง** · Super ระเบิดซ้ำสองครั้ง (หน่วง 0.4s) ที่จุดเดิม |
| **Passive** | Charge System | เดินสะสม Charge → เต็ม 100 = weapon ยิงพิเศษ (dash ไม่นับระยะ ป้องกันลูปชาร์จตัวเอง) |
| **Q — Valor** | Dash + AoE | dash หาศัตรูใกล้สุด + AoE damage ตอนลง + **stun 2s** · ระยะ dash มาจาก `AbilityData.range` |
| **E — Blade of the Exile** | Exile Mode | +50% move speed, charge rate x2, จำกัดเวลา (Wind Slash ถูกถอดออกแล้ว เพราะซ้อนกับ projectile burst) |

### 2.2 Gunner
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | Gunner Weapon (ปืนพก) → Super: **Magnum** | ยิงกระสุนเดี่ยว · Magnum ยิงกระสุนใหญ่ทะลุศัตรูทั้งหมด |
| **Passive** | Kill Streak | ทุก 30 kills → +50% move speed, +50 Ability Haste เป็นเวลา 10s |
| **Q — Rocket Mode** | Mode Switch | starting weapon ยิง Sticky Rocket แทน projectile ปกติ จำกัดเวลา |
| **E — Giant Rocket** | Big AoE | ยิง Giant Rocket ระเบิดพื้นที่กว้าง |

### 2.3 Hunter
| Slot | Name | Description |
|---|---|---|
| **Starting Weapon** | Laser → Super: **Railgun** | ลำแสงแนวตรง · Railgun ทะลวงทั้งแถว |
| **Passive** | Rapid Fire | ทุก 50 hits → force-fire ทุก weapon ที่ equipped ทันที (scale กับ Ability Haste) |
| **Q — Homing Missiles** | Lock-on AoE | ยิง homing missile 5 ลูกหา enemy ใกล้สุด (เป้าไม่ซ้ำ) แต่ละลูกระเบิด AoE |
| **E — Funnel Storm** | Ultimate | starting weapon ยิงเร็วขึ้น + spawn 5 Funnel โคจรรอบตัว ยิง laser หา enemy |

---

## 3. Weapon System

ถืออาวุธได้พร้อมกัน **5 ช่อง** (`PlayerWeaponManager.MaxWeaponSlots`) — passive weapon ของตัวละครและ ability ไม่นับ
Fusion กิน Super สองช่องคืนมาหนึ่ง จึงคืนช่องว่างให้หนึ่งช่อง

### 3.1 Weapon Tiers

| Tier | Max Level | ได้จาก | หมายเหตุ |
|---|---|---|---|
| **Normal** | Lv 1-5 | Level Up card | อาวุธพื้นฐาน ทุก hero สุ่มได้ (ยกเว้น exclusive) |
| **Super** | Lv 1 | Zone Objective Orb | Normal Lv5 + เงื่อนไข → upgrade เป็น Super (`WeaponData.superVersion`) |
| **Fusion** | Lv 1 | Auto (มี Super A + Super B) | รวม Super 2 ตัวตาม recipe |

### 3.2 Super Upgrade Conditions
Normal weapon ต้องถึง Lv5 + เงื่อนไขเพิ่มเติม (ทุกข้อต้องครบ):
- `StatAtLevel` — ต้องมี stat X ถึง level Y
- `WeaponAtLevel` — ต้องมี weapon X ถึง level Y
- `PlayerLevel` — shared level ถึง X

ความคืบหน้าของเงื่อนไขแสดงผ่าน **Evolution Synergy** บนการ์ดและแถบ build (ข้อ 6.2) — เป็นช่องทางเดียวที่เกมบอกผู้เล่นเรื่องนี้ ([ADR-009](docs/adr-009-evolution-progress-single-source.md))

### 3.3 Weapon List

#### Hero Weapons (exclusive · Lv 1-5)
| Weapon | Hero | Super |
|---|---|---|
| **BunnyHop** | Riven | RabbitHop |
| **Gunner Weapon** (ปืนพก) | Gunner | Magnum |
| **Laser** | Hunter | Railgun |

#### Normal Weapons (Normal Tier · Lv 1-5)
| Weapon | Type | Mechanic |
|---|---|---|
| **Shotgun** | Projectile | ยิงกระสุนลูกปรายกระจายมุมกว้าง (Spread) เล็ง Auto หรือ Mouse Aim |
| **Boomerang** | Projectile | ขว้างบูมเมอแรงพุ่งออกไปและลอยย้อนกลับมาหาตัวละคร |
| **Vortex** | Projectile | สร้างพายุดูดศัตรูเข้าจุดศูนย์กลางพร้อมยิงกระสุนปะทะ · ยิงสลับสายไฟ/น้ำแข็ง |
| **Dual Slash** | Melee / AoE | ฟันดาบคู่ซ้าย-ขวา สร้างความเสียหายกวาดหน้า |
| **Lance** | Melee / AoE | แทงหนวดเดี่ยวพุ่งทะลวงไปข้างหน้า (Narrow Line AoE) ปลดล็อก Knockback ที่เลเวล 5 |
| **Lightning Chain** | Beam | ยิงกระแสไฟฟ้าช็อตชิ่งข้ามตัวศัตรูในระยะใกล้เคียงต่อเนื่อง |
| **Orbiter** | Orbital | ลูกบอลพลังงานโคจรรอบตัว (วงคู่) สร้างความเสียหายเมื่อศัตรูเดินชน |
| **Radiant Aura** | Orbital | วงออร่าแผ่รัศมีสร้างดาเมจรอบตัวผู้เล่นอย่างต่อเนื่อง |
| **Grenade** | Explosive | โยนระเบิดไปจุดเป้าหมาย สร้างแรงระเบิด AoE วงกว้าง |
| **Molotov** | Explosive | โยนระเบิดขวดสร้างแอ่งไฟดาเมจต่อเนื่องบนพื้นผิว |
| **BigAoE** | AoE | อัญเชิญเขตระเบิดเวทมนตร์โจมตีเป็นวงกว้างรอบตัว |
| **BigCannon** | Projectile | ยิงลูกปืนใหญ่ทะลวงแนวตรง ระเบิด AoE เมื่อปะทะหรือหมดระยะ |
| **Fence** | Projectile | ปักเสาไฟฟ้าเชื่อมแนวเลเซอร์ ช็อตศัตรูที่เดินผ่านระหว่างเสา |
| **MagicMissile** | Projectile | ยิงกระสุนเวทมนตร์ติดตามเป้าหมายอัตโนมัติ |
| **Orbital Strike** | AoE | เรียกลำแสงเลเซอร์จากวงโคจรลงมายังพื้น |
| **Spike** | Projectile | ยิงหนามแหลมเจาะทะลวง สะท้อนเด้งกำแพง/สิ่งกีดขวาง |
| **Support Arena** | Utility / AoE | ปักเสาสร้างอาณาเขตสนับสนุน ให้ move speed, **Shield** และฟื้น HP แก่ผู้เล่น ([ADR-008](docs/adr-008-shield-system-support-arena.md)) |

#### Super Weapons (Super Tier · Lv 1)
*แทนที่อาวุธ Lv5 เมื่อผ่านเงื่อนไข · ได้จาก Zone Objective*
| Super Weapon | Base Weapon | Special Mechanic |
|---|---|---|
| **RabbitHop** | BunnyHop (Riven) | dash ระเบิดสองครั้งหน่วง 0.4s ที่จุดเดิม |
| **Magnum** | Gunner Weapon | กระสุนขนาดใหญ่เจาะทะลวงศัตรูทั้งหมด |
| **Railgun** | Laser (Hunter) | ลำแสงเส้นตรงทะลวงทั้งแถว |
| **Blunderbuss** | Shotgun | ลูกซองยักษ์ยิงกระจายหนาแน่นและผลักศัตรูถอยหลังอย่างรุนแรง |
| **TriRang** | Boomerang | ขว้างบูมเมอแรงกระจาย 3 ทิศพร้อมกัน |
| **Spiral Galaxy** | Vortex | พายุดูดที่ยิงกระสุนกระจายรอบตัวเป็นก้นหอย |
| **Blade Storm** | Dual Slash | หมุนตัวฟันดาบพายุหมุน 360 องศา |
| **Tendril Storm** | Lance | แทงหนวดหลัก + งอกหนวดเสริมอีก N เส้น (default 3) ทิศสุ่มจากปลายหนวดหลัก |
| **Stormcaller** | Lightning Chain | เรียกสายฟ้าผ่ากระจายรอบตัว ช็อตชิ่งเป้าหมายหนาแน่น |
| **Star Ring** | Orbiter | วงแหวนโคจรด้วยความเร็วและรัศมีที่กว้างขึ้น |
| **Death Field** | Radiant Aura | สนามพลังความมืดรอบตัว ศัตรูที่ตายในสนามระเบิดพลังงานออกมา |
| **SplitterBomb** | Grenade | ระเบิดใหญ่แตกเป็น Cluster bomb กระจายรอบบริเวณ |
| **NapalmBomb** | Molotov | ระเบิดนาปาล์มพ่นไฟเผาพื้นวงกว้างนานและรุนแรง |
| **SuperBigAoE** | BigAoE | เขตระเบิดขนาดยักษ์ ดาเมจต่อเนื่องรุนแรงมาก |
| **SuperBigCannon** | BigCannon | กระสุนแนวตรงกว้างขึ้น ระเบิดหนักขึ้น |
| **SuperFence** | Fence | เชื่อมแนวเลเซอร์ได้หลายทิศ ดาเมจแรงขึ้น พร้อม Stun/Slow |
| **EvoMagicMissile** | MagicMissile | มิสไซล์ติดตามจำนวนมากขึ้น ความถี่สูง อัตราคริติคอลสูงมาก |
| **SuperOrbitalStrike** | Orbital Strike | ลำแสงจากวงโคจรใหญ่และหนาแน่นขึ้น |
| **SplitSpike** | Spike | กระทบสิ่งกีดขวางแล้วสะท้อนและแตกเป็น 2 ลูก |
| **SuperSupportArena** | Support Arena | โดมบัฟใหญ่ขึ้น ครอบผู้เล่นทุกคน |

> **ปรับตัวเลข 2026-09-24** (เจอจากตาราง DPS ใน Balance Tool):
> Fence Lv2 เสา ×1 → ×2 (เลเวลเคยถอยหลัง) · MagicMissile Lv1 กระสุน 5 → 2 และรัศมี 2 → 1.5 (ค่าที่ก๊อปมาจาก Super) ·
> SuperFence damage 50 → 60 และเสา ×2 → ×3 (เดิมได้เสาน้อยกว่า Fence Lv5 และ DPS ต่ำกว่า) ·
> ล้าง `WD_Superbig aoe.superVersion` ที่ชี้ไป Spiral Galaxy (Super ไม่ควรมี superVersion)

#### Fusion Weapons (Fusion Tier · Lv 1)
*รวม Super 2 ชนิดเมื่อสวมใส่ครบสูตร (ประมวลผลอัตโนมัติ)*
*   **Cluster Bomb** (Blunderbuss + SplitterBomb): ยิงลูกปรายพร้อมโยน Grenade วงกว้าง ศัตรูที่ตาย **ด้วยอาวุธนี้** ระเบิด AoE ที่จุดตายและกระจายลูกระเบิดย่อย
*   **Cyclone Blade** (Blade Storm + NapalmBomb): สลับโหมดทุก cooldown ระหว่างหมุนดาบ 360 องศา กับคลื่นดับเบิ้ลสแลชพุ่งไปข้างหน้าสองเส้น
*   **Orbital Cannon** (Magnum + Star Ring): วัตถุโคจรสร้างดาเมจรอบตัว และแต่ละลูกยิงกระสุนแสงใส่ศัตรูใกล้สุดแยกกันอิสระ
*   **Thunder Rail** (Stormcaller + Railgun): ลำเลเซอร์ทะลวงแนวตรง ทุกเป้าที่โดนเกิด Chain Lightning พร้อมทิ้งแอ่งไฟฟ้า (Mini Lightning Zone)

### 3.4 Fusion Recipes

> ⚠️ **รอออกแบบใหม่ (ตัดสิน 2026-09-24)** — สูตรและอาวุธ Fusion ชุดนี้จะถูกออกแบบใหม่ทั้งชุด · ปัญหาที่เจอไว้แล้ว:
> - **Cyclone Blade fuse ไม่ได้** — `FR_CycloneBlade.superWeaponB` ชี้ไปอาวุธที่ถูกลบไปตั้งแต่ 2026-07-10 (น่าจะเป็น Chainsaw) ไม่ใช่ NapalmBomb ตามตารางข้างล่าง
> - DPS ดิบของ Fusion ต่ำกว่าวัตถุดิบตัวที่แรงกว่า ยกเว้น Thunder Rail (Cluster Bomb 80 < Blunderbuss 579 · Orbital Cannon 400 < Star Ring 3,600)
>
> ตารางข้างล่างคือสูตรที่ **ตั้งใจไว้** ไม่ใช่สิ่งที่ทำงานจริงทั้งหมด
(`Assets/ScriptableObjects/FusionRecipes/`)
| Super Weapon A | Super Weapon B | Fusion Result |
|---|---|---|
| **Stormcaller** (Super Lightning Chain) | **Railgun** (Super Laser) | **Thunder Rail** |
| **Blunderbuss** (Super Shotgun) | **SplitterBomb** (Super Grenade) | **Cluster Bomb** |
| **Blade Storm** (Super Dual Slash) | **NapalmBomb** (Super Molotov) | **Cyclone Blade** |
| **Magnum** (Super Gunner Weapon) | **Star Ring** (Super Orbiter) | **Orbital Cannon** |

> Railgun กับ Magnum เป็นของ exclusive ของ Hunter / Gunner — Thunder Rail ทำได้เฉพาะ Hunter, Orbital Cannon เฉพาะ Gunner

### 3.5 Weapon Stats Per Level
แต่ละ level (`WeaponLevelData`) กำหนด:
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

### 4.1 VFX Reference (`VFXAsset`) & VFXDatabase
เดิม VFX ถูกเรียกด้วยสตริงคีย์ที่พิมพ์เองใน asset ซึ่งพังเงียบเมื่อสะกดผิด ตอนนี้เปลี่ยนเป็น **`VFXAsset`** — ScriptableObject ที่ลากใส่ช่องได้ ([ADR-006](docs/adr-006-vfx-asset-reference.md)) เช่น `SpawnAoEActionBase.detonateVfx`, `BossPhase.phaseVfx`, `TriggerAugment.vfxOnTrigger`
**VFXDatabase** ยังเป็นที่เก็บ prefab, ขนาด และ `poolSize` ต่อรายการ และสตริงคีย์ยังใช้เป็น fallback ภายใน

รายการหลักที่ใช้ทั่วเกม:
*   **HitEffect / CritHitEffect**: ยิงอัตโนมัติจาก `Enemy.NotifyHitClientRpc` ใน `EnemyTakeDamage` — **อาวุธห้ามยิงซ้ำเอง**
*   **GrenadeExplosion**: Grenade, Rocket, Mine
*   **OrbiterHit**: Orbiter, RadiantAura, DeathField
*   **OrbPickup**: เก็บไอเทมเควสและ Objective Orb
*   **EnemyDeath**: ศัตรูตาย
*   **SlashHit / WhipSlash**: อาวุธฟัน
*   **VortexSpawn / DashTrail / MeteorAoE / Stormcaller_AOE / Beam_Railgun**
*   **PhaseShockwave**: ตอนบอสเปลี่ยนเฟส

### 4.2 Networked Object Pooling (NetworkedVFXPool)
*   **Pre-allocation:** ทุก client สร้างอินสแตนซ์ตาม `poolSize` ตั้งแต่โหลดฉาก และคืนเข้าพูลเมื่อหมดอายุ — ไม่มี Instantiate/Destroy ระหว่างเล่น
*   **Recursion Guard:** depth limit สูงสุด 8 ชั้น กัน prefab ที่เรียกตัวเองวนลูป (บั๊ก DeathField เดิม)
*   **Pool report:** DevTools (F1) → *Log VFX Report* บอกว่าพูลไหนเล็กเกินและควรตั้งเท่าไร (วัดตอน co-op จะได้ค่าจริงกว่า solo)
*   **Floating damage numbers** รวมอยู่ในพูลเดียวกัน · ปรับสี/ขนาด/คริตได้จาก Inspector ของ `NetworkedVFXPool` · แสดงบนทุก client
*   **API:** `VFXFactory.Play(key, position, scale)` · `VFXFactory.PlayBeam(beamKey, hitVfxKey, from, to)`

### 4.3 Beam VFX Pool
อาวุธลำแสงวาดผ่าน **LineRenderer** จากพูลแยก ระบุด้วย `beamKey` · ถ้าหาไม่เจอจะ fallback เป็น LineRenderer ชั่วคราวสีฟ้า
beam prefab ต้อง **ไม่มี** NetworkObject/NetworkTransform (เป็นของ local ล้วน)

### 4.4 Dynamic VFX Scaling
*   แต่ละรายการมี **`designedRadius`** (รัศมีดั้งเดิมของพาร์ติเคิล)
*   `ComputeVfxScale = actualRange / designedRadius` ปรับขนาดให้ตรงกับขอบเขตดาเมจจริง
*   `PlayByName3D` สำหรับ non-uniform scale (Line / Cross / Cone)

---

## 5. Stat System

### 5.1 Player Stats (สูงสุด 5 ช่อง, Lv1-5 ต่อ stat)

ทุก stat มีไอคอนชุดเดียวกัน (`StatIcons.asset`) ใช้ร่วมกันทั้งการ์ดอัปเกรด แผงสเตตัส และร้าน Talent

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

#### Full Build Bonuses (ออกเมื่อทุกอย่างเต็มแล้วเท่านั้น)
| Stat | Effect |
|---|---|
| **Gain Gold** | +25 gold |
| **Heal on Full Build** | heal 25% HP |

### 5.2 Stat Card Pool
- Weighted random: Common ~100, Uncommon ~70, Rare ~40, Epic ~15

### 5.3 Shield
HP ชั้นนอกที่รับดาเมจก่อน HP จริง (`ShieldStack`) · มาจาก Support Arena และ augment *Second Wind* ([ADR-008](docs/adr-008-shield-system-support-arena.md))

---

## 6. Progression & Upgrade System

### 6.1 Shared Experience
- **EXP เป็นของกลาง** — ทุก player share level เดียวกัน
- Enemy drop EXP Orb → player เก็บ → บวก shared EXP pool
- Max Level: 30
- EXP to next level: `baseExpToLevel * expGrowthRate^level` (base=100, growth=1.25x)
- EXP Orb ดูดอัตโนมัติตาม Pickup Radius stat · มี Magnet Orb (ดูดทั้งจอ) และ Healing Orb

### 6.2 Level Up Phase
```
Level Up → pause → ทุก player เห็น Upgrade Cards (default 3 ใบ · ปรับได้ที่ UpgradeManager.cardsPerLevel)
  → Card = Weapon (new/level up) หรือ Stat (new/level up)
  → เลือกครบทุกคน (หรือหมดเวลา 30s) → resume
```

- Card pool: Weighted random จาก allWeapons + allStats
- Character-exclusive weapons: ออกเฉพาะ hero ที่กำหนด
- หมดเวลา → auto-pick สุ่ม · **เล่นคนเดียวไม่มีการนับถอยหลัง**
- **Augment ไม่ออกในการ์ดเลเวลอัป** — ได้จาก Augment Orb ทางเดียว (ข้อ 6.4)
- การ์ดเป็น prefab แยกไฟล์ (ไม่ล็อกสามช่อง) · สีการ์ดตามชนิด (weapon / stat / augment)
- **Evolution Synergy (แทนระบบแนะนำการ์ดเดิม):** บนการ์ดบอกว่าใบนี้เชื่อมกับ Super/Fusion ไหน และยังขาดอะไร เช่น "Laser Lv5 · ขาด Armor อีก 2"
  > **ระบบ `⭐ Recommended` ถูกถอดทิ้งแล้ว (2026-09-14, [ADR-009](docs/adr-009-evolution-progress-single-source.md))** เพราะชักจูงผู้เล่นเกินไป — กติกาจากนี้: เกม **บอกข้อเท็จจริง** ได้ ("ขาดอีก 2") แต่ **ห้ามตัดสินแทน** ("ควรเอาใบนี้")

### 6.3 Zone Objective
- เกิดตาม **ตารางเวลาของแมพ** (ข้อ 9) ที่จุดวางในฉาก (`ZoneObjectiveLocation[]`) ห่างผู้เล่น ≥ 10m
- 2 เฟส: **Phase 1** ยืนในโซนจนเปิดใช้งาน → **Phase 2** เควสต์สุ่ม:
  - **FetchAndDeliver** — เก็บไอเทมที่กระจายรอบแมพ (ห่างโซน ≥ 8m) มาส่ง · ผู้เล่นถือผ่าน `playermove.carriedQuestItems`
  - **Survive** — เอาตัวรอดในโซนจนหมดเวลา
- เสร็จ → **Objective Orb** · ทุกคนได้ +80 EXP และ heal 20
- **โซนมีหลายแบบ (Zone Variant)** — กติกาเหมือนกัน ต่างกันที่ orb ที่ให้:
  - `normal` → การ์ดรางวัล 1 ใบ (upgrade weapon/stat · หรือ Super เมื่อเงื่อนไขครบ)
  - `augment` → เลือก Augment (ข้อ 6.4)
- **Objective Tracker HUD** แสดงหลายโซนพร้อมกัน พร้อมเวลาที่เหลือ
- **Objective Pointer:** ไม่มีมินิแมป (ผู้เล่นต้องจ้องตัวละครตลอด) ใช้ลูกศรรอบตัว/ขอบจอพร้อมระยะเป็นเมตรแทน (แบบ Rabbit and Steel)

### 6.4 Augment
ของรางวัลพิเศษที่ติดตัวทั้งรัน ได้จาก **Augment Orb** เท่านั้น เลือก 1 จาก 3 ใบ (`UpgradeManager.augmentCardCount`) · กำหนดช่วงเวลาที่ออกได้ต่อใบ (`availableFromMinutes` / `availableUntilMinutes`) และจำกัดตัวละครได้

มี 3 ชนิด: **StatAugment** (โบนัสถาวร มักมีข้อเสีย) · **TriggerAugment** (ทำงานตามเงื่อนไข) · **WeaponGrantAugment** (มอบอาวุธ)

| Augment | ชนิด | ผล |
|---|---|---|
| **Glass Cannon** | Stat | Damage +45% · Max HP −60 |
| **Juggernaut** | Stat | Max HP +150 · Armor +10 · Move Speed −5% |
| **Overdrive** | Stat | Ability Haste +30 · Area +15% |
| **Sharpshooter** | Stat | Crit +20% · Damage +10% |
| **Swarm Lord** | Stat | Projectile +2 · Damage −10% |
| **Bloodthirst** | Trigger | ทีมฆ่าครบทุก 20 ตัว → heal 5% |
| **Adrenaline** | Trigger | โดนตี → Move Speed +35% นาน 2s (cooldown 5s) |
| **Momentum** | Trigger | ทุกครั้งที่ level up → Damage +60% นาน 12s |
| **Second Wind** | Trigger | ทุก 15s → Shield +60 |

### 6.5 Super Upgrade
- Normal weapon Lv5 + SuperCondition ครบ → Objective Orb เสนอ Super
- Super มี 1 level, stat แรงกว่า Normal Lv5

### 6.6 Fusion
- มี Super A + Super B ตาม recipe → auto-fuse เป็น Fusion weapon
- Fusion มี 1 level, stat สูงสุด

---

## 7. Enemy System

### 7.1 Enemy Types

| Type | Behavior | Script |
|---|---|---|
| **Normal** | เดินเข้าหา player ตาม flow field, contact damage | `Enemy.cs` |
| **Charge** | เดิน + ชาร์จพุ่งเป็นระยะ (x4 speed, 0.4s) | `Enemy.cs` + `EnemyCharge.cs` |
| **Ranged** | ยืนห่าง + ยิง bullet-hell pattern (multi-shot spread) | `Enemy.cs` + `EnemyRanged.cs` |

### 7.2 Elite Enemies — แนวคิด (ถอดออกจากเกมแล้ว)

> **สถานะ: ถอดออก 2026-09-24 · ไม่ได้ลบ** — `EnemySpawner` ไม่เรียกใช้แล้ว · โค้ด (`Script/Elite/`, `EliteModifierDef`) และ asset (`ScriptableObjects/Elite/`) ยังเก็บไว้เป็นจุดตั้งต้น
> เหตุผล: ไม่เคยเกิดจริง (`eliteSpawnRate` ในซีน = 0) และทางเดิมเพิ่ม `EliteController` (NetworkBehaviour) **หลัง** Spawn บน server อย่างเดียว ซึ่ง NGO ไม่รองรับ — client จะไม่มี component นั้น
> **ถ้าจะทำใหม่:** ใส่ `EliteController` ไว้ใน prefab ศัตรูตั้งแต่แรก แล้วตั้ง modifier ก่อน `Spawn()` · เป็นตัวเลือกที่ดีสำหรับทำให้ระดับ Hard ต่างจาก Normal (ข้อ 15)

แนวคิด: ศัตรูปกติที่สุ่มได้ modifier (`EliteModifierDef`) · ตัวใหญ่ขึ้น (x1.35) มีเส้นขอบสีและมงกุฎ · HP x1.5 · EXP x2 (ปรับต่อ modifier)
| Modifier | ผล | สถานะโค้ดเดิม |
|---|---|---|
| **Shield** | มี HP ชั้นพิเศษก่อนโดน HP จริง | มีพฤติกรรม |
| **Rage** | HP ≤ 30% → เร็วขึ้น x1.8 | มีพฤติกรรม |
| **Split** | ตายแล้วแตกเป็นตัวเล็ก | มีแค่ asset — `BehaviorType` ไม่มีค่านี้ |
| **Exploder** | ตายแล้วระเบิด AoE | มีแค่ asset — `BehaviorType` ไม่มีค่านี้ |

### 7.3 Enemy Stats & Targeting
- `speed` · `contactDamage` · `maxHealth` (replicate ไปทุก client เพื่อให้หลอด HP ถูก) · `expReward`
- เดินตาม **Flow Field Pathfinder** (ข้อ 7.5)

### 7.4 Wave Scaling
- **เลข wave คำนวณจากนาฬิกาเกม** (`GameTimeline`) ไม่มีตัวนับแยก · wave ละ 60s (`waveDuration`) · เริ่มหลังดีเลย์ 5s
- ค่าสเกลตั้งได้สองชั้น — ในฉาก (`WaveManager`) หรือ **แมพ override** (`MapData.TierContent.enemyScaling`):

| ค่า | ค่าในฉาก (default) | Arena01 (Normal) |
|---|---|---|
| HP ต่อ wave | +20% | +10% |
| Speed ต่อ wave | +5% (เพดาน x2) | +5% (เพดาน x2) |
| EXP ต่อ wave | +15% | +10% |
| Spawn rate เร็วขึ้นต่อ wave | 10% | 15% |

- **องค์ประกอบศัตรู** เปลี่ยนตามช่วงเวลา: `WavePhase[]` ระบุ "ตั้งแต่นาที X ใช้ WaveConfig ใบนี้" (แทนแบบเก่า "ทุก 3 wave") · ว่าง = ใช้ `waveConfigs` + `wavesPerConfig`
- **หยุด spawn เมื่อรันจบ** (บอสใหญ่ออกหรือเกมจบ)

### 7.5 Flow Field Pathfinding (Server-only)
รองรับศัตรูหลักพันตัวในจอเดียว (แบบ LoL Swarm):
*   **Grid Partitioning:** 100×100 ช่อง ช่องละ 2m (200m×200m)
*   **Static Obstacle Baking:** bake กำแพง (Layer "Wall") ครั้งเดียวตอนเริ่ม · **bake บน server เท่านั้น**
*   **Multi-Source Dijkstra (BFS):** ทุก 0.5s จากตำแหน่งผู้เล่นทุกคนที่ยังมีชีวิต → ศัตรูแยกไปรุมคนที่ใกล้สุด (Voronoi-like)
*   **Crowd Density Map:** ช่องที่ศัตรูหนาแน่นได้ cost เพิ่ม → ตัวหลังเบี่ยงออกโอบล้อม (Flank)
*   **O(1) Sample Lookup:** ศัตรูอ่านทิศจากช่องที่ยืนอยู่ทันที

---

## 8. Boss System

บอสทุกตัว (ใหญ่และมินิ) ใช้ component เดียวคือ **`BossController`** ขับด้วยข้อมูลใน **`BossEncounterConfig`** — ไม่มี subclass ต่อบอสแล้ว (`MainBoss` / `MiniBossAI` ถูกลบ 2026-07)
ที่มาของการออกแบบ: [ADR-003](docs/adr-003-ffxiv-rns-mechanic-gaps.md) (ปิดช่องว่างกับกลไกแบบ FFXIV / Rabbit and Steel) · คู่มือ: [boss_designer.md](docs/boss_designer.md)

### 8.1 โครงของ Encounter

```
BossEncounterConfig
 ├─ arena        → ArenaDefinition (จุดศูนย์กลาง + จุด anchor ในสนาม)
 ├─ rolls[]      → RollDefinition (การสุ่มที่ล็อกต่อไฟต์)
 ├─ firstAttackDelay (จ่ายครั้งเดียวตอนเกิด)
 └─ phases[]     → BossPhase
      ├─ transitionHealthPct  (HP ต่ำกว่านี้ = ไปเฟสถัดไป)
      ├─ invincibilityDuration (อมตะตอนเปลี่ยนเฟส · ผ่าน Enemy.serverInvincible)
      ├─ attackInterval
      ├─ announcement + สี · phaseVfx · camera shake
      ├─ actions[]        → BossAction (วนตามลำดับ)
      └─ enrageTime + enrageActions[]
```

- ตอนเปลี่ยนเฟส ท่าเก่าที่ยังไม่ลงมือถูกยกเลิก (ใช้เลขรอบ) และกลไกที่ค้างอยู่ถูกล้าง (`CleanupMechanics`)
- **สัญญาของ action:** `ExecuteCoroutine` คืนค่าเมื่อ **กลไกจบจริง** ไม่ใช่แค่ยิงออกไป — timeline ที่วาดใน Boss Designer จึงตรงกับของจริง

### 8.2 Main Boss
- เกิดตอนจบเวลาของแมพ (Arena01 = นาทีที่ 15) → **wave หยุด**
- เนื้อหาปัจจุบัน (`BossConfig_01`, arena `Arena_BossPoc`):

| Phase | HP | Interval | ท่า |
|---|---|---|---|
| **1** | 100 → 75% | 3s | Timeline: Line → Donut → Circle |
| **2** | 75 → 40% | 2s | Timeline: Circle · Circle Chasing · Cross · Donut · Donut Chasing · Line · Tether |
| **3** | 40 → 0% | 1s | Timeline ชุดเดียวกับเฟส 2 เรียงใหม่ |

- เปลี่ยนเฟส: อมตะ 1.5s · ข้อความประกาศ (เฟส 2 สีส้ม) · `PhaseShockwave` · กล้องสั่น
- ยังไม่ตั้ง enrage (`enrageTime = -1`)
- **Enrage (แก้ 2026-09-24):** นับจากตอนเข้าเฟส (หลังอมตะจบ) · ถึง `enrageTime` แล้วตัดท่าปกติทันทีเริ่มท่า enrage ตัวแรก · วงที่เตือนไปแล้วยังระเบิดตามเดิม · เดิมรอให้ timeline รอบที่เล่นอยู่จบก่อน จึงช้ากว่าที่ตั้งได้เกือบทั้งรอบ

### 8.3 Mini Boss
- เกิดตามตารางของแมพ (Arena01: นาที 2, 4, 9, 11, 14) · `BossManager` สุ่ม prefab จาก `miniBossPrefabs[]`
- ใช้ `BossController` เหมือนบอสใหญ่ · สเกลจาก wave ปัจจุบัน: HP x5 · Speed x1.2 · EXP x4
- ⚠️ `MiniBossConfig "Purple 1"` มี Donut ที่ใช้ `ArenaAnchor` แต่ไม่ได้ผูก arena → วงไปโผล่ที่ตัวบอสแทน (รอตัดสินใจ: ผูก arena หรือเปลี่ยนเป็น `BossPosition`)

### 8.4 Boss Actions (ScriptableObject)
ทุกท่าเป็น asset แยก ประกอบกันในเฟสได้อิสระ ท่า AoE ทั้งหมดสืบจาก `SpawnAoEActionBase`

| Action | กลไก |
|---|---|
| **CircleAoEAction** | วงกลม — ที่ผู้เล่น / ที่บอส / ที่ anchor ในสนาม · ไล่ตามผู้เล่นได้ (Chasing) |
| **LineAoEAction** | แนวยาวตรง · กวาดหมุนได้ (`sweepDegreesPerSecond`) |
| **CrossAoEAction** | กากบาท 4 ทิศ |
| **DonutAoEAction** | วงแหวน กลางปลอดภัย · ขยาย/หดได้ (`scaleStart` → `scaleEnd`) |
| **ConeAoEAction** | พัดหันหาเป้า |
| **ColorMatchAoEAction** | โซนสีอันตราย/ปลอดภัยทับกัน ต้องยืนโซนสีที่ตรงเงื่อนไข · วางกลางสนามหรือรอบผู้เล่นแต่ละคน |
| **TetherAction** | โซ่ผูกผู้เล่น 4 โหมด: **Far** (ต้องอยู่ห่าง) · **Close** (ต้องอยู่ใกล้) · **Leash** (ห้ามเกินระยะ) · **Transferable** (ส่งต่อให้เพื่อนได้) — [ADR-002](docs/adr-002-tether-modes.md) |
| **LimitCutAction** | ติดเลขลำดับให้ผู้เล่น แล้วลงดาเมจตามลำดับ (แบบ FFXIV) |
| **KeepMovingAction** | หยุดเดินเกินเวลาที่กำหนด = โดนดาเมจ |
| **RandomAttackAction** | สุ่มเลือกท่าจาก pool |
| **ComboAction** | ท่าหลายท่าติดกันเป็นชุด |
| **BossTimelineAction** | วางท่าบนเส้นเวลา (วินาทีที่แน่นอน) — เนื้อหาหลักของบอสตอนนี้ใช้ตัวนี้ |

ตัวเลือกร่วมของท่า AoE: `castName` + `castTime` (**Cast Bar** เหนือหัวบอส) · `repeatCount` / `repeatInterval` · **knockback** (`KnockbackMode` + ระยะ + ทิศคงที่) · ปรับหน้าตา telegraph ต่อท่า (สีเตือน/อันตราย/ขอบ, pulse, ring) · `detonateVfx`

### 8.5 Rolls — การสุ่มที่ล็อกต่อไฟต์
เพื่อให้ท่าเดิม "เล่นไม่เหมือนเดิม" ในแต่ละไฟต์ แต่ **คงเส้นคงวาภายในไฟต์** (seed ต่อไฟต์ · `FightSeed` ทำซ้ำไฟต์ได้)

| Kind | ผล |
|---|---|
| **Anchor** | เลือกจุดในสนาม (เช่น quadrant) |
| **SnapAngle** | หมุนแพตเทิร์นเป็นมุมคงที่ (เช่น ×4 = 90°) |
| **MirrorX / MirrorZ** | สะท้อนแพตเทิร์น |
| **Victim** | เลือกผู้เล่นเป้าหมาย |
| **Order** | สลับลำดับ |

- ท่าอ้าง roll ด้วยชื่อ (`rollName` · เลือกจาก dropdown ใน Inspector) · หมุน/สะท้อนรอบ `arena.center`
- Container (`ComboAction`, `BossTimelineAction`) roll **ครั้งเดียว** แล้วลูกทุกท่าใช้ค่าเดียวกัน
- ⚠️ `BossConfig_01` นิยาม roll ไว้ 5 ตัว (mirror · spin · quadrant · victim · order) แต่ **ยังไม่มีท่าไหนใช้เลย**

### 8.6 Status Effects
`StatusEffectData` — สถานะที่บอสติดให้ผู้เล่น: stack สูงสุด · ระยะเวลา · decay · ตัวคูณดาเมจที่รับต่อ stack · **`onExpire` → BossAction** (ระเบิดตอนหมดเวลา แบบ Spell-in-Waiting ของ FFXIV) · ตัวอย่างหลัก: **Vulnerability**

### 8.7 Telegraph Zone
- ทุก AoE เป็น `TelegraphZone` (NetworkObject) · **warning** → กะพริบ → ดาเมจ **ครั้งเดียว** ตอนจบ
- Shape: `Circle`, `Line`, `Cross`, `Donut`, `Cone` · การไล่ตาม (Chase) เป็นสวิตช์แยก ไม่ใช่ shape
- วาดด้วย **SDF quad** ([ADR-004](docs/adr-004-telegraph-sdf-quad.md)) · ขอบหน่วยเมตร · pulsating rings · โดนัทเจาะรูจริง · ไม่ทอดเงา
- **สีตามหมวดกลไก** (palette ใน Boss Designer) · ค่าที่ client เห็นส่งใน `TelegraphInit` struct เดียว
- บอสตาย → telegraph และ tether ของบอสถูก despawn ทั้งหมด

### 8.8 Boss HP Bar UI / HUD
*   **Main Boss (บนกลางจอ):** หลอดสีเปลี่ยนตามเฟส · มาร์กเกอร์ตำแหน่งเฟส · Cast Bar
*   **Mini Boss (ด้านข้าง):** รายการไดนามิก (`MiniBossBarEntry`) เพิ่ม/ลบตาม static event `OnAnyBossSpawned` / `OnAnyBossDespawned`
*   **WorldHPBar:** หลอดลอยเหนือหัวบอสทุกตัว
*   Boss HUD แสดงบนทุก client (event ยิงก่อนเช็ค `IsServer`)

---

## 9. Game Timeline

ตารางเวลาเป็น **เนื้อหาของแมพ** (`MapData.TierContent.schedule` · `TimelineSchedule`) — ไม่ใช่ค่าตายตัวในฉากอีกต่อไป ประกอบด้วย:
- `cues[]` — นัดหมาย "ให้ของแบบนี้ เกิดที่นาทีเหล่านี้" (ZoneObjective หรือ MiniBoss + ชื่อ variant)
- `mainBossMinutes` — ความยาวรัน
- `wavePhases[]` — ช่วงของ WaveConfig ตามนาที

ตารางปัจจุบันของ **Arena01 (Normal)**:
```
 0:00  เริ่มเกม · wave 1 (ดีเลย์ 5s)
 1:00  Zone Objective (normal)
 2:00  Mini Boss
 3:00  Zone Objective (augment)
 4:00  Mini Boss
 5:00  Zone Objective (normal)
 8:00  Zone Objective (normal) + Zone Objective (augment)
 9:00  Mini Boss
11:00  Mini Boss
12:00  Zone Objective (augment)
13:00  Zone Objective (normal)
14:00  Mini Boss
15:00  MAIN BOSS → wave หยุด
       Kill Boss → WIN · All dead → LOSE
```
ในรันหนึ่งได้ Zone Objective 7 ครั้ง (augment 3 · normal 4) และ Mini Boss 5 ตัว

> เกมรอผู้เล่นทุกคนโหลดเสร็จก่อนเริ่มนาฬิกา (`GameTimeline.startWaitTimeout` 20s · `GameplayLoadingGate.safetyTimeout` ต้องมากกว่าค่านี้)

---

## 10. Multiplayer Architecture

### 10.1 Network Model
- **Server-authoritative** — Unity Netcode for GameObjects · Host = Server + Client
- Server ถือ: enemy spawn, damage, HP, boss AI, wave, quest progress · base stat ของผู้เล่นคำนวณจาก `CharacterData` บน server (ไม่เชื่อค่าจาก client)
- Client ส่ง input ผ่าน ServerRpc · แสดง VFX (local pool) · UI
- **ข้อยกเว้น: การเคลื่อนที่เป็น owner-authoritative** — owner เขียน velocity เอง, server ย้ายผู้เล่นได้ทาง `ApplyKnockbackClientRpc` เท่านั้น
- ตรวจ input ฝั่ง server (anti-cheat พื้นฐาน) ผ่าน `UnityIdExtensions`

### 10.2 Session & Lobby
- เชื่อมต่อผ่าน **Unity Multiplayer Services (Relay / Sessions)** · เข้าห้องด้วย **รหัสห้อง**
- ปุ่ม Play เดียว: เล่นคนเดียวแบบออฟไลน์ได้
- สถานะล็อบบี้ทั้งหมดอยู่ใน `LobbyState` (NetworkVariable) — client แก้ผ่าน ServerRpc เท่านั้น: ตัวละคร (ส่งเป็น `characterName` สตริง) · แมพ · ความยาก · Ready
- สีผู้เล่นมาจาก `PlayerSlotRegistry.GetSlot()`
- player object spawn **หลังโหลดฉากเกม** (ไม่ใช่ตอนกด Play) เพื่อให้ได้ตัวละครที่เลือกจริง
- ปิดเกม → ออกจากห้องก่อน ไม่ปล่อยค้างจนหมดอายุ

### 10.3 Pause ในโหมดหลายคน
- `Time.timeScale` มีเจ้าของเดียว (ไม่ softlock)
- **Solo:** ESC หยุดเกมทั้งหมด
- **Co-op:** host กด ESC เปิดเมนูได้โดย **โลกไม่หยุด** — ศัตรูเดินต่อ, client เล่นต่อ, host ขยับ/ยิงไม่ได้ระหว่างเปิดเมนู

### 10.4 Key RPCs
| Direction | RPC | Purpose |
|---|---|---|
| Client→Server | `FireProjectileServerRpc` | spawn projectile NetworkObject |
| Client→Server | `FireMeleeServerRpc` | melee AoE damage |
| Client→Server | `FireRaycastServerRpc` | raycast pierce damage |
| Client→Server | `FireLineAoEServerRpc` | line AoE damage |
| Server→Client | `Enemy.NotifyHitClientRpc` | hit/crit VFX + เลขดาเมจ |
| Server→Client | `BroadcastBeamServerRpc` | broadcast beam VFX |
| Server→Client | `BeginUpgradePhaseClientRpc` | start level up pause |
| Server→Owner | `playermove.ApplyKnockbackClientRpc` | ผลักผู้เล่น (ทางเดียวที่ server ย้ายผู้เล่นได้) |

### 10.5 แผนระยะยาว
เป้าหมายปลายทาง **client reconnect** และ **host migration** — ต้องย้าย player state ไปอยู่ฝั่ง server ก่อน ([plan-server-state-and-reconnect.md](docs/plan-server-state-and-reconnect.md))
⚠️ ยังไม่เคยทดสอบเล่นจริงสองเครื่อง

---

## 11. UI / HUD

ภาษาภาพของ UI ทั้งเกมคือ **Persona 3 Reload (P3R)** — ตัวอักษรเอียง แถบเฉียง พื้นเกรเดียนต์ · สร้างด้วย uGUI ผ่าน screen builder (skill `game-ui`) · ควบคุมรวมที่ `Window > Clone Swarm > P3R Screens`

### 11.1 จอทั้งหมด
| กลุ่ม | จอ |
|---|---|
| **Menu** | Title · Lobby · Character · Map Select · Talent Shop · Config · Loading · Join Room |
| **In-game** | HUD · Level Up · Pause · Win/Lose |

- **Carousel** เลือกตัวละคร/แมพ (หมุนวนไม่รู้จบ · ลาก/ลูกกลิ้ง/คลิกใบข้าง)
- **Lobby:** แถวปาร์ตี้พร้อมพอร์เทรต · ชิปสกิล · ยอดทอง · รหัสห้อง · ตัวเลือกความยาก · สถานะ READY
- ปุ่มถอยบอกปลายทางจริง (BACK / BACK TO LOBBY)

### 11.2 In-Game HUD (v2)
- **มุมล่างซ้าย — สถานะผู้เล่น:** พอร์เทรต · HP · EXP/Level
- **Build Bar** (แถบเดียวทั้งเกม ใช้ร่วมกับจอ Level Up): อาวุธ 5 ช่อง + cooldown · สเตตัส 5 ช่อง · passive
- **Ability HUD** — ช่อง Q/E + cooldown/active timer + ปุ่มจริง
- **Passive Bar** — Charge (Riven) / Kill counter (Gunner) / Hit counter (Hunter)
- **Party Row** — แสดงเฉพาะเพื่อน
- **Game Clock** · **Objective Tracker** (หลายโซน + เวลา) · **Objective Pointer** · **Quest Carry**
- **Boss HUD** (ข้อ 8.8) · **Augment HUD** · **Status HUD** (สถานะจากบอส) · **Floating Buff** เหนือหัว · **เลขดาเมจลอย**

### 11.3 Level Up UI
- การ์ดตามจำนวนที่ตั้ง (default 3) · แต่ละใบ: icon, ชื่อ, คำอธิบาย, สเตตัสที่เปลี่ยน, เลเวล
- หัวเรื่องสามชั้น · สีการ์ดตามชนิด · Evolution Synergy lines
- Countdown 30s (ไม่นับเมื่อเล่นคนเดียว) · "X / Y players picked"

### 11.4 Win / Lose Screen
- WIN / LOSE · เวลาเล่น · เลเวลที่ถึง · แถวปาร์ตี้พร้อมพอร์เทรต · ทองที่ได้
- ปุ่มเล่นใหม่ (กันกดซ้ำ · ไม่เกิดใหม่ขณะยังตาย) / กลับล็อบบี้

### 11.5 Localization
- **Unity Localization 2.0** · 2 ภาษา: **ไทย / อังกฤษ**
- String Table: UI (79 key) · Content (192) · Announcements (28) — ข้อความบน ScriptableObject (ชื่ออาวุธ, augment, ประกาศเฟสบอส) ใช้ `LocalizedString`
- ข้อความไทยใน TextMeshPro: แก้การวางวรรณยุกต์ การตัดคำ และชดเชยขนาดเมื่ออยู่ข้างตัวละติน (skill `thai-text`)

---

## 12. Technical Architecture

### 12.1 Key Managers (Singleton)
| Manager | Scope | Responsibility |
|---|---|---|
| `GameTimeline` | Server | นาฬิกาเกม · ยิงนัดหมายตามตารางของแมพ |
| `WaveManager` | Server | Wave scaling, enemy config ตามนาที |
| `EnemySpawner` | Server | Enemy instantiation |
| `BossManager` | Server | Mini/Main boss spawn |
| `ObjectiveManager` | Server | Zone objective spawn + variant |
| `SharedExperienceManager` | Server+Client | Shared EXP/level, upgrade phase |
| `NetworkedVFXPool` | Client | VFX + damage number pool |
| `SoundManager` | Client | SFX pool, music, volume (PlayerPrefs) |
| `GameSessionManager` / `LobbyState` | Server+Client | ห้อง, relay, สถานะล็อบบี้ |
| `SaveManager` / `MetaProgression` | Local | เซฟทอง + talent |

### 12.2 Per-Player Components
| Component | Responsibility |
|---|---|
| `playermove` | Movement (owner-auth), HP, death/respawn, quest items |
| `PlayerWeaponManager` | Weapon slots (5), fire RPCs, projectile spawn |
| `PlayerStatManager` | Stat levels, multiplier getters |
| `PlayerAbilityManager` | Ability slots (Q/E) |
| `PlayerAugmentManager` | Augment ที่ถืออยู่ + trigger |
| `PlayerStatusManager` | Status effect จากบอส |
| `PlayerTalentApplier` | ใส่ talent ถาวรตอนเกิด |
| `UpgradeManager` | สร้างการ์ด, apply upgrade, Evolution Synergy |
| `ChargeManager` | Riven charge system |

### 12.3 Weapon Architecture
```
WeaponBase (abstract)  — auto cooldown loop บน owner
  ├── Normal: ShotgunWeapon, BoomerangWeapon, VortexWeapon, BigAoEWeapon, BigCannonWeapon, FenceWeapon,
  │           MagicMissileWeapon, MolotovWeapon, OrbitalStrikeWeapon, SpikeWeapon, SupportArenaWeapon, OrbiterWeapon, ...
  ├── Super/Fusion: StormcallerWeapon, ThunderRailWeapon, SuperBigAoEWeapon, SuperFenceWeapon,
  │                 SuperOrbitalStrikeWeapon, SplitSpikeWeapon, SplitterBombWeapon, NapalmBombWeapon, ...
  ├── Hero: BunnyHopWeapon, StormBunnyWeapon, Gunner weapon, Laser
  └── Passive (ไม่กินช่อง): GunnerPassiveWeapon, HunterPassiveWeapon

AbilityBase (abstract)  — ไม่มี auto cooldown · input จาก AbilityPressedThisFrame / AbilityHeld
  ├── ValorWeapon (Riven Q) · BladeOfExileWeapon (Riven E)
  ├── GunnerRocketMode (Gunner Q) · GunnerGiantRocket (Gunner E)
  └── HunterMissileAbility (Hunter Q) · HunterUltimate (Hunter E)
```
VFX/SFX อยู่บน **prefab** ของอาวุธ/ability ไม่ใช่บน ScriptableObject · ส่วนบอสเก็บ VFX บนข้อมูลของท่า (เพราะทุกท่าใช้ `TelegraphZone` prefab ตัวเดียว)

### 12.4 Data Architecture (ScriptableObjects)
```
CharacterData       → hero identity, base stats, starting weapon, abilities, unlock
WeaponData          → identity, tier, 5 levels, super conditions, superVersion, exclusive
AbilityData         → Q/E slot, prefab, levels
StatData            → stat identity, values per level (ไอคอนจาก StatIconSet)
WeaponFusionRecipe  → Super A + Super B = Fusion
AugmentData         → Stat / Trigger / WeaponGrant augment
EliteModifierDef    → elite modifier (ถอดออกจากเกม · เก็บไว้ ดูข้อ 7.2)
StatusEffectData    → status ที่บอสติดให้
MapData             → แมพ · ต่อ DifficultyTier: waves, boss configs, schedule, enemy scaling
WaveConfig          → องค์ประกอบศัตรูต่อช่วง
BossEncounterConfig → arena, rolls, phases (MiniBossConfig = alias)
BossAction          → ท่าบอสแต่ละท่า
ArenaDefinition     → จุดศูนย์กลาง + anchor ของสนาม
TalentData / MetaDatabase → talent ถาวร + ข้อมูล meta
VFXAsset / VFXDatabase    → VFX reference + pool config
```

### 12.5 Projectile Scripts
อยู่ใน `Assets/Script/Projectile/` แยกจากสคริปต์อาวุธ:
*   `BouncingSpikeProjectile` — สะท้อนกำแพงพร้อมแตกหนาม
*   `GrenadeProjectile` — แรงระเบิด รัศมี และดีเลย์
*   `MolotovProjectile` — แอ่งไฟต่อเนื่อง
*   `MagicMissileProjectile` — ล็อกเป้าและ homing

---

## 13. Developer Tools

### 13.1 Weapon Test Scene & WeaponTestManager
*   **Quick Equip Panel:** ปุ่มสวมอาวุธ Lv5 / ability ทุกตัวจาก `allWeapons[]` ทันที
*   **Real-time DPS Tracker:** ดาเมจสะสมและ DPS เฉลี่ยรายวินาที
*   **Target Dummies:** respawn / รีเซ็ต HP ได้จากจอ
*   **Stat Multiplier Sliders:** Damage 1x–10x · Haste 0–200 · Crit 0–100%

### 13.2 DevTools (F1)
แผงในเกม: TimeScale · VFX Pool Report · ตั้งทองในโปรไฟล์จาก Inspector (`DevProfileOverride`)

### 13.3 Boss Designer (`Tools > Boss Designer`)
หน้าต่างออกแบบ encounter:
*   **Timeline** ต่อเฟส — ลาก/วางท่าตามวินาที · ตรงกับ runtime จริง
*   **ArenaPreview** — แผนผังสนาม ณ ตำแหน่ง playhead · ปุ่ม 🎲 เปลี่ยน seed ดูผลของ roll
*   สร้าง action ใหม่ในหน้าต่างได้ · palette สี telegraph ตามหมวดกลไก
*   ใช้ `SpawnAoEActionBase.ResolveWave()` ตัวเดียวกับ server — พรีวิวกับของจริงไม่มีทางต่างกัน

### 13.4 การตรวจอัตโนมัติ (ไม่ต้องกด Play)
| เครื่องมือ | ตรวจอะไร |
|---|---|
| `Tools > Clone Swarm > Audit Boss Configs` (`BossConfigAudit`) | rollName ผิด · arena ขาด · ท่าวางตำแหน่งได้จริง · มี entry สำหรับ batchmode |
| `P3RSmokeTest` | จอ UI · การต่อสาย · ตารางเวลาของทุกแมพ · boss config |
| Run probe | เล่นเกมจริงแบบ headless แล้วอ่าน Console |
| Build script | build Windows พร้อมสร้าง Addressables ก่อนเสมอ |
| Netcode auditor | หา NetworkBehaviour ที่ผิดกติกา |

---

## 14. Meta-Progression System

ข้อมูลอยู่ใน `MetaDatabase` + `TalentData` · เซฟในเครื่อง (`SaveManager`)

*   **Gold:** ได้จากการฆ่า/ผ่าน objective/บอส ระหว่างรัน · เข้าบัญชีตอนจบรัน **ทั้งแพ้และชนะ** (`RunRewardTracker`)
*   **Talent Shop:** อัปเกรดถาวร มีผลกับทุกตัวละครตั้งแต่เลเวล 1 · ราคาเพิ่มเท่าตัวทุกเลเวล

| Talent | ต่อเลเวล | Max | ราคา (Lv1 → Lv5) |
|---|---|---|---|
| Attack | Damage +3% | 5 | 100 → 1,600 |
| Health | Max HP +100 | 5 | 100 → 1,600 |
| Duration | +5% | 5 | 100 → 1,600 |
| Armor | +3 | 5 | 120 → 1,920 |
| Area | +4% | 5 | 120 → 1,920 |
| Regen | +0.5 HP/s | 5 | 120 → 1,920 |
| Haste | Ability Haste +5 | 5 | 150 → 2,400 |
| Crit | +5% | 5 | 150 → 2,400 |
| Move Speed | +2% | 5 | 150 → 2,400 |
| Magnet | Pickup +10% | 5 | 80 → 1,280 |
| EXP | +5% | 5 | 200 → 3,200 |
| Gold Finder | ทอง +10% | 5 | 200 → 3,200 |
| Projectile | +1 | 2 | 8,000 → 16,000 |
| **Second Chance** | ฟื้นคืนชีพ 1 ครั้งต่อเกม | 1 | 5,000 |

*   **Unlocks:** ใช้ทองปลดล็อกตัวละคร · ต้องมีอย่างน้อยหนึ่งตัวที่ `unlockedByDefault` (ไม่งั้นเกมเริ่มไม่ได้เพราะทองเริ่มที่ 0)

---

## 15. Stage Difficulty System

Host เลือกความยากในล็อบบี้ มี **5 ระดับ** (`DifficultyTier`): **Easy · Normal · Hard · Savage · Epic**

### 15.1 ความยากคือ "เนื้อหาต่อระดับ" ไม่ใช่ตัวคูณ
แต่ละแมพกำหนดเนื้อหาแยกต่อระดับใน `MapData.tiers[]` (`TierContent`):
- `wavesByPhase` — ศัตรูชุดไหน
- `mainBossConfig` / `miniBossConfig` — บอสใช้ท่าชุดไหน (ว่าง = ใช้ของบน prefab)
- `schedule` — ตารางเวลา (ข้อ 9)
- `enemyScaling` — สเกลศัตรูต่อ wave

แนวคิด: ระดับสูงไม่ใช่แค่ "ศัตรูเลือดหนาขึ้น" แต่ได้ **ท่าบอสและจังหวะที่ต่างกัน** (แบบ Savage ของ FFXIV · ดู [plan-savage-boss-system.md](docs/plan-savage-boss-system.md))

⚠️ **สถานะ:** Arena01 มีเนื้อหาแค่ระดับ **Normal** · ระดับอื่นเลือกได้ในล็อบบี้แต่ยังไม่มีเนื้อหาของตัวเอง

### 15.2 เป้าหมายการออกแบบ (ยังไม่ได้ทำ)
ตัวเลขชุดนี้เป็นเป้า ใช้เป็นแนวตอนสร้าง `TierContent` ของแต่ละระดับ

| ระดับ | HP ศัตรู | ดาเมจศัตรู | ทอง & EXP | จุดเด่น |
|---|:---:|:---:|:---:|---|
| **Easy** | 0.7x | 0.7x | 0.7x | สำหรับผู้เริ่มต้น / ลองอาวุธ |
| **Normal** | 1.0x | 1.0x | 1.0x | มาตรฐาน |
| **Hard** | 1.5x | 1.5x | 2.0x | มี Elite (ต้องทำระบบใหม่ก่อน — ข้อ 7.2) |
| **Savage** | 2.5x | 2.5x | 4.0x | ศัตรูเร็วขึ้น · cast time บอสสั้นลง · telegraph เตือนสั้นลง · ท่าซ้อนกัน (เช่น stack + gaze พร้อมกัน) |
| **Epic** | — | — | — | ยังไม่กำหนด |

- **AI Aggression Scaling:** ระดับสูง ศัตรูเปลี่ยนเป้าเร็วขึ้น โอบล้อมเร็วขึ้น
- **Raid Mechanic Modification:** บอสข้ามคูลดาวน์บางท่า และใช้กลไกซ้อนทับ (Overlapping Telegraphs) บังคับจัดตำแหน่งแม่นยำวินาทีต่อวินาที (แบบ *Rabbit and Steel*)
