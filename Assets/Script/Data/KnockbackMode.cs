/// <summary>
/// ทิศทางการผลักผู้เล่นของ TelegraphZone
///
/// ระยะผลักตั้งเป็น "หน่วยระยะทาง" (knockbackDistance) ไม่ใช่แรง —
/// TelegraphZone แปลงเป็นความเร็วให้เอง (speed = distance ÷ duration)
/// designer จึงกะได้ตรงๆ ว่าผู้เล่นจะถูกผลักไปไกลกี่หน่วย
/// </summary>
public enum KnockbackMode
{
    /// <summary>ออกจากจุดศูนย์กลางของโซน (พฤติกรรมเดิม)</summary>
    FromCenter,
    /// <summary>ออกจากตัวบอสผู้ปล่อยท่า (ต้องตั้ง followCaster หรือมี casterNetworkObject)</summary>
    FromCaster,
    /// <summary>ดูดเข้าหาจุดศูนย์กลางของโซน</summary>
    TowardCenter,
    /// <summary>ทิศคงที่ในพิกัดโลก — ตั้งค่าที่ knockbackFixedDirection</summary>
    FixedDirection,
    /// <summary>ตามแนวเส้นของโซน (แกน +Z ของ zone) — ใช้กับ Line / Cross</summary>
    AlongLine,
}
