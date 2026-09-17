using System;
using UnityEngine;

/// <summary>
/// ชนิดของสิ่งที่นัดไว้บนไทม์ไลน์ — ตรงกับ event ที่ GameTimeline ยิงอยู่แล้ว
/// </summary>
public enum TimelineCueKind { ZoneObjective, MiniBoss }

/// <summary>
/// ช่องนี้เก็บ **ชื่อของแบบ** ที่ต้องมีอยู่จริงที่อื่น — ให้ Inspector เปลี่ยนจาก
/// ช่องพิมพ์เปล่าเป็นรายการให้เลือกแทน
///
/// ═══ ทำไมยังเป็นสตริง ═══
///
/// ตารางเวลาอยู่ได้ทั้งในซีนและใน MapData ซึ่งเป็น **asset** · asset อ้างถึง
/// component ในซีนไม่ได้เลย (Unity ไม่ให้ และถ้าให้ก็จะพังทุกครั้งที่เปลี่ยนซีน)
/// ชื่อจึงเป็นทางเดียวที่ทั้งสองที่ใช้ร่วมกันได้
///
/// ราคาของมันคือคอมไพเลอร์ตรวจให้ไม่ได้ · จ่ายคืนด้วยสามชั้น — ตัวเลือกใน
/// Inspector (ชั้นนี้) · คำเตือนตอนรันใน ObjectiveManager/BossManager ·
/// และสโมกเทสต์ที่ไล่ทุกตารางรวมของทุกแมพ
/// </summary>
public class VariantIdAttribute : PropertyAttribute { }

/// <summary>
/// นัดหมายหนึ่งรายการ — "ให้ของแบบนี้ เกิดที่นาทีเหล่านี้"
///
/// ═══ ทำไมอยู่นอก GameTimeline ═══
///
/// ตารางเวลาเป็น **เนื้อหาของแมพ** ไม่ใช่ค่าของซีน · MapData เก็บ wave กับ
/// bossConfig ไว้แล้ว การให้ตารางเวลาอยู่คนละที่แปลว่าจังหวะของแมพถูกนิยาม
/// สองแห่ง แล้ววันหนึ่งจะจูนแห่งเดียว
///
/// ชนิดนี้จึงเป็น type บนสุด ทั้งซีนและ ScriptableObject อ้างได้เหมือนกัน
/// </summary>
[Serializable]
public class TimelineCue
{
    [Tooltip("ชื่อไว้อ่านใน Inspector กับใน log — ไม่มีผลกับเกม")]
    public string label = "";

    public TimelineCueKind kind = TimelineCueKind.ZoneObjective;

    [Tooltip("นาทีที่ต้องเกิด · ใส่ได้หลายค่า เช่น 3, 8, 12")]
    public float[] atMinutes = new float[0];

    // ชื่อแบบที่ต้องการ — ต้องตรงกับ id ใน ObjectiveManager.zoneVariants
    // หรือชื่อ prefab ใน BossManager.miniBossPrefabs · ว่าง = สุ่มตามปกติ
    [Tooltip("ชื่อแบบที่ต้องการ (เช่น augment) · ว่าง = สุ่มตามปกติ")]
    [VariantId] public string variant = "";

    public string DisplayName => string.IsNullOrEmpty(label) ? kind.ToString() : label;

    public int TimeCount => atMinutes != null ? atMinutes.Length : 0;
}

/// <summary>
/// ตารางเวลาหนึ่งชุด — นัดหมายทั้งหมด + ความยาวรอบ
///
/// ═══ ไม่มีสถานะ "ยิงไปแล้ว" อยู่ในนี้ ═══
///
/// ตารางชุดหนึ่งอาจมาจาก **ScriptableObject** ซึ่งเป็นสินทรัพย์ที่อยู่ข้ามรัน ·
/// ถ้าจำว่ายิงไปแล้วไว้บนตัวนัดหมาย เล่นรอบแรกจบแล้วธงจะติดค้างอยู่บนไฟล์
/// asset ในเอดิเตอร์ แล้วรอบที่สองจะไม่มีอะไรเกิดขึ้นเลยทั้งเกม
///
/// สถานะการยิงจึงอยู่ที่ GameTimeline ซึ่งมีอายุเท่ากับหนึ่งรัน
/// </summary>
[Serializable]
public class TimelineSchedule
{
    [Tooltip("นัดหมายตามเวลาที่แน่นอน")]
    public TimelineCue[] cues = new TimelineCue[0];

    [Tooltip("ความยาวรอบ — นาทีที่บอสใหญ่ออก · 0 = ใช้ค่าในซีน ไม่ override")]
    [Min(0f)] public float mainBossMinutes = 0f;

    public bool HasCues => cues != null && cues.Length > 0;

    /// <summary>นับว่ามีนัดหมายชนิดนี้กี่ครั้งตลอดรัน — ใช้เตือนตอนตารางขาดทั้งชนิด</summary>
    public int CountTimes(TimelineCueKind kind)
    {
        if (cues == null) return 0;
        int n = 0;
        foreach (var c in cues)
            if (c != null && c.kind == kind) n += c.TimeCount;
        return n;
    }
}
