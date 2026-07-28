# แผนงาน: Server-side State → Reconnect → Host Migration

> เขียนหลัง audit ใหม่ 2026-07-28 · ตัดสินใจแล้ว: **co-op PvE เน้นเล่นกับเพื่อน · matchmaking เปิด-ปิดได้ ·
> ต้องการ client reconnect และ host migration**
>
> เอกสารนี้เป็น **roadmap** ไม่ใช่ใบสั่งงานของลูป — แต่ละ stage ค่อยแตกเป็น `implementation_plan.md` ทีละอัน

---

## ทำไมทุกอย่างมารวมกันที่จุดเดียว

```
client reconnect  ─┐
host migration    ─┼─→ ต้องการ state ที่ server เป็นเจ้าของ + serialize ได้ + ผูกกับ playerId ที่คงที่
anti-cheat        ─┘
audit 1.2/1.3/2.6 ─┘
```

ตอนนี้ progression ของผู้เล่นอยู่ใน **Dictionary ฝั่ง client ล้วน** — `PlayerStatManager.statLevels`
และ `PlayerWeaponManager.slots` server ไม่เคยมีสำเนา
client หลุด = ข้อมูลหายไปกับเครื่องนั้น ไม่มีอะไรให้กู้ และ server คำนวณ damage เองไม่ได้เพราะไม่รู้ stat

**Stage 1 จึงเป็นรากของทุกอย่าง** ถึงหยุดแค่ stage 1 ก็ยังคุ้ม เพราะปิด audit 1.2 / 1.3 / 2.6 ไปในตัว

---

## ข้อจำกัดที่ต้องรู้ก่อน (ยืนยันแล้ว)

- `com.unity.netcode.gameobjects` **2.13.0** · `com.unity.services.multiplayer` **2.2.4**
- Multiplayer Services มี `WithHostMigration` ให้ (snapshot อัตโนมัติ + เลื่อน host + ให้ client ต่อกลับ)
  แต่ต้องเขียน **`IMigrationDataHandler`** เอง — Unity แถม handler สำเร็จรูปให้เฉพาะ **Netcode for Entities**
- คำแนะนำทางการของ Unity สำหรับ NGO คือใช้ **Distributed Authority** —
  **เราไม่เอาทางนั้น** เพราะย้ายอำนาจไปฝั่ง client สวนทางกับ server-authoritative ทั้งโปรเจกต์
  และสวนทางกับความตั้งใจจะเปิด matchmaking
- `GameSessionManager` มี **`playerId` (string) จาก Unity Authentication** อยู่แล้ว
  → **นี่คือกุญแจของ reconnect** ต่างจาก `OwnerClientId` ที่เปลี่ยนทุกครั้งที่ต่อใหม่
- **ยังไม่มี `ConnectionApproval` / `OnClientDisconnectCallback` / `OnClientConnectedCallback` เลยสักบรรทัด**

---

## Stage 0 — เก็บของเล็กก่อน (ทำคู่ขนานได้ ไม่บล็อกใคร)

### 0.1 buff ชั่วคราวไม่มีผลกับ client  🔴 บั๊กจริงที่คนเล่นเจอวันนี้

field ที่ไม่ replicate แต่ถูกใช้ในโค้ดฝั่ง server:

| field | ไฟล์ | ใช้ที่ไหน |
|---|---|---|
| `tempMoveSpeedBonus` | `playermove.cs:295` | `:155` คำนวณความเร็ว (server) |
| `tempHealthRegenBonus` | `playermove.cs:298` | `:111` regen (server, มี `if (IsServer)`) |
| `tempAbilityHaste` | `PlayerStatManager.cs:20` | ตัวคูณ cooldown |
| `tempDamageBonusMult` | `PlayerStatManager.cs:23` | ตัวคูณ damage |

weapon เซ็ตค่าบน **owner client** → สำเนาฝั่ง server ของคนที่ไม่ใช่ host ยังเป็น 0
→ **Blade of Exile / SupportArena และบัฟอื่นไม่ทำงานเลยสำหรับทุกคนที่ไม่ใช่ host**

**อย่าเพิ่งแก้ด้วย `NetworkVariable` ทีละตัว** — Stage 1 จะย้ายการคำนวณทั้งหมดไป server อยู่แล้ว
ทำแบบชั่วคราวคือเพิ่ม ServerRpc ให้ weapon แจ้ง server ว่า "ขอ buff X นาน Y" แล้ว server ถือค่าเอง
ซึ่งเป็นรูปแบบเดียวกับที่ Stage 1 จะใช้ → ไม่เสียของ

### 0.2 Hit RPC ยิงทุกครั้งที่โดนดาเมจ  🟠 perf

`Enemy.cs:369` `NotifyHitClientRpc` ยิงหาทุก client ทุกครั้งที่ enemy โดนตี
bullet-heaven โดนตีหลายสิบครั้ง/วินาที = ต้นทุน network ที่ใหญ่ที่สุดในเกม

**เป็นงานออกแบบ ไม่ใช่งานกล** — `CLAUDE.md` ระบุว่า `HitEffect`/`CritHitEffect` มาจาก RPC ตัวนี้
ต้องเลือกก่อน: batch ต่อเฟรม · throttle ต่อ enemy · หรือให้ฝั่งคนยิง predict เอง
**ควรตัดสินใจก่อนเริ่ม Stage 1** เพราะ Stage 1 จะแตะ damage path เดียวกัน

### 0.3 ของเล็กที่ทำเมื่อว่าง

- `TelegraphZone.cs:295` ยัง `Instantiate(detonateVfxPrefab)` — ต้องเพิ่ม entry ใน `VFXDatabase` ก่อน
  พร้อมอีก 3 จุด: `CharacterAnimationEvents.cs:50` · `MineObject.cs:79` · `ObjectiveOrb.cs:147`
- `timeScale = 0` ไม่มี failsafe ฝั่ง client (server มี `ForceAutoPick` timer แล้ว)

---

## Stage 1 — Server ถือ player state  ⭐ รากของทุกอย่าง

### ขนาดข้อมูลจริง (เล็กกว่าที่คิด)

| อะไร | ขนาด | สถานะตอนนี้ |
|---|---|---|
| character index | 1 int | ✅ มี `NetworkVariable` แล้ว (`PlayerVisual._charIndex`) |
| HP / maxHP / isDead | 3 ค่า | ✅ มี `NetworkVariable` แล้ว |
| **stat levels** | **สูงสุด 6 × (StatType, int)** | ❌ `Dictionary` ฝั่ง client |
| **weapons** | **สูงสุด 6 × (weaponName, level)** | ❌ `List<WeaponSlot>` ฝั่ง client |
| carried quest items | 1 int | ✅ มี `NetworkVariable` แล้ว |

**รวมแล้วไม่กี่ร้อยไบต์ต่อผู้เล่น** — `MaxStatSlots = 6` และ `MaxWeaponSlots = 6` เป็นเพดานที่มีอยู่แล้ว

**`statTotals` ไม่ต้อง replicate** — คำนวณกลับจาก `statLevels` + `StatData` asset ได้
เก็บแค่ level แล้ว derive ที่เหลือ ลดขนาดและตัดโอกาส desync

### งานที่ต้องทำ

1. **struct ที่ serialize ได้** — `StatEntry { StatType type; byte level; }` และ
   `WeaponEntry { FixedString64Bytes name; byte level; }` (ใช้ `FixedString` ไม่ใช่ `string` เพราะต้อง blittable)
2. **ย้ายเจ้าของ** — `PlayerStatManager.statLevels` และ `PlayerWeaponManager.slots` เป็น
   `NetworkList<StatEntry>` / `NetworkList<WeaponEntry>` (Server write, Everyone read)
   client อ่านอย่างเดียว UI เดิมยังใช้ getter ตัวเดิมได้ (`GetStatLevel`, `GetEquippedStats`)
3. **ย้ายการคำนวณ** — `ApplyStatLocal` ที่รันบน client ย้ายไป server
   getter ทั้ง 10 ตัว (`GetPowerMultiplier` ฯลฯ) ต้องอ่านจาก `NetworkList` แทน Dictionary
4. **ปิดช่องแจก progression** ← ได้มาฟรีตรงนี้
   `AddWeaponServerRpc` / `UpgradeWeaponServerRpc` / `ReplaceWeaponServerRpc` /
   `FuseWeaponsServerRpc` / `SpawnPassiveWeaponServerRpc` / `ApplyStatServerRpc`
   → server เช็คก่อนว่า **ผู้เล่นอยู่ใน upgrade phase จริงและยังไม่ใช้สิทธิ์**
   (`SharedExperienceManager` รู้อยู่แล้ว) และ **การ์ดใบนั้นอยู่ในชุดที่เสนอไปจริง**
5. **ย้าย damage/crit ไป server** (audit 2.6) — RPC ส่งแค่ "อาวุธไหน ทิศไหน"
   server รู้ weapon level + stat แล้ว คำนวณ damage + roll crit เอง
   → ลบพารามิเตอร์ `damage` / `isCrit` ออกจาก ServerRpc ~25 ตัว
6. **EXP multiplier** (audit 1.2) — server คำนวณจาก `statLevels` ของจริง ลบ `ApplyExpMultiplierServerRpc(float)`

### ปิด audit item ที่ค้าง

**1.2 · 1.3 · 1.4 · 2.6 · P2.1 · P2.2 ปิดหมดใน stage เดียว** เพราะทั้งหมดติดคอขวดเดียวกัน

### เกณฑ์ว่าเสร็จ

- ไม่มี ServerRpc ตัวไหนรับ `damage` / `isCrit` / `multiplier` เป็นพารามิเตอร์อีก
- ปิดเกม client แล้วเปิดใหม่ → server ยังรู้ว่าผู้เล่นคนนั้นมีอาวุธ/stat อะไร (เตรียมทาง stage 2)
- เล่น 2 คน stat/weapon ของแต่ละคนถูกต้องบนทุกจอ

---

## Stage 2 — Connection lifecycle + client reconnect

ต้องมี Stage 1 ก่อน (ไม่มี state ก็ไม่มีอะไรให้กู้)

1. **`ConnectionApproval`** — เปิดใน `NetworkManager` เพื่อรับ payload ตอนต่อเข้ามา
   client ส่ง `playerId` จาก Sessions มาด้วย
2. **`OnClientDisconnectCallback`** — server ไม่ลบ state ทันที แต่ย้ายเข้า
   `Dictionary<string playerId, PlayerSnapshot>` พร้อม timestamp
3. **จับคู่ตอนกลับมา** — approval เจอ `playerId` ที่ค้างอยู่ → คืน state เดิมให้ NetworkObject ตัวใหม่
   (`OwnerClientId` จะเป็นคนละค่า **ห้ามใช้เป็นกุญแจ**)
4. **นโยบายที่ต้องตัดสิน**
   - เก็บ slot ไว้กี่วินาที (แนะนำ 120s แล้วค่อยปล่อย)
   - ตัวละครที่หลุดค้างอยู่ในโลกหรือหายไป (แนะนำ: หายไป กันโดนรุมตอนไม่มีคนคุม)
   - เข้ากลับกลางบอสได้ไหม
   - เต็มห้องแล้วมีคนค้างสิทธิ์อยู่ คนใหม่เข้าได้ไหม

**จบ stage นี้ = client reconnect ใช้ได้จริง** ไม่ต้องรอ stage 3-4

---

## Stage 3 — Run state snapshot

state ระดับ "รอบการเล่น" ที่ไม่ได้ผูกกับผู้เล่นคนใดคนหนึ่ง — จำเป็นสำหรับ migration
และใช้ทำ "เล่นต่อจากที่ค้าง" ได้ด้วย

| อะไร | อยู่ที่ไหนตอนนี้ |
|---|---|
| game time / phase | `GameTimeline` (`gameTime`, `isMainBossPhase` เป็น NetworkVariable แล้ว) |
| wave / spawn rate | `EnemySpawner` + `WaveManager` |
| objective progress | `ZoneObjective` (phase, delivery progress) |
| boss phase + HP | `MainBoss` / `MiniBossAI` |
| shared EXP / level | `SharedExperienceManager` (NetworkVariable แล้ว) |

งาน: รวมเป็น struct เดียวที่ serialize ได้ + เมธอด `Capture()` / `Restore()`

---

## Stage 4 — Host migration

1. เปิด session ด้วย **`WithHostMigration`** (`com.unity.services.multiplayer` 2.2.4)
2. เขียน **`IMigrationDataHandler`** — serialize = Stage 1 (ทุกผู้เล่น) + Stage 3 (รอบการเล่น)
3. host ใหม่ `Restore()` แล้วให้ client ที่เหลือต่อกลับ

### จุดตัดสินใจใหญ่: โลกในเกมหลัง migrate

| ทาง | ได้ | เสีย |
|---|---|---|
| **สร้างโลกใหม่** (แนะนำ) | serialize แค่ player + run progress · งานเล็กกว่าหลายเท่า | enemy/boss ที่กำลังสู้หายไป boss กลับไปต้น phase |
| เก็บโลกทั้งหมด | ต่อเนื่องสมบูรณ์ | ต้อง serialize ทุก NetworkObject ที่มีชีวิต — enemy หลายร้อยตัว, projectile, telegraph, VFX |

เกม co-op ส่วนใหญ่เลือกทางแรก ผู้เล่นยอมรับได้เพราะ progression ไม่หาย

---

## ลำดับที่แนะนำ

```
0.2 ตัดสินใจเรื่อง Hit RPC   ← ตัดสินใจอย่างเดียว ยังไม่ต้องเขียน
0.1 แก้ buff ไม่ทำงาน         ← บั๊กจริง ทำได้เลย
Stage 1  server ถือ player state   ← ก้อนใหญ่สุด แตกเป็นหลายลูป
Stage 2  reconnect                 ← ได้ฟีเจอร์แรก
Stage 3  run snapshot
Stage 4  host migration            ← ได้ฟีเจอร์ที่สอง
```

Stage 1 ควรแตกเป็นลูปย่อย: struct + NetworkList → ย้าย stat → ย้าย weapon →
ปิดช่อง progression → ย้าย damage/crit

---

## กฎกันหนี้บานปลาย (ใช้ตั้งแต่วันนี้)

> เวลาเพิ่มอาวุธ/สกิลใหม่ **อย่าเพิ่ม ServerRpc ที่รับ `damage` เป็นพารามิเตอร์อีก**
> ทุกตัวที่เพิ่มวันนี้คือของที่ต้องรื้อใน Stage 1
> ถ้าเลี่ยงไม่ได้ ให้ส่ง "ชื่ออาวุธ + level" แทนตัวเลขดิบ จะได้ย้ายไปคำนวณฝั่ง server ทีหลังโดยไม่แก้ call site

ตอนนี้มี ~25 ตัวที่ต้องรื้อ ถ้าไม่หยุดเพิ่มจะเป็น 40 ก่อนถึงวันลงมือ

---

## สิ่งที่สแกนแล้วไม่มีปัญหา (ไม่ต้องเสียเวลาอีก)

- **ไม่มี `RequireOwnership = false` เลยสักตัว** → client โกงได้แค่ให้ตัวเอง ยุ่งกับคนอื่นไม่ได้
- **Owner-write NetworkVariable มีตัวเดียว** — `PlayerVisual._netSpeed` (animation ไม่ใช่ gameplay)
- **static event ไม่รั่ว** — สมดุลทุกไฟล์ ยกเว้น `OnlineMenuUI` ที่ `-=` เกิน (ไม่อันตราย)
