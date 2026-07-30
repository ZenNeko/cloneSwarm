# Handoff → Antigravity  (Round 8)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **11 tasks · 7 ไฟล์** — มาจากผลเทสต์จริงและการตัดสินใจของผู้ใช้ ไม่ใช่จาก audit

**ก้อนที่ 1 — host กด ESC ไม่ให้หยุดทั้งห้อง (T1-T5)**
- T1 `GamePause.cs` — เพิ่ม `LocalInputSuspended` (ระงับ input **ห้ามแตะ `timeScale`**)
- T2 `UI/PauseMenuUI.cs` — solo หยุดโลกเหมือนเดิม · multiplayer ระงับแค่ input
- T3 `playermove.cs:152` — เพิ่มเงื่อนไขในบรรทัด gate ที่มีอยู่
- T4 `Weapon/WeaponBase.cs:92-96` — เพิ่ม gate ชั้นที่ 4 (ทุกอาวุธผ่านจุดนี้หมด)
- T5 ability — **grep หาเอง** ถ้ากระจายเกิน 3 ไฟล์ให้หยุดถาม

**ก้อนที่ 2 — เลขดาเมจปรับได้จาก Inspector (T6-T8)**
- ยกค่าที่ฝังในโค้ดขึ้นมาเป็น field: อายุ · ความเร็วลอย · ขนาด · สี · สีคริต · ระยะสุ่ม

**ก้อนที่ 3 — ย้าย pool (T9-T10)**
- `FloatingDamageTextPool` เลิกสร้าง GameObject เอง ให้ผู้ใช้แปะบน `NetworkedVFXPool` แทน

- T11 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**)

## กฎที่สำคัญที่สุดของรอบนี้

> **`LocalInputSuspended` ห้ามแตะ `Time.timeScale` เด็ดขาด**
> นั่นคือบั๊กที่กำลังแก้อยู่พอดี — host คือ server พอ `timeScale = 0` ศัตรูหยุดทั้งห้อง
> ตัวนี้ระงับแค่ input ของเครื่องตัวเอง **โลกต้องเดินต่อ**

## ข้อควรระวัง

**อย่าลืมคืนค่า** — `GamePause.ResetAll()` ต้องเคลียร์ `LocalInputSuspended` ด้วย
และ `PauseMenuUI` ต้องคืนทั้งใน `Resume()` · `OnDisable()` · `OnQuitClicked()`
ไม่งั้นเปลี่ยนฉากแล้วขยับไม่ได้ **นี่คือรูปแบบเดียวกับบั๊ก `prevTimeScale` ที่ Round 2 แก้ไป**

**T3 ห้ามแตะ regen/สถานะฝั่ง server ใน `playermove`** — แก้เฉพาะบรรทัด gate ของการเคลื่อนที่
`:112` regen รันฝั่ง server ต้องเดินต่อ

**T4 ห้ามแตะ `PlayerWeaponManager.WeaponsEnabledInScene`** — มันแปลว่า "อยู่ในฉากเกม"
คนละความหมายกับ pause ให้เพิ่ม gate ใหม่ต่อจากมัน

**T9 ห้ามลบ guard ที่ใช้ `_instance` ตรงๆ ใน `Awake`/`OnDestroy`** — มันกัน stack overflow
ที่ getter เรียกตัวเองไม่รู้จบ (เจอมาแล้วใน Round 7) **อ่านคอมเมนต์ในไฟล์ก่อนแก้**

**T6 ห้ามเปลี่ยนค่า default** — ย้ายที่มาของค่าขึ้นมา Inspector เฉยๆ หน้าตาต้องเหมือนที่เทสต์ไปแล้ว

**Round 3-7 มี scope creep ทุกรอบ** รูปแบบ: เติมโค้ดกันไว้ก่อนที่ไม่มีใครขอ
ถ้าติดจนทำตามแผนไม่ได้ **หยุดแล้วเขียนใต้ `## Questions`** อย่าหาทางอ้อม

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
