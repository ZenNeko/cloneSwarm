using UnityEngine;

public enum ArenaAnchor
{
    Center,
    N, E, S, W,                       // cardinal
    NE, SE, SW, NW,                   // intercardinal
    QuadrantNE, QuadrantSE, QuadrantSW, QuadrantNW,   // กลางของแต่ละควอดแรนต์
    Clock1, Clock2, Clock3, Clock4, Clock5, Clock6,
    Clock7, Clock8, Clock9, Clock10, Clock11, Clock12
}

public static class ArenaAnchors
{
    /// <summary>
    /// คืนพิกัดโลกของ anchor · distanceScale 1 = ขอบสนาม · 0.5 = ครึ่งทาง
    /// </summary>
    public static Vector3 Resolve(ArenaDefinition arena, ArenaAnchor anchor, float distanceScale = 1f)
    {
        Vector3 center = arena != null ? arena.center : Vector3.zero;
        if (anchor == ArenaAnchor.Center) return center;

        float radius = arena != null ? arena.radius : 20f;
        ArenaShape shape = arena != null ? arena.shape : ArenaShape.Circle;

        Vector3 dir = GetDirectionVector(anchor);

        Vector3 offset;
        if (shape == ArenaShape.Square)
        {
            float absX = Mathf.Abs(dir.x);
            float absZ = Mathf.Abs(dir.z);
            float scaleFactor = radius;
            if (absX > 0.0001f || absZ > 0.0001f)
            {
                float maxComp = Mathf.Max(absX, absZ);
                scaleFactor = radius / maxComp;
            }
            offset = dir * scaleFactor * distanceScale;
        }
        else
        {
            offset = dir * radius * distanceScale;
        }

        return center + offset;
    }

    private static Vector3 GetDirectionVector(ArenaAnchor anchor)
    {
        switch (anchor)
        {
            case ArenaAnchor.N:  return new Vector3(0f, 0f, 1f);
            case ArenaAnchor.E:  return new Vector3(1f, 0f, 0f);
            case ArenaAnchor.S:  return new Vector3(0f, 0f, -1f);
            case ArenaAnchor.W:  return new Vector3(-1f, 0f, 0f);

            case ArenaAnchor.NE:
            case ArenaAnchor.QuadrantNE:
                return new Vector3(1f, 0f, 1f).normalized;

            case ArenaAnchor.SE:
            case ArenaAnchor.QuadrantSE:
                return new Vector3(1f, 0f, -1f).normalized;

            case ArenaAnchor.SW:
            case ArenaAnchor.QuadrantSW:
                return new Vector3(-1f, 0f, -1f).normalized;

            case ArenaAnchor.NW:
            case ArenaAnchor.QuadrantNW:
                return new Vector3(-1f, 0f, 1f).normalized;

            case ArenaAnchor.Clock1:  return GetClockVector(1);
            case ArenaAnchor.Clock2:  return GetClockVector(2);
            case ArenaAnchor.Clock3:  return GetClockVector(3);
            case ArenaAnchor.Clock4:  return GetClockVector(4);
            case ArenaAnchor.Clock5:  return GetClockVector(5);
            case ArenaAnchor.Clock6:  return GetClockVector(6);
            case ArenaAnchor.Clock7:  return GetClockVector(7);
            case ArenaAnchor.Clock8:  return GetClockVector(8);
            case ArenaAnchor.Clock9:  return GetClockVector(9);
            case ArenaAnchor.Clock10: return GetClockVector(10);
            case ArenaAnchor.Clock11: return GetClockVector(11);
            case ArenaAnchor.Clock12: return GetClockVector(12);

            default:
                return Vector3.zero;
        }
    }

    private static Vector3 GetClockVector(int hour)
    {
        float angleDeg = hour * 30f;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));
    }
}
