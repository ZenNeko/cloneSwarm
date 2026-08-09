# BossTether — ผลตรวจโค้ด

> Claude · 2026-08-09 · เขียนไว้ให้เซสชันถัดไปอ่านต่อ ยังไม่ได้แก้อะไรเลยสักข้อ
> ฐาน: `745befe5` (PR [#12](https://github.com/ZenNeko/cloneSwarm/pull/12)) · ทุกข้อยืนยันจากไฟล์จริงแล้ว

## ไฟล์ที่เกี่ยว

| ไฟล์ | บทบาท |
|---|---|
| `Assets/Script/BossTether.cs` (332 บรรทัด) | ตัวกลไก — NetworkBehaviour บน prefab ที่ถูก spawn |
| `Assets/Script/Data/TetherAction.cs` (77) | `BossAction` ที่ spawn prefab แล้วสั่ง `Activate` |
| `Assets/Prefab/Enemy/Boss/BossTether.prefab` | ค่า default ที่ **client** มองเห็น |
| `Assets/ScriptableObjects/BossAction/TetherAction.asset` | ค่าที่ **server** ใช้จริง |

**กลไก** — บอสผูกผู้เล่น 2 คนด้วยสาย ต้องวิ่งห่างกันให้ถึง `requiredDistance` ภายในเวลาที่กำหนด
ไม่ทันกินดาเมจทั้งคู่ · เล่นคนเดียวจะผูกกับเสา anchor แทน (`ActivateSolo`)

---

## ปัญหา 1 · ค่าที่ action ตั้ง ไม่เคยไปถึง client  ← แก้ก่อน

`TetherAction.cs:54–56` เซ็ตค่าลง instance ฝั่ง server ก่อน `Spawn()`

```csharp
tether.requiredDistance = tetherDistance;
tether.duration         = tetherDuration;
tether.failDamage       = tetherFailDamage;
```

ทั้งสามเป็น **public field ธรรมดา ไม่ใช่ `NetworkVariable`** → client ได้ค่าจาก prefab เท่านั้น

| | prefab (client เห็น) | asset (server ใช้) |
|---|---|---|
| `duration` | **6** | **8** |
| `requiredDistance` | 8 | 20 |
| `failDamage` | 40 | 50 |

`BossTether.cs:217` ฝั่ง client คำนวณเวลาที่เหลือเอง:

```csharp
float remaining = duration - clientTimer;   // ใช้ 6 ไม่ใช่ 8
bool  urgent    = remaining < 2f;
```

**อาการ** — สายเปลี่ยนเป็นแดงตั้งแต่วินาทีที่ 4 แล้ว**แดงค้างจนจบ** ทั้งที่กลไกจริงยาว 8 วินาที
ผู้เล่นได้สัญญาณตกใจเร็วไป 2 วิ แล้วสัญญาณนั้นก็ไร้ความหมายตลอดครึ่งหลังของกลไก

เป็นบั๊กตระกูล **"ผิดเฉพาะบนจอ client"** ที่ compile จับไม่ได้ — เล่นเป็น host อย่างเดียวไม่มีทางเจอ

**วิธีแก้ที่เสนอ** — ส่ง `duration` ไปกับ ClientRpc ที่มีอยู่แล้ว ไม่ต้องเพิ่ม `NetworkVariable`

```csharp
SetPlayersClientRpc(playerA, playerB, duration);
SetSoloPlayerClientRpc(playerA, duration);
```

`requiredDistance` กับ `failDamage` client ไม่ได้ใช้เลย ปล่อยไว้ได้
แต่ควรคอมเมนต์กำกับว่า **server-only** กันคนถัดไปพลาดซ้ำ

---

## ปัญหา 2 · ไม่กรองผู้เล่นที่ตายแล้ว

`TetherAction.cs:31` เอา `ConnectedClientsList` มาทั้งก้อนโดยไม่เช็ค `isDead`
ขณะที่ **พี่น้องของมันอย่าง `ColorMatchAoEAction` กรอง** — `grep isDead` ได้ **1 ต่อ 0**

- **co-op** — สุ่มติดศพ → ศพไม่วิ่ง → tether ไม่มีทางแตก → คนเป็นกินดาเมจจากกลไกที่แก้ไม่ได้
- **solo** — `clients.Count == 1` แต่คนนั้นตายอยู่ → เสาขึ้นมาให้ศพ
- สาขา co-op ยัง**ไม่เช็ค `PlayerObject != null`** ด้วย (สาขา solo เช็คที่บรรทัด 35)

ก๊อปเงื่อนไขจาก `ColorMatchAoEAction` มาใช้ได้ตรงๆ

---

## เรื่องรอง

| หัวข้อ | รายละเอียด |
|---|---|
| **Material รั่วทุกครั้งที่ tether เกิด** | `SetupLineRenderer:244` และ `SetupPillarVisual:266` `new Material(shader)` แล้วไม่เคย `Destroy` · `OnNetworkDespawn` ลบแค่ `pillarVisual` ซึ่งเป็นลูกที่โดนลบตามพ่ออยู่แล้ว บอสยิง tether หลายรอบต่อเกม สะสมเรื่อยๆ |
| **อีโมจิในข้อความ HUD** | `🔗` `✅` `💥` ใน `ShowAnnouncement` — Sarabun SDF น่าจะไม่มี glyph จะได้กล่องสี่เหลี่ยม · **ยังไม่ยืนยัน ต้องดูด้วยตาตอนรัน** · `docs/STATUS.md` เคยบันทึกปัญหา "ข้อความไทยใน TMP" มาแล้ว |
| **ไม่มีช่วงผ่อนผัน** | ถ้าคู่ที่สุ่มได้บังเอิญห่างกันเกิน `requiredDistance` อยู่แล้ว `Update():167` ประกาศ BROKEN ตั้งแต่เฟรมแรก ได้ฟรี · ยิ่ง `tetherDistance = 20` ในสนามกว้างยิ่งเกิดง่าย |
| **`SkipTether()` เป็น API ตาย** | ไม่มีใครเรียก · `TetherAction` `yield break` ไปก่อนตั้งแต่ `clients.Count == 0` ที่บรรทัด 32 |

---

## ตรวจแล้วว่า **ไม่ใช่** ปัญหา — อย่าเสียเวลาซ้ำ

`FindPlayerTransforms()` (`:286`) และ `GetPlayerTransform()` (`:298`) ใช้
`NetworkManager.Singleton.ConnectedClients` ซึ่งดูเหมือนจะเป็น server-only

เปิด NGO source ในโปรเจกต์ดูแล้ว — **ไม่ใช่**
`com.unity.netcode.gameobjects@d43d28498f17/Runtime/Messaging/Messages/ClientConnectedMessage.cs:41`
เรียก `AddClient` ฝั่ง client ด้วย dictionary จึงถูกเติมบน client เหมือนกัน ใช้ได้ปกติ

---

## ข้อเสนอลำดับงาน

1. **ปัญหา 1** — ส่ง `duration` ผ่าน ClientRpc (กระทบการเล่นจริง เห็นผลทันทีบนจอ client)
2. **ปัญหา 2** — กรอง `isDead` + เช็ค `PlayerObject != null` ในสาขา co-op
3. material leak — อยู่ไฟล์เดียวกัน เก็บไปพร้อมกันได้

ทั้งหมดเป็น**งานโค้ดล้วน ไม่มีงาน Editor** · ไม่แตะ prefab หรือ asset

## ต้องเทสต์ด้วยมือ (compile จับไม่ได้สักข้อ)

| ทำอะไร | ต้องเห็นอะไร |
|---|---|
| co-op 2 คน โดน tether · **ดูจากจอ client** | สายเปลี่ยนเป็นแดงที่วินาทีที่ 6 ไม่ใช่ 4 และไม่แดงค้าง |
| ให้คนหนึ่งตายก่อน แล้วรอ tether รอบถัดไป | **ต้องไม่สุ่มไปโดนศพ** |
| solo ตายแล้วรอ tether | ไม่มีเสาขึ้นมา |
| ยิง tether ซ้ำหลายรอบในเกมเดียว | Profiler → material count ไม่ควรไต่ขึ้นเรื่อยๆ |
| อ่านข้อความ TETHER บนจอ | ตัวอักษรครบ ไม่มีกล่องสี่เหลี่ยมแทนอีโมจิ |
