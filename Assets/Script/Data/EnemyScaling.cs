using System;
using UnityEngine;

/// <summary>
/// ศัตรูแรงขึ้นเท่าไรต่อหนึ่ง wave
///
/// ═══ ทำไมต้องมีสวิตช์ ไม่ใช่ "0 = ไม่ override" ═══
///
/// ตารางเวลาใช้กติกา "ว่าง = ใช้ของซีน" ได้เพราะลิสต์ว่างไม่ใช่ค่าที่ใครตั้งใจตั้ง ·
/// แต่ตัวเลขสเกลเป็นคนละเรื่อง — <c>healthMultPerWave = 0</c> แปลว่า "ศัตรูไม่แรงขึ้น
/// เลยทั้งเกม" ซึ่งเป็นการออกแบบที่ถูกต้องสำหรับโหมดฝึกซ้อม
///
/// ถ้าใช้ 0 เป็นสัญญาณว่า "ไม่ได้ตั้ง" แมพที่ตั้งใจให้สเกลเป็น 0 จะถูกมองว่าไม่ได้ตั้ง
/// แล้วโดนแทนที่ด้วยค่าของซีนเงียบๆ · สวิตช์แยกจึงจำเป็น ไม่ใช่ของฟุ่มเฟือย
///
/// ═══ ค่าเริ่มต้นไม่ตรงกับค่าในซีน และนั่นตั้งใจ ═══
///
/// ค่าที่นี่เป็นค่าเริ่มต้นของ**คลาส** ส่วนซีนถูกจูนไว้คนละชุด · ตัวคัดลอก
/// (Tools > Clone Swarm > Copy Scene Timeline → MapData) ดึงค่าจริงจากซีนมาใส่ให้
/// เพื่อไม่ให้การเปิดสวิตช์กลายเป็นการเปลี่ยนความยากโดยไม่ตั้งใจ
/// </summary>
[Serializable]
public class EnemyScaling
{
    [Tooltip("ปิด = ใช้ค่าในซีน (WaveManager) · เปิด = แมพ/tier นี้ตั้งเอง")]
    public bool enabled = false;

    [Tooltip("HP ของศัตรูเพิ่มขึ้นกี่ % ต่อ wave (ทศนิยม) · 0.20 = +20% ต่อ wave")]
    [Min(0f)] public float healthMultPerWave = 0.20f;

    [Tooltip("ความเร็วศัตรูเพิ่มขึ้นกี่ % ต่อ wave · จำกัดด้วย maxSpeedMultiplier")]
    [Min(0f)] public float speedMultPerWave = 0.05f;

    [Tooltip("EXP reward เพิ่มขึ้นกี่ % ต่อ wave — ชดเชย scaling ของศัตรู")]
    [Min(0f)] public float expMultPerWave = 0.15f;

    // ต่ำกว่า 1 = ศัตรูช้ากว่าความเร็วพื้นฐานตั้งแต่ wave แรก เพราะเพดานนี้ครอบ
    // ค่าที่เริ่มจาก 1.0 เสมอ — เกือบทุกครั้งที่เห็นค่าต่ำกว่า 1 คือพิมพ์ผิด
    [Tooltip("เพดาน speed multiplier · ต้องไม่ต่ำกว่า 1 ไม่งั้นศัตรูช้ากว่าปกติตั้งแต่ต้นเกม")]
    [Min(0f)] public float maxSpeedMultiplier = 2f;

    [Tooltip("Spawn rate เร็วขึ้นกี่ % ต่อ wave (ทศนิยม) · 0.10 = interval สั้นลง 10% ต่อ wave")]
    [Range(0f, 0.9f)] public float spawnRateAccel = 0.10f;

    /// <summary>สำเนาใหม่ — กันสองที่แชร์ instance เดียวกันแล้วแก้ที่หนึ่งไปโดนอีกที่</summary>
    public EnemyScaling Clone() => new EnemyScaling
    {
        enabled            = enabled,
        healthMultPerWave  = healthMultPerWave,
        speedMultPerWave   = speedMultPerWave,
        expMultPerWave     = expMultPerWave,
        maxSpeedMultiplier = maxSpeedMultiplier,
        spawnRateAccel     = spawnRateAccel,
    };

    public override string ToString()
        => $"HP+{healthMultPerWave:P0} SPD+{speedMultPerWave:P0} (เพดาน ×{maxSpeedMultiplier:0.#}) " +
           $"EXP+{expMultPerWave:P0} rate-{spawnRateAccel:P0}";
}
