using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// หนึ่งช่องใน grid ของ Talent Shop (ทรงเดียวกับร้าน meta ของ LoL Swarm)
    /// คลิกที่ช่อง = เลือก → panel รายละเอียดฝั่งขวาอัปเดต (ไม่ใช่ซื้อทันที)
    ///
    /// Hierarchy แนะนำ (auto-find ถ้าชื่อ child ตรง):
    ///   TalentTile  (Image BG + Button + TalentTileUI)
    ///   ├── Icon           (Image)          ← icon ใหญ่กลางช่อง
    ///   ├── NameText       (TMP)
    ///   ├── PipsContainer  (Horizontal Layout Group)
    ///   │   └── Pip ×6     (Image)          ← ใส่เผื่อไว้ 6 อัน เกิน maxLevel จะถูกซ่อนเอง
    ///   ├── CostText       (TMP)            ← "3,000" หรือ "สูงสุด"
    ///   ├── CoinIcon       (Image)          ← optional, ซ่อนเองตอนตัน
    ///   └── SelectedFrame  (Image กรอบ)     ← optional, โชว์ตอนถูกเลือก
    /// </summary>
    public class TalentTileUI : MonoBehaviour
    {
        [Header("Elements (auto-find ถ้าไม่ assign)")]
        public Image           iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;
        public Image           coinIcon;
        public Button          tileButton;
        public Image           background;

        [Header("Pips")]
        [Tooltip("Container ของขีดบอกเลเวล — ใส่ child Image เผื่อไว้เท่ากับ maxLevel สูงสุดที่รองรับ")]
        public Transform pipsContainer;
        public Color     pipFilledColor = new Color(0.173f, 0.773f, 0.627f);   // #2CC5A0
        public Color     pipEmptyColor  = new Color(0.141f, 0.165f, 0.212f);   // #242A36

        [Header("Selection")]
        [Tooltip("กรอบไฮไลต์ตอนถูกเลือก — ปล่อยว่างได้ (จะใช้สีขอบ background แทน)")]
        public GameObject selectedFrame;
        public Color      selectedBgColor = new Color(0.082f, 0.096f, 0.133f);
        public Color      normalBgColor   = new Color(0.082f, 0.098f, 0.133f);

        [Header("Cost Colors")]
        public Color affordableColor = new Color(0.941f, 0.839f, 0.541f);   // #F0D68A
        public Color tooPoorColor    = new Color(0.420f, 0.447f, 0.502f);   // #6B7280
        public Color maxedColor      = new Color(0.173f, 0.773f, 0.627f);   // #2CC5A0

        TalentData         talent;
        Action<TalentData> onClicked;

        public TalentData Talent => talent;

        // ═══════════════════════════════════════════════════════════════════
        void Awake()
        {
            if (background    == null) background    = GetComponent<Image>();
            if (tileButton    == null) tileButton    = GetComponent<Button>();
            if (iconImage     == null) iconImage     = transform.Find("Icon")?.GetComponent<Image>();
            if (nameText      == null) nameText      = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (costText      == null) costText      = transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            if (coinIcon      == null) coinIcon      = transform.Find("CoinIcon")?.GetComponent<Image>();
            if (pipsContainer == null) pipsContainer = transform.Find("PipsContainer");
            if (selectedFrame == null) selectedFrame = transform.Find("SelectedFrame")?.gameObject;
        }

        public void SetData(TalentData t, Action<TalentData> clickCallback)
        {
            talent    = t;
            onClicked = clickCallback;

            if (tileButton != null)
            {
                tileButton.onClick.RemoveAllListeners();
                tileButton.onClick.AddListener(() => onClicked?.Invoke(talent));
            }

            // DisplayName ไม่ใช่ talentName — talentName เป็นชื่อสำรองตอนยังไม่ผูก String Table
            if (nameText != null) nameText.text = t.DisplayName;

            if (iconImage != null)
            {
                // Icon ตัวพิมพ์ใหญ่ = ตกไปใช้รูปกลางของ StatIconSet ให้เมื่อ talent ไม่ได้ใส่เอง
                var spr = t.Icon;
                iconImage.sprite  = spr;
                iconImage.enabled = true;
                // มีรูป = ขาวล้วน ห้ามย้อม · ไม่มีรูป = สีหมวดทำหน้าที่เป็นบล็อกแทนรูป
                // (ของเดิมตั้ง enabled = false ตอนไม่มีรูป สีหมวดจึงไม่เคยได้วาดเลย
                //  ทั้งที่คอมเมนต์เขียนไว้ว่า "ใช้แม้ตอนไม่มี sprite")
                iconImage.color   = spr != null ? Color.white : t.tintColor;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (talent == null) return;

            int  level = MetaProgression.GetTalentLevel(talent);
            int  maxLv = talent.MaxLevel;
            bool maxed = level >= maxLv;
            int  cost  = talent.GetCostToUpgrade(level);

            if (costText != null)
            {
                costText.text  = maxed ? "สูงสุด" : $"{cost:N0}";
                costText.color = maxed ? maxedColor
                               : (MetaProgression.Gold >= cost ? affordableColor : tooPoorColor);
            }

            // เหรียญไม่ต้องโชว์ตอนตันแล้ว (ไม่มีราคา)
            if (coinIcon != null) coinIcon.enabled = !maxed;

            RefreshPips(level, maxLv);
        }

        void RefreshPips(int level, int maxLv)
            => ApplyPips(pipsContainer, level, maxLv, pipFilledColor, pipEmptyColor);

        /// <summary>
        /// ย้อมขีดบอกเลเวลตามเลเวลจริง — **ที่เดียวของทั้งร้าน**
        ///
        /// เปิดเป็น static เพราะ <see cref="TalentShopUI"/> ก็มีขีดชุดเดียวกันบนแผงขวา
        /// และ **มันเคยไม่ถูกขับเลย** — builder ฝังสีไว้ว่าเต็มสามขีดตอนสร้างซีน
        /// แล้วไม่มีใครแตะอีก แผงขวาจึงขึ้น 3/5 ตลอดแม้ป้ายข้างๆ จะเขียน "Lv 5 / 5"
        /// ผู้เล่นเห็นสองอย่างขัดกันเองบนจอเดียว
        ///
        /// มีที่เดียวแล้วอีกฝั่งลืมเรียกก็ยังพังได้ — แต่ก๊อปตรรกะไปสองที่คือรับประกัน
        /// ว่าวันหนึ่งมันจะเพี้ยนกันแน่นอน
        /// </summary>
        public static void ApplyPips(Transform container, int level, int maxLv,
                                     Color filled, Color empty)
        {
            if (container == null) return;

            int n = container.childCount;
            for (int i = 0; i < n; i++)
            {
                var child = container.GetChild(i);
                bool used = i < maxLv;
                child.gameObject.SetActive(used);        // ขีดส่วนเกิน maxLevel → ซ่อน
                if (!used) continue;

                var img = child.GetComponent<Image>();
                if (img != null) img.color = i < level ? filled : empty;
            }
        }

        public void SetSelected(bool on)
        {
            if (selectedFrame != null) selectedFrame.SetActive(on);
            if (background    != null) background.color = on ? selectedBgColor : normalBgColor;
        }
    }
}
