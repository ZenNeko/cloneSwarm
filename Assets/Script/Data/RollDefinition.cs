using System;

public enum RollKind { Variant, Anchor, SnapAngle, Target, MirrorX, MirrorZ, Order }

[Serializable]
public class RollDefinition
{
    public string   rollName = "roll";   // คลิปอ้างด้วยชื่อนี้
    public RollKind kind = RollKind.Variant;
    public int      optionCount = 2;     // จำนวนตัวเลือก
    public bool     excludePrevious = false;
}
