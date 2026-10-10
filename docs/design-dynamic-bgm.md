# ออกแบบ: BGM ที่เปลี่ยนตามช่วงเวลาของรันและการเจอบอส

**Status:** Implemented (โค้ด) 2026-09-23 — คอมไพล์ผ่านทั้งสอง assembly · ยังไม่ได้ต่อในซีน · ยังไม่ได้ฟังจริง · DevTools แถว Music ยังไม่ทำ
**Date:** 2026-09-23 (แก้ครั้งที่ 2 — เปลี่ยนจากสลับเพลงเป็น **ซ้อนชั้นเพลง** ตามที่เจ้าของเลือก)
**เกี่ยวข้อง:** `Assets/Script/Audio/SoundManager.cs` · `SceneBGMPlayer.cs` · `GameTimeline.cs` · `BossController.cs` · `MapData.cs`

---

## 0 · แนวคิด

> *"ส่วนใหญ่จะเป็นเพลงเดียวกัน แต่เพิ่มองค์ประกอบของเพลง เช่นเพิ่มเสียงเบส"* — เจ้าของ

เพลงหนึ่งเพลงถูกแยกเป็น **stem** (ชั้นเครื่องดนตรี) ที่ยาวเท่ากันเป๊ะ เล่นพร้อมกันทุกชั้นตลอดเวลา
เกมเปลี่ยน "อารมณ์" ด้วยการ **เร่ง/ลดเสียงของแต่ละชั้น** ไม่ใช่เปลี่ยนเพลง

```
นาที     0 ──────── 5 ──────── 10 ─────── 15 ── บอส
pad      ████████████████████████████████████████████
melody   ████████████████████████████████████████████
drums              ██████████████████████████████████
bass                          ███████████████████████
perc     ░░░░░░███░░░░░░░███░░░░░░░███░░░░░░███░░░░░   ← เปิดเฉพาะตอนมินิบอสมีชีวิต
```

บอสใหญ่: ถ้าแมพมี **ธีมบอส** → crossfade ไปเพลงบอส แล้วปรับชั้นตามเฟส · ถ้า **ไม่มี** → เพลงเดิมเล่นต่อตามที่เป็นอยู่ ไม่ปรับอะไร (ข้อ 4.5)

เพลงไม่เคยหยุดหรือเริ่มใหม่ → ไม่มีรอยต่อ · ความตึงค่อยๆ ขึ้นแบบที่ผู้เล่นรู้สึกได้แต่ไม่สะดุด

---

## 1 · Requirements

### ต้องทำได้
| # | พฤติกรรม |
|---|---|
| F1 | ชั้นเพลง **เพิ่มตามช่วงเวลาของรัน** (เช่น นาที 5 เปิด drums · นาที 10 เปิด bass) |
| F2 | **มินิบอสมีชีวิต** → เปิดชั้นเสริม · ตายหมด → ชั้นนั้นค่อยๆ ลดลง |
| F3 | **บอสใหญ่** → มีธีมบอส: สลับไปเพลงบอสและปรับชั้นตามเฟส · ไม่มี: เพลงเดิมเล่นต่อ ไม่ปรับ (ข้อ 4.5) |
| F4 | ชนะ/แพ้ → stinger ปิดท้าย |
| F4b | **จอเลือกการ์ด (Level Up / Orb)** → เปิด/ปิดบางชั้นได้ + ลดเสียงทั้งเพลง · ปิดจอแล้วกลับ mix เดิม |
| F4c | **Pause → ไม่ทำอะไรกับเพลง** (ตัดสินแล้ว 2026-09-23) |
| F5 | ชั้นเพลงทุกชั้น **ตรงจังหวะกันระดับ sample** ตลอดทั้งรัน |
| F6 | ตั้งค่าได้ต่อแมพ/ต่อระดับความยาก โดย designer ไม่ต้องแก้โค้ด |

### คุณภาพ
- **ไม่เพิ่มทราฟฟิกเครือข่ายเลย** — เพลงเป็นเรื่องที่แสดงผลบนแต่ละเครื่องล้วน
- ทุก client ได้ **mix เดียวกัน** (ไม่ต้องตรงจังหวะกันข้ามเครื่อง)
- ทำงานถูกเมื่อเข้ากลางเกม / reconnect ในอนาคต
- ไม่มี GC ต่อเฟรม · slider เสียงใน Pause ยังทำงาน · เดินต่อได้ตอน `Time.timeScale = 0`

### ข้อจำกัด
- Unity AudioSource ล้วน ไม่มี FMOD/Wwise
- `SoundManager` ตอนนี้มี music source ตัวเดียว และ `RescalePlaying()` เขียน `volume` ทับตรงๆ

---

## 2 · การตัดสินใจหลัก

### D1 · สถานะเพลงคำนวณบนแต่ละ client จากสถานะที่ sync อยู่แล้ว — ไม่มี RPC ใหม่

| สัญญาณ | มาจาก | ถึง client ยังไง |
|---|---|---|
| เวลาเกม | `GameTimeline.gameTime` | NetworkVariable |
| อยู่ช่วงบอสใหญ่ | `GameTimeline.isMainBossPhase` | NetworkVariable |
| มินิบอสเกิด/ตาย | `BossController.OnAnyBossSpawned / OnAnyBossDespawned` | ยิงบนทุก client |
| เฟสบอส | `BossController.PhaseChangedClientRpc` | ClientRpc — **ต้องเพิ่ม static event** |
| ชนะ/แพ้ | `GameTimeline.OnGameWon / OnGameLost` | ClientRpc → static event |
| เปิดจอเลือกการ์ด | `SharedExperienceManager.OnUpgradePhaseStart` (เลเวลอัป) · `OnOrbPhaseStart` (orb) | ClientRpc → static event |
| ปิดจอเลือกการ์ด | `SharedExperienceManager.OnUpgradePhaseEnd` (ใช้ร่วมกันทั้งสองแบบ) | ClientRpc → static event |

ถ้า server สั่ง "เปิดเบส" ผ่าน RPC คนที่เข้ามากลางเกมจะพลาดคำสั่ง · คำนวณจากสถานะแทน → เข้ามาเมื่อไรก็ได้ mix ถูกทันที (หลักเดียวกับ `BossHUDUI`)

### D2 · Vertical layering เป็นแกนหลัก · สลับเพลงเป็นทางเสริม

| วิธี | ใช้ตอนไหน |
|---|---|
| **ซ้อนชั้น (vertical)** — ปรับเสียงของแต่ละ stem | **เกือบทุกอย่าง**: ช่วงเวลา · มินิบอส · เฟสบอส |
| **สลับเพลง (horizontal)** — crossfade ไปอีกชุด stem | เฉพาะตอนที่ต้องการเพลงใหม่จริงๆ เช่น บอสใหญ่มีธีมของตัวเอง · ใส่หรือไม่ใส่ก็ได้ |

### D3 · มินิบอส = ชั้นเสริมแบบ **overlay** บวกทับ mix ของช่วงเวลา

มินิบอส 5 ตัวใน 15 นาที (Arena01) · ถ้าสลับเพลงทุกครั้งจะวุ่นมาก · แต่ถ้า **เปิดชั้นเพิ่ม** (เช่น percussion) ทับ mix ปัจจุบัน เพลงเดินต่อเหมือนเดิมแค่ดุขึ้น ตรงกับแนวคิดของเจ้าของพอดี
ผลคือ mix = **ค่ามากกว่าของ (mix ช่วงเวลา, overlay มินิบอส)** ต่อชั้น — นาที 12 ที่มี bass อยู่แล้ว มินิบอสก็ไม่ไปลด bass ลง

---

## 3 · ภาพรวมโครงสร้าง

```
 ┌──────────────── ทุก client (ไม่มี network ใหม่) ─────────────────┐
 │                                                                    │
 │  gameTime (NV) ───────────────┐                                    │
 │  isMainBossPhase (NV) ────────┤                                    │
 │  OnAnyBossSpawned/Despawned ──┼──►  MusicDirector   (ใหม่ · ต่อซีน)│
 │  OnAnyPhaseChanged (ใหม่) ────┤     คำนวณ "mix เป้าหมาย"          │
 │  OnGameWon / OnGameLost ──────┘     = float[] ต่อ stem             │
 │                                             │                      │
 │     MusicProfile (SO) ◄─ MapData.TierContent │                      │
 │                                             ▼                      │
 │                               LayeredMusicPlayer (ใหม่ · ใต้ SoundManager)
 │                               stem 0..N  AudioSource ต่อชั้น       │
 │                               เริ่มพร้อมกันด้วย PlayScheduled       │
 │                               fade แต่ละชั้นไปหาค่าเป้าหมาย         │
 └────────────────────────────────────────────────────────────────────┘
```

| ส่วน | ใหม่/แก้ | หน้าที่ |
|---|---|---|
| `LayeredTrack` (SO) | ใหม่ | เพลงหนึ่งเพลง = รายการ stem + BPM |
| `MusicProfile` (SO) | ใหม่ | mix ของแต่ละสถานการณ์ในแมพหนึ่ง/ระดับหนึ่ง |
| `LayeredMusicPlayer` | ใหม่ (อยู่ใต้ SoundManager ข้ามฉาก) | เล่น stem ให้ตรงกัน · fade รายชั้น · สลับเพลง |
| `MusicDirector` (MonoBehaviour ธรรมดา) | ใหม่ | อ่านสัญญาณ → คำนวณ mix เป้าหมาย |
| `SoundManager` | แก้ | ถือ `LayeredMusicPlayer` · แก้ `RescalePlaying` · duck |
| `BossController` | แก้ 1 บรรทัด | `static event OnAnyPhaseChanged` |
| `MapData.TierContent` | แก้ 1 ช่อง | `musicProfile` |
| `SceneBGMPlayer` | คงไว้ | MenuScene ใช้เหมือนเดิม (= เพลง 1 stem) |

แยก `LayeredMusicPlayer` ออกจาก `SoundManager` เพราะมีสถานะของตัวเองเยอะ (source ต่อชั้น, เวลา dsp, fade) · SoundManager ยังเป็นทางเข้าทางเดียวตามกติกาเสียงของโปรเจกต์

---

## 4 · รายละเอียด

### 4.1 Data model

```csharp
[CreateAssetMenu(menuName = "Clone Swarm/Audio/Layered Track")]
public class LayeredTrack : ScriptableObject
{
    [Serializable] public struct Stem
    {
        public string    name;      // "pad", "drums", "bass" — ใช้แสดงใน Inspector/DevTools
        public AudioClip clip;
    }
    public Stem[] stems;            // ทุกคลิปต้องยาวเท่ากันเป๊ะ (จำนวน sample)
    public float  bpm = 120f;       // ใช้จัดจังหวะการเปิดชั้น (ข้อ 4.4)
    public int    beatsPerBar = 4;
}

[Serializable] public struct StemLevel
{
    [StemId] public string stem;             // อ้างด้วย "ชื่อ" ไม่ใช่เลขลำดับ · dropdown จาก track
    [Range(0f, 1f)] public float level;
}

[Serializable] public class StemMix
{
    public StemLevel[] levels;               // stem ที่ไม่อยู่ในรายการ = 0
    public float fadeSeconds = -1f;          // < 0 = ใช้ค่าตั้งต้นของ profile
}

[CreateAssetMenu(menuName = "Clone Swarm/Audio/Music Profile")]
public class MusicProfile : ScriptableObject
{
    public LayeredTrack track;               // เพลงหลักของรัน

    [Serializable] public struct TimeBand { [Min(0f)] public float atMinutes; public StemMix mix; }
    public TimeBand[] timeBands;             // เรียงตามนาที · เหมือน WavePhase

    public StemMix  miniBossOverlay;         // บวกทับ (ค่ามากกว่าต่อชั้น)
    [Min(0f)] public float miniBossReleaseHold = 3f;

    public LayeredTrack mainBossTrack;       // ว่าง = เพลงเดิมเล่นต่อ ไม่ปรับอะไร
    public StemMix[]    mainBossPhases;      // ใช้เฉพาะเมื่อมี mainBossTrack · index = phase · ขาดช่องใช้ช่องก่อนหน้า

    [Header("จอเลือกการ์ด (Level Up / Orb)")]
    public StemLevel[] cardPickOverrides;             // มีในรายการ = บังคับค่า · ไม่มี = ไม่แตะ
    [Range(0f, 1f)] public float cardPickDuck = 0.5f; // ลดเสียงทั้งเพลง
    public float cardPickFade = 0.4f;                 // สั้นกว่าปกติ — จอเด้งขึ้นทันที

    public AudioClip winStinger, loseStinger;
    [Min(0f)] public float defaultFade = 2f;
}

// จอเลือกการ์ดใช้ StemLevel[] เหมือนกัน — stem ที่ "มีในรายการ" ถูกบังคับเป็นค่านั้น
// stem ที่ไม่มีในรายการ = ไม่แตะ (ต่างจาก StemMix ที่ไม่มี = 0)
// public StemLevel[] cardPickOverrides;
```

### 4.1b จำนวน stem ต่อแมพไม่เท่ากัน

> *"stem ทำให้ปรับเพิ่มในแต่ละ map ไม่เท่ากันได้"* — เจ้าของ

แต่ละแมพ (และแต่ละระดับความยาก) ชี้ไปที่ `LayeredTrack` ของตัวเองผ่าน `MusicProfile` — แมพหนึ่งมี 3 ชั้น อีกแมพมี 7 ชั้นก็ได้

**ทำไมอ้างด้วยชื่อ ไม่ใช่เลขลำดับ:** ถ้าเก็บเป็น `float[]` ตามลำดับ stem พอมาแทรก stem ใหม่ตรงกลางภายหลัง (เช่นเพิ่ม `choir` ระหว่าง `drums` กับ `bass`) ค่าทุกช่องหลังจุดนั้นจะเลื่อนไปชั้นผิดเงียบๆ — bass จะไปได้ค่าของ choir · อ้างด้วยชื่อแล้วเพิ่ม/สลับ/ลบ stem ได้โดย mix เดิมยังถูก
(บทเรียนเดียวกับ `rollName` ในระบบบอส — ใช้ชื่อ แต่มี dropdown กับตัวตรวจกันพิมพ์ผิด)

| ชั้นกันพลาด | ทำอะไร |
|---|---|
| `[StemId]` drawer | dropdown ชื่อ stem จาก track ของ profile นั้น · ชื่อที่ไม่มีแล้วขึ้นป้าย "ไม่มีใน track" ไม่เด้งเป็นค่าแรกเอง (แบบ `RollIdDrawer`) |
| ตอนรัน | ชื่อที่ไม่เจอ → warn ครั้งเดียวต่อชื่อ แล้วข้าม |
| `P3RSmokeTest` | ไล่ทุก profile ของทุกแมพ ทุกชื่อใน mix ต้องมีใน track · stem ชื่อซ้ำใน track เดียว = ตก |

ตอนโหลด profile ระบบแปลงชื่อเป็น index **ครั้งเดียว** เก็บไว้ในตาราง → ตอนเล่นไม่มีการเทียบสตริงต่อเฟรม

**`LayeredMusicPlayer` รองรับจำนวนชั้นที่เปลี่ยนไป:** จอง AudioSource ตามจำนวน stem ของ track ที่ยาวที่สุดที่เคยเจอ (ขยายได้ ไม่หด) · เข้าแมพที่ชั้นน้อยกว่า ตัวที่เกินปิดเงียบไว้ · ไม่สร้าง/ทำลาย source ใหม่ทุกครั้งที่เปลี่ยนแมพ

ตัวอย่าง: ระหว่างเลือกการ์ด ปิด drums/bass/perc เหลือ pad + melody แล้วลดเสียงทั้งเพลงลงครึ่งหนึ่ง → ให้ความรู้สึก "หยุดหายใจ" แล้วพอปิดจอทุกชั้นกลับมาเท่าเดิม
ใช้ override **ต่อชั้น** ไม่ใช่ mix เต็ม เพราะจอเลือกการ์ดเกิดได้ทุกช่วงของรัน (นาที 2 หรือกลางไฟต์บอส) · ชั้นที่ไม่ได้ตั้งจะยังเป็นไปตามสถานการณ์ตอนนั้น

Inspector แสดงแต่ละแถวเป็น dropdown ชื่อ stem + แถบเลื่อนระดับ — designer เลือกจากชื่อ ไม่ต้องจำลำดับ

**ตัวอย่าง Arena01 — เพลงหลัก**
| สถานการณ์ | pad | melody | drums | bass | perc |
|---|---|---|---|---|---|
| นาที 0 | 1 | 1 | 0 | 0 | 0 |
| นาที 5 | 1 | 1 | 1 | 0 | 0 |
| นาที 10 | 1 | 1 | 1 | 1 | 0 |
| overlay มินิบอส | – | – | – | – | 1 |

**ธีมบอส (ถ้าใส่ `mainBossTrack`)** — stem ของเพลงบอสเอง ตั้งชื่อ/จำนวนต่างจากเพลงหลักได้
| เฟส | … ทุก stem ของเพลงบอส … |
|---|---|
| 1 | บางชั้น |
| 2 | เพิ่มชั้น |
| 3 | ครบทุกชั้น (= เวอร์ชันเต็ม) |

### 4.2 ลำดับความสำคัญ

```
Result (ชนะ/แพ้)          ← fade ทุกชั้นลง + stinger · ไม่ถอยกลับ
  > MainBoss(phase)         ← isMainBossPhase **และ** มี mainBossTrack
  > MainBoss ไม่มีธีม       ← ค้าง mix ล่าสุดก่อนบอสออก (ไม่ปรับอะไร)
  > max(TimeBand(t), MiniBossOverlay ถ้ามีมินิบอส)
แล้วค่อยใส่ CardPick override ทับชั้นที่ตั้งไว้ + duck   ← เป็นตัวปรับ ไม่ใช่สถานะแยก
```

```csharp
float[] ResolveTargetLevels()
{
    if (_result != Result.None)            return Silence;
    float[] levels;
    if (_timeline.isMainBossPhase.Value)
        levels = _profile.mainBossTrack != null
            ? PhaseMix(_bossPhase)                           // ธีมบอส ปรับตามเฟส
            : _lastPreBossMix;                               // ไม่มีธีม: ค้าง mix เดิม
    else
    {
        levels = BandMix(_timeline.gameTime.Value);          // copy ใส่ buffer ที่จองไว้
        if (_aliveMiniBosses > 0 || Time.unscaledTime < _miniReleaseAt)
            MaxInto(levels, _profile.miniBossOverlay.levels);
        Copy(levels, _lastPreBossMix);                       // จำไว้ใช้ตอนบอสออก
    }
    if (_cardPickOpen)
        ApplyOverrides(levels, _profile.cardPickOverrides);  // เฉพาะชั้นที่ enabled
    return levels;
}
// duck = _cardPickOpen ? cardPickDuck : 1  → ส่งให้ player แยก
```

**ทำไม CardPick เป็นตัวปรับทับ ไม่ใช่สถานะที่สี่:** ระหว่างเลือกการ์ด เวลาเกมหยุดแต่มินิบอสยังมีชีวิต/บอสยังอยู่เฟสเดิม · ถ้าทำเป็นสถานะแยก พอปิดจอต้องจำว่าก่อนหน้าคืออะไร · ทำเป็นตัวปรับที่ใส่ท้ายสุด ปิดจอแล้วสูตรเดิมคำนวณ mix ที่ถูกให้เอง

`MusicDirector` เก็บแค่ข้อเท็จจริง แล้วคำนวณ mix ใหม่ทุกครั้งที่มีอะไรเปลี่ยน ไม่มีสถานะแบบทีละขั้นให้หลุด · บอสใหญ่ถูกนับเป็นมินิบอสหนึ่งเฟรมก็ไม่เป็นไร เพราะ MainBoss ชนะลำดับอยู่แล้ว

### 4.3 `LayeredMusicPlayer` — เล่น stem ให้ตรงกัน

- **AudioSource หนึ่งตัวต่อชั้น** ทุกตัว `loop = true`
- เริ่มพร้อมกันด้วย `source.PlayScheduled(AudioSettings.dspTime + 0.1)` **เวลาเดียวกันทุกตัว** — ถ้าใช้ `Play()` ทีละตัว จะเหลื่อมกันได้หลายมิลลิวินาทีและได้ยินเป็นเสียงก้อง
- ตรวจตอนโหลด: ทุก stem ต้องมี `samples` และ `frequency` เท่ากัน ไม่งั้นลูปจะค่อยๆ เหลื่อม → warn ชื่อ stem ที่ผิด
- **Import settings ของ stem** (ต้องเหมือนกันทุกตัว): Load Type = *Compressed In Memory* · Vorbis · Preload = on
  ไม่ใช้ *Streaming* เพราะหลายชั้นอ่านดิสก์พร้อมกัน และจังหวะเริ่มเล่นไม่แน่นอน
- **Fade รายชั้น:** แต่ละชั้นมี `current` → `target` เดินด้วย `Time.unscaledDeltaTime` (จอ Level Up ตั้ง `timeScale = 0`)
- **volume จริงของแต่ละชั้น** = `stemLevel × musicScale (Music × Master) × duck` — คำนวณจากสามค่านี้ทุกครั้ง
  → **แก้กับดักเดิม:** `RescalePlaying()` ต้องบอก player ให้คำนวณใหม่ ห้าม set `volume` ตรง ไม่งั้นขยับ slider แล้วชั้นที่ปิดอยู่จะดังขึ้นเต็ม
- ชั้นที่ `current == 0` ยังเล่นต่อ (แค่เงียบ) — ห้าม `Stop()` เพราะเปิดกลับจะหลุดจังหวะ
- **สลับเพลง** (ถ้ามี `mainBossTrack`): เตรียมชุด source ชุดที่สอง `PlayScheduled` ที่ต้นห้องถัดไป แล้ว crossfade ทั้งชุด · ต้องมี source 2 เท่าของจำนวน stem ในช่วง fade

### 4.4 จังหวะการเปิดชั้น

| แบบ | วิธี | ใช้ |
|---|---|---|
| ทันที | fade เริ่มเลย | **v1** — fade 1–2 วินาทีฟังเป็นธรรมชาติอยู่แล้ว เพราะเพลงไม่หยุด |
| ตรงห้อง | รอถึงต้นห้องถัดไป (`bpm`, `beatsPerBar`, เวลา dsp ตอนเริ่ม) แล้วค่อย fade | v2 — เช่น เบสเข้าที่จังหวะ 1 พอดี · data มี BPM ไว้แล้ว ไม่ต้องแก้ asset |

### 4.5 บอสใหญ่ — เพลงเดิมหรือเพลงใหม่

| `mainBossTrack` | ตอนบอสออก | ระหว่างไฟต์ |
|---|---|---|
| **ใส่** (ธีมบอส) | crossfade ไปเพลงบอสครั้งเดียว | ปรับชั้นตาม `mainBossPhases` ทุกครั้งที่เปลี่ยนเฟส |
| **ว่าง** | **ไม่มีอะไรเกิดขึ้น** เพลงเดิมเล่นต่อ | **ไม่ปรับ** — ค้าง mix ล่าสุดก่อนบอสออก · `mainBossPhases` ถูกข้าม (ตัดสินแล้ว 2026-09-23) |

- "ค้าง mix ล่าสุด" หมายถึงรวม overlay มินิบอสด้วย ถ้ามินิบอสยังมีชีวิตตอนบอสใหญ่ออก · ถ้าอยากให้ชั้นมินิบอสลดลงตอนบอสใหญ่ออก ให้ใส่ธีมบอสแทน
- ชนะ/แพ้ยังทำงานเหมือนกันทั้งสองแบบ

### 4.6 `MusicDirector`

- วางใน SampleScene · subscribe static event ใน `OnEnable` / unsubscribe ใน `OnDisable` (ข้อ 5 ของ CLAUDE.md)
- ช่วงเวลา: poll `gameTime` ทุก 0.5s · มินิบอส: นับจาก Spawned/Despawned · หลังตัวสุดท้ายตาย ค้าง overlay ไว้ `miniBossReleaseHold` วินาที กันชั้นเปิดปิดถี่ๆ
- **ตอนเริ่ม:** อ่านสถานะปัจจุบันครั้งเดียว (นับ `BossController` ในฉาก) → ทางที่ทำให้ reconnect ได้ mix ถูก
- ชนะ/แพ้: fade ลง + `SoundManager.PlaySfx2D(stinger)`
- **จอเลือกการ์ด:** `OnUpgradePhaseStart` / `OnOrbPhaseStart` → `_cardPickOpen = true` · `OnUpgradePhaseEnd` → `false` · ใช้ `cardPickFade` ทั้งขาเข้าและขาออก
  - fade ต้องใช้ `Time.unscaledDeltaTime` — ตอนนี้แหละที่ `timeScale = 0` (`GamePause.PauseReason.PhaseSelect`)
  - เล่นคนเดียวไม่มีการนับถอยหลัง → จออาจเปิดค้างนาน · เพลงยังวนต่อเงียบๆ ตามปกติ ไม่ต้องทำอะไรพิเศษ
- **Pause: ไม่ทำอะไร** · เพลงเล่นต่อตามปกติ · (AudioSource ไม่ขึ้นกับ `timeScale` และโค้ดไม่ได้แตะ `AudioListener.pause` จึงไม่ต้องกันอะไร)
- **เฟสบอส:** v1 ใช้ `OnAnyPhaseChanged` (static event ใหม่ ยิงใน `OnPhaseChangedClient`) · ตอนทำ reconnect ย้ายไปอ่าน `NetworkVariable<int> CurrentPhase` บน `BossController` (HUD ก็ต้องใช้เหมือนกัน)

---

## 5 · ความทนทานและการตรวจ

| กรณี | ผล |
|---|---|
| Profile ว่าง | ถอยไปใช้คลิปของ `SceneBGMPlayer` (1 stem) · warn ครั้งเดียว |
| stem ความยาวไม่เท่ากัน | ยังเล่น แต่ warn ชื่อ stem และจำนวน sample · smoke test ตกด้วย |
| mix ไม่ได้พูดถึงบาง stem | ชั้นนั้นเป็น 0 (จอเลือกการ์ด: ไม่แตะ) |
| mix อ้างชื่อ stem ที่ไม่มีใน track | warn ครั้งเดียว แล้วข้าม · smoke test ตก |
| เปลี่ยนแมพไปแมพที่ stem น้อยกว่า | source ที่เกินปิดเงียบ ไม่ถูกทำลาย |
| `mainBossPhases` สั้นกว่าจำนวนเฟส | ใช้ช่องสุดท้ายที่มี |
| Play Again (ฉากโหลดใหม่) | `MusicDirector` เกิดใหม่ · player อยู่ข้ามฉาก → กลับไป mix ของนาที 0 ด้วย fade ไม่เริ่มเพลงใหม่ถ้าเป็น track เดิม |

**การตรวจ** (batchmode ไม่มีเสียง):
- `P3RSmokeTest`: ทุก profile ที่แมพอ้าง → stem ยาวเท่ากัน · sample rate เท่ากัน · band เรียงนาที · ไม่มีคลิปว่าง
- DevTools (F1): แถว "Music" แสดงแถบระดับของแต่ละ stem แบบสด + ปุ่มกระโดดนาที / เรียกมินิบอส / บังคับเฟส
- `ResolveTargetLevels()` เป็นฟังก์ชันบริสุทธิ์จากข้อเท็จจริง → เขียน EditMode test ได้

---

## 6 · Trade-offs ที่ยอมรับ

| เลือก | แทน | เพราะ |
|---|---|---|
| คำนวณบน client | server ส่ง RPC | ไม่มีทราฟฟิก · เข้ากลางเกมได้ mix ถูก |
| Vertical layering | สลับเพลงทั้งเพลง | ตรงกับแนวคิดเจ้าของ · ไม่มีรอยต่อ · เพลงเดินต่อตลอด |
| เล่นทุกชั้นตลอด (บางชั้นเงียบ) | เปิด/ปิด source ตามต้องการ | ไม่มีทางหลุดจังหวะ · แลกกับ CPU ถอดรหัส N ชั้นตลอดเวลา (6 ชั้น Vorbis ยังเบามาก) |
| Compressed In Memory | Streaming | จังหวะเริ่มแม่น · แลกกับ RAM (ประมาณ 6 ชั้น × 3 นาที ≈ 15–20 MB) |
| อ้าง stem ด้วยชื่อ | เลขลำดับ (`float[]`) | แต่ละแมพมี stem ไม่เท่ากัน และเพิ่มทีหลังได้โดย mix เดิมไม่เลื่อน · แลกกับต้องมี drawer + ตัวตรวจชื่อ |
| overlay แบบค่ามากกว่า | บวกกัน | ไม่มีชั้นไหนดังเกิน 1 · มินิบอสไม่ลดชั้นที่เปิดอยู่แล้ว |

## 7 · สิ่งที่จะกลับมาดูเมื่อระบบโต

1. **เปิดชั้นตรงห้อง** (ข้อ 4.4) — data รองรับแล้ว
2. **ความตึงจากสถานการณ์จริง** นอกจากเวลา — จำนวนศัตรูบนจอ · HP ของทีม → เป็นแค่อีก overlay หนึ่งในสูตรเดิม
3. **เฟสบอสเป็น NetworkVariable** — ทำพร้อม reconnect
4. ถ้ามีหลายแมพหลายเพลง → โหลด `LayeredTrack` ผ่าน Addressables ตอนเข้าแมพ แทนการโหลดทุกเพลงพร้อมกัน

## 8 · ประมาณงาน

| ขั้น | ไฟล์ | ขนาด |
|---|---|---|
| 1 · `LayeredTrack` + `MusicProfile` + `[StemId]` drawer | 3 | เล็ก–กลาง |
| 2 · `LayeredMusicPlayer` + ต่อเข้า SoundManager + แก้ `RescalePlaying` + duck | 2 | **กลาง–ใหญ่** (ส่วนที่ยากที่สุด) |
| 3 · `MusicDirector` + event เฟสใน `BossController` + ช่องใน `MapData` | 3 | กลาง |
| 4 · DevTools แถว Music + smoke test | 2 | เล็ก |
| 5 · import stem + ทำ profile ของ Arena01 | asset | ขึ้นกับเพลง |

## 9 · คำถามที่ยังค้าง

ไม่มีคำถามค้าง — พร้อมลงมือ

**ตัดสินแล้ว**
- เพลง **ยังไม่ได้แยก stem** → โค้ดต้องทำงานได้กับ track ที่มี stem เดียว (= เพลงเต็ม) ตั้งแต่วันแรก · ระหว่างรอ ทดสอบการซ้อนชั้นด้วย stem ชั่วคราวที่แยกจากเพลงเดิมด้วยเครื่องมือแยกเสียงอัตโนมัติ (เช่น Demucs → drums / bass / other) · ของจริงมาเมื่อไรแค่เปลี่ยนคลิปใน asset (2026-09-23)
- บอสใหญ่ **ใช้ระบบเดียวกัน อาจมีธีมแยก** — ธีมบอสคือ `LayeredTrack` อีกตัว (มักเป็นเพลงเดิมในเวอร์ชันที่ครบทุก stem) ใส่ใน `mainBossTrack` · **ว่าง = เพลงเดิมเล่นต่อ ไม่เร่งชั้น** (2026-09-23)
- จำนวน stem **ไม่เท่ากันในแต่ละแมพ** → mix อ้าง stem ด้วยชื่อ (2026-09-23)
- ~~จอ Level Up / Pause~~ → Level Up: เปิด/ปิดชั้นได้ + ลดเสียง · Pause: ไม่ทำอะไร (2026-09-23)
- จอเลือกการ์ดจาก **Orb** ใช้ค่าชุดเดียวกับ Level Up (เป็นจอเลือกการ์ดแบบเดียวกัน และจบด้วย event ตัวเดียวกัน) · ถ้าอยากแยกค่า เพิ่มช่องที่สองได้ภายหลัง
