# Handoff → Antigravity  (Round 7)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

รอบนี้ **10 tasks · 8 ไฟล์ (ใหม่ 2)** — เป็น**รอบฟีเจอร์** ไม่ใช่แก้บั๊ก
(backlog จาก audit เก็บไปเกือบหมดแล้วใน Round 1-6)

**ของหลัก: เลขดาเมจลอยขึ้นตอนตี**
- T1 `VFX/FloatingDamageText.cs` **ใหม่** — ตัวเลขลอย+จาง หันหน้าหากล้อง
- T2 `Enemy.cs` — เพิ่ม `float damage` เข้า `NotifyHitClientRpc` ที่มีอยู่
- T3 `VFX/FloatingDamageTextPool.cs` **ใหม่** — pool 200 ใบ
- T4 `Enemy.cs` — ต่อ RPC เข้ากับ pool · crit ต้องดูต่างชัด
- T5 กระจายตำแหน่งไม่ให้เลขทับกัน

**อื่นๆ**
- T6 `SharedExperienceManager.cs` — เล่นคนเดียวไม่ต้องนับถอยหลังตอนเลือกการ์ด
- T7 `ZoneObjective.cs` — log ตอนคืน spawn rate
- T8 ล้าง VFX key ตาย (`LanceThrust` `SlashAoE360` `VortexSpawn` `OrbiterHit`) → `"None"`
- T9 grep ตรวจปิดงาน (**ไม่แก้ไฟล์**) · T10 แก้คอมเมนต์ที่ล้าสมัย

## กฎที่สำคัญที่สุดของรอบนี้

> **ห้ามสร้าง ClientRpc/ServerRpc ตัวใหม่ในเส้นทาง damage เด็ดขาด**
> `NotifyHitClientRpc` ยิงทุกครั้งที่ศัตรูโดนตี และ audit ระบุว่าเป็นต้นทุน network
> ที่ใหญ่ที่สุดในเกม (รายงานจริง: solo คนเดียวมี `HitEffect` ทำงานพร้อมกัน **559 ชิ้น**)
> เลขดาเมจต้องอาศัย RPC ตัวเดิม **เพิ่มพารามิเตอร์ ไม่ใช่เพิ่มข้อความ**

**T2 เป็นข้อเดียวในรอบนี้ที่อนุญาตให้เปลี่ยน signature** ข้ออื่นห้ามทั้งหมด

## ข้อควรระวัง

**Round 3, 4, 5+6 มี scope creep รอบละครั้ง — สามครั้งติด**
รูปแบบ: Round 3 เปลี่ยน private field เป็น property · Round 4 เติม `else Destroy()` ·
Round 5+6 เปิด shader keyword บน `sharedMaterial` (= เขียนทับ asset ตอน runtime)
→ **ห้ามเพิ่ม `else` สำรอง ห้ามเพิ่ม fallback ห้ามเขียนทับ asset**
ครั้งที่สามอันตรายที่สุดเพราะเป็นการ**เลี่ยงข้อจำกัดด้วยวิธีที่แผนห้ามไว้ตรงๆ**
ถ้าติดจนทำตามแผนไม่ได้ **หยุดแล้วเขียนใต้ `## Questions`** อย่าหาทางอ้อม

**T1 ห้ามเรียก `Camera.main` ทุกเฟรม** — Round 5+6 เพิ่งไล่แก้เรื่องนี้ทั้งโปรเจกต์
ใช้รูปเดียวกับ `WorldHPBar.cs:102` · `FloatingDamageText` **ห้ามเป็น NetworkBehaviour**

**T3 ห้ามใช้ `NetworkedVFXPool`** — pool นั้น key ด้วย prefab และตั้งค่าใน `VFXDatabase` (`.asset` ห้ามแตะ)
และตัวเลขต้องตั้งข้อความต่างกันทุกใบ ซึ่ง MaterialPropertyBlock ทำไม่ได้

**T6 ห้ามลบ `ForceAutoPick`** — multiplayer ยังต้องใช้ ไม่งั้นคน AFK ค้างทั้งห้อง

**ห้ามแตะ 3 อย่างนี้**
- `MainBoss.GenerateLegacyConfig` — ต้องรันบน client จริงๆ (`OnPhaseChangedClient` อ่าน `config.phases`) **ไม่ใช่บั๊ก**
- `CrateSpawnManager` — เพิ่งแก้ รอเทสต์
- `PauseMenuUI` / `GamePause` — บั๊ก host กด ESC รอผู้ใช้ตัดสินใจ

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
