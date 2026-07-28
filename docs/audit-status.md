# Audit Backlog — สถานะจริง (verify แล้ว)

ตรวจโดย Gemini (read-only) แล้ว **verify ด้วย Grep ทุกข้อ** เมื่อ 2026-07-28
แผน 21 ข้อเดิมมาจาก Antigravity ซึ่งบางข้อทำไปแล้วใน commit `262bc9ec`

---

## Phase 1 — Security / Authority

| # | ไฟล์ | สถานะ | หลักฐาน |
|---|---|---|---|
| 1.1 HP spoofing | `playermove.cs` | ⚠️ **มีแค่ band-aid** | [playermove.cs:325](../Assets/Script/playermove.cs:325) ยังเป็น `void SyncBaseStatsServerRpc(float hp)` มี `Mathf.Clamp(hp, 10f, 1000f)` ที่บรรทัด 328 |
| 1.2 EXP multiplier injection | `SharedExperienceManager.cs` | ⚠️ **มีแค่ band-aid** | [SharedExperienceManager.cs:129](../Assets/Script/SharedExperienceManager.cs:129) ยังรับ `float newMultiplier` มี `Mathf.Clamp(1f, 5f)` |
| 1.3 Card validation | `UpgradeManager.cs` | ❌ ยังค้าง | `ApplyCard` (563) / `ApplyOrbCard` (572) รันบน owner client เรียก `ApplyCardInternal` ทันที ไม่มี `RequestPickCardServerRpc` |
| 1.4 Networked stat sync | `PlayerStatManager.cs` | ❌ ยังค้าง | [PlayerStatManager.cs:14](../Assets/Script/PlayerStatManager.cs:14) `private Dictionary<StatType, int> statLevels = new();` |

> **จุดที่ต้องเข้าใจ**: 1.1 กับ 1.2 commit `262bc9ec` ใส่ `Mathf.Clamp` ไว้แล้ว ซึ่ง **ลดความเสียหาย
> แต่ไม่ได้ปิดช่องโหว่** — client ยังเลือกส่ง HP = 1000 หรือ EXP multiplier = 5.0x ได้ตลอดเวลา
> ต่างกับการให้ server อ่านค่าจาก `CharacterData` เอง ซึ่ง client กำหนดอะไรไม่ได้เลย

### Phase 1 ไม่ใช่ 4 งานอิสระ — เป็นห่วงโซ่

```
1.4 stat sync (รากฐาน)
 ├── 1.2 EXP multiplier  ← server ต้องรู้ stat level จริงถึงจะคำนวณเองได้
 └── 1.3 card validation ← server ต้องรู้ weapon+stat ถึงจะ gen/ตรวจ card pool ได้
1.1 HP spoofing (อิสระ ทำได้เลย)
```

`PlayerStatManager` เก็บ `statLevels`/`statTotals` เป็น Dictionary ใน client ล้วน · `ApplyStat` รันบน client
เรียก `pm.GainMaxHealth()` ตรงๆ · `UpgradeManager.BuildLevelUpPool()` gen การ์ดจาก state ฝั่ง client ทั้งหมด
→ **server ไม่รู้อะไรเลยเกี่ยวกับ progression** จึงแก้ 1.2/1.3 ไม่ได้ถ้าไม่แก้ 1.4 ก่อน

### ✅ ตัดสินใจแล้ว 2026-07-28

- **ทำ 1.1 ตอนนี้** — อิสระ ปลอดภัย ใช้ `PlayerVisual._charIndex` ที่ server validate อยู่แล้ว
- **เลื่อน 1.2 / 1.3 / 1.4** — เป็นการรื้อสถาปัตยกรรม ไม่ใช่งานพิมพ์ ให้ไปทำ Phase 3 (งานกล เสี่ยงต่ำ) ก่อน
- เมื่อกลับมาทำ 1.4 → Claude เสนอ 2 ทางเลือก (NetworkList เฉพาะ stat / server ถือ progression ทั้งก้อน)
  แล้วค่อยออกแบบ 1.3 ตามผลของ 1.4

---

## Phase 2 — Memory / Threading

| # | ไฟล์ | สถานะ | หลักฐาน |
|---|---|---|---|
| 2.1 Burn DOT race | `Enemy.cs` | ✅ แก้แล้ว | [Enemy.cs:406](../Assets/Script/Enemy.cs:406) `if (!NetworkObject.IsSpawned \|\| netHealth.Value <= 0f) break;` |
| 2.2 dead ExperienceManager | `Enemy.cs` / `ExperienceManager.cs` | ⚠️ ครึ่งเดียว | fallback ชี้ไป `SharedExperienceManager` แล้วที่ [Enemy.cs:521](../Assets/Script/Enemy.cs:521) แต่ [ExperienceManager.cs](../Assets/Script/ExperienceManager.cs) ยังไม่ถูกลบ |
| 2.3 static event leak | `SharedExperienceManager.cs` | ✅ แก้แล้ว | [:83-95](../Assets/Script/SharedExperienceManager.cs:83) ใช้ method reference + unsubscribe ครบ |
| 2.4 temp stat bonuses ไม่ sync | `playermove.cs` | ❌ **ยังค้าง** | [:295](../Assets/Script/playermove.cs:295) `tempMoveSpeedBonus` และ [:298](../Assets/Script/playermove.cs:298) `tempHealthRegenBonus` ยังเป็น `[HideInInspector] public float` ธรรมดา · ถูกใช้ในการคำนวณความเร็วที่ [:155](../Assets/Script/playermove.cs:155) แต่ set โดย weapon ฝั่ง owner → **server อาจไม่เคยเห็นค่า buff เลย** |
| 2.5 Material leak | `TelegraphZone.cs` | ✅ **แก้แล้ว** (ลูป 1) | เปลี่ยนเป็น `sharedMaterial` + `MaterialPropertyBlock` ครบทั้ง 6 จุด |
| 2.6 crit/damage คำนวณฝั่ง client | `WeaponBase.cs` | ❌ **ยังค้าง — ช่องโหว่ใหญ่สุดที่เหลือ** | `damage` และ `isCrit` ถูกคำนวณบน client แล้วส่งเป็น **parameter** เข้า `FireProjectileServerRpc` ([:174](../Assets/Script/Weapon/WeaponBase.cs:174)), `FireMeleeServerRpc` ([:181](../Assets/Script/Weapon/WeaponBase.cs:181)), `FireArcMeleeServerRpc` ([:186](../Assets/Script/Weapon/WeaponBase.cs:186)), `FireLineAoE` ([:189](../Assets/Script/Weapon/WeaponBase.cs:189)) → **client ส่งค่า damage เท่าไหร่ก็ได้** |

> **2.6 ร้ายแรงกว่า 1.1 ที่เพิ่งแก้ไป** — 1.1 ทำได้แค่ปลอม HP ของตัวเอง แต่ 2.6 ให้ client
> กำหนด damage ที่ส่งเข้า server ได้อิสระ ไม่มี clamp ไม่มีการตรวจสอบใดๆ
> วิธีแก้ที่ถูกคือย้ายการคำนวณ damage + crit roll ไปฝั่ง server ทั้งหมด แล้วให้ RPC ส่งแค่
> "ยิงอาวุธอะไร ทิศไหน" — ซึ่งต้องให้ server รู้ weapon level + stat ของผู้เล่น = **ผูกกับ 1.4 เหมือนกัน**

---

## Phase 3 — Performance / NGO

| # | ไฟล์ | สถานะ | หลักฐาน + ผลกระทบจริง |
|---|---|---|---|
| 3.1 List → HashSet | `Enemy.cs` | ✅ **แก้แล้ว** (ลูป 3) | [:20](../Assets/Script/Enemy.cs:20) เป็น `HashSet<Enemy>` · แก้ `FlowFieldPathfinder.cs:233` เป็น `foreach` ด้วย |
| 3.2 InvokeRepeating | `EnemySpawner.cs` | ✅ **แก้แล้ว** (ลูป 3) | [:67](../Assets/Script/EnemySpawner.cs:67) `IEnumerator SpawnLoop(float rate)` — timing เดิม (0.5s แรก แล้วทุก rate) + cache `WaitForSeconds` |
| 3.3 CheckLoseCondition spam | `GameTimeline.cs` | ✅ **แก้แล้ว** (ลูป 3) | [:117](../Assets/Script/GameTimeline.cs:117) timer accumulator แทน `% 2 == 0` |
| 3.4 Hit RPC throttling | `Enemy.cs` | ❌ **ค้าง — ตั้งใจข้าม** | [:369](../Assets/Script/Enemy.cs:369) ยิง `NotifyHitClientRpc` ทุกครั้งที่โดนดาเมจ · เป็นงานออกแบบ ไม่ใช่งานกล (ดูหมายเหตุด้านล่าง) |
| 3.5 timeScale recovery | `SharedExperienceManager.cs` | ⚠️ ค้าง — คุ้มน้อยสุด | **server มี timer + ForceAutoPick อยู่แล้ว** ([:208-212](../Assets/Script/SharedExperienceManager.cs:208)) ขาดแค่ failsafe ฝั่ง client |
| 3.6 VFX pool orphaning | `NetworkedVFXPool.cs` | ✅ **แก้แล้ว** (ลูป 3) | `SetParent(transform, false)` ทั้ง 2 จุด |
| 3.7 ExpOrb double collect | `ExpOrb.cs` | ✅ แก้แล้ว | [:89-95](../Assets/Script/ExpOrb.cs:89) มี `isCollected` guard |
| **บั๊กนอก audit** — `MissingReferenceException` | `AbilityBase.cs` | ✅ **แก้แล้ว** (ลูป 3) | [:64](../Assets/Script/Weapon/AbilityBase.cs:64) `if (this == null) return;` ใน `OnActiveSceneChanged` — เจอตอนเทสต์เล่นจริง กระทบทุก ability |

### จัดลำดับใหม่ตามผลกระทบจริง (ไม่ใช่ตามเลขข้อ)

1. **3.4 สูงสุด** — bullet-heaven โดนตีหลายสิบครั้งต่อวินาที ยิง ClientRpc หาทุก client ทุกครั้ง คือต้นทุน network ที่ใหญ่ที่สุดในลิสต์นี้
2. **3.3** — เปลืองฟรีๆ แก้ง่าย
3. **3.1** — O(n) จริง แต่ต้องแก้ call site ด้วย
4. **3.6** → 5. **3.2** → 6. **3.5** (มี failsafe ฝั่ง server แล้ว คุ้มน้อยสุด)

### ข้อควรระวังก่อนลงมือ

- **3.1** เปลี่ยนเป็น `HashSet` จะพัง [`FlowFieldPathfinder.cs:233-235`](../Assets/Script/FlowFieldPathfinder.cs:233)
  ที่ใช้ `ActiveEnemies[i]` — HashSet ไม่มี indexer ต้องแก้เป็น `foreach` ด้วย
  (ตรวจแล้ว `i` ใช้แค่ดึง element ไม่ได้ใช้ทำอย่างอื่น → แปลงได้ปลอดภัย)
  จุดอื่น (`DevTools.cs:95`, `PlayerWeapon.cs:99`, `FunnelObject.cs:376`) ใช้ foreach/ctor อยู่แล้ว ไม่กระทบ
- **3.4 ไม่ใช่งานกล ห้ามโยนเข้าลูป** — `CLAUDE.md` ระบุว่า `HitEffect`/`CritHitEffect` ถูกยิงจาก
  `NotifyHitClientRpc` โดยอัตโนมัติ และห้ามให้ weapon spawn ซ้ำ การ throttle จึง**เปลี่ยน feedback ที่ผู้เล่นเห็น**
  ต้องตัดสินใจก่อนว่าจะ batch, จะทำ predictive visual ฝั่งคนยิง, หรือ throttle ต่อ enemy — เป็นงานออกแบบ

---

## Phase 4

ยังไม่ได้ตรวจ

---

## ลูปที่ควรรันตามลำดับ

1. **ลูป 1 (เสี่ยงต่ำ พิสูจน์ระบบ)** — `TelegraphZone` เปลี่ยน `r.material` → `MaterialPropertyBlock` + ลบ `ExperienceManager.cs` แล้วไล่แก้ reference
2. **ลูป 2** — ตรวจ Phase 3 ก่อนว่าเหลืออะไร แล้วค่อยแก้
3. **ลูป 3 (เสี่ยงสูง)** — Phase 1 ทั้ง 4 ข้อ แตะ RPC contract ต้องเทสต์ multiplayer จริงด้วย ParrelSync
4. **ลูป 4** — Phase 4 polish

---

## บทเรียนจากการใช้ Gemini ตรวจ

- **เลขบรรทัดเชื่อไม่ได้เสมอ** — รอบแรกคลาดเคลื่อน 1–20 บรรทัด รอบสองตรงเป๊ะ ไม่มีแบบแผน
  → ต้อง Grep verify ทุกครั้งก่อนเอาไปใช้วางแผน
- **ข้อสรุปเชิงตรรกะแม่น** — บอกถูกทุกข้อว่าอะไรแก้แล้ว/ยังค้าง รวมถึงจับได้ว่า clamp ≠ แก้จริง
- → ใช้ Gemini "หาว่าอยู่ตรงไหน + สรุปว่าเป็นยังไง" ได้ แต่ **อย่าใช้เลขบรรทัดมันไปเขียนแผนตรงๆ**
