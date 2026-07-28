# Session Handoff — 2026-07-28

อ่านไฟล์นี้ก่อนถ้าเปิดเซสชันใหม่ · เป็นสารบัญ ไม่ใช่ที่เก็บเนื้อหา

---

## ✅ ขอบเขต audit — ตรวจครบแล้ว ~170/171 ไฟล์

ทำ 2 รอบ:
1. **grep สแกนรูปแบบ** ทั้งโปรเจกต์ — authority, NetworkVariable permission, static event, `Instantiate` VFX, `.material`
2. **Sonnet 5 ตัวอ่านลึก ~170 ไฟล์** แล้ว **Claude verify ทุกข้อ HIGH ด้วยตัวเอง** ก่อนบันทึก

| ตรวจแล้ว | ผลหลัก |
|---|---|
| `Weapon/` 44 | HitEffect ซ้อน 3 จุด · `ClusterBomb` ไม่มี distance gate · crit flag หาย |
| boss / objective / `Elite/` | `Enemy.maxHealth` ไม่ replicate · กลไกบอสมีชีวิตต่อหลังบอสตาย · Survive quest ไม่มี timeout |
| `Projectile/` · `Weapon/hero/` · orb | `HealingOrb`/`MagnetOrb` เก็บซ้ำ · crit flag หาย 4 จุด |
| `Data/` 22 · `Audio/` · manager | FlowField bake ทุก client · **ไม่พบการเขียนทับ SO ตอน runtime เลย** |
| `UI/` 15 + HUD | **`Time.timeScale` softlock** · HUD ปลอดภัยกับ 4 ผู้เล่นจริง |

**ไม่ได้ตรวจ**: `Editor/` (1 ไฟล์ ไม่ขึ้น build)

### พื้นที่ที่ยืนยันว่าสะอาด — ไม่ต้องกลับมาดูอีก

ไม่มีการเขียนทับ ScriptableObject ตอน runtime · HUD กรอง `IsOwner` ถูกทุกตัว ·
static event pairing ถูกหมด · card pool ของ `UpgradeManager` ไม่มีบั๊ก ·
`WaveConfig` weight กันหารศูนย์แล้ว · ไม่มี `RequireOwnership = false` เลยสักตัว

### ⚠️ รูปแบบร่วมที่ต้องจำ — "แก้ไฟล์เดียว ลืมพี่น้อง"

โปรเจกต์มีไฟล์โครงเหมือนกันเป็นตระกูล และการแก้บั๊กที่ผ่านมาลงแค่ไฟล์เดียว **4 ครั้ง**
(`ExpOrb` vs `HealingOrb`/`MagnetOrb` · `Stormcaller` vs `LightningChain`/`StormBunny` ·
`DeathField` vs `ClusterBomb` · `playermove.GetHealthPercent` vs `Enemy`/`WorldHPBar`)

**แก้บั๊กเสร็จต้อง grep หาไฟล์รูปแบบเดียวกันก่อนปิดงานเสมอ**

---

## สถานะงาน

- branch **`audit-fixes-2026-07`** · **8 commits** เหนือ `main` · [PR #10](https://github.com/ZenNeko/cloneSwarm/pull/10) เปิดแล้ว ยังไม่ merge
- playtest ผ่านครบตาม [playtest-checklist.md](playtest-checklist.md) (เทสต์ 2 client จริง)
- **ค้างใน working tree**: `WD_Spike.asset` = งานปรับบาลานซ์ของผู้ใช้ (damage 20→25/30,
  projectileCount 1→**10**, cooldown 1→1.9/1.7) **ไม่เกี่ยว PR นี้ ควรแยก commit**

## เอกสารที่ต้องอ่าน (เรียงตามความสำคัญ)

| ไฟล์ | คืออะไร |
|---|---|
| [STATUS.md](STATUS.md) | **เขียนให้ผู้ใช้อ่าน ไม่ใช่ agent** — ถึงไหนแล้ว + อะไรค้างที่ตัวเขา · อัปเดตทุกครั้งที่จบ Round |
| [plan-server-state-and-reconnect.md](plan-server-state-and-reconnect.md) | **แผนหลัก** Stage 0-4 · งานถัดไปทั้งหมดอยู่ในนี้ |
| [audit-status.md](audit-status.md) | ผล audit ใหม่ + ลำดับความสำคัญ + สิ่งที่สแกนแล้วไม่มีปัญหา |
| [multi-ai-workflow.md](multi-ai-workflow.md) | วิธีทำงาน Claude วางแผน → Antigravity เขียน → unity-check ตรวจ |
| [playtest-checklist.md](playtest-checklist.md) | สิ่งที่ compile check จับไม่ได้ ต้องเล่นเทสต์ |
| [../AGENTS.md](../AGENTS.md) | กฎโปรเจกต์สำหรับ coder agent |
| [../.claude/commands/unity-loop.md](../.claude/commands/unity-loop.md) | ตัวคุมลูป (`/unity-loop`) |

## การตัดสินใจที่ล็อกแล้ว

- **co-op PvE เน้นเล่นกับเพื่อน** · matchmaking เปิด-ปิดได้ ยังไม่ตัดสิน
- **ต้องมี client reconnect และ host migration** ← ตัวนี้ทำให้ Stage 1 (server ถือ state) เลื่อนไม่ได้
- coder = **Antigravity** (Gemini CLI ใช้โควตา AI Pro ไม่ได้แล้ว Google ปิด OAuth 1 ก.ค. 2026)
- **ไม่ใช้ Distributed Authority** แม้ Unity แนะนำสำหรับ NGO migration — สวนทางกับ server-authoritative

## ยังไม่ตัดสิน (บล็อกงานถัดไป)

1. **Hit RPC throttling** — batch / throttle ต่อ enemy / predict ฝั่งคนยิง
   ต้องเลือกก่อนเริ่ม Stage 1 เพราะแตะ damage path เดียวกัน
2. **โลกในเกมหลัง host migrate** — สร้างใหม่ (แนะนำ) หรือเก็บทั้งหมด · ตอบตอน Stage 4

## งานถัดไป เรียงตามลำดับ

**1. ลูปที่เขียนแผนไว้แล้ว รอวางใน Antigravity** — `docs/handoff.md` + `docs/implementation_plan.md`
N1-N4: `Enemy.netMaxHealth` + `WorldHPBar` · orb guard · HitEffect ซ้อน · ClusterBomb gate (9 ไฟล์ งานกลล้วน)

**2. N5 `Time.timeScale` softlock** — ✅ **มีแผนแล้ว** [plan-n5-timescale.md](plan-n5-timescale.md) (Round 2, 5 ไฟล์)
**4** ระบบเขียน `timeScale` อิสระกัน ไม่ใช่ 3 — ตกไป `DevTools:124`
(`PauseMenuUI:85,102,112,119` คืนค่า 3 จุด · `SharedExperienceManager:225,234,291` · `WinLoseUI:121,128`)
ตัดสินใจแล้ว: `GamePause` static เจ้าของเดียว **flag set + enum ที่ derive จาก priority**
(`GameOver > PhaseSelect > PauseMenu > Playing`) · กด ESC ซ้อน card UI ได้ · DevTools slow-mo ผ่าน `ResumeScale`
เหตุผลที่ไม่ใช้ counter: การเรียกไม่สมดุล — `EndUpgradePhaseClientRpc` ยิงจาก 2 เส้นทาง (`:189` upgrade · `:313` orb)

**3. N6 FlowField bake ทุก client** — `FlowFieldPathfinder.cs:113` แก้บรรทัดเดียว
`Update()` มี server gate แล้ว แต่ `Start()` ไม่มี → 10,000 `Physics.CheckSphere` ฟรีบนทุก client

**4. Stage 0.1 buff ไม่ทำงานสำหรับ client**
`tempMoveSpeedBonus` · `tempHealthRegenBonus` (`playermove.cs:295,298`) ·
`tempAbilityHaste` · `tempDamageBonusMult` (`PlayerStatManager.cs:20,23`)
→ **อย่าแก้ด้วย NetworkVariable ทีละตัว** อ่าน `plan-server-state-and-reconnect.md` §0.1

**5. Stage 1 เป็นต้นไป** — server ถือ progression → reconnect → host migration

## กฎที่ต้องบังคับตั้งแต่วันนี้

> เพิ่มอาวุธ/สกิลใหม่ **ห้ามเพิ่ม ServerRpc ที่รับ `damage` เป็นพารามิเตอร์อีก**
> ตอนนี้มี ~25 ตัวที่ต้องรื้อใน Stage 1 · ถ้าเลี่ยงไม่ได้ให้ส่ง "ชื่ออาวุธ + level" แทนตัวเลขดิบ

## บทเรียนจากเซสชันนี้

- ลูป multi-agent **5 ลูป ไม่มี Fix Round เลย** — เพราะแผนระบุ *กับดัก* ไว้ล่วงหน้า
  (เช่น "เปลี่ยนเป็น HashSet แล้ว `FlowFieldPathfinder` จะพังด้วย")
  ไม่ใช่เพราะเครื่องมือดี
- **บั๊กที่หนักที่สุด compile check จับไม่ได้เลย** — Lance ไม่แสดง VFX, `MissingReferenceException`
  ทั้งคู่เจอจากการเล่นจริง โปรเจกต์ไม่มี automated test นี่คือช่องว่างที่ยังไม่มีอะไรแทน
- `unity-check.ps1`: cold **62 นาที** / warm **~41 วินาที** · Editor เปิดค้าง = exit 2 ไม่ใช่ compile error
