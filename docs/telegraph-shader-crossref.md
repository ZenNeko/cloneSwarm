# Telegraph — ตาราง cross-reference C# hit test ↔ shader graph

**บริบท:** ADR-007 §2 หนี้ข้อ 8 — ภาพ (`TelegraphUniversal.shadergraph`) กับ hit test
(`Assets/Script/TelegraphGeometry.cs`) เป็นคนละ implementation ของทรงเดียวกัน ไม่มีอะไรจับได้
อัตโนมัติถ้าฝั่งใดฝั่งหนึ่งเพี้ยน มาตรการที่ตกลงไว้คือคอมเมนต์โยงสองฝั่งเข้าหากัน — ราคาศูนย์
ไม่กันความเพี้ยน แต่ทำให้หาเจอ

ฝั่ง C# มีคอมเมนต์อยู่แล้วในแต่ละเมธอดของ `TelegraphGeometry.cs` ตารางนี้คือฝั่งกราฟ —
**paste ข้อความในคอลัมน์ "สิ่งที่ควรติดโน้ต" ลง sticky note บน node ที่เกี่ยวข้องใน
`TelegraphUniversal.shadergraph`** (เจ้าของกราฟทำเองทีหลัง เอเจนต์นี้ไม่แตะ `.shadergraph`)

| C# (`TelegraphGeometry` / `TelegraphZone.IsInXxx`) | Node/region ในกราฟที่เกี่ยวข้อง | สถานะ | สิ่งที่ควรติดโน้ตบน node |
|---|---|---|---|
| `IsInDonut` (annulus: `innerRadius..radius`) | node ที่ใช้ `_InnerRadius` — เทียบระยะจากศูนย์กลาง (object space) กับ `_InnerRadius` แล้วตัด alpha ออกเป็นรูตรงกลาง | 🔴 **มีคู่จริง — จุดเสี่ยงเพี้ยน** | "↔ `IsInDonut()` ใน `TelegraphGeometry.cs` — ค่าที่เข้ามาคือ `Clamp01(innerRadius/radius)` ส่งจาก `TelegraphZone.PushShaderColors()` ถ้าจะเปลี่ยนสูตร annulus ต้องแก้ทั้งสองฝั่งพร้อมกัน ดู `docs/telegraph-shader-crossref.md`" |
| `IsInCircle` | ไม่มี — ขอบเขตมาจาก mesh (`circlePrefab` สเกล `radius*2`) | ⚪ ไม่มีคู่ | ไม่ต้องติดโน้ตเรื่อง hit test — `_OutlineWidth`/`_EdgeGlow`/`_EdgeStrength` วาดเส้นขอบตกแต่งรอบ mesh เท่านั้น ไม่ตัดสินระยะ |
| `IsInCone` | ไม่มี — รูปทรงมาจาก mesh ที่ C# ปั้นเอง (`TelegraphZone.CreateConePrimitive`) | ⚪ ไม่มีคู่ในกราฟ (มีคู่กันเองใน C# ระหว่าง mesh กับ hit test) | ไม่เกี่ยวกับกราฟ — ถ้าจะโน้ตอะไร ให้โน้ตใน `TelegraphZone.cs` แทนว่า `coneAngle` ต้องตรงกันระหว่าง `CreateConePrimitive` กับ `IsInCone` |
| `IsInLine` / `IsInLineCross` | node ที่ใช้ `_OutlineShape` (0 = เหลี่ยม, 1 = โค้ง) | ⚪ ไม่มีคู่สูตรระยะ | "`_OutlineShape=0` แค่บอกวาดขอบเหลี่ยม ไม่ใช่สูตรระยะซ้ำกับ `IsInLine()` — กราฟไม่รู้เรื่อง lineWidth/lineLength เลย ขอบเขตจริงมาจาก mesh (`linePrefab` สเกล)" |

## ทำไมมีแค่ Donut ที่ "แดง"

ทรงอื่น (Circle/Line/Cross/Cone) ขอบเขตอันตรายมาจาก **mesh scale** ล้วนๆ — C# ตั้ง
`transform.localScale` ตรงกับ `radius`/`lineWidth`/`lineLength` ที่ hit test ใช้ ไม่มีสูตรระยะ
ซ้ำในกราฟให้เพี้ยน มีแต่ property ตกแต่งขอบ (`_OutlineWidth` ฯลฯ) ซึ่งไม่กระทบว่าอยู่ในโซนหรือไม่

Donut ต่างออกไปเพราะ **รูตรงกลางไม่ได้มาจาก mesh** — ใช้จานเต็มของ Circle เป็นวงนอก
(ดูคอมเมนต์ `TrySpawnVfxPrefab()` ใน `TelegraphZone.cs`) แล้วเจาะรูด้วย `_InnerRadius` ใน shader
graph แทน นี่คือจุดเดียวที่ annulus math ถูกคิดสองที่จริงๆ
