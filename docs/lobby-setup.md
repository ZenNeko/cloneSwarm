# Setup — Lobby rework (งานมือใน Unity Editor)

> Claude · 2026-08-06 · คู่กับ `docs/implementation_plan.md` Round 1
> Antigravity เขียนได้แค่ `.cs` — ทุกอย่างในไฟล์นี้ต้องทำเองในเอดิเตอร์
> **ทำหลังจาก `unity-check` คืน exit 0 แล้วเท่านั้น** ไม่งั้นสคริปต์ยังไม่มีให้ลาก

---

## 1 · LobbyState prefab  ← ข้อนี้พลาดแล้วทุกอย่างพังเงียบ

1. `Assets/Prefab/manager/` → คลิกขวา → Create Empty → ตั้งชื่อ **`LobbyState`**
2. Add Component → **NetworkObject**
3. Add Component → **LobbyState** (สคริปต์จาก T4)
4. Add Component → **PlayerSlotRegistry** (จาก T14) — **ตัวเดียวกับ prefab นี้**
   ไม่ต้องสร้าง prefab แยกและไม่ต้องลงทะเบียนซ้ำ เพราะแชร์ `NetworkObject` เดียวกัน
5. ลากลง `Assets/Prefab/manager/` ให้เป็น prefab แล้วลบตัวใน scene ทิ้ง
6. เปิด **`Assets/DefaultNetworkPrefabs.asset`** → กด **+** → ลาก `LobbyState` prefab ใส่

> ข้ามข้อ 5 แล้ว NGO จะปฏิเสธการ spawn เงียบๆ — ไม่มี error ตอนคอมไพล์
> อาการคือล็อบบี้ว่างเปล่า ไม่มีใครเห็นใครเลย

7. ที่ `MenuScene` → GameObject ที่มี **MenuManager** → ลาก `LobbyState` prefab
   ใส่ช่อง **Lobby State Prefab**

---

## 2 · MapData asset

1. `Assets/ScriptableObjects/` → สร้างโฟลเดอร์ **`Maps`**
2. คลิกขวา → Create → **LoL Swarm → Map Data** → ตั้งชื่อ `Map_Arena01`
3. กรอก:

| ช่อง | ค่า |
|---|---|
| Map Id | `arena01` — **ห้ามซ้ำกับแผนที่อื่น** ค่านี้วิ่งบนสาย |
| Display Name | `Arena 01` |
| Description | อะไรก็ได้ โชว์ใน map preview |
| Preview Image | Sprite ของแผนที่ (ปล่อยว่างไปก่อนได้) |
| Scene Name | `SampleScene` — **ต้องสะกดตรงกับ Build Settings เป๊ะ** |

4. **Tiers** → size = **1** (รอบนี้ author แค่ Normal)
   - Element 0 → Tier = **Normal**
   - Waves By Phase → size 5 → ลาก `WaveConfig_Early` `_Mid` `_Late` `_PreBoss` `_Chaos`
     จาก `Assets/ScriptableObjects/Waves/` ตามลำดับ
   - Main Boss Config → **ปล่อยว่าง** = ใช้ config ที่อยู่บน boss prefab เดิม

> tier อื่น (Easy / Hard / Savage / Epic) ยังไม่ต้องสร้าง
> `MapData.GetTier()` จะ fallback มา Normal ให้เอง — เลือกได้ในล็อบบี้แต่ยังเล่นเหมือนกัน

---

## 3 · Canvas ล็อบบี้

สร้างใน `MenuScene` ใต้ Canvas เดิม ชื่อ **`LobbyPanel`** (SetActive = false ไว้)

```
LobbyPanel
├── TopBar
│   ├── BackButton
│   ├── TabBar                 ← ใส่ TabBar.cs ตรงนี้
│   │   ├── CapQ  (Text "Q")
│   │   ├── Tab_Lobby     (Button + Text)
│   │   ├── Tab_Character (Button + Text)
│   │   ├── Tab_Map       (Button + Text)
│   │   ├── Tab_Shop      (Button + Text)
│   │   └── CapE  (Text "E")
│   ├── RoomCodeLabel
│   └── GoldLabel
├── Panel_Lobby      ← 4 panel นี้เปิดทีละอัน TabBar เป็นคนสลับ
├── Panel_Character
├── Panel_Map
├── Panel_Shop
├── BottomBar
│   ├── ReadyButton
│   └── StartRunButton
└── ControlHintBar  (Text)
```

### ต่อ TabBar

ที่ component **TabBar** → `tabs` size = 4 · แต่ละ element กรอกครบ 4 ช่อง:

| id | panel | button | label |
|---|---|---|---|
| `lobby` | Panel_Lobby | Tab_Lobby | Text ใน Tab_Lobby |
| `character` | Panel_Character | Tab_Character | … |
| `map` | Panel_Map | Tab_Map | … |
| `shop` | Panel_Shop | Tab_Shop | … |

**id ต้องสะกดตรงตามนี้เป๊ะ** — `LobbyUI` เรียก `SetTabVisible("map", …)` ด้วยสตริงตรงๆ

Active Color = `#4DB3FF` · Inactive Color = `#8A90A0`

### ต่อ LobbyUI

ใส่ `LobbyUI.cs` บน `LobbyPanel` แล้วลาก: TabBar · panel ทั้งสี่ · ปุ่ม ready/start ·
label ชื่อแผนที่ · label ความยาก · party row template · และ **`maps` list → ลาก `Map_Arena01` ใส่**

---

## 4 · TalentShop สองแท็บ

1. บน `TalentShopPanel` เดิม → เพิ่ม GameObject **TabBar** (ใส่ `TabBar.cs`) tabs size = **2**
   - `talent` → panel เดิมทั้งก้อน
   - `character` → GameObject ตัวใหม่
2. ที่ `TalentShopUI` → ลาก TabBar กับ panel ทั้งสองเข้าช่องใหม่
3. ใน `characterPanel` → สร้าง **Grid Layout Group** ตั้งชื่อ `CharacterGrid`
   และ tile ต้นแบบหนึ่งอัน (Image + Text + Button) **SetActive = false**
4. ที่ `TalentShopUI` → ลาก `CharacterGrid` เข้า **Character Grid Container**
   และ tile ต้นแบบเข้า **Character Tile Template**

---

## 4b · UI ในเกม (SampleScene) — ของใหม่จาก Round 2

### LevelUpUI — แถบรอเพื่อน
1. ใน `SampleScene` หา Canvas ที่มี `LevelUpUI`
2. สร้าง GameObject ใหม่ใต้ `panelRoot` ชื่อ **`WaitingStrip`** — แถบเล็กมุมจอ
   ใส่ Text `"รอเพื่อน…"` + ตัวนับเวลา · **SetActive = false**
3. ลากเข้าช่อง **Waiting Strip** บน `LevelUpUI`

> ปล่อยว่างได้ แต่ถ้าว่างจะกลับไปเป็นพฤติกรรมเดิม คือแผงคลุมทั้งจอตอนรอเพื่อน
> ซึ่งเป็นข้อที่ชุมชน The Spell Brigade บ่นหนักสุด

### WinLoseUI — ปุ่มเล่นอีกครั้ง
1. หา Canvas ที่มี `WinLoseUI` → สร้าง Button ใหม่ชื่อ **`PlayAgainButton`**
   วางเป็นปุ่มหลัก (เด่นกว่า Return)
2. ลากเข้าช่อง **Play Again Button**

> ปุ่มนี้ enable เฉพาะ host — client จะเห็นแต่กดไม่ได้

---

## 5 · ตรวจก่อนกด Play

- [ ] `LobbyState` อยู่ใน `DefaultNetworkPrefabs.asset` แล้ว
- [ ] prefab นั้นมี **ทั้ง** `LobbyState` และ `PlayerSlotRegistry` บน GameObject เดียวกัน
- [ ] `MenuManager.lobbyStatePrefab` ไม่ว่าง
- [ ] `MenuManager.lobbyPanel` + `lobbyUI` ไม่ว่าง
- [ ] `Map_Arena01.sceneName` สะกดตรงกับ Build Settings
- [ ] `TabBar.tabs` ครบ 4 อัน id สะกดถูก
- [ ] Panel ทั้งสี่ SetActive = **true** ไว้ตอน edit (TabBar จะปิดให้เองตอนรัน)
- [ ] `LevelUpUI.waitingStrip` + `WinLoseUI.playAgainButton` ต่อแล้ว
- [ ] `TalentShopUI` character grid container + tile template ต่อแล้ว

---

## 6 · วิธีเทสต์ — อ่านให้ครบ

compile ผ่านไม่ได้แปลว่าเกมถูก **บั๊กที่หนักที่สุดในโปรเจกต์นี้ compile จับไม่ได้เลยสักตัว**

### Solo (เร็วสุด เจอปัญหาพื้นฐาน)
1. MenuScene → PLAY → เลือกตัวละคร → **ต้องเข้าหน้า Lobby ไม่ใช่โหลดเข้าเกมทันที**
2. กด `Q` `E` → วนครบ 4 แท็บ
3. แท็บแผนที่ → เลือก Arena 01 + ความยาก
4. START RUN → เข้าเกม → เวฟต้องออกเหมือนเดิมทุกประการ (เพราะ author แค่ Normal)

### Multiplayer — **ต้องเปิดสองหน้าต่าง ห้ามเทสต์เป็น host อย่างเดียว**
ใช้ ParrelSync หรือ Multiplayer Play Mode

| ดูจากจอไหน | ต้องเห็นอะไร |
|---|---|
| **client** | แท็บแผนที่ **หายไป** · กด Q/E วนแค่ 3 แท็บ ไม่ตกหลุมแท็บที่ซ่อน |
| **client** | ชื่อแผนที่ + ความยากที่ host เลือก โผล่บนแท็บ Lobby ถูกต้อง |
| **client** | ปุ่ม START RUN ไม่มี หรือกดไม่ได้ |
| **host** | เห็นตัวละครที่ client เลือก ขึ้นในแถวปาร์ตี้ |
| **ทั้งคู่** | client เปลี่ยนตัวละคร → **ready ของ client หลุดคนเดียว host ยังพร้อมอยู่** |
| **ทั้งคู่** | host เปลี่ยนแผนที่หรือความยาก → **ready หลุดทุกคน** |
| **client** | เข้าห้องทีหลัง (join ตอน host อยู่ล็อบบี้แล้ว) → เห็น state ครบ ไม่ว่างเปล่า |

### ในเกม — ของใหม่จาก Round 2

| ทำอะไร | ต้องเห็นอะไร |
|---|---|
| กด **ESC ตอนหน้า Level Up เปิดอยู่** | **ไม่มีอะไรเกิดขึ้น** ไม่ใช่แผงซ้อนสองชั้น |
| กด **ESC หลังจอ VICTORY/DEFEAT ขึ้น** | ไม่มีอะไรเกิดขึ้นเช่นกัน |
| เลือกการ์ดเสร็จก่อนเพื่อน | แผงหด เหลือแถบมุมจอ **มองเห็นสนามและศัตรู** |
| เล่นคนเดียว แล้วเลเวลอัป | **ไม่มี timer** และไม่มีป้ายรอผู้เล่น (เป็นแบบนี้อยู่แล้วก่อนหน้า) |
| เล่นสองคน คนแรกเลือกการ์ด | timer **เพิ่งเริ่มนับตอนนั้น** ไม่ใช่ตั้งแต่แผงเปิด |
| จบเกม | ปุ่มเล่นอีกครั้งขึ้น · host กดได้ · client กดไม่ได้ |

### สีผู้เล่น — เทสต์ยากสุดแต่สำคัญสุด

บั๊กที่ T14–T16 แก้จะโผล่**เฉพาะตอนมีคน reconnect** เล่นรวดเดียวสี่คนไม่มีทางเจอ

1. เข้าเกม 3–4 คน จนเจอ mechanic color match (วงสีประจำตัว)
2. **ให้คนกลางๆ ออกจากเกม แล้วต่อกลับเข้ามาใหม่**
3. รอ color match รอบถัดไป → **สีต้องยังไม่ซ้ำกันเลย**
4. ถ้าซ้ำ แปลว่า `PlayerSlotRegistry` ไม่คืนช่องตอนคนออก หรือ `Instance` เป็น null
   แล้วโค้ดตกไปใช้ fallback `clientId % 4` ตัวเดิม

### บอสตาม tier

ดูจาก **จอ client** ว่า phase marker กับหลอดเลือดบอสตรงกับฝั่ง host ไหม
`BossManager` set `config` ฝั่ง server อย่างเดียว — ถ้า client ต้องใช้ด้วยจะเห็นเป็นบอสคนละ phase

### จุดที่น่าจะพังที่สุด
- **client เข้าทีหลังแล้วเห็นล็อบบี้ว่าง** → `LobbyState` ไม่อยู่ใน `DefaultNetworkPrefabs.asset`
- **ready ไม่ยอมหลุด** → ServerRpc เขียน `Players[i]` ไม่ครบ (แก้ struct ในลิสต์ตรงๆ ไม่ได้)
- **กด Q/E แล้วจอว่าง** → `Cycle()` ไม่ข้ามแท็บที่ซ่อน
- **สีซ้ำหลัง reconnect** → `PlayerSlotRegistry` ไม่ได้อยู่บน prefab เดียวกับ `LobbyState`

---

## ยังไม่ได้ทำในรอบนี้ (รู้ไว้ก่อน)

- ความยากยัง **ไม่ต่างกันจริง** — เลือก Savage ได้แต่เล่นเหมือน Normal เพราะยังไม่มีเนื้อหา
- `BossManager` แค่อ่านค่า tier เก็บไว้ ยังไม่เอาไปใช้ (ต้องแก้ `BossController` ซึ่งอยู่นอกขอบเขต)
- แท็บตัวละครในร้านเป็น panel เปล่า
- **ห้องค้างเพราะเพื่อน AFK ยังไม่มีทางออก** — host กด START RUN ไม่ได้ตลอดกาล ยังไม่ได้ตัดสิน
