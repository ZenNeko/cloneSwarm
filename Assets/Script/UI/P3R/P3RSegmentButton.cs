using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// เจ้าของชุดปุ่มแบ่งช่อง — มีสองแบบที่ขับคนละแหล่งข้อมูล
    /// <see cref="P3RSegmentedControl"/> ขับ TMP_Dropdown · <see cref="P3RDifficultySelector"/> ขับ enum
    /// ปุ่มไม่ต้องรู้ว่าใครเป็นเจ้าของ รู้แค่ว่าจะบอกใครตอนถูกกด
    /// </summary>
    public interface IP3RSegmentOwner
    {
        void Choose(int index);
    }

    /// <summary>
    /// ปุ่มหนึ่งช่องในชุดปุ่มแบ่งช่อง — พื้นเอียง ตัวหนังสือไม่เอียง ตามภาษาภาพ P3R
    ///
    /// จงใจ **ไม่ใช้ <c>UnityEngine.UI.Button</c>** ด้วยเหตุผลเดียวกับ <see cref="P3RMenuItem"/>
    /// (`handoff-ui-2026-08-22` §7) — Selectable ที่ค้างโฟกัสทำให้ Space/Enter ครั้งถัดไป
    /// ไปยิงปุ่มที่ผู้เล่นไม่ได้มองอยู่ ซึ่งเจ็บเป็นพิเศษบนจอที่มี <see cref="P3RMenuList"/>
    /// อยู่ด้วย เพราะปุ่ม Space ของ list จะยิงทั้งสองที่ในเฟรมเดียว
    ///
    /// ตัวนี้ไม่ตัดสินใจอะไรเอง — <see cref="P3RSegmentedControl"/> เป็นคนบอกว่าใครถูกเลือก
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RSegmentButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("── Wiring ─────────────────────────────")]
        public Image           background;
        public TextMeshProUGUI label;

        [Header("── Colours ────────────────────────────")]
        public Color selectedBg   = new Color32(0x18, 0x24, 0xD8, 0xFF);
        public Color normalBg     = new Color(1f, 1f, 1f, 0.06f);
        public Color hoverBg      = new Color(1f, 1f, 1f, 0.14f);
        public Color selectedText = Color.white;
        public Color normalText   = new Color(1f, 1f, 1f, 0.66f);

        /// <summary>ช่องที่เท่าไรในชุด — ตรงกับ index ของ TMP_Dropdown.options</summary>
        public int Index { get; private set; }

        private IP3RSegmentOwner owner;
        private bool selected;
        private bool hovered;

        public void Init(IP3RSegmentOwner control, int index, string text, float tracking)
        {
            owner = control;
            Index = index;
            // ผ่าน P3RText เสมอ — ชื่อภาษาอาจเป็นไทย ("ไทย") ซึ่งถ่างระยะแล้วสระหลุด
            P3RText.SetTextAndTracking(label, text, tracking);
            Refresh();
        }

        public void SetSelected(bool on)
        {
            selected = on;
            Refresh();
        }

        private void Refresh()
        {
            if (background != null)
                background.color = selected ? selectedBg : (hovered ? hoverBg : normalBg);
            if (label != null)
                label.color = selected ? selectedText : normalText;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true;  Refresh(); }
        public void OnPointerExit (PointerEventData e) { hovered = false; Refresh(); }
        public void OnPointerClick(PointerEventData e) => owner?.Choose(Index);
    }
}
