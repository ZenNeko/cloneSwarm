# Handoff → Antigravity  (Round 1)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ให้ทำ: Party HUD ชั่วคราว (แสดง host + ผู้เล่นที่จอยมา พร้อม HP)
1 task · **สร้างไฟล์ใหม่ไฟล์เดียว** `Assets/Script/UI/TempPartyHUD.cs`

ข้อกำหนดที่สำคัญกว่าฟีเจอร์ — งานนี้เป็น placeholder ที่ตั้งใจจะรื้อทิ้ง:

- **ห้ามแก้ไฟล์เดิมแม้แต่ไฟล์เดียว** ถ้าคิดว่าจำเป็นต้องแก้ ให้หยุดแล้วเขียนใต้ ## Questions
- ห้ามสร้าง prefab / ห้ามแก้ scene / ห้ามให้คนต้องลาก reference ใน Inspector
- สร้าง Canvas + UI ทั้งหมดด้วยโค้ดตอน runtime
- bootstrap ตัวเองด้วย [RuntimeInitializeOnLoadMethod] → ลบไฟล์ทิ้งแล้วต้องไม่เหลือ Missing Script ค้าง
- ห้ามให้ระบบอื่นอ้างอิงถึงคลาสนี้

เกณฑ์ตัดสิน: `git status` ต้องขึ้นไฟล์ใหม่ไฟล์เดียว ไม่มีไฟล์เดิมถูกแตะ

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
