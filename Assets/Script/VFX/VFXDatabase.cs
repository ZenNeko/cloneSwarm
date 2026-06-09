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

    [Tooltip("รายชื่อ VFX ทั้งหมดในเกม")]
    public List<VFXEntry> entries = new();
}
