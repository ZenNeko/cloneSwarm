# ส่งต่อ — UI เลือกตัวละคร / เลือกแมพ / ล็อบบี้ · 2026-08-22

โค้ดเสร็จและคอมไพล์ผ่านทั้งหมด · ที่เหลือเป็นงานต่อสายใน Editor กับการกรอกข้อมูลใน asset

**ยังไม่เคยกด Play โดยผู้เขียนเอกสารนี้** — สิ่งที่ยืนยันแล้วคือคอมไพล์ 0 error และการไล่โค้ดตามเส้นทางจริง
ส่วนที่ผู้เล่นเทสต์เองแล้วและทำงาน: carousel ของแมพ (เห็นใน Play), carousel ของตัวละคร

---

## 1 · ภาพรวมโครงสร้าง

```
Canvas
├─ CharacterSelectUI      แผงขวา · แท็บ · ปุ่ม · เจ้าของ roster
├─ MapSelectUI            แผงขวาของแมพ · อ่าน maps จาก LobbyUI
├─ LobbyUI                แถวปาร์ตี้ · ปุ่ม/ภาพสรุปในแท็บ Lobby
└─ Panel_Character
    ├─ LeftPanel          ← CharacterCarousel + RectMask2D (โค้ดใส่ให้)
    │   └─ cardsContainer     ← Slot_0..5 สร้างตอนรัน
    └─ (ชั้น hero)        ← นอก mask ล้นออกนอกแถบได้
```

`CarouselBase` ถือกลไกร่วมทั้งหมด (วางตำแหน่ง ลาก ลูกกลิ้ง snap settle scale alpha mask)
`CharacterCarousel` กับ `MapCarousel` สืบทอดแล้วบอกแค่ "มีกี่ชิ้น" กับ "ผูกข้อมูลยังไง"

**ข้อบังคับ:** carousel ต้องอยู่บน *แผงที่ลาก* ไม่ใช่บน Canvas — `IDragHandler` ของ uGUI
ส่งอีเวนต์ให้เฉพาะ object ที่ pointer ชี้โดนกับ parent ของมัน ถ้าอยู่บน Canvas จะโดน panel ระหว่างทางดักก่อน

---

## 2 · สถานะการต่อสาย (จากซีนจริง)

### ต่อครบแล้ว

| Component | หมายเหตุ |
|---|---|
| `CharacterCarousel` | hero + ปุ่ม ▲▼ ครบ |
| `LobbyUI` | `mapButton` / `mapImage` / `characterImage` ครบ |
| `MapSelectUI` | ขาดแค่ `detailSceneName` ซึ่งเป็น optional |

### ยังว่าง — ต้องทำ

| Component | ช่อง | ผลถ้าไม่ทำ |
|---|---|---|
| `CharacterSelectUI` | `statHpValue` `statAtkValue` `statDefValue` `statSpdValue` `statCritRateValue` `statCritDmgValue` | แท็บ Stats เปิดได้แต่ไม่มีตัวเลข |
| `CharacterSelectUI` | `selectButton` | ไม่มีปุ่ม Select — คลิกการ์ดยืนยันทันทีตามเดิม |
| `CharacterSelectUI` | `confirmButtonLabel` | โค้ดหา label จาก child ของปุ่มให้เอง (fallback) |
| `CharacterSelectUI` | `lockStatusText` `goldText` | ผู้เล่นไม่เห็นยอดทองและเหตุผลที่ซื้อไม่ได้ |
| `MapCarousel` | **`previewImage`** | **ผิดจังหวะ: `previewGroup` ต่อแล้วแต่ `previewImage` ว่าง** → ภาพใหญ่จาง/โผล่ตามจังหวะ แต่รูปไม่เคยเปลี่ยนตามแมพ |

`MapCarousel.previewImage` เป็นช่องเดียวที่ทำให้เกิดพฤติกรรมครึ่งๆ ควรต่อก่อนเพื่อน

---

## 3 · ค่าที่ผูกกันอยู่ ห้ามแก้ตัวเดียว

| ค่า A | ค่า B | ความสัมพันธ์ |
|---|---|---|
| `CarouselBase.pitch` = 250 | ความสูงการ์ด | ต้องเท่ากันถึงจะชิดพอดี · ตอนนี้ CharacterCard 180 (ห่าง 70) · MapCard 215 (ห่าง 35) |
| `BunnyHop.knockBackForce` 1.5 | รัศมี AoE | ผลักไกลกว่ารัศมี = ศัตรูหลุดวงระเบิดลูกที่สอง ดาเมจหายโดยไม่มีอะไรฟ้อง |
| `ChargeManager.chargePerUnit` 10 | `BunnyHop.dashDistance` 6 | dash ไม่ถูกนับเป็นระยะชาร์จแล้ว · ค่า 10 ยังไม่ได้จูนใหม่หลังตัด dash ออก |
| `edgeAlpha` 0.45 | `clipToPanel` | ปิด `clipToPanel` เมื่อไหร่ ใบที่ ±2 จะไม่ถูกตัด ต้องพึ่ง alpha กลบแทน |

**หน่วยเทียบกับ LoL Swarm:** Valor RANGE 250 ต้นฉบับ = `ld.range` 4 ของเรา → **1 หน่วย Unity ≈ 62.5 หน่วย Swarm**

---

## 4 · สถานะและปฏิสัมพันธ์

### Carousel (ใช้ร่วมทั้งตัวละครและแมพ)

| องค์ประกอบ | สถานะ | พฤติกรรม |
|---|---|---|
| การ์ดใบกลาง | default | `centerScale` 1.0 · alpha 1.0 · ปิด mask ของตัวเอง (ภาพล้นกรอบ) |
| การ์ดห่าง 1 ช่อง | — | scale/alpha ไล่ต่อเนื่องตามระยะ ไม่ใช่ขั้นบันได |
| การ์ดห่าง 2 ช่อง | — | `edgeScale` 0.65 · `edgeAlpha` 0.45 · ถูกขอบแผงตัดเหลือ ~30% |
| ลากนิ้ว | dragging | การ์ดตามนิ้ว 1:1 · ภาพใหญ่จางหาย |
| ปล่อยนิ้ว | settling | ไถลเข้าช่องใกล้สุดใน `snapSmoothTime` 0.12 วิ |
| เข้าช่องแล้ว | settled | ยิง `OnSettled` **ครั้งเดียว** → คอมมิต → ภาพใหญ่โผล่กลับ |
| คลิกใบที่ไม่อยู่กลาง | — | หมุนใบนั้นมาที่กลาง |
| ลูกกลิ้ง / ปุ่ม ▲▼ | — | ก้าวละ `wheelStep` 1 ช่อง พร้อมอนิเมชัน |

**คอมมิตเกิดหลัง snap นิ่งเท่านั้น** — ลากยาวผ่าน 4 ตัวยิง RPC ครั้งเดียว ไม่ใช่ 4 ครั้ง
ทุกครั้งที่คอมมิตจะ **ปลด ready ของผู้เล่นเอง** (`LobbyState.SetCharacterServerRpc` เซ็ต `ready = false`)

### ตัวละครที่ยังล็อก

อยู่ในวงหมุน หมุนถึงได้ แต่คอมมิตไม่ได้ · ช่องกลางแปลว่า "กำลังดู" ไม่ใช่ "เลือกแล้ว"
`selectButton` ปิดการกดอัตโนมัติ (ไม่ซ่อน) · `confirmButton` โผล่มาแทนพร้อมราคา

### แท็บ Stats / Ability

เปิดมาที่ Stats เสมอ · `statsPanel` / `abilityPanel` สลับด้วย `SetActive`
**แถว passive/อาวุธ/Q/E เดิมต้องเป็นลูกของ `abilityPanel`** ไม่งั้นสลับแท็บแล้วไม่หาย

---

## 5 · ข้อมูลใน asset ที่ยังขาด

| Asset | ช่อง | สถานะ |
|---|---|---|
| `Char_Hunter` `Char_Gunner` | `portrait` | **ว่างทั้งคู่** — มีแค่ `icon` · ชั้น hero จะหายตอนหมุนไปเจอ |
| `Char_*` ทั้งสาม | `baseAttack` `baseDefense` `baseCritRate` `baseCritDamage` | **ยังไม่ถูกเขียนลงไฟล์เลย** Unity จะใช้ค่า default (0 / 0 / 5% / 50%) จนกว่าจะเปิด asset แล้วเซฟ |
| `MapData_Arena01` | `previewImage` | มีแล้ว ✓ |

`baseAttack` ปล่อย 0 ได้ — โค้ดจะดึงดาเมจจาก `startingWeapon` ให้แทน

**โรสเตอร์แมพมีตัวเดียว** → carousel 6 ช่องจะโชว์ Arena 01 ซ้ำหกใบ ตามกติกา "ยอมให้ซ้ำ" ที่ตกลงไว้
ถ้าอยากให้ดูปกติระหว่างนี้ ลด `MapCarousel.viewCount` เหลือ 3 (base บังคับขั้นต่ำ 3)

---

## 6 · Motion

| องค์ประกอบ | ทริกเกอร์ | อนิเมชัน | ระยะเวลา | Easing |
|---|---|---|---|---|
| การ์ดใน carousel | ปล่อยนิ้ว / ลูกกลิ้ง / ปุ่ม | ไถลเข้าช่อง | `snapSmoothTime` 0.12 วิ | `Mathf.SmoothDamp` |
| scale / alpha การ์ด | ตำแหน่งเปลี่ยน | ไล่ตามระยะจากกลาง | ต่อเนื่องรายเฟรม | linear (`Mathf.Lerp`) |
| ภาพใหญ่ (hero / preview) | เริ่มลาก → จาง · settle → โผล่ | fade | `heroFadeSpeed` 6 หน่วยอัลฟา/วิ ≈ 0.17 วิ | linear, `unscaledDeltaTime` |
| ระเบิดลูกที่สอง (RabbitHop) | cast คู่ + Super | หน่วงแล้วระเบิดซ้ำที่จุดเดิม | `secondBlastDelay` 0.4 วิ | — |

ภาพใหญ่ใช้ `unscaledDeltaTime` โดยตั้งใจ — เมนูต้องลื่นแม้ `timeScale` เป็น 0

---

## 7 · อินพุตและการเข้าถึง

เทมเพลตเดิมของ skill พูดถึง ARIA/screen reader ซึ่งไม่มีใน Unity uGUI · ส่วนที่เทียบเท่าและสำคัญจริงคือ

- **ปุ่มบนการ์ดตั้ง `Navigation.Mode.None`** และเคลียร์โฟกัสหลังคลิก — เคยมีบั๊กที่ Enter/Space ไปยิงการ์ดที่โฟกัสค้างแล้วเปลี่ยนตัวละครโดยผู้เล่นไม่รู้ตัว
- **Q / E ถูกจองโดย `TabBar`** (สลับแท็บ) · Esc = PauseMenu · F3 = debug HUD · ลูกศร/WASD/ลูกกลิ้งว่าง
- **ภาพใหญ่ปิด raycast อัตโนมัติ** (`blocksRaycasts=false`, `raycastTarget=false`) ไม่งั้นมันทับช่องกลางแล้วดูดคลิกไปหมด
- ยังไม่มีการรองรับ gamepad หรือคีย์บอร์ดล้วนสำหรับ carousel — เลื่อนได้ด้วยเมาส์ ปุ่ม ▲▼ และการคลิกการ์ดเท่านั้น

---

## 8 · ที่ยังเปิดอยู่

- **`IndexOutOfRangeException` ที่ `Selectable.OnEnable`** — ยังไม่ทราบต้นเหตุ · `s_SelectableCount` เป็น static ระดับเกม เพี้ยนครั้งเดียวแล้วทุก Selectable ที่ enable หลังจากนั้นพังหมด · วิธีตัด: Stop→Play ใหม่ (หายคือ static ค้างข้ามรอบ) แล้วลองปิด component carousel
- **`weapon?.` ใน `ChargeManager:101`** — `?.` ของ C# ไม่รู้จัก destroyed object ของ Unity · หน้าต่าง 1 วิตอนอัปเป็น Super จะโยน `MissingReferenceException`
- **CHARGE ที่เต็มถูกทิ้งเงียบ** — `CurrentCharge = 0f` เกิดก่อนเช็ก `weapon` null
- **ability ไม่เคยเลเวลอัป** — `AbilityBase.SetLevel` ไม่มีผู้เรียก · ตาราง 5 เลเวลของ Valor/Exile เป็นข้อมูลตาย
- **`AD_Riven_BladeOfExile` เลเวล 2 แย่กว่าเลเวล 1** ทั้ง cooldown และ duration
- **`Char_Gunner 1` / `Char_Gunner 2`** ถูกอ้างในซีนแล้ว ทั้งสามไฟล์มี `characterName: Gunner` เหมือนกัน — ใช้เทสต์ carousel ได้แต่ห้ามหลุดไป commit เพราะ `characterName` เป็นตัวระบุบนเน็ตเวิร์ก
- **`StormBunnyWeapon` ไม่ได้อัปเดตตาม `FireBlast`** — fusion ยังเข้าถึงไม่ได้ (ไม่มี WeaponData ไม่มี recipe)

---

## 9 · ลำดับที่แนะนำ

1. ต่อ `MapCarousel.previewImage` — จุดเดียวที่ตอนนี้ให้พฤติกรรมครึ่งๆ
2. ใส่ `portrait` ให้ Hunter กับ Gunner
3. ต่อหกช่อง stat แล้วเปิด asset ตัวละครเซฟทีนึงเพื่อให้ `baseAttack` ฯลฯ ถูกเขียนลงไฟล์
4. ตัดสิน `IndexOutOfRangeException` ด้วยขั้นตอนในข้อ 8
5. แก้ `weapon?.` กับ charge ที่ถูกทิ้ง (งานซ่อมล้วน ไม่แตะบาลานซ์)
