# ADR-001: โครงสร้าง UI ของ MenuScene

**Status:** Proposed
**Date:** 2026-08-08
**Deciders:** เจ้าของโปรเจกต์
**คำถามตั้งต้น:** "TabBar กับ panels ไม่ควรอยู่ใน Talent Shop หรือเปล่า"

> ทุกข้อในเอกสารนี้ยืนยันจาก `Assets/GameScenes/MenuScene.unity` ด้วย fileID จริง
> ไม่ได้เดาจาก `lobby-setup.md` — เอกสาร setup กับซีนจริง**ไม่ตรงกัน**หลายจุด

---

## Context

### แผนผังที่เอกสารบอก vs ที่ซีนเป็นจริง

`lobby-setup.md` §3–§4 บอกว่ามี TabBar สองตัว (ล็อบบี้ 4 แท็บ · ร้าน 2 แท็บ) และแต่ละหน้าจอ
มี controller ของตัวเอง **ซีนจริงไม่ใช่แบบนั้นเลย**

```
Canvas  (GameObject 1090413584 · active ตลอด ไม่เคยถูกปิด)
│
├── [Component] LobbyUI              ← กองอยู่บน Canvas
├── [Component] TalentShopUI         ← กองอยู่บน Canvas
├── [Component] CharacterSelectUI    ← กองอยู่บน Canvas
├── [Component] SettingsMenuUI       ← กองอยู่บน Canvas
│
├── MainPanel          2087758673
├── LobbyPanel          617942636 ──── TopPanel ──── Tabbar 134406115  ← TabBar ตัวเดียวในซีน
├── Panel_Character    1215575963
├── Panel_Shop          488045696     ← m_Children: []  ว่างเปล่า
├── (548266800)
├── (2000065609)
├── TalentShopPanel    1786704022
├── SettingsPanel      1192783358
└── LoadingPanel       1723364215

MenuManager อยู่บน GameObject แยก (1095722225)
```

### สายที่ต่อข้ามหน้าจอกัน

| ฟิลด์ | ค่าในซีน | คือของใคร |
|---|---|---|
| `TalentShopUI.tabBar` | `134406118` | **TabBar ของล็อบบี้** |
| `TalentShopUI.characterPanel` | `1215575963` | **Panel_Character ของล็อบบี้ ตัวเดียวกันเป๊ะ** |
| `TalentShopUI.talentPanel` | `0` | null |
| `TalentShopUI.characterGridContainer` | `0` | null |
| `TalentShopUI.characterTileTemplate` | `0` | null |
| `MenuManager.lobbyUI` | `0` | null |
| `MenuManager.loadingText` | `94555532` | **ตัวเดียวกับ `versionText`** |
| `MenuManager.goldText` | `0` | null |

---

## ปัญหาที่ตรวจเจอ

### F1 · controller ทั้งสี่กองบน Canvas → `OnEnable`/`OnDisable` ไม่มีความหมาย  ← รากของปัญหา

Canvas ไม่เคยถูกปิด แปลว่า `OnEnable`/`OnDisable` ของ `LobbyUI` `TalentShopUI` `CharacterSelectUI`
`SettingsMenuUI` ยิง**แค่ตอนเข้า/ออกซีน** ไม่ใช่ตอนเปิด/ปิดหน้าจอ ผลที่ตามมาเป็นบั๊กจริง:

| โค้ดที่เขียนไว้ | ตั้งใจให้เกิดตอน | จริงๆ เกิดตอน |
|---|---|---|
| `TalentShopUI.OnDisable → SaveManager.FlushIfDirty()` | ออกจากร้าน | ออกจากซีนเท่านั้น — **ซื้อ talent แล้วปิดเกมจากเมนู = เงินหาย** |
| `TalentShopUI.OnEnable → BuildTiles()` | เปิดร้านทุกครั้ง | ตอนโหลดซีนครั้งเดียว |
| `LobbyUI.OnEnable → Refresh()` | เปิดล็อบบี้ | ตอนโหลดซีนครั้งเดียว |
| `SettingsMenuUI.OnDisable → RemoveListener` | ปิดหน้า settings | ออกจากซีนเท่านั้น |

`LobbyUI` รอดมาได้เพราะบังเอิญมีทางที่สอง — `LobbyState.OnLobbyChanged` เรียก `Refresh()` ให้
ถ้าวันไหน `LobbyState` spawn ช้าหรือไม่ spawn ล็อบบี้จะไม่วาดใหม่เลย

> ข้อนี้ยังทำให้ **CLAUDE.md ข้อ 5** ("subscribe `OnEnable` / unsubscribe `OnDisable`")
> ไม่ได้ผลตามเจตนา — T73 ที่เพิ่งทำไปย้าย subscribe ของ `MenuManager` ไป `OnEnable` แล้ว
> แต่ `MenuManager` ก็อยู่บน GameObject ที่ active ตลอดเหมือนกัน พฤติกรรมจึงเท่ากับอยู่ใน `Start()`
> — ไม่ได้แย่ลง แต่ก็ไม่ได้ดีขึ้น

### F2 · `TalentShopUI.tabBar` / `talentPanel` / `characterPanel` เป็น dead field ที่ต่อผิดหน้าจอ  ← ตรงคำถาม

ทั้งสามฟิลด์ประกาศที่ `TalentShopUI.cs:28–30` แล้ว **ไม่มีเมธอดไหนในไฟล์อ่านเลยสักบรรทัด**
(`grep tabBar\|talentPanel\|characterPanel` ได้แค่บรรทัดประกาศ)

แย่กว่าไม่มี เพราะสองในสามถูกลากใส่ค่าไว้แล้ว และค่าที่ใส่คือ**ของหน้าจอล็อบบี้**:
ถ้าวันหนึ่งมีคนเขียนโค้ดมาใช้ฟิลด์พวกนี้ `TalentShopUI` กับ `TabBar` จะแย่งกันสั่ง
`SetActive` บน `Panel_Character` ตัวเดียวกัน

### F3 · ระบบปลดล็อกตัวละครในร้านตายสนิท

`BuildCharacterTiles()` (`TalentShopUI.cs:142`) `return` ทันทีเพราะ `characterGridContainer` และ
`characterTileTemplate` เป็น null → grid ไม่เคยถูกสร้าง

แปลว่า **ปลดล็อกตัวละครทำได้ทางเดียว** คือปุ่ม Confirm ใน `CharacterSelectUI` ที่แท็บล็อบบี้
ซึ่ง**ตรงกับข้อตัดสินใจที่ล็อกไว้แล้ว** ("เลือกตัวละคร = แท็บในล็อบบี้ที่เดียว")
→ โค้ดปลดล็อกใน `TalentShopUI` คือของซ้ำที่ไม่มีใครใช้

### F4 · แท็บ `shop` ในล็อบบี้ชี้ไปที่ panel ว่าง

`Panel_Shop` (488045696) มี `m_Children: []` — กดแท็บนี้ได้จอว่างเปล่า
ร้านจริงคือ `TalentShopPanel` (1786704022) ซึ่งเป็นคนละ GameObject เข้าได้จากปุ่มบนเมนูหลักเท่านั้น

### F5 · `loadingText` กับ `versionText` ชี้ Text ตัวเดียวกัน

ทั้งคู่ = `94555532` วันไหนมีโค้ดเขียน `loadingText` ป้ายเวอร์ชันล่างจอจะโดนทับ
เป็นกับดักคลาสเดียวกับ `joinStatusText` ที่ไปเขียนทับป้ายปุ่ม JoinLobby ซึ่ง handoff เคยเตือนไว้

### F6 · กลไกเปิด/ปิดจอมี 5 แบบพร้อมกัน

`MenuManager.ShowPanel()` (exclusive 5 ตัว) · `TabBar.Select()` (exclusive ในลิสต์) ·
`JoinRoomPanel` (modal จัดการเอง) · `LobbyUI.busyOverlay` (`SetActive` ตรงๆ) ·
`TalentShopUI.resetConfirmPanel` (`SetActive` ตรงๆ)

ไม่ผิดในตัวเอง แต่ไม่มีที่ไหนเขียนไว้ว่าอันไหนใช้ตอนไหน → คนต่อไปเดาผิดแน่

---

## Decision

**เลือก Option A** — คืน controller ให้ไปอยู่บนหน้าจอของตัวเอง แล้วลบของตายทิ้ง

---

## Options Considered

### Option A · แยก controller ลงหน้าจอของตัวเอง + ลบ dead field

| มิติ | ประเมิน |
|---|---|
| ความซับซ้อนโค้ด | **ต่ำ** — ลบอย่างเดียว ไม่เพิ่มคลาสใหม่ |
| งาน Editor | **ปานกลาง–สูง** — ย้าย component 4 ตัว ต้องต่อ Inspector ใหม่ |
| ความเสี่ยง | **ปานกลาง** — ย้าย component แล้ว reference หลุด ถ้าตกช่องจะ null เงียบ |
| ได้อะไร | `OnEnable`/`OnDisable` กลับมาหมายถึง "เปิด/ปิดหน้าจอ" จริง แก้ F1 ที่ราก |

**ทำอะไรบ้าง**
1. ย้าย `LobbyUI` → `LobbyPanel` · `TalentShopUI` → `TalentShopPanel` ·
   `CharacterSelectUI` → `Panel_Character` · `SettingsMenuUI` → `SettingsPanel`
   (ใช้ Copy Component → Paste Component As New เพื่อไม่ให้ reference หลุด)
2. ลบ `TalentShopUI.tabBar` / `talentPanel` / `characterPanel` /
   `characterGridContainer` / `characterTileTemplate` + `BuildCharacterTiles()` + `RefreshCharacterTile()`
3. ลบ `MenuManager.lobbyUI` (dead field)
4. แยก `loadingText` ออกจาก `versionText` ให้เป็นคนละ Text
5. ตัดสินเรื่องแท็บ `shop` (ดู "คำถามที่ยังต้องตอบ")

**ข้อดี** — แก้ที่ราก · ลบโค้ดมากกว่าเพิ่ม · ไม่ต้องเรียนรู้ pattern ใหม่ · ทำให้กฎ CLAUDE.md ข้อ 5 มีผลจริง
**ข้อเสีย** — งานมือใน Editor เยอะ (`TalentShopUI` มี ~25 ฟิลด์ · `CharacterSelectUI` ~20) ตกช่องเดียวก็ null เงียบ

### Option B · เขียน `UIScreen` base class + ScreenManager มี back stack

| มิติ | ประเมิน |
|---|---|
| ความซับซ้อนโค้ด | **สูง** — คลาสใหม่ + refactor ทั้ง 5 หน้าจอ |
| งาน Editor | สูงพอกับ A |
| ความเสี่ยง | **สูง** — เขียน framework ใหม่ตอนที่ยังไม่เคยเล่นจบเกมสักรอบ |
| ได้อะไร | back stack จริง (ESC ย้อนได้ทุกชั้น) · เพิ่มหน้าจอใหม่ง่ายขึ้น |

**ข้อดี** — โครงสะอาดที่สุด · แก้ F6 ด้วย (กลไกเดียวจบ)
**ข้อเสีย** — จ่ายค่า abstraction ก่อนรู้ว่าต้องการจริงไหม · โปรเจกต์นี้มี 5 หน้าจอ ไม่ใช่ 30 ·
บทเรียนของโปรเจกต์เองบอกว่าบั๊กหนักทุกตัว compile จับไม่ได้ การรื้อใหญ่ตอนนี้เพิ่มพื้นที่เสี่ยงโดยไม่จำเป็น

### Option C · ไม่ย้ายอะไร แก้เฉพาะจุดที่พัง

| มิติ | ประเมิน |
|---|---|
| ความซับซ้อนโค้ด | ต่ำสุด |
| งาน Editor | น้อยสุด |
| ความเสี่ยง | ต่ำ |
| ได้อะไร | อาการหาย รากยังอยู่ |

**ทำอะไร** — ลบ dead field · เรียก `SaveManager.FlushIfDirty()` ตอนกด Back ในร้านแทนที่จะพึ่ง `OnDisable` ·
แยก `loadingText`

**ข้อดี** — ถูกและเร็ว ไม่เสี่ยง reference หลุด
**ข้อเสีย** — **`OnEnable`/`OnDisable` ยังโกหกต่อไป** ทุกคนที่เขียนโค้ดใหม่บนหน้าจอเหล่านี้จะตกหลุมเดิม
และ CLAUDE.md ข้อ 5 จะยังเป็นกฎที่บังคับใช้ไม่ได้จริง

---

## Trade-off Analysis

**A กับ C ต่างกันที่เดียว: จะปล่อยให้ `OnEnable` โกหกต่อไปไหม**

ตอนนี้มีจุดที่พึ่ง `OnEnable`/`OnDisable` แล้วเสียหายจริง 1 จุด (`FlushIfDirty` → เงินหาย) และ
จุดที่รอดมาได้ด้วยความบังเอิญอีก 2 จุด ถ้าเลือก C ต้องเขียนกำกับไว้ทุกไฟล์ว่า "อย่าใช้ `OnEnable`
บนหน้าจอเมนู" ซึ่งเป็นกฎที่ขัดกับ CLAUDE.md ข้อ 5 และคนจะลืม

**B แพงเกินขนาดปัญหา** — 5 หน้าจอไม่คุ้มกับ framework และโปรเจกต์ยังไม่เคยเล่นจบเกมสักรอบ
(handoff บอกเอง) การรื้อโครงตอนที่ยังไม่รู้ว่าของเดิมเล่นได้จริงไหม คือเพิ่มตัวแปรผิดจังหวะ

**ค่าใช้จ่ายจริงของ A คืองาน Editor ไม่ใช่โค้ด** — และลดได้ด้วย Copy/Paste Component As New
ซึ่ง Unity คงค่า reference ให้ครบ ทำทีละหน้าจอ ตรวจทีละหน้าจอ ไม่ต้องทำรวดเดียว

---

## Consequences

**ง่ายขึ้น**
- เขียนโค้ดหน้าจอใหม่ได้ตามสัญชาตญาณ — `OnEnable` = เปิดหน้านี้
- ลบโค้ดตายได้ ~60 บรรทัดใน `TalentShopUI` + 4 dead field
- กฎ CLAUDE.md ข้อ 5 กลายเป็นกฎที่บังคับใช้ได้จริง

**ยากขึ้น / ต้องระวัง**
- ต้องต่อ Inspector ใหม่ 4 หน้าจอ — ตกช่องไหนจะเป็น `NullReferenceException` ตอนรัน ไม่ใช่ตอนคอมไพล์
- `CharacterSelectUI.SelectedCharacter` เป็น static ที่ `PlayerWeaponManager` อ่านข้ามซีน —
  ย้าย component ได้ แต่**ห้ามแตะ static นั้น**

**ต้องกลับมาดูอีก**
- F6 (5 กลไกเปิด/ปิดจอ) — A ไม่ได้แก้ ถ้าวันหนึ่งหน้าจอเกิน 8 อันค่อยพิจารณา B
- ESC ในเมนูยังไม่มี — ถ้าจะทำ back stack จริงค่อยยกเป็น ADR-002

---

## คำถามที่ยังต้องตอบก่อนลงมือ

**Q1 · แท็บ `shop` ในล็อบบี้จะเอายังไง** — ตอนนี้ชี้ไป panel ว่าง เลือกได้ 3 ทาง
- **ลบแท็บทิ้ง** → ล็อบบี้เหลือ 3 แท็บ (client เหลือ 2) ร้านเข้าจากเมนูหลักอย่างเดียว
- **ย้ายร้านเข้าเป็นแท็บจริง** → ซื้อของระหว่างรอเพื่อน ready ได้ (ทรงเดียวกับ Deep Rock / RoR2)
  แต่ต้องเอา `TalentShopPanel` ออกจากกลุ่ม `ShowPanel()` แล้วยกไปเป็นลูก `LobbyPanel`
  และปุ่มร้านบนเมนูหลักต้องหายไป ไม่งั้นกลับมาเป็นสองเจ้าของอีก
- **คงไว้แล้วใส่ของ** → ทำ panel ร้านย่อในล็อบบี้แยกจากร้านใหญ่ — **ไม่แนะนำ** ซ้ำซ้อนอีกชั้น

**Q2 · ปลดล็อกตัวละครอยู่ที่เดียวใช่ไหม** — ถ้าใช่ ลบโค้ดปลดล็อกใน `TalentShopUI` ได้เลย
ถ้าอยากให้ปลดล็อกจากร้านได้ด้วย ต้องสร้าง grid จริงและตัดสินว่า `CharacterSelectUI.OnConfirm`
จะเหลือหน้าที่อะไร

---

---

## Amendment — 2026-08-08 (หลังผู้ใช้ตัดสิน + ตรวจเพิ่ม)

**Status เปลี่ยนเป็น: Accepted (ฉบับแก้)**

### ผู้ใช้ตัดสิน Q1 · Q2 แล้ว

- **Q1** → ปุ่ม Talent Shop กับแท็บ `shop` เรียก **panel เดียวกัน** · กดปุ่มร้านแล้ว TabBar
  **เหลือ 2 แท็บ** (`shop` · `character`) → ออกแบบเป็น **Hub เดียวสองโหมด** ดู `implementation_plan.md` Round 7
- **Q2** → ปลดล็อกตัวละคร **ที่เดียว** คือ `CharacterSelectUI` · ลบของซ้ำใน `TalentShopUI`

### แก้ Option A — ย้าย component แค่ตัวเดียว ไม่ใช่สี่ตัว

ข้อความเดิมใน Option A ("ย้าย controller ทั้งสี่ลงหน้าจอของตัวเอง") **ผิด** ตรวจเพิ่มแล้วพบว่า:

| Component | ย้ายไหม | เหตุผล |
|---|---|---|
| `CharacterSelectUI` | **ห้ามย้าย** | `Awake():91` เซ็ต `SelectedCharacter = FirstUnlocked()` — `Awake` ไม่ทำงานบน GameObject ที่ inactive ถ้าย้ายไปอยู่บน `Panel_Character` ที่ปิดอยู่ ผู้เล่นที่ไม่เปิดแท็บตัวละครจะเริ่มเกมโดยไม่มีตัวละคร |
| `TalentShopUI` | **ย้าย** | ตัวเดียวที่พึ่ง `OnDisable` จริง (`SaveManager.FlushIfDirty()` → บั๊กทองหาย) และ `OnEnable → BuildTiles()` |
| `LobbyUI` | ไม่จำเป็น | `SetMode()` เรียก `Refresh()` ให้ตรงๆ อยู่แล้ว |
| `SettingsMenuUI` | ไม่จำเป็น | `OnEnable` `AddListener` / `OnDisable` `RemoveListener` เข้าคู่กัน ไม่มีอาการ |

### แก้ Action item เรื่อง `MenuManager.lobbyUI`

เดิมเขียนว่าเป็น dead field ให้ลบ — **แบบใหม่ต้องใช้** เป็นทางเรียก `LobbyUI.SetMode()`
ตอนนี้เป็น `fileID: 0` ในซีน ต้องลากใส่ใน Inspector

### พบเพิ่มหลังเขียนฉบับแรก

- **F7 · `Panel_Character` และ `Panel_Shop` เป็นลูกของ Canvas ไม่ใช่ลูกของ `LobbyPanel`**
  → `ShowPanel(mainPanel)` ปิดแค่ `LobbyPanel` แท็บที่เปิดค้างจะลอยทับหน้าเมนูหลัก
  ปุ่ม Back จาก Round 6 จะดูเหมือนพังทันทีที่ทดสอบ
- **F8 · ซีนบน working tree สูญ GameObject 3 ตัวเทียบกับ `467970d7`**
  `partyContainer` (`1419853810`) · `roomCodeLabel` (`1952422931`) · `copyCodeButton` (`342843837`)
  — ทั้งสามไม่มีอยู่ในซีนแล้ว ไม่ใช่แค่หลุดสาย
  `partyContainer` เป็น null ทำให้ `LobbyUI.Refresh()` **ข้ามลูปวาดแถวปาร์ตี้ทั้งก้อน** →
  ล็อบบี้ว่างเปล่าไม่ว่าเน็ตจะดีแค่ไหน · เป็นสาเหตุที่**ซ้อนอยู่คนละชั้น**กับบั๊ก `currentSession`
  ที่ `SESSION-HANDOFF.md` ตามอยู่
  **ผู้ใช้เลือกสร้างใหม่เองตอนจัด Hub** (ไม่ `git checkout` ซีนกลับ)
- **F9 · ของเก่าค้างระดับ Canvas** — `Panel_JoinLobby` (`2000065609`) แทนที่ด้วย `JoinRoomPanel` แล้ว ·
  `Panel_Map` (`548266800`) เป็นตัวซ้ำที่ TabBar ไม่ได้ชี้ถึง

---

## Action Items

1. [x] ตอบ Q1 · Q2
2. [ ] ลบ dead field + โค้ดปลดล็อกซ้ำใน `TalentShopUI` (งานโค้ด — เข้ารอบ Antigravity ได้)
3. [ ] ลบ `MenuManager.lobbyUI` (งานโค้ด)
4. [ ] ย้าย component 4 ตัวลงหน้าจอของตัวเอง (**งานมือ Editor ทีละหน้าจอ**)
5. [ ] แยก `loadingText` ออกจาก `versionText` (งานมือ Editor)
6. [ ] แก้ `docs/lobby-setup.md` ให้ตรงกับซีนจริง — ตอนนี้บอกว่ามี TabBar 2 ตัวแต่มีจริงตัวเดียว
7. [ ] หลังย้ายเสร็จ เทสต์: เปิดร้าน → ซื้อ talent → กด Back → **ปิดเกมทันที** → เปิดใหม่ ทองต้องหายไปจริง
