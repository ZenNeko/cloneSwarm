# Session Handoff — 2026-08-07

> เขียนไว้ให้เซสชันถัดไปอ่านต่อ · branch `ux-ui-flow-2026-08`
> **ยังไม่ commit อะไรตั้งแต่ `41376152`** — Round 4, Round 5 และ asset reorg ค้างอยู่ทั้งหมด

---

## จบไปแล้ว

| Round | ได้อะไร | commit |
|---|---|---|
| 1–2 | ล็อบบี้ networked · `LobbyState` · Q/E TabBar · `PlayerSlotRegistry` (แก้บั๊ก `clientId % 4`) · map/difficulty | `deef957d` |
| 3 | cast bar · `ArenaDefinition`/`ArenaAnchors` · `RollContext` + seed | `41376152` |
| 4 | `StatusEffectData` · `PlayerStatusManager` · `damageTakenMult` · `StatusHUDUI` · roll ผูกกับ action | **ยังไม่ commit** |
| 5 | รื้อเส้นทางเมนู · ลบ `OnlineMenuUI` · ยก session logic ขึ้น `GameSessionManager` | **ยังไม่ commit** |

รวม 5 รอบ · 57 task · Fix Round เกิดครั้งเดียว · Claude แก้เองรวม 5 บรรทัด (mechanical ล้วน)

---

## กำลังทำอยู่ตอนนี้ — debug ล็อบบี้ออนไลน์

### อาการเดิม
กด "เชิญเพื่อน" แล้ว **ปุ่มรหัสห้องไม่โผล่** ทั้งที่ session สร้างสำเร็จ

### root cause ที่หาเจอ
`CreateSessionAsync` / `JoinSessionAsync` **ทิ้ง `ISession` ที่ได้มา** แล้ว `return true` โดยไม่เซ็ต
`currentSession` — ตัวแปรนั้นถูกเซ็ตที่เดียวคือ `OnSessionAdded()` ซึ่งพึ่ง `SessionObserver`
ที่เงียบหลัง `NetworkManager.Shutdown()`

หลักฐานจาก log จริง:
```
[DBG-lobby7] CreateSessionAsync คืน True · CurrentSession null? True · code=''
[DBG-lobby7] Refresh — hasSession=False · ทุกฟิลด์ไม่ null
```
และ `OnSessionJoined ยิงแล้ว` ไม่โผล่เลย

### แก้ไปแล้ว (ยังไม่ได้ทดสอบ)
1. `OnSessionAdded` — เพิ่ม guard `session == null || currentSession == session`
2. `CreateSessionAsync` — เรียก `OnSessionAdded(created)` **นอก try** (ถ้าอยู่ในนั้น handler ที่ throw
   จะทำให้รายงานว่าสร้างไม่สำเร็จทั้งที่ห้องเปิดแล้ว)
3. `JoinSessionAsync` — เหมือนกัน
4. `RestoreFromExistingSession` — **ลบทิ้ง** ไม่มีใครเรียก และ `Start():65` ทำงานเดียวกัน
5. `Status()` — ตัดรหัสห้องออกจากข้อความทั้ง 3 จุด (เคยรั่วไปโผล่บนปุ่ม JoinLobby)
6. `LobbyUI` — subscribe `GameSessionManager.OnSessionJoined` + `OnSessionLeft` → `Refresh()`
   (เดิมไม่มีใครสั่งวาดใหม่หลังสร้าง session)

### ขั้นถัดไป
กด Invite → กรอง Console ด้วย **`DBG-lobby7`** → ต้องเห็น
`OnSessionAdded ลงทะเบียนแล้ว` ตามด้วย `Refresh — hasSession=True`

---

## ความเสี่ยงที่ยังไม่ปิด

**`EnsureLobbyStateSpawned` อาจพลาดเหมือนบั๊ก B1 เดิม** — `MenuManager` subscribe
`OnSessionJoined` แล้วเรียก `EnsureLobbyStateSpawned()` ซึ่งเช็ค `if (!nm.IsServer) return;`
ตอนนี้ `OnSessionJoined` ยิง synchronous จากกลาง `CreateSessionAsync` ถ้า Building Block
ยัง `StartHost` ไม่เสร็จ จะ return เงียบอีกครั้ง

**ยังไม่มีหลักฐานว่าเกิดจริง** — ดูจาก log รอบหน้าว่า `OnSessionAdded ลงทะเบียนแล้ว` มาก่อนหรือหลัง
LobbyState spawn ถ้าเกิดจริง แก้โดยผูกกับ `NetworkManager.OnServerStarted` แทน

---

## ต้องเก็บกวาดก่อนปิดงาน

- **ลบ probe ทั้งหมด**: `grep -rn "DBG-lobby7" Assets/Script/` แล้วลบ (ตอนนี้ 12 จุด ใน 2 ไฟล์)
- **`CLAUDE.md` ชี้ path ผิด** — SO ย้ายจาก `Assets/Script/Data/` ไป `Assets/ScriptableObjects/` แล้ว
  (ผู้ใช้ย้ายเองใน Editor · 97 ไฟล์ · GUID ไม่ขาด) แต่ `CLAUDE.md` ยังเขียน path เดิม
- **`git status` มี 233 D + 24 ??** ส่วนใหญ่คือ asset reorg ที่ git ยังไม่ detect เป็น rename
  แนะนำแยกเป็น 3 commit: Round 4 · Round 5 · asset reorg

---

## งาน Editor ที่ยังค้าง (`docs/lobby-setup.md`)

ทำไปแล้ว: `MapData_Arena01.asset` · ต่อสาย `LobbyUI` ครบ 6 ฟิลด์ใหม่

ยังไม่ได้ทำ:
- `PlayerStatusManager` add ลง player prefab
- `StatusHUDUI` ต่อ Canvas + สร้าง `StatusEffectData` asset อย่างน้อย 1 ตัว
- `LevelUpUI.waitingStrip` · `WinLoseUI.playAgainButton`
- ย้าย `CharacterSelectUI` ไปเป็นลูกของ `Panel_Character` (Round 5 ทำโค้ดไว้แล้ว)
- ลบ missing script ของ `OnlineMenuUI` ที่เหลือบน Canvas
- **`joinStatusText` ต้องเป็น Text แยก ห้ามใช้ป้ายบนปุ่ม JoinLobby** — เคยทำให้รหัสห้องไปเขียนทับป้ายปุ่ม

---

## ยังไม่เคยเล่นจริงสักครั้ง

ทุกอย่างตั้งแต่ Round 1 **คอมไพล์ผ่านแต่ยังไม่เคยรันจนจบเกม** สิ่งที่ compile check จับไม่ได้
และต้องเทสต์ด้วยมือ:

- **สีผู้เล่นหลัง reconnect** — ให้คนออกแล้วต่อกลับ แล้วดูว่า color match ยังแจกสีไม่ซ้ำ
  (เล่นรวดเดียว 4 คนไม่มีทางเจอ)
- **boss config ตาม tier** — ดูจากจอ client ว่า phase marker ตรงกับ host ไหม
- **`GetDamageTakenMult()`** — ต้องคืน 1.0 พอดีเมื่อไม่มี status ไม่งั้นดาเมจทั้งเกมเพี้ยนเงียบ
- **กด ESC ตอน Level Up** — ต้องไม่มีอะไรเกิดขึ้น

---

## สิ่งที่ตัดสินใจไปแล้ว ห้ามรื้อ

| หัวข้อ | ผล |
|---|---|
| Solo | ต้องเล่นออฟไลน์ได้ → ไม่สร้าง session |
| จุดตัดสินออนไลน์ | ล็อบบี้ออฟไลน์เสมอ · เชิญเพื่อน = `Shutdown()` แล้วสร้าง session |
| แหล่งความจริง | `RunSetup` เป็นหลัก · `LobbyState` เป็นกระจก |
| เลือกตัวละคร | แท็บในล็อบบี้ที่เดียว · คลิก = เลือกทันที |
| reset ready | เปลี่ยนตัวละคร = reset เฉพาะคนนั้น · เปลี่ยนแผนที่/ความยาก = reset ทุกคน |
| ความยาก | 5 tier · สลับ WaveConfig/BossConfig ไม่ใช่คูณเลข · author จริงแค่ Normal |
| แท็บแผนที่ | client ซ่อน (วงเหลือ 3) |

เอกสารเต็ม: `docs/plan-savage-boss-system.md` · `docs/lobby-setup.md` · `docs/implementation_plan.md`
