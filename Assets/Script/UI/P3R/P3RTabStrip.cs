using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ซ่อน/โชว์ปุ่มบนแถบแท็บที่จอนี้วาดไว้ ให้ตรงกับ <see cref="TabBar"/> ตัวจริง
    ///
    /// ═══ ทำไมต้องมีตัวนี้ ═══
    ///
    /// จอ P3R **แต่ละจอวาดแถบแท็บของตัวเอง** — `P3R_TalentShop/TabBar/Tab_LOBBY`,
    /// `P3R_Character/TabBar/Tab_LOBBY` … เป็นคนละ object กันสี่ชุด
    /// ส่วน `TabBar` ตัวที่คุมจริงอยู่บน `P3R_Hub` และช่อง `button` ของมันว่างทั้งสี่
    ///
    /// `LobbyUI.Refresh()` สั่ง `SetTabVisible("lobby", false)` ตอนเข้าร้านจากเมนูหลัก
    /// มาตลอด แต่คำสั่งนั้นไปไม่ถึงปุ่มที่ผู้เล่นเห็น มันแค่พลิก flag ในตัว TabBar เอง
    /// อาการคือเข้าร้านจากเมนูหลักแล้วยังเห็นแท็บ LOBBY กับ MAP ทั้งที่ยังไม่มีห้อง
    /// กดแล้วก็ไม่ไปไหนเพราะ `TabBar.Select()` ปฏิเสธแท็บที่ไม่ visible
    ///
    /// ═══ ทำไมไม่ให้ P3RTabJump จัดการเอง ═══
    ///
    /// มันอยู่บนปุ่มที่ต้องถูกซ่อน · component ที่ปิด GameObject ตัวเองแล้วจะไม่ได้รับ
    /// event ต่อ และเปิดตัวเองกลับไม่ได้ตลอดกาล · ตัวที่ซ่อนคนอื่นต้องอยู่คนละชั้นกับของที่ถูกซ่อน
    ///
    /// ═══ ไฮไลต์แท็บที่เลือก (highlight = true) ═══
    ///
    /// ใน MenuScene หัวจอเหลือชุดเดียวบน P3R_Hub (P3RScreenWirer.EnsureHubHeader) · สีแท็บที่ builder
    /// อบไว้เป็นของจอที่ถูกยกขึ้นมา (LOBBY) จึงต้องย้อมใหม่ทุกครั้งที่ TabBar เปลี่ยนแท็บ
    /// ย้อมทั้งพื้น (ลูกชื่อ Bg) และป้าย (ลูกชื่อ Label) · TabBar ย้อมได้แค่ป้าย
    ///
    /// ═══ ของเฉพาะจอบนหัวจอ (extras) ═══
    ///
    /// หัวจอใช้ร่วมกันสี่จอ แต่ของบางอย่างเป็นของจอเดียว (ปุ่มห้องของ LOBBY · BACK ของ CHARACTER/SHOP)
    /// ตัวต่อสายย้ายชั้น HeaderExtras ของแต่ละจอขึ้นมาไว้บนหัวจอ · ตัวนี้เปิดเฉพาะชั้นของแท็บที่เลือก
    ///
    /// ═══ เรื่องระยะห่าง ═══
    ///
    /// builder วางปุ่มไว้ที่ x คงที่ไล่ไปทีละ 186px · ซ่อนสองตัวแรกเฉยๆ จะเหลือช่องว่าง
    /// ที่ขอบซ้ายแล้วปุ่มที่เหลือลอยอยู่กลาง · ตัวนี้จึงจัดระยะใหม่ให้ตัวที่เหลือชิดกัน
    /// โดยอ่าน x ตั้งต้นกับระยะห่างจากที่ builder วางไว้ ไม่ได้ฮาร์ดโค้ดเลข
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RTabStrip : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("id ที่ตรงกับ TabBar.tabs[].id — lobby / map / character / shop")]
            public string id;
            public RectTransform button;
        }

        [Tooltip("ปล่อยว่างได้ — จะหา TabBar ในซีนเอง")]
        public TabBar tabBar;

        public List<Entry> entries = new();

        [System.Serializable]
        public class Extra
        {
            [Tooltip("id ของแท็บเจ้าของ — โผล่เฉพาะตอนแท็บนี้ถูกเลือก")]
            public string id;
            public GameObject root;
        }

        [Tooltip("ของเฉพาะจอบนหัวจอ (HeaderExtras_<id>) — P3RScreenWirer เติมให้")]
        public List<Extra> extras = new();

        [Header("── ไฮไลต์แท็บที่เลือก ─────────────────")]
        [Tooltip("เปิดเมื่อแถบนี้เป็นหัวจอชุดเดียวที่ใช้ร่วมทุกจอ — ปิด = ใช้สีที่ builder อบไว้")]
        public bool  highlight;
        public Color activeBg     = new Color32(0x18, 0x24, 0xD8, 0xFF);
        public Color inactiveBg   = new Color32(0x14, 0x17, 0x23, 0xFF);
        public Color activeText   = Color.white;
        public Color inactiveText = new Color(1f, 1f, 1f, 0.55f);

        // x ที่ builder วางไว้ตอนแรก — ใช้เป็นจุดตั้งต้นกับระยะห่าง ไม่เก็บก็จัดใหม่ไม่ได้
        // เพราะพอซ่อนรอบแรกแล้วตำแหน่งเดิมหายไป
        private float baseX;
        private float pitch;
        private bool  captured;

        private void Awake()
        {
            if (tabBar == null) tabBar = FindAnyObjectByType<TabBar>(FindObjectsInactive.Include);
            Capture();
        }

        private void OnEnable()
        {
            TabBar.OnTabsChanged += Apply;
            TabBar.OnTabChanged  += Highlight;
            // จอถูกเปิดทีหลังคำสั่งซ่อน — ต้องอ่านสถานะปัจจุบันเองรอบหนึ่งเสมอ
            // ไม่งั้นจอที่เพิ่งเปิดจะโชว์แท็บครบทั้งที่ TabBar สั่งซ่อนไปแล้ว
            Apply();
            Highlight(tabBar != null ? tabBar.CurrentTabId : null);
        }

        private void OnDisable()
        {
            TabBar.OnTabsChanged -= Apply;
            TabBar.OnTabChanged  -= Highlight;
        }

        private void Highlight(string id)
        {
            // ยังไม่มีแท็บที่เลือก → ซ่อนหมด ไม่ให้ของสี่จอซ้อนกันบนหัวจอ
            foreach (var x in extras)
            {
                if (x == null || x.root == null) continue;
                bool show = !string.IsNullOrEmpty(id) && x.id == id;
                if (x.root.activeSelf != show) x.root.SetActive(show);
            }

            if (!highlight || string.IsNullOrEmpty(id)) return;
            foreach (var e in entries)
            {
                if (e == null || e.button == null) continue;
                bool on = e.id == id;
                var bg = e.button.Find("Bg")?.GetComponent<Image>();
                if (bg != null) bg.color = on ? activeBg : inactiveBg;
                var label = e.button.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null) label.color = on ? activeText : inactiveText;
            }
        }

        private void Capture()
        {
            if (captured) return;
            captured = true;

            var xs = new List<float>();
            foreach (var e in entries)
                if (e != null && e.button != null) xs.Add(e.button.anchoredPosition.x);

            if (xs.Count == 0) return;
            baseX = xs[0];
            // ระยะห่างจากสองตัวแรก — ปุ่มทุกตัวกว้างเท่ากันและเรียงห่างเท่ากันอยู่แล้ว
            pitch = xs.Count >= 2 ? xs[1] - xs[0] : 0f;
        }

        private void Apply()
        {
            if (tabBar == null) return;
            Capture();

            float x = baseX;
            foreach (var e in entries)
            {
                if (e == null || e.button == null) continue;

                bool show = tabBar.IsVisible(e.id);
                if (e.button.gameObject.activeSelf != show) e.button.gameObject.SetActive(show);
                if (!show) continue;

                var p = e.button.anchoredPosition;
                if (!Mathf.Approximately(p.x, x)) e.button.anchoredPosition = new Vector2(x, p.y);
                x += pitch;
            }
        }
    }
}
