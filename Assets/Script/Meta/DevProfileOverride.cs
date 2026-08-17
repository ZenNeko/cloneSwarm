using UnityEngine;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// สวิตช์ dev สำหรับตั้งทองโดยไม่ต้องไปแก้ profile.json เอง
    ///
    /// ตอนนี้อยู่ที่ Assets/ScriptableObjects/Resources/DevProfileOverride.asset
    /// (Resources.Load หาโฟลเดอร์ชื่อ Resources ที่ไหนก็ได้ใต้ Assets — ชื่อ**ไฟล์**ต้องตรงเท่านั้น)
    /// SaveManager อ่านตอนโหลดโปรไฟล์ — **ทำงานเฉพาะใน Editor เท่านั้น**
    ///
    /// ซื้อทุกอย่างในเกมหมดใช้ 78,690g (talent 77,590 + ปลดล็อกตัวละคร 1,100 เมื่อ 2026-08-13)
    /// ค่า default 10000 ด้านล่างจึงได้แค่ราวหนึ่งในแปดของ tree
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
