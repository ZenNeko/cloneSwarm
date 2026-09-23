# Boss Designer

หน้าต่างออกแบบ encounter ของบอสแบบเห็นภาพ — แทนการไล่แก้ตัวเลขใน Inspector

**เปิดใช้**: `Tools → Boss Designer` หรือดับเบิลคลิก `BossEncounterConfig` / `BossTimelineAction` asset ใน Project

## โครงหน้าต่าง

```
┌─ toolbar: เลือก BossEncounterConfig ──────────────────────────┐
├─ Encounter Graph ─────────────────────────────────────────────┤
│  [Phase 1] --HP≤75%--> [Phase 2] --HP≤30%--> [Phase 3]  [+ Phase]
│   (คลิก node เพื่อแก้ timeline ของเฟส / คลิกป้าย enrage เพื่อแก้ enrage)
├─ Timeline Editor ─────────────────────────────────────────────┤
│  track headers │ ruler (คลิก/ลาก = playhead)                  │
│                │ [==Circle AoE==]   [==Cross AoE==]           │
│                │      [==Line AoE==]        [==Chase==]       │
│                │        [========Tether (10s)========]        │
└────────────────────────────────────────────────────────────────┘
```

## ระบบข้อมูล

- `BossTimelineAction` (`Assets/Script/Data/BossTimelineAction.cs`) — BossAction ชนิดใหม่
  เก็บ tracks → clips (`action` + `startTime`) ทุกคลิปถูก schedule คู่ขนานนับจากจุดเริ่ม timeline
  (generalize มาจาก `ComboAction`)
- ใช้เป็นชุดท่าประจำเฟส: ใส่เป็น action **ตัวเดียว** ใน `BossPhase.actions`
  → `BossController.AttackLoop` รัน timeline จนจบ เว้น `cooldownAfter` (หรือ `attackInterval`) แล้ววนใหม่
- เฟสที่ยังเป็น action list แบบเก่า จะมีปุ่ม **"แปลงเป็น Timeline"** — วางท่าเดิมเรียงต่อกันบน
  track เดียวตามระยะ interval เดิม แล้ว timeline ถูกเก็บเป็น **sub-asset** ใน config ตัวนั้น
- `BossAction.GetEditorDuration()` — ค่าประมาณความยาวท่า (ใช้วาดความยาวคลิปเท่านั้น ไม่มีผล gameplay)

## การควบคุมใน timeline

| ทำอะไร | วิธี |
|---|---|
| เลื่อนเวลาเริ่มคลิป | ลากคลิป (snap 0.1s — กด **Shift** เพื่อ snap ละเอียด 0.01s) |
| แก้ค่าท่า (damage, radius, …) | คลิกคลิป → แก้ใน Inspector (Odin) |
| เพิ่มคลิป | ปุ่ม `+` ที่หัว track (วางที่ playhead), ดับเบิลคลิกพื้นเลนว่าง, หรือ**ลาก BossAction asset จาก Project มาวางบนเลน** |
| ย้ายคลิปข้าม track / ลบ / duplicate | คลิกขวาที่คลิป |
| เพิ่ม/ลบ/เปลี่ยนชื่อ track | ปุ่ม `+ Track`, คลิกขวาหัว track, พิมพ์ชื่อในช่อง |
| Zoom | slider มุมขวาบนของ timeline |
| Undo/Redo | Ctrl+Z / Ctrl+Y ตามปกติ (รองรับทุก operation) |

แถบสีอ่อนในคลิป AoE = ช่วง telegraph (warning) — ส่วนที่เหลือคือช่วง resolve/damage
สีคลิปตามชนิด: ส้ม = AoE, ม่วง = Tether, เขียว = RandomAttack, ฟ้า = Timeline/Combo ซ้อน

## ข้อควรระวัง

- ห้ามลาก timeline ใส่ตัวเอง (ระบบกันไว้แล้วทั้งใน editor และ runtime แต่ timeline A ↔ B ซ้อนกันเป็นวงยังเป็นไปได้ — มี depth guard กันค้างที่ 8 ชั้น)
- `waitForTimelineEnd` (ค่า default = เปิด) ทำให้ AttackLoop รอจนจบ timeline ก่อนวนรอบ — ถ้าปิด
  พฤติกรรมจะเหมือน ComboAction (ยิงแล้วคืน control ทันที)
- ตัวเลขความยาวคลิปเป็น **ค่าประมาณ** สำหรับวาดภาพ — timeline รอ "คลิปจบจริง" ไม่ได้รอตามตัวเลขนี้
  (เดิมรอตามตัวเลข จังหวะของบอสจึงขึ้นกับค่าที่ตั้งใจให้แค่พอเห็นภาพ)
  ถ้าเขียน `BossAction` ใหม่ **ต้องคืนค่าเมื่อกลไกจบ ไม่ใช่เมื่อยิงออกไปแล้ว** — ดูสัญญาใน
  `BossAction.ExecuteCoroutine` · และ override `GetEditorDuration()` ให้คลิปที่วาดยาวเท่ากลไกจริง
- เปลี่ยนเฟสจะ **ล้างกระดาน**: telegraph/tether ที่ค้างถูก despawn และคลิปของเฟสเก่าที่ยังไม่ถึงคิว
  จะทิ้งตัวเอง (run generation ใน `BossController`) ช่องว่างของการเปลี่ยนเฟส = `invincibilityDuration`
  อย่างเดียว ไม่จ่าย `firstAttackDelay` ซ้ำ
- ทุกบอสในเกมขับด้วย `BossController` + `BossEncounterConfig` แล้ว (`MiniBossAI` ถูกลบไปตั้งแต่ 2026-07)
- **Enrage ตรงเวลาแล้ว (2026-09-24)** — `BossController` มีตัวจับเวลาแยก พอครบ `enrageTime` ตัด timeline ปกติ
  ทันที (เลขรอบ) แล้วเริ่มท่า enrage ตัวแรก · วงที่วางไปแล้วยังระเบิดตามที่เตือน · เดิมเช็คแค่ตอนเริ่มท่า
  เลยรอจน timeline ทั้งเส้นจบ
- **เกณฑ์ HP ของเฟส** — เปลี่ยนเฟสเมื่อ HP ≤ ค่าของ **เฟสปัจจุบัน** · เฟสสุดท้ายไม่ใช้ค่านี้ · ค่า 0% บนเฟสที่ไม่ใช่
  เฟสสุดท้าย = เฟสถัดไปไม่มีวันมา · chip บนกราฟขึ้นสีแดงและ `BossConfigAudit` ตก · ปุ่ม `+ Phase` ตั้งค่าให้เอง

## แผงขวาเมื่อคลิก node เฟส (2026-09-24)

แก้ค่าของเฟสได้ในหน้าต่าง: เกณฑ์ HP · อมตะ · interval · กล้องสั่น · ประกาศ · VFX · enrage
toolbar มีปุ่มผลตรวจ (⚠ / ✓ — คลิกดูรายละเอียด) และป้ายว่าแมพ/ระดับไหนใช้ config นี้ · คลิปที่ใช้ roll ขึ้น 🎲ชื่อ
