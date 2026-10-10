using System.Collections.Generic;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// หัวจออันเดียวของ <c>P3R_Hub</c> — โชว์ของเฉพาะจอเฉพาะตอนแท็บนั้นถูกเลือก
    ///
    /// ═══ ทำไมต้องมีตัวนี้ ═══
    ///
    /// เดิมสี่จอแท็บวาด TopBar ของตัวเอง และแต่ละอันมีของไม่เหมือนกัน
    /// (ล็อบบี้มีรหัสห้อง · COPY · เชิญเพื่อน · ร้านมี RUNS/WINS/BEST)
    /// พอยุบเหลือหัวเดียว (<c>P3RHubHeaderUnify</c>) ของพวกนั้นถูกย้ายเข้ากลุ่ม
    /// `Extras_&lt;id&gt;` ใต้ TopBar · ตัวนี้เปิดกลุ่มที่ตรงกับแท็บที่เลือก ปิดที่เหลือ
    /// หน้าตาแต่ละแท็บจึงเหมือนเดิมทุกอย่าง แค่ไม่มีสำเนาแล้ว
    ///
    /// ═══ ทำไมสลับที่ "กลุ่ม" ไม่ใช่ที่ปุ่มเอง ═══
    ///
    /// `LobbyUI.Refresh()` สั่ง `inviteButton.SetActive(true)` และ
    /// `copyCodeButton.SetActive(hasSession)` เองทุกครั้งที่ล็อบบี้เปลี่ยน — ซึ่งเกิดได้
    /// ตอนอยู่แท็บอื่น · สองคนเขียน activeSelf ของปุ่มเดียวกัน = คนหลังชนะ
    /// แยกเป็นสองชั้นแทน: LobbyUI คุมปุ่ม ตัวนี้คุมกลุ่ม ไม่มีใครทับใคร
    ///
    /// อยู่บน `HubHeader` ซึ่งเปิดตลอดที่ hub เปิด — component ที่ปิด GameObject
    /// ตัวเองจะไม่ได้รับ event อีก (เหตุผลเดียวกับ <see cref="P3RTabStrip"/>)
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RHubHeader : MonoBehaviour
    {
        [System.Serializable]
        public class Group
        {
            [Tooltip("id ที่ตรงกับ TabBar.tabs[].id — lobby / map / character / shop")]
            public string tabId;
            public GameObject root;
        }

        [Tooltip("ปล่อยว่างได้ — จะหา TabBar ในซีนเอง")]
        public TabBar tabBar;

        public List<Group> groups = new();

        private void Awake()
        {
            if (tabBar == null) tabBar = FindAnyObjectByType<TabBar>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            TabBar.OnTabChanged += Show;
            // hub เปิดทีหลัง Select ได้ (LobbyUI.SetMode สั่งก่อน panel เปิด) — อ่านเองหนึ่งรอบ
            Show(tabBar != null ? tabBar.CurrentTabId : null);
        }

        private void OnDisable() => TabBar.OnTabChanged -= Show;

        private void Show(string currentId)
        {
            foreach (var g in groups)
            {
                if (g == null || g.root == null) continue;
                bool on = g.tabId == currentId;
                if (g.root.activeSelf != on) g.root.SetActive(on);
            }
        }
    }
}
