/// <summary>
/// โหมดของ BossTether — ดู docs/adr-002-tether-modes.md
///
/// ทุกโหมดใช้ component เดียวกัน (BossTether) prefab เดียวกัน และ TetherAction ตัวเดียวกัน
/// ส่วนที่ใช้ร่วมกัน (เลือกเป้า · วาดสาย · จับเวลา · ประกาศ HUD · despawn) เขียนครั้งเดียว
/// แล้ว switch เฉพาะตอน resolve
/// </summary>
public enum TetherMode
{
    /// <summary>วิ่งออกจากกันให้ถึงระยะ สายจะขาด · ไม่ทันหมดเวลา = กินดาเมจทั้งคู่</summary>
    Far,
    /// <summary>อยู่ใกล้กันไว้ · หมดเวลาแล้วห่างกัน = กินดาเมจทั้งคู่</summary>
    Close,
    /// <summary>ห้ามออกนอกรัศมีจาก anchor · ออกไปจะถูกดึงกลับ ไม่มีดาเมจ</summary>
    Leash,
    /// <summary>
    /// ส่งต่อสายให้คนที่เดินตัดผ่าน — **ยังไม่ได้ทำ**
    /// เลือกแล้ว BossTether จะ warn แล้ว fallback เป็น Far
    /// </summary>
    Transferable,
}

/// <summary>บทลงโทษของ TetherMode.Close เมื่อสองคนห่างกันเกินระยะ</summary>
public enum CloseFailMode
{
    /// <summary>เช็คตอนหมดเวลาอย่างเดียว — ระหว่างทางแยกกันได้เพื่อหลบ AoE</summary>
    OnTimeout,
    /// <summary>
    /// กินดาเมจต่อเนื่องตลอดเวลาที่ห่าง — **ยังไม่ได้ทำ**
    /// ต้องตัดสินใจเรื่องระบบ DOT ก่อน (ดู ADR-002 Decision 4)
    /// </summary>
    Continuous,
    /// <summary>ห่างเมื่อไหร่แตกทันที กินดาเมจก้อนเดียว — **ยังไม่ได้ทำ**</summary>
    Instant,
}

/// <summary>TetherMode.Leash ใน co-op โดนกี่คน และ anchor อยู่ไหน</summary>
public enum LeashScope
{
    /// <summary>ทุกคนโดน แต่ละคนมีเสาของตัวเองที่ตำแหน่งตอนร่าย — บังคับให้ทุกคนแยกกันอยู่โซนตัวเอง</summary>
    EachPlayerOwnAnchor,
    /// <summary>สุ่มคนเดียว คนอื่นเล่นต่อได้อิสระ</summary>
    SingleRandom,
    /// <summary>ทุกคนโดน แต่ใช้เสาร่วมต้นเดียวที่ตัวบอส — บังคับให้ทีมอยู่กองกัน</summary>
    AllSharedAnchor,
}
