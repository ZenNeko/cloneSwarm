# ADR-002: โครงสร้าง Tether หลายโหมด

**Status:** Accepted
**Date:** 2026-08-10
**Deciders:** เจ้าของโปรเจกต์ (ตอบครบทุกข้อผ่านการซัก)
**คำถามตั้งต้น:** "อยากได้ Far / Close tether · ขึ้นโครง Transferable กับ Leash ไว้ · DOT อาจยังไม่ดีกับเกม"

> ทุกข้อยืนยันจากไฟล์จริงแล้ว · อ้างอิงพฤติกรรมปัจจุบันจาก `BossTether.cs` หลังแก้รอบ 2026-08-10
> อ้างอิงกลไกต้นแบบใน [`boss-tether-findings.md`](boss-tether-findings.md) และผลค้น FF14 / Rabbit and Steel

---

## Context

ตอนนี้มี tether แบบเดียว — **Far** (วิ่งออกจากกันให้สายขาด) เพิ่งแก้บั๊ก 4 ข้อไปเมื่อ 2026-08-10
ต้องการเพิ่มอีก 3 โหมดโดยไม่รื้อของเดิม

### ข้อเท็จจริงจากโค้ดที่เปลี่ยนรูปแผน

**1. `PlayerStatusManager` มีระบบ status ครบแล้ว — DOT ไม่ใช่ระบบใหม่**

[`PlayerStatusManager.cs`](../Assets/Script/Data/Augment/PlayerStatusManager.cs) มี `NetworkList<StatusEntry>`
พร้อม stack / หมดอายุ / `decayInterval` / ตัวคูณดาเมจต่อ stack / hook `onExpire` ที่ยิง `BossAction` ได้
ที่ขาดคือ **ดาเมจต่อ tick อย่างเดียว** — เพิ่ม 2 field ใน `StatusEffectData` + ตัวสะสมใน `Update` ที่มีลูปอยู่แล้ว ≈ 15 บรรทัด

ดังนั้น "DOT แพงเกินไป" ไม่เป็นความจริงในเชิงต้นทุน · แต่**สุดท้ายก็ไม่ต้องทำอยู่ดี** ด้วยเหตุผลอื่น (ดู Decision 4)

**2. การเคลื่อนที่เป็น owner-authoritative — `CLAUDE.md` เขียนผิด**

`CLAUDE.md` ระบุ `client → playermove ServerRpc → server moves transform`
แต่ [`playermove.cs:150-159`](../Assets/Script/playermove.cs) เขียน `rb.linearVelocity` **บน owner client**
และ `OnNetworkSpawn` ปิด physics ให้ non-owner โดยให้ NetworkTransform sync แทน
knockback ก็เดินทางเดียวกัน — `ApplyKnockbackClientRpc` → owner รัน coroutine เอง + ธง `isKnockedBack` กัน `FixedUpdate` เขียนทับ

**ผลกับ Leash โดยตรง:** server clamp ตำแหน่งไม่ได้ จะสู้กับ owner แล้วยางยืด

**3. ผู้เล่นที่ตายแล้ว "ยังมีศพ" อยู่ในสนามและ respawn อัตโนมัติ**

- ตายแล้ว object ไม่ถูกทำลาย/ซ่อน — `PlayerVisual` แค่เซ็ต animator bool (`PlayerVisual.cs:208`) ศพจึงมองเห็นและมีตำแหน่งจริง
- respawn อัตโนมัติหลัง 10–60 วิ (สเกลตามเวลาเกม) แล้ว **teleport ไปหาผู้เล่นที่รอด** (`playermove.cs:254-266`)

**4. `RandomAttackAction` มีอยู่แล้วและรับ `BossAction` อะไรก็ได้**

[`RandomAttackAction.cs`](../Assets/Script/Data/RandomAttackAction.cs) สุ่มจาก `List<BossAction>` — ไม่ต้องเขียนการสุ่มโหมดลงใน `TetherAction`

---

## Decision

### 1 · component เดียว + `TetherMode` enum

```csharp
public enum TetherMode { Far, Close, Leash, Transferable }
```

`BossTether.cs` ตัวเดียว · prefab ตัวเดียว · `TetherAction` ตัวเดียวที่มี field `mode`
ส่วนที่ใช้ร่วมกันทั้งหมด (เลือกเป้า · วาดสาย · จับเวลา · ประกาศ HUD · despawn) เขียนครั้งเดียว
`switch (mode)` เฉพาะตอน resolve

### 2 · Leash ดึงกลับด้วย knockback เดิม ไม่ทำดาเมจ

ออกนอกรัศมีได้ แต่ server ยิง `ApplyKnockbackClientRpc` ดึงกลับเข้าหา anchor
ไม่สร้าง authority path ใหม่ ไม่มียางยืด และหลบ AoE อื่นออกนอกรัศมีชั่วคราวได้

**Leash ใน co-op — ทำครบ 3 scope**

```csharp
public enum LeashScope { EachPlayerOwnAnchor, SingleRandom, AllSharedAnchor }
```

ต่างกันแค่ spawn กี่ตัวและ anchor อยู่ไหน — เป็นลูปกับการ resolve จุด ไม่ใช่ logic ใหม่

### 3 · Close เช็คตอนหมดเวลาอย่างเดียว

หมดเวลาแล้วอยู่ใกล้กัน = รอด · ห่างกัน = กินดาเมจก้อนเดียวทั้งคู่
เป็น code path เดียวกับ Far แค่กลับเครื่องหมายเปรียบเทียบ

```csharp
public enum CloseFailMode { OnTimeout, Continuous, Instant }   // ใช้จริงแค่ OnTimeout
```

`Continuous` / `Instant` — **enum + TODO ไม่มี behavior**

### 4 · ไม่ทำ DOT

หลังตัดสินข้อ 2 และ 3 แล้ว **ไม่มีโหมดไหนต้องใช้ DOT เลย**

| โหมด | ดาเมจ |
|---|---|
| Far | ก้อนเดียวตอนหมดเวลา (ของเดิม) |
| Close | ก้อนเดียวตอนหมดเวลา |
| Leash | ไม่มี — ดึงกลับแทน |
| Transferable | TODO |

DOT จะจำเป็นก็ต่อเมื่อเปิด `CloseFailMode.Continuous` ซึ่งเป็น TODO
**ตอนนั้นค่อยตัดสินใจ** ว่าจะใส่ใน `StatusEffectData` (ได้ HUD + stack ฟรี) หรือ tick เองใน `BossTether`

### 5 · ของที่ "ขึ้นโครงไว้" = enum + TODO เท่านั้น

`TetherMode.Transferable`, `CloseFailMode.Continuous`, `CloseFailMode.Instant`
คอมไพล์ผ่าน เลือกได้ใน Inspector แต่ไม่มีพฤติกรรม

**ต้องมี guard เสมอ** — เลือกโหมดที่ยังไม่ทำต้อง `Debug.LogWarning` ชัดเจนแล้ว fallback เป็น `Far`
ห้ามเงียบ ไม่งั้น designer จะเจอ tether ที่ spawn แล้วไม่ทำอะไรและอ่านว่าเป็นบั๊ก

### 6 · การกรองคนตายต้องขึ้นกับโหมด — **แก้ของที่เพิ่ง ship ไป**

รอบ 2026-08-10 ใส่ตัวกรอง `isDead` แบบเหมารวมใน `TetherAction` **ซึ่งถูกเฉพาะ Far**

| | เป้าหลัก (คนที่ต้องวิ่ง) | anchor |
|---|---|---|
| **Far** | ต้องเป็น | **ต้องเป็น** — ศพวิ่งไม่ได้ สายไม่มีทางขาด กลไกแก้ไม่ได้ |
| **Close** | ต้องเป็น | **เป็นศพได้** — คนเป็นเดินไปยืนข้างศพเพื่อนได้ กลไกแก้ได้ปกติ |
| **Leash** | ต้องเป็น | จุดคงที่ ไม่เกี่ยว |

Close ที่เหลือคนเดียวจริงๆ (solo) → ผูกกับเสา anchor เหมือน Far-solo

### 7 · การสุ่มโหมดใช้ `RandomAttackAction` ไม่เขียนใหม่

ทำ `TetherAction.asset` หลายตัวต่างโหมด แล้วโยนเข้า pool ของ `RandomAttackAction`

---

## Options Considered

### โครงสร้าง component

| | component เดียว + enum ✅ | แยก component + base class | Far/Close รวม · Transferable แยก |
|---|---|---|---|
| ความซับซ้อน | Med (switch เดียว) | Low ต่อไฟล์ แต่ Med รวม | Med |
| งาน Editor | prefab 1 ตัว | **prefab 4 ตัว** | prefab 2 ตัว |
| ความเสี่ยง NGO | ลงทะเบียนครั้งเดียว | **4 จุดที่ลืมได้** (convention #7) | 2 จุด |
| ต้นทุน Transferable ที่อาจไม่ใช้ | 1 case ใน switch | **ไฟล์+prefab+asset ทั้งชุด** | ไฟล์ทั้งชุด |

**เลือก component เดียว** — ตรงกับ precedent ที่มีอยู่: `TelegraphZone` จัดการ Circle/Line/Cross/Donut/Chase
ผ่าน `aoeType` enum + `isColorMatch` เป็น variant flag บน prefab เดียว
และ Far กับ Close ต่างกันแค่เครื่องหมายเปรียบเทียบจริงๆ การแยกไฟล์จะได้โค้ดซ้ำมากกว่าได้ความชัด

### การบังคับรัศมีของ Leash

| | knockback ดึงกลับ ✅ | owner clamp แข็ง | ดาเมจต่อเนื่อง |
|---|---|---|---|
| เข้ากับ authority ที่มีอยู่ | ใช้ทางเดิมทั้งหมด | ต้องเขียน clamp ใหม่ | ได้ |
| ความรู้สึกตอนเล่น | ยางยืด อ่านออก | กำแพงล่องหน อึดอัด | ลงโทษเงียบ |
| เสี่ยงดาเมจหลบไม่ได้ | ต่ำ — ออกนอกรัศมีชั่วคราวได้ | **สูง** — ติดกับดักถ้า AoE ลงในรัศมี | สูง |
| ต้องมี DOT | ไม่ | ไม่ | **ต้อง** |

---

## Trade-off Analysis

**สิ่งที่ยอมแลก** — `BossTether.cs` จะโตขึ้นและมี `switch (mode)` หลายจุด
ยอมรับได้ตราบที่ส่วน "เลือกเป้า → วาดสาย → จับเวลา → despawn" ยังเป็นเส้นทางเดียว
ถ้าวันหนึ่ง `switch` เริ่มกระจายไปทั่วไฟล์ นั่นคือสัญญาณให้แยก strategy object — **ยังไม่ใช่ตอนนี้**

**สิ่งที่ยอมแลกข้อสอง** — enum ที่ไม่มี behavior คือหนี้ที่ต้องจ่าย
โปรเจกต์นี้เพิ่งลบ `SkipTether()` ที่เป็น dead API ไปเมื่อรอบก่อน จึงรู้ราคาดี
guard ที่ fail loud (Decision 5) คือส่วนลดความเสี่ยงที่ถูกที่สุด — ไม่ทำถือว่าไม่ครบ

**สิ่งที่ไม่ยอมแลก** — ไม่สร้างกลไกที่ผู้เล่นแก้ไม่ได้
เป็นบทเรียนจากบั๊ก "ผูกกับศพ" · Decision 2 (ไม่ขังแข็ง) และ 6 (กรองตามโหมด) มาจากข้อนี้ทั้งคู่

---

## Consequences

**ง่ายขึ้น** — เพิ่มโหมดที่ 5 = enum + case · ผูก `RandomAttackAction` ได้ทันทีโดยไม่แตะโค้ด

**ยากขึ้น** — `BossTether.cs` กลายเป็นจุดรวมของ 4 กลไกที่ต่างกัน อ่านครั้งแรกใช้เวลานานขึ้น
และการเทสต์บานเป็นเมทริกซ์ (โหมด × solo/co-op × เพื่อนเป็น/ตาย)

**ต้องกลับมาทบทวน**

1. **ศพ respawn แล้ว teleport — Close ที่ผูกกับศพจะพังกลางคัน**
   `playermove.cs:263` ย้ายศพไปหา "ผู้เล่นที่รอดคนแรกในลิสต์" ซึ่งอาจไม่ใช่คนที่ถูกผูก
   anchor จะกระโดดข้ามสนามแล้วคนเป็นแพ้ทั้งที่ไม่ได้ทำอะไรผิด
   หน้าต่างชนกันแคบ (respawn 10–60 วิ · tether ~8 วิ) แต่เกิดได้จริง
   **ทางแก้ที่เสนอ:** anchor ฟื้นระหว่าง tether → ถือว่าสำเร็จทันที ไม่มีดาเมจ
2. `CLAUDE.md` ระบุ authority การเคลื่อนที่ผิด — ควรแก้แยกจากงานนี้
3. ถ้าเปิด `CloseFailMode.Continuous` ต้องตัดสินใจเรื่อง DOT ตอนนั้น

---

## Action Items

**ทำครบแล้ว 2026-08-10 · compile ผ่าน (`scripts/unity-check.ps1` exit 0) · ยังไม่ได้เทสต์ในเกม**

1. [x] `TetherMode` + `CloseFailMode` + `LeashScope` enum → [`TetherMode.cs`](../Assets/Script/Data/TetherMode.cs) · field ใน `BossTether` และ `TetherAction`
2. [x] แยกทาง resolve ตามโหมด — `switch (mode)` ใน `Update()` แยกช่วง "ระหว่างทาง" กับ "หมดเวลา"
3. [x] Leash — `PullBackToAnchor()` ยิง `ApplyKnockbackClientRpc` · ครบ 3 scope ใน `TetherAction.SpawnLeash()`
4. [x] guard fail-loud — `NormalizeUnimplemented()` เรียกก่อนส่ง ClientRpc เสมอ client จึงได้ mode ที่ normalize แล้ว
5. [x] ตัวกรอง `isDead` ขึ้นกับโหมด — เป้าหลักมาจาก `CollectPlayers(aliveOnly: true)` เสมอ · Close ตกไปใช้ `aliveOnly: false` เฉพาะตอนไม่มีคนเป็นเหลือ
6. [x] `anchorStartedDead` + เช็คใน `Update()` → anchor ฟื้นเมื่อไหร่ resolve เป็นสำเร็จทันที
7. [x] `BaseLineColor()` — ฟ้า Far · เขียว Close · ส้ม Leash · ใช้ทั้งสาย ประกาศ HUD และตอน setup LineRenderer
8. [x] `InitTetherClientRpc(pA, pB, duration, (int)mode, pillar)` — รวม RPC เดิมสองตัวเป็นตัวเดียว ส่ง enum เป็น `int` ตาม `TelegraphZone.InitClientRpc`
9. [x] แก้ `CLAUDE.md` — authority การเคลื่อนที่

### เพิ่มระหว่างทาง (ไม่ได้อยู่ในแผนเดิม)

**วงรัศมีของ Leash — จำเป็น ไม่ใช่ของแถม** แผนเดิมพลาดไป: Leash ใช้เสา anchor เหมือน Far-solo
แต่ `leashAnchorOffset` default = 0 แปลว่าเสาไปโผล่**ทับตัวผู้เล่นพอดี** และผู้เล่น
**มองไม่เห็นขอบรัศมี**เลย — กลไกที่ลงโทษเมื่อออกนอกวง แต่ไม่บอกว่าวงอยู่ไหน เล่นไม่ได้จริง

`SetupLeashRing()` วาดวงบนพื้นด้วย LineRenderer (`loop = true`) รัศมี `requiredDistance`
**ผลข้างเคียงที่ต้องบันทึก:** `requiredDistance` ไม่ใช่ server-only อีกต่อไป ต้องส่งไปกับ ClientRpc
ไม่งั้นวงจะวาดด้วยค่า prefab — **บั๊กตระกูลเดียวกับปัญหา 1 ใน `boss-tether-findings.md` เป๊ะๆ**

อื่นๆ

- `HasTimeoutPenalty()` — Leash ไม่กระพริบแดงตอนใกล้หมดเวลา เพราะหมดเวลาแล้วไม่มีอะไรเกิดขึ้น การกระพริบจะหลอกผู้เล่น
- `ResolveBreakDistance()` จำกัดให้มีผลเฉพาะ Far — Close/Leash การเริ่มห่างไม่ใช่การได้เปรียบ ไม่ต้องชดเชย
- `DealFailDamage` / `DealSoloFailDamage` ยุบเป็น `Fail(reason)` ตัวเดียว · `TetherBrokenClientRpc` เปลี่ยนชื่อเป็น `TetherSucceededClientRpc` (Leash "สำเร็จ" โดยไม่มีอะไรขาด)

---

## ต้องเทสต์ด้วยมือ (compile จับไม่ได้สักข้อ)

| ทำอะไร | ต้องเห็นอะไร |
|---|---|
| Close 2 คน ยืนใกล้กันจนหมดเวลา | รอดทั้งคู่ ไม่มีดาเมจ |
| Close 2 คน แยกกันตอนหมดเวลา | กินดาเมจก้อนเดียวทั้งคู่ ไม่ใช่ทีละ tick |
| Close ผูกกับศพเพื่อน | คนเป็นเดินไปยืนข้างศพแล้วรอด |
| **Close ผูกกับศพ แล้วศพ respawn กลางคัน** | **ต้องไม่แพ้ฟรี** (Action item 6) |
| Leash ทั้ง 3 scope | ออกนอกรัศมีแล้วถูกดึงกลับ ไม่กินดาเมจ ไม่ยางยืด |
| **Leash — ดูวงบนพื้น** | **ขนาดวงต้องตรงกับ `tetherDistance` ใน asset ไม่ใช่ค่า prefab** · ดูจากจอ client |
| Leash — จังหวะที่ถูกดึง | ยังขยับเองได้บ้างระหว่างการดึงแต่ละครั้ง ไม่ใช่ล็อคยาว (ปรับ `leashPullDuration` / `leashPullInterval`) |
| Leash แล้วมี AoE ลงในรัศมีพร้อมกัน | ออกไปหลบชั่วคราวได้ ไม่ติดกับดัก |
| เลือก `Transferable` ใน Inspector | **เห็น warning ใน Console** แล้ว fallback เป็น Far ไม่ใช่เงียบ |
| ดูจากจอ client ทุกโหมด | สีและข้อความตรงกับโหมด · เวลาถูกต้อง (ไม่ใช่ค่า prefab) |
