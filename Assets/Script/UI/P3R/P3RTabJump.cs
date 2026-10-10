using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ปุ่มที่พาไปแท็บอื่นใน <c>TabBar</c> — ใช้กับปุ่ม BACK / CONFIRM MAP ที่ท้ายจอ
    ///
    /// จอ MAP SELECT และ TALENT SHOP อยู่ใน TabBar อยู่แล้ว การ "ย้อนกลับ" จึงไม่ใช่
    /// การปิด panel แต่คือการสลับไปแท็บ lobby · ถ้าไปปิด panel เองแท็บจะค้างว่าง
    ///
    /// การเลือกแมพถูกส่งขึ้น <c>LobbyState</c> ตั้งแต่ตอนคลิกการ์ดแล้ว
    /// (`MapSelectUI.SelectMap()` → `LobbyUI.SelectMap()`) ปุ่ม CONFIRM จึงไม่ต้อง
    /// ยืนยันอะไรเพิ่ม แค่พากลับ — ตัวปุ่มมีไว้บอกผู้เล่นว่า "เลือกเสร็จแล้วกดตรงนี้"
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class P3RTabJump : MonoBehaviour
    {
        [Header("── Target ─────────────────────────────")]
        [Tooltip("ปล่อยว่างได้ — จะหาเองในซีน")]
        public TabBar tabBar;

        [Tooltip("id ของแท็บปลายทาง — lobby / map / character / shop\n\n" +
                 "หรือ \"back\" = ย้อนกลับหนึ่งชั้น ซึ่งไปคนละที่กันตามทางที่เข้ามา")]
        public string tabId = "lobby";

        [Header("── ป้ายปุ่ม (ใช้เฉพาะ tabId \"back\") ────")]
        [Tooltip("ปล่อยว่างได้ — จะหาลูกชื่อ Label เอง")]
        public TMP_Text label;

        [Tooltip("ป้ายตอนที่กดแล้วกลับเมนูหลัก")]
        public string backToMainText = "BACK";

        [Tooltip("ป้ายตอนที่กดแล้วกลับล็อบบี้")]
        public string backToLobbyText = "BACK TO LOBBY";

        [Tooltip("ป้ายตอนอยู่แท็บ lobby — กดแล้วออกจากห้อง (LobbyUI.Back)")]
        public string backAtLobbyText = "BACK";

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (tabBar == null)
                tabBar = FindAnyObjectByType<TabBar>(FindObjectsInactive.Include);
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(Jump);

            // ป้ายต้องบอกปลายทางจริง ไม่ใช่คำว่า "ถอย" ลอยๆ — ปลายทางต่างกันตามทางที่เข้ามา
            // โหมดถูกตั้งก่อน panel เปิด (MenuManager สั่ง SetMode ทันทีหลัง ShowPanel)
            // จึงอ่านตอน OnEnable ได้ตรง · และ TabBar.OnTabsChanged ยิงทุกครั้งที่ Refresh
            // ซึ่งคือทุกครั้งที่โหมดเปลี่ยน จึงพอสำหรับการอัปเดตระหว่างอยู่ในจอ
            TabBar.OnTabsChanged += RefreshLabel;
            // ปุ่ม BACK ใน MenuScene มีปุ่มเดียวใช้ร่วมทุกแท็บ — ป้ายต้องตามแท็บที่เปิดอยู่
            TabBar.OnTabChanged  += OnTabChanged;
            RefreshLabel();
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Jump);
            TabBar.OnTabsChanged -= RefreshLabel;
            TabBar.OnTabChanged  -= OnTabChanged;
        }

        private void OnTabChanged(string _) => RefreshLabel();

        private bool AtLobbyTab() => tabBar != null && tabBar.CurrentTabId == "lobby";

        private void RefreshLabel()
        {
            if (tabId != "back" || label == null) return;
            label.text = AtLobbyTab()     ? backAtLobbyText
                       : BackGoesToMain() ? backToMainText : backToLobbyText;
        }

        /// <summary>
        /// โหมด Shop = เข้า hub มาจากเมนูหลักตรงๆ (`MenuManager.OnTalentShopClicked`)
        /// ไม่ใช่จากล็อบบี้ · การถอยจึงคือออกจาก hub ไม่ใช่กลับไปแท็บ lobby
        /// </summary>
        private static bool BackGoesToMain()
        {
            var hub = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
            return hub != null && hub.Mode == HubMode.Shop;
        }

        private void Jump()
        {
            // ── "back" = ย้อนกลับหนึ่งชั้น ซึ่งไม่ใช่ที่เดียวกันเสมอ ───────────
            //
            // ร้าน Talent เข้าได้สองทาง และปุ่ม BACK ต้องพากลับทางที่มา
            //   เมนูหลัก → OnTalentShopClicked() → hub โหมด Shop  ⇒ กลับ **เมนูหลัก**
            //   ล็อบบี้  → กดแท็บ SHOP (ไม่แตะโหมด)               ⇒ กลับ **แท็บ lobby**
            //
            // ของเดิมผูกปุ่มนี้ไว้กับ tabId "lobby" ตรงๆ · เข้าร้านจากเมนูหลักแล้วกด BACK
            // จึงไปโผล่ที่ล็อบบี้ ซึ่งเป็นหน้าที่ผู้เล่นไม่เคยเห็นมาก่อนในเส้นทางนั้น
            if (tabId == "back")
            {
                // อยู่แท็บ lobby = ถอยออกจากล็อบบี้ (ออกจากห้อง) — งานของ LobbyUI ไม่ใช่การสลับแท็บ
                if (AtLobbyTab())
                {
                    var lobbyUi = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                    if (lobbyUi != null) { lobbyUi.Back(); return; }
                }

                if (BackGoesToMain())
                {
                    var menu = FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);
                    if (menu != null) { menu.ShowMain(); return; }

                    Debug.LogWarning("[P3R] P3RTabJump 'back': ไม่พบ MenuManager — ถอยไปแท็บ lobby แทน", this);
                }
                // มาจากล็อบบี้ (หรือหา MenuManager ไม่เจอ) — ถอยเป็นแท็บ lobby ตามเดิม
            }

            if (tabBar == null)
            {
                Debug.LogWarning($"[P3R] P3RTabJump: ไม่พบ TabBar — ปุ่มนี้กดแล้วไม่ไปไหน", this);
                return;
            }

            string target = tabId == "back" ? "lobby" : tabId;

            // แท็บ lobby กับ map ถูกซ่อนตอนอยู่โหมดร้าน (`LobbyUI.Refresh` เรียก SetTabVisible)
            // และ `TabBar.Select` มองข้ามแท็บที่ซ่อนอยู่เงียบๆ — กดแล้วไม่ไปไหน
            // ต้องสลับโหมดกลับก่อน ไม่งั้นปุ่ม BACK ในร้านจะกดไม่ติด
            if (target is "lobby" or "map")
            {
                var lobby = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                if (lobby != null) lobby.SetMode(HubMode.Lobby);
            }

            tabBar.Select(target);
        }
    }
}
