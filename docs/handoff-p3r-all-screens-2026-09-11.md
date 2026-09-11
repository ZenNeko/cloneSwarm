# สเปก P3R — ครบทั้ง 12 จอ · 2026-09-11

ที่มา: `C:\Users\choro\Downloads\Menu UI mockups\design_handoff_ingame_screens\Menu UI.dc.html`
เอกสาร `README.md` ที่แนบมากับไฟล์แบบเขียนไว้แค่ **3 จอในเกม** (Level Up / Win-Lose / Pause)
อีก **9 จอไม่มีสเปกเขียนไว้เลย** — ไฟล์นี้เก็บสิ่งที่อ่านได้จากไฟล์แบบโดยตรง

**วิธีเปิดดูไฟล์แบบ** — เปิดเป็น `file://` ไม่ได้ ปุ่มสลับจอเป็น template ที่ต้องรันด้วย `support.js`
```bash
cd "C:/Users/choro/Downloads/Menu UI mockups/design_handoff_ingame_screens" && py -m http.server 8791
```
แล้วเปิด `http://127.0.0.1:8791/Menu UI.dc.html`

ทุกตัวเลขอ่านที่กรอบ **1920 × 1080**

---

## สถานะ

| # | จอ | เรา | ซีนต้นแบบ |
|---|---|---|---|
| 1 | TITLE | ✅ | `Proto_Title.unity` |
| 2 | MAIN MENU | ✅ | `Proto_P3RMenu.unity` |
| 3 | CHARACTER | ✅ | `Proto_Character.unity` |
| 4 | LOBBY | ✅ | `Proto_Lobby.unity` |
| 5 | MAP SELECT | ✅ | `Proto_MapSelect.unity` |
| 6 | TALENT SHOP | ✅ | `Proto_TalentShop.unity` |
| 7 | CONFIG | ✅ | `Proto_Config.unity` |
| 8 | LOADING | ✅ | `Proto_Loading.unity` |
| 9 | GAMEPLAY HUD | ✅ ภาพอ้างอิง | `Proto_GameplayHUD.unity` |
| 10 | LEVEL UP | ✅ | `Proto_LevelUp.unity` |
| 11 | WIN / LOSE | ✅ | `Proto_WinLose.unity` |
| 12 | PAUSED | ✅ | `Proto_Pause.unity` |

---

## Design tokens

ตรงกับ `P3RTheme` ที่ใช้อยู่ · รายละเอียดเต็มอยู่ใน `README.md` ของโฟลเดอร์แบบ

| ชื่อ | ค่า |
|---|---|
| Ink / Ink deep | `#0A0E1E` / `#060812` |
| Card / Panel | `#111838` / `#0D1226` |
| Primary | `#1824D8` |
| Gold / Amber | `#FFE633` / `#D99A1A` |
| Blue slot / Green | `#4073D9` / `#4DA659` |
| Teal | `#2CC5A0` — **ไม่ใช่สีใหม่** เป็น `TalentShopUI.buyAffordableColor` เดิม |
| Orange | `#F08654` — `talent_attack.tintColor` **ใช้เฉพาะคำเตือนกับแถบร่าย** |
| Red / Wash lose | `#D82020` / `#5A0E0E` |
| Muted ink | `#8C8678` |

**รูปทรง** — ไม่มีมุมโค้งเลย · ปุ่ม/แถบเอียง `skewX(-9deg)` = `UIShear.angleDegrees = 9`
เส้นเน้นซ้าย `border-left: 6px` เป็นลายเซ็นของการ์ดทุกใบ

**ตัวหนังสือ** — Sarabun 800 หัวเรื่อง/ตัวเลข · 400/600 เนื้อความ · ป้ายระบบใช้ mono
โปรเจกต์ไม่มีฟอนต์ mono จริง → ใช้ Sarabun-SemiBold + ถ่างระยะแทน

---

## 1 · TITLE

**ไม่มีอะไรรองรับในเกมเลย** — ไม่มีทั้งซีนและสคริปต์ ต้องสร้างใหม่ทั้งหมด

```
พื้น: ภาพ title render เต็มจอ + ลายทับ
แถบน้ำเงินแนวตั้งชิดขอบซ้าย
บนซ้าย   CLONE SWARM  (เล็ก)
           CO-OP  SURVIVOR  ARENA  (mono ถ่างมาก)
กลางซ้าย  PRESS
          ANY        ← สามบรรทัด ตัวโครงร่าง ใหญ่มาก ชิดซ้าย
          KEY
ล่างซ้าย  v0.8.2
ล่างขวา   © 2026 CLONE SWARM
```

**ต้องทำใหม่:** ซีน/พาเนล Title · ตัวรับ "กดปุ่มอะไรก็ได้" · เส้นทางไป Main Menu
**คำถามค้าง:** จะเป็นซีนแยก หรือเป็น panel แรกใน `MenuScene`? ถ้าเป็น panel ต้องเข้ากลุ่ม `MenuManager.ShowPanel()`

---

## 2 · MAIN MENU ✅

ทำแล้ว — `Proto_P3RMenu.unity`

**ข้อที่คนออกแบบฝากไว้และเรายังไม่ได้ตัดสิน:**
`barExtendLeft` เป็นค่าคงที่ 320 ทำให้แถบสั้นกว่าคำยาวอย่าง `TALENT SHOP`
- **MAIN · A** — แถบกอดคำ (ความกว้างป้าย + 130 ที่ยื่นขวา)
- **MAIN · B** — แถบกว้างคงที่ ปล่อยคำล้น ← **ตอนนี้โค้ดเราเป็นแบบนี้**

ทั้งสองแบบอยู่ในไฟล์ `Menu UI - In-Game v1 -A-B-C variants-.dc.html`

---

## 3 · CHARACTER

```
แถบบน   CLONE SWARM              ฿ 8,420 G · COPY · 7K4M2P
แท็บ    LOBBY  MAP  [CHARACTER]  SHOP
┌──────────┬───────────────────────┬──────────────┐
│ รายชื่อ   │ พอร์เทรต 900 × 850     │ RIVEN        │
│ HUNTER   │ (ลายทาง placeholder)   │ DUELIST      │
│  RANGED  │                       │ Lv. 1 / 25   │
│  · OWNED │                       │ [STATS][ABIL]│
│ GUNNER   │  ┌─ RIVEN ✓ ─┐        │ HP    120    │
│  BURST   │  │ คำบรรยาย   │        │ ATK   —      │
│  · OWNED │  │ passive    │        │ DEF   0      │
│ RIVEN    │  └───────────┘        │ SPD   6.0    │
│  DUELIST │                       │ CRIT  5.0%   │
│  · LOCKED│  ← ใบที่เลือกกรอบขาว    │ CRITDMG 50%  │
│ ? ? ? ?  │                       │              │
│  COMING  │                       │  [ SELECT ]  │
└──────────┴───────────────────────┴──────────────┘
```

**คำบรรยาย passive ตัวอย่าง:** *"Builds charge by moving. Spends it to break her blade open — three dashes, then one heavy finisher."*

### แม็ปกับโค้ด
`Assets/Script/UI/CharacterSelectUI.cs` มีเกือบครบแล้ว —
`detailPortrait` `detailName` `detailDesc` · แถว passive/weapon/ability/ultimate ·
6 ช่องสเตต (`statHpValue` … `statCritDmgValue`) · `confirmButton` + `lockStatusText` + `goldText`
`CharacterCarousel` + `CharacterCardUI` ทำรายการซ้ายอยู่แล้ว

### ของใหม่
- ป้ายสถานะ `OWNED` / `LOCKED` / `COMING SOON` บนการ์ด
- **`Lv. 1 / 25` — ระบบเลเวลรายตัวละคร ที่ยังไม่มีในเกม** (ดูหัวข้อ "ฟีเจอร์ที่ซ่อนอยู่")

### ที่ต้องระวัง
- `CharacterSelectUI.SelectedCharacter` เป็น static ที่ `PlayerWeaponManager` อ่านข้ามซีน — **ห้ามแตะ**
- ตัวระบุบนเน็ตเวิร์กใช้ `CharacterData.characterName` (string) ห้ามใช้ index
- `Char_Hunter` / `Char_Gunner` มี `portrait` ว่างทั้งคู่ — จอนี้พอร์เทรตกินพื้นที่กลางจอ

---

## 4 · LOBBY

```
แถบบน   CLONE SWARM              ฿ 8,420 G · COPY · 7K4M2P
แท็บ    [LOBBY]  MAP  CHARACTER  SHOP
┌───────────────────────────────┬──────────────────────────────┐
│ พอร์เทรต 880 × 752             │ PARTY · 3 / 4      2 READY   │
│                               │  PLAYER 0 [HOST][YOU]        │
│ YOUR CHARACTER                │    RIVEN · HP 120/120  READY │
│ RIVEN            [CHANGE · C] │  PLAYER 1                    │
│ DUELIST · MELEE · HP · SPD    │    HUNTER · HP 100/100 READY │
│ [P RUNIC BLADE][Q VALOR]      │  PLAYER 2                    │
│ [E BLADE OF EXILE]            │    GUNNER · HP 90/90 PICKING…│
│                               │  EMPTY SLOT · INVITE (เส้นประ)│
│                               │ MAP                          │
│                               │  พรีวิว · ARENA 01 · NORMAL   │
└───────────────────────────────┴──────────────────────────────┘
BACK                                      READY   START RUN
```

### แม็ปกับโค้ด
| แบบ | สคริปต์ | สถานะ |
|---|---|---|
| แท็บ 4 อัน | `TabBar` | มี |
| พอร์เทรต · ชื่อ | `LobbyUI.characterImage` · `CharacterSelectUI.detailName` | มี |
| แถวปาร์ตี้ | `LobbyUI.partyContainer` + `partyRowTemplate` | **ตอนนี้เป็น TMP บรรทัดเดียว** `Player 1: Riven [READY]` |
| พรีวิวแมพ | `mapImage` · `mapNameLabel` · `difficultyLabel` | มีฟิลด์ · **`MapCarousel.previewImage` ยังไม่ต่อสาย** |
| ปุ่ม | `backButton` · `readyButton` · `startRunButton` | มี |
| รหัสห้อง | `CopyRoomCode()` | มีแต่ไปแปะกับ `inviteButton` · **ไม่มี `roomCodeLabel`** |
| ยอดทอง | — | **ไม่มีฟิลด์ใน LobbyUI** (`MetaProgression.OnGoldChanged` มีอยู่แล้ว) |

### โค้ดใหม่
`LobbyPartyRowUI` (แถวเต็มรูป) · `LobbyAbilityChipUI` (ชิป `[คีย์] ชื่อ`)
ฟิลด์เพิ่มใน `LobbyUI`: `goldText` `roomCodeLabel` `copyCodeButton` `partyCountLabel` `readyCountLabel`

### ที่ต้องระวัง
- **ชิปสกิลในแบบเขียน `Q` / `E` ซึ่งล้าสมัยแล้ว** — ย้ายไปคลิกซ้าย/ขวาตั้งแต่ `28154a4f`
  ต้องอ่านป้ายจริงจาก `IHUDAbility.HUDKeyLabel`
- สถานะ ready ข้ามเน็ตอยู่ที่ `LobbyState` — client เขียนตรงไม่ได้ ต้องผ่าน ServerRpc

---

## 5 · MAP SELECT

```
แถบบน + แท็บ (MAP active)
┌──────────────────────────────────────────────────────┐
│  พรีวิวแมพ 1920 × 668  (ลายทาง placeholder)            │
└──────────────────────────────────────────────────────┘
OPEN FIELD · RUN 12 MIN                ENEMY HP ×1.0  GOLD ×1.0
ARENA 01                               [NORMAL] HARD  NIGHTMARE
Flat ground, no cover. Waves close from every edge,
so movement matters more than positioning.
┌────────┬────────┬────────┬────────┐          LOCKED ×2
│ARENA 01│FOUNDRY │CRYO    │THE     │
│(เลือก) │        │VAULT   │SPRAWL  │
└────────┴────────┴────────┴────────┘
BACK      A / D  BROWSE · ENTER CONFIRM      [CONFIRM MAP]
```

### แม็ปกับโค้ด
`MapSelectUI` (`detailPreview` `detailName` `detailDesc`) + `MapCarousel` + `MapCardUI`
`LobbyUI.SelectDifficulty(DifficultyTier)` มีอยู่แล้ว

### ของใหม่
- ชิป `ENEMY HP ×1.0` / `GOLD ×1.0` — **ต้องเช็คว่า `DifficultyTier` มีตัวคูณให้อ่านจริงไหม**
- ป้าย `LOCKED ×2` — ระบบปลดล็อกแมพ (ยังไม่มี)
- ป้าย `OPEN FIELD · RUN 12 MIN` — ประเภทแมพ + เวลารอบ

### ที่ต้องระวัง
- **โรสเตอร์แมพมีตัวเดียว** (`MapData_Arena01`) · carousel จะโชว์ซ้ำ ตามกติกา "ยอมให้ซ้ำ" ที่ตกลงไว้
  ระหว่างนี้ลด `MapCarousel.viewCount` เหลือ 3 ได้ (base บังคับขั้นต่ำ 3)
- `LobbyUI.MapLocked` — client เปลี่ยนแมพไม่ได้ มีแต่ host

---

## 6 · TALENT SHOP

```
แถบบน + แท็บ (SHOP active)   ฿ 8,420 G   RUNS 12 · WINS 3 · BEST 14:22   BACK
┌─────────────────────────────┬──────────────────────────┐
│ Attack Power │ Max Health   │  [ไอคอนส้ม]              │
│ Armor        │ Crit Chance  │  Attack Power            │
│ Move Speed   │ Haste        │  OFFENCE                 │
│ Area Size    │ Duration     │  คำบรรยาย…                │
│ Projectiles  │ Regen        │  Lv 3 / 5  ●●●○○         │
│ Magnet       │ EXP Gain     │  NOW +9%  ›  NEXT +12%   │
│ Gold Gain    │ Revive       │  DAMAGE +3% / LEVEL      │
│                             │  [ UPGRADE  400 ]        │
│ ← ใบที่เลือกพื้นน้ำเงิน        │  AFTER PURCHASE · 8,020 G│
└─────────────────────────────┴──────────────────────────┘
```

**14 talent:** Attack Power · Max Health · Armor · Crit Chance · Move Speed · Haste ·
Area Size · Duration · Projectiles · Regen · Magnet · EXP Gain · Gold Gain · Revive

**คำบรรยายตัวอย่าง:** *"Raises the damage of every weapon and ability by a flat percentage. Applies before crit."*

### แม็ปกับโค้ด
`Assets/Script/Meta/UI/TalentShopUI.cs` + `TalentTileUI` + `TalentData` · `MetaProgression`
dead field ที่ ADR-001 สั่งลบ **ลบไปแล้ว**

### ของใหม่
- แถบสถิติ `RUNS · WINS · BEST` — `MetaProgression.RecordRunResult(won, gold, time, level, kills)` เก็บอยู่แล้ว ต้องเช็คว่ามี getter
- แผง `NOW › NEXT` เทียบค่าปัจจุบันกับเลเวลถัดไป
- `AFTER PURCHASE · 8,020 G` — ยอดคงเหลือหลังซื้อ

### ที่ต้องระวัง — บั๊กจริงที่ยังไม่แก้
`TalentShopUI` ยังอยู่บน `Canvas` ไม่ใช่บน `TalentShopPanel`
→ `OnDisable → SaveManager.FlushIfDirty()` ยิงแค่ตอนออกจากซีน
→ **ซื้อ talent แล้วปิดเกมจากเมนู = เงินหาย** (ADR-001 F1 · Action Item 4 ที่ยังค้าง)
**ควรแก้ก่อนแตะจอนี้** เพราะเป็นงานย้าย component ที่ทำให้ layout ขยับ

---

## 7 · CONFIG

**จอที่ง่ายที่สุด — เป็นงานรีสกินล้วน ไม่มีตรรกะใหม่เลย**

```
        ┌────────────────────────────────────┐
        │ CONFIG   ESC TO GO BACK            │
        │ AUDIO                              │
        │  Master  ▇▇▇▇▇▇▇▇░░        80%     │
        │  Music   ▇▇▇▇▇░░░░░        55%     │
        │  SFX     ▇▇▇▇▇▇▇░░░        70%     │
        │ GRAPHICS                           │
        │  Quality  [Low][Medium][High][Ultra]│
        │ LANGUAGE                           │
        │  Locale   [English][ไทย]            │
        │                                    │
        │ [RESET DEFAULTS]          [ BACK ] │
        └────────────────────────────────────┘
```

- สไลเดอร์พื้นเทา เติมน้ำเงิน `#1824D8` · % ชิดขวา
- ปุ่มแบ่งช่อง ตัวที่เลือกพื้นน้ำเงิน
- `RESET DEFAULTS` กรอบส้ม (ซ้าย) · `BACK` พื้นน้ำเงิน (ขวา)

### แม็ปกับโค้ด — ครบแล้วทั้งหมด
`Assets/Script/UI/SettingsMenuUI.cs` มี `masterSlider` `musicSlider` `sfxSlider` ·
`qualityDropdown` (→ `QualitySettings`) · `languageDropdown` (→ Unity Localization) ·
`resetDefaultsButton` · `backButton`

> ภาษาไทยในแบบตรงกับ Unity Localization ที่เพิ่ง commit ไปที่ `c95842f0` พอดี

### ของใหม่
- แบบใช้ **ปุ่มแบ่งช่อง** แต่โค้ดเป็น `TMP_Dropdown` → ต้องเลือกว่าจะเปลี่ยนเป็นปุ่ม
  หรือคง dropdown แล้วรีสกินให้เข้าชุด · **ปุ่มแบ่งช่องอ่านง่ายกว่าเมื่อตัวเลือกมี ≤ 4**
- `PauseMenuUI.settingsSubPanel` (ทางประนีประนอมที่ทำไว้) ควรเลิกใช้เมื่อจอนี้เข้าซีนเกมได้

---

## 8 · LOADING

```
พื้น: ภาพ art ของแมพเต็มจอ + ลายทับ
ล่างซ้าย   ARENA 01 · NORMAL   (mono เล็ก)
           NOW
           LOADING             (สองบรรทัด ใหญ่)
           ▇▇▇▇▇▇▇▇░░░░        (แถบบางสีน้ำเงิน)
ล่างขวา    SKIP →
ล่างสุด    TIP — Riven builds charge by moving. Standing still wastes her passive.
```

### แม็ปกับโค้ด
`MenuManager.loadingPanel` + `loadingText` (แยกจาก `versionText` แล้ว ตาม ADR-001 ข้อ 5)

### ของใหม่
- ภาพ art รายแมพ — ต้องเพิ่มฟิลด์ใน `MapData`
- แถบความคืบหน้า — `SceneManager.LoadSceneAsync` มี `progress` แต่ NGO โหลดผ่าน
  `NetworkManager.SceneManager` ซึ่งรายงานคนละทาง **ต้องเช็คว่าดึง progress ได้จริงไหม**
- `SKIP →` — ข้ามไปไหน? ถ้าโหลดยังไม่เสร็จก็ข้ามไม่ได้ · น่าจะหมายถึงข้าม TIP
- TIP รายตัวละคร — ต้องเพิ่มฟิลด์ใน `CharacterData`

---

## 9 · GAMEPLAY HUD

จอที่หนาแน่นที่สุด และเป็นจอเดียวที่ **อ่านต้องทันระหว่างศัตรูเต็มจอ**

```
บนซ้าย    [14:22] WAVE 31            (สแลบน้ำเงินเอียง)
บนกลาง    PURPLE ▇▇▇▇▇▇░░ 68%
          LICH ▇▇░   DUO ▇░   CAST ▇▇▇ (ส้ม)
บนขวา     OBJECTIVES
            HOLD THE ZONE      0:42
            COLLECT CORES      4 / 6
            ESCORT DONE          ✓     (จาง)
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

### แม็ปกับโค้ด
| แบบ | สคริปต์ |
|---|---|
| นาฬิกา + เวฟ | `GameTimeline` → `GameHUD` |
| แถบบอส + เฟส + มินิบอส | `BossHUDUI` + `MiniBossBarEntry` |
| รายการ objective | `ObjectiveIndicatorUI` (หลายอันแล้วจาก `847aa0e0`) |
| แถวปาร์ตี้ | `TempPartyHUD` |
| บัฟลอย | `FloatingBuffUI` |
| เลเวล/EXP/HP | `StatusHUDUI` + `SharedExperienceManager` |
| ช่องอาวุธ/augment | `AbilityHUDUI` · `AugmentHUDUI` · `WeaponStatHUD` |
| แถบ CHARGE | `ChargeBarUI` (Riven) |

### ที่ต้องระวัง — สำคัญกว่าจออื่นทั้งหมด
**นี่คือโซน C** ตามที่ตกลงกันไว้ตั้งแต่รอบแรก — เอาได้แค่ **สี · ฟอนต์ · มุมบากเฉียง**
**ห้าม** เฉือนทั้งแถบ HP · ห้าม halftone ทับตัวเลข · ห้ามโมชั่น overshoot บนตัวเลขที่เปลี่ยนตลอด

ก่อน/หลังต้องผ่าน **5-second test**: แคปจอตอนศัตรูเต็มจอ โชว์ 5 วิ ปิด แล้วถามว่า
HP เหลือกี่ % / สกิลไหนพร้อม · ถ้าความถูกต้องตกแม้แต่นิด แปลว่าแรงเกินไป

ป้ายปุ่มสกิลต้องอ่านจาก `IHUDAbility.HUDKeyLabel` — แบบเขียน `Q`/`E` ซึ่งล้าสมัยแล้ว

---

## 10–12 · LEVEL UP ✅ · WIN / LOSE ✅ · PAUSED ✅

สเปกเต็มอยู่ที่ `design_handoff_ingame_screens/README.md`
ทำครบแล้วทั้งสามจอ — ดูซีนต้นแบบตามตารางสถานะข้างบน

---

## ฟีเจอร์ที่ซ่อนอยู่ในแบบ — ไม่ใช่งาน UI

แบบวาดของพวกนี้ไว้เหมือนมันมีอยู่แล้ว แต่ในเกมยังไม่มี · **ต้องตัดสินทีละอัน**
ว่าจะทำจริง หรือทำ UI ไว้รอแล้วซ่อนไว้ก่อน

| ของ | อยู่จอ | สถานะในเกม |
|---|---|---|
| หน้าจอ Title | 1 | ไม่มีทั้งซีนและสคริปต์ |
| `Lv. 1 / 25` ของตัวละคร | 3 | ไม่มีระบบเลเวลรายตัวละคร |
| `LOCKED ×2` แมพ | 5 | ไม่มีระบบปลดล็อกแมพ |
| ตัวคูณ `ENEMY HP ×1.0` / `GOLD ×1.0` | 5 | มี `DifficultyTier` · ต้องเช็คว่ามีตัวคูณให้อ่าน |
| `RUNS · WINS · BEST` | 6 | `MetaProgression` เก็บข้อมูลแล้ว · ต้องเช็ค getter |
| แถบความคืบหน้าโหลด | 8 | NGO โหลดผ่าน `NetworkManager.SceneManager` · ต้องเช็ค progress |
| art + TIP รายแมพ/ตัวละคร | 8 | ต้องเพิ่มฟิลด์ใน `MapData` / `CharacterData` |

---

## คำถามที่ยังไม่ได้ตัดสิน

1. **ลุค ALL-CAPS กับภาษาไทย** — คนออกแบบเขียนเองว่า *"it will not [survive Thai], and that decision changes the type scale on every screen"*
   เราแก้ส่วนที่ทำให้สระลอยไปแล้ว (`P3RText`, commit `cbc5172a`) แต่ **การปรับ type scale ชดเชยยังไม่ได้ทำ**
2. **MAIN · A หรือ B** — แถบกอดคำ หรือแถบกว้างคงที่ปล่อยคำล้น (ตอนนี้เป็น B)
3. **CONFIG ใช้ปุ่มแบ่งช่องหรือ dropdown** — แบบเป็นปุ่ม โค้ดเป็น dropdown
4. **Title เป็นซีนแยกหรือ panel** ใน `MenuScene`

---

## ลำดับที่แนะนำ

1. **CONFIG** — รีสกินล้วน ไม่มีคำถามค้าง ได้ผลเร็ว
2. **LOADING** — เล็ก ตรรกะน้อย
3. **LOBBY** — สเปกพร้อม ไม่ติดบล็อกเกอร์แล้ว (F7 ของ ADR-001 ถูกแก้ไปแล้ว)
4. **MAP SELECT** → **CHARACTER** — ต่อเนื่องกัน ใช้ carousel ตัวเดียวกัน
5. **TALENT SHOP** — แก้บั๊กเงินหายก่อน (ย้าย `TalentShopUI` ลง `TalentShopPanel`)
6. **TITLE** — ต้องตัดสินเรื่องซีน/panel ก่อน
7. **GAMEPLAY HUD** — ท้ายสุด ต้องผ่าน 5-second test

---

## แก้ไขจากรอบสร้างจริง · 2026-09-11

สามข้อแรกคือที่เอกสารรุ่นก่อนเขียนผิด · ที่เหลือคือของที่เพิ่งเจอตอนลงมือ

**1 · `RUNS · WINS · BEST` มีอยู่แล้ว ไม่ต้องทำใหม่**
`TalentShopUI.RefreshAll()` อ่าน `SaveManager.Data` แล้วเติม `statsText` เองตั้งแต่แรก
เอกสารเดิมเขียนว่า "ต้องเช็คว่ามี getter" — มันไม่ได้ผ่าน `MetaProgression` แต่ทำงานอยู่

**2 · บั๊กเงินหายไม่จริงแล้ว**
`SaveManager` ติดตั้ง `SaveAutoFlush` เองผ่าน `[RuntimeInitializeOnLoadMethod]`
ซึ่ง `FlushIfDirty()` ตอน quit · pause · เสียโฟกัส · การที่ `TalentShopUI` อยู่บน
`Canvas` แทน `TalentShopPanel` ยังเป็นเรื่อง lifecycle ที่ควรแก้ (OnEnable วิ่งตอนโหลดซีน
ไม่ใช่ตอนเปิดแผง) แต่ **ไม่ได้ทำให้ข้อมูลหาย** ตามที่เอกสารเดิมเตือนไว้

**3 · `ENEMY HP ×1.0` / `GOLD ×1.0` ไม่มีที่มา**
`DifficultyTier` เป็น enum เปล่าห้าค่า ไม่มีตัวคูณอยู่ที่ไหนในโปรเจกต์เลย
จอ MAP SELECT จึงโชว์ `—` · การโชว์ ×1.0 คือการบอกผู้เล่นว่ามีระบบที่ยังไม่มีอยู่จริง
ถ้าจะทำ ต้องตัดสินก่อนว่าตัวคูณอยู่ที่ไหน — บน enum, บน SO แยก, หรือใน `WaveConfig`

**4 · วรรณยุกต์ไทยทับสระ — พังทุกจอมาตั้งแต่ต้น**
`เพื่อ` เห็นเป็น `เพือ` · `ที่` เห็นเป็น `ที` · `อยู่` เห็นเป็น `อยู`
กลิฟไม่ได้หาย แค่ไม่ถูกยกขึ้นชั้นสอง TMP ไม่ทำ mark-to-mark positioning ให้เอง
แก้ด้วยแพ็กเกจ `ThaiTextCare` (MIT) + เครื่องมือ `Tools > Clone Swarm > Fix Thai Fonts`
ซึ่งอบตัวอักษรลง character table พร้อม `includeFontFeatures` แล้วปิด
`m_ClearDynamicDataOnBuild` (ไม่งั้นตอน build ตารางถูกล้างแล้วอาการกลับมาเฉพาะในบิลด์)

**5 · ภาษาไทยไม่มีช่องว่างระหว่างคำ TMP จึงตัดบรรทัดผ่ากลางคำ**
`บรรทัด` ถูกผ่าเป็น `บร` + `รทัด` · แก้ด้วย `ThaiTextNurse` ที่แทรกช่องว่างความกว้างศูนย์
ตามขอบคำจากพจนานุกรม · ใส่ให้ TMP ทุกตัวที่ตั้ง wrap แล้ว 249 ตัวใน 8 ซีน
จอที่ทำต่อจากนี้ใช้ `P3RBuilderKit.Wrap()` แทนการเขียน `textWrappingMode` ตรงๆ

**6 · โปรเจกต์อยู่ใน Linear color space — `Image` alpha ต่ำสว่างกว่าที่ค่า CSS บอก**
วัดจริง: ขาว `.10` บนพื้น `#0D1226` ได้ `#5B5D61` แทนที่จะเป็น `#252A3C`
ใช้ `P3RBuilderKit.Over()` / `Lift()` ผสมล่วงหน้าเป็นสีทึบแทนการวางทับ

**7 · Sarabun ไม่มีกลิฟ `✓` และ `⚠`** — TMP วาดเป็นกล่องสี่เหลี่ยม
เครื่องหมายถูก/ไอคอนเตือนต้องวาดเป็นรูปเอง ไม่ใช้อักขระ

**8 · ป้ายปุ่มสกิลเรนเดอร์ออกมาเป็น `LMB` / `RMB` จริงแล้ว**
`LobbyAbilityChipUI` ถาม `AbilityInput.Find(slot)` — ยืนยันว่าที่แบบเขียน `Q`/`E` ล้าสมัย

**9 · บั๊กที่แก้ไประหว่างทาง** — `TalentShopUI.detailName` โชว์ `talentName` ดิบ
แทน `DisplayName` ทำให้แผงขวาเป็นภาษาอังกฤษอยู่แผงเดียวตอนสลับเป็นไทย

---

## กับดักที่เจอมาแล้ว — อย่าเหยียบซ้ำ

- **โหลด asset ก่อน `EditorSceneManager.NewScene()` ไม่ได้** — batchmode ปลด asset ทิ้งจนกลายเป็น fake-null
  ซีนออกมาไม่มีฟอนต์/สี โดย exit 0 ไม่มี error (กินเวลาไปสองวัน)
- **`EditorUtility.DisplayDialog` คืน false เสมอใน batchmode** — กันด้วย `!Application.isBatchMode`
- **`UnityEngine.UI.Outline` ใช้กับ graphic ที่ alpha 0 ไม่ได้** — ต้องวาดขอบสี่ด้านจริง
- **`UIShear` ใช้กับ TMP ไม่ได้** (BaseMeshEffect) · ประกอบรูปจากหลายชิ้นต้องใช้ `pivotOnParent`
- **letter-spacing ทำภาษาไทยแตก** — ใช้ `P3RText.SetTracking` เสมอ ห้ามเขียน `characterSpacing` ตรงๆ
- **ห้ามลบ public field ที่มีสายต่อในซีนแล้ว** — พังเงียบตอนรัน คอมไพล์จับไม่ได้

## เครื่องมือ

```
Tools > Clone Swarm > Build P3R <ชื่อจอ> Scene    สร้างซีนต้นแบบ
Tools > Clone Swarm > Capture P3R Proto Screens   เรนเดอร์ทุกจอเป็น PNG ที่ /Screenshots
```
batchmode ใช้ได้เฉพาะตอน Unity Editor ปิด (ต้องการ project lock):
```bash
Unity.exe -quit -batchmode -projectPath <proj> -executeMethod CloneSwarm.EditorTools.<Builder>.Build -logFile <log>
```
