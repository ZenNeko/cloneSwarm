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

        [Tooltip("id ของแท็บปลายทาง — lobby / map / character / shop")]
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
            if (tabBar == null)
            {
                Debug.LogWarning($"[P3R] P3RTabJump: ไม่พบ TabBar — ปุ่มนี้กดแล้วไม่ไปไหน", this);
                return;
            }
            tabBar.Select(tabId);
        }
    }
}
