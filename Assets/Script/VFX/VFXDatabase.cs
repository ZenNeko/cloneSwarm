using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VFXDatabase", menuName = "LoL Swarm/VFX Database")]
public class VFXDatabase : ScriptableObject
{
    [System.Serializable]
    public class VFXEntry
    {
        public string key;
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
    }

    // ── LEGACY — กำลังถูก migrate ออกตาม ADR-006 ─────────────────────────────
    // key เป็น string ที่ designer พิมพ์เอง พิมพ์ผิด = ไม่มีอะไรเกิดขึ้น ไม่มี error
    // ยังใช้งานอยู่จริงโดย [VFXKey] dropdown บน WeaponBase / StickyRocketProjectile /
    // GiantRocketProjectile / BossPhase และ 7 call site จึง**ห้ามลบ**ในเฟสนี้
    // การลบ path นี้เป็นงานเฟสถัดไป (ADR-006 Action Item 4-5) ไม่ใช่เฟสนี้
    [Tooltip("รายชื่อ VFX ทั้งหมดในเกม")]
    public List<VFXEntry> entries = new();

    // ── ทางใหม่ (ADR-006) — VFXAsset ลากใส่ได้ + id เสถียรข้ามเครื่อง ──────────
    // id ของแต่ละ VFXAsset คือ "index ในลิสต์นี้" เท่านั้น — ทุก client ต้องมีลิสต์
    // เดียวกันในบิลด์ ห้ามใช้ GetInstanceID() (ดู ADR-006 §2 / ADR-007 §4)
    // ห้ามแทรก/ลบกลางลิสต์หลัง id ถูกอ้างอิงไปแล้ว (เช่นใน TelegraphInit ในเฟสถัดไป)
    // มิฉะนั้น id ทุกตัวหลังจุดนั้นจะขยับความหมาย — ต่อท้ายลิสต์เท่านั้นถ้าจะเพิ่ม
    [Header("VFXAsset (ADR-006) — id เสถียร: index ในลิสต์นี้คือ id")]
    [Tooltip("รายชื่อ VFXAsset ทั้งหมดในเกม — index ในลิสต์นี้คือ id ที่เสถียรข้ามเครื่อง\n" +
             "ห้ามลบ/แทรกกลางลิสต์หลังใช้งานจริงแล้ว ต่อท้ายลิสต์เท่านั้น\n" +
             "ใช้เมนู Tools/LoL Swarm/Validate VFX Database ตรวจช่องว่าง/รายการซ้ำ")]
    public List<VFXAsset> assets = new();

    /// <summary>คืน VFXAsset จาก id (null = id นอกขอบเขตหรือช่องว่าง)</summary>
    public VFXAsset GetAssetById(int id)
        => (assets != null && id >= 0 && id < assets.Count) ? assets[id] : null;

    /// <summary>คืน id ของ VFXAsset ตัวนี้ในลิสต์ (-1 = ไม่พบ)</summary>
    public int GetIdForAsset(VFXAsset asset)
        => (asset != null && assets != null) ? assets.IndexOf(asset) : -1;
}
