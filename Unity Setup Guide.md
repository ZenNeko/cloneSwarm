# คู่มือการตั้งค่าใน Unity: การรวมระบบอาวุธ (Weapon Integration)

คู่มือนี้สรุปขั้นตอนที่ต้องทำใน Unity Editor เพื่อนำอาวุธที่เพิ่งสร้างขึ้นใหม่เข้าสู่ระบบสุ่มการ์ดอัปเกรด (Upgrade Card Pool) ระบบอัปเกรดร่าง Super และการลงทะเบียน Prefab บนตัวละครผู้เล่น

---

## 📋 สรุปรายการอาวุธใหม่ที่ต้องตั้งค่า
อาวุธและสคริปต์ต่อไปนี้ถูกเพิ่มเข้ามาในโปรเจกต์แล้ว แต่ต้องนำไปคอนฟิกใน Unity Editor เพิ่มเติม:

1. **Molotov & Napalm Bomb** (อาวุธเริ่มต้น & ร่าง Super)
   - สคริปต์: `MolotovWeapon.cs` (เริ่มต้น), `NapalmBombWeapon.cs` (Super)
   - Prefabs: `Molotov Weapon.prefab`, `NapalmBombWeapon.prefab`
2. **Spike & Split Spike** (อาวุธเริ่มต้น & ร่าง Super)
   - สคริปต์: `SpikeWeapon.cs` (เริ่มต้น), `SplitSpikeWeapon.cs` (Super)
   - Prefabs: `SpikeWeapon.prefab`, `SplitSpikeWeapon.prefab`
3. **Big AoE**
   - สคริปต์: `BigAoEWeapon.cs`
4. **Big Cannon**
   - สคริปต์: `BigCannonWeapon.cs`
5. **Fence**
   - สคริปต์: `FenceWeapon.cs`
6. **Magic Missile**
   - สคริปต์: `MagicMissileWeapon.cs`
7. **Orbital Strike**
   - สคริปต์: `OrbitalStrikeWeapon.cs`
8. **Support Arena**
   - สคริปต์: `SupportArenaWeapon.cs`

---

## 🛠️ ขั้นตอนการรวมระบบใน Unity Editor

### ขั้นตอนที่ 1: เตรียมและกำหนดค่า Prefabs ของอาวุธ
สำหรับอาวุธใหม่แต่ละชิ้น:
1. เปิดโฟลเดอร์ `Assets/Prefab/Weapon/` (หรือ `Assets/Prefab/Weapon/Super/` สำหรับร่าง Super)
2. ลากและวางสคริปต์อาวุธที่เกี่ยวข้องใส่เข้าไปใน Prefab ของอาวะนั้น (เช่น ลาก `MolotovWeapon` ไปใส่ใน `Molotov Weapon.prefab`)
3. ปรับแต่งค่าต่างๆ ใน Inspector ของสคริปต์อาวุธ:
   - **การตั้งค่า VFX**: ระบุ `weaponVfxType` และ `secondaryVfxType` ให้ตรงกับ Key ในระบบคลังเอฟเฟกต์ `NetworkedVFXPool`
   - **การตั้งค่า SFX**: ลากไฟล์เสียงคลิปเสียง (AudioClips) ใส่ในอาเรย์ `fireSfx[]` และ `hitSfx[]`
   - **ระดับเสียงและรายละเอียด**: กำหนดระดับเสียง `fireVolume`, `hitVolume` และเปอร์เซ็นต์ความเพี้ยนเสียง `pitchVariance`
   - **Prefab อื่นๆ**: (ถ้ามี) ลากระเบิดหรือกระสุนที่ต้องใช้ไปเชื่อมโยงในช่องตัวแปรของสคริปต์ (เช่น ลาก `MolotovProjectile` ไปใส่ในช่องกระสุนของ `MolotovWeapon`)

### ขั้นตอนที่ 2: สร้างไฟล์ WeaponData (ScriptableObjects)
อาวุธใหม่ทั้งหมดต้องมีไฟล์ข้อมูล `WeaponData` เพื่อให้สเกลความแรงและระยะทำงานในฉากเกมได้:
1. ไปที่พาธโฟลเดอร์: `Assets/Script/Data/WeaponData/` (หรือโฟลเดอร์ย่อย `Super/` / `Fusion/` ตามระดับของอาวุธ)
2. **คลิกขวา** ในโฟลเดอร์ → **Create** → **LoL Swarm** → **Weapon Data**
3. เปลี่ยนชื่อไฟล์ให้ตรงกับอาวุธ (เช่น `WD_Molotov`)
4. กำหนดค่าต่างๆ ใน Inspector:
   - **Identity**: กรอก `weaponName` (ต้องสะกดให้ตรงกับชื่อที่เรียกใช้ในโค้ด) และคำอธิบายอาวุธ
   - **Tier**: เลือกระดับความหายากให้ถูกต้อง (`Normal` สำหรับร่างเริ่มต้น, `Super` สำหรับร่างพัฒนา และ `Fusion` สำหรับการผสมอาวุธ)
   - **Prefab**: ลาก Prefab อาวุธที่ตั้งค่าไว้จากขั้นตอนแรกมาวางในช่องนี้
   - **Aim Mode**: เลือกระบบเล็งเป้าหมาย (`AutoNearest` ล็อกตัวใกล้สุด หรือ `MouseAim` เล็งตามเมาส์)
   - **Levels (เลเวลอาวุธ)**: 
     - อาวุธปกติระดับ **Normal**: กดบวกสร้างเลเวลให้ครบ **5 เลเวล** และกรอกรายละเอียดในแต่ละเลเวล (เช่น พลังโจมตี cooldown, จำนวนกระสุน, ระยะ, คำอธิบายตอนอัปเกรดเลเวล)
     - อาวุธระดับ **Super/Fusion**: กดบวกสร้างเลเวลแค่ **1 เลเวล**

### ขั้นตอนที่ 3: เชื่อมโยงอาวุธเริ่มต้นกับร่าง Super (สำหรับ Molotov และ Spike)
เพื่อให้ระบบรู้ว่าเวลาเก็บกล่อง/ลูกแก้วออบเจกต์จะพัฒนาเป็นร่างใด:
1. คลิกเลือกไฟล์ `WeaponData` ของอาวุธเริ่มต้น (เช่น `WD_Molotov`)
2. เลื่อนลงมาที่หัวข้อ **Super Upgrade**
3. ลากไฟล์ `WeaponData` ของร่าง Super (เช่น `WD_NapalmBomb`) มาใส่ในช่อง `superVersion`
4. **ตั้งเงื่อนไขสเตตัส (Super Conditions)**: หากการพัฒนาอาวุธต้องการสเตตัสอื่นประกอบ (เช่น ต้องมีขนาดพื้นที่ `AreaSize` เลเวล 1 ขึ้นไป) ให้กดเพิ่มช่องเงื่อนไขในลิสต์ `superConditions` และระบุให้ถูกต้อง
   *(หมายเหตุ: เลเวลของอาวุธเริ่มต้นที่ต้องถึงเลเวล 5 ระบบโค้ดจะเช็คให้อัตโนมัติอยู่แล้ว ไม่ต้องกรอกซ้ำที่นี่)*

### ขั้นตอนที่ 4: ลงทะเบียนในระบบสุ่มการ์ดอัปเกรด (Upgrade Pool)
อาวุธจะไม่โผล่มาให้สุ่มเลือกในตอนเลเวลอัป หากไม่ได้ลงทะเบียนไว้กับ `UpgradeManager`:
1. เปิดฉากเกมหลัก (เช่น `SampleScene` หรือห้องล็อบบี้)
2. คลิกเลือก Game Object ที่ถือสคริปต์ `UpgradeManager` อยู่
3. หาลิสต์ชื่อ `allWeapons` ใน Inspector
4. ลากไฟล์ `WeaponData` ของอาวุธระดับ **Normal** ใหม่ทั้งหมดที่สร้างเสร็จแล้วไปใส่เพิ่มลงในลิสต์นี้
5. (ถ้ามีสูตรผสม) ลากสูตรฟิวชันใหม่ใส่ในลิสต์ `allRecipes` ด้วย
