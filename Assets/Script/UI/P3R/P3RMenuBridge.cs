using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ต่อ <see cref="P3RMenuList"/> เข้ากับ <c>MenuManager</c> ของจริง
    ///
    /// เมนูหลักแบบ P3R **ไม่ได้ใช้ <c>UnityEngine.UI.Button</c>** (ดูเหตุผลใน <see cref="P3RMenuItem"/>)
    /// `MenuManager` จึงต่อสายเข้าไม่ได้ตรงๆ เพราะฟิลด์ของมันเป็น `Button` ทั้งหมด
    /// ตัวนี้จึงรับ `OnConfirm(id)` แล้วเรียกเมธอดของ `MenuManager` ให้แทน
    ///
    /// ต่างจาก <see cref="P3RMainMenuProto"/> ที่แค่ `Debug.Log` — ตัวนั้นมีไว้ให้ซีนต้นแบบ
    /// กด Play แล้วเห็นว่าสายต่อถึง · ตัวนี้คือของจริง
    ///
    /// **id ต้องตรงกับที่ `P3RMenuSceneBuilder` ใส่ไว้** — เป็น string ไม่ใช่ index
    /// สลับลำดับรายการบนจอแล้วไม่พัง แต่เปลี่ยนชื่อ id แล้วพัง
    /// id ที่ไม่รู้จักจะเตือนใน Console ไม่ใช่เงียบหาย
    /// </summary>
    [RequireComponent(typeof(P3RMenuList))]
    [DisallowMultipleComponent]
    public class P3RMenuBridge : MonoBehaviour
    {
        [Header("── Target ─────────────────────────────")]
        [Tooltip("ปล่อยว่างได้ — จะหาเองในซีนตอน Awake")]
        public MenuManager menuManager;

        private P3RMenuList list;

        private void Awake()
        {
            list = GetComponent<P3RMenuList>();

            // หาเองถ้าไม่ได้ต่อไว้ — MenuManager มีตัวเดียวต่อซีนอยู่แล้ว
            // ใช้ includeInactive เพราะบางซีนวางมันไว้บน object ที่ปิดอยู่ตอนเริ่ม
            if (menuManager == null)
                menuManager = FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (list == null) return;
            list.OnConfirm += HandleConfirm;
            list.OnCancel  += HandleCancel;
        }

        private void OnDisable()
        {
            if (list == null) return;
            list.OnConfirm -= HandleConfirm;
            list.OnCancel  -= HandleCancel;
        }

        private void HandleConfirm(string id)
        {
            if (menuManager == null)
            {
                Debug.LogError("[P3R] P3RMenuBridge: ไม่พบ MenuManager — เมนูหลักจะกดไม่ติดทั้งหน้า", this);
                return;
            }

            switch (id)
            {
                case "play":     menuManager.OnPlayClicked();       break;
                case "join":     menuManager.OnJoinRoomClicked();   break;
                case "shop":     menuManager.OnTalentShopClicked(); break;
                case "settings": menuManager.OnSettingsClicked();   break;
                case "quit":     menuManager.OnQuitClicked();       break;

                // CONTINUE ยังไม่มีระบบเซฟรอบเล่นค้าง — รายการถูกตั้งเป็นกดไม่ได้อยู่แล้ว
                // ถ้ามาถึงตรงนี้แปลว่ามีคนเปิด interactable ทิ้งไว้
                case "continue":
                    Debug.LogWarning("[P3R] ยังไม่มีระบบเล่นต่อ — ตั้ง P3RMenuItem.interactable = false ไว้", this);
                    break;

                default:
                    Debug.LogWarning($"[P3R] P3RMenuBridge: ไม่รู้จัก id '{id}'", this);
                    break;
            }
        }

        /// <summary>Esc บนเมนูหลัก = ไม่ทำอะไร — ไม่มีหน้าไหนให้ถอยกลับไปแล้ว</summary>
        private void HandleCancel() { }
    }
}
