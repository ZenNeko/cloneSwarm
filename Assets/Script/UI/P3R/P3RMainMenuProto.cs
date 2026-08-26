using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ตัวรับปลายทางของซีนต้นแบบ — มีไว้เพื่อ "กด Play แล้วเห็นว่าสายต่อถึง" เท่านั้น
    ///
    /// จงใจ **ไม่แตะ MenuManager ของจริง** — งานรอบนี้คือทดลองภาษาภาพในซีนแยก
    /// ถ้าลุคผ่านค่อยตัดสินว่าจะเอา P3RMenuList ไปแทน MainPanel เดิมหรือไม่
    /// (ต้องรอ ADR-001 Action Item 4/5 ปิดก่อน — ตอนนี้ hierarchy ของ MenuScene ยังค้าง refactor)
    /// </summary>
    [RequireComponent(typeof(P3RMenuList))]
    public class P3RMainMenuProto : MonoBehaviour
    {
        private P3RMenuList list;

        private void Awake() => list = GetComponent<P3RMenuList>();

        private void OnEnable()
        {
            list.OnConfirm += HandleConfirm;
            list.OnCancel  += HandleCancel;
        }

        private void OnDisable()
        {
            list.OnConfirm -= HandleConfirm;
            list.OnCancel  -= HandleCancel;
        }

        private void HandleConfirm(string id)
        {
            Debug.Log($"[P3R Proto] CONFIRM → {id}");
        }

        private void HandleCancel()
        {
            Debug.Log("[P3R Proto] CANCEL");
        }
    }
}
