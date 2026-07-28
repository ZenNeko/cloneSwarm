# Playtest Checklist — หลังลูป 1-3 (2026-07-28)

compile ผ่านแล้ว แต่ **compile ผ่าน ≠ เกมถูก** — 3 ลูปที่ผ่านมาเปลี่ยนของที่ compiler จับไม่ได้เลย:
สีที่ส่งเข้า shader, การหยุด coroutine, ค่า HP ข้าม network, transform ของ pooled object

เรียงตามความเสี่ยง — ทำจากบนลงล่าง เจอพังตรงไหนหยุดแล้วบอกได้เลย

---

## 🔴 กลุ่ม 1 — เสี่ยงสูงสุด ทำก่อน

### 1.1 Telegraph zone เปลี่ยนสีได้จริงไหม  *(ลูป 1)*

เปลี่ยนจาก `r.material` (แต่ละ renderer มี material ของตัวเอง) เป็น **`sharedMaterial` ใช้ร่วมกันทั้งหมด**
+ ส่งสีผ่าน `MaterialPropertyBlock` แทน

**ถ้า shader ไม่รองรับ MPB สำหรับ property ที่ใช้ → สีจะไม่ขึ้นเลย หรือขึ้นแต่เปลี่ยนพร้อมกันหมดทุกโซน**

- [ ] MainBoss/MiniBoss ปล่อย AoE → วงเตือนไล่สี **warning → danger** ตามเวลา (ไม่ใช่ค้างสีเดียว)
- [ ] `_FillProgress` เดินจริง — วงค่อยๆ เติม ไม่ใช่กระโดดหรือนิ่ง
- [ ] **Donut** — โซนปลอดภัยตรงกลางเป็นสี **teal** ต่างจากวงอันตราย
- [ ] **Chase** — telegraph เป็นสี **magenta** แยกออกจาก AoE ปกติ
- [ ] ⚠️ **Color Match** — ผู้เล่นแต่ละคนเห็นสีของตัวเอง (แดง/น้ำเงิน/เขียว/เหลือง ตาม client ID)
      **ข้อนี้สำคัญที่สุดในลิสต์** — ถ้า MPB ไม่ทำงาน ทุกคนจะเห็นสีเดียวกัน กลไกพัง
- [ ] ⚠️ **มี telegraph หลายวงพร้อมกันที่ต้องคนละสี** → แต่ละวงต้องเป็นสีของตัวเอง
      ถ้าเปลี่ยนสีตามกันหมด = MPB ไม่ถูกใช้ กลับไปเขียนทับ shared material

### 1.2 Enemy หยุด spawn จริงไหม  *(ลูป 3)*

`InvokeRepeating` → coroutine — **coroutine ที่หยุดไม่ได้จะ spawn ไปเรื่อยๆ**

- [ ] enemy spawn ทุก ~`spawnRate` วินาทีเหมือนเดิม ไม่ถี่/ห่างผิดปกติ
- [ ] ครั้งแรกออกมาหลังเริ่มเกม ~0.5 วินาที
- [ ] wave เปลี่ยน → **rate เปลี่ยนตาม** (ไม่ใช่ค้าง rate เดิม หรือ spawn ซ้อน 2 ชุด)
- [ ] ⚠️ **Boss phase / จบเกม → spawn หยุดสนิท** ไม่มีตัวโผล่เพิ่ม
- [ ] ⚠️ **เล่นซ้ำรอบสอง** (กลับเมนู → เข้าเกมใหม่) → ไม่มี spawn ซ้อนจากรอบเก่า

### 1.3 HP ตรงตามตัวละครไหม  *(ลูป 2 — ต้อง multiplayer)*

server อ่าน `baseHealth` จาก `CharacterData` เอง โดยหา index ผ่าน `PlayerVisual.characters[]`
**ถ้า array ใน prefab ไม่ครบ/เรียงผิด → `IndexOfCharacter` คืน -1 → HP ไม่ถูกเซ็ตเลย**

- [ ] **เช็คก่อนเล่น**: `PlayerVisual.characters[]` บน `player.prefab` ใส่ครบ 5 ตัว
      (Brawler / Scholar / Hunter / Gunner / Riven) เรียงตรงกับ `CharacterSelectUI.characters`
- [ ] host + client เลือก **คนละตัวละคร** → HP ของแต่ละคนตรงกับ `baseHealth` ของตัวที่เลือก
- [ ] ⚠️ ดู **HP บนจอเพื่อน** ด้วย ไม่ใช่แค่จอตัวเอง (host path กับ client path คนละเส้นทาง)
- [ ] late-join: client เข้าห้องหลังเกมเริ่ม → HP ยังถูก
- [ ] ตาย → respawn → HP กลับมาเต็มตามตัวละคร ไม่ใช่ค่า default ของ prefab

---

## 🟠 กลุ่ม 2 — เทสต์คนเดียวได้

### 2.1 บั๊ก MissingReferenceException  *(ลูป 3 — บั๊กที่เจอตอนเทสต์)*

- [ ] เข้าเกม → เล่นจนมี ability (Hunter ult) → **ออกกลับ MenuScene**
      → Console **ไม่มี** `MissingReferenceException: 'HunterUltimate'` อีก
- [ ] ⚠️ **เข้าเกมใหม่อีกรอบ → ability ยังใช้งานได้** (กด Q/E/R ติด)
      null guard ต้องไม่ไปบล็อกการ re-enable ของ ability ที่ยังมีชีวิต

### 2.2 VFX ยังอยู่ถูกที่ ถูกขนาด  *(ลูป 3)*

pooled object ตอนคืน pool เปลี่ยนจาก `SetParent(null)` → `SetParent(transform, false)`
คือไปอยู่ **ใต้ GameObject ของ NetworkedVFXPool**

- [ ] ⚠️ **เช็คใน Inspector ก่อน**: GameObject `NetworkedVFXPool` ต้องมี
      **scale = (1,1,1) และ rotation = (0,0,0)**
      ถ้าไม่ใช่ → VFX ทุกตัวจะ**ยืด/หมุนเพี้ยน**ตามพ่อ (ของเดิมไม่มีพ่อเลยไม่โดน)
- [ ] ยิงอาวุธรัวๆ นานๆ → VFX ยังขึ้นทุกนัด ไม่หาย ไม่ค้างกลางจอ
- [ ] VFX ขึ้น **ตรงตำแหน่งที่โดน** ไม่ใช่ที่จุดกำเนิด pool
- [ ] Hierarchy: object ที่ใช้เสร็จแล้วอยู่ใต้ `NetworkedVFXPool` ไม่ลอยเกลื่อนที่ scene root

### 2.3 EXP ยังทำงาน  *(ลูป 1 — ลบ `ExperienceManager.cs`)*

- [ ] ฆ่า enemy → EXP orb ดรอป → เก็บได้ → หลอด EXP ขึ้น
- [ ] เลเวลอัพ → การ์ดขึ้นปกติ เลือกได้
- [ ] Console ไม่มี error เกี่ยวกับ missing script บน player

### 2.4 Lose condition  *(ลูป 3)*

- [ ] ตายหมดทุกคน → เกมจบภายใน **~2 วินาที** (ไม่ค้าง ไม่จบทันทีแบบผิดปกติ)

---

## 🟡 กลุ่ม 3 — regression เร็วๆ

### 3.1 Auto-aim / targeting  *(ลูป 3 — `List` → `HashSet`)*

`HashSet` **ไม่มีลำดับ** ต่างจาก `List` ที่เรียงตามลำดับ spawn
โค้ดที่วน `ActiveEnemies` แล้วหยิบตัวแรกที่เข้าเงื่อนไข อาจได้คนละตัวกับเมื่อก่อน

- [ ] อาวุธ auto-aim ยังเล็งเป้าใกล้สุดถูกต้อง ไม่สลับเป้ามั่วถี่ผิดปกติ
- [ ] Hunter funnel ยังยิงเป้าได้ปกติ
- [ ] enemy เดินเข้าหาผู้เล่นปกติ ไม่กองรวมกันแปลกๆ (crowd density ใน FlowField)

### 3.2 ทั่วไป

- [ ] เล่นต่อเนื่อง ~3-5 นาที ผ่านช่วง wave เปลี่ยน + mini boss → ไม่มี error ใหม่ใน Console
- [ ] FPS ไม่ตกกว่าเดิม (ลูป 3 ควรทำให้ **ดีขึ้น** ไม่ใช่แย่ลง)

---

## วิธีรายงานกลับ

เจอพัง บอกแค่ **ข้อไหน + เห็นอะไร** พอ เช่น
`1.1 Color Match — ทุกคนเห็นสีแดงหมด`
ผมจะไล่หาสาเหตุแล้วเปิดลูปแก้ให้

> เกือบทุกข้อในนี้ compile check จับไม่ได้ — นี่คือช่องว่างที่ใหญ่ที่สุดของ workflow multi-agent
> เพราะโปรเจกต์ไม่มี automated test
