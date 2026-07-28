# ถึงไหนแล้ว

> ไฟล์นี้เขียนให้ **คุณ** อ่าน ไม่ใช่ให้ agent อ่าน — ตอบคำถามเดียวคือ "ตอนนี้ถึงไหน แล้วต้องทำอะไรต่อ"
> รายละเอียดทางเทคนิคอยู่ไฟล์อื่น ที่นี่เก็บแค่สถานะ
>
> อัปเดตล่าสุด **2026-07-29**

---

## 🔴 ค้างที่ตัวคุณ — ไม่มีใครทำแทนได้

เรียงตามความสำคัญ ไม่ใช่ตามลำดับเวลา

### 1. Round 1 **ยังไม่ได้เทสต์เป็น client** ← สำคัญสุด

Round 1 แก้บั๊กหลอดเลือดบอสที่ **โผล่เฉพาะบนจอ client** ที่ผ่านมาเล่นเป็น host ตลอด
ซึ่งเป็นเหตุผลเดียวกับที่บั๊กนี้รอดมาได้นาน — **เล่นเป็น host เท่ากับไม่ได้เทสต์**

ต้องเปิด 2 instance แล้วดูจากจอ **client**:
- หลอดเลือด mini-boss ต้องลดลงเรื่อยๆ ไม่ใช่เต็มค้างแล้วฮวบท้าย
- ตัวเลขต้องเป็น `90 / 150` ไม่ใช่ `150 / 30`
- ดูทั้ง **หลอดในโลก** และ **แถบใน HUD** ต้องตรงกันทั้งคู่
- ลองกับ Elite ที่มี Shield ด้วย

ที่เหลือดูที่ `## Manual Steps` ท้าย [implementation_plan.md](implementation_plan.md)

### 2. วาง Round 3 ใน Antigravity

ก๊อปจาก [handoff.md](handoff.md) → วางใน Antigravity → รอมันบอก "เสร็จ" → กลับมาบอกผม

**ค้างเทสต์สะสม 2 รอบแล้ว** — Round 1 (หลอดเลือด client) และ Round 2 (ESC softlock · FlowField)
compile ผ่านทั้งคู่แต่ยังไม่มีใครเห็นด้วยตา ยิ่งกองยิ่งหาต้นตอยากถ้าพัง

### 3. `NetworkAnimator` บนบอส — เลื่อนไว้เอง

`Purple 1.prefab` มี `NetworkAnimator` ที่ไม่ได้ assign Animator → **error 1 อันต่อเฟรม**
ตลอดที่บอสมีชีวิต (590 อันในรอบที่เล่น) คุณบอกว่ายังเป็น mock-up art เลยพักไว้

พอ art นิ่งแล้วเลือก: ถอด component ทิ้ง (มันไม่ได้ sync อะไรอยู่แล้ว) หรือ assign + ตั้ง `ParameterEntries`

### 4. เทสต์ VFX pool report

เข้าเกม → เล่นถึงช่วงหนักสุด (wave 30 + บอส) → **กด F1** ดูส่วน VFX Pool → กด **Log VFX Report**
เอาคอลัมน์ "ควรตั้ง" ไปใส่ `poolSize` ใน `VFXDatabase`

⚠️ เลขที่ได้จาก solo **ต่ำกว่าความจริง** — ถ้าจะ bake ลง asset ควรวัดตอน co-op

### 5. ตัดสินใจ 2 เรื่องที่บล็อกงานใหญ่

| เรื่อง | ต้องตอบเมื่อไร |
|---|---|
| **Hit RPC throttling** — batch / throttle ต่อ enemy / ให้คนยิง predict | **ก่อนเริ่ม Stage 1** เพราะแตะ damage path เดียวกัน ตอบช้า = ทำซ้ำ |
| **โลกในเกมหลัง host migrate** — สร้างใหม่ (แนะนำ) หรือเก็บทั้งหมด | ตอน Stage 4 ยังไม่รีบ |

---

## 📦 สถานะ repo

- branch **`audit-fixes-2026-07`** · **17 commits** เหนือ `main`
- [PR #10](https://github.com/ZenNeko/cloneSwarm/pull/10) เปิดอยู่ ยังไม่ merge
- **ยังไม่ push 1 commit** (`24d004b8` แผน Round 2)
- PR title ยังเป็นของรอบเก่า — เนื้อในครอบคลุมมากกว่านั้นแล้ว ควรแก้ก่อน merge

**ไฟล์ที่คุณแก้ค้างไว้ ผมไม่แตะ** — งานบาลานซ์สาย Spike
```
Assets/Prefab/Weapon/Super/SplitSpikeWeapon.prefab
Assets/Script/Data/WeaponData/Super/WD_SplitSpike.asset
Assets/Script/Data/WeaponData/WD_Spike.asset
```

---

## 🔁 Round — ลูป Antigravity

| Round | ทำอะไร | สถานะ |
|---|---|---|
| **1** | หลอดเลือดบอสผิดบน client · orb เก็บซ้ำ · HitEffect ซ้อน · ClusterBomb ระเบิดทั่วแมพ | ✅ เสร็จ · compile ผ่าน · **ยังไม่ได้เทสต์ client** |
| **2** | `Time.timeScale` softlock (กด ESC แล้วเกมค้าง) · FlowField bake บนทุก client | ✅ เสร็จ · compile ผ่าน · **ยังไม่ได้เล่นเทสต์** |
| **3** | P1+P3 ที่ค้าง — crit flag · win ยิงซ้ำ · Survive quest ไม่มีวันจบ · orb auto-pick · ฯลฯ | 📋 แผนพร้อม รอวางใน Antigravity |
| 4+ | ยังไม่เขียน | — |

Round 3 **ตั้งใจให้ใหญ่กว่าเดิม** (10 tasks/12 ไฟล์ เทียบกับ 6/10 และ 7/6) เพื่อวัดเพดานของ Antigravity

**นอก Round** (ผมทำตรงๆ ไม่ผ่านลูป): VFX pool report + DevTools F1 · แก้ path scene ผิดใน `CLAUDE.md`

---

## 🗺 แผนระยะยาว — ประมาณ ไม่ใช่สัญญา

เป้าหมายปลายทาง: **client reconnect** และ **host migration**

| กอง | ประมาณ | หมายเหตุ |
|---|---|---|
| เศษ audit ที่ค้าง (P1/P3 + config) | ~2–3 Round | ทำเมื่อไรก็ได้ ไม่บล็อกใคร |
| Stage 0.1 buff ไม่ทำงานสำหรับ client | ~1 Round | บั๊กที่คนเล่นเจอจริง |
| **Stage 1 server ถือ player state** | **~5 Round** | **รากของทุกอย่าง** · ก้อนใหญ่สุด · ปิด audit ค้างหลายข้อในตัว |
| Stage 2 reconnect | ~1–2 Round | ได้ฟีเจอร์แรก |
| Stage 3 run snapshot | ~1–2 Round | |
| Stage 4 host migration | ~2–3 Round | ได้ฟีเจอร์ที่สอง |

**รวม ~12–16 Round** โดย Stage 1 กินเกือบครึ่ง

⚠️ ตัวเลขนี้เชื่อได้แค่หยาบๆ — Round 3 เป็นต้นไปยังไม่มีใครเขียน และสองรอบที่ผ่านมา
**ขอบเขตโตขึ้นทุกครั้งพอลงไปอ่านโค้ดจริง** (N1 จาก 9 ไฟล์เป็น 10 · N5 จาก 3 ไฟล์เป็น 4 ·
N6 จาก "แก้บรรทัดเดียว" กลายเป็นต้องรื้อลำดับ ไม่งั้น enemy ยืนนิ่งทั้งแมพ)

รอ Round 2 จบก่อนแล้วค่อยเอาเวลาจริงมาคูณ จะได้ตัวเลขที่ใช้วางแผนได้

---

## 📖 ไฟล์ไหนอ่านเมื่อไร

| อยากรู้ว่า... | อ่าน |
|---|---|
| ตอนนี้ถึงไหน | **ไฟล์นี้** |
| รอบนี้ Antigravity ต้องทำอะไร | [handoff.md](handoff.md) → [implementation_plan.md](implementation_plan.md) |
| มีบั๊กอะไรค้างอยู่บ้าง | [audit-status.md](audit-status.md) |
| แผนใหญ่ reconnect / host migration | [plan-server-state-and-reconnect.md](plan-server-state-and-reconnect.md) |
| เปิดเซสชันใหม่กับ AI ต้องเล่าอะไร | [SESSION-HANDOFF.md](SESSION-HANDOFF.md) |
| ต้องเล่นเทสต์อะไรบ้าง | [playtest-checklist.md](playtest-checklist.md) |

---

## 💡 บทเรียนที่แลกมาแล้ว — อย่าลืม

- **compile ผ่านไม่ได้แปลว่าถูก** — บั๊กที่หนักที่สุดทุกตัวจนถึงตอนนี้ compile จับไม่ได้เลยสักตัว
  โปรเจกต์ไม่มี automated test การเล่นเทสต์คือตาข่ายเดียวที่มี
- **เล่นเป็น host = ไม่ได้เทสต์** ครึ่งหนึ่งของบั๊กที่เจอโผล่เฉพาะบน client
- **แก้บั๊กเสร็จต้อง grep หาไฟล์รูปแบบเดียวกันก่อนปิดงาน** — โปรเจกต์นี้มีไฟล์โครงเหมือนกันเป็นตระกูล
  และเคยแก้ตกพี่น้องมาแล้ว **5 ครั้ง** (ครั้งล่าสุดคือ `MiniBossBarEntry` ที่แผน Round 1 เขียนเตือน
  เรื่องนี้ไว้เองแล้วยังตกอยู่ดี)
- **audit ที่อ่านแต่โค้ดมองไม่เห็นบั๊กใน `.prefab`** — สองบั๊กจากการเล่นจริงล่าสุดไม่มีตัวไหนอยู่ในโค้ด
