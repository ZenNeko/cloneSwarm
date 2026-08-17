using UnityEngine;

/// <summary>
/// ตัวจัดหมวดหมู่ VFXAsset — ใช้จัดกลุ่มตอนเลือกใน Editor และ (ในอนาคต) โหลดเฉพาะ
/// หมวดที่บอสตัวนั้นต้องใช้ แทนการ allocate ทั้งเกม (ADR-006 ข้อ 1)
/// </summary>
public enum VFXCategory
{
    UI,
    Boss,
    Enemy,
    Weapon,
    Environment,
}

/// <summary>
/// VFX ตัวละไฟล์ ScriptableObject — ยกมาจาก VFXDatabase.VFXEntry ทีละช่อง (ADR-006 Action Item 1)
/// ลากใส่ตรงๆ แทนพิมพ์ string key ที่พังเงียบเมื่อพิมพ์ผิด (ไม่มี error ไม่มีอะไรเกิดขึ้น)
///
/// id ที่เสถียรข้ามเครื่องมาจาก "index ในลิสต์" ของ VFXDatabase.assets เท่านั้น —
/// ห้ามใช้ GetInstanceID() เพราะไม่ตรงกันข้ามเครื่อง (ดู ADR-006 §2 / ADR-007 §4)
/// </summary>
[CreateAssetMenu(fileName = "VFXAsset", menuName = "LoL Swarm/VFX Asset")]
public class VFXAsset : ScriptableObject
{
    [Tooltip("Prefab ที่มี ParticleSystem หรือ VFX Effect")]
    public GameObject prefab;

    [Min(1), Tooltip("จำนวน pre-allocate ต่อ client")]
    public int poolSize = 5;

    [Tooltip("radius ที่ prefab ถูกออกแบบมา\n" +
             "0 = fixed size ไม่ scale\n" +
             "ใช้เพื่อ scale VFX ตาม stat จริง")]
    public float designedRadius = 0f;

    [Tooltip("ระยะเวลา VFX (วินาที) — ใช้กับ VFX Graph ที่ไม่มี ParticleSystem\n" +
             "0 = auto detect จาก ParticleSystem duration")]
    public float fixedDuration = 0f;

    [Tooltip("หมวดหมู่ — ใช้จัดกลุ่มตอนเลือกใน Editor และในอนาคตใช้โหลดเฉพาะหมวดที่บอสตัวนั้นต้องใช้\n" +
             "แทนการ allocate ทั้งเกม (ADR-006)")]
    public VFXCategory category = VFXCategory.Weapon;
}
