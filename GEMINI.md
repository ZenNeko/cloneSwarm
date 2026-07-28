# GEMINI.md

**อ่าน `AGENTS.md` ที่ root ของโปรเจกต์ก่อนเริ่มทำงานทุกครั้ง** — กติกากลางทั้งหมดอยู่ในไฟล์นั้น
ไฟล์นี้เป็นแค่ตัวชี้ทาง เพื่อไม่ให้กฎแตกเป็นสองชุดแล้วขัดกันเอง

สรุปข้อที่ห้ามพลาดที่สุด (รายละเอียดใน `AGENTS.md`):

1. ใบสั่งงานคือ `docs/implementation_plan.md` ทำเฉพาะ task ที่เป็น `[ ]` เสร็จแล้วติ๊ก `[x]`
2. server-authoritative — damage ต้องผ่าน `PlayerWeaponManager.<Action>ServerRpc(...)` เท่านั้น
3. VFX ใช้ `NetworkedVFXPool.Instance.PlayByType(...)` · SFX ใช้ `SoundManager.Instance.PlaySfx*`
4. **ห้ามรัน build / test / `unity-check.ps1`** — Claude เป็นคนรันและตรวจเอง
5. ห้ามแตะ `Library/` `Temp/` `obj/` `Build/` `scratch/`
