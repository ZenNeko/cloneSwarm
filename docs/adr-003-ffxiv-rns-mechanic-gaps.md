# ADR-003: ปิดช่องว่างกลไกบอสให้ใกล้ FF14 / Rabbit and Steel

**Status:** Proposed — ยังไม่ได้เขียนโค้ดสักบรรทัด
**Date:** 2026-08-10
**Deciders:** เจ้าของโปรเจกต์
**คำถามตั้งต้น:** "ถ้าอยากออกบอสให้ใกล้ FF14 กับ Rabbit and Steel ขาดอะไรบ้าง"

> ทุกข้อยืนยันจากไฟล์จริงและ asset จริงแล้ว
> ต่อจาก [ADR-002](adr-002-tether-modes.md) (tether 4 โหมด — ทำเสร็จแล้ว)

**ขอบเขต:** เอาทุกข้อจากผลตรวจ **ยกเว้น** กระสุนบอส (bullet pattern) และการเรียกลูกน้อง (adds)
สองข้อนั้นเป็นระบบใหม่ ไม่ใช่ส่วนขยาย ควรเป็นงานของตัวเอง

---

## Context

### ฐานที่มีอยู่แข็งกว่าที่คิด

| หมวด | มีแล้ว |
|---|---|
| รูปทรง | Circle · Line · Cross · Donut |
| ตัวปรับ | Chase · RotatingChase · Stack (หารดาเมจ) · Gaze (เช็ค dot product กับทิศที่หันจริง) · ColorMatch · Knockback 5 โหมด |
| Tether | Far · Close · Leash |
| Status | stack · หมดอายุ · decay · ตัวคูณดาเมจ · `onExpire` → BossAction = **Spell-in-Waiting ของ FF14 ตรงตัว** |
| เฟส | HP threshold · invuln · enrage · ประกาศ · VFX · camera shake |
| สนาม | `ArenaAnchor` 25 จุด รวมตำแหน่งนาฬิกา 1–12 |
| ประกอบท่า | Combo · Random · Timeline + Boss Designer · cast bar |

### สิ่งที่ทำให้ฐานนี้ยังออกบอสแบบ FF14/R&S ไม่ได้

**ระบบ roll สร้างเสร็จทั้งระบบแล้วไม่มีใครใช้**

```
RollDefinition  → RollKind { Variant, Anchor, SnapAngle, Target, MirrorX, MirrorZ, Order }
RollContext     → seeded RNG + excludePrevious + Peek        ← ครบ
BossController  → สร้าง context (:103) · roll ทุกครั้งที่ยิงท่า (:216)   ← ต่อแล้ว
BossAction      → protected int GetRoll(runner)               ← ไม่มีใครเรียก
```

`GetRoll` ([BossAction.cs:63](../Assets/Script/Data/BossAction.cs:63)) — grep ทั้งโปรเจกต์ได้ hit เดียวคือบรรทัดที่ประกาศมันเอง
บอสสุ่มค่าไว้ทุกท่าแล้วโยนทิ้ง ทุกรอบที่สู้ท่าออกเหมือนกันเป๊ะ

**และยังไม่มีใครใส่ข้อมูลด้วย**

```yaml
# ทุก BossEncounterConfig.asset
arena: {fileID: 0}     # null
rolls: []              # ว่าง
```

`ArenaDefinition` **ไม่มีอยู่ในโปรเจกต์เลยสักไฟล์** → `ColorMatchAoEAction` โหมด `centerMode = Arena`
ตกเข้า LogWarning fallback ทุกครั้ง ใช้งานจริงไม่ได้ · anchor 25 จุดไม่เคยถูกใช้

นี่คือแกนของทั้งสองเกมอ้างอิง — ท่าเดิมแต่พลิกด้าน หมุนมุม เปลี่ยนคนโดน
ทำให้ผู้เล่นต้อง**อ่านสถานการณ์** ไม่ใช่ท่องลำดับ ขาดข้อนี้บอสจะเป็น "จำครั้งเดียวจบ" ต่อให้มีท่าเยอะแค่ไหน

---

## Decision

### 1 · เสียบปลั๊ก Roll — แบ่งเป็นสองชั้น

**ชั้นพื้นที่ (generic)** — ทำที่ `SpawnAoEActionBase` ระหว่าง `GetSpawnPositions()` กับ `Instantiate`
ทุก AoE subclass ได้ฟรีโดยไม่ต้องแก้ทีละตัว

| RollKind | ผล |
|---|---|
| `Anchor` | เลือก `ArenaAnchor` 1 ใน N เป็นจุดเกิด |
| `SnapAngle` | หมุนทั้งชุด `360/N × value` องศารอบ pivot |
| `MirrorX` / `MirrorZ` | 1 = พลิกตำแหน่งและมุมข้าม pivot |

**pivot = จุดศูนย์กลาง arena · ไม่มี arena ให้ fallback ไปตำแหน่งบอสพร้อม warning**
ตามแบบ `ColorMatchAoEAction.ResolveSharedCenter()` ที่ทำไว้แล้ว

**roll ครั้งเดียวต่อการยิงท่า 1 ครั้ง แล้วใช้ transform เดียวกันกับทุกจุดเกิด** — ถ้า roll แยกทีละจุด
แพตเทิร์นแบบ `AllPlayers` จะกลายเป็นมั่วแทนที่จะเป็นลวดลายที่อ่านออก

**ชั้นเฉพาะท่า** — `Target` (เลือกคนโดน) · `Variant` (subclass ตีความเอง) · `Order` (ข้อ 4)

**ต้องเพิ่ม `TargetingMode.ArenaAnchor`** — ตอนนี้ AoE ธรรมดาไม่มีทางเกิดที่ anchor ได้เลย
มีแค่ `StaticCoords` ที่ใช้พิกัดดิบ · `RollKind.Anchor` จะไร้ความหมายถ้าไม่มีอันนี้ก่อน

**roll ใน Timeline / Combo** — ปัจจุบัน [BossController:214-216](../Assets/Script/BossController.cs:214) roll ให้เฉพาะ
action ระดับบนสุดในลิสต์ของเฟส · คลิปใน timeline รันผ่าน `runner.StartCoroutine` ตรงๆ **จึงไม่เคยถูก roll**

แก้โดยให้ `BossTimelineAction` / `ComboAction` roll ทุก `rollName` ที่คลิปลูกใช้ **ครั้งเดียวตอนเริ่ม**
แล้วคลิปทั้งหมด `Peek` เอา — ได้ "หนึ่งกลไก หนึ่ง roll ทุกส่วนสอดคล้องกัน" ซึ่งเป็นพฤติกรรมที่ถูก

### 2 · เพิ่มทรง Cone

`AoEType { Circle, Line, Cross, Donut, Cone }` + `ConeAoEAction`
hit test = `dist <= radius && angle(forward, player - center) <= arcAngle/2`

`NetworkedVFXPool.PlayByName(..., float arcAngle = 360f)` รองรับอยู่แล้ว ขาดแค่ฝั่ง telegraph

> หมายเหตุ: `CLAUDE.md` เขียนว่า `AoEType` มี `Chase` ด้วย — **ไม่จริง**
> ของจริงคือ `{ Circle, Line, Cross, Donut }` ส่วน Chase เป็น `bool isChasing` แยกต่างหาก · แก้เอกสารด้วย

### 3 · ยิงซ้ำเป็นชุด

`repeatCount` + `repeatInterval` + `rerollEachRepeat` บน `SpawnAoEActionBase`

ตอนนี้ "วงกลม 4 ลูกห่างกัน 1 วิ" ต้องวาง 4 คลิปเอง · `rerollEachRepeat` เปิดให้ทำ
"4 ควอดแรนต์สุ่มไม่ซ้ำ" ได้ในคลิปเดียว (คู่กับ `excludePrevious` ที่ `RollDefinition` มีอยู่แล้ว)

`GetEditorDuration()` ต้องบวก `repeatCount × repeatInterval` ไม่งั้นความยาวคลิปใน Boss Designer จะโกหก

### 4 · Limit Cut — เลขลำดับ + UI ลอยเหนือหัว

- `LimitCutAction` แจกเลข 1..N ให้ผู้เล่นที่ยังไม่ตาย (ใช้ `RollKind.Order` สลับลำดับทุกครั้ง)
- เก็บใน `playermove.limitCutNumber` เป็น `NetworkVariable<int>` (Server write · Everyone read) — แบบเดียวกับ `carriedQuestItems`
- `WorldNumberTag` component ลอยเหนือหัว — ล้อ `WorldHPBar` ที่มีอยู่แล้ว
- action เดียวกันไล่ resolve ทีละเลขตาม `perNumberDelay`

**กลไกนี้ไม่มีเลขให้เห็นก็เล่นไม่ได้เลย** — UI เป็นส่วนหนึ่งของกลไก ไม่ใช่ของแถม

### 5 · Hazard ที่ขยาย / กวาด

เพิ่มเป็น**ตัวปรับบนทรงเดิม** ไม่ใช่ทรงใหม่

- `radiusStart` → `radiusEnd` ไล่ระหว่างช่วง warning (วงขยาย / วงหด)
- `sweepDegreesPerSecond` หมุน zone ระหว่าง warning (ลำแสงกวาด)

**ข้อจำกัดที่ยอมรับ** — ดาเมจยัง resolve ครั้งเดียวตอนจบ ใช้รัศมี/มุม ณ วินาทีนั้น
"วงขยายที่ทำดาเมจตอนขอบกวาดผ่าน" ต้องใช้โมเดล damage ต่อเนื่อง ซึ่งเป็นงานคนละก้อน — **ไม่อยู่ในขอบเขตนี้**

### 6 · ตัวนับเวลาแบบ R&S

R&S ใส่วงหดบนทุกอย่างที่กำลังจะเกิด — อ่านเวลาที่เหลือได้โดยไม่ต้องละสายตาจากตัวละคร

- **tether ยังไม่มีเลย** — วงหดที่จุดกึ่งกลางสาย (ตรงตามที่ R&S ทำ)
- zone ส่ง `WarningDuration` เข้า VFX Graph อยู่แล้ว — ต้องไล่ดูว่าทุกทรงแสดงจริงและใช้ภาษาเดียวกัน

### 7 · Arena — งาน Editor เข้าขอบเขตด้วย

- `ArenaDefinitionEditor` — `SceneView.duringSceneGui` + `Handles` ลาก center/radius ได้ในหน้า Scene
  และโชว์ anchor ทั้ง 25 จุดพร้อมป้ายชื่อ (ตอนนี้ต้องเดาว่า `Clock3` กับ `QuadrantNE` อยู่ตรงไหน)
- สร้าง `ArenaDefinition` asset ตัวแรกจริง + ผูกเข้า `BossConfig_01`
- ตั้ง `RollDefinition` ชุดแรกให้เป็นตัวอย่าง

**ข้อ 1 / 2 / 5 พึ่ง arena ทั้งหมด ถ้าไม่มี arena จะเทสต์ของจริงไม่ได้**

---

## Options Considered

### Roll จะเสียบตรงไหน

| | ชั้น generic ที่ `SpawnAoEActionBase` ✅ | ให้แต่ละ subclass อ่าน `GetRoll()` เอง |
|---|---|---|
| งานที่ต้องทำ | จุดเดียว | ทุก subclass (6+ ตัว) และทุกตัวที่จะเพิ่มในอนาคต |
| ความสม่ำเสมอ | mirror/rotate ได้ผลเหมือนกันทุกทรงโดยอัตโนมัติ | แต่ละตัวตีความเอง เพี้ยนกันได้ง่าย |
| เข้ากับ `RollKind` ที่มี | `Anchor`/`SnapAngle`/`MirrorX`/`MirrorZ` **ล้วนเป็น transform เชิงพื้นที่** ชี้ชัดว่าตั้งใจให้เป็นชั้นกลาง | ไม่ตรงเจตนาเดิม |

`RollKind` ที่คนก่อนหน้าออกแบบไว้บอกคำตอบอยู่แล้ว — 4 ใน 7 ตัวเป็นการแปลงพิกัดล้วนๆ

### Hazard ที่ขยาย — ทรงใหม่ vs ตัวปรับ

เลือก**ตัวปรับ** เพราะ "วงขยาย" ไม่ใช่รูปทรงใหม่ มันคือวงกลมที่รัศมีเปลี่ยนตามเวลา
ถ้าทำเป็น `AoEType` ใหม่จะต้องทำซ้ำสำหรับ donut ที่ขยาย, cone ที่ขยาย… บานเป็น N×M

---

## Trade-off Analysis

**`InitClientRpc` จะทะลุขีดที่รับได้** — ตอนนี้ 12 พารามิเตอร์ ([TelegraphZone.cs:133](../Assets/Script/TelegraphZone.cs:133))
ADR นี้เติม `arcAngle`, `radiusEnd`, `sweepSpeed`, `detonateVfxKey` เป็น 16+

เคยบันทึกไว้ว่าการยุบเป็น `struct : INetworkSerializable` เป็น "ของ optional" — **ตอนนี้ไม่ optional แล้ว**
16 พารามิเตอร์เรียงกันคือที่ที่สลับลำดับ `float` สองตัวแล้วไม่มีอะไรเตือน เป็นบั๊กที่ compile จับไม่ได้
และหาไม่เจอจนกว่าจะเห็นวงขนาดผิดในเกม · **ทำก่อนข้อ 2 และ 5**

**สิ่งที่ยอมแลก** — ข้อ 5 ทำแค่ครึ่งเดียวของกลไกจริง (ดาเมจยัง resolve ครั้งเดียวตอนจบ)
วงขยายจะดูเหมือนขยาย แต่ปลอดภัยจนวินาทีสุดท้าย ไม่เหมือน FF14 ที่ขอบวงกวาดโดนตอนไหนก็เจ็บตอนนั้น
ยอมรับไว้ก่อนเพราะ damage ต่อเนื่องกระทบโมเดล resolve ทั้งระบบ

**สิ่งที่ไม่ยอมแลก** — ไม่ทำกลไกที่ผู้เล่นมองไม่เห็นเงื่อนไข
เป็นบทเรียนจาก Leash ใน ADR-002 ที่เกือบ ship โดยไม่มีวงบอกขอบรัศมี · ข้อ 4 (เลข) และ 6 (ตัวนับเวลา)
มาจากข้อนี้ทั้งคู่ และห้ามตัดออกเพื่อประหยัดเวลา

---

## Consequences

**ง่ายขึ้น** — เมื่อ roll ต่อแล้ว **ท่าที่มีอยู่ทั้งหมดในตอนนี้มีชีวิตขึ้นทันทีโดยไม่ต้องเพิ่มท่าใหม่สักท่า**
ท่าเดิมพลิกด้าน/หมุนมุม/สลับคนโดน ได้ความหลากหลายฟรีจากของที่เขียนไว้แล้ว

**ยากขึ้น** — ท่าจะไม่ deterministic อีกต่อไป ต้องเทสต์หลายรอบถึงจะเห็นทุก variant
`RollContext` ใช้ seed คงที่ต่อ fight จึง reproduce ได้ถ้าจด seed ไว้ — **ควรพ่น seed ลง log ตอนบอสเกิด**

**ต้องกลับมาทบทวน**

1. damage ต่อเนื่องสำหรับ hazard ที่ขยาย/กวาด
2. กระสุนบอส (bullet pattern) — ช่องว่างใหญ่สุดสำหรับ "ใกล้ R&S" ที่ตัดออกจาก ADR นี้
3. adds / summon — `EnemySpawner` กับ `WaveConfig` มีอยู่แล้ว ต่อท่อไม่ยาก

**ที่ตัดออกอย่างตั้งใจ ไม่ใช่หนี้** — FF14 ออกแบบบนสมมติฐานว่ามี interrupt / cleanse / defensive cooldown / role แยก
เกมนี้เป็น bullet-heaven roguelike ที่มีแค่ dash · กลไกตระกูล "cleanse ให้ทัน" หรือ "tank สลับ aggro"
ทำไม่ได้และไม่ควรทำ

---

## Action Items

### Stage A · เตรียมพื้น — **ทำแล้ว `8470ac2e`** · compile ผ่าน · ยังไม่ได้เทสต์ในเกม

1. [x] ยุบ `InitClientRpc` เป็น `struct TelegraphInit : INetworkSerializable` → [TelegraphInit.cs](../Assets/Script/Data/TelegraphInit.cs)
2. [x] `ArenaDefinitionEditor` — handles ลาก center/radius + โชว์ anchor 25 จุดพร้อมป้าย (ซ่อนตำแหน่งนาฬิกาได้ ไม่งั้นรก)
3. [x] สร้าง `Arena_BossPoc.asset` + ผูกเข้า `BossConfig_01` — Square · center origin · radius 38
4. [x] **รวบ `detonateVfxKey` เข้ามาด้วย** — อยู่ใน RPC เดียวกัน แยกทำจะแก้ไฟล์เดิมสองรอบ
   `SpawnAoEActionBase` / `ColorMatchAoEAction` / `KeepMovingAction` + `ExplodeClientRpc` ใช้ `PlayByName`
   (ของเดิม `Instantiate` ตรงๆ ผิด convention #2 — แก้ไปพร้อมกัน) · ว่างไว้ = ใช้ prefab เดิม asset เก่าไม่พัง

**ค่า arena มาจากสนามจริง** — `LevelLayoutBuilder` สร้างพื้น 80×80 กำแพงที่ ±40 (`float hw = 40f`)
ตั้ง radius 38 เผื่อระยะ 2 m ไม่ให้ anchor ที่ `distanceScale = 1` ไปจมในกำแพง
**ต้องเปิดดูในซีนจริงแล้วลากปรับ** — ตัวเลขนี้มาจากสคริปต์ build level ไม่ใช่จากการวัดซีนที่ใช้จริง

### Stage B · Roll

4. [ ] `TargetingMode.ArenaAnchor` ใน `SpawnAoEActionBase`
5. [ ] ชั้น transform จาก roll — `Anchor` / `SnapAngle` / `MirrorX` / `MirrorZ` · pivot = arena center + fallback บอส
6. [ ] `RollKind.Target` ใน `TargetingMode.RandomPlayer`
7. [ ] `BossTimelineAction` / `ComboAction` roll ให้คลิปลูกครั้งเดียวตอนเริ่ม แล้วคลิป `Peek`
8. [ ] `BossController` พ่น roll seed ลง log ตอนบอสเกิด
9. [ ] ตั้ง `RollDefinition` ชุดแรกใน `BossConfig_01` เป็นตัวอย่าง
10. [ ] **แก้ `TargetingMode.RandomPlayer` ให้กรอง `isDead`** — ตอนนี้ไม่กรอง ([:124-146](../Assets/Script/Data/SpawnAoEActionBase.cs:124)) ขณะที่ `AllPlayers` กรอง ([:169](../Assets/Script/Data/SpawnAoEActionBase.cs:169)) · บั๊กตระกูลเดียวกับ tether

### Stage C · ท่าและตัวปรับใหม่

11. [ ] `AoEType.Cone` + `ConeAoEAction` + hit test เชิงมุม
12. [ ] `repeatCount` / `repeatInterval` / `rerollEachRepeat` + แก้ `GetEditorDuration()`
13. [ ] `radiusStart` → `radiusEnd` และ `sweepDegreesPerSecond`

### Stage D · การมองเห็น

14. [ ] `playermove.limitCutNumber` + `WorldNumberTag` (ล้อ `WorldHPBar`)
15. [ ] `LimitCutAction` — แจกเลขด้วย `RollKind.Order` + ไล่ resolve ตาม `perNumberDelay`
16. [ ] วงนับเวลาที่จุดกึ่งกลางสาย tether
17. [ ] ไล่ดูว่าทุก AoEType แสดงตัวนับเวลาด้วยภาษาเดียวกัน

### Stage E · เอกสาร

18. [ ] `CLAUDE.md` — `AoEType` ไม่มี `Chase` · เพิ่มกฎ roll (roll ที่ระดับบนสุด คลิปลูก Peek)

---

## ต้องเทสต์ด้วยมือ (compile จับไม่ได้สักข้อ)

| ทำอะไร | ต้องเห็นอะไร |
|---|---|
| สู้บอสตัวเดิม 3 รอบ | ท่าเดียวกันออกคนละด้าน/คนละมุม **ไม่ซ้ำเป๊ะทุกรอบ** |
| จด seed แล้วสู้ซ้ำด้วย seed เดิม | ออกเหมือนเดิมทุกประการ (reproduce ได้) |
| ท่า `AllPlayers` + `MirrorX` | ทั้งชุดพลิกพร้อมกัน **ไม่ใช่แต่ละวงพลิกเอง** |
| timeline ที่คลิปหลายตัวใช้ `rollName` เดียวกัน | ทุกคลิปได้ค่าเดียวกัน แพตเทิร์นสอดคล้อง |
| ลบ arena ออกจาก config แล้วยิงท่าที่ใช้ `MirrorX` | **เห็น warning** แล้ว fallback ไปตำแหน่งบอส ไม่ใช่ไปโผล่ที่ world origin เงียบๆ |
| Cone | มุมและรัศมีตรงกับที่ตั้ง · ยืนนอกมุมแต่ในรัศมีต้องปลอดภัย |
| `repeatCount = 4` + `rerollEachRepeat` | เกิด 4 ครั้งคนละที่ ไม่ซ้ำติดกัน · ความยาวคลิปใน Boss Designer ตรงกับของจริง |
| Limit Cut 4 คน | **เห็นเลขบนหัวทุกคนรวมคนอื่น** แล้วระเบิดไล่ตามเลข |
| ผู้เล่นตายก่อนท่า `RandomPlayer` ลง | ไม่สุ่มไปโดนศพ |
