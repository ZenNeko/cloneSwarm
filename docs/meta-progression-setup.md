# Meta-Progression + Augments — คู่มือติดตั้ง

> เพิ่มเมื่อ 2026-08-03 · ครอบคลุม GDD §14 (Meta-Progression) + ระบบ Augment สไตล์ LoL Swarm
>
> **โค้ดเสร็จหมดแล้ว — ที่เหลือคืองาน Editor ที่ CLI ทำแทนไม่ได้** (สร้าง prefab, ลาก reference, จัด UI)

---

## ภาพรวม 4 ระบบ

| ระบบ | ทำอะไร | ไฟล์หลัก |
|---|---|---|
| **Save** | เขียน/อ่าน `profile.json` แบบ atomic + backup + migration | `Meta/SaveData.cs`, `Meta/SaveManager.cs` |
| **Talent Shop** | ใช้ทองอัปสเตตัสถาวร 6 อย่าง ตาม GDD §14 | `Meta/TalentData.cs`, `Meta/UI/TalentShopUI.cs` |
| **Character Unlock** | ซื้อตัวละครด้วยทองในหน้าเลือกตัว | `CharacterData.unlockCost`, `UI/CharacterSelectUI.cs` |
| **Augments** | เลือก 1 จาก 3 ตอนเลเวล 3/7/12/18 (แทน card ปกติ) | `Data/Augment/*`, `UpgradeManager.cs` |

ทองได้จากการเล่น: `RunRewardTracker` นับ kill ฝั่ง server → จบเกมจ่ายให้ทุกคน (คูณ Gold Finder ของแต่ละคน) → แต่ละเครื่องเขียนลงเซฟตัวเอง

---

## ขั้นตอนติดตั้ง (ทำตามลำดับ)

### 1. สร้าง asset ตั้งต้น — 2 คลิก

```
Tools > Clone Swarm > Meta > Create Sample Augments
Tools > Clone Swarm > Meta > Create Default Talents + Database
```

คำสั่งแรกสร้าง augment ตัวอย่าง 9 ใบใน `Assets/Script/Data/Augment/Definitions/`
คำสั่งที่สองสร้าง talent 14 ตัวใน `Assets/Script/Data/Talent/` แล้วรวบทุกอย่าง
(talents + CharacterData ทุกตัว + augments ทุกใบ) เข้า **`Assets/Resources/MetaDatabase.asset`**

> ⚠️ ต้องรัน **คำสั่ง Augments ก่อน** ไม่งั้น augment จะยังไม่ถูกลงทะเบียนใน database
> (รันคำสั่งที่สองซ้ำได้เสมอ — ของเดิมไม่ถูกทับ)

`MetaDatabase.asset` **ต้องอยู่ใน `Assets/Resources/`** เท่านั้น โค้ดโหลดด้วย `Resources.Load`

---

### 2. Player Prefab — เพิ่ม 2 component

เปิด Player Prefab (ตัวที่มี `playermove` / `PlayerWeaponManager`) แล้ว **Add Component**:

- `PlayerTalentApplier`  ← ใส่ talent ถาวรตอนเกิด
- `PlayerAugmentManager` ← ถือ augment ของ run นี้

ไม่ต้องลาก reference อะไร — ทั้งคู่ `GetComponent` เอง

> ลำดับ: `PlayerTalentApplier` หน่วง 2 เฟรมก่อนใส่ค่า เพื่อรอ `PlayerWeaponManager.SetBaseStats`
> ไม่งั้นโบนัส MaxHealth จะโดน `baseHealth` ทับ — อย่าเอา delay ออก

---

### 3. Gameplay Scene (`SampleScene`) — เพิ่ม RunRewardTracker

หา GameObject ที่มี `GameTimeline` → **Add Component → `RunRewardTracker`**

ต้องอยู่บน GameObject ที่มี `NetworkObject` (GameTimeline มีอยู่แล้ว) เพราะต้องส่ง ClientRpc

ตั้งค่า economy ได้ที่ `MetaDatabase.asset`:

| ฟิลด์ | ค่า default | ความหมาย |
|---|---|---|
| `goldPerExp` | 0.1 | ทอง = `Enemy.expReward` × ค่านี้ |
| `winBonusGold` | 500 | โบนัสตอนฆ่า Main Boss สำเร็จ |
| `goldPerMinuteSurvived` | 20 | โบนัสต่อนาทีที่รอด |

---

### 4. Augment levels

เลือก GameObject ที่มี `SharedExperienceManager` → ฟิลด์ **Augment Levels** = `3, 7, 12, 18`
(แก้ได้ตามใจ — เลเวลไหนอยู่ในลิสต์ จะเป็นการ์ด Augment แทน weapon/stat)

ถ้า augment ถูกเลือกครบทุกใบแล้ว ระบบจะตกกลับเป็นการ์ดปกติเอง

---

### 5. MenuScene — Talent Shop panel

ทรงเดียวกับร้าน meta ของ LoL Swarm — **grid ฝั่งซ้าย + panel รายละเอียดฝั่งขวา**
คลิกช่อง = *เลือก* (ไม่ใช่ซื้อ) แล้วกดปุ่มซื้อปุ่มเดียวใน panel ขวา

```
TalentShopPanel  (TalentShopUI)
├── Header
│   ├── GoldText      (TMP)   "8,549"
│   ├── StatsText     (TMP)   "Runs 12 · Wins 3 · Best 14:22"
│   └── BackButton
├── TileGrid          ← Grid Layout Group, Constraint = Fixed Column Count 3
│   └── TalentTile    ← template, SetActive = false
└── DetailPanel
    ├── DetailIcon        (Image)
    ├── DetailName        (TMP)
    ├── DetailDescription (TMP)
    ├── DetailLevelText   (TMP)   "Lv 3 / 5"
    ├── ValueRow
    │   ├── CurrentValue  (TMP)   "+9% Damage"
    │   ├── Arrow         (GameObject)  ›
    │   └── NextValue     (TMP)   "+12% Damage"
    ├── MaxedNote         (GameObject)  "ไปถึงเลเวลสูงสุดแล้ว"
    └── BuyButton
        ├── BuyCoinIcon   (Image)
        └── BuyCostText   (TMP)
```

**โครง TalentTile prefab** (ชื่อ child ตรงตามนี้ = auto-find ไม่ต้องลาก):

```
TalentTile  (Image BG + Button + TalentTileUI)
├── Icon           (Image)   ← ใหญ่ กลางช่อง · โค้ดย้อมสีให้เองจาก tintColor
├── NameText       (TMP)
├── PipsContainer  (Horizontal Layout Group)
│   └── Pip ×6     (Image)   ← ใส่เผื่อ 6 อัน · เกิน maxLevel จะถูกซ่อนอัตโนมัติ
├── CostText       (TMP)     ← "3,000" หรือ "สูงสุด"
├── CoinIcon       (Image)   ← ซ่อนเองตอนตัน
└── SelectedFrame  (Image)   ← กรอบไฮไลต์ · SetActive = false ไว้
```

ลากใส่ Inspector ของ `TalentShopUI` ตามชื่อในผังข้างบน
(`tilesContainer` = TileGrid · `tileTemplate` = TalentTile)

แล้วที่ `MenuManager`: ลาก `TalentShopPanel` → ช่อง **Talent Shop Panel**
และปุ่มเปิดร้าน → ช่อง **Talent Shop Button** (+ `goldText` ถ้าอยากโชว์ทองบนหน้า Main)

> ช่อง Pip ต้องมีลูกอย่างน้อย 6 อัน — talent ที่ maxLevel 5 จะซ่อนอันที่ 6 เอง
> ส่วน Extra Shot / Second Chance ที่ maxLevel 1 จะเหลือขีดเดียว

**สีไอคอน** มาจาก `tintColor` บน asset ไม่ต้องมีหัวข้อหมวดคั่นใน grid:
ส้ม = สายดาเมจ · ฟ้า = ระยะ/ความเร็ว/EXP · เขียว = สายเลือด · ทอง = เก็บของ/ทอง/เกราะ · ม่วง = ซื้อได้ครั้งเดียว

---

### 6. Character Select — ปุ่มปลดล็อก

ที่ `CharacterSelectUI` เพิ่มการลาก (ทุกช่องปล่อยว่างได้ ระบบยังทำงาน แต่ UX จะงง):

- `confirmButtonLabel` → TMP บนปุ่ม Confirm (จะสลับเป็น "ปลดล็อก 1,000 G")
- `lockStatusText`     → TMP บอกสถานะ
- `goldText`           → TMP โชว์ทอง

ที่ **CharacterCard prefab** เพิ่ม child (auto-find ตามชื่อ):

```
CharacterCard
├── ...ของเดิม...
└── LockOverlay   (Image ทึบ + ไอคอนกุญแจ)
    └── LockCostText (TMP)   "1,000 G"
```

ถ้าไม่ทำ overlay ก็ยังเห็นความต่าง — ไอคอนตัวที่ล็อกจะถูก tint ให้มืดลง

**สุดท้าย: ตั้งค่าที่ CharacterData แต่ละตัว**

| ตัวละคร | `unlockedByDefault` | `unlockCost` |
|---|---|---|
| ตัวเริ่มต้น (อย่างน้อย 1 ตัว) | ✅ ติ๊ก | — |
| ตัวอื่น | ไม่ติ๊ก | 1000 / 2000 / ... |

> ⚠️ **ต้องมีอย่างน้อย 1 ตัวที่ติ๊ก `unlockedByDefault`** ไม่งั้นผู้เล่นใหม่เลือกตัวละครไม่ได้เลย

---

### 7. (optional) HUD ในเกม

- **Win/Lose**: ที่ `WinLoseUI` ลาก TMP ใส่ `goldEarnedLabel` / `goldTotalLabel`
- **Augment bar**: สร้าง empty ใต้ Canvas + Horizontal Layout Group + component `AugmentHUDUI`
  แล้วลาก Image template ใส่ `iconTemplate` (SetActive = false)

---

## Talent ที่มีให้ (14 ตัว)

`TalentData` **ไม่มี enum ประเภทของตัวเอง** — ใช้ `StatType` ตัวเดียวกับ `StatData` ตรงๆ
ผ่านฟิลด์ `mode` + `statType`:

| `mode` | ความหมาย |
|---|---|
| `Stat` | บวกเข้า `StatType` ที่ระบุ (ผ่าน `PlayerStatManager.AddPermanentBonus`) |
| `GoldFind` | ตัวคูณทองท้ายเกม |
| `SecondChance` | ชุบชีวิต 1 ครั้ง/เกม |

หน่วยของ `valuePerLevel` เหมือน `StatData.valuePerLevel` เป๊ะ (% → decimal, flat → ใส่ตรงๆ)

### Combat

| Talent | StatType | ผล/เลเวล | Max | ราคา |
|---|---|---|---|---|
| Attack Power | Damage | +3% damage | 5 | 100→1600 |
| Haste Speed | AbilityHaste | +5 Ability Haste | 5 | 150→2400 |
| Critical Edge | CriticalChance | +4% crit | 5 | 150→2400 |
| Wide Impact | AreaSize | +4% AoE/range | 5 | 120→1920 |
| Lingering Force | Duration | +5% projectile lifetime | 5 | 100→1600 |
| Extra Shot | ProjectileCount | +1 กระสุนทุกอาวุธ | **1** | 8000 |

### Survival

| Talent | StatType | ผล/เลเวล | Max | ราคา |
|---|---|---|---|---|
| Max Health | MaxHealth | +50 HP | 5 | 100→1600 |
| Iron Skin | Armor | +3 armor | 5 | 120→1920 |
| Regeneration | HealthRegen | +0.5 HP/s | 5 | 120→1920 |

### Utility

| Talent | StatType | ผล/เลเวล | Max | ราคา |
|---|---|---|---|---|
| Swift Boots | MoveSpeed | +2% ความเร็ว | 5 | 150→2400 |
| Magnet Boost | PickupRadius | +10% รัศมีดูด | 5 | 80→1280 |
| Fast Learner | ExpBonus | +5% EXP | 5 | 200→3200 |

### Meta (ไม่ใช่ stat)

| Talent | mode | ผล/เลเวล | Max | ราคา |
|---|---|---|---|---|
| Gold Finder | GoldFind | +10% ทองท้ายเกม | 5 | 200→3200 |
| Second Chance | SecondChance | ชุบชีวิต 1 ครั้ง/เกม ที่ 30% HP | 1 | 5000 |

### ทำไมไม่มี GainGold / HealOnFullBuild

สอง `StatType` นี้เป็นโบนัส **"ยิงครั้งเดียวตอนบิลด์ตัน"** ที่ทำงานอยู่ใน `PlayerStatManager.ApplyStatLocal`
เท่านั้น (เช็ค `lv + 1 >= MaxLevel` แล้วเรียก `HealPercent` / log) — ไม่ใช่ค่าสะสมที่มีใครอ่านจาก `statTotals`
ถ้าทำเป็น talent ผู้เล่นจะจ่ายทองแล้วไม่ได้อะไรเลย จึงเว้นไว้
(`GainGold` ทับซ้อนกับ Gold Finder อยู่แล้ว · `HealOnFullBuild` ทับซ้อนกับ Second Chance)

ถ้าอยากได้จริง ต้องไปเพิ่ม read path ใน `PlayerStatManager` ก่อน แล้วค่อยสร้าง asset เพิ่ม

---

### เพิ่ม Talent ใหม่เอง

สร้าง asset จาก `Assets > Create > LoL Swarm/Meta/Talent Data` แล้วตั้ง `mode` + `statType`
**ไม่ต้องแก้โค้ดเลย** — `PlayerTalentApplier` ส่งทุก `mode = Stat` เข้าทางเดียวกันหมด
เสร็จแล้วลากเข้าลิสต์ `talents` ใน `MetaDatabase` (หรือรัน `Create Default Talents + Database` ซ้ำ)

แก้ราคา/ค่าได้ที่ asset ใน `Assets/Script/Data/Talent/`
**ห้ามแก้ `talentId`** หลังปล่อยเกม — เป็นคีย์ในไฟล์เซฟ

> **ไอคอนยังว่างอยู่ทุกตัว** — เครื่องมือไม่ได้ใส่ `icon` ให้ ต้องลาก sprite เองใน Inspector
> ยืมจาก `Assets/Script/Data/StatData/SD_*.asset` ได้เลย เพราะ `statType` ตรงกันพอดี
> (ระหว่างที่ยังไม่ใส่ ช่องจะไม่มีรูป แต่ `tintColor` ยังทำงาน จึงพอแยกหมวดได้อยู่)

> ถ้าเคยรันเครื่องมือสร้าง talent **ก่อน** เปลี่ยนมาใช้ `StatType` — รัน
> `Create Default Talents + Database` อีกครั้ง เครื่องมือจะซ่อม `mode`/`statType`/`category`
> ของ asset เดิมให้ (ไม่แตะราคาและค่าที่ปรับไว้) ดู log `[MetaSetup] ซ่อม identity ของ ...`

---

## เขียน Augment ใหม่

3 subclass ที่มีให้ ครอบคลุมเกือบทุกกรณี — สร้างจากเมนู `Assets > Create > LoL Swarm/Augment/...`

| Subclass | ใช้ทำอะไร | ตัวอย่าง |
|---|---|---|
| `StatAugment` | โบนัสสเตตัสก้อนใหญ่ (ใส่ค่าติดลบได้) | Glass Cannon: DMG +45%, HP −60 |
| `TriggerAugment` | เอฟเฟกต์ตามเงื่อนไข | Bloodthirst: ทุก 20 kill → heal 5% |
| `WeaponGrantAugment` | แถมอาวุธ/สกิลทันที | ให้ Super weapon เลย |

ถ้าต้องการอะไรที่ 3 ตัวนี้ทำไม่ได้ → เขียน subclass ใหม่ของ `AugmentData` แล้ว override `OnAcquire(PlayerAugmentManager ctx)`
ใน `ctx` มี `Stats` / `Move` / `Weapons` / `Abilities` ให้ใช้ครบ

**สร้าง asset ใหม่แล้วอย่าลืมรัน `Create Default Talents + Database` ซ้ำ** เพื่อลงทะเบียนเข้า `MetaDatabase`
(หรือลากเข้าลิสต์ `augments` เองใน Inspector)

> ลำดับใน `MetaDatabase.augments` คือ index ที่ client ส่งให้ server —
> **แทรกตรงกลางระหว่างเกมออนไลน์ที่กำลังเล่นอยู่ไม่ได้** แต่ระหว่าง dev ไม่มีปัญหา

---

## เรื่อง authority ที่ต้องรู้

ระบบนี้ตามแบบเดียวกับ `PlayerStatManager.ApplyStat` ที่มีอยู่แล้ว:

```
owner client  ── apply ฝั่งตัวเอง ──→ ServerRpc (ส่งแค่ index / level)
                                        ↓
server ── clamp + อ่านค่าจริงจาก asset ของตัวเอง ──→ apply สำเนาฝั่ง server
```

**client ปลอมค่าโบนัสไม่ได้** (server ไม่เคยรับตัวเลขจาก client) แต่ปลอม *เลเวล talent ที่อ้างว่าซื้อมา* ได้
เพราะไฟล์เซฟอยู่บนเครื่องผู้เล่น — ยอมรับได้สำหรับ co-op PvE เล่นกับเพื่อน
ถ้าจะเปิด matchmaking สาธารณะเมื่อไหร่ ต้องย้าย profile ขึ้น server (ดู `docs/plan-server-state-and-reconnect.md`)

ทองจ่ายจาก **server → ClientRpc เจาะจงรายคน** แล้วแต่ละเครื่องเขียนเซฟตัวเอง — กันคนแก้ทองระหว่าง run ได้ระดับหนึ่ง

---

## ไฟล์เซฟ

- ที่อยู่: `%USERPROFILE%/AppData/LocalLow/<Company>/<Product>/profile.json`
  (เปิดเร็ว: `Tools > Clone Swarm > Meta > Open Save File Folder`)
- เขียนแบบ temp → backup → replace ทุกครั้ง มี `profile.bak.json` สำรองเสมอ
- ลบทดสอบ: `Tools > Clone Swarm > Meta > Delete Save File`
- เขียนลงดิสก์ทันทีตอน: ซื้อ talent / ปลดล็อกตัวละคร / จบเกม
  นอกนั้นใช้ `MarkDirty()` แล้ว flush ตอนออกเกม/สลับหน้าต่าง

**เพิ่ม field ใหม่ใน `SaveData` ได้เสมอ · ห้ามลบ field เก่า** (ไฟล์เซฟเดิมจะอ่านไม่ออก)
ถ้าเปลี่ยนโครงสร้างแบบ breaking → bump `SaveData.CurrentVersion` แล้วเพิ่ม case ใน `SaveManager.Migrate`

---

## เช็กลิสต์ทดสอบ

- [ ] เปิดเกมครั้งแรก → มี log `[Save] ไม่พบไฟล์เซฟ — สร้างโปรไฟล์ใหม่`
- [ ] เล่นจบ 1 run (แพ้ก็ได้) → เห็น log `[Reward] จบเกม ...` และ `[Meta] จบเกม ...`
- [ ] กลับ MenuScene → ทองเพิ่มขึ้นจริง
- [ ] ซื้อ talent → ปิดเกม → เปิดใหม่ → เลเวล talent ยังอยู่
- [ ] เปิดร้าน → คลิกช่องอื่น → panel ขวาเปลี่ยนตาม + กรอบไฮไลต์ย้ายช่อง
- [ ] panel ขวาโชว์ `ค่าปัจจุบัน › ค่าหลังอัป` และหลังกดซื้อ ตัวเลขทั้งสองขยับขึ้น 1 ขั้น
- [ ] talent ที่ตันแล้ว → ช่องขึ้น "สูงสุด" · panel ขวาซ่อนปุ่มซื้อ + โชว์ "ไปถึงเลเวลสูงสุดแล้ว"
- [ ] Extra Shot / Second Chance (maxLevel 1) → ขีด pip เหลืออันเดียว ไม่ใช่ 6 อัน
- [ ] ซื้อ Max Health แล้วเข้าเกม → HP เริ่มต้นมากกว่า `baseHealth` จริง (ดูหลอดเลือด)
- [ ] ซื้อ Second Chance → ตายครั้งแรกต้องชุบชีวิตที่ 30% HP ไม่ใช่ตายจริง
- [ ] เลเวลอัปถึง 3 → การ์ดที่ขึ้นต้องเป็น Augment (สีตาม rarity)
- [ ] เลือก Augment แล้ว stat เปลี่ยนจริง (เช่น Glass Cannon → damage ขึ้น, HP ลด)
- [ ] **ทดสอบ 2 เครื่อง (ParrelSync)**: augment ของ client ที่ไม่ใช่ host ต้องมีผลจริง
      (ถ้าไม่มีผล = mirror ServerRpc ไม่ทำงาน)
- [ ] ตัวละครที่ยังไม่ปลดล็อก → กด Confirm แล้วต้องเป็นการซื้อ ไม่ใช่เข้าเกม
