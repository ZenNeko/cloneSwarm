# Handoff → Antigravity  (Round 5)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **10 tasks · 8 ไฟล์** — เป็น **hot-path sweep ของใหม่** ไม่ใช่ backlog เดิม
(backlog จาก audit ถูกเก็บไปเกือบหมดแล้วใน Round 1-4)

- T1 `playermove.cs:118,154` — `GetComponent<PlayerStatManager>()` ทุกเฟรม → cache
- T2 `FloorHazard.cs:137-139` — `GetComponent` + `.material` ทุกเฟรม → cache + `MaterialPropertyBlock`
- T3 `FloorHazard.cs:160,180` — `.material` ตอน setup → `sharedMaterial`
- T4 `BossTether.cs:246,268,272` — `.material` → `sharedMaterial` + MPB
- T5 `Enemy.cs:495-510` — `.material` ตอน freeze → MPB (ศัตรูหลักร้อยตัว = clone หลักร้อยชิ้น)
- T6 `VFX/BillboardFaceCamera.cs` — `Camera.main` ทุกเฟรม → cache
- T7 `UI/FloatingBuffUI.cs:92,94` — `Camera.main` สองครั้งต่อเฟรม → cache
- T8 `MineObject.cs:48,58` — `OverlapSphere` ทุกเฟรม → `OverlapSphereNonAlloc`
- T9 `DevTools.cs:274` — canvas ซ้อนทุกครั้งที่โหลดฉากใหม่
- T10 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**)

## กฎที่สำคัญที่สุดของรอบนี้

> **ห้ามเปลี่ยนสิ่งที่ผู้เล่นเห็น** — สี ขนาด ความเร็ว จังหวะ ต้องเหมือนเดิมเป๊ะทุกจุด
> รอบนี้เปลี่ยน**วิธีทำ** ไม่ใช่**ผลลัพธ์** ถ้าแก้แล้วภาพเปลี่ยน แปลว่าแก้ผิด

## ข้อควรระวัง

**Round 3 และ Round 4 มี scope creep อย่างละครั้ง — รอบนี้ห้ามซ้ำ**
ทั้งสองครั้งเป็นรูปแบบเดียวกันคือ **เติมโค้ด "กันไว้ก่อน" ที่ไม่มีใครขอ**
(Round 3 เปลี่ยน private field เป็น property · Round 4 เติม `else Destroy(gameObject)`)
→ **ห้ามเพิ่ม `else` สำรอง ห้ามเพิ่ม null-check เกิน ห้ามเพิ่ม fallback ที่แผนไม่ได้สั่ง**

**ต้นแบบมีอยู่แล้วในโปรเจกต์ ใช้ลอก ห้ามแก้**
`TelegraphZone.cs` (`:52` `:244` `:684-687`) = รูปแบบ `sharedMaterial` + `MaterialPropertyBlock` ·
`WorldHPBar.cs:102` = รูปแบบ cache `Camera.main` · `Enemy.cs:261` = รูปแบบ `OverlapSphereNonAlloc`

**T8 ระวังกับดักของ NonAlloc** — มันคืน**จำนวนที่เจอ** และ buffer มีของเก่าค้างเกินจำนวนนั้น
ต้องวนแค่ `0..count-1` **ห้ามวนทั้ง buffer** ไม่งั้นจะไปโดนของจากเฟรมก่อน

**T3 ถ้าพบว่ามีการแก้สีของ material ตัวนั้นทีหลัง — หยุด** เพราะ `sharedMaterial` จะไปแก้ asset ต้นฉบับ
เขียนใต้ `## Questions` อย่าเดา

**ห้ามแตะ `.material` ในไฟล์อื่น** — `HealingOrb` `MagnetOrb` `OrbVisual` `FenceWeapon` `BigAoEWeapon`
`OrbitalStrikeWeapon` `SupportArenaWeapon` `NetworkedVFXPool` ยังไม่ได้ตรวจว่าอยู่ในเส้นทางร้อนไหม **จงใจเว้น**

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
