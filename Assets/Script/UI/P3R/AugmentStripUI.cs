using System.Collections.Generic;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถบ augment ที่ผู้เล่นถืออยู่ — ช่องจำนวนคงที่ ข้างไอคอนตัวละคร
    ///
    /// ═══ ใช้ช่องของแถบ build ไม่ทำช่องชนิดใหม่ ═══
    ///
    /// <see cref="BuildStripSlot"/> ทำสิ่งที่ต้องการครบอยู่แล้ว — รูป · ตัวย่อตอนไม่มีรูป ·
    /// เลข Lv มุมล่างขวา · กรอบเส้นประตอนว่าง · และมันเป็น prefab ที่ตัวสร้างดูแลอยู่แล้ว
    ///
    /// แถบนี้แยกจาก <see cref="BuildStripUI"/> เพราะอยู่คนละที่บนจอและมีจำนวนช่องของตัวเอง
    /// **แต่ช่องเป็นของเดียวกัน** — แก้หน้าตาช่องที่เดียวแล้วเปลี่ยนทั้งสองแถบ
    /// ถ้าทำช่องชนิดใหม่ วันหนึ่งสองแบบจะไม่เหมือนกันโดยไม่มีใครตั้งใจ
    ///
    /// ═══ augment ถูกถอดออกจากแถว PASSIVES แล้ว ═══
    ///
    /// ก่อนหน้านี้ `BuildStripUI` ยัด augment ต่อท้ายสเตตัสในแถว PASSIVES
    /// พอมีแถบของตัวเองแล้วต้องถอดออกจากที่นั่น ไม่งั้นใบเดียวโผล่สองที่
    /// </summary>
    [DisallowMultipleComponent]
    public class AugmentStripUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("แม่แบบช่อง — **ต้องชี้ไฟล์ prefab** (BuildStripSlot.prefab) ไม่ใช่ของในซีน")]
        public BuildStripSlot slotTemplate;
        [Tooltip("ที่วางช่อง — ปล่อยว่าง = วางบน GameObject ตัวเอง")]
        public RectTransform  slotArea;

        [Header("── Layout ─────────────────────────────")]
        [Tooltip("จำนวนช่องที่โชว์เสมอ — ช่องที่ยังไม่มีของเป็นกรอบเส้นประ\n\n" +
                 "ไม่ผูกกับเพดานจำนวน augment ที่ถือได้ (ไม่มีเพดานแบบนั้น) — นี่คือ\n" +
                 "จำนวนที่ **จอมีที่ให้โชว์** ถือเกินกว่านี้จะเห็นแค่ใบแรกๆ")]
        [Min(1)]
        public int   slotCount = 3;
        [Tooltip("ขนาดช่อง (px ที่ 1920×1080)")]
        public float slotSize = 44f;
        [Tooltip("ช่องไฟระหว่างช่อง")]
        public float slotGap  = 6f;
        [Tooltip("true = เรียงซ้ายไปขวา · false = เรียงล่างขึ้นบน")]
        public bool  horizontal = true;

        [Header("── Behaviour ──────────────────────────")]
        [Tooltip("วินาทีระหว่างการอ่านค่าใหม่ · 0 = ไม่เดินจังหวะเอง (ต้องเรียก Refresh เอง)")]
        public float refreshInterval = 0.4f;

        [Header("── Palette ────────────────────────────")]
        public Color borderColor = new Color(1f, 1f, 1f, 0.28f);
        [Tooltip("สีพื้นช่อง — **สีเดียวทุกใบ** augment ไม่มีระดับให้ไล่สี")]
        public Color augmentColor = new Color(0.70f, 0.35f, 0.95f);

        private readonly List<BuildStripSlot> slots = new();
        private float timer;

        private void OnEnable()
        {
            HideSceneTemplate();
            timer = 0f;
            Refresh();
        }

        private void Update()
        {
            if (refreshInterval <= 0f) return;

            // unscaled — แถบนี้ต้องตรงตอนจอเลือกการ์ดเปิดอยู่ ซึ่ง timeScale เป็น 0
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            Refresh();
        }

        /// <summary>อ่าน augment ของผู้เล่นเครื่องนี้แล้วเขียนลงช่อง</summary>
        public void Refresh()
        {
            EnsureSlots();

            var pam = FindLocalAugmentManager();
            if (pam == null) { ShowAllEmpty(); return; }

            // augment ถือได้ใบละครั้งเดียวแล้ว — แต่ยังยุบซ้ำไว้เพราะลิสต์นี้มาจาก
            // ฝั่ง gameplay ซึ่งอาจถูกเรียก Acquire ซ้ำจากเส้นทางที่ยังไม่รู้จัก
            // ยุบไว้ราคาถูกกว่าไล่หาว่าทำไมช่องหมดทั้งที่ถือใบเดียว
            var seen = new HashSet<AugmentData>();
            int i = 0;

            foreach (var a in pam.GetAcquired())
            {
                if (a == null || !seen.Add(a)) continue;
                if (i >= slots.Count) break;

                // level = 0 → ช่องไม่โชว์เลข · augment ไม่มีเลเวลและถือได้ใบเดียว
                // มีตัวเลขบนช่องแปลว่ามีอะไรให้สะสม ซึ่งไม่จริงแล้ว
                slots[i].ShowEntry(
                    new BuildStripUI.Entry
                    {
                        icon      = a.icon,
                        abbrev    = Abbrev(a.DisplayName),
                        level     = 0,
                        highlight = false,
                    },
                    augmentColor,
                    borderColor,
                    augmentColor);
                i++;
            }

            for (; i < slots.Count; i++) slots[i].ShowEmpty(borderColor);
        }

        /// <summary>
        /// หา <see cref="PlayerAugmentManager"/> ของ **ผู้เล่นเครื่องนี้**
        ///
        /// แถบนี้เป็นของในมือเรา ไม่ใช่ของทีม — หยิบตัวที่ IsOwner เท่านั้น
        /// `NetworkManager.LocalClient.PlayerObject` ว่างได้ตอนยังไม่ spawn จึงไล่จาก component
        /// </summary>
        private static PlayerAugmentManager FindLocalAugmentManager()
        {
            var all = Object.FindObjectsByType<PlayerAugmentManager>(
                          FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var p in all)
                if (p != null && p.IsOwner) return p;

            return null;
        }

        private void EnsureSlots()
        {
            if (slotTemplate == null) return;

            var parent = slotArea != null ? slotArea : (RectTransform)transform;

            while (slots.Count < slotCount)
            {
                var s = Instantiate(slotTemplate, parent);
                s.name = $"AugSlot_{slots.Count}";
                s.gameObject.SetActive(true);

                var rt = s.transform as RectTransform;
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(slotSize, slotSize);
                    float step = slotSize + slotGap;
                    rt.anchoredPosition = horizontal
                        ? new Vector2(slots.Count * step, 0f)
                        : new Vector2(0f, slots.Count * step);
                }
                slots.Add(s);
            }

            for (int i = slotCount; i < slots.Count; i++)
                if (slots[i] != null) slots[i].gameObject.SetActive(false);
        }

        private void ShowAllEmpty()
        {
            foreach (var s in slots) if (s != null) s.ShowEmpty(borderColor);
        }

        /// <summary>
        /// ปิดแม่แบบถ้ามันเป็นของในซีน — **ห้ามแตะถ้าเป็นไฟล์ prefab**
        /// สั่ง SetActive บน asset คือการแก้ไฟล์ในโปรเจกต์จากโค้ดตอนรัน
        /// `gameObject.scene.IsValid()` เป็น false สำหรับ prefab asset — เช็คได้ตอนรันจริง
        /// </summary>
        private void HideSceneTemplate()
        {
            if (slotTemplate == null) return;
            if (!slotTemplate.gameObject.scene.IsValid()) return;
            slotTemplate.gameObject.SetActive(false);
        }

        /// <summary>ตัวย่อสามตัวอักษรแบบเดียวกับแถบ build — โชว์ตอนไม่มีไอคอน</summary>
        private static string Abbrev(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var clean = name.Replace(" ", "");
            return clean.Length <= 3 ? clean.ToUpperInvariant()
                                     : clean.Substring(0, 3).ToUpperInvariant();
        }
    }
}
