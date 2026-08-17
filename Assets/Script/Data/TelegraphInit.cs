using Unity.Netcode;

/// <summary>
/// พารามิเตอร์ทั้งหมดที่ server ส่งให้ client ตอน TelegraphZone เกิด — ดู docs/adr-003-ffxiv-rns-mechanic-gaps.md
///
/// เดิมเป็นพารามิเตอร์เรียงกัน 12 ตัวใน InitClientRpc · พอเพิ่ม cone / hazard ที่ขยาย / VFX key
/// จะกลายเป็น 16+ ซึ่งเป็นจุดที่สลับลำดับ float สองตัวแล้ว**ไม่มีอะไรเตือน** compile ก็ผ่าน
/// หาไม่เจอจนกว่าจะเห็นวงขนาดผิดในเกม — ยุบเป็น struct เพื่อให้เพิ่ม field ทีหลังไม่ต้องแตะ signature
///
/// ค่าที่ไม่อยู่ในนี้คือค่าที่ **server ใช้ฝ่ายเดียว** (knockback ทั้งชุด) — client ไม่ต้องรู้
/// </summary>
public struct TelegraphInit : INetworkSerializable
{
    public int   aoeType;              // (int)AoEType — ส่งเป็น int ตามแบบเดิม
    public float radius;
    public float lineLength;
    public float lineWidth;
    public float warningDuration;
    public float damage;
    public float innerRadius;          // Donut: รัศมีวงในที่ปลอดภัย
    public ulong chaseTargetClientId;

    public float coneAngle;            // Cone: มุมกางทั้งหมด (องศา)
    public float scaleStart;           // ตัวคูณขนาดตอนเริ่ม — 1 = ขนาดเต็มตั้งแต่แรก
    public float scaleEnd;             // ตัวคูณขนาดตอนระเบิด
    public float sweepDegreesPerSecond;

    public bool isChasing;
    public bool isStackMarker;
    public bool isGaze;
    public bool isRotatingChase;

    /// <summary>id ใน VFXDatabase.assets สำหรับ VFX ตอนระเบิด (ADR-006) — -1 = ใช้ detonateVfxPrefab บน prefab
    /// เดิมเป็น FixedString32Bytes (32 ไบต์) ย้ายมาเป็น int (4 ไบต์) เพราะ id เสถียรข้ามเครื่องแล้ว
    /// (ดู VFXDatabase.assets / VFXAsset)</summary>
    public int detonateVfxId;

    /// <summary>สีที่ action สั่งมาต่อท่า — false = ใช้ palette บน TelegraphZone prefab</summary>
    public bool         overrideColors;
    public UnityEngine.Color warningColor;
    public UnityEngine.Color dangerColor;

    /// <summary>
    /// สีขอบแยกอิสระ — false = ขอบใช้สีอันตรายของหมวดเดียวกัน (พฤติกรรมเดิม)
    /// แยก gate ออกจาก overrideColors เพราะสองอย่างนี้คนละเจตนา:
    /// ทับสีพื้น = ท่าหลุดจากภาษาสีกลาง · ทับสีขอบ = แค่ให้ขอบเด่นขึ้นบนพื้นบางแบบ
    /// </summary>
    public bool         overrideOutlineColor;
    public UnityEngine.Color outlineColor;

    /// <summary>หน้าตาทั้งชุดที่ action สั่งมาต่อท่า — false = ใช้ค่าบน material ทั้งหมด</summary>
    public bool  overrideEffects;
    public float pulseAmount;      // จังหวะเต้นของ alpha
    public float blinkAmount;      // การกระพริบ
    public float ringAmount;       // ความสว่างของวงที่ไหลออก
    public float ringSpeed;        // ความเร็ววง (เมตร/วินาที)
    public float pulseSpeed;       // ความถี่ pulse (รอบ/วินาที)
    public float ringSpacing;      // ระยะห่างระหว่างวง (เมตร)
    public float ringWidth;        // ความหนาวง (เมตร)
    public float outlineWidth;     // ความหนาแถบขอบ (เมตร)
    public float edgeGlow;         // ความเรืองของขอบ
    public float edgeStrength;     // ความคมของขอบ
    public float baseAlpha;        // ความทึบรวมของ zone

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref aoeType);
        s.SerializeValue(ref radius);
        s.SerializeValue(ref lineLength);
        s.SerializeValue(ref lineWidth);
        s.SerializeValue(ref warningDuration);
        s.SerializeValue(ref damage);
        s.SerializeValue(ref innerRadius);
        s.SerializeValue(ref chaseTargetClientId);
        s.SerializeValue(ref coneAngle);
        s.SerializeValue(ref scaleStart);
        s.SerializeValue(ref scaleEnd);
        s.SerializeValue(ref sweepDegreesPerSecond);
        s.SerializeValue(ref isChasing);
        s.SerializeValue(ref isStackMarker);
        s.SerializeValue(ref isGaze);
        s.SerializeValue(ref isRotatingChase);
        s.SerializeValue(ref detonateVfxId);
        s.SerializeValue(ref overrideColors);
        s.SerializeValue(ref warningColor);
        s.SerializeValue(ref dangerColor);
        s.SerializeValue(ref overrideOutlineColor);
        s.SerializeValue(ref outlineColor);
        s.SerializeValue(ref overrideEffects);
        s.SerializeValue(ref pulseAmount);
        s.SerializeValue(ref blinkAmount);
        s.SerializeValue(ref ringAmount);
        s.SerializeValue(ref ringSpeed);
        s.SerializeValue(ref pulseSpeed);
        s.SerializeValue(ref ringSpacing);
        s.SerializeValue(ref ringWidth);
        s.SerializeValue(ref outlineWidth);
        s.SerializeValue(ref edgeGlow);
        s.SerializeValue(ref edgeStrength);
        s.SerializeValue(ref baseAlpha);
    }
}
