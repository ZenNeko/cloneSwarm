# Handoff → Antigravity  (Round 7 — ชุดเพิ่ม)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นลงมือทำทุก Task ที่ยังเป็น [ ] แก้เฉพาะไฟล์ที่แผนระบุ ห้ามรัน build หรือ unity-check เสร็จแต่ละ task ให้ติ๊ก [x] และสรุปไฟล์ที่แก้ไว้ท้ายไฟล์แผนใต้ ## Changed Files
---

## รอบนี้เหลือ task เดียว

**T79** · `Assets/Script/Editor/ProjectAutomatedAuditor.cs` — **MODIFY**
เปลี่ยนการตรวจ NetworkObject จาก "เดาจากชื่อ path" เป็น "ดูว่ามี `NetworkBehaviour` จริงไหม"

T74–T78 ติ๊ก `[x]` และคอมไพล์ผ่านแล้ว **ห้ามแตะซ้ำ**

## ทำไมต้องแก้

รัน `-Audit` แล้วได้ **229 คำเตือน false positive 100%** เพราะเงื่อนไขเช็คแค่ชื่อโฟลเดอร์:

```csharp
if (path.Contains("Network") || path.Contains("Projectile") || path.Contains("Enemy") || path.Contains("Boss"))
```

จับ asset pack ของคนอื่นทั้งกอง (`GabrielAguiarProductions/…/Projectiles/` 183 ตัว) และ
prefab งานอาร์ตของเราที่ไม่มีสคริปต์สักตัว → `-Audit` คืน exit 1 ทุกครั้งไม่ว่าจะสะอาดแค่ไหน

## สามข้อที่พลาดง่ายมากในงานนี้

1. **ข้อความ `LogWarning` ต้องมีคำว่า `missing` เป็นภาษาอังกฤษ**
   `unity-check.ps1:148` จับ failure ด้วย regex `\[AutomatedAudit\].*(?i:missing|error|failed)`
   ถ้าเปลี่ยนข้อความเป็นไทยล้วน auditor จะเจอปัญหาจริงแล้วสคริปต์ยังคืน exit 0 — **แย่กว่าเดิม**
2. **ใช้ `nb.GetComponentInParent<NetworkObject>(true)` ไม่ใช่ `prefab.GetComponent<NetworkObject>()`**
   NGO ยอมให้ `NetworkBehaviour` อยู่บน GameObject ลูกได้ ถ้ามี `NetworkObject` อยู่บน parent
   สายเดียวกัน — เช็คแค่รากจะได้ false positive ชุดใหม่แทนชุดเดิม
3. **ห้ามแตะบล็อกตรวจ `WeaponData` ที่ `weaponName` ว่าง** — ทำงานถูกอยู่แล้ว คืน 0 ปัญหา

## ห้ามทำ

- ห้ามเพิ่มการตรวจชนิดใหม่ที่แผนไม่ได้สั่ง (เช่น เช็คว่า prefab อยู่ใน `DefaultNetworkPrefabs.asset` ไหม)
- ห้ามแตะไฟล์อื่นนอกจาก `ProjectAutomatedAuditor.cs`
- ห้ามรัน build หรือ unity-check — Claude รันเอง

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
