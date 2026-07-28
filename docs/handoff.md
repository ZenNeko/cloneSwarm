# Handoff → Antigravity  (Round 2)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ให้ทำ: **N5 timeScale softlock + N6 FlowField** — 7 tasks · 6 ไฟล์ (ใหม่ 1)

- T1 `GamePause.cs` **(ไฟล์ใหม่)** — static เจ้าของเดียวของ `Time.timeScale` (โค้ดเต็มอยู่ในแผน ลอกได้เลย)
- T2 `SharedExperienceManager.cs` — 3 จุด → `GamePause.Add/Remove(PhaseSelect)`
- T3 `UI/PauseMenuUI.cs` — **ลบ field `prevTimeScale` ทิ้ง** + 4 จุด
- T4 `WinLoseUI.cs` — 2 จุด → `GameOver` / `ResetAll()`
- T5 `DevTools.cs` — slow-mo เขียน `GamePause.ResumeScale` แทน
- T6 grep ตรวจว่าไม่เหลือ `Time.timeScale` นอก `GamePause.cs` (**ไม่แก้ไฟล์**)
- T7 `FlowFieldPathfinder.cs` — ย้าย `BakeWalkable()` จาก `Start()` ไป lazy ใน `Update()` หลัง server gate

ข้อควรระวัง:
- **T7 อย่าเชื่อที่ audit เขียนว่า "แก้บรรทัดเดียว"** — `BakeWalkable()` allocate array 4 ตัวด้วย
  ถ้าใส่ `if (!IsServer) return;` ใน `Start()` แล้ว NetworkManager ยังไม่ listening จังหวะนั้น
  **host จะไม่ bake ทั้งเกม → enemy ยืนนิ่งทั้งแมพ** ต้องทำ lazy ใน `Update()` ตามแผนเท่านั้น
- **ห้ามแตะ `WaitForSecondsRealtime` / `Time.unscaledDeltaTime`** (`SharedExperienceManager:210` ·
  `WinLoseUI:92,113`) พวกนี้ต้องเดินตอนเกมหยุด ถูกอยู่แล้ว
- `GamePause` ต้องเป็น **static ธรรมดา** ห้ามทำเป็น MonoBehaviour/singleton ในซีน และ**ห้าม sync ข้าม network**
  — pause เป็นเรื่อง local คนอื่นต้องเล่นต่อได้
- T1-T6 กับ T7 **ไม่แตะไฟล์เดียวกันเลย** ทำสลับลำดับได้

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
