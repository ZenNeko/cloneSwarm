using UnityEngine;

/// <summary>
/// การแปลงพิกัดที่ได้จาก roll — ดู docs/adr-003-ffxiv-rns-mechanic-gaps.md
///
/// `RollKind` 4 ใน 7 ตัว (`Anchor` `SnapAngle` `MirrorX` `MirrorZ`) เป็นการแปลงพิกัดล้วนๆ
/// จึงทำเป็นชั้นกลางที่ `SpawnAoEActionBase` ใช้ครั้งเดียว แทนที่จะให้ทุก subclass ตีความเอง
///
/// **สำคัญ: roll ครั้งเดียวต่อการยิงท่า 1 ครั้ง แล้วใช้ transform ตัวเดียวกันกับทุกจุดเกิด**
/// ถ้าแปลงแยกทีละจุด แพตเทิร์นแบบ AllPlayers จะกลายเป็นมั่วแทนที่จะเป็นลวดลายที่ผู้เล่นอ่านออก
/// </summary>
public struct RollTransform
{
    public Vector3 pivot;
    public float   angleDeg;
    public bool    mirrorX;
    public bool    mirrorZ;

    public static RollTransform Identity => new RollTransform();

    /// <summary>true ถ้าไม่ได้แปลงอะไรเลย — ข้ามการคำนวณได้</summary>
    public bool IsIdentity => !mirrorX && !mirrorZ && Mathf.Abs(angleDeg) < 0.001f;

    public Vector3 Apply(Vector3 world)
    {
        if (IsIdentity) return world;

        Vector3 local = world - pivot;
        if (mirrorX) local.x = -local.x;
        if (mirrorZ) local.z = -local.z;
        if (Mathf.Abs(angleDeg) > 0.001f) local = Quaternion.Euler(0f, angleDeg, 0f) * local;
        return pivot + local;
    }

    /// <summary>แปลงทิศการหันให้สอดคล้องกับตำแหน่ง — ไม่งั้น Line จะพาดผิดทางหลังพลิก</summary>
    public Quaternion Apply(Quaternion rot)
    {
        if (IsIdentity) return rot;

        Vector3 fwd = rot * Vector3.forward;
        if (mirrorX) fwd.x = -fwd.x;
        if (mirrorZ) fwd.z = -fwd.z;
        if (Mathf.Abs(angleDeg) > 0.001f) fwd = Quaternion.Euler(0f, angleDeg, 0f) * fwd;

        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd) : rot;
    }
}
