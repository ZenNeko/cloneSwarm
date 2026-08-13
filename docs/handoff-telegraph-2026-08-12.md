# ส่งต่อ — ระบบ Boss / Telegraph · 2026-08-12

> เขียนไว้ให้เซสชันถัดไปอ่านต่อ · ทุกข้อยืนยันจากไฟล์จริงและ git แล้ว
> ฐาน: `0c60bf7d` · branch `ux-ui-flow-2026-08`

---

## 1 · ทำอะไรไปบ้าง (6 commit)

| commit | เรื่อง |
|---|---|
| `f280c167` | BossTether 4 โหมด (Far/Close/Leash/Transferable) |
| `8470ac2e` | ADR-003 Stage A — `TelegraphInit` struct · VFX ระเบิดต่อ action · arena ตัวแรก |
| `f5591838` | ADR-003 Stage B–E — roll · cone · repeat · limit cut |
| `2f0cc1ad` | Boss Designer สร้าง action ในหน้าต่างได้ · palette สี telegraph ตามหมวดกลไก |
| `0c60bf7d` | outline หน่วยเมตร · pulsating rings · ปุ่มความแรงต่อเอฟเฟกต์ |

เอกสารอ้างอิง — [ADR-002](adr-002-tether-modes.md) · [ADR-003](adr-003-ffxiv-rns-mechanic-gaps.md) ·
[ADR-004 (Rejected)](adr-004-telegraph-sdf-quad.md) · [boss-tether-findings](boss-tether-findings.md)

---

## 2 · สถานะการเทสต์ — สำคัญที่สุด

**เจ้าของโปรเจกต์เล่นจริงไปเกือบหมดแล้ว** ยกเว้นสองข้อ

| ยังไม่ยืนยัน | ทำไมสำคัญ |
|---|---|
| **roll** | ต้อง**ผูก `rollName` ให้ action ก่อน** ถึงจะเห็นผล · ถ้าไม่ผูก งาน roll ทั้งก้อนไม่มีผลอะไรเลย · `BossConfig_01` มี roll ให้ใช้แล้ว 5 ตัว: `mirror` `spin` `quadrant` `victim` `order` |
| **cone** | ท่าใหม่ ยังไม่มี asset ที่ใช้จริง |

> เซสชันก่อนเขียนว่า "ยังไม่ได้เทสต์สักข้อ" ซ้ำหลายรอบ — **ผิด** อย่าเชื่อข้อความเก่า

---

## 3 · บั๊กที่รู้แล้วยังไม่แก้

**`innerRadius` ของโดนัทไม่มีผลกับภาพ** — *ต้นเหตุลึกกว่าที่เคยเขียนไว้ (ตรวจซ้ำ 2026-08-12)*

เดิมสรุปไว้ว่า "โค้ดส่งค่าเข้า `VisualEffect` แต่ prefab ไม่มี `VisualEffect` สักตัว" — **จริงแต่ไม่ครบ**
`AoEType_Donut.prefab` ใช้ mesh `SM_Primitive_Torus_04.fbx` ซึ่งเป็น **torus** ทั้งรูตรงกลาง
และความหนาแถบถูกอบมากับ mesh · ต่อให้ต่อ `VisualEffect` หรือดันค่าเข้า shader ก็ยังขยับไม่ได้

และไม่ใช่แค่รูที่เพี้ยน — `IsInDonut` คิดอันตรายเป็น **annulus ตัน** ตั้งแต่ `innerRadius` ถึง `radius`
แต่ภาพที่เห็นเป็นแถบวงบางๆ ของ torus · **พื้นที่อันตรายจริงกว้างกว่าที่วาดมาก**

**เจ้าของโปรเจกต์ตัดสินแล้วว่ายังไม่แก้** (2026-08-12) · ทางเลือกที่ประเมินไว้ถ้าจะกลับมาทำ:
| ทาง | ได้ | เสีย |
|---|---|---|
| Donut ใช้ `circlePrefab` (จานแบน) + เพิ่ม `_InnerRadius` ใน shader ให้ alpha ตรงกลางหาย | รูตรงกับ hit test เป๊ะทุกอัตราส่วน | ต้องแก้ shader graph · เสียหน้าตา torus 3D |
| วัดอัตราส่วน torus แล้วบังคับ `innerRadius = radius × ratio` ฝั่ง server | เกือบไม่ต้องเขียนโค้ด | designer คุมความหนาโดนัทไม่ได้อีก |

~~**ผนังข้างของ `AoEType_Line` แดงทั้งแผง**~~ — **หายไปแล้ว 2026-08-12**
เป็นกล่องทึบ ที่ผิว ±X/±Z ระยะถึงขอบ = 0 พอดี จึงติดแถบขอบเต็มผืน · แก้ด้วยการทำทุกทรง
ให้แบนติดพื้น (`DecalThickness`) ผนังข้างจึงเหลือ 0.02 เมตรและมองไม่เห็นจากมุมกล้อง
· **ไม่ใช่งาน Editor อย่างที่เคยเขียน** — เป็นงานโค้ด เพราะเดิม `TrySpawnVfxPrefab`
ทับ `localScale` ทั้งเวกเตอร์ ค่า y ที่ตั้งบน prefab จึงไม่เคยมีผล

### เจอเพิ่มตอนตรวจ 2026-08-12 — ไม่ใช่บั๊ก แต่ควรรู้

**telegraph prefab ทั้งสามตัวแบก trigger collider มาด้วย**
`AoEType_Circle` = CapsuleCollider · `AoEType_Line` = BoxCollider · `AoEType_Donut` = MeshCollider (convex)
ทุกตัว `m_IsTrigger: 1` บน layer 0 (Default) · **ตรวจแล้วว่าไม่กระทบดาเมจ** —
`OverlapEnemy` กรองด้วย layer/tag `Enemy` และ `FireRaycastServerRpc` ใช้ `LayerMask.GetMask("Enemy")`
เป็นแค่ของหนักเปล่าๆ ที่ spawn ตามทุก telegraph · ลบทิ้งได้ถ้าจะเก็บกวาด

**`AoEType_Donut` วาง mesh เยื้องศูนย์ `x = -3.705`**
ไม่มีผลตอนรัน เพราะ `TrySpawnVfxPrefab` เซ็ต `localPosition = Vector3.zero` ทับอยู่แล้ว
แต่เปิดดูใน Editor จะเห็นเยื้อง — งงได้ตอนไปแก้ prefab

**มี player prefab สองตัว** — `player.prefab` (592d55c3…) คือตัวจริง ผูกอยู่ที่ช่อง `PlayerPrefab`
ของ `Prefab/manager/NetworkManager .prefab` · ส่วน `player 1.prefab` (48ad08ec…) ลงทะเบียนใน
`DefaultNetworkPrefabs.asset` เฉยๆ ไม่มีใครอ้างถึง · **งาน Editor ต่อ player ต้องลงที่ตัวแรก**

---

## 4 · งาน Editor ที่ยังต้องทำด้วยมือ

| ทำอะไร | ถ้าไม่ทำ |
|---|---|
| **ผูก `rollName` ให้ action** | ท่าออกเหมือนเดิมทุกรอบ งาน roll สูญเปล่า |
| ~~ลาก `WorldNumberTag` ลง player prefab~~ | **ทำแล้ว 2026-08-12** — ใส่ผ่าน YAML ตอน Editor ปิด · อยู่บน `Assets/Prefab/player.prefab` (root, fileID `8811223344556677889`) · เหลือแค่กด Play เทสต์ |
| สร้าง asset ของ `ConeAoEAction` / `LimitCutAction` | ท่าใหม่ไม่มีใครเรียกใช้ |
| เปิด `Arena_BossPoc.asset` ดูใน Scene แล้วลากปรับ | radius 38 มาจาก `LevelLayoutBuilder` ไม่ได้วัดซีนจริง |
| ตั้ง `detonateVfxKey` ต่อ action | ทุก AoE ระเบิดหน้าตาเดียวกัน |

---

## 5 · Shader `TelegraphUniversal` — ปุ่มที่มี

| property | หน่วย | หมายเหตุ |
|---|---|---|
| `_OutlineWidth` | เมตร | แถบขอบ |
| `_RingSpacing` | เมตร | ระยะห่างวง |
| `_RingSpeed` | เมตร/วิ | ความเร็ววง · คูณ `(1 + _FillProgress)` ให้เร็วขึ้นเมื่อใกล้ระเบิด |
| `_RingWidth` | เมตร | **ความหนาวง** |
| `_RingAmount` | — | **ความสว่างวง** |
| `_BlinkAmount` | 0–1 | 0 = ปิดกระพริบ |
| `_PulseAmount` | 0–1 | 0 = ปิด pulse |
| `_OutlineColor` `_OutlineShape` `_WarningColor` `_DangerColor` `_FillProgress` `_EdgeGlow` `_EdgeStrength` `_BaseAlpha` | | |

### ปรับต่อ action ได้ครบทุกตัวแล้ว (2026-08-12)

เดิม action สั่งได้แค่ `_PulseAmount` `_BlinkAmount` `_RingAmount` `_RingSpeed` + สีพื้นสองสี
ตอนนี้ครบทั้งชุด · ช่องอยู่ที่ `SpawnAoEActionBase` → เดินทางผ่าน `TelegraphInit` → `TelegraphZone.PushShaderColors`

| กลุ่มใน Inspector | ช่อง |
|---|---|
| ความแรง | `pulseAmount` `blinkAmount` `ringAmount` |
| จังหวะ | `ringSpeed` `pulseSpeed` |
| ขนาด (เมตร) | `ringSpacing` `ringWidth` `outlineWidth` |
| ขอบ / ความทึบ | `edgeGlow` `edgeStrength` `baseAlpha` |
| สี | `telegraphWarningColor` `telegraphDangerColor` + **`telegraphOutlineColor`** (gate แยก) |

### ค่ากลางย้ายมาอยู่บน `TelegraphZone` แล้ว (2026-08-12, รอบสอง)

เดิมค่ากลางของ ring/ขอบอยู่บน `Mat_Tele_Universal` และ `TelegraphZone` จะ**ไม่แตะ shader เลย**
ถ้า action ไม่ได้ override · ตอนนี้ย้ายมาเป็นช่องบน prefab `TelegraphZone` ใต้หัวข้อ
**`Telegraph Rings / Edge`** ล้อโครงเดียวกับ `Telegraph Colors` ที่อยู่เหนือมัน

```
ค่ากลาง = ช่องบน TelegraphZone prefab
override ต่อท่า = overrideTelegraphEffects บน action SO  (ชนะค่ากลาง)
เลือกด้วย TelegraphZone.Fx()  ← คู่กับ ResolveColors()
```

**สี ring ไล่ warning→danger แล้ว** — เดิมขอบกับ ring เป็นสีอันตรายค้างตั้งแต่วินาทีแรก
เพราะ `PushShaderColors` เซ็ต `_OutlineColor = danger` ครั้งเดียวจบ · ตอนนี้ `ResolveRingColor(progress)`
ไล่สีทุกเฟรมใน `ApplyTelegraphState` ลำดับ **action สั่งสีตายตัว > ตามหมวดกลไก > คู่สีกลางบน prefab**

ค่าตั้งต้นคือ `ringFollowsCategory = true` (ตามหมวด) ไม่ใช่คู่สีคงที่ — เพราะขอบเป็นตัวบอกว่า
ท่านี้หมวดไหน (Gaze ม่วง · Stack ฟ้า · Chase ชมพู) บังคับสีเดียวทั้งเกมแล้วสัญญาณนั้นหาย
· ปิดสวิตช์เมื่อไหร่ค่อยใช้ `ringWarningColor` / `ringDangerColor`

> **ข้อจำกัดที่ยังอยู่** — shader ย้อม **ring กับแถบขอบด้วย `_OutlineColor` ตัวเดียวกัน**
> (ตรวจจากกราฟ: มันแตกไปทั้ง `Lerp` ของขอบ และสาย `Multiply → Multiply(_RingAmount)` ของ ring)
> จึงตั้งสี ring ให้ต่างจากขอบ**ไม่ได้** · ถ้าอยากแยกจริงต้องเพิ่ม `_RingWarningColor`/`_RingDangerColor`
> ในกราฟแล้วต่อเข้าสาย ring อย่างเดียว — งานก้อนเดียวกับ §10 ทำพร้อมกันได้

> **กับดัก — `_Ring*` / `_Edge*` / `_BaseAlpha` บน `Mat_Tele_Universal` กลายเป็นค่าตายแล้ว**
> `PushShaderColors` ดันค่าทับ**ทุกครั้ง** ปรับที่ material ต่อไปนี้จะไม่เห็นอะไรเปลี่ยนเลย และ
> **ไม่มี error เตือน** — เป็นอาการเดียวกับ `_SweepAmount` ที่กำพร้าอยู่หลายเดือนโดยไม่มีใครรู้
> ค่า default บน prefab คัดมาจาก material ตอนย้าย ของที่เห็นวันนี้จึงไม่เปลี่ยน
> ถ้าจะเก็บกวาด ลบสี่ช่องนั้นออกจาก `.mat` ได้ แต่เก็บไว้เป็นบันทึกว่าค่าเดิมคืออะไรก็ได้เหมือนกัน

**กติกาที่ยึด** — ค่า default ทุกช่องตรงกับ `Mat_Tele_Universal` เพื่อให้ติ๊กเปิด `overrideTelegraphEffects`
แล้วหน้าตายังเหมือนเดิมจนกว่าจะลงมือปรับ · ถ้าไม่ทำแบบนี้ การเปิดเพื่อแก้ค่าเดียวจะเผลอรีเซ็ตอีกสิบค่า
· **ข้อยกเว้น** asset ที่มีก่อนวันนี้เก็บ `ringAmount: 1` / `ringSpeed: 2` ไว้แล้ว (default เดิมของโค้ด)
Unity ไม่เขียนทับให้ — เปิด override บน asset เก่าต้องตั้งเป็น `3` / `1` เอง

**สีขอบเป็นคนละ gate กับสีพื้น** (`overrideTelegraphOutlineColor`) — ปิดไว้ = ขอบใช้สีอันตรายตามหมวด
เหมือนเดิม ตามกฎ "สีบอกความหมาย" ใน §7 · ถ้ารวม gate เดียวกัน การทับสีพื้นจะพ่วงรีเซ็ตสีขอบไปด้วย

**ยังไม่ครอบคลุมสามท่า** — `ColorMatchAoEAction` · `KeepMovingAction` · `LimitCutAction`
สืบทอด `BossAction` ตรงๆ ไม่ผ่าน `SpawnAoEActionBase` และ spawn zone เอง จึงไม่มีช่องพวกนี้
zone ของมันใช้ palette + material ตามเดิม · ถ้าจะเพิ่มให้ **อย่าให้กลุ่มสีกับ `ColorMatchAoEAction`**
เพราะสีคือเงื่อนไขของกลไกนั้น การเปิดช่องทับสีจะตีกับตัวกลไกเอง

**หนี้ชื่อ `_Sweep*` — เก็บแล้ว (2026-08-12)** · ทำจริงคือเปลี่ยน **reference name** ด้วย
ไม่ใช่แค่ `displayName` อย่างที่เสนอไว้เดิม · `_SweepWidth → _RingWidth` · `_SweepAmount → _RingAmount`

ผลพลอยได้ที่ตามมาแล้วแก้ไปพร้อมกัน — จดไว้เพราะทั้งคู่พังแบบ**เงียบ** ไม่มี error:

| อาการ | แก้ที่ |
|---|---|
| `.mat` ยังถือ `_SweepAmount: 3` / `_SweepWidth: 0.15` เป็นค่ากำพร้า → shader ใช้ default (1.0 / 0.12) แทนค่าที่จูนไว้ | ย้ายค่าเดิมมาเป็น `_RingAmount: 3` / `_RingWidth: 0.15` |
| `TelegraphZone.PushShaderColors` ยังเขียน `_SweepAmount` → `HasProperty` เป็น false → **ปุ่มความแรงวงต่อ action ไม่ทำงานเลย** | เขียน `_RingAmount` แทน |

ลบ `_ConeAngle` กับ `_InnerRadius` ที่ค้างใน `.mat` ออกด้วยแล้ว — shader ไม่เคยมีสอง property นี้

---

## 6 · แก้ shader ผ่าน CLI ได้ — พิสูจน์แล้ว

`.shadergraph` แก้ผ่าน batchmode ได้จริง ใช้ reflection บน internal API

```
MultiJson.Deserialize<GraphData>(instance, json)   // round-trip ไม่เสียข้อมูล
MultiJson.Serialize(graph)
GraphData.AddNode / Connect / RemoveEdge / RemoveElements / AddGraphInput
AbstractMaterialNode.GetSlotReference(int)          // ใช้แทน SlotReference ที่หา type ไม่เจอ
```

**slot id** — Multiply/Subtract/Minimum/Divide: in 0,1 out 2 · Absolute/Length/OneMinus/Saturate/Fraction: in 0 out 1
Split: in 0, out R=1 G=2 B=3 A=4 · **Object: Position=0 Scale=1** · Lerp: a=0 b=1 t=2 out=3

**กับดัก** — slot ของ Multiply เป็น dynamic เก็บค่าคงที่เป็น `Matrix4x4` (แถวแรก) ไม่ใช่ float
· `Vector1ShaderProperty` อยู่ใน namespace `Internal`

**ขั้นตอนที่ใช้ได้ผล** — สำรองไฟล์ → รันสคริปต์ → **ตรวจ edge จากไฟล์จริง ไม่เชื่อ log** → `unity-check.ps1`
· backup อยู่ที่ `scratch/sgbackup/` (4 จุด)

**ต้องปิด Unity Editor ก่อนรัน batchmode** — เจอปัญหานี้ซ้ำหลายรอบ เช็คด้วย
`scripts/unity-check.ps1 -DryRun` ก่อนเสมอ

---

## 7 · กฎที่ยึดตลอดเซสชัน

**สีบอกความหมาย** — `ResolveColors()` เรียง ColorMatch > override ต่อท่า > Gaze > Stack > Chase > default
ตามหลัก "ทำตามสัญชาตญาณปกติแล้วตายแค่ไหน" · zone ที่ติดหลายหมวดจะ warn

**ห้ามมีกลไกที่ผู้เล่นมองไม่เห็นเงื่อนไข** — Leash ต้องมีวงบอกรัศมี · Limit Cut ต้องมีเลข
· โหมดที่ยังไม่ทำต้อง warn + fallback ไม่ใช่เงียบ

**ห้ามสร้างกลไกที่แก้ไม่ได้** — ที่มาของการกรอง `isDead` ตามโหมด และการไม่ขังผู้เล่นแบบแข็ง

**การเคลื่อนที่เป็น owner-authoritative** — server clamp ตำแหน่งไม่ได้ ต้องใช้ทาง
`ApplyKnockbackClientRpc` (แก้ `CLAUDE.md` ที่เขียนผิดไปแล้วใน `f280c167`)

---

## 8 · ค้างใน working tree

**ของเจ้าของโปรเจกต์ — อย่า commit แทน**
`GameScenes/*.unity` · `Sarabun*` font SDF · `MiniBossConfig*` · `AoE_*.asset` ·
`BossConfig_01.asset` · `TetherAction.asset` · `DonutAoEAction.asset` · `BehaviorSettings.asset`

**แก้ต่อหลัง `0c60bf7d`** — `TelegraphUniversal.shadergraph` ถูกแก้อีกรอบ ยังไม่ commit
· **ไม่ใช่การจูนค่า** อย่างที่เดาไว้ตอนแรก แต่คือการ rename `_Sweep*` → `_Ring*` (ดู §5)
· `Mat_Tele_Universal.mat` กับ `TelegraphZone.cs` แก้ตามให้แล้ว

**ไม่ได้ใช้แล้ว** — `Tele_outline.shadergraph` + `Mat_Tele_OutLine.mat`
เป็น inverted hull ซึ่งทำในกราฟเดียวไม่ได้ (ShaderGraph ปล่อย pass เดียว) · ลบได้ถ้าไม่เอาไว้ทำเส้นสัน 3D

---

## 9 · ถ้าจะทำต่อ — เรียงตามคุ้ม

1. **ผูก `rollName` แล้วเทสต์** — ปลดล็อกงานที่ทำไปแล้วทั้งก้อน ไม่ต้องเขียนโค้ดเพิ่ม
2. ~~แก้บั๊ก `innerRadius` โดนัท~~ — **เจ้าของตัดสินว่ายังไม่แก้** · ต้นเหตุจริงบันทึกไว้ใน §3 แล้ว
3. ~~เก็บหนี้ชื่อ `_Sweep*`~~ — **ทำแล้ว 2026-08-12** (ดู §5)
4. **เทสต์ Limit Cut** — `WorldNumberTag` ใส่ให้แล้ว เหลือกด Play อย่างเดียว
   (ยังต้องสร้าง asset ของ `LimitCutAction` แล้วผูกเข้า phase ก่อน)
5. **เส้นตามสัน 3D** ถ้ายังอยากได้ — สำรวจไว้แล้ว 5 เทคนิค ผลสรุปอยู่ในประวัติแชท
   สำหรับ telegraph แนะนำวาดเส้นเป็น geometry จริง (ล้อ `BossTether.SetupLeashRing`)
   ถ้าอยากได้ทั้งเกมให้ใช้ Sobel post-process แทน

**ยังไม่ทำและตั้งใจไม่ทำ** — กระสุนบอส (bullet pattern) และ adds/summon
ตัดออกจาก ADR-003 อย่างตั้งใจ เป็นระบบใหม่ ไม่ใช่ส่วนขยาย

---

## 10 · สเปกสูตรใน `TelegraphUniversal` — ตรวจจากไฟล์กราฟแล้ว (2026-08-12)

ถอดจาก `.shadergraph` โดยตรง ไม่ได้เดา · ใช้ทำงานก้อนถัดไปได้เลยไม่ต้องขุดซ้ำ

### สนามระยะที่มีอยู่

```
p_n   = Combine(Position.x, Position.z) × 2        // Multiply b4a50561 · ขอบทรง = 1
half  = Object.Scale × 0.5                          // Multiply c85c39a3 · Split เอา R=halfX, B=halfZ
เหลี่ยม = min( (1-|p_n.x|)·halfX , (1-|p_n.y|)·halfZ )
กลม    = ( 1 - length(p_n) )·halfX
d     = Lerp(เหลี่ยม, กลม, _OutlineShape)
```

**`d` เป็นเมตรจริง ไม่ใช่สองเท่า** — เพราะ `×2` ที่ position ถูกหักล้างด้วย `×0.5` ที่ scale พอดี
(เคยเข้าใจผิดว่าเป็นสองเท่า · ค่าคงที่ทั้งสองอ่านจากไฟล์แล้ว)

`d` ป้อนสองที่: `Step(_OutlineWidth)` → แถบขอบ · และ `Add → Divide(_RingSpacing) → Fraction` → วงระลอก
**แก้ `d` ที่เดียว ได้ทั้งขอบและวง**

### พจน์ที่ต้องเพิ่ม — ทุกตัวเป็นเมตร บวก = อยู่ข้างใน

> **ขอบเขตหดเหลือทรงเดียวแล้ว (2026-08-13)** — ตัดสินหลังจากไล่ดูทีละทรงแล้วพบว่า
> **Cross กับ Cone วาดเนื้อพื้นที่อันตรายถูกอยู่แล้ว** ผิดแค่เส้นขอบตกแต่ง ไม่ใช่บั๊กความถูกต้อง
> · มีโดนัททรงเดียวที่วาดพื้นที่ผิดจริง (torus แถบบาง vs `IsInDonut` ที่เป็น annulus ตัน)
> · สายส่งค่าฝั่ง C# ของ Cross/Cone **รื้อออกไปแล้ว** เหลือแต่ `_InnerRadius`
>
> **งานที่เหลือจริง 2 ข้อ**
> 1. `_InnerRadius` + `min(d, dIn)` + guard · แล้วสลับ `TrySpawnVfxPrefab` ให้ Donut ใช้ `circlePrefab`
>    (สองอย่างนี้ต้องลงพร้อมกัน สลับก่อนจะได้จานตันไม่มีรู)
> 2. **ผนังข้าง Line แดงทั้งแผง** — กลับมาแล้วหลังเปลี่ยนเป็นก้อนหนา 2 หน่วย
>    ผิว ±X/±Z มีระยะถึงขอบ = 0 พอดี · แก้ให้สูตรขอบคิดเฉพาะระนาบ XZ ไม่ยิงบนผนังตั้ง
>
> แถวล่างของตารางเก็บไว้เป็นบันทึกเผื่อวันหนึ่งกลับมาทำ Cross/Cone

| ทรง | สูตร | ค่าปิด |
|---|---|---|
| Cross | `dSib = min(sibHalfX - \|p_n.x\|·halfX , sibHalfZ - \|p_n.y\|·halfZ)` → `max(d, dSib)` | `_SiblingHalfExtents = (0,0)` → dSib ≤ 0 → max คืน d |
| Donut | `dIn = (length(p_n) - _InnerRadius)·halfX` → `min(d, dIn)` | `_InnerRadius = 0` → **ต้อง guard** ไม่งั้นได้จุดขอบกลางวง |
| Cone | `dWedge = sin(h)·(p_n.y·halfZ) - cos(h)·\|p_n.x·halfX\|` โดย `h = _ConeAngle/2` → `min(d, dWedge)` | `_ConeAngle = 360` → **ต้อง guard** ด้วยเหตุผลเดียวกัน |

ลำดับรวม: `min( min( max(d, dSib), dInGuard ), dWedgeGuard )` แล้วป้อนแทนที่ `d` เดิม

**ทางลัดที่ควรใช้** — `|p_n.x|·halfX` หาได้จากโหนดที่มีอยู่แล้วโดยไม่ต้องรู้ค่าคงที่:
มันคือ `halfX − (พจน์เหลี่ยมแกน X)` ซึ่งทั้งสองตัวมีสายอยู่ในกราฟแล้ว

**guard ทั้งสองตัวจำเป็นจริง ไม่ใช่ความระมัดระวังเกินเหตุ** — ถ้าไม่มี `min()` จะเจอศูนย์ที่จุดกึ่งกลาง
ของทุกวงกลม แล้ววาดจุดขอบขนาดเท่า `_OutlineWidth` ไว้กลางวงทุกอัน · ทำด้วย `Step` + `Lerp` ไปค่ามากๆ

### ฝั่ง C# ต่อสายไว้ให้แล้ว

`TelegraphZone.PushShaderColors` ดัน `_SiblingHalfExtents` (ต่อ renderer) · `_InnerRadius` (สัดส่วน 0–1)
· `_ConeAngle` ครบแล้ว ทุกตัวมี `HasProperty` คุม — **กราฟยังไม่มี property พวกนี้ก็ไม่พัง เป็น no-op เฉยๆ**

`rendererSiblingHalfExtents` เก็บค่าคู่ index กับ `visualRenderers` เพราะ Cross เป็นทรงเดียว
ที่ renderer สองตัวต้องได้คนละค่า · ทาง primitive fallback ไม่เติมลิสต์นี้ จึงมี bounds check

### ยังไม่ได้สลับ Donut ไปใช้จานแบน

`TrySpawnVfxPrefab` ยังชี้ `donutPrefab` (torus) อยู่ — **สลับไป `circlePrefab` พร้อมกับตอนต่อโหนด
`_InnerRadius` เท่านั้น** ถ้าสลับก่อนจะได้จานตันไม่มีรู ซึ่งแย่กว่า torus รูผิดขนาด · มีคอมเมนต์เตือนไว้ในโค้ดแล้ว
