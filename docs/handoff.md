# Handoff → Antigravity  (Round 1)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ให้ทำ: P0 จาก audit รอบสอง — 6 tasks · 9 ไฟล์

- T1 `Enemy.cs` — เพิ่ม `netMaxHealth` + `ServerSetMaxHealth()` + แก้ `GetHealthPercent`
- T2 `Elite/EliteController.cs` — ใช้ setter แทนเขียน field ตรง
- T3 `UI/WorldHPBar.cs` — อ่าน `netMaxHealth.Value` แทน `maxHealth`
- T4 `HealingOrb.cs` + `MagnetOrb.cs` — เพิ่ม `isCollected` guard (ลอกจาก `ExpOrb.cs:89-95`)
- T5 `ValorWeapon.cs` ลบ 2 บรรทัด · `LightningChainWeapon.cs` + `StormBunnyWeapon.cs` เปลี่ยน `hitVfx` เป็น `"None"`
- T6 `ClusterBombWeapon.cs` — เพิ่ม distance gate (ลอกจาก `DeathFieldWeapon.cs:60-68`)

ข้อควรระวัง:
- **3 ไฟล์ที่ห้ามแตะ** — `ExpOrb.cs` · `DeathFieldWeapon.cs` · `StormcallerWeapon.cs`
  เป็นต้นแบบที่ถูกอยู่แล้ว **ใช้ลอกเท่านั้น**
- T1 ต้องเป็น **setter** ไม่ใช่เซ็ตค่าตรงทีละจุด — เพื่อกันไม่ให้คนเพิ่มจุดใหม่แล้วลืมอีก
- ห้ามเปลี่ยนค่าสมดุลเกม T6 คือคืนพฤติกรรมที่ตั้งใจไว้ ไม่ใช่ปรับบาลานซ์

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
