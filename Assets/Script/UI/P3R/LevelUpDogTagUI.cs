using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถบ "รอเพื่อนเลือก" มุมซ้ายล่างของจอ Level Up — ป้าย dog-tag 52×52 หนึ่งใบต่อผู้เล่นหนึ่งคน
    ///
    /// **ข้อจำกัดที่ต้องรู้ (สำคัญ)**
    /// ข้อมูลเดียวที่มีคือ <c>SharedExperienceManager.OnPickedCountChanged(picked, total)</c>
    /// ซึ่งบอกแค่ **จำนวน** ไม่ได้บอกว่า *ใคร* เลือกไปแล้ว
    /// (ฝั่ง server เก็บ <c>pickedPlayers</c> เป็น HashSet ของ clientId ไว้จริง แต่ไม่ได้ส่งออกมา)
    /// ที่นี่จึงเติมป้ายจากซ้ายไปขวาตามจำนวน — ป้ายใบที่ i ไม่ได้แปลว่าเป็นผู้เล่นคนที่ i
    /// ถ้าจะให้ตรงคนจริงต้องแก้ SharedExperienceManager ให้ส่งรายชื่อ clientId มาด้วย (อยู่นอกขอบเขตงานนี้)
    ///
    /// **ทำไมกะพริบด้วย unscaledTime**
    /// solo จะ <c>Time.timeScale = 0</c> ระหว่างเลือกการ์ด · ใช้ Time.time แล้วป้ายจะค้างนิ่งสนิท
    ///
    /// โครงที่ builder สร้างให้:
    ///   WaitingStrip
    ///   ├─ Label      TMP mono 17px "รอผู้เล่น 1 / 2"
    ///   └─ TagArea
    ///       └─ TagTemplate (ปิดไว้ · ถูก clone)
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelUpDogTagUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("ข้อความ 'รอผู้เล่น X / Y' — ปล่อยว่างได้ถ้าอยากโชว์แค่ป้าย")]
        public TextMeshProUGUI countLabel;
        [Tooltip("ป้ายต้นแบบที่จะถูก clone — ปิด (SetActive false) ไว้เสมอ")]
        public LevelUpDogTag tagTemplate;
        [Tooltip("ที่วางป้าย")]
        public RectTransform tagArea;

        [Header("── Layout ─────────────────────────────")]
        [Tooltip("ขนาดป้าย (px ที่กรอบ 1920) — design handoff ระบุ 52")]
        public float tagSize = 52f;
        public float tagGap  = 10f;

        [Header("── Colors ─────────────────────────────")]
        [Tooltip("กรอบ 2px ของคนที่เลือกแล้ว — Teal ตาม design token")]
        public Color pickedColor  = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
        [Tooltip("กรอบของคนที่ยังไม่เลือก")]
        public Color waitingColor = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("ตัวหนังสือของคนที่ยังไม่เลือก")]
        public Color waitingTextColor = new Color(1f, 1f, 1f, 0.55f);

        [Header("── Blink ──────────────────────────────")]
        [Tooltip("รอบการกะพริบของคนที่ยังไม่เลือก (วินาที) — design handoff ระบุ 1.1")]
        public float blinkPeriod = 1.1f;
        [Range(0f, 1f)] public float blinkMinAlpha = 0.5f;

        [Tooltip("ซ่อนทั้งแถบเมื่อเล่นคนเดียว — solo ไม่มีใครให้รอ")]
        public bool hideWhenSolo = true;

        [Tooltip("รูปแบบข้อความ · {0} = จำนวนที่เลือกแล้ว · {1} = ทั้งหมด")]
        public string countFormat = "รอผู้เล่น {0} / {1}";

        // ── runtime ────────────────────────────────────────────────────────
        private readonly List<LevelUpDogTag> tags = new();
        private int pickedCount;
        private int totalCount;

        private void Awake()
        {
            if (tagTemplate != null) tagTemplate.gameObject.SetActive(false);
        }

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>เรียกจาก LevelUpUI ทุกครั้งที่ OnPickedCountChanged ยิง</summary>
        public void SetCounts(int picked, int total)
        {
            pickedCount = Mathf.Max(0, picked);
            totalCount  = Mathf.Max(0, total);

            bool solo = totalCount <= 1;
            if (countLabel != null)
            {
                countLabel.enabled = !(solo && hideWhenSolo);
                if (countLabel.enabled)
                    countLabel.text = string.Format(countFormat, pickedCount, totalCount);
            }

            EnsureTags(solo && hideWhenSolo ? 0 : totalCount);

            for (int i = 0; i < tags.Count; i++)
            {
                // เติมจากซ้ายไปขวา — ดูข้อจำกัดที่หัวคลาส
                bool done = i < pickedCount;
                tags[i].SetState(done, done ? pickedColor : waitingColor,
                                       done ? pickedColor : waitingTextColor);
            }
        }

        /// <summary>เคลียร์ตอนเปิดจอใหม่ — ไม่งั้นรอบก่อนจะค้างเป็นเขียวทั้งแถว</summary>
        public void ResetAll() => SetCounts(0, totalCount);

        // ═══════════════════════════════════════════════════════════════════
        private void EnsureTags(int wanted)
        {
            if (tagTemplate == null || tagArea == null) return;

            // เพิ่มเท่าที่ขาด แล้วปิดตัวเกิน — ไม่ destroy เพราะห้องเปลี่ยนขนาดได้ระหว่างรอบ
            while (tags.Count < wanted)
            {
                int i   = tags.Count;
                var tag = Instantiate(tagTemplate, tagArea);
                tag.name = $"DogTag_{i}";

                var rt = (RectTransform)tag.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(tagSize, tagSize);
                rt.anchoredPosition = new Vector2(i * (tagSize + tagGap), 0f);

                tag.SetLabel($"P{i + 1}");
                tags.Add(tag);
            }

            for (int i = 0; i < tags.Count; i++)
                tags[i].gameObject.SetActive(i < wanted);
        }

        private void Update()
        {
            if (blinkPeriod <= 0f) return;

            // sin ครบรอบทุก blinkPeriod วินาที · แมปจาก [-1,1] ไป [blinkMinAlpha,1]
            float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / blinkPeriod) * 0.5f + 0.5f;
            float alpha = Mathf.Lerp(blinkMinAlpha, 1f, phase);

            for (int i = 0; i < tags.Count; i++)
            {
                if (!tags[i].gameObject.activeSelf) continue;
                tags[i].SetAlpha(i < pickedCount ? 1f : alpha);
            }
        }
    }

    /// <summary>
    /// ป้าย dog-tag หนึ่งใบ — ที่เก็บ reference ให้ <see cref="LevelUpDogTagUI"/> สั่ง
    /// แยกเป็นคลาสเพราะป้ายถูก clone จาก template ในซีน reference จึงต้องติดกับ GameObject
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelUpDogTag : MonoBehaviour
    {
        [Tooltip("พื้นป้าย")]
        public Image background;
        [Tooltip("กรอบ 2px สี่ด้าน เรียง บน/ล่าง/ซ้าย/ขวา")]
        public Image[] borderEdges = new Image[4];
        [Tooltip("ตัวหนังสือในป้าย (P1 / P2 …)")]
        public TextMeshProUGUI label;
        [Tooltip("ใช้คุม opacity ตอนกะพริบ — ถูกเพิ่มโดย builder")]
        public CanvasGroup group;

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetState(bool picked, Color border, Color textColor)
        {
            foreach (var e in borderEdges)
            {
                if (e == null) continue;
                e.color = border;
            }
            if (label != null) label.color = textColor;
            if (background != null)
                // คนที่เลือกแล้วได้พื้นเขียวจางๆ เพิ่ม เพื่อให้แยกออกแม้มองผ่านๆ
                background.color = picked
                    ? new Color(border.r, border.g, border.b, 0.14f)
                    : new Color(1f, 1f, 1f, 0.05f);
        }

        public void SetAlpha(float a)
        {
            if (group != null) group.alpha = a;
        }
    }
}
