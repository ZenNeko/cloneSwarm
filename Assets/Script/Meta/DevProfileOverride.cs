using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// สวิตช์ dev สำหรับตั้งทองโดยไม่ต้องไปแก้ profile.json เอง
    ///
    /// วางไว้ที่ Assets/Resources/DevProfileOverride.asset (ชื่อไฟล์ต้องตรง)
    /// SaveManager อ่านตอนโหลดโปรไฟล์ — **ทำงานเฉพาะใน Editor เท่านั้น**
    ///
    /// คลาสนี้ไม่ได้ครอบ #if UNITY_EDITOR โดยตั้งใจ ถ้าครอบแล้ว asset จะกลายเป็น
    /// missing script ในบิลด์ — ตัวที่ครอบคือฝั่งที่เรียกใช้ใน SaveManager
    /// </summary>
    [CreateAssetMenu(menuName = "LoL Swarm/Dev/Profile Override", fileName = "DevProfileOverride")]
    public class DevProfileOverride : ScriptableObject
    {
        [Tooltip("ปิดไว้ = ไม่ทำอะไรเลย · เปิดแล้วค่าข้างล่างจะทับโปรไฟล์ตอนกด Play")]
        public bool applyOnPlay = false;

        [Tooltip("ทองที่จะตั้งให้ตอนโหลดโปรไฟล์")]
        public int gold = 10000;

        [Tooltip("ตั้ง lifetimeGold ตามไปด้วย — ปิดไว้ถ้าอยากเก็บสถิติสะสมของจริง")]
        public bool alsoSetLifetimeGold = true;
    }
}
