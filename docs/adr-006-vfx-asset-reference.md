# ADR-006: เปลี่ยน VFX key จากสตริงเป็น `VFXAsset` ที่ลากใส่ได้

**Status:** Accepted — ยังไม่ลงมือ
**Date:** 2026-08-13
**Deciders:** เจ้าของโปรเจกต์
**เกี่ยวข้อง:** [ADR-005](adr-005-telegraph-architecture-review.md) หนี้ข้อ 1 (สัญญาเป็นสตริง พังเงียบ)

---

## Context

VFX ถูกเรียกด้วย `NetworkedVFXPool.PlayByName(string key, ...)` · ตรวจ call site ทั้งหมดแล้ว 7 จุด
มีสตริงดิบในโค้ดตัวเดียวคือ `"EnemyDeath"` ที่เหลือ key มาจากช่องที่ **designer พิมพ์เองใน asset**

> **แก้ 2026-08-15 — การสำรวจข้างบนนับพลาด สตริงดิบมี 8 จุด ไม่ใช่ 1**
>
> สาเหตุ: ไล่เฉพาะ call site ของ `PlayByName` แต่ส่วนใหญ่เรียกผ่าน `VFXFactory.Play`
> ซึ่งเป็นชั้นบางๆ ที่เรียก `PlayByName` ต่ออีกทีหนึ่ง จึงหลุดจากการสำรวจทั้งหมด
>
> | ไฟล์ | คีย์ |
> |---|---|
> | `Enemy.cs` | `EnemyDeath` (ตัวเดียวที่ ADR รู้ · ตอนนี้เป็น fallback หลัง migrate แล้ว) |
> | `GrenadeProjectile.cs` | `GrenadeExplosion` (เป็นค่าตั้งต้นของ `explosionVfxKey` แล้ว) |
> | `MineObject.cs` · `MissileProjectile.cs` | `GrenadeExplosion` |
> | `ObjectiveOrb.cs` · `VFX/OrbVisual.cs` | `OrbPickup` |
> | `ZoneObjective.cs` | `None` · `EnemyDeath` |
>
> เป้าหมายของ ADR นี้คือทำให้ "พิมพ์คีย์ผิดแล้วตายเงียบ" เป็นไปไม่ได้ แต่เก็บได้จริงแค่ 1 ใน 8
> **งานยังไม่จบตามที่ Action Items ติ๊กไว้** — ที่เหลือรอรอบถัดไป
>
> `ZoneObjective.cs` เรียก `Play("None")` ซึ่ง `PlayByName` ตีความว่าไม่ต้องเล่นแล้ว return ทันที
> อ่านจากโค้ดอย่างเดียวแยกไม่ออกว่าตั้งใจให้เป็น no-op หรือลืมใส่คีย์จริง

| asset | ช่อง |
|---|---|
| `SpawnAoEActionBase` | `detonateVfxKey` |
| `BossPhase` | `phaseVfxName` |
| `AugmentData` | `vfxKeyOnTrigger` |

พิมพ์ผิด = ไม่มีอะไรเกิดขึ้น **ไม่มี error** · compiler ช่วยไม่ได้เพราะค่าไม่ได้อยู่ในโค้ด
เป็นตระกูลเดียวกับบั๊ก `_SweepAmount` ที่ตายเงียบอยู่หลายเดือน

แผนบอสที่กำลังจะมามี **3 ธาตุ (ไฟ/น้ำแข็ง/สายฟ้า) × หลายความยาก** ซึ่งจะทำให้จำนวน key โตเร็ว

---

## Decision

เปลี่ยนช่อง VFX จาก `string` เป็น **object reference ไปยัง `VFXAsset`** (ScriptableObject ตัวละ VFX)

```
detonateVfxKey : string   →   detonateVfx : VFXAsset
```

**ทำครั้งเดียวจบ ไม่ทำ dropdown เป็นขั้นกลาง** — เจ้าของตัดสินแล้ว

---

## ทำไมไม่ใช้ prefab ตรงๆ

เคยพิจารณาแล้วและตกไป ด้วยเหตุผลเชิงโครงสร้างสองข้อ

**ส่งข้ามเน็ตเวิร์กไม่ได้** — `detonateVfxKey` เดินทางใน `TelegraphInit` ไปบอกทุก client ว่าเล่นอะไร
เพราะ **zone ไม่รู้ว่า action ไหนสร้างมัน** (action เซ็ตค่าให้แล้วจบ) · prefab reference ส่งไม่ได้
และ instance id ของ prefab ไม่ตรงกันข้ามเครื่อง จึง pool by prefab ก็ไม่รอด — ต้องมีทะเบียนอยู่ดี

**เสีย pool** — `NetworkedVFXPool` จองล่วงหน้าตาม `poolSize` และมี recursion guard กัน stack overflow
แบบ DeathField · ไป `Instantiate` ตรงจะเสียทั้งคู่ และผิดกฎข้อ 2 ใน `CLAUDE.md`
(เส้นทางนั้นมีอยู่จริงคือ `TelegraphZone.detonateVfxPrefab` แต่เป็นของเก่าที่เก็บไว้เพื่อ backward compat)

---

## แผนลงมือ — เรียงตามลำดับที่ปลอดภัย

### 1 · สร้าง `VFXAsset`

ยก field ทั้งหมดจาก `VFXDatabase.VFXEntry` มาเป็น ScriptableObject ตัวละไฟล์
`prefab` · `poolSize` · `designedRadius` · `fixedDuration` · **`category`** (UI / Boss / Enemy / Weapon / Environment)

`category` เป็นของใหม่ที่ตกลงกันไว้ ใช้ได้สองอย่าง: จัดกลุ่มตอนเลือก และ**โหลดเฉพาะกลุ่มของบอสที่จะเจอ**
แทนการ allocate ทั้งเกม ซึ่งจะสำคัญตอนขึ้นเครื่องจริง

### 2 · id ที่เสถียรข้ามเครื่อง

`VFXAsset` ต้องมี id ที่**ทุก client เห็นตรงกัน** · ห้ามใช้ `GetInstanceID()`
ทางที่ตรงไปตรงมาคือให้ `VFXDatabase` ถือ `List<VFXAsset>` แล้ว **index ในลิสต์คือ id**
(ลิสต์เดียวกันอยู่ในบิลด์ทุกเครื่อง) · เพิ่ม validator ว่าไม่มีช่องว่างและไม่ซ้ำ

### 3 · เปลี่ยน `TelegraphInit`

`FixedString32Bytes detonateVfxKey` → `int detonateVfxId`
**ประหยัด bandwidth ด้วย** — จาก 32 ไบต์เหลือ 4

`-1` = ไม่มี VFX (แทน string ว่างเดิม)

### 4 · เปลี่ยนช่องใน asset ทั้งสามที่

`SpawnAoEActionBase` · `BossPhase` · `AugmentData`
· `TelegraphZone.detonateVfxKey` ก็เปลี่ยนตาม

> **แก้ 2026-08-13** — `detonateVfxKey` ไม่ได้มีที่เดียว · 3 action ที่ข้าม base (หนี้ข้อ 3 ใน ADR-005)
> ถือ field ของตัวเองซ้ำอีก 3 ชุด: `ColorMatchAoEAction:25` `KeepMovingAction:20` `LimitCutAction:30`
> **จึงยุบ 3 action เข้า base ก่อน** แล้วขั้นนี้จะเหลือแก้จุดเดียวจริงตามที่เขียนไว้เดิม

### 5 · migrate ค่าเดิม

เขียน editor script วนทุก asset อ่านสตริงเดิม หา `VFXAsset` ที่ key ตรงกัน แล้วเซ็ต reference ให้
· **ถ้าหาไม่เจอต้อง `LogError` พร้อมชื่อ asset** ไม่ใช่ปล่อยว่างเงียบ — จุดนี้คือที่เดียวที่จะรู้ว่า
มี key พิมพ์ผิดค้างอยู่ในโปรเจกต์กี่ตัว ซึ่งไม่มีใครเคยรู้มาก่อน

### 6 · `"EnemyDeath"` ที่ hardcode

`Enemy.cs:592` เป็นสตริงดิบตัวเดียวที่เหลือ · เปลี่ยนเป็นช่อง `VFXAsset` บน `Enemy`

---

## Consequences

**ง่ายขึ้น** — ลากใส่ได้เลยไม่ต้องพิมพ์และไม่ต้องไปลงทะเบียนก่อน · เปลี่ยนชื่อ VFX แล้วไม่พัง
· กด Find References หาได้ว่าใครใช้อยู่ · สถานะ "key ผิด" เป็นไปไม่ได้อีก

**ยากขึ้น** — asset เยอะขึ้นตัวละไฟล์ · ต้องดูแล id ให้เสถียร (validator ช่วย)

**ต้องกลับมาทบทวน** — ถ้าจำนวน VFX โตจนการ pre-allocate ทุกตัวกินหน่วยความจำเกินรับ
ให้ทำ lazy pool + preload ตาม `category` ซึ่ง ADR นี้วางทางไว้ให้แล้ว

---

## สิ่งที่ตัดสินไปแล้วและไม่ต้องรื้อ

**ขนาด AoE ที่ต่างตามความยาก ไม่ใช่ VFX ตัวใหม่** — `PlayByName` รับ `scale` อยู่แล้วและ
`ExplodeClientRpc` ส่งให้จาก `radius` จริง · ทำเป็น entry แยกจะได้ 45 ตัวแทนที่จะเป็น 9–15

**ธาตุห้ามแย่งช่องสีกับหมวดกลไก** — สี telegraph บอก *ต้องทำอะไรถึงรอด* (Gaze ม่วง / Stack ฟ้า / Chase ชมพู)
ธาตุบอก *มันคืออะไร* ให้ไปอยู่ที่ VFX กับอนุภาค · ถ้าให้ธาตุชนะ ผู้เล่นจะตายโดยไม่รู้ว่าต้องหันหลัง
ถ้าอยากให้ธาตุมีสีจริงๆ ต้องเป็นมิติที่สอง เช่นสีขอบ ซึ่งแยก gate ไว้แล้วตั้งแต่ 2026-08-13

**ยังไม่ได้ทำ: พารามิเตอร์สีใน `PlayByName`** — ถ้าไม่มี จะเริ่มมีคนสร้าง `Detonate_Fire_Purple`
แยกจาก `Detonate_Fire_Blue` ซึ่งทำให้ asset ระเบิดเป็นทวีคูณ · ควรทำพร้อมกันในรอบนี้

---

## Action Items

**ทำครบทั้งหมดแล้ว 2026-08-13** · คอมไพล์ผ่าน 0 error · migration รันจริงแล้ว

1. [x] `VFXAsset` ScriptableObject + `category`
2. [x] `VFXDatabase` ถือ `List<VFXAsset>` · index = id · validator กันว่าง/ซ้ำ
3. [x] `TelegraphInit` สตริง → `int` (`FixedString32Bytes` → `int`, -1 = ไม่มี)
4. [x] เปลี่ยนช่องใน `SpawnAoEActionBase` / `BossPhase` / `AugmentData` / `TelegraphZone`
5. [x] editor script migrate ค่าเดิม — **หาไม่เจอต้อง LogError**
6. [x] `Enemy` เลิก hardcode `"EnemyDeath"` (มี fallback + warning ครั้งเดียว จนกว่าจะเติม prefab)
6b. [ ] **สตริงดิบอีก 7 จุดที่การสำรวจเดิมนับพลาด** — ทั้งหมดเรียกผ่าน `VFXFactory.Play` ดู §Context
7. [x] เพิ่มพารามิเตอร์สีใน `PlayByName` (ทำพร้อมกันเพื่อกัน asset ระเบิด)

---

## ผลการ migrate จริง — คำตอบของคำถามที่ค้างมานาน

ADR นี้เขียนไว้ว่าขั้น migrate "คือที่เดียวที่จะรู้ว่ามี key พิมพ์ผิดค้างอยู่ในโปรเจกต์กี่ตัว
ซึ่งไม่มีใครเคยรู้มาก่อน" · รันแล้วได้คำตอบ:

```
resolved 2 · unresolved 0 · empty 16   (21 ไฟล์ที่ตรวจ)
```

**ไม่มี key พิมพ์ผิดเลยสักตัว** · ค่าที่ไม่ว่างในโปรเจกต์ทั้งหมดมีแค่ 2 จุด คือ
`BossConfig_01.asset` phase 2 กับ 3 ที่ชี้ `PhaseShockwave` ทั้งคู่ — แปลงสำเร็จทั้งคู่

ความเสี่ยงของงานนี้จึงต่ำกว่าที่ ADR ประเมินไว้มาก **แต่นั่นเป็นเพราะยังไม่มีใครใส่ค่า
ไม่ใช่เพราะระบบเดิมปลอดภัย** — ช่อง `detonateVfxKey` ว่างอยู่ 16 จุดแปลว่าทุก AoE
ยังระเบิดหน้าตาเหมือนกันหมด ซึ่งเป็นสิ่งที่ ADR-007 §4 เตือนไว้ว่าเป็นงาน Editor ที่ค้าง

## ยังต้องให้ designer ทำต่อใน Editor

- `VFXAsset.category` ทั้ง 23 ตัว — ตอนนี้เป็นค่าที่สคริปต์เดาจากชื่อ key
- `Enemy.deathVfx` ว่างบนทุก prefab — ยังทำงานผ่าน fallback แต่มี warning
- `detonateVfx` ว่าง 16 จุด — ทุก AoE ระเบิดหน้าตาเดียวกันจนกว่าจะตั้ง
