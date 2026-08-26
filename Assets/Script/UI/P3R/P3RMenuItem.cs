using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// รายการเมนูหนึ่งบรรทัดสไตล์ P3R — ตัวหนังสือชิดขวา + แถบทึบที่กางออกจากขวาตอนถูกเลือก
    ///
    /// ตัวนี้ไม่ตัดสินใจอะไรเอง — <see cref="P3RMenuList"/> เป็นคนสั่งทั้งหมด
    /// ตัวมันมีหน้าที่เดียวคือ "แสดงสถานะที่ถูกสั่ง" ให้ตรงตาม <see cref="P3RTheme"/>
    ///
    /// จงใจ **ไม่ใช้ UnityEngine.UI.Button** — handoff-ui-2026-08-22 §7 บันทึกบั๊กไว้แล้วว่า
    /// Selectable ที่ค้างโฟกัสทำให้ Enter/Space ไปยิงปุ่มที่ผู้เล่นไม่ได้มอง
    /// ที่นี่รับเมาส์ด้วย IPointer* ตรงๆ ไม่มี Selectable ไม่มีโฟกัส จึงไม่มีบั๊กชุดนั้น
    ///
    /// โครงที่ builder สร้างให้:
    ///   Item_xxx  (RectTransform pivot ขวา · CanvasGroup · P3RMenuItem)
    ///   ├─ HitArea  Image alpha 0 · raycastTarget → ตัวรับเมาส์ แล้ว bubble ขึ้นมาที่นี่
    ///   ├─ Bar      Image pivot ขวา → scale.x 0..1 = กางจากขวาไปซ้าย
    ///   └─ Label    TextMeshProUGUI ชิดขวา
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [Header("── Identity ───────────────────────────")]
        [Tooltip("ตัวระบุที่ P3RMenuList.OnConfirm ส่งกลับมา — ใช้ string ไม่ใช่ index จะได้ไม่พังตอนสลับลำดับ")]
        public string id = "new_game";

        [Tooltip("ข้อความที่โชว์ · ปล่อยว่างแล้วมันจะไม่แตะ label ให้แก้ตรง TMP ได้เอง")]
        public string labelText = "NEW GAME";

        [Tooltip("กดได้ไหม — false จะเป็นสีเทาและถูกข้ามตอนเลื่อนขึ้น/ลง")]
        public bool interactable = true;

        [Header("── Wiring ─────────────────────────────")]
        public TextMeshProUGUI label;
        public Image           bar;
        public CanvasGroup     group;

        // จำ theme ไว้ตอน Apply() เพื่อให้ SetSelected() ใช้ได้โดยไม่ต้องวิ่งหา list ทุกครั้ง
        // SerializeField เพื่อให้ค่าที่ Editor builder ใส่ไว้รอดข้าม domain reload
        [SerializeField, HideInInspector] private P3RTheme theme;

        // ── runtime ────────────────────────────────────────────────────────
        private RectTransform selfRt;
        private RectTransform barRt;
        private float baseX;
        private bool  baseXCaptured;

        /// <summary>0 = แถบหุบสนิท · 1 = กางเต็ม — P3RMenuList เป็นคนไล่ค่าให้</summary>
        public float BarReveal
        {
            get => barRt != null ? barRt.localScale.x : 0f;
            set
            {
                if (barRt == null) return;
                float v = Mathf.Max(0f, value);
                barRt.localScale = new Vector3(v, 1f, 1f);
                // ปิดทิ้งตอนหุบสนิท — Image ที่ scale 0 ยังกิน draw call อยู่ดี
                if (bar != null) bar.enabled = v > 0.001f;
            }
        }

        private void Awake() => CacheRefs();

        private void CacheRefs()
        {
            selfRt = (RectTransform)transform;
            if (bar != null) barRt = bar.rectTransform;
            if (!baseXCaptured && selfRt != null)
            {
                baseX = selfRt.anchoredPosition.x;
                baseXCaptured = true;
            }
        }

        /// <summary>
        /// ยัดค่าจาก theme ลงของจริง — เรียกได้ทั้งตอนรันและจาก Editor builder
        /// แยกเป็นเมธอดเดียวเพื่อให้ "เปลี่ยน theme แล้วเห็นผล" มีทางเดียว
        /// </summary>
        public void Apply(P3RTheme t)
        {
            CacheRefs();
            if (t == null) return;
            theme = t;

            if (label != null)
            {
                if (!string.IsNullOrEmpty(labelText)) label.text = labelText;
                if (theme.font != null) label.font = theme.font;
                label.fontSize            = theme.fontSize;
                label.characterSpacing    = theme.characterSpacing;
                label.alignment           = TextAlignmentOptions.Right;
                label.textWrappingMode     = TextWrappingModes.NoWrap;
                label.raycastTarget       = false;
                label.rectTransform.localScale = new Vector3(theme.horizontalScale, 1f, 1f);
            }

            if (barRt != null)
            {
                bar.color         = theme.barColor;
                bar.raycastTarget = false;
                barRt.sizeDelta        = new Vector2(theme.barExtendLeft + theme.barBleedRight, theme.barHeight);
                barRt.anchoredPosition = new Vector2(theme.barBleedRight, theme.barOffsetY);
            }

            SetSelected(false, instant: true);
        }

        public void SetSelected(bool on, bool instant)
        {
            if (label != null)
            {
                Color c = !interactable ? (theme != null ? theme.textDisabled : Color.gray)
                        : on            ? (theme != null ? theme.textSelected : Color.white)
                                        : (theme != null ? theme.textNormal   : Color.white);
                label.color = c;

                // แตะ outlineWidth เมื่อจำเป็นจริงเท่านั้น — setter ของ TMP สร้าง material instance ให้
                // ถ้าเรียกรัวๆ ทุกตัวจะได้ material ค้างในซีนโดยไม่ได้ใช้
                if (theme != null && theme.selectedOutline > 0f)
                    label.outlineWidth = (on && interactable) ? theme.selectedOutline : 0f;
            }

            if (instant) BarReveal = (on && interactable) ? 1f : 0f;
        }

        /// <summary>อนิเมชันเข้าตอนเปิดหน้า · t 0..1 ผ่าน easing มาแล้ว</summary>
        public void SetIntroProgress(float t, float slideDistance)
        {
            CacheRefs();
            if (selfRt == null) return;
            selfRt.anchoredPosition = new Vector2(baseX + slideDistance * (1f - t), selfRt.anchoredPosition.y);
            if (group != null) group.alpha = Mathf.Clamp01(t);
        }

        // ── mouse ──────────────────────────────────────────────────────────
        // hover = เลือกเลย · click = ยืนยัน
        // P3R ไม่มีสถานะ "ชี้อยู่แต่ยังไม่เลือก" — ตัวชี้กับตัวเลือกคือตัวเดียวกันเสมอ
        public void OnPointerEnter(PointerEventData e)
        {
            if (!interactable) return;
            GetComponentInParent<P3RMenuList>()?.SelectByItem(this, fromPointer: true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            GetComponentInParent<P3RMenuList>()?.ConfirmItem(this);
        }
    }
}
