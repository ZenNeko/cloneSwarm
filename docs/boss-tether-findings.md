# BossTether — ผลตรวจโค้ด + สิ่งที่แก้ไปแล้ว

> Claude · ตรวจ 2026-08-09 · **แก้ 2026-08-10**
> ฐานตอนตรวจ: `745befe5` (PR [#12](https://github.com/ZenNeko/cloneSwarm/pull/12))
> **สถานะ: แก้ครบทุกข้อที่เป็นงานโค้ดแล้ว · compile ผ่าน (`scripts/unity-check.ps1` exit 0)**
> **ยังไม่ได้เทสต์ในเกมจริงสักข้อ** — ตารางท้ายไฟล์คือสิ่งที่ต้องดูด้วยตา
>
> **หลังจากเอกสารนี้ BossTether ถูกขยายเป็น 4 โหมด** (Far / Close / Leash / Transferable)
> โครงสร้างและเหตุผลอยู่ใน [`adr-002-tether-modes.md`](adr-002-tether-modes.md) — อ่านคู่กัน
> โค้ดในเอกสารนี้บางส่วนเปลี่ยนชื่อไปแล้ว (`soloMode` → `pillarAnchor` ·
> `SetPlayersClientRpc`/`SetSoloPlayerClientRpc` → `InitTetherClientRpc` ตัวเดียว)

## ไฟล์ที่เกี่ยว

| ไฟล์ | บทบาท |
|---|---|
| `Assets/Script/BossTether.cs` | ตัวกลไก — NetworkBehaviour บน prefab ที่ถูก spawn |
| `Assets/Script/Data/TetherAction.cs` | `BossAction` ที่ spawn prefab แล้วสั่ง `Activate` |
| `Assets/Prefab/Enemy/Boss/BossTether.prefab` | ค่า default ที่ **client** มองเห็น |
| `Assets/ScriptableObjects/BossAction/TetherAction.asset` | ค่าที่ **server** ใช้จริง |

**กลไก** — บอสผูกผู้เล่น 2 คนด้วยสาย ต้องวิ่งห่างกันให้ถึง `requiredDistance` ภายในเวลาที่กำหนด
ไม่ทันกินดาเมจทั้งคู่ · เล่นคนเดียวจะผูกกับเสา anchor แทน (`ActivateSolo`)

ไม่แตะ prefab หรือ asset เลย · `minSeparationGain` เป็น field ใหม่ที่ใช้ค่า default 2 m

---

## ✅ ปัญหา 1 · ค่าที่ action ตั้ง ไม่เคยไปถึง client — **แก้แล้ว**

`TetherAction` เซ็ต `requiredDistance` / `duration` / `failDamage` ลง instance ฝั่ง server ก่อน `Spawn()`
แต่ทั้งสามเป็น **public field ธรรมดา ไม่ใช่ `NetworkVariable`** → client ได้ค่าจาก prefab เท่านั้น

| | prefab (client เห็น) | asset (server ใช้) |
|---|---|---|
| `duration` | **6** | **8** |
| `requiredDistance` | 8 | 20 |
| `failDamage` | 40 | 50 |

ฝั่ง client คำนวณเวลาที่เหลือเองจาก `duration` → สายเปลี่ยนเป็นแดงตั้งแต่วินาทีที่ 4
แล้ว**แดงค้างจนจบ** ทั้งที่กลไกจริงยาว 8 วินาที

**แก้ด้วย** — ส่ง `duration` ไปกับ ClientRpc ที่มีอยู่แล้ว ไม่เพิ่ม `NetworkVariable`

```csharp
SetPlayersClientRpc(playerA, playerB, duration);
SetSoloPlayerClientRpc(playerA, duration);
```

`requiredDistance` / `failDamage` / `minSeparationGain` client ไม่ได้ใช้ ปล่อยไว้เหมือนเดิม
แต่ใส่คอมเมนต์ **server-only** กำกับไว้บนตัว field แล้ว กันคนถัดไปพลาดซ้ำ

## ✅ ปัญหา 2 · ไม่กรองผู้เล่นที่ตายแล้ว — **แก้แล้ว**

`TetherAction` เอา `ConnectedClientsList` มาทั้งก้อนโดยไม่เช็ค `isDead`
(สาขา co-op ยังไม่เช็ค `PlayerObject != null` ด้วย)

**แก้ด้วย** — สร้าง list `alive` ก่อน โดยใช้เงื่อนไขเดียวกับ `ColorMatchAoEAction`
แล้วให้ทุกสาขาหลังจากนั้นตัดสินจาก `alive.Count` แทน `clients.Count`

```csharp
if (client.PlayerObject == null) continue;
var pm = client.PlayerObject.GetComponent<playermove>();
if (pm == null || pm.isDead.Value) continue;
```

ผลพลอยได้ — โหมด solo ที่คนเดียวนั้นตายอยู่ ตอนนี้ `alive.Count == 0` → `yield break` ไม่มีเสาขึ้น

## ✅ Material รั่วทุกครั้งที่ tether เกิด — **แก้แล้ว**

`SetupLineRenderer` / `SetupPillarVisual` `new Material(shader)` แล้วไม่เคย `Destroy`
`OnNetworkDespawn` ลบแค่ `pillarVisual` ซึ่งเป็นลูกที่โดนลบตามพ่ออยู่แล้ว

**แก้ด้วย** — เก็บ ref ไว้ใน `lineMaterial` / `pillarMaterial` แล้วลบใน `OnDestroy`
(ย้ายจาก `OnNetworkDespawn` เพราะ `OnDestroy` รันแน่ทั้งตอน despawn ปกติและตอนปิดฉาก)

## ✅ ไม่มีช่วงผ่อนผัน (แตกฟรี) — **แก้แล้ว**

ถ้าคู่ที่สุ่มได้บังเอิญห่างกันเกิน `requiredDistance` อยู่แล้ว `Update()` ประกาศ BROKEN ตั้งแต่เฟรมแรก
ยิ่ง `tetherDistance = 20` ในสนามกว้างยิ่งเกิดง่าย

**แก้ด้วย** — `ResolveBreakDistance()` ตอน `Activate` วัดระยะเริ่มต้นก่อน แล้วเก็บเกณฑ์จริงไว้ใน `breakDistance`

```csharp
breakDistance = requiredDistance;
if (startDistance >= requiredDistance)
    breakDistance = startDistance + minSeparationGain;   // ต้องวิ่งห่าง "เพิ่ม" จริงๆ
```

เคสปกติ (เริ่มใกล้กัน) `breakDistance == requiredDistance` เท่าเดิม — พฤติกรรมไม่เปลี่ยน
`minSeparationGain` ปรับได้ใน Inspector ของ prefab · default 2 m

## ✅ `SkipTether()` เป็น API ตาย — **ลบแล้ว**

ไม่มีใครเรียก (grep ทั้ง repo ได้แต่ในเอกสารนี้) · `TetherAction` `yield break` ไปก่อนตั้งแต่ยังไม่ spawn
ตรรกะซ้ำกับ `DespawnDelayed` อยู่แล้ว

---

## ยังไม่ได้แก้ — ตั้งใจ

**อีโมจิในข้อความ HUD** (`🔗` `✅` `💥`) — Sarabun SDF อาจไม่มี glyph จะได้กล่องสี่เหลี่ยม
**ยังไม่ยืนยัน ต้องดูด้วยตาตอนรัน** · `docs/STATUS.md` เคยบันทึกปัญหา "ข้อความไทยใน TMP" มาแล้ว

ไม่แก้เฉพาะ BossTether เพราะ**ทั้งโปรเจกต์ใช้แบบเดียวกันหมด** — `FloorHazard`, `TelegraphZone:156/446`,
`BossHUDUI:154` ก็มีอีโมจิใน `ShowAnnouncement` เหมือนกัน ถ้าจะแก้ต้องแก้พร้อมกันทั้งชุด
เป็นงานแยกหลังยืนยันด้วยตาแล้วว่าเป็นปัญหาจริง

---

## ตรวจแล้วว่า **ไม่ใช่** ปัญหา — อย่าเสียเวลาซ้ำ

`FindPlayerTransforms()` และ `GetPlayerTransform()` ใช้
`NetworkManager.Singleton.ConnectedClients` ซึ่งดูเหมือนจะเป็น server-only

เปิด NGO source ในโปรเจกต์ดูแล้ว — **ไม่ใช่**
`com.unity.netcode.gameobjects@d43d28498f17/Runtime/Messaging/Messages/ClientConnectedMessage.cs:41`
เรียก `AddClient` ฝั่ง client ด้วย dictionary จึงถูกเติมบน client เหมือนกัน ใช้ได้ปกติ

---

## ต้องเทสต์ด้วยมือ — **ยังไม่ได้ทำ** (compile จับไม่ได้สักข้อ)

| ทำอะไร | ต้องเห็นอะไร |
|---|---|
| co-op 2 คน โดน tether · **ดูจากจอ client ไม่ใช่ host** | สายเปลี่ยนเป็นแดงที่วินาทีที่ 6 ไม่ใช่ 4 และไม่แดงค้าง |
| ให้คนหนึ่งตายก่อน แล้วรอ tether รอบถัดไป | **ต้องไม่สุ่มไปโดนศพ** |
| solo ตายแล้วรอ tether | ไม่มีเสาขึ้นมา |
| ยืนห่างกันเกิน 20 m อยู่แล้วตอน tether ลง | **ต้องไม่แตกฟรีทันที** — ต้องวิ่งห่างเพิ่มอีก ~2 m |
| ยืนใกล้กันปกติแล้ววิ่งแยก | แตกที่ 20 m เท่าเดิม (เกณฑ์ไม่ควรเปลี่ยนในเคสปกติ) |
| ยิง tether ซ้ำหลายรอบในเกมเดียว | Profiler → material count ไม่ควรไต่ขึ้นเรื่อยๆ |
| อ่านข้อความ TETHER บนจอ | ตัวอักษรครบ ไม่มีกล่องสี่เหลี่ยมแทนอีโมจิ (ข้อนี้ยังไม่แก้) |
