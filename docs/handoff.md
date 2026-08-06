# Handoff → Antigravity  (Round 2)

ก๊อปข้อความข้างล่างนี้ไปวางใน Antigravity:

---
อ่าน AGENTS.md แล้วอ่าน docs/implementation_plan.md ทำ F1 ก่อนเป็นข้อแรก แล้วค่อยทำ Task ที่เป็น [ ] ตามลำดับ แก้เฉพาะไฟล์ที่แผนระบุ ห้ามแตะไฟล์ที่ Round 1 สร้างไว้ (LobbyState TabBar LobbyUI MapData RunSetup DifficultyTier) ห้ามรัน build หรือ unity-check ห้ามสร้าง prefab หรือ asset ห้าม commit เสร็จแต่ละข้อให้ติ๊ก [x] และสรุปไฟล์ที่แก้ใต้ ## Changed Files ถ้าติดให้หยุดแล้วเขียนใต้ ## Questions อย่าเดาต่อ
---

รอบนี้ให้ทำ: F1 + T11–T22 (13 ข้อ · 12 ไฟล์)

F1  MenuManager.cs         ลบปีกกาเกินบรรทัด 261-263   ← ทำก่อนทุกข้อ ตอนนี้คอมไพล์ไม่ผ่าน

กอง A — UI ซ้อนกัน
T11 PauseMenuUI.cs         การ์ดใน Toggle() กัน LevelUp/GameOver
T12 LevelUpUI.cs           waitingStrip แทนแผงเต็มจอตอนรอเพื่อน
T13 SharedExperienceManager.cs  timer เริ่มหลังคนแรกเลือก

กอง B — savage Phase 1A (บั๊กสีผู้เล่นจริง)
T14 PlayerSlotRegistry.cs  CREATE — server แจก slot 0-3
T15 ColorMatchAoEAction.cs บรรทัด 68 กับ 75 ใช้ slot แทน clientId % 4
T16 TelegraphZone.cs       บรรทัด 271 เหมือนกัน

กอง C — ของค้างจาก Round 1
T17 BossManager.cs         เอา activeBossConfig ไปใส่ BossController.config ก่อน Spawn
T18 TalentShopUI.cs        แท็บตัวละครทำ grid จริง

กอง D — หน้าจบเกม
T19 WinLoseUI.cs           ปุ่มเล่นอีกครั้ง (host เท่านั้น)
T20 TempPartyHUD.cs        โชว์ชื่อตัวละครต่อท้ายชื่อผู้เล่น

กอง E — เอกสาร
T21 CLAUDE.md              แก้กฎ VFX ที่ชี้ไป VFXType ที่ถูกลบไปแล้ว
T22 CLAUDE.md              เพิ่มกติกาใหม่ 4 ข้อ

จุดที่ให้ระวัง:
- T15/T16 เป็นที่เดียวที่แผนอนุญาตให้ใส่ fallback ที่อื่นห้าม
- T13 ถ้าหา pickedPlayers.Add ไม่เจอหรือกระจายเกิน 2 จุด ให้หยุดเขียนใต้ Questions
- T17 ถ้าพบว่า client ต้องเห็น config เดียวกับ server ให้หยุดเขียนใต้ Questions ห้ามทำ sync เอง
- T20 ถ้า PlayerVisual ไม่มี public accessor ของ CharacterData ให้หยุดเขียนใต้ Questions
- ห้ามแตะ BossController.cs

เสร็จแล้วกลับมาบอกว่า "เสร็จ"
