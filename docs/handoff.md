# Handoff → Antigravity  (Round 1)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ให้ทำ: Phase 3 performance + บั๊ก AbilityBase
6 tasks · 6 ไฟล์

- T1  `AbilityBase.cs` — null guard ใน `OnActiveSceneChanged` (แก้ MissingReferenceException)
- T2a `Enemy.cs` — `List<Enemy> ActiveEnemies` → `HashSet<Enemy>`
- T2b `FlowFieldPathfinder.cs` — `for (i)` → `foreach` (HashSet ไม่มี indexer)
- T3  `EnemySpawner.cs` — `InvokeRepeating` → `IEnumerator SpawnLoop()`
- T4  `GameTimeline.cs` — `% 2 == 0` → timer accumulator
- T5  `NetworkedVFXPool.cs` — `SetParent(null)` → `SetParent(transform, false)` **2 จุด**

ข้อควรระวัง:
- T2a กับ T2b **ต้องทำคู่กัน** ไม่งั้นคอมไพล์ไม่ผ่าน
- T1 ห้ามย้ายไป OnEnable/OnDisable — อ่านเหตุผลในแผน
- งานนี้คือ optimize ล้วน behavior ที่ผู้เล่นเห็นต้องเหมือนเดิมทุกอย่าง

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
