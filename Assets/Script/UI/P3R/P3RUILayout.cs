using System;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ตำแหน่ง/ขนาดของชิ้นส่วน UI ที่ **จัดไว้ด้วยมือในซีน** แล้วเก็บกลับมาเป็นข้อมูล
    ///
    /// ═══ ปัญหาที่ของนี้แก้ ═══
    ///
    /// จอทุกจอในโปรเจกต์สร้างด้วย builder ซึ่งดีตรงที่ rebuild ได้และ diff อ่านรู้เรื่อง
    /// แต่แปลว่า **ทุกอย่างที่ขยับด้วยมือใน Editor จะหายทันทีที่ rebuild** — ไม่มีคำเตือน
    /// ไม่มี error แค่กลับไปเป็นตัวเลขในโค้ดเหมือนไม่เคยมีใครแตะ
    ///
    /// ผลคือคนจัดหน้าจอต้องเลือกอย่างใดอย่างหนึ่ง: จัดใน Editor แล้วห้ามใครกด rebuild
    /// หรือไปแก้ตัวเลขในโค้ดโดยไม่เห็นภาพ · ทั้งสองทางแย่
    ///
    /// asset นี้เป็นทางที่สาม — จัดใน Editor ให้พอใจ แล้ว "ดูด" ค่ากลับมาเก็บไว้
    /// builder ยังเป็นเจ้าของ **โครงสร้าง** (มีอะไรบ้าง ชื่ออะไร ต่อสายกับใคร)
    /// ส่วนไฟล์นี้เป็นเจ้าของ **ตำแหน่ง** ของชิ้นที่ถูกดูดไว้
    ///
    /// ═══ ข้อควรระวังที่ต้องรู้ก่อนใช้ ═══
    ///
    /// ชิ้นไหนถูกดูดไว้ที่นี่แล้ว **ค่าที่ builder เขียนจะไม่มีผลอีก** — แก้ตัวเลขใน builder
    /// แล้วจอไม่เปลี่ยนคืออาการปกติของชิ้นที่ถูก override ไม่ใช่บั๊ก
    /// ถ้าอยากให้ builder คุมกลับ ให้ลบ entry นั้นทิ้งจาก asset
    ///
    /// ═══ ทำไมไม่ดูดทั้งต้นไม้ ═══
    ///
    /// ช่องในแถบ build ถูก **สร้างตอนรัน** โดย `BuildStripUI.SpawnRow` แล้ววางตำแหน่ง
    /// จาก `slotSize`/`slotGap` ในโค้ด · ดูดตำแหน่งช่องพวกนั้นไว้ = เก็บค่าที่ไม่มีใครใช้
    /// และหลอกคนอ่านว่าแก้ที่นี่แล้วจะมีผล · <see cref="capturedDepth"/> จึงมีไว้กันชั้นลึก
    /// </summary>
    [CreateAssetMenu(fileName = "P3RUILayout", menuName = "LoL Swarm/UI/P3R UI Layout")]
    public class P3RUILayout : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("เส้นทางจาก panel ลงมา เช่น 'BuildStrip/Row_Weapons/SlotArea'")]
            public string path;

            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;

            [Tooltip("สถานะเปิด/ปิดของ GameObject ตอนที่ดูดมา")]
            public bool activeSelf = true;
        }

        [Header("── ดูดมาจากไหน ──────────────────────")]
        [Tooltip("ซีนต้นทางตอนที่ดูด — ไว้ให้คนอ่านรู้ว่าค่ามาจากไหน ไม่ได้ใช้ตอน apply")]
        public string sourceScene;
        [Tooltip("ชื่อ panel ที่เป็นรากของทุก path ในลิสต์")]
        public string panelName;
        [Tooltip("ดูดลึกกี่ชั้นจาก panel · 3 = ลูก/หลาน/เหลน ซึ่งครอบ 'บล็อก' ที่คนขยับจริง")]
        public int    capturedDepth = 3;
        [Tooltip("เวลาที่ดูดล่าสุด — ไว้เทียบกับ git log ว่าใครจัดเมื่อไร")]
        public string capturedAt;

        [Header("── ค่าที่เก็บไว้ ─────────────────────")]
        public Entry[] entries = Array.Empty<Entry>();
    }
}
