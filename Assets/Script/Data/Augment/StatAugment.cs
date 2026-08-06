using System;
using UnityEngine;

/// <summary>
/// Augment ที่ให้โบนัสสเตตัสก้อนใหญ่ — Assets &gt; Create &gt; LoL Swarm/Augment/Stat Augment
///
/// ตัวอย่าง "Glass Cannon": Damage +0.40, MaxHealth -30
/// (ใส่ค่าติดลบได้ ใช้ทำ augment ที่มีข้อเสีย)
/// </summary>
[CreateAssetMenu(fileName = "Aug_Stat_New", menuName = "LoL Swarm/Augment/Stat Augment")]
public class StatAugment : AugmentData
{
    [Serializable]
    public struct StatBonus
    {
        public StatType type;
        [Tooltip("% → decimal (0.40 = +40%)  ·  flat → ใส่ตรงๆ  ·  ติดลบได้")]
        public float    value;
    }

    [Header("Bonuses")]
    public StatBonus[] bonuses = Array.Empty<StatBonus>();

    public override void OnAcquire(PlayerAugmentManager ctx)
    {
        if (ctx == null || bonuses == null) return;

        foreach (var b in bonuses)
            ctx.Stats?.AddPermanentBonus(b.type, b.value, ctx.Move);
    }
}
