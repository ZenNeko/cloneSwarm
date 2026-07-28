# Handoff → Antigravity  (Round 5+6 รวมเป็นรอบเดียว)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **14 tasks · 14 ไฟล์** — ใหญ่ที่สุดที่เคยส่ง เป็น **hot-path sweep ของใหม่ทั้งหมด**
(backlog จาก audit ถูกเก็บไปเกือบหมดแล้วใน Round 1-4)

**จัดสรรทุกเฟรม**
- T1 `playermove.cs:118,154` — `GetComponent<PlayerStatManager>()` ทุกเฟรม → cache
- T8 `MineObject.cs:48,58` — `OverlapSphere` ทุกเฟรม → `OverlapSphereNonAlloc`

**material clone (leak)**
- T2 `FloorHazard.cs:137-139` — `GetComponent` + `.material` ทุกเฟรม → cache + MPB
- T3 `FloorHazard.cs:160,180` — setup → `sharedMaterial`
- T4 `BossTether.cs:246,268,272` — → `sharedMaterial` + MPB
- T5 `Enemy.cs:495-510` — freeze → MPB (ศัตรูหลักร้อยตัว)
- T10 `VFX/OrbVisual.cs:47,50,51` — **ปริมาณสูงสุดในเกม** orb เกิดทุกครั้งที่ศัตรูตาย
- T11 `HealingOrb.cs:40` + `MagnetOrb.cs:35` — → MPB

**`Camera.main` ไม่ cache**
- T6 `VFX/BillboardFaceCamera.cs` — ทุกเฟรม
- T7 `UI/FloatingBuffUI.cs:92,94` — สองครั้งต่อเฟรม
- T12 `Weapon/WeaponBase.cs:350,357` — ทุกนัดที่ยิง
- T13 `Weapon/ClusterBombWeapon.cs:154,156` + `Weapon/hero/GunnerGiantRocket.cs:90,92`

**อื่นๆ**
- T9 `DevTools.cs:274` — canvas ซ้อนทุกครั้งที่โหลดฉากใหม่
- T14 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**)

## กฎที่สำคัญที่สุดของรอบนี้

> **ห้ามเปลี่ยนสิ่งที่ผู้เล่นเห็น** — สี ขนาด ความเร็ว จังหวะ ต้องเหมือนเดิมเป๊ะทุกจุด
> รอบนี้เปลี่ยน**วิธีทำ** ไม่ใช่**ผลลัพธ์** ถ้าแก้แล้วภาพเปลี่ยน แปลว่าแก้ผิด

## บทเรียนจาก Round 4 — รอบที่แล้วคอมไพล์ไม่ผ่าน

แผนสั่งให้เติม `(runner as BossController)?.RegisterMechanic(no);` ที่ 4 จุด
แต่ **2 จุดอยู่ใน private helper ที่ไม่มี `runner` ใน scope** → `CS0103: The name 'runner' does not exist`

**เป็นความผิดของแผน ไม่ใช่ของคุณ** — แต่รอบนี้ช่วยกันไว้ด้วย:
> **ก่อนใช้ตัวแปรใดๆ ให้เช็คว่ามันอยู่ใน scope ของเมธอดนั้นจริง**
> ถ้าไม่อยู่ **อย่าเดา อย่าเปลี่ยน signature เอง** เขียนใต้ `## Questions` แล้วข้ามไป task ถัดไป

## ข้อควรระวังอื่น

**Round 3 และ Round 4 มี scope creep อย่างละครั้ง** รูปแบบเดียวกันคือ **เติมโค้ด "กันไว้ก่อน"
ที่ไม่มีใครขอ** (Round 3 เปลี่ยน private field เป็น property · Round 4 เติม `else Destroy(gameObject)`)
→ **ห้ามเพิ่ม `else` สำรอง ห้ามเพิ่ม null-check เกิน ห้ามเพิ่ม fallback ที่แผนไม่ได้สั่ง**

**ต้นแบบมีอยู่แล้ว ใช้ลอก ห้ามแก้** — `TelegraphZone.cs` (`:52` `:244` `:684-687`) = `sharedMaterial`
+ `MaterialPropertyBlock` · `WorldHPBar.cs:102` = cache `Camera.main` · `Enemy.cs:261` = `OverlapSphereNonAlloc`

**T8 กับดักของ NonAlloc** — คืน**จำนวนที่เจอ** และ buffer มีของเก่าค้างเกินจำนวนนั้น
ต้องวนแค่ `0..count-1` **ห้ามวนทั้ง buffer**

**T10 `EnableKeyword` ทำผ่าน MPB ไม่ได้** — ถ้าติด ให้คงบรรทัดนั้นแล้วแปลงเฉพาะ `color`/`SetColor`
และเขียนใต้ `## Questions`

**T3 ถ้าพบว่ามีการแก้สีของ material ตัวนั้นทีหลัง — หยุด** `sharedMaterial` จะไปแก้ asset ต้นฉบับ

**ห้ามแตะของที่รอบก่อนใส่ไว้** — `isCollected` guard ใน orb · distance gate ใน `ClusterBombWeapon` ·
`RollDamage(..., out bool _)` ใน `GunnerGiantRocket:68`

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
