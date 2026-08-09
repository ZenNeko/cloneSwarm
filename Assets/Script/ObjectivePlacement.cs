using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สุ่มตำแหน่งวางของ objective (FetchItem / Destructible) รอบจุดศูนย์กลาง
///
/// โปรเจกต์นี้ไม่มี NavMesh — validate ด้วย physics แทน:
///   1. สุ่มจุดในวงแหวน [minRadius, maxRadius] แบบกระจายเท่ากันต่อพื้นที่
///      (r = sqrt(lerp(min², max², u)) — ถ้าใช้ lerp ตรงๆ ของจะกระจุกด้านใน)
///   2. snap Y ลงพื้นด้วย raycast ลง layer "Floor"
///   3. ทิ้งจุดที่ทับ layer "Wall"
///   4. ทิ้งจุดที่ใกล้จุดที่เลือกไปแล้วเกิน minSpacing
///
/// ถ้าหาไม่ครบใน pass แรก จะ relax เงื่อนไข minSpacing ให้ก่อน (ยังเลี่ยงกำแพงอยู่)
/// แล้วค่อยยอมคืนของไม่ครบ — caller ต้องอ่านจำนวนที่ได้จริงจาก list เสมอ
/// </summary>
public static class ObjectivePlacement
{
    const int LAYER_FLOOR = 6;
    const int LAYER_WALL  = 7;

    public static readonly LayerMask GroundMask   = 1 << LAYER_FLOOR;
    public static readonly LayerMask BlockerMask  = 1 << LAYER_WALL;

    /// <summary>ความสูงที่ยิง raycast ลงหาพื้น</summary>
    const float PROBE_HEIGHT = 20f;

    /// <summary>
    /// สุ่ม <paramref name="count"/> ตำแหน่งในวงแหวนรอบ <paramref name="center"/>
    /// </summary>
    /// <param name="minSpacing">ระยะห่างขั้นต่ำระหว่างของแต่ละชิ้น</param>
    /// <param name="clearRadius">รัศมีที่ต้องว่างจากกำแพง</param>
    public static List<Vector3> SampleRing(
        Vector3 center,
        int     count,
        float   minRadius,
        float   maxRadius,
        float   minSpacing       = 3f,
        float   clearRadius      = 0.6f,
        int     attemptsPerPoint = 24)
    {
        var result = new List<Vector3>(Mathf.Max(0, count));
        if (count <= 0) return result;

        minRadius = Mathf.Max(0f, minRadius);
        maxRadius = Mathf.Max(maxRadius, minRadius + 0.01f);

        float minSpacingSq = minSpacing * minSpacing;

        for (int i = 0; i < count; i++)
        {
            if (TryFindPoint(center, minRadius, maxRadius, clearRadius,
                             result, minSpacingSq, attemptsPerPoint, out var p))
            {
                result.Add(p);
                continue;
            }

            // pass 2 — ยอมให้ของอยู่ชิดกัน แต่ยังต้องไม่จมกำแพง
            if (TryFindPoint(center, minRadius, maxRadius, clearRadius,
                             result, 0f, attemptsPerPoint, out p))
            {
                result.Add(p);
                continue;
            }

            Debug.LogWarning($"[ObjectivePlacement] หาที่ว่างได้แค่ {result.Count}/{count} " +
                             $"รอบ {center} (ring {minRadius:F1}-{maxRadius:F1}) — แผนที่แน่นเกินไป?");
            break;
        }

        return result;
    }

    static bool TryFindPoint(
        Vector3       center,
        float         minRadius,
        float         maxRadius,
        float         clearRadius,
        List<Vector3> taken,
        float         minSpacingSq,
        int           attempts,
        out Vector3   point)
    {
        for (int a = 0; a < attempts; a++)
        {
            // uniform-by-area sampling ในวงแหวน
            float u   = Random.value;
            float r   = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, u));
            float ang = Random.value * Mathf.PI * 2f;

            Vector3 p = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
            p = SnapToGround(p, center.y);

            // จมกำแพง / ในสิ่งกีดขวาง → ทิ้ง
            if (Physics.CheckSphere(p + Vector3.up * 0.5f, clearRadius,
                                    BlockerMask, QueryTriggerInteraction.Ignore))
                continue;

            if (minSpacingSq > 0f)
            {
                bool tooClose = false;
                for (int k = 0; k < taken.Count; k++)
                {
                    if ((taken[k] - p).sqrMagnitude < minSpacingSq) { tooClose = true; break; }
                }
                if (tooClose) continue;
            }

            point = p;
            return true;
        }

        point = default;
        return false;
    }

    /// <summary>ยิง raycast ลงหาพื้น — ไม่เจอก็ใช้ระดับ Y ของ center</summary>
    public static Vector3 SnapToGround(Vector3 p, float fallbackY)
    {
        var origin = new Vector3(p.x, fallbackY + PROBE_HEIGHT, p.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, PROBE_HEIGHT * 2f,
                            GroundMask, QueryTriggerInteraction.Ignore))
            return new Vector3(p.x, hit.point.y, p.z);

        return new Vector3(p.x, fallbackY, p.z);
    }
}
