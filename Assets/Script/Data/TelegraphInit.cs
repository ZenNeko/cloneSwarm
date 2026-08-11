using Unity.Collections;
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

    /// <summary>key ใน VFXDatabase สำหรับ VFX ตอนระเบิด — ว่าง = ใช้ detonateVfxPrefab บน prefab</summary>
    public FixedString32Bytes detonateVfxKey;

    /// <summary>สีที่ action สั่งมาต่อท่า — false = ใช้ palette บน TelegraphZone prefab</summary>
    public bool         overrideColors;
    public UnityEngine.Color warningColor;
    public UnityEngine.Color dangerColor;

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
        s.SerializeValue(ref detonateVfxKey);
        s.SerializeValue(ref overrideColors);
        s.SerializeValue(ref warningColor);
        s.SerializeValue(ref dangerColor);
    }
}
