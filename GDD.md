# Game Design Document — Swarm Survivors

> **Genre:** Top-down Co-op Bullet Heaven / Horde Survival
> **Platform:** PC (Unity 6, Netcode for GameObjects)
> **Players:** 1-4 Online Co-op
> **Inspiration:** LoL Swarm, Vampire Survivors, rabbit and steel

---

## 1. Game Overview

### 1.1 Concept
ผู้เล่น 1-4 คน เลือก Hero แล้วเอาตัวรอดจาก horde ของศัตรูที่ยากขึ้นเรื่อยๆ เป็นเวลา **15 นาที** โดยสะสม weapon, stat, และ upgrade ระหว่างทาง เป้าหมายคือ **ฆ่า Main Boss** ที่ spawn ตอนนาทีที่ 15

> [!NOTE]
> **สถานะโครงการปัจจุบัน (Project Status):** โครงการในขั้นตอนนี้เป็นการจำลองระบบ **Proof of Concept (PoC) / Mock-up** เพื่อทดสอบระบบเครือข่าย (Multiplayer Synchronization) และระบบการโจมตีเชิงกลไกเป็นหลัก งานศิลป์ (Art Assets) รวมถึงโมเดล 3 มิติของตัวละคร ศัตรู ฉาก และอินเตอร์เฟส (UI) ทั้งหมดที่เห็นเป็นตัวต้นแบบชั่วคราว (Placeholder Art) โดยมีแผนจะ **ออกแบบและจัดทำชิ้นงานศิลป์ใหม่ทั้งหมด** ในเวอร์ชันจำหน่ายจริง ยกเว้นระบบเอฟเฟกต์ (VFX System) ที่พัฒนาขึ้นเป็นพิเศษเสร็จสมบูรณ์แล้ว

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

#### Normal Weapons (Normal Tier - Lv 1-5)
| Weapon | Type | Mechanic |
|---|---|---|
| **Pistol** | Projectile | ยิงกระสุนเดี่ยวตรงหน้า เล็ง Auto (ศัตรูใกล้สุด) หรือ Mouse Aim |
| **Shotgun** | Projectile | ยิงกระสุนลูกปรายกระจายมุมกว้าง (Spread) เล็ง Auto หรือ Mouse Aim |
| **Orbital Cannon (Normal Base)** | Projectile | ยิงอุกกาบาต/ลำแสงสุ่มลงมาจากฟากฟ้า |
| **Boomerang** | Projectile | ขว้างบูมเมอแรงพุ่งออกไปและลอยย้อนกลับมาหาตัวละคร |
| **TriRang** | Projectile | ขว้างบูมเมอแรงกระจายออกไป 3 ทิศทางพร้อมกัน |
| **Vortex** | Projectile | สร้างพายุดูดศัตรูเข้าจุดศูนย์กลางพร้อมยิงกระสุนปะทะ |
| **Spiral Galaxy** | Projectile | พายุดูดศัตรูที่ยิงกระสุนกระจายรอบตัวเป็นก้นหอย |
| **Dual Slash** | Melee / AoE | ฟันดาบคู่ซ้าย-ขวา สร้างความเสียหายกวาดหน้า |
| **Whip** | Melee / AoE | ฟาดแส้หนวดทิศทางเดียว (Line AoE) ด้านหน้าและหลังตัวละคร |
| **Lance** | Melee / AoE | เสือกแทงหนวดเดี่ยวพุ่งทะลวงไปข้างหน้า (Narrow Line AoE) ปลดล็อก Knockback ที่เลเวล 5 |
| **Laser** | Beam | ยิงลำแสงเลเซอร์ยาวแนวตรงค้างชั่วขณะ (Line AoE) |
| **Lightning Chain** | Beam | ยิงกระแสไฟฟ้าช็อตชิ่งข้ามตัวศัตรูในระยะใกล้เคียงต่อเนื่อง |
| **Orbiter** | Orbital | ลูกบอลพลังงานโคจรรอบตัว สร้างความเสียหาย melee เมื่อศัตรูเดินชน |
| **Radiant Aura** | Orbital | วงออร่าแผ่รัศมีสร้างดาเมจรอบตัวผู้เล่นอย่างต่อเนื่อง |
| **Grenade** | Explosive | โยนระเบิดไปจุดเป้าหมาย สร้างแรงระเบิด AoE วงกว้าง |

#### Super Weapons (Super Tier - Lv 1)
*สลัดอาวุธเลเวล 5 ทิ้งเพื่ออัปเกรดเมื่อผ่านเงื่อนไขจาก Zone Objective*
| Super Weapon | Base Weapon | Special Mechanic |
|---|---|---|
| **Tendril Storm** | Lance | แทงหนวดหลักไปข้างหน้า และงอกหนวดเสริมอีก N เส้น (default 3) แทงทิศสุ่มต่อเนื่องจากปลายหนวดหลัก |
| **Whip Plasma** | Whip | ฟาดแส้หนวดหลักพร้อมผลักศัตรู (Knockback) แล้วสร้างสายฟ้ากระดอน (chain beam) ช็อตชิ่งต่อหาศัตรูใกล้สุด 3 ขั้น |
| **Blade Storm** | Dual Slash | หมุนตัวฟันดาบพายุหมุน 360 องศารอบตัว |
| **Death Field** | Radiant Aura | กางสนามพลังความมืดรอบตัวต่อเนื่อง เมื่อศัตรูตายในสนามจะระเบิดพลังงานออกมา |
| **Minefield** | Grenade | วางทุ่นระเบิดสุ่มบนพื้นรอบตัว เมื่อศัตรูเหยียบจะระเบิดเป็นวงกว้าง |
| **Stormcaller** | Lightning Chain | เรียกสายฟ้าผ่ากระจายลงมารอบตัว ช็อตชิ่งเป้าหมายหนาแน่น |
| **Magnum** | Pistol | อัปเกรดปืนพกยิงกระสุนขนาดใหญ่เจาะทะลวงศัตรูทั้งหมด |
| **Star Ring** | Orbiter | วงแหวนดาวเคราะห์โคจรรอบตัวด้วยความเร็วและรัศมีที่กว้างขึ้น |
| **Blunderbuss** | Shotgun | ปืนลูกซองยักษ์ยิงกระจายกระสุนหนาแน่นและผลักศัตรูถอยหลังอย่างรุนแรง |

#### Fusion Weapons (Fusion Tier - Lv 1)
*รวมร่างอาวุธระดับ Super 2 ชนิดเมื่อสวมใส่ครบสูตร (สูตรผสมถูกประมวลผลอัตโนมัติ)*
*   **Plasma Whip** (Railgun + Whip Plasma / Chainsaw): หมุนฟันรอบตัวเป็นวงกว้าง (Melee Spin AoE) พร้อมยิงลำแสง Raycast ออกไป N ทิศทางพร้อมกันตาม projectile count
*   **Cluster Bomb** (Blunderbuss + Minefield): ยิงลูกปรายกระบอกกระจายรอบหน้าพร้อมโยน Grenade วงกว้าง และเมื่อศัตรูตายจะระเบิด AoE ที่จุดตายพร้อมกระจายลูกระเบิดย่อย (Child Grenades) เด้งเกลื่อนพื้นรอบบริเวณ
*   **Cyclone Blade** (Blade Storm + Chainsaw): ทำการสลับโหมดโจมตีสลับกันทุก Cooldown ระหว่างหมุนดาบฟันรอบตัว 360 องศา และการปล่อยคลื่นฟันดับเบิ้ลสแลชพุ่งตรงไปด้านหน้าสองเส้นคู่
*   **Orbital Cannon** (Magnum + Star Ring): เรียกใช้วัตถุโคจรสร้างดาเมจมีเลย์รอบตัว โดยที่ตัวลูกบอลแต่ละลูกจะทำการค้นหาและยิงกระสุนแสงใส่ศัตรูที่ใกล้ที่สุดแยกกันอย่างอิสระ
*   **Thunder Rail** (Stormcaller + Railgun): ยิงลำเลเซอร์ทะลวงแถวยาวแนวตรง โดยทุกเป้าหมายที่โดนจะเกิดสายฟ้าชิ่งต่อเนื่อง (Chain Lightning) พร้อมทิ้งแอ่งกระแสไฟฟ้าแปรปรวน (Mini Lightning Zone) ช็อตดาเมจบนพื้นต่อเนื่อง
*   **Storm Bunny** (BunnyHop Super + Stormcaller): การพุ่งหลบหลีก (Dash) ที่ทำดาเมจกระแทกพื้นกว้าง (Meteor AoE) และฟาดสายฟ้าช็อตชิ่งที่จุดแลนดิ้ง พร้อมมอบบาเรียป้องกันดาเมจแก่ผู้เล่น เมื่ออยู่ในโหมดปลุกพลัง Exile จะยิงกระสุนเลเซอร์ชิ่งสายฟ้ารอบทิศเพิ่มเติม

### 3.4 Fusion Recipes
| Super Weapon A | Super Weapon B | Fusion Result |
|---|---|---|
| **Stormcaller** (Super Lightning Chain) | **Railgun** (Super Laser) | **Thunder Rail** |
| **Blunderbuss** (Super Shotgun) | **Minefield** (Super Grenade) | **Cluster Bomb** |
| **Railgun** (Super Laser) | **Whip Plasma** (Super Whip) | **Plasma Whip** |
| **Blade Storm** (Super Dual Slash) | **Chainsaw** | **Cyclone Blade** |
| **Magnum** (Super Pistol) | **Star Ring** (Super Orbiter) | **Orbital Cannon** |
| **BunnyHop Super** | **Stormcaller** (Super Lightning Chain) | **Storm Bunny** |

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

## 4. VFX System (String-based Overhauled System)

### 4.1 VFX Database & Keys
ระบบ VFX ได้รับการปรับปรุงโครงสร้างจากเดิมที่เป็น Enum-based (ID ตัวเลข) มาเป็นระบบ **String-key-based lookup** ผ่าน **VFXDatabase** (ScriptableObject) ซึ่งช่วยให้ดีไซเนอร์ปรับจูนและกำหนดค่า Prefab, ขนาดวัตถุ และขนาด Pool Size ได้ผ่านอินเตอร์เฟส Unity Editor โดยตรงโดยไม่ต้องทำการแก้ไขโค้ดใหม่

คีย์ VFX หลักที่พอร์ตมาใช้งานเรียบร้อยแล้ว:
*   **"HitEffect"**: พาร์ติเคิลปะทะการโจมตีพื้นฐาน — แสดงเมื่อการโจมตีปกติโดน Enemy
*   **"CritHitEffect"**: พาร์ติเคิลปะทะแบบคริติคอล — แสดงทดแทน HitEffect เมื่อเกิดการโจมตีติดคริติคอล
*   **"GrenadeExplosion"**: เอฟเฟกต์การระเบิดวงกว้าง สำหรับ Grenade, Rocket และทุ่นระเบิด Mine
*   **"OrbiterHit"**: เอฟเฟกต์การชน/ปะทะสำหรับอาวุธประเภท Orbiter, RadiantAura และ DeathField
*   **"OrbPickup"**: เอฟเฟกต์การเก็บไอเทมเควส และการสัมผัสเก็บ Objective Orb
*   **"EnemyDeath"**: เอฟเฟกต์กระจายตัวเมื่อศัตรูตาย
*   **"WhipSlash"**: เอฟเฟกต์ฟันฟาดรูปเสี้ยวคลื่นสำหรับอาวุธแส้ Whip และ WhipPlasma
*   **"SlashHit"**: เอฟเฟกต์รอยดาบตัดผ่านสำหรับอาวุธ DualSlash, BladeStorm, Cyclone และ Chainsaw
*   **"VortexSpawn"**: วงแสดงจุดศูนย์กลางพายุดูดของอาวุธ Vortex
*   **"DashTrail"**: ควันที่ลากตามรอยการพุ่งแดชของตัวละคร (เช่น BunnyHop)
*   **"MeteorAoE"**: เอฟเฟกต์หินระเบิดกระแทกพื้น AoE ณ จุดแดชแลนดิ้ง
*   **"Stormcaller_AOE"**: เอฟเฟกต์พายุสายฟ้าผ่าของ Stormcaller
*   **"Beam_Railgun"**: พาร์ติเคิลเส้นแสงแนวตรงของ Railgun

### 4.2 Networked Object Pooling (NetworkedVFXPool)
*   **Pre-allocation:** เพื่อลดอาการหน่วง (GC Spikes) ขณะเล่นเกม ไคลเอนต์ทุกคนจะทำการสร้างอินสแตนซ์ของเอฟเฟกต์ตามจำนวน `poolSize` ที่กำหนดในฐานข้อมูลตั้งแต่วินาทีแรกที่โหลดเข้าฉาก และเก็บกลับเข้าพูลเมื่อหมดอายุการแสดงผล
*   **Recursion Guard:** ระบบพูลติดตั้ง depth limit ตรวจสอบความลึกการเกิดซ้ำกรณี Prefab ไปเรียกคำสั่งเปิดตัวเองวนลูป (สูงสุด 8 ชั้น) ป้องกันปัญหาระบบค้างแบบ Stack Overflow
*   **VFXFactory API (Static wrapper):**
    *   `VFXFactory.Play(string key, Vector3 position, float scale = 1f)` - เล่นเอฟเฟกต์ปกติระบุพิกัดและอัตราขยาย
    *   `VFXFactory.PlayBeam(string beamKey, string hitVfxKey, Vector3 from, Vector3 to)` - สั่งวาดเส้นเอฟเฟกต์ลำแสงพร้อมสปอว์น burst ปะทะที่ปลายสาย

### 4.3 Beam VFX Pool
อาวุธลำแสงประเภทเลเซอร์และสายฟ้าวาดผ่าน **LineRenderer** โดยพูลของ Beam จะถูกจัดเก็บเป็นวัตถุเฉพาะทางในพูล:
*   รองรับการระบุ **`beamKey`** เพื่อดึงดีไซน์พาร์ติเคิลเส้นเลเซอร์จากพูลขึ้นมาวาดตำแหน่งจาก `from` ไปยัง `to`
*   หากไม่มีการกำหนดหรือหาไม่เจอ ระบบจะทำงาน fallback อัตโนมัติด้วยการสร้าง LineRenderer ชั่วคราว (สีฟ้าใส)

### 4.4 Dynamic VFX Scaling
*   เอฟเฟกต์แต่ละตัวในฐานข้อมูลจะมีคุณสมบัติ **`designedRadius`** (รัศมีขนาดดั้งเดิมของพาร์ติเคิล)
*   เมื่อส่งระยะจริงที่คำนวณผ่านสเตตัสของผู้เล่น (Actual Range) มาให้พูล ระบบจะทำการคำนวณอัตราส่วนย่อขยาย `ComputeVfxScale = actualRange / designedRadius` เพื่อแปลงขนาด Prefab VFX ให้ตรงกับขอบเขตดาเมจจริงโดยอัตโนมัติ
*   มีฟังก์ชัน `PlayByName3D` สำหรับย่อขยายขนาดแบบ 3D (Non-uniform scale) เหมาะกับการใช้สร้างกรอบ Warning Telegraph แบบเส้นหรือพัด (Line / Cross / Cone) ที่ยืดความยาวแยกจากความกว้างได้

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
- **ระบบแนะนำการ์ด (Card Recommendation System):** เพิ่มเครื่องหมายกากดาวหรือแถบ `⭐ Recommended` บนหน้าการ์ดที่สุ่มได้ เพื่อช่วยให้ผู้เล่นตัดสินใจได้เร็วขึ้นในจังหวะเร่งรีบ:
  * **Fusion/Super Builder:** แนะนำการ์ดที่เป็นวัตถุดิบในการอัปเกรด Super/Fusion ร่วมกับอาวุธที่ผู้เล่นสวมใส่อยู่ปัจจุบัน
  * **Hero Core Synergy:** แนะนำสเตตัสที่เป็น Core Stat ของฮีโร่นั้นๆ (เช่น แนะนำ Ability Haste ให้ Hunter / แนะนำ Move Speed หรือ Area Size ให้ Riven)

### 6.3 Zone Objective (Orb Reward)
- ทุก 2 นาที → spawn Zone Objective ที่จุดสุ่ม (ห่างจาก player)
- ผู้เล่นเข้าไปยืนใน zone → capture progress
- เสร็จ → ทุกคนได้ 1 Orb Reward card (upgrade weapon/stat ที่มีอยู่แล้ว)
- ให้ EXP bonus + heal ทุกคน
- **Objective Pointer (ตัวชี้เป้าหมาย):** หลีกเลี่ยงการทำมินิแมป (เนื่องจากตัวเกมมีกระสุนและศัตรูหนาแน่น ผู้เล่นต้องเพ่งสายตาอยู่ที่ตัวละคร การเหลือบมองมุมจอจะทำให้แทงก์/ตัวละครหลบกระสุนไม่ทัน) จึงเลือกใช้ **"ลูกศรบอกระยะรอบตัวผู้เล่น (Orbiting Arrow/Pointer)"** หรือ **"ลูกศรบอกระยะที่ขอบจอ (Edge-of-Screen Pointer)"** ชี้ระบุทิศทางไปยัง Objective พร้อมแสดงระยะห่างเป็นเมตร (เช่น `Objective 45m ➔`) เพื่อช่วยไกด์ผู้เล่นโดยไม่ต้องละสายตาจากสถานการณ์ตรงหน้า (เลียนแบบการนำทางของ Rabbit and Steel)

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

### 7.2 Enemy Stats & Targeting
- `speed` — ความเร็วเคลื่อนที่
- `contactDamage` — damage เมื่อชน player
- `maxHealth` — HP
- `expReward` — EXP ที่ drop เมื่อตาย
- ทำงานร่วมกับ **Flow Field Pathfinder** เพื่อระบุทิศทางเดินไปยังตำแหน่งเป้าหมายที่อัปเดตแบบเรียลไทม์

### 7.3 Wave Scaling
- ทุก `waveDuration` (60s) → wave ถัดไป
- **HP**: +20% per wave
- **Speed**: +5% per wave (cap 2x)
- **EXP reward**: +15% per wave
- **Spawn rate**: เร็วขึ้น 10% per wave (min 0.3s)
- Wave config เปลี่ยน enemy composition ทุก 3 waves

### 7.4 Flow Field Pathfinding (Server-only)
เพื่อรองรับศัตรูจำนวนหลักพันในหน้าจอเดียวแบบลื่นไหลและประมวลผลได้ดี เกมจึงใช้งานระบบนำทางแบบ **Flow Field Pathfinding** (เทียบเท่า LoL Swarm):
*   **Grid Partitioning:** แบ่งพื้นที่ในแผนที่เป็นตาราง Grid (ดีฟอลต์ 100×100 ช่อง ขนาดช่องละ 2 เมตร ครอบคลุมพื้นที่ 200m×200m)
*   **Static Obstacle Baking:** ตรวจสอบและอบ (Bake) พิกัดสิ่งกีดขวาง (เช่น กำแพงบน Layer "Wall") ตั้งแต่เริ่มฉาก ทำให้ประหยัดโหลดประมวลผลฟิสิกส์ก้าวต่อก้าว
*   **Multi-Source Dijkstra (BFS):** ทำการรัน BFS บนเซิร์ฟเวอร์ทุกๆ `updateInterval` (ปกติทุกๆ 0.5 วินาที) โดยอ้างอิงตำแหน่งของ Player ทุกคนที่ยังมีชีวิตอยู่ ทำให้มอนสเตอร์ถูกคำนวณทางเดินแยกสายกระจายออกไปรุมผู้เล่นที่อยู่ใกล้ที่สุดโดยอัตโนมัติ (Voronoi-like behavior)
*   **Flanking / Crowd Routing (ระบบเบี่ยงฝูงชน):** มีการอัปเดตตาราง **Crowd Density Map** ด้วยพิกัดมอนสเตอร์ทั้งหมด หากจุดใดมีมอนสเตอร์หนาแน่น ช่องแถวนั้นจะได้รับค่า Cost Penalty เพิ่มขึ้น ทำให้มอนสเตอร์ด้านหลังตัดสินใจเดินเบี่ยงออกซ้ายขวาเพื่อโอบล้อม (Flank) ผู้เล่นแทนการเดินต่อคิวเรียงเป็นเส้นตรงเดียว
*   **O(1) Sample Lookup:** มอนสเตอร์แต่ละตัวดึงเวกเตอร์ทิศทางจากช่องพิกัดที่ตนเองทับอยู่ได้ทันที ทำให้ใช้ทรัพยากร CPU ต่ำมาก สามารถรองรับศัตรูจำนวนมากได้โดยไม่มีอาการสะดุด

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

### 8.4 Boss HP Bar UI / HUD (BossHUDUI)
ระบบแสดงผลแถบพลังชีวิตของระดับบอส เพื่อความตื่นเต้นและชัดเจนแก่ผู้เล่นทุกคน:
*   **Main Boss HP Bar (ด้านบนกลางจอ):**
    *   แสดงผลพร้อมหลอดสีเปลี่ยนตามเฟส: เฟส 1 (สีแดง) -> เฟส 2 (สีส้ม) -> เฟส 3 (สีแดงเข้ม/Enrage)
    *   มีจุดระบุมาร์กเกอร์เฟส (`Phase2Marker` 60% และ `Phase3Marker` 30%) ชัดเจนบนหน้าหลอด
    *   **Enrage Warning:** เมื่อหมดเวลาของด่านหลักและบอสกำลังจะเปิดเฟสคลั่งในอีก 45 วินาที จะมีระบบส่งประกาศขึ้นข้อความเตือนตัวเบ้อเร่อกลางหน้าจอ (`⚠ ENRAGE IN X seconds!`) พร้อมแจ้งเตือนผู้เล่น
*   **Mini Boss HP Bars (ด้านขวา/มุมล่าง):**
    *   ทำงานแบบไดนามิก (Dynamic Layout Panel) ดึงข้อมูลผ่าน static event เมื่อ Mini Boss เกิดหรือตาย
    *   หลอด HP ของ Mini Boss แต่ละตัวจะถูกสร้าง (Instantiate) และจัดกลุ่มเข้า Layout อัตโนมัติ และจะลบแถบออก (Destroy) ทันทีที่ผู้เล่นกำจัดสำเร็จ

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
- **Boss HP HUD** — แถบเลือดหลักของ Main Boss (บนกลางจอ) พร้อมมาร์กเกอร์เฟส และแถบย่อยแบบซ้อนของ Mini Boss ทุกตัวที่กำลัง active (มุมล่าง/ขวา)
- **Objective Distance Pointer** — ลูกศรบอกทิศทางของ Zone Objective รอบตัวหรือขอบจอพร้อมระบุระยะห่างเป็นเมตร ช่วยไกด์ผู้เล่นโดยไม่ต้องเหลือบมองมินิแมป

### 11.2 Level Up UI
- 3 Upgrade Cards แสดงพร้อมกัน
- แต่ละ card แสดง: icon, name, description, level
- Countdown timer (30s)
- "X / Y players picked" status
- **ระบบแนะนำการ์ด (Card Recommendation System):** แสดงเครื่องหมาย `⭐ Recommended` บนหน้าการ์ดที่แนะนำ เช่น การ์ดวัตถุดิบทำ Fusion หรือ Core Stat ของฮีโร่นั้นๆ

### 11.3 Online Lobby & Session UX
- **Online Menu & Character Select UI:** หน้าต่างเลือกฮีโร่และตั้งค่าล็อบบี้ รองรับการตรวจจับการเชื่อมต่อและแสดงสถานะความพร้อม (Ready) ของผู้เล่นแต่ละคนก่อนเริ่มเกม

### 11.4 End Screen
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

---

## 13. Developer Tools & Test Playgrounds

เพื่ออำนวยความสะดวกในระหว่างการทดสอบสมดุลและการทำงานของอาวุธ ได้มีการสร้างห้องทดลองจำลองเฉพาะสำหรับทีมผู้พัฒนาขึ้น:

### 13.1 Weapon Test Scene & WeaponTestManager
*   **Quick Equip Panel:** เมนูปุ่มกดบนหน้าจอที่จะเจนชื่ออาวุธทั้งหมดของฐานข้อมูล (`allWeapons[]`) และความสามารถพิเศษขึ้นมาแบบไดนามิก เพื่อคลิกสวมใส่อาวุธเลเวล 5 หรือความสามารถนั้นๆ ได้ทันทีแบบไม่ต้องรอเลเวลอัป
*   **Real-time DPS Tracker:** ระบบนับดาเมจรวมสะสม และคำนวณ DPS (Damage Per Second) เฉลี่ยแบบเรียลไทม์ (คำนวณแยกและรีเซ็ตค่าเฉลี่ยใหม่ทุกๆ 1 วินาที) เพื่อเปรียบเทียบระดับความแรงที่แท้จริง
*   **Target Dummies (ตุ๊กตาหุ่นทดลอง):** หุ่นทดสอบแบบตั้งเป้าที่สามารถ respawn ใหม่ได้โดยกดปุ่มบนหน้าจอ หรือกดปุ่มรีเซ็ตเลือดหุ่นทั้งหมดพร้อมกันเพื่อทดสอบคอมโบ
*   **Stat Multiplier Sliders:** สไลเดอร์ปรับตัวคูณและสเตตัสเฉพาะแบบเรียลไทม์เพื่อจำลองบิลด์ของผู้เล่น เช่น ปรับดาเมจ (1x–10x), เพิ่ม Ability Haste (0–200) หรือคริติคอลแชนซ์ (0–100%) เพื่อดูผลกระทบต่ออาวุธที่กำลังสวมใส่ได้ทันที

---

## 14. Meta-Progression System (ระบบพัฒนาการนอกเกม)

เพื่อสร้างเป้าหมายระยะยาวให้แก่ผู้เล่น (Long-term Progression Loop) โครงการจะออกแบบระบบการพัฒนาตัวละครถาวรนอกเกม:
*   **Gold Accumulation (การสะสมทอง):** ผู้เล่นจะสะสมเหรียญทองจากการกำจัดมอนสเตอร์/บอส และการผ่านด่าน/Objective ในแต่ละรอบ (Run) โดยเหรียญทองจะซิงก์เข้าบัญชีส่วนกลางเมื่อสิ้นสุดการเล่น (ไม่ว่าจะแพ้หรือชนะ)
*   **Talent Shop (ร้านอัปเกรดความสามารถถาวร):** นำทองที่สะสมได้มาปลดล็อกและอัปเกรดสเตตัสถาวรที่จะส่งผลกับตัวละครทุกตัวตั้งแต่เลเวล 1:
    *   *Attack Power Up:* เพิ่มพลังโจมตีถาวร (+3% ต่อระดับเลเวล, สูงสุดเลเวล 5)
    *   *Max Health Up:* เพิ่ม HP สูงสุดถาวร (+50 HP ต่อระดับเลเวล, สูงสุดเลเวล 5)
    *   *Haste Speed:* เพิ่มค่า Ability Haste เริ่มต้น (+5 flat ต่อระดับเลเวล, สูงสุดเลเวล 5)
    *   *Magnet Boost:* เพิ่มรัศมีการดูดเหรียญ/EXP ถาวร (+10% ต่อระดับเลเวล, สูงสุดเลเวล 5)
    *   *Gold Finder:* เพิ่มโอกาสดรอปและตัวคูณทองถาวร (+10% ต่อระดับเลเวล, สูงสุดเลเวล 5)
    *   *Second Chance (ชุบชีวิต):* ฟื้นคืนชีพตัวละครทันทีเมื่อพลังชีวิตหมดลง 1 ครั้งต่อเกม (ชุบชีวิตขึ้นมาด้วย HP 30%, ซื้อได้สูงสุดเลเวล 1)
*   **Unlocks (การปลดล็อกคอนเทนต์):** ใช้ทองในการปลดล็อกตัวละครใหม่ (เช่น Gunner, Hunter) หรือการ์ดอาวุธเฉพาะตัวละครในตารางเลือกอัปเกรด

---

## 15. Stage Difficulty System (ระบบความยากของด่าน)

ก่อนเริ่มห้องเล่นออนไลน์ Co-op หัวหน้าห้อง (Host) สามารถเลือกปรับระดับความยากของด่านในหน้าล็อบบี้ ซึ่งจะมีผลโดยตรงต่อการคำนวณข้อมูลฝั่ง Server-authoritative ดังนี้:

### 15.1 ตารางเปรียบเทียบระดับความยาก (Difficulty Multipliers)

| ระดับความยาก (Difficulty) | ตัวคูณ HP ศัตรู | ตัวคูณดาเมจศัตรู | ตัวคูณทอง & EXP | คุณลักษณะเด่นและผลกระทบเชิงระบบ (System Impact) |
| :--- | :---: | :---: | :---: | :--- |
| **Easy (ง่าย)** | 0.7x | 0.7x | 0.7x | เหมาะสำหรับผู้เริ่มต้น หรือใช้ทดลองการประสานอาวุธใหม่ๆ |
| **Normal (ปกติ)** | 1.0x | 1.0x | 1.0x | ค่าสเตตัสมาตรฐานและความเร็วปกติ |
| **Hard (ยาก)** | 1.5x | 1.5x | 2.0x | เพิ่มโอกาส 20% ที่มอนสเตอร์ระดับ Elite จะสุ่มเกิดมาพร้อมม็อดพิเศษ (เช่น บาเรียซับดาเมจ Shielding หรือ ปล่อยไฟรอบตัว Fire Aura) |
| **Nightmare (ฝันร้าย)** | 2.5x | 2.5x | 4.0x | ศัตรูเดินเร็วขึ้น 15%, บอสลด Cast time การโจมตีลง 20%, และลดเวลาเตือนภัยของ Telegraph Area ลงเหลือ 2.0 วินาที (จาก 2.5 วินาที) |

### 15.2 ผลกระทบระบบภายใต้โหมดความยากระดับสูง (Advanced Mechanics Impact)
1.  **AI Aggression Scaling:** ยิ่งเลือกระดับความยากสูง ความถี่ในการเปลี่ยนพิกัดเป้าหมาย (Re-target interval) ของฝูงศัตรูจะเร็วขึ้น ทำให้ศัตรูโอบล้อมบีบผู้เล่นได้รวดเร็วยิ่งขึ้น
2.  **Raid Mechanic Modification (Nightmare Mode):** บอสระดับ Main Boss จะข้ามลำดับคูลดาวน์บางตัว และอาจใช้กลไกการโจมตีแบบซ้อนทับกัน (Overlapping Telegraphs) เช่น การบังคับให้ผู้เล่นทำกลไกแชร์ดาเมจ (Stack) ไปพร้อมกับการหันหลังหลบพลังงานบอส (Gaze) บังคับการจัดตำแหน่งที่แม่นยำสูงแบบวินาทีต่อวินาที (สไตล์เกมแนว *Rabbit and Steel*)
