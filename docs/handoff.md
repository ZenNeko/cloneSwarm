# Handoff → Antigravity  (Round 3)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **ใหญ่กว่าสองรอบก่อน** — 10 tasks · 12 ไฟล์ (แก้ 10 · **ลบ 2**)
Round 1 = 6 tasks/10 ไฟล์ · Round 2 = 7 tasks/6 ไฟล์

- T1 `Projectile.cs:77` — ส่ง `isCrit` (1 บรรทัด)
- T2 `BoomerangProjectile.cs:87` — ส่ง `isCrit` (1 บรรทัด)
- T3 `BossManager.cs` — guard กัน win ยิงซ้ำ **+ รีเซ็ต flag ตอน spawn ใหม่**
- T4 `ZoneObjective.cs` — quest Survive ต้องมี deadline จริง (เพิ่ม field 1 ตัว)
- T5 `UpgradeManager.cs` — auto-pick ตอน Orb Phase แจ้งผิดช่องทาง
- T6 `CrateSpawnManager.cs` + `OrbDropManager.cs` — เพิ่ม else log
- T7 `DevTools.cs` — `OnKillAllEnemies` ขาด `RequireServer()`
- T8 `BossTether.cs` + `FloorHazard.cs` — เรียก `base.OnNetworkDespawn()`
- T9 **ลบ** `MinefieldWeapon.cs` + `PlasmaWhipWeapon.cs` (+ `.meta`)
- T10 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**)

## ข้อควรระวัง

**`audit-status.md` มีข้อมูลผิดในเรื่อง crit flag — เชื่อ implementation_plan.md เท่านั้น**
audit บอกว่าหาย 4 จุด ตรวจจริงแล้วมีแค่ 2 ที่แก้ได้ ส่วนอีก 4 ไฟล์ (`BouncingSpike` `Train`
`Molotov` `MagicMissile`) **ถูกอยู่แล้ว ห้ามแตะ**

**ห้ามแตะ 3 ไฟล์นี้แม้ audit จะบอกว่ามีบั๊ก** — `GunnerGiantRocket.cs` · `HunterMissileAbility.cs` ·
`SplitterBombWeapon.cs` การแก้ต้องเพิ่มพารามิเตอร์ combat เข้า ServerRpc ซึ่งชนกฎโปรเจกต์
และ Stage 1 จะแก้ให้ฟรีอยู่แล้ว

**T3 ห้ามลืมรีเซ็ต flag ตอน spawn บอสตัวใหม่** ไม่งั้นบอสตัวที่สองจะฆ่าแล้วไม่ชนะ
**T4 ห้ามแตะ body ของ `QuestSurvive()`** แก้แค่ค่าที่ส่งเข้าไป — โค้ดข้างในถูกอยู่แล้ว
**T9 ถ้าเปิดไฟล์มาแล้วมีเนื้อหามากกว่า 1 บรรทัด หรือ grep เจอคนอ้างถึง — หยุด อย่าลบ** เขียนใต้ `## Questions`

ถ้าติดตรงไหนจน signature ต้องเปลี่ยน **อย่าเปลี่ยนเอง** เขียนใต้ `## Questions` แล้วข้ามไป task ถัดไป

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
