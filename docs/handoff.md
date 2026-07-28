# Handoff → Antigravity  (Round 4)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **9 tasks · 8 ไฟล์** — ไม่ได้ใหญ่กว่า Round 3 แต่**ยากกว่า**
T1-T5 เป็นการเพิ่ม API บนคลาสฐานแล้วเดินสายไป call site 4 จุด ไม่ใช่แก้จุดต่อจุด

- T1 `BossController.cs` — เพิ่มทะเบียนกลไก + `RegisterMechanic()` + `CleanupMechanics()`
- T2 `Data/SpawnAoEActionBase.cs:87` — แจ้งทะเบียนหลัง spawn
- T3 `Data/ColorMatchAoEAction.cs:63` — เหมือน T2
- T4 `Data/KeepMovingAction.cs:106` — เหมือน T2
- T5 `Data/TetherAction.cs:56` — เหมือน T2
- T6 `Projectile/MissileProjectile.cs` — เพิ่ม `IsSpawned` guard (เป็นตัวเดียวใน 10 ตัวที่ไม่มี)
- T7 `Projectile/BouncingSpikeProjectile.cs` — hit-dedup (ลอกจาก `Boomerang`)
- T8 `Weapon/hero/BunnyHopWeapon.cs` — เช็คตายระหว่าง dash
- T9 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**)

## บั๊กหลักคืออะไร

ฆ่าบอสตอนวงเตือน AoE กำลังนับถอยหลัง → **วงยังระเบิดและกินดาเมจ** เพราะ telegraph/tether
เป็น NetworkObject อิสระที่ไม่มี back-reference กลับหาบอส และ `OnDeath()` หยุดแค่ coroutine ของตัวเอง

## ข้อควรระวัง

**Round 3 มี scope creep — รอบนี้ห้ามซ้ำ**
Round 3 เปลี่ยน `private Enemy activeMainBossEnemy;` เป็น `public ... { get; private set; }`
เองโดยไม่มีในแผน และไม่มีใครใช้นอกไฟล์นั้นเลย ต้องย้อนกลับ
→ **รอบนี้ห้ามเปลี่ยน access modifier หรือ signature ของสมาชิกที่มีอยู่แล้ว เพิ่มของใหม่ได้อย่างเดียว**

**T1 ห้าม over-engineer** — ของที่ despawn ตัวเองไปแล้วจะค้างเป็น entry ใน list ซึ่ง**ไม่เป็นไร**
`CleanupMechanics` เช็ค `!= null && IsSpawned` อยู่แล้ว **ห้ามเพิ่ม coroutine มาไล่ prune**

**ห้ามแตะ `TelegraphZone.cs` / `BossTether.cs` / `FloorHazard.cs`** — จัดการตัวเองถูกอยู่แล้ว
**ห้ามแตะ `BoomerangProjectile.cs`** — ต้นแบบของ T7
**ห้ามแตะ `MainBoss.cs`** — `GenerateLegacyConfig` รันทุก client ดูเหมือนบั๊ก แต่ client อ่าน
phase threshold จาก `config` อยู่ **ยังวิเคราะห์ไม่ครบ จงใจเว้น**

**T7 ถ้าพบว่า spike ตั้งใจให้ตีซ้ำได้ตอนเด้งกลับ — หยุด อย่าเดา** เขียนใต้ `## Questions`
เพราะการใส่ dedup จะเปลี่ยนสมดุลเกม

ถ้าติดจน signature ต้องเปลี่ยน **อย่าเปลี่ยนเอง** เขียนใต้ `## Questions` แล้วข้ามไป task ถัดไป

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
