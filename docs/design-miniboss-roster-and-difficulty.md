# มินิบอสตามเวลา + บอสตามระดับความยาก — ออกแบบ

สถานะ: **ทำแล้ว ก + ข + ค** (2026-09-26) — ใช้แบบ §10 · ฝั่งผู้เล่น (HP/ชุบ) ยังไม่ทำ · หน้าจอเส้นเวลาใน MapData ยังไม่ทำ
โค้ด: `DifficultyProfile` · `TimelineClip.limitTiers/minTier/maxTier` · `MapData.miniBosses` + `TierContent.miniBossOverrides` · `BossManager` (HP ระดับ × จำนวนคน · เลือกจากรายชื่อแมพ) · `DifficultySetup` (สร้างไฟล์เริ่ม)

คำถามจากเจ้าของเกม:
1. อยากกำหนดใน MapData ว่า **มินิบอสตัวไหนเกิดนาทีไหน**
2. ถ้าเปลี่ยนระดับความยากของด่าน จะ **ปรับ boss config แต่ละตัวยังไง**

---

## 0. ตอนนี้เป็นยังไง (อ่านจากโค้ดจริง)

```
MapData_Arena01
 └─ tiers[]            ← มีแค่ Normal ตัวเดียว
     ├─ mainBossConfig   (ว่าง = ใช้ของบน prefab)
     ├─ miniBossConfig   (ว่าง = ใช้ของบน prefab)   ⚠ ตัวเดียวทับ "ทุก" มินิบอส
     ├─ schedule.cues[]  MiniBoss: นาที 2, 4, 9, 11, 14 · variant = ""  → สุ่ม
     └─ enemyScaling     HP/ความเร็วศัตรูต่อ wave

SampleScene › BossManager
 ├─ miniBossPrefabs[]  = Purple 1, newRed        ← รายชื่อมินิบอสอยู่ในซีน ไม่ใช่ในแมพ
 ├─ miniBossBaseHealthMult = 1                   ← ค่าในซีน ไม่แยกตามระดับ
 └─ mainBossPrefab
```

ข้อเท็จจริงที่เจอ:

| # | เรื่อง | ผล |
|---|---|---|
| F1 | cue แบบ MiniBoss ระบุตัวได้ผ่าน `variant` = **ชื่อ prefab** ในลิสต์ของซีน | ทำได้แล้วบางส่วน แต่ต้องพิมพ์ชื่อให้ตรงกับของในซีน และ 1 cue = 1 ตัว |
| F2 | `miniBossConfig` ของ tier **ทับทุกตัว** | ถ้าตั้งเมื่อไหร่ Purple กับ Red จะได้ท่าชุดเดียวกัน — ทำให้ "มินิบอสคนละตัว" ไม่มีความหมาย |
| F3 | บอสใหญ่ **ไม่ถูกสเกล HP เลย** (ไม่เรียก `ApplyWaveScaling`) | Easy/Hard/Epic HP บอสใหญ่เท่ากันหมด · 1 คนกับ 4 คนก็เท่ากัน |
| F4 | HP มินิบอส = HP prefab × `miniBossBaseHealthMult` (ซีน) × ตัวคูณ wave | ปรับตามระดับได้ทางอ้อมผ่าน `enemyScaling` เท่านั้น |
| F5 | Arena01 มี tier เดียว (Normal) — `GetTier` ถอยไป Normal | **ตอนนี้เลือกระดับไหนก็เล่นเหมือนกันทุกอย่าง** |
| F6 | จำนวนผู้เล่นไม่มีผลกับ HP บอส/มินิบอส | co-op 4 คนละลายบอสเร็วกว่าเดี่ยว 4 เท่า |

---

## 1. ความต้องการ

**ต้องทำได้**
- R1 ต่อแมพ: ประกาศว่ามีมินิบอสตัวไหนบ้าง (prefab + ท่า)
- R2 ต่อระดับ: ตารางว่านาทีไหนออกตัวไหน — ระบุตัว / สุ่มจากบางตัว / สุ่มทั้งหมด
- R3 ต่อระดับ: ความยากของบอสแต่ละตัว — ตัวเลข (HP, ดาเมจ, เวลาเตือน, ความถี่ท่า) และ **ท่าที่ต่างกันได้** ในระดับสูง
- R4 เปิดดูครั้งเดียวรู้ว่า "ระดับนี้ นาทีไหน ใครออก แรงแค่ไหน"

**ข้อจำกัด**
- ทำคนเดียว + ใช้ Boss Designer เป็นหลัก → ต้องน้อยไฟล์ ไม่ต้องจำชื่อพิมพ์เอง
- ของเดิมต้องเล่นได้เหมือนเดิมถ้าไม่ได้แตะ (migration เงียบ)
- ค่าทั้งหมดใช้บน server ที่เดียว (BossManager / BossController) — ไม่มี state ใหม่ต้อง sync

---

## 2. ภาพรวมที่เสนอ

```
MapData
 ├─ miniBosses[]  ── รายชื่อมินิบอสของแมพ (ครั้งเดียว ใช้ทุกระดับ)
 │    { id: "purple", prefab: Purple 1, config: MiniBossConfig Purple 1 }
 │    { id: "red",    prefab: newRed,   config: MiniBossConfig }
 │
 └─ tiers[]
      ├─ schedule.cues[]  MiniBoss: { นาที [2, 9], ตัว: "purple" }
      │                    MiniBoss: { นาที [4, 11], ตัว: "red" }
      │                    MiniBoss: { นาที [14], ตัว: "" = สุ่มจาก roster }
      │
      ├─ bossTuning       ── ตัวคูณของระดับนี้ (ใช้กับบอสทุกตัว)
      │    HP ×1.0 · ดาเมจ ×1.0 · เวลาเตือน ×1.0 · ช่วงห่างท่า ×1.0 · HP ต่อผู้เล่นเพิ่ม +35%
      │
      ├─ mainBossConfig   ── ว่าง = ใช้ของ prefab · ใส่ = ท่าชุดของระดับนี้ (เช่น Savage)
      └─ miniBossOverrides[]  ── { id: "purple", config: Purple_Savage }  (เฉพาะตัวที่อยากเปลี่ยนท่า)

             │ server เท่านั้น
             ▼
BossManager.SpawnMiniBoss(id)
   roster[id] → prefab + config (override ของระดับ > ของ roster > ของ prefab)
   HP = prefab × tier.hp × (1 + perPlayer × (คน-1)) × wave
BossController  ← ได้ tuning ไปคูณ damage / warning / interval ตอนยิงท่า
```

---

## 3. คำถามที่ 1 — มินิบอสตัวไหนเกิดตอนไหน

### ข้อมูล

```csharp
// MapData — ระดับแมพ
[Serializable] public class MiniBossEntry
{
    public string id = "purple";              // ใช้ใน cue · ห้ามซ้ำในแมพ
    public GameObject prefab;                 // asset อ้าง prefab ได้ตรงๆ (ต่างจากของในซีน)
    public BossEncounterConfig config;        // ว่าง = ของบน prefab
}
public MiniBossEntry[] miniBosses;

// TierContent — ระดับความยาก
[Serializable] public class MiniBossOverride { [VariantId] public string id; public BossEncounterConfig config; }
public MiniBossOverride[] miniBossOverrides;  // แทน miniBossConfig เดิม (ดู migration)
```

`TimelineCue.variant` ของ MiniBoss ชี้ `id` ใน `miniBosses` แทนชื่อ prefab ในซีน —
dropdown เดิม (`VariantIdAttribute`) เปลี่ยนมาดึงจาก roster ของแมพที่เปิดอยู่ ไม่ต้องพิมพ์เอง

**ทำไม roster อยู่ระดับแมพ ไม่ใช่ระดับ tier:** ตัวที่มีในแมพเปลี่ยนน้อย แต่ตารางกับความแรงเปลี่ยนตามระดับ
ถ้าอยู่ใน tier ต้องใส่ prefab ซ้ำ 5 รอบ แล้ววันหนึ่งจะลืมอัปเดตสักระดับ

### การเลือกตัวตอนเกิด (ลำดับ)

1. cue ระบุ `id` → ตัวนั้นใน roster
2. `id` ว่าง → สุ่มจาก roster ของแมพ
3. roster ว่าง → ใช้ `BossManager.miniBossPrefabs` ในซีนแบบเดิม (แมพเก่าไม่พัง)
4. `id` ไม่มีใน roster → **เตือน** แล้วสุ่ม (เหมือนพฤติกรรมเดิมของชื่อพิมพ์ผิด)

config ที่ใช้: `tier.miniBossOverrides[id]` → `roster[id].config` → ของบน prefab

### ตัวเลือกที่ไม่ได้เลือก

| ทาง | ทำไมไม่เอา |
|---|---|
| คงชื่อ prefab ในซีน (F1) แค่ทำ dropdown ดีขึ้น | ยังแยกท่าต่อตัวไม่ได้ (F2) และรายชื่อยังอยู่ในซีน ไม่ใช่เนื้อหาของแมพ |
| "สุ่มจากบางตัว" เป็นลิสต์ id ใน cue | ยังไม่มีเคสจริง — เพิ่มทีหลังได้โดยไม่เปลี่ยนข้อมูลเดิม (ดู §6) |
| ตารางมินิบอสแยกจาก `schedule.cues` | cue มีอยู่แล้วและ GameTimeline ยิงให้ถูกจังหวะแล้ว · แยกจะมีสองที่นิยามเวลา |

---

## 4. คำถามที่ 2 — เปลี่ยนระดับแล้วปรับบอสยังไง

มีสองแบบ และ **แนะนำใช้ทั้งคู่ คนละหน้าที่**

### แบบ A — ตัวคูณของระดับ (config เดียว ใช้ทุกระดับ)

```csharp
[Serializable] public class BossTuning
{
    public float hpMult        = 1f;     // HP บอสใหญ่และมินิ
    public float hpPerExtraPlayer = 0.35f; // +35% ต่อผู้เล่นที่เพิ่มจาก 1 คน
    public float damageMult    = 1f;     // ดาเมจ AoE / โซ่
    public float warningMult   = 1f;     // เวลาเตือน (น้อยกว่า 1 = หลบยากขึ้น)
    public float intervalMult  = 1f;     // ช่วงห่างระหว่างท่า
}
```

- ใช้ตอนรัน: `BossController` ถือ tuning แล้ว `SpawnAoEActionBase.SpawnZoneAt` คูณ `damage` / `warningDuration`
  ส่วน interval คูณใน `AttackLoop` · HP คูณตอน spawn
- **ข้อดี:** แก้ท่าที่ Normal ที่เดียว ทุกระดับได้ตาม · ไม่มีไฟล์ซ้ำ
- **ข้อจำกัด:** เปลี่ยนได้แค่ตัวเลข — ท่าเหมือนเดิมทุกระดับ
- ⚠ timeline ใน Boss Designer วางคลิปตามเวลาจริง: ลดเวลาเตือน ×0.8 แต่ระยะระหว่างคลิปไม่หด → ช่องว่างระหว่างท่ายาวขึ้นนิดหน่อย
  (เหมือนที่ "สร้างเวอร์ชันความยาก" บอกไว้) · แถบปลอดภัยต้องคิดตามระดับที่เลือกดู

### แบบ B — config แยกต่อระดับ (มีแล้ว: "สร้างเวอร์ชันความยาก…")

- ก๊อปทั้งชุดแล้วแก้ **ท่า** ได้อิสระ — Savage เพิ่ม roll, ท่าซ้อน, เฟสใหม่
- **ข้อเสีย:** แก้ Normal แล้ว Savage ไม่ตาม — สองไฟล์ต้องดูแลคู่กัน

### ใช้คู่กันยังไง (แนะนำ)

| ระดับ | ท่า (config) | ตัวเลข (BossTuning) — ค่าตั้งต้นที่เสนอ |
|---|---|---|
| Easy | ใช้ Normal | HP ×0.7 · ดาเมจ ×0.6 · เตือน ×1.25 · ห่าง ×1.2 |
| Normal | **ต้นฉบับ** | ×1 ทั้งหมด |
| Hard | ใช้ Normal | HP ×1.4 · ดาเมจ ×1.3 · เตือน ×0.9 · ห่าง ×0.9 |
| Savage | **config แยก** (เพิ่มท่า) | HP ×1.8 · ดาเมจ ×1.5 · เตือน ×0.85 |
| Epic | config Savage หรือแยกอีกชุด | HP ×2.5 · ดาเมจ ×2 · เตือน ×0.8 |

กติกาเดียว: **Easy/Normal/Hard ต่างกันที่ตัวเลข · Savage ขึ้นไปต่างกันที่ท่า** (แบบ FFXIV Normal vs Savage)
ตัวเลขด้านบนเป็นจุดเริ่มสำหรับเพลย์เทส ไม่ใช่ค่าที่วัดมา

**ข้อควรแก้ในเครื่องมือ "สร้างเวอร์ชันความยาก" ถ้าใช้แบบ A:** ตอนนี้มันคูณค่าลงไฟล์ (เตือน ×0.8 ดาเมจ ×1.5)
ถ้า tier Savage มี BossTuning ด้วยจะถูกคูณสองรอบ → เครื่องมือต้องเลิกคูณตัวเลข เหลือแค่ก๊อปลึก + ผูก MapData

### HP ต่อจำนวนผู้เล่น (F6)

`HP = HP prefab × tier.hpMult × (1 + hpPerExtraPlayer × (คน − 1)) × ตัวคูณ wave (มินิเท่านั้น)`
นับจำนวนผู้เล่น **ตอนบอสเกิด** (ไม่ปรับกลางไฟต์ เวลามีคนหลุด — ไม่งั้นหลอด HP กระโดด)
0.35 ต่อคน = 4 คน HP ×2.05 · เป็นค่าเริ่มต้นให้เทส

---

## 5. Migration (ของเดิมเล่นเหมือนเดิม)

| ของเดิม | ของใหม่ |
|---|---|
| `tier.miniBossConfig` (ทับทุกตัว) | ยังอ่านได้เป็น fallback ถ้า roster ว่าง · ปุ่มใน Inspector "ย้ายเข้า roster" · เลิกใช้แล้วค่อยลบ |
| `BossManager.miniBossPrefabs` (ซีน) | ใช้เมื่อแมพไม่มี roster · สคริปต์ batch เติม roster ของ Arena01 จากค่าในซีน (purple / red) |
| `cue.variant` = ชื่อ prefab | รับทั้ง id และชื่อ prefab (id ก่อน) · สคริปต์ batch แปลงเป็น id |
| `miniBossBaseHealthMult` (ซีน) | คงไว้เป็นฐาน · tier.hpMult คูณทับ |
| ไม่มี BossTuning | ค่าเริ่ม ×1 ทั้งหมด = พฤติกรรมเดิม · HP ต่อผู้เล่น **เริ่มที่ 0** จนกว่าจะเลือกเปิด (ไม่เปลี่ยนเกมเงียบๆ) |

---

## 6. หน้าจอ

- **MapData Inspector:** ตาราง roster + ต่อระดับแสดง "เส้นเวลา" แนวนอน 0–15 นาที มีจุดมินิบอส (สีต่อตัว) จุดโซน และบอสใหญ่
  → ตอบ R4 ในหน้าเดียว
- **Boss Designer:** ป้ายบน toolbar มีอยู่แล้ว ("ใช้โดย arena01/Normal") · เพิ่มตัวเลือก "ดูแบบระดับ: Normal / Hard …"
  ให้การ์ดดาเมจ/เตือนและแถบปลอดภัยคิดตาม tuning ของระดับนั้น
- **ปุ่มทดสอบในเกม:** เพิ่มเลือกระดับ (ตอนนี้ใช้ระดับแรกที่เจอ config)

---

## 7. Trade-offs สรุป

| ตัดสินใจ | ได้ | เสีย |
|---|---|---|
| roster ระดับแมพ | ใส่ prefab ครั้งเดียว · ต่อระดับแค่ตาราง + override | ระดับหนึ่งจะ "ไม่มี" ตัวไหนต้องไม่ใส่ในตารางเอง (ไม่มีสวิตช์ปิดต่อระดับ) |
| id เป็นสตริง | cue อยู่ได้ทั้งในซีนและ asset (เหตุผลเดิมของ VariantId) | คอมไพเลอร์ไม่ตรวจ — ชดเชยด้วย dropdown + เตือนตอนรัน + audit |
| ตัวเลขผ่าน BossTuning | แก้ท่าที่เดียว | ต้องแก้ runtime 3 จุด (SpawnZoneAt, TetherAction, AttackLoop) + preview |
| ท่าผ่าน config แยก (Savage+) | อิสระเต็มที่ | ดูแลสองไฟล์ |
| HP ตามจำนวนคน | co-op สมดุลขึ้น | ต้องเพลย์เทสใหม่ทั้ง 1 และ 4 คน |

## 8. ที่จะกลับมาดูเมื่อโตขึ้น

- "สุ่มจากบางตัว" ใน cue (ลิสต์ id + น้ำหนัก) — เมื่อมีมินิบอส > 3 ตัว
- มินิบอสสองตัวออกพร้อมกัน / กันออกตัวเดิมติดกัน
- BossTuning ต่อบอส (บางตัวแรงเกินในระดับหนึ่ง) — ตอนนี้ต่อระดับพอ
- ย้าย tuning ของศัตรูทั่วไป (`enemyScaling`) กับบอสมาอยู่ในหน้าเดียวกันใน Balance Tool

## 9. ต้องตัดสินใจ

1. **HP ตามจำนวนผู้เล่น:** เปิดเลย (0.35/คน) หรือเริ่มที่ 0 ไว้ก่อน
2. **เครื่องมือ "สร้างเวอร์ชันความยาก":** เลิกคูณตัวเลขเมื่อมี BossTuning (แนะนำ) หรือคงไว้
3. **ขอบเขตรอบแรก:** (ก) roster + ตารางมินิบอสอย่างเดียว · (ข) ก + BossTuning · (ค) ทั้งหมดรวมหน้าจอเส้นเวลา

---

## 10. เกมอื่นทำยังไง (ค้นเพิ่ม 2026-09-26) — และแบบที่แก้ใหม่ตาม Rabbit and Steel

### Rabbit and Steel (แบบหลักของบอส/มินิบอส)

| เรื่อง | Cute | Normal | Hard | Lunar |
|---|---|---|---|---|
| HP ศัตรู (ตัวคูณ B) | ×0.7 | ×1.0 | ×1.1 | ×1.2 |
| เลเวลศัตรู | ต่ำ | — | สูงขึ้น (+HP อีก) | สูงขึ้นอีก |
| ท่าบอส | ชุดเดิม | ชุดเดิม | **ต่อยอดจากท่าเดิม** ซับซ้อนขึ้น | ต่อยอดอีก ("เลเซอร์เยอะขึ้น") |
| enrage | — | ผ่อน | มี · เลยเวลาแล้วใช้ท่าหลบไม่ได้ | เข้มสุด |
| ผู้เล่น | HP 8 ฟื้นเต็มทุกไฟต์ · KO 10s | HP 5 · KO 10→17→24→30s | — | HP 3 ฟื้นทีละ 1 |
| เงิน/exp | — | — | น้อยลง | น้อยลงอีก |

สูตร HP: `ฐาน × (1 + 0.05 × เลเวลศัตรู) × A(จำนวนคน) × B(ระดับ)`
A = **1 คน ×0.9 · 2 คน ×1.7 · 3 คน ×2.4 · 4 คน ×3.0** (รวมมากขึ้น แต่ต่อคนน้อยลง ไฟต์ 4 คนจบเร็วกว่า)
enrage บน Hard: มอบข้างทาง 60s · มินิบอส ~150s · บอสเฟส 1 ~160s · เฟส 2 ~210s

**หัวใจ:** บอสตัวเดียวกันทุกระดับ · ระดับสูง **เพิ่มชั้นท่า** บนท่าเดิม ไม่ได้ออกแบบบอสใหม่ · ตัวเลขแยกเป็นสูตรกลาง

### เกมอื่นที่เทียบ

| เกม | ปรับอะไร |
|---|---|
| FFXIV Normal → Savage | ไฟต์เดียวกัน ตีแรงขึ้น HP มากขึ้น · ท่าเพิ่ม/เปลี่ยน (ชั้น 4 มีเฟสใหม่) · enrage เข้มงวด |
| Brotato Danger 0–5 | บอสตัวเดิม แต่ **ตาราง** เปลี่ยน: D2 มี horde · D3 มี elite · D5 บอสสองตัวพร้อมกัน (HP ×0.75 × 1.4) |
| Hades Heat | เงื่อนไขแยกชิ้นเลือกเอง: ศัตรูตีแรง +20%/ขั้น · Extreme Measures = บอสได้ท่าใหม่ทีละตัว |
| Vampire Survivors Hyper/Inverse | ตัวคูณล้วน: ความเร็ว +65–75% · Inverse HP ศัตรู +200% และ +5%/นาที · แลกกับทอง/โชคมากขึ้น |

ข้อสังเกตรวม: ทุกเกมแยก **(1) ตัวเลขกลางของระดับ** ออกจาก **(2) เนื้อหาที่เพิ่มในระดับสูง** และแทบไม่มีใครทำ "ไฟล์บอสแยกต่อระดับ" ทั้งชุด

### แบบที่แก้ใหม่ (แทน §4 แบบ B)

**ก. ช่วงระดับบนคลิป — ท่าต่อยอดแบบ Rabbit and Steel โดยไม่ต้องก๊อปไฟล์**

```csharp
// BossTimelineAction.TimelineClip
public DifficultyTier minTier = DifficultyTier.Easy;   // คลิปนี้ออกตั้งแต่ระดับนี้
public DifficultyTier maxTier = DifficultyTier.Epic;   // ถึงระดับนี้
```
- Hard เพิ่มเลเซอร์: วางคลิปเพิ่ม ตั้ง `minTier = Hard`
- Lunar เปลี่ยนท่าเดิมเป็นแบบยาก: คลิปเดิม `maxTier = Hard` + คลิปใหม่ `minTier = Savage` ที่เวลาเดียวกัน
- Boss Designer: ตัวเลือก "ดูระดับ: Easy…Epic" บน toolbar · คลิปที่ไม่ออกในระดับนั้นจาง · คลิปที่มีช่วงระดับขึ้นป้าย `H+` / `≤N`
  แถบปลอดภัย / แผนผัง / ทดสอบในเกม คิดตามระดับที่เลือก
- ใช้ได้ทั้งบอสใหญ่และมินิบอส (มินิบอสใช้ timeline เดียวกัน)
- enrage ต่อระดับ: `BossPhase.enrageTime` เป็นค่าของ Normal · ตัวคูณ `enrageMult` ใน DifficultyProfile (Hard ×0.85 ฯลฯ) · Easy ปิดได้

config แยกทั้งไฟล์ ("สร้างเวอร์ชันความยาก") ยังอยู่ — ใช้เฉพาะเมื่อระดับนั้นต่างจนเป็นคนละไฟต์

**ข. DifficultyProfile — กติกากลางของระดับ (ทุกแมพใช้ร่วม)**

ระดับความยากเป็น **กติกาของเกม** ไม่ใช่เนื้อหาของแมพ (Rabbit and Steel ใช้ค่าเดียวกันทุกด่าน)
จึงแยกเป็น asset ต่อระดับ 5 ไฟล์ แทนการใส่ตัวคูณซ้ำในทุก MapData

| หมวด | ช่อง | อยู่ที่ไหนตอนนี้ |
|---|---|---|
| บอส/มินิบอส | hpMult · damageMult · warningMult · intervalMult · enrageMult | ไม่มี (F3) |
| จำนวนคน | hpByPlayers = [0.9, 1.7, 2.4, 3.0] (ของ R&S เป็นจุดเริ่ม) | ไม่มี (F6) |
| ศัตรูทั่วไป | ตัวคูณทับ `enemyScaling` ของแมพ · spawn rate · เพดานจำนวน | MapData.tier.enemyScaling |
| ผู้เล่น | HP เริ่ม · ฟื้นหลังบอส · เวลาชุบ (revive) | ซีน/ตัวละคร |
| รางวัล | exp × · ทอง/talent × | ไม่มี |

**ค. MapData.tier เหลือแค่เนื้อหาของแมพ** — ตาราง (ใครออกนาทีไหน · แบบ Brotato: ระดับสูงมีมินิบอสถี่ขึ้น/สองตัวพร้อมกัน) ·
wave · เพลง · (ถ้าจำเป็น) config ทับทั้งไฟล์

```
DifficultyProfile_Hard  (กติกา)      MapData_Arena01 › tier Hard  (เนื้อหา)
 hp ×1.1  enrage ×0.85  exp ×0.9      schedule: มินิบอส 2,4,6,9,11,14
 hpByPlayers [0.9,1.7,2.4,3.0]        waves / เพลง
          │                                     │
          └──────────► BossManager / BossController / WaveManager ◄─┘
                        คลิปใน timeline: ออกถ้า minTier ≤ Hard ≤ maxTier
```

### ต้องตัดสินใจ (แทน §9)
1. รับโมเดล **ก + ข + ค** ไหม (แนะนำ) — หรือคงแบบก๊อป config ต่อระดับ
2. จำนวนระดับ: คงไว้ 5 (Easy/Normal/Hard/Savage/Epic) หรือ 4 แบบ R&S
3. ฝั่งผู้เล่น (HP/ชุบ/รางวัล) ทำในรอบนี้ หรือทำแค่บอส + ศัตรูก่อน
4. HP ตามจำนวนคน: ใช้ตาราง R&S [0.9, 1.7, 2.4, 3.0] เป็นจุดเริ่ม

Sources: Rabbit and Steel — [Steam: Impact of Difficulty Setting](https://steamcommunity.com/app/2132850/discussions/0/4335357742819071258/),
[Steam guide: numbers](https://steamcommunity.com/sharedfiles/filedetails/?id=3247112738), [R&S wiki](https://rabbitandsteel.fandom.com/wiki/Map_%26_Difficulty),
[Steam: transition normal→hard](https://steamcommunity.com/app/2132850/discussions/0/762932162500642789/) ·
[Brotato Danger Levels](https://brotato.wiki.spellsandguns.com/Danger_Levels) · [Hades Pact of Punishment](https://hades.fandom.com/wiki/Pact_of_Punishment) ·
[Vampire Survivors Stages](https://vampire-survivors.fandom.com/wiki/Stages) · [FFXIV Raids](https://ffxiv.consolegameswiki.com/wiki/Raids)
