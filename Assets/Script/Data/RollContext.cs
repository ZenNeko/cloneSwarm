using System;
using System.Collections.Generic;

public class RollContext
{
    private readonly System.Random _rng;
    private readonly Dictionary<string, RollDefinition> _defs = new();
    private readonly Dictionary<string, int> _lastValues = new();

    public int Seed { get; }

    public RollContext(int seed, RollDefinition[] defs)
    {
        Seed = seed;
        _rng = new System.Random(seed);

        if (defs != null)
        {
            foreach (var def in defs)
            {
                if (def != null && !string.IsNullOrEmpty(def.rollName))
                {
                    _defs[def.rollName] = def;
                }
            }
        }
    }

    /// <summary>
    /// สุ่มค่าใหม่ให้ roll ชื่อนี้แล้วคืนค่า · เคารพ excludePrevious
    /// </summary>
    public int Roll(string rollName)
    {
        if (string.IsNullOrEmpty(rollName)) return 0;

        _defs.TryGetValue(rollName, out var def);
        int count = def != null ? Math.Max(1, def.optionCount) : 2;
        bool excludePrev = def != null && def.excludePrevious;

        int prev = _lastValues.TryGetValue(rollName, out int last) ? last : -1;
        int next;

        if (count <= 1 || !excludePrev || prev < 0)
        {
            next = _rng.Next(0, count);
        }
        else
        {
            next = _rng.Next(0, count);
            if (next == prev)
            {
                next = (next + 1 + _rng.Next(0, count - 1)) % count;
            }
        }

        _lastValues[rollName] = next;
        return next;
    }

    /// <summary>
    /// อ่านค่าล่าสุดโดยไม่สุ่มใหม่ · -1 ถ้ายังไม่เคยสุ่ม
    /// </summary>
    public int Peek(string rollName)
    {
        if (!string.IsNullOrEmpty(rollName) && _lastValues.TryGetValue(rollName, out int val))
        {
            return val;
        }
        return -1;
    }

    /// <summary>
    /// ล้างประวัติการ roll ทั้งหมด
    /// </summary>
    public void ResetAll()
    {
        _lastValues.Clear();
    }
}
