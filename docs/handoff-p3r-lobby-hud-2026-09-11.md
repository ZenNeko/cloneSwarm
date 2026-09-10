# สเปก P3R — จอ Lobby และ Gameplay HUD · 2026-09-11

ที่มา: `Menu UI.dc.html` แท็บ `LOBBY` และ `GAMEPLAY HUD`
**สองจอนี้ไม่มีในเอกสาร handoff เดิม** (`design_handoff_ingame_screens/README.md` เขียนไว้แค่ 3 จอในเกม)
สเปกด้านล่างอ่านจากไฟล์แบบโดยตรง — เปิดดูได้ด้วย `py -m http.server` ในโฟลเดอร์นั้นแล้วเปิดในเบราว์เซอร์
(เปิดเป็น `file://` ไม่ได้ ปุ่มสลับจอเป็น template ที่ต้องรันด้วย `support.js`)

ทุกตัวเลขอ่านที่กรอบ **1920 × 1080** · design token ชุดเดียวกับ `P3RTheme` ที่ใช้อยู่

---

## สิ่งที่คนออกแบบฝากไว้เอง (จาก "NOTES FROM THE SOURCE")

1. **`barExtendLeft` เป็นค่าคงที่ 320 ใน `P3RTheme`** ทำให้แถบสั้นกว่าคำยาวอย่าง `TALENT SHOP`
   แบบ MAIN·A เลือกให้แถบกอดคำแทน (ความกว้างป้าย + 130 ที่ยื่นขวา) · MAIN·B คงแถบกว้างคงที่แล้วปล่อยคำล้น
   → **ยังไม่ได้ตัดสิน** ตอนนี้โค้ดเราเป็นแบบ B
2. **`#2CC5A0` ไม่ใช่สีใหม่** — เป็น `TalentShopUI.buyAffordableColor` ที่มีในรีโปอยู่แล้ว
   ส้ม `#F08654` คือ `talent_attack.tintColor` ใช้เฉพาะคำเตือนกับแถบร่ายเท่านั้น
3. **สิ่งที่ยังขาดจากฝั่งเรา** — พอร์เทรตของ Hunter กับ Gunner (ทั้งคู่ `portrait` ว่าง),
   ภาพพื้นหลังจริง, และ **คำตอบว่าลุค ALL-CAPS รอดภาษาไทยไหม**
   > คนออกแบบเขียนเองว่า *"it will not, and that decision changes the type scale on every screen"*
   > ตรงกับที่เราเจอตอนเรนเดอร์จริง และแก้ไปแล้วบางส่วนใน `P3RText` (commit `cbc5172a`)
   > แต่ **การปรับ type scale ชดเชยยังไม่ได้ทำ**

---

## จอ 1 · LOBBY

### โครง
```
แถบบน      CLONE SWARM (ซ้าย)          ฿ 8,420 G · COPY · 7K4M2P (ขวา)
แท็บ       [LOBBY] MAP  CHARACTER  SHOP
┌───────────────────────────────┬──────────────────────────────┐
│ ตัวละคร (ซ้าย ~56%)            │ PARTY · 3 / 4      2 READY   │
│  พอร์เทรต 880 × 752            │  แถวผู้เล่น × 3               │
│  YOUR CHARACTER               │  EMPTY SLOT · INVITE (เส้นประ)│
│  RIVEN            [CHANGE · C]│                              │
│  DUELIST · MELEE · HP · SPD   │ MAP                          │
│  [P RUNIC BLADE][Q VALOR][E …]│  พรีวิว + ARENA 01 · NORMAL  │
└───────────────────────────────┴──────────────────────────────┘
BACK (ล่างซ้าย)                        READY   START RUN (ล่างขวา)
```

### รายละเอียด
- **แถบบน** — ยอดทองเป็นชิปเขียว `#2CC5A0` · รหัสห้องเป็นปุ่ม `COPY · <code>` ตัว mono
- **แท็บ** — 4 แท็บ ตัวที่เลือกพื้นน้ำเงิน `#1824D8` ตัวอื่นพื้นโปร่ง
- **พอร์เทรต** — พื้นที่ลายทางเฉียงเป็น placeholder (`CharacterData.portrait` ว่างจริงในรีโป)
- **ชื่อตัวละคร** — ตัวใหญ่มาก หนัก 800 · ใต้ลงมาเป็นบรรทัดสเตตคั่นด้วย `·`
- **ชิปสกิล 3 อัน** — `[คีย์] ชื่อ` · คีย์อยู่ในกล่องเล็กพื้นเข้ม
  ⚠ คีย์ในแบบเขียน `Q` / `E` แต่โปรเจกต์ **ย้ายไปคลิกซ้าย/ขวาแล้ว** (commit `28154a4f`)
  ต้องอ่านป้ายจริงจาก `IHUDAbility.HUDKeyLabel` ไม่ใช่ฮาร์ดโค้ด
- **แถวผู้เล่น** — พอร์เทรตแถบยาว + `PLAYER n` + ป้าย `HOST` / `YOU` + `<ตัวละคร> · HP x / y`
  ขวาสุดเป็นสถานะ `READY` (teal) หรือ `PICKING…` (เทา)
- **ช่องว่าง** — กรอบเส้นประ `EMPTY SLOT · INVITE`
- **ปุ่ม** — `BACK` (กรอบ) · `READY` (กรอบ teal) · `START RUN` (พื้นน้ำเงินทึบ)

### แม็ปกับโค้ดที่มีอยู่
| แบบ | สคริปต์ |
|---|---|
| แท็บ 4 อัน | `Assets/Script/UI/TabBar.cs` (มีตัวเดียวในซีน ดู ADR-001) |
| แถวปาร์ตี้ · ready · รหัสห้อง | `Assets/Script/UI/LobbyUI.cs` |
| แผงตัวละคร + ชิปสกิล | `Assets/Script/UI/CharacterSelectUI.cs` + `CharacterCarousel` |
| พรีวิวแมพ | `Assets/Script/UI/MapSelectUI.cs` · **`MapCarousel.previewImage` ยังไม่ต่อสาย** |
| สถานะ ready ข้ามเน็ต | `LobbyState` (ห้ามเขียนจาก client ตรงๆ) |

### ที่ต้องระวัง
- **ADR-001 ยังไม่ปิด** — `Panel_Character` / `Panel_Shop` ยังเป็นลูกของ Canvas ไม่ใช่ `LobbyPanel`
  รีสกินก่อนย้ายจะต้องจัด layout สองรอบ
- `CharacterSelectUI.SelectedCharacter` เป็น static ที่ `PlayerWeaponManager` อ่านข้ามซีน — **ห้ามแตะ**
- ตัวระบุตัวละครบนเน็ตเวิร์กใช้ `CharacterData.characterName` (string) ห้ามใช้ index

---

## จอ 2 · GAMEPLAY HUD

จอที่หนาแน่นที่สุด และเป็นจอเดียวที่ **อ่านต้องทันระหว่างศัตรูเต็มจอ**

### โครง
```
บนซ้าย    [14:22] WAVE 31              (สแลบน้ำเงินเอียง)
บนกลาง    PURPLE ▇▇▇▇▇▇░░ 68%
          LICH ▇▇░  DUO ▇░  CAST ▇▇▇ (ส้ม)
บนขวา     OBJECTIVES
            HOLD THE ZONE      0:42
            COLLECT CORES      4 / 6
            ESCORT DONE          ✓   (จาง)
กลางจอ    ⚠ ENRAGE IN 12s!            (แบนเนอร์ส้ม)
กลางซ้าย  PLAYER 1 HUNTER 240/240
          PLAYER 2 GUNNER  REVIVE 6s  (แดง)
ล่างซ้าย  [HST][BRN 3][SHD]
          [LV 27]  181 / 275   +38
                   ▇▇▇▇▇░  EXP 62%
ล่างกลาง  [BLD][ARC][ORB][SPK]
          [ATK][HST][AOE]
ล่างขวา   [Q 3.4]  [E]        RMB / LMB
ล่างสุด   CHARGE ▇▇▇▇▇▇░ 78%   TAB STATS
```

### รายละเอียดสี
- แถบบอสหลัก **ม่วง** · แถบร่าย **ส้ม `#F08654`** (ตามโน้ต: ส้มใช้เฉพาะคำเตือนกับแถบร่าย)
- `REVIVE 6s` แดง · `ENRAGE` แบนเนอร์ส้มพื้นทึบตัวหนังสือเข้ม
- ช่องอาวุธ/augment มีสีตามประเภทเหมือนการ์ดเลเวลอัป (น้ำเงิน/เขียว/amber)
- สกิลที่ติดคูลดาวน์เป็นเทาพร้อมตัวเลขวินาที · ที่พร้อมใช้เป็นกรอบทอง

### แม็ปกับโค้ดที่มีอยู่
| แบบ | สคริปต์ |
|---|---|
| นาฬิกา + เวฟ | `GameTimeline` → `GameHUD` |
| แถบบอส + เฟส + มินิบอส | `Assets/Script/UI/BossHUDUI.cs` + `MiniBossBarEntry` |
| รายการ objective | `Assets/Script/UI/ObjectiveIndicatorUI.cs` (เพิ่งทำหลายอันใน `847aa0e0`) |
| แถวปาร์ตี้ระหว่างเล่น | `Assets/Script/UI/TempPartyHUD.cs` |
| บัฟลอย | `Assets/Script/UI/FloatingBuffUI.cs` |
| เลเวล/EXP/HP | `Assets/Script/UI/StatusHUDUI.cs` + `SharedExperienceManager` |
| ช่องอาวุธ/augment | `AbilityHUDUI` · `AugmentHUDUI` · `WeaponStatHUD` |
| แถบ CHARGE | `Assets/Script/UI/ChargeBarUI.cs` (Riven) |

### ที่ต้องระวัง — สำคัญกว่าจออื่นทั้งหมด
- **นี่คือโซน C** ตามที่ตกลงกันไว้รอบแรก: เอาได้แค่ **สี · ฟอนต์ · มุมบากเฉียง**
  **ห้าม** เฉือนทั้งแถบ HP · ห้าม halftone ทับตัวเลข · ห้ามโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด
- ก่อน/หลังต้องผ่าน **5-second test** ที่วางไว้: แคปจอตอนศัตรูเต็มจอ โชว์ 5 วิ แล้วถามว่า
  HP เหลือกี่ % / สกิลไหนพร้อม · ถ้าความถูกต้องตกแม้แต่นิด แปลว่าแรงไป
- ป้ายปุ่มสกิลต้องอ่านจาก `IHUDAbility.HUDKeyLabel` — แบบเขียน `Q`/`E` ซึ่งล้าสมัยแล้ว

---

## ลำดับที่แนะนำ

1. **Lobby ก่อน** — ปิด ADR-001 Action Item 4/5 แล้วค่อยรีสกิน ไม่งั้นจัด layout สองรอบ
2. **HUD ทีหลัง และทีละชิ้น** — เริ่มจากชิ้นที่ไม่ผูกกับความเร็วในการอ่าน
   (นาฬิกา · objective · แถบบอส) แล้วค่อยแตะ HP/สกิล/เลข
3. ทั้งสองจอสร้างเป็นซีนต้นแบบแยกด้วย Editor builder ตามแพตเทิร์นเดิม
   แล้วเรนเดอร์ตรวจด้วย `Tools > Clone Swarm > Capture P3R Proto Screens`

## กับดักที่เจอมาแล้ว — อย่าเหยียบซ้ำ

- **โหลด asset ก่อน `EditorSceneManager.NewScene()` ไม่ได้** — batchmode จะปลดทิ้งจนกลายเป็น fake-null
  ซีนออกมาไม่มีฟอนต์/สี โดย exit 0 ไม่มี error (กินเวลาไปสองวัน)
- **`EditorUtility.DisplayDialog` คืน false เสมอใน batchmode** — ต้องกันด้วย `!Application.isBatchMode`
- **`UnityEngine.UI.Outline` ใช้กับ graphic ที่ alpha 0 ไม่ได้** — ต้องวาดขอบสี่ด้านจริง
- **`UIShear` ใช้กับ TMP ไม่ได้** (BaseMeshEffect) · ถ้าประกอบรูปจากหลายชิ้นต้องใช้ `pivotOnParent`
- **letter-spacing ทำภาษาไทยแตก** — ใช้ `P3RText.SetTracking` เสมอ ห้ามเขียน `characterSpacing` ตรงๆ
