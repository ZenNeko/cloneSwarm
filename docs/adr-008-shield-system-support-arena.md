# ADR-008: รื้อระบบ Shield และซ่อม SupportArenaWeapon

**Status:** Accepted — **กลไกลงมือแล้วครบ 2026-08-15** · เหลือเฉพาะข้อบาลานซ์ที่รอเจ้าของตัดสิน
คอมไพล์ผ่าน 0 error · EditMode test 44/44 ผ่าน (13 ตัวเป็นของ `ShieldStack`)
**Date:** 2026-08-15
**Deciders:** เจ้าของโปรเจกต์
**ที่มา:** รีวิว `SupportArenaWeapon` + สแกนระบบ shield ทั้งเกม + ค้นคว้าเทียบ LoL SR / LoL Swarm / TFT / FF14

---

## Context

รีวิว `SupportArenaWeapon` แล้วพบว่าบั๊กที่หนักที่สุดไม่ได้อยู่ที่ตัวอาวุธ แต่อยู่ที่ **ระบบ shield กลาง**
ซึ่งทุกอาวุธใช้ร่วมกัน · สแกนทั้งเกมแล้วมีแหล่งเติมโล่ 4 แหล่ง ปลายทางเดียว

| แหล่ง | เรียกผ่าน | คุมค่าไหม |
|---|---|---|
| `BunnyHopWeapon:159` | `AddShieldServerRpc` | ❌ ไม่ตรวจอะไรเลย |
| `StormBunnyWeapon:73` | `AddShieldServerRpc` | ❌ ไม่ตรวจอะไรเลย |
| `PlayerAugmentManager:262` | `AddShield` ตรง | ✅ `Clamp(0, 1000)` |
| `SupportArenaPillar:105` | `AddShield` ตรง | ❌ ให้ `maxHealth` ต่อทิก |

**"Shield" ของ Elite เป็นคนละระบบ** — `EliteController:93` บวก `shieldHP` เข้า `maxHealth` ของศัตรูตรงๆ
ไม่แตะ `netShieldHP` ของผู้เล่นเลย · ชื่อชนกันเฉยๆ ไม่ต้องรื้อพร้อมกัน

---

## บั๊กที่ต้องแก้ เรียงตามความหนัก

### 🔴 CRITICAL — โล่เติมตัวเองกลับหลังกันดาเมจ

`playermove.cs:120-126` คำนวณการสลายแบบ absolute จาก snapshot:

```csharp
netShieldHP.Value = Mathf.Lerp(shieldAtLastAdd, 0f, elapsed / effectiveDuration);
```

แต่ `playermove.cs:184-190` ที่หักโล่ตอนโดนตี **ไม่อัปเดต `shieldAtLastAdd`** เฟรมถัดไปสมการจึงเขียนทับ

| เวลา | เกิดอะไร | `netShieldHP` |
|---|---|---|
| 0.0 | `AddShield(100)` | 100 |
| 1.0 | สลายตามเวลา | 66.7 |
| 1.1 | โดนตี 50 | **16.7** |
| 1.2 | `Update` คำนวณ `Lerp(100, 0, 0.4)` | **60.0** ← เด้งกลับ |

ติดค้างที่ 0 เฉพาะตอนถูกหักจนหมดพอดี เพราะ guard `netShieldHP.Value > 0f` ตัดการสลายทิ้ง
**กระทบทุกแหล่งพร้อมกัน** โล่ทุกใบในเกมทนกว่าตัวเลขที่ออกแบบไว้มาตลอด และไม่มีอะไรฟ้อง

### 🔴 CRITICAL — บัฟ Support Arena ค้างถาวรถ้าอาวุธถูกทำลายก่อนเสาหมดอายุ

`SupportArenaPillar` เรียก `RemoveBuffs` ผ่าน `weapon` ซึ่งเป็นคอมโพเนนต์บน GameObject ลูกของผู้เล่น
`ReplaceWeapon()` สั่ง `Destroy` ทิ้งตอนอัปเป็น Super → `weapon` เป็น null → guard `if (weapon != null)`
ที่ `SupportArenaPillar.cs:158` และ `:178` ตัด `RemoveBuffs` ทิ้งทั้งคู่

ผลคือ **+20% ดาเมจ +20% ความเร็ว +20 haste ติดตัวผู้เล่นไปตลอดเกม** ไม่มี error
`Update()` ก็หยุดฮีลเงียบๆ ด้วยเหตุเดียวกัน และ `:137` ยัง `buffedPlayers.Add(pm)` อยู่นอก guard
คนที่เดินเข้ามาหลังอาวุธตายจึงถูกบันทึกว่าได้บัฟแล้วทั้งที่ไม่เคยได้

### 🟡 WARNING

- `AddShieldServerRpc` (`PlayerWeaponManager.cs:1046`) ไม่ clamp ไม่กันค่าติดลบ · ทั้งจำนวนโล่และตัวตั้ง
  (`damage × shieldPercent × จำนวนศัตรู`) คำนวณบน client · เส้นทาง augment กันไว้แล้ว เส้นทางอาวุธไม่กัน
- หน้าต่างสลายรีเซ็ตทุกครั้งที่เติม — แหล่งที่เติมถี่กว่า `shieldDuration` ทำให้โล่ไม่มีวันสลาย
- `WD_superSupportArena` ตั้ง `duration: 0` → โค้ดตกไปใช้ `arenaDuration` บน prefab = **1000 วินาที**
  ยาวกว่าเกมทั้งเกม (900 วิ) เสา Super จึงไม่มีวันหมดอายุ
- `evoRegenBonus` ไม่เคยถูกใช้ — ปลายทาง `playermove.tempHealthRegenBonus` มีจริง ถูกอ่านที่
  `playermove.cs:115` คอมเมนต์เขียนว่า *"จาก SupportArenaWeapon"* แต่ไม่มีใครเขียนค่าลงไป
- แถบโล่บน HUD ตันที่ `maxHP * 0.5` (`GameHUD.cs:145`) — ออกแบบมาสำหรับโล่เล็ก
- `shieldDuration` มีสองค่า: โค้ด default `1f` (`playermove.cs:20`) vs prefab `3`
- Material สร้างตอนรันไทม์ 3 จุดใน `SupportArenaWeapon` ไม่เคย `Destroy` และ `Shader.Find`
  คืน null ได้ในบิลด์ถ้า shader ไม่อยู่ใน Always Included
- `Debug.Log` ยิงทุกทิกต่อผู้เล่นทุกคนในวง

---

## งานค้นคว้า — เกมอื่นตัดสินใจอย่างไร

| | สแต็ก | อายุ | เติมซ้ำ | ขนาดอ้างอิง |
|---|---|---|---|---|
| **LoL SR** | บวกกันทุกแหล่ง | pool **และ** เวลา หมดอันไหนก่อนก็จบ | แต่ละแหล่งเป็นชั้นแยก | ไม่มีเพดาน |
| **LoL Swarm** | โล่ไม่ใช่แกนการรอด | สั้น / บล็อกครั้งเดียว | — | เล็ก |
| **TFT** | บาง trait ไม่สแต็ก | ~4 วิ มักสลายเร็ว | รีเฟรช/แทนที่ | 40% ของ maxHP |
| **FF14** | ชื่อเดียวกันไม่สแต็ก ชื่อต่างสแต็กได้ | เวลาคงที่ ไม่สลายทีละนิด | ชื่อเดียวกันทับทิ้ง | อิงพลังฮีล |
| **ของเรา** | บวกเข้ากองเดียวไร้ตัวตน | Lerp จาก snapshot | รีเซ็ตนาฬิกาทั้งก้อน | ไม่มีเพดาน |

**ข้อสรุปที่ใช้ได้จริงสามข้อ**

1. **ทุกเกมมองโล่เป็น "กองที่ถูกใช้" ไม่ใช่ "ตัวเลขที่ถูกอนิเมทลงศูนย์"** — LoL แยก "ดูดดาเมจครบ"
   กับ "หมดเวลา" เป็นสองเงื่อนไขที่ไม่แตะกัน · เรารวมเป็นสมการเดียว การหักจึงถูกเขียนทับ
2. **LoL แยก "สลาย" ออกจาก "หมดอายุ" เป็นคนละเรื่อง และใช้เป็นคันโยกบาลานซ์**
   Sterak's Gage สลายตลอด 4.5 วิ · Immortal Shieldbow อยู่เต็มค่าครบ 3 วิแล้วหายทันที
   ของเราสลายโดยไม่ได้ตั้งใจให้เป็นคันโยก มันเป็นผลข้างเคียงของวิธีเขียน
3. **ไม่มีเกมไหนให้การเติมครั้งใหม่ยืดอายุยอดสะสมทั้งก้อน** ซึ่งเป็นสิ่งที่ `AddShield` ทำอยู่

**ขนาดของเราอยู่นอกกรอบ** — TFT ให้เยอะสุด 40% ของ maxHP นาน 4 วิ · LoL Swarm ซึ่งเป็นเกมอ้างอิงตรง
แทบไม่ใช้โล่เลย ตัวรอดหลักคือเลือดสูงสุดกับการฮีล · Support Arena ของเราให้ **100% ของ maxHP ทุกวินาที**

น่าสังเกต: Swarm มีของที่ตรงกับ BunnyHop เป๊ะ คือ Paw Print Poisoner ร่างพัฒนา ที่ให้ "โล่สะสมตามดาเมจที่ทำได้"
แนวคิดนี้มีที่ยืนจริงในเกมแนวนี้ — **ตัวที่ผิดปกติคือ Support Arena ไม่ใช่ BunnyHop**

---

## Decision

### D1 · โล่เป็น "รายการชั้น" ฝั่ง server ไม่ใช่ตัวเลขเดียว (โมเดล LoL)

```csharp
struct ShieldLayer { public float amount; public float expireAt; public bool decays; public float initialAmount; }
List<ShieldLayer> _layers;      // server เท่านั้น
NetworkVariable<float> netShieldHP;   // ผลรวม replicate ให้ HUD เหมือนเดิม
```

ดูดดาเมจจากชั้นที่ **ใกล้หมดอายุที่สุดก่อน** ตามที่ LoL ทำ — ชั้นที่จะหายอยู่แล้วถูกใช้ให้คุ้มก่อน

**เหตุผลที่เลือกโมเดลนี้** เรามีแหล่งโล่สามชนิดที่พฤติกรรมควรต่างกันอยู่แล้วแต่กองเดียวแสดงไม่ได้:

| แหล่ง | ควรเป็น | เทียบกับ |
|---|---|---|
| BunnyHop / StormBunny | ก้อนใหญ่จากดาเมจ สลายต่อเนื่อง | Sterak's Gage |
| Augment Second Wind | ก้อนเล็กคงที่ อยู่ครบเวลา | Immortal Shieldbow |
| Support Arena | ต่อเนื่องขณะยืนในวง **รีเฟรช ไม่สะสม** | TFT |

`netShieldHP` ยังส่งเป็นผลรวมตัวเดียว **HUD กับเน็ตเวิร์กไม่ต้องแก้**

### D2 · การสลายเป็นการลบรายเฟรม ไม่ใช่ Lerp จาก snapshot

```csharp
layer.amount -= (layer.initialAmount / duration) * Time.deltaTime;
```

การหักจากดาเมจกับการสลายบวกกันได้ตรงๆ ไม่มีใครเขียนทับใคร · ปิดบั๊ก CRITICAL ข้อแรกที่ต้นเหตุ

### D3 · `AddShieldServerRpc` ต้องตรวจค่าที่ client ส่งมา

ให้ตรงกับที่เส้นทาง augment ทำไว้แล้ว — `Mathf.Clamp(amount, 0f, เพดาน)`
เพดานควรอิง `maxHealth` ไม่ใช่เลขคงที่ 1000 เพราะเลือดสูงสุดโตตาม talent/augment

### D4 · เสา Support Arena ต้องไม่พึ่ง reference ไปยังอาวุธ

`SupportArenaPillar` มีค่าบัฟครบอยู่แล้วตั้งแต่ `Init` (`dmgBuff`/`moveBuff`/`hasteBuff`)
ให้ apply/remove ในตัวเสาเอง เลิกเด้งไปเรียกเมธอดบน `SupportArenaWeapon`
เสาจะรอดแม้เจ้าของอาวุธจะอัปเป็น Super หรือหลุดออกจากเกม

`isServer` ก็ต้องหาจากทางอื่น — เก็บ `bool` ที่ส่งมาตอน `Init` แทนการอ่านผ่าน `weapon.manager`

### D5 · Support Arena เลิกสะสมโล่ เปลี่ยนเป็นรีเฟรชชั้นเดียว

ตามโมเดล TFT · ยืนในวงแล้วมีโล่ค่าคงที่ที่ถูกต่ออายุเรื่อยๆ ออกจากวงแล้วสลาย
แทนพฤติกรรมปัจจุบันที่พอกไปเรื่อยจนลู่เข้า 3 เท่าของเลือดสูงสุด

---

## ต้องให้เจ้าของตัดสิน — ไม่ใช่เรื่องที่โค้ดตอบได้

1. **โล่ Support Arena ควรเป็นเท่าไหร่** · อ้างอิง TFT = 40% ของ maxHP · ตอนนี้คือ 100% ต่อวินาที
2. **`arenaDuration: 1000` บน `SuperSupportArenaWeapon.prefab` ตั้งใจหรือพิมพ์ผิด**
   ถ้าตั้งใจให้ถาวรจริง ต้องรีบาลานซ์ทั้งชุด ถ้าพิมพ์ผิดก็แค่แก้ตัวเลข
3. **`evoRegenBonus` จะต่อสายหรือลบทิ้ง** · ปลายทางมีอยู่แล้วรออยู่ ต่อสายใช้เวลาสองบรรทัด
4. **D2 จะทำให้โล่ทุกใบอ่อนลงจากที่เล่นกันอยู่** — นี่คือการเปลี่ยนความรู้สึกของเกม ไม่ใช่แค่ซ่อมบั๊ก
   อาจต้องขยับ `shieldPercent` ของ BunnyHop ขึ้นชดเชย ต้องเล่นจริงถึงจะรู้
5. **`WD_SupportArena` ฮีลลดลงตอนเลเวล 2** (`ld.damage` = จำนวนฮีล ไล่ 25 → 20 → 20 → ...)
   อัปเลเวลแล้วแย่ลง — ตั้งใจหรือเปล่า

---

## แผนลงมือ เรียงตามลำดับที่ปลอดภัย

**รอบที่ 1 — ซ่อมของที่พังโดยไม่เปลี่ยนความรู้สึกเกม**

1. D4 · ตัด `weapon` ออกจาก `SupportArenaPillar` — ปิด CRITICAL ข้อสอง ไม่กระทบตัวเลขใดๆ
2. D3 · clamp ใน `AddShieldServerRpc` — กันค่าติดลบและค่าเกินจริง
3. ลบ `Debug.Log` รายทิก · ลบ `lastShieldTime` ที่ไม่เคยถูกเขียน
4. ย้าย material ที่สร้างรันไทม์ไปเป็น asset จริง หรือ `Destroy` ให้ครบ
5. ตัดสินข้อ 2 กับ 3 ด้านบน (`arenaDuration`, `evoRegenBonus`)

**รอบที่ 2 — รื้อระบบ ต้องเทสต์ด้วยการเล่นจริง**

6. D1 + D2 · เปลี่ยน `netShieldHP` เป็นรายการชั้น + การสลายแบบลบรายเฟรม
7. D5 · Support Arena เปลี่ยนเป็นรีเฟรชชั้นเดียว
8. ปรับสเกลแถบ HUD ให้ตรงกับขนาดจริงหลังตัวเลขนิ่ง
9. รวม `shieldDuration` ให้เหลือแหล่งความจริงเดียว

**เทสต์ที่ต้องมี** — คณิตของชั้นโล่เป็นคณิตล้วน เทสต์ได้แบบเดียวกับ `TelegraphGeometry`:
ดูดจากชั้นใกล้หมดอายุก่อน · หักบางส่วนแล้วสลายต่อไม่เด้งกลับ · หลายชั้นหมดอายุพร้อมกัน · ค่าติดลบ

---

## Consequences

**ดีขึ้น** — การหักดาเมจกับการสลายเลิกเขียนทับกัน · แต่ละแหล่งโล่มีบุคลิกของตัวเองได้
· ค่าจาก client ถูกตรวจก่อนเชื่อ · เสาไม่ทิ้งบัฟค้าง

**แย่ลง** — `playermove` ถือ state มากขึ้น (list แทน float) · ต้องระวัง list โตถ้าแหล่งโล่ยิงรัว
(กัน: รวมชั้นที่มาจากแหล่งเดียวกันและหมดอายุใกล้กัน หรือจำกัดจำนวนชั้น)

**ต้องกลับมาทบทวน** — ถ้าเพิ่มแหล่งโล่ที่ต้องการกฎ "ชื่อเดียวกันทับทิ้ง" แบบ FF14
รายการชั้นรองรับได้อยู่แล้วโดยเพิ่มฟิลด์ `sourceId` ADR นี้วางทางไว้ให้แล้ว

**ที่ตัดสินว่าไม่ทำ** — โมเดล FF14 (ชื่อเดียวกันทับทิ้ง) เป็นกฎหลักของทั้งระบบ
ออกแบบมาสำหรับ healer ไม่กี่คนที่ร่ายทีละครั้งอย่างตั้งใจ ในเกมที่โล่ยิงรัวอัตโนมัติหลายสิบครั้งต่อนาที
กฎนี้จะให้ความรู้สึกว่า "ของหาย" โดยไม่มีใครเข้าใจ — แม้ในหมู่ผู้เล่น FF14 เองก็เป็นเรื่องที่บ่นกันประจำ

---

## Action Items

1. [x] ตัด `weapon` ออกจาก `SupportArenaPillar` — apply/remove บัฟในตัวเสาเอง (D4)
2. [x] clamp + กันค่าติดลบใน `AddShieldServerRpc` เพดานอิง `maxHealth` (D3)
3. [x] ลบ log รายทิก · ลบ `lastShieldTime` · ซ่อม material ที่สร้างรันไทม์
4. [x] ตัดสินแล้ว 2026-08-15 — `arenaDuration: 1000` **เป็นการพิมพ์ผิด** แก้เป็น `15` ·
       `evoRegenBonus` **ต่อสายแล้ว** ไปที่ `playermove.tempHealthRegenBonus` (เฉพาะร่าง evo)
5. [x] `netShieldHP` → รายการชั้น + ดูดจากชั้นใกล้หมดอายุก่อน (D1)
6. [x] การสลายเป็นการลบรายเฟรม (D2)
7. [x] Support Arena เปลี่ยนเป็นรีเฟรชชั้นเดียว (D5)
8. [x] EditMode test ของคณิตชั้นโล่ — 13 ตัว รวม regression ของบั๊ก CRITICAL
9. [ ] ปรับสเกลแถบ HUD + รวม `shieldDuration` ให้เหลือค่าเดียว — **รอตัวเลขนิ่งก่อน**

### ที่พบเพิ่มระหว่างลงมือ

**`GetEffectiveShieldDuration()` ตรึงตัวคูณ Duration stat ไว้ตอนสร้างชั้น** ไม่ได้อ่านสดทุกเฟรม
เป็นผลจำเป็นของ D2 (ต้องมี `duration` คงที่เป็นตัวตั้งของอัตราลบ) · ผลคือการเปลี่ยน Duration stat
ระหว่างที่โล่ยังไม่หมด จะไม่ย้อนไปดัดโค้งสลายของชั้นที่มีอยู่แล้ว มีผลกับชั้นที่สร้างหลังจากนั้นเท่านั้น
เป็นกลไก ไม่ใช่ตัวเลขบาลานซ์ แต่บันทึกไว้เพราะ ADR ไม่ได้เขียนไว้ล่วงหน้า

**`ShieldStack.Absorb` ห้ามจองหน่วยความจำ** — ถูกเรียกทุกครั้งที่ผู้เล่นโดนตี ซึ่งในเกมแนวนี้คือ
หลายครั้งต่อวินาทีต่อคน · เวอร์ชันแรกใช้ `List<int>` + `Sort` + `RemoveAll` ซึ่งจองทั้งลิสต์และ closure
เปลี่ยนเป็นสแกนหาค่าน้อยสุดซ้ำ (ชั้นตามปกติมี 1-3 ชั้น) และลบจากท้ายไปหน้า — ไม่จองอะไรเลย

---

## แหล่งอ้างอิงงานค้นคว้า

- [Shield — League of Legends Wiki](https://wiki.leagueoflegends.com/en-us/Shield)
- [Sterak's Gage](https://wiki.leagueoflegends.com/en-us/Sterak's_Gage) · [Immortal Shieldbow](https://wiki.leagueoflegends.com/en-us/Immortal_Shieldbow)
- [Understanding the Purpose of Decaying Shields in LoL](https://www.zleague.gg/theportal/understanding-the-purpose-of-decaying-shields-in-league-of-legends/)
- [Swarm — League of Legends Wiki](https://wiki.leagueoflegends.com/en-us/Swarm) · [All Augments in LoL Swarm](https://dotesports.com/league-of-legends/news/all-augments-in-lol-swarm)
- [Protector (TFT)](https://leagueoflegends.fandom.com/wiki/Protector_(Teamfight_Tactics)) · [Item stacking in TFT](https://www.esportstales.com/teamfight-tactics/item-stacking-in-tft)
- [Galvanize (FFXIV)](https://ffxiv.consolegameswiki.com/wiki/Galvanize) · [Divine Benison (FFXIV)](https://ffxiv.consolegameswiki.com/wiki/Divine_Benison)
