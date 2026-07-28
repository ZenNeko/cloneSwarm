# Multi-AI Workflow — แผนการใช้งานจริง

ระบบ: **Claude วางแผน + ตรวจ** → **Gemini เขียนโค้ด** → **Unity CLI ตัดสิน** → วนจนคอมไพล์ผ่าน
ค่าใช้จ่ายเพิ่ม = $0 (Claude กินโควตา Pro, Gemini กินโควตา Google)

---

## 1. แบ่งบทบาท

| Agent | ทำอะไร | ทำไมถึงเป็นตัวนี้ |
|---|---|---|
| **Claude** (`/unity-loop`) | ตัดสินใจสถาปัตยกรรม, เลือกไฟล์ที่ต้องแก้, เขียนสเปกระดับ signature, วิเคราะห์ compile error, ตรวจว่าละเมิดกฎ NGO ไหม | โควตาแพง ใช้กับงานที่ต้องคิด |
| **Coder** — เลือก 1 ใน 3 (ดูข้อ 5) | พิมพ์โค้ดตามสเปก, แก้หลายไฟล์รวดเดียว, แก้ error ที่ Claude วินิจฉัยแล้ว | แยกงานพิมพ์ออกจากงานคิด |
| **`unity-check.ps1`** | ตัดสินว่าโค้ดใช้ได้จริงไหม | AI สองตัวเถียงกันได้ compiler เถียงไม่ได้ |

**หลักการ**: Claude ไม่แตะ `.cs`, Gemini ไม่ตัดสินใจออกแบบ, ไม่มีใครเชื่อตัวเองจนกว่า compiler จะยืนยัน

---

## 2. ไฟล์ที่เป็นข้อต่อของระบบ

```
.claude/commands/unity-loop.md   ← Claude อ่าน: วิธีคุมลูป
GEMINI.md                        ← Gemini อ่านอัตโนมัติ: กฎโปรเจกต์ (NGO, VFXPool, SoundManager)
docs/implementation_plan.md      ← ทั้งคู่อ่าน/เขียน: สเปกงาน + ช่องติ๊ก [x]
scratch/errors.txt               ← Claude อ่าน: เฉพาะบรรทัด error ล้วน
```

`implementation_plan.md` คือช่องทางสื่อสารเดียวระหว่างสอง agent — ไม่มี API เชื่อมกัน ไม่มี state ร่วม
ถ้าไฟล์นี้เขียนไม่ชัด Gemini จะเดา แล้วลูปจะเสียรอบฟรี

---

## 3. รอบการทำงาน (1 ลูป)

```
Phase 0  เช็ค env       unity-check.ps1 -DryRun   (<1 วินาที)
Phase 1  Claude วางแผน  → docs/implementation_plan.md
Phase 2  Gemini เขียน   gemini --approval-mode auto_edit -p "..."
Phase 3  ตรวจ           unity-check.ps1  → exit 0/1/2
Phase 4  Claude วิเคราะห์ error → เขียน "Fix Round n" ต่อท้ายแผน → กลับ Phase 2
Phase 5  ปิดงาน         สรุป + บอกงานมือที่เหลือใน Editor
```

เพดาน 5 รอบ หยุดก่อนถ้า: error เดิมซ้ำ 3 รอบ (Gemini ติดลูป) หรือ error เพิ่มขึ้นทุกรอบ (กำลังทำพัง)

**exit 2 ไม่นับเป็นรอบล้มเหลว** — แปลว่า Unity Editor เปิดค้าง หรือหา Unity.exe ไม่เจอ ไม่ใช่ปัญหาโค้ด

---

## 4. งานแบบไหนควร / ไม่ควรเข้าลูป

### ✅ เหมาะ — งานมีแบบแผน ปริมาณเยอะ สเปกชัด

- สร้างอาวุธ/สกิลใหม่จาก template ที่มีอยู่ (`WeaponBase` subclass + level data)
- ไล่แก้ audit backlog หลายไฟล์ที่รูปแบบเดียวกัน (เช่น เปลี่ยน `Instantiate` → `NetworkedVFXPool` ทั้งโปรเจกต์)
- refactor เชิงกล: rename, extract method, ย้าย constant เข้า header
- แก้ compile error ที่วินิจฉัยแล้ว

### ❌ ไม่เหมาะ — overhead มากกว่าประโยชน์

- **แก้บรรทัดเดียว** — ลูปหนึ่งรอบกิน Unity re-import หลายนาที สู้แก้เองใน Editor ไม่ได้
- **งานที่ต้องออกแบบ NGO authority** — ตัดสินใจว่าอะไรควรอยู่ server/client เป็นงานคิด ไม่ใช่งานพิมพ์
- **งานที่ผลลัพธ์อยู่ใน Inspector** — สร้าง prefab, ผูก reference, สร้าง ScriptableObject, เพิ่มลง
  `DefaultNetworkPrefabs.asset` → Gemini ทำไม่ได้เลย ต้องคนทำ
- **บั๊ก multiplayer เชิงพฤติกรรม** — desync, race condition ตอน runtime → compile check จับไม่ได้
  ต้องเล่นทดสอบด้วย ParrelSync

> กฎง่ายๆ: ถ้าอธิบายสเปกให้ Gemini ยาวกว่าเขียนโค้ดเอง → อย่าใช้ลูป

---

## 5. งบโควตา

**Claude Pro** ~45 ข้อความ / 5 ชั่วโมง และ **แชร์ก้อนเดียวกับ claude.ai chat + Cowork**
1 ลูปเต็ม 5 รอบ ≈ 10–15 ข้อความ → **3–4 ลูปใหญ่ต่อหน้าต่าง**

วิธียืดโควตา:
- รวมงานหลายอย่างที่เกี่ยวข้องกันไว้ในลูปเดียว ดีกว่าแยกลูปละงาน
- ห้าม glob ทั้ง `Assets/` — ใช้ Grep หา symbol ก่อนแล้วค่อย Read เฉพาะไฟล์ (เขียนไว้ในกติกาแล้ว)
- งานเล็กแก้เองใน Rider/Editor อย่าเปิดลูป

### ⛔ Gemini CLI ใช้โควตา AI Pro ไม่ได้แล้ว (ยืนยัน 2026-07-28)

OAuth login ของ Gemini CLI ตอบกลับว่า:

> This client is no longer supported for Gemini Code Assist for individuals.
> To continue using Gemini, please migrate to the Antigravity suite of products.

Google ปิดทาง OAuth ของ Gemini CLI สำหรับบัญชีบุคคลทั่วไป ([issue #28229](https://github.com/google-gemini/gemini-cli/issues/28229),
เปิด 1 ก.ค. 2026, กระทบ v0.49.0 ซึ่งเป็นเวอร์ชันที่ติดตั้งอยู่, ยังไม่มี maintainer ตอบ)

**แปลว่าเป้าหมาย "ลูปอัตโนมัติเต็มรูปแบบ ฟรี" ทำไม่ได้ตอนนี้** เหลือทางเลือก:

| ทาง | โควตา | อัตโนมัติ? | ค่าใช้จ่าย |
|---|---|---|---|
| A. Gemini CLI + free API key | free tier **5 req/นาที** | ✅ เต็มรูปแบบ | $0 |
| ✅ **B. Antigravity IDE เป็น coder** ← **เลือกแล้ว** | Google AI Pro | ❌ ต้องสลับหน้าต่างเอง | $0 |
| C. Claude Code ทำเองทั้งหมด | Claude Pro | ✅ เต็มรูปแบบ | $0 |
| D. API key + เปิด billing / Vertex | สูง | ✅ | **จ่ายตาม token** |

**เลือก B** เพราะได้โควตา AI Pro เต็มโดยไม่จ่ายเพิ่ม และต้นทุนที่จ่ายแทนคือกดสลับหน้าต่างรอบละ ~10 วินาที
ซึ่งถูกกว่าการรอ rate limit 4-8 นาทีของทาง A มาก
ทาง A เก็บไว้ทำ **read-only scan** (5 req/นาทีพอไหว) · ทาง C ใช้เมื่องานเล็กหรือต้องตัดสินใจเชิงออกแบบ

Antigravity ไม่มี CLI shim (มีแค่ `Antigravity.exe` ตัว GUI) → สั่ง headless จาก Claude Code ไม่ได้
ทาง B จึงเป็น **manual handoff**: Claude เขียนแผน → คุณสลับไป Antigravity พิมพ์บรรทัดเดียว
→ กลับมาให้ Claude รัน `unity-check` แล้ววิเคราะห์

**1 prompt ≠ 1 request** — Gemini CLI เป็น agent วนเรียก model ทุกครั้งที่ใช้ tool
คำสั่งตรวจ 4 ไฟล์ = ~10-15 requests, รอบแก้โค้ดจริง = 20-40 requests
ที่ 5 req/นาที → **รอ 4-8 นาทีต่อรอบ** โดยยังไม่นับเวลา Unity

---

## 6. เริ่มใช้จริง

```bash
claude
```

แล้วพิมพ์:

```
/unity-loop <สิ่งที่อยากได้>
```

ตัวอย่างที่สเปกดีพอจะเข้าลูป:

```
/unity-loop แก้ Task 2.1-2.3 จาก audit: Burn DOT race condition ใน Enemy.cs,
ลบ ExperienceManager.cs แล้ว redirect fallback ไป SharedExperienceManager,
เปลี่ยน anonymous lambda ที่ subscribe OnValueChanged เป็น field delegate
```

ตัวอย่างที่**ไม่ควร**เข้าลูป (กว้างไป Claude จะเสียโควตาไปกับการสำรวจ):

```
/unity-loop ทำให้เกมดีขึ้น
```

---

## 7. แผนใช้งานช่วงแรก — เอา audit 21 ข้อเป็นงานตั้งต้น

แผนเดิมจาก Antigravity มี backlog 21 ข้ออยู่แล้ว เหมาะเป็นสนามทดสอบระบบ เพราะสเปกชัดและเป็นงานปริมาณ
แนะนำแบ่งเป็น 4 ลูป ตาม phase ไม่ใช่ลูปเดียวรวด (ถ้าพังจะหาสาเหตุไม่เจอ):

| ลูป | เนื้อหา | ความเสี่ยง |
|---|---|---|
| 1 | Phase 2 — memory/threading (Burn DOT, dead code, event leak, material leak) | ต่ำ เริ่มด้วยอันนี้เพื่อพิสูจน์ระบบ |
| 2 | Phase 3 — performance (HashSet, coroutine, timer accumulator, ExpOrb guard) | ต่ำ–กลาง |
| 3 | Phase 1 — security/authority (HP spoofing, EXP injection, card validation) | **สูง** แตะ RPC contract ต้องเทสต์ multiplayer จริง |
| 4 | Phase 4 — polish | ต่ำ |

commit หลังทุกลูปที่ผ่าน — จะได้ `git checkout` กลับได้ตอน Gemini ทำพัง

> หมายเหตุ: audit บางข้อในแผนเดิมทำไปแล้วใน commit `262bc9ec` (ExpOrb double collect, auditor tool)
> ลูปแรกควรให้ Claude เช็คก่อนว่าข้อไหนยังค้างจริง

---

## 8. ข้อจำกัดที่ต้องรู้

- **compile ผ่าน ≠ ทำงานถูก** — ทั้งระบบนี้ยืนยันได้แค่ว่าโค้ดคอมไพล์ผ่าน ตรรกะเกมและพฤติกรรม
  multiplayer ยังต้องเล่นทดสอบเอง โปรเจกต์นี้ไม่มี automated test
- **Unity Editor เปิดค้าง = ลูปรันไม่ได้** — batchmode แย่งล็อกไม่ได้ ต้องปิด Editor ก่อน
- **`Library/` cache คือตัวชี้ขาดเรื่องเวลา** (วัดจริง 2026-07-28)

  | สภาพ | เวลา |
  |---|---|
  | cold (Library ว่าง / เปลี่ยนเวอร์ชัน Editor) | **62 นาที** |
  | warm | **41 วินาที** |

  ต่างกัน ~90 เท่า → ห้ามเริ่มลูปตอน cache เย็น ให้รัน `scripts/unity-check.ps1` มือเปล่าครั้งหนึ่งก่อน
  แปลว่า **ตัวตรวจไม่ใช่คอขวด** คอขวดคือฝั่ง coder (Gemini free tier 5 req/นาที = 4-8 นาทีต่อรอบ)
- **Gemini แก้ไฟล์นอกแผนได้** — ลูปเช็คด้วย `git diff --stat` ทุกรอบ แต่ไม่ revert เอง จะรายงานให้ตัดสิน
- **สอง agent ไม่มี memory ร่วม** — Gemini ไม่รู้ว่ารอบก่อนคุยอะไรไว้ ทุกอย่างต้องอยู่ในไฟล์แผน
