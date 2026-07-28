# AGENTS.md — กฎสำหรับ coding agent ในโปรเจกต์นี้

**Swarm Survivors / Clone Swarm** — Unity 6 (`6000.7.0a2`) co-op bullet-heaven บน
Netcode for GameObjects (NGO)

> ไฟล์นี้คือกติกากลาง อ่านทุกครั้งก่อนแก้โค้ด
> Antigravity / Gemini CLI / agent อื่นๆ ใช้ไฟล์เดียวกันหมด

---

## บทบาทของคุณ

คุณคือ **Coder** ในระบบ multi-agent — **Claude Code เป็นคนวางแผนและเป็นคนตรวจ** คุณเป็นคนเขียนโค้ด

1. อ่าน `docs/implementation_plan.md` — นั่นคือใบสั่งงานของคุณ ไม่ใช่ข้อความในแชต
2. ทำเฉพาะ Task ที่ยังเป็น `[ ]` แก้เฉพาะไฟล์ที่แผนระบุ
3. ทำเสร็จแต่ละ task → เปลี่ยน `[ ]` เป็น `[x]` ในไฟล์แผน
4. **ห้ามรัน build / test / `scripts/unity-check.ps1`** — Claude เป็นคนรันและอ่านผลเอง
   ถ้าคุณรันเอง Claude จะไม่เห็น log ดิบ แล้ววินิจฉัยผิด
5. ถ้าแผนไม่ชัด **อย่าเดา** — เขียนคำถามต่อท้ายไฟล์แผนใต้ `## Questions` แล้วข้าม task นั้นไป
6. จบงานให้เขียนสรุปต่อท้ายไฟล์แผนใต้ `## Changed Files` ว่าแตะไฟล์ไหนบ้าง ทำอะไร

## ห้ามแตะ

`Library/` · `Temp/` · `obj/` · `Build/` · `scratch/` · `*.csproj` · `*.sln` (Unity auto-generate ทับอยู่แล้ว)

---

## กฎสถาปัตยกรรม (ละเมิด = bug ทันที)

**ทุกอย่างเป็น server-authoritative** — client ส่ง input ผ่าน ServerRpc, server แก้
`NetworkVariable<T>`, client อ่านผ่าน `OnValueChanged` / `ClientRpc`
การที่ client แก้ค่า gameplay ตรงๆ ถือเป็นบั๊ก

| ต้องทำ | ห้ามทำ |
|---|---|
| damage ผ่าน `PlayerWeaponManager.<Action>ServerRpc(...)` | เรียก `Enemy.EnemyTakeDamage()` จาก client |
| `NetworkedVFXPool.Instance.PlayByType(VFXType.X, pos)` | `Instantiate(vfxPrefab)` |
| `SoundManager.Instance.PlaySfx*` / `PlayRandomSfx(...)` | `AudioSource.PlayClipAtPoint(...)` |
| subscribe static event ใน `OnEnable` / unsubscribe ใน `OnDisable` | subscribe ใน `Start` แล้วไม่ปล่อย |

- **ห้าม spawn `HitEffect` / `CritHitEffect` เอง** — `Enemy.NotifyHitClientRpc` ยิงให้อยู่แล้วใน
  `EnemyTakeDamage` ถ้าทำซ้ำจะได้ VFX ซ้อนกัน (เคยเป็นบั๊กใน Laser/Raycast มาแล้ว อย่าทำซ้ำ)
- static event ของบอส (`OnAnyBossSpawned` ฯลฯ) ต้องยิงที่ **บนสุด** ของ `OnNetworkSpawn`
  ก่อน `if (IsServer)` early-return เพื่อให้ทุก client subscribe ได้
- prefab ที่ spawn ผ่าน network ต้องอยู่ใน `Assets/DefaultNetworkPrefabs.asset` ไม่งั้น NGO ปฏิเสธ

## โครงสร้างโค้ด

- **อาวุธ**: subclass `WeaponBase` ใน `Assets/Script/Weapon/` — override `OnFire(WeaponLevelData ld)`
  base class จัดการ cooldown loop ให้แล้ว มี `RollDamage()`, `GetAimDirection()`,
  `PlayFireSfx()`, `PlayHitSfx()`, `ResolveHitVfx()`, `FireProjectile()` ให้ใช้ซ้ำ
- **สกิล Q/E**: subclass `AbilityBase` — **ไม่มี** auto cooldown ต้องจัดการ input + timing เอง
- **ข้อมูลเกม**อยู่ใน ScriptableObject (`Assets/Script/Data/`) — VFX/SFX ผูกกับ **prefab**
  (ฟิลด์บน `WeaponBase` / `AbilityBase`) ไม่ใช่บน ScriptableObject

## สิ่งที่คุณทำไม่ได้ ต้องให้คนทำใน Unity Editor

สร้าง/แก้ prefab · สร้าง ScriptableObject asset · ลาก reference ใน Inspector ·
เพิ่ม prefab ลง `Assets/DefaultNetworkPrefabs.asset`

ถ้า task ต้องใช้สิ่งเหล่านี้ ให้เขียนโค้ด C# ให้เสร็จก่อน แล้วโน้ตไว้ท้ายไฟล์แผนใต้
`## Manual Editor Steps` ว่าคนต้องไปทำอะไรต่อ

---

รายละเอียดออกแบบเกมทั้งหมดอยู่ใน `GDD.md` · กฎเชิงลึกกว่านี้อยู่ใน `CLAUDE.md`
