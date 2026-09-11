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

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (tabBar == null)
                tabBar = FindAnyObjectByType<TabBar>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(Jump);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Jump);
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
                var hub = FindAnyObjectByType<LobbyUI>(FindObjectsInactive.Include);
                if (hub != null && hub.Mode == HubMode.Shop)
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
