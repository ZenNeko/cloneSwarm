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
- ตัวเลขความยาวคลิปเป็น **ค่าประมาณ** จาก warning/duration — ท่า runtime จริงอาจต่างเล็กน้อย
- MiniBoss ที่ยังใช้ `MiniBossAI` (ระบบ Mechanic เดิม) ยังไม่ผ่านระบบนี้ — ใช้ได้เฉพาะบอสที่ขับด้วย
  `BossController` + `BossEncounterConfig`
