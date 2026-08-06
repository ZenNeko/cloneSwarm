# Handoff → Antigravity  (Round 3)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นทำทุก Task ที่เป็น [ ] ตามลำดับ แก้เฉพาะไฟล์ที่แผนระบุ ห้ามแตะไฟล์ที่ Round 1-2 ทำไปแล้ว (LobbyState TabBar LobbyUI MapData RunSetup DifficultyTier PlayerSlotRegistry) ห้ามรัน build หรือ unity-check ห้ามสร้าง prefab หรือ asset ห้าม commit เสร็จแต่ละข้อให้ติ๊ก [x] และสรุปไฟล์ที่แก้ใต้ ## Changed Files ถ้าติดให้หยุดแล้วเขียนใต้ ## Questions อย่าเดาต่อ
---

รอบนี้ให้ทำ: T30–T40 (11 ข้อ · 9 ไฟล์)

กอง A — cast bar (savage 1C)
T30 BossAction.cs           castName + castTime + บวกเข้า GetEditorDuration
T31 BossController.cs       ClientRpc cast start/end + static event  ← ต้อง grep AttackLoop ก่อน
T32 BossHUDUI.cs            cast bar ของ main boss
T33 MiniBossBarEntry.cs     cast bar ต่อ mini-boss filter ด้วย instance

กอง B — arena anchor (savage 1B)
T34 ArenaDefinition.cs      CREATE — shape / center / radius
T35 ArenaAnchors.cs         CREATE — enum จุด + Resolve() แปลงเป็นพิกัด
T36 BossEncounterConfig.cs  เพิ่มช่อง arena

กอง C — โครงข้อมูล roll (savage 2B เฉพาะ data model)
T37 RollDefinition.cs       CREATE — serializable class ไม่ใช่ SO
T38 BossEncounterConfig.cs  เพิ่ม rolls[] (ไฟล์เดียวกับ T36)
T39 RollContext.cs          CREATE — สุ่มจาก seed ด้วย System.Random
T40 BossController.cs       NetworkVariable FightSeed + สร้าง RollContext (ไฟล์เดียวกับ T31)

จุดที่ให้ระวังเป็นพิเศษ:
- T31 ถ้า AttackLoop ไม่ใช่ coroutine หรือมีหลายจุดเรียก ExecuteCoroutine ให้หยุดเขียนใต้ Questions พร้อมเลขบรรทัด
- T32 ต้อง grep field ของ main boss panel ที่มีอยู่เดิมก่อน อย่าเดาชื่อ
- T39 ห้ามใช้ UnityEngine.Random เด็ดขาด ต้อง System.Random เพื่อให้ deterministic
- T40 ยังไม่ต้องมีใครเรียก Rolls.Roll() รอบนี้แค่ให้มันมี
- castTime = 0 หรือ castName ว่าง ต้องไม่มี cast bar โผล่ พฤติกรรมเดิมทุกประการ

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
