# Handoff → Antigravity  (Round 5)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md จากนั้นทำทุก Task ที่เป็น [ ] เรียงตามลำดับห้ามสลับ เพราะ T64 ลบไฟล์ที่ T56 ต้องเลิกอ้างอิงก่อน แก้เฉพาะไฟล์ที่แผนระบุ ห้ามแตะไฟล์ที่ Round 1-4 ทำไปแล้ว (LobbyState TabBar MapData RunSetup DifficultyTier PlayerSlotRegistry PlayerStatusManager StatusHUDUI BossController) ห้ามรัน build หรือ unity-check ห้ามแตะไฟล์ .unity .prefab .asset ห้าม commit เสร็จแต่ละข้อให้ติ๊ก [x] และสรุปไฟล์ที่แก้ใต้ ## Changed Files ถ้าติดให้หยุดแล้วเขียนใต้ ## Questions อย่าเดาต่อ
---

รอบนี้ให้ทำ: T51–T64 (13 ข้อ · 6 ไฟล์)

กอง A — ยกลอจิก session ขึ้น GameSessionManager (ทำก่อนทุกกอง)
T51 GameSessionManager.cs   เพิ่ม sessionSettings + OnStatus event + isBusy
T52 GameSessionManager.cs   ยก CreateSessionAsync มา ครบทั้ง 4 กันชน
T53 GameSessionManager.cs   ยก JoinSessionAsync(code) มา
T54 GameSessionManager.cs   ยก LeaveSessionIfActiveAsync + RestoreFromExistingSession
T55 GameSessionManager.cs   IsHost = NetworkManager.IsServer || session.IsHost   ← แก้บั๊ก solo

กอง B — เส้นทางเมนู
T56 MenuManager.cs          ลบ MenuMode + charSelect + onlinePanel · PLAY เข้าล็อบบี้ตรง
T57 MenuManager.cs          ปุ่มเข้าห้องเพื่อนที่เมนูหลัก

กอง C — ล็อบบี้
T58 LobbyUI.cs              RunSetup เป็นแหล่งความจริง
T59 LobbyUI.cs              เชิญเพื่อน = Shutdown แล้วสร้าง session
T60 LobbyUI.cs              รหัสห้อง + copy + เข้าห้องจากในล็อบบี้
T61 LobbyUI.cs              ใช้ PlayerSlotRegistry แทน clientId ดิบ

กอง D — เก็บกวาด
T62 CharacterSelectUI.cs    คลิกการ์ด = เลือกทันที · Confirm เหลือแค่ปลดล็อก
T63 LobbyUI.cs              subscribe OnCharacterConfirmed
T64 OnlineMenuUI.cs         DELETE ทั้งไฟล์ + .meta   ← ข้อสุดท้ายเท่านั้น

จุดที่ให้ระวังเป็นพิเศษ:
- T52 ห้ามตัดกันชน 4 อย่างทิ้ง (รอ services 15 วิ / รอ auth 15 วิ / isBusy / WebGL guard) ทั้งหมดมาจากบั๊กจริงที่เคยเจอ
- T55 แก้บรรทัดเดียวแต่แก้ทั้ง START RUN ใน solo และปุ่มเล่นอีกครั้งใน WinLoseUI ห้ามไปแก้ StartGame หรือ WinLoseUI เอง
- T62 บรรทัด SelectedCharacter = cd ต้องมาก่อนยิง event เสมอ ตัวละครที่เล่นจริงพึ่งเส้นนี้ ห้ามแตะ
- T61 ถ้า GetSlot คืน -1 ให้แสดง "?" ห้าม fallback ไป clientId
- T64 ก่อนลบ grep หา OnlineMenuUI ให้เหลือศูนย์ก่อน ถ้ายังมีแปลว่า T56 ไม่ครบ ให้หยุดเขียนใต้ Questions
- ห้ามเขียน retry loop เองทุกที่

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
