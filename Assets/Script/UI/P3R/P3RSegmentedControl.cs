using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ชุดปุ่มแบ่งช่องตามแบบ — `[Low][Medium][High][Ultra]` · `[English][ไทย]`
    ///
    /// **ตัวนี้ไม่ได้แทนที่ <c>TMP_Dropdown</c> แต่ขับมันอีกที**
    ///
    /// เหตุผล: <c>SettingsMenuUI</c> ต่อสายไว้กับซีน `MenuScene` แล้ว และถือตรรกะที่ไม่อยาก
    /// เขียนซ้ำทั้งหมด — เติมรายการภาษาแบบ async หลัง `LocalizationSettings.InitializationOperation`,
    /// เขียน `PlayerPrefs` ของระดับกราฟิก, จำ locale ข้ามรอบเล่น
    /// ถ้าเปลี่ยน field เป็นปุ่มตรงๆ สายในซีนจะขาดเงียบๆ โดยคอมไพเลอร์จับไม่ได้
    ///
    /// ที่ทำแทน: อ่าน <c>options</c> จาก dropdown มาสร้างปุ่ม แล้วตอนกดก็เขียน <c>value</c>
    /// กลับเข้า dropdown — callback เดิมทั้งหมดยังทำงานเหมือนเดิมทุกอย่าง
    /// dropdown เองซ่อนไว้ (ปิดเฉพาะ graphic ไม่ได้ปิด GameObject เพราะต้องให้ callback ยังวิ่ง)
    ///
    /// รายการภาษาเติมแบบ async จึงต้อง <see cref="Rebuild"/> ใหม่เมื่อ options เปลี่ยน —
    /// เช็คทุกเฟรมด้วยการเทียบจำนวน ซึ่งถูกกว่าการ subscribe อะไรที่ TMP_Dropdown ไม่มีให้
    /// </summary>
    [DisallowMultipleComponent]
    public class P3RSegmentedControl : MonoBehaviour, IP3RSegmentOwner
    {
        [Header("── Source ─────────────────────────────")]
        [Tooltip("dropdown ตัวจริงที่ถือตรรกะไว้ — ตัวนี้แค่ขับมัน")]
        public TMP_Dropdown source;

        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("แม่แบบปุ่มหนึ่งช่อง · ปิดไว้ · builder เป็นคนสร้างให้")]
        public P3RSegmentButton segmentTemplate;

        [Tooltip("พาเรนต์ที่ปุ่มไปเรียงกัน — ปกติเป็นตัวเดียวกับพาเรนต์ของแม่แบบ")]
        public RectTransform segmentContainer;

        [Header("── Layout ─────────────────────────────")]
        public float segmentWidth   = 132f;
        public float segmentHeight  = 44f;
        public float segmentSpacing = 8f;

        [Header("── Type ───────────────────────────────")]
        [Tooltip("ระยะถ่างตัวอักษรของป้ายในปุ่ม (หน่วย TMP) — ข้อความไทยจะถูกบังคับเป็น 0 ให้เอง")]
        public float labelTracking = 8f;

        private readonly List<P3RSegmentButton> segments = new();
        private int builtOptionCount = -1;

        private void OnEnable()
        {
            if (source != null) source.onValueChanged.AddListener(OnSourceChanged);
            Rebuild();
        }

        private void OnDisable()
        {
            if (source != null) source.onValueChanged.RemoveListener(OnSourceChanged);
        }

        private void Update()
        {
            if (source == null) return;
            // รายการภาษาโผล่มาทีหลังตอน Localization init เสร็จ — ต้องสร้างปุ่มใหม่ตอนนั้น
            if (source.options.Count != builtOptionCount) Rebuild();
            else                                          Highlight(source.value);
        }

        /// <summary>สร้างปุ่มใหม่ทั้งชุดจาก options ปัจจุบันของ dropdown</summary>
        public void Rebuild()
        {
            if (source == null || segmentTemplate == null) return;

            var parent = segmentContainer != null
                       ? segmentContainer
                       : (RectTransform)segmentTemplate.transform.parent;
            if (parent == null) return;

            // ล้าง **ทุกลูกที่ไม่ใช่แม่แบบ** ไม่ใช่แค่ที่ตัวเองสร้าง — เพราะ builder วางปุ่มตัวอย่าง
            // ไว้ในซีนให้เห็นหน้าตาตอนยังไม่กด Play (ปุ่มพวกนั้นไม่ได้อยู่ใน segments)
            // ถ้าล้างแค่ของตัวเอง ตอนรันจะได้ปุ่มซ้อนกันสองชุด
            var doomed = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (segmentTemplate != null && child == segmentTemplate.transform) continue;
                doomed.Add(child.gameObject);
            }
            // Immediate เสมอ — Destroy ธรรมดารอจบเฟรม ปุ่มชุดใหม่จะทับชุดเก่าอยู่หนึ่งเฟรม
            foreach (var go in doomed) DestroyImmediate(go);
            segments.Clear();

            var options = source.options;
            builtOptionCount = options.Count;

            for (int i = 0; i < options.Count; i++)
            {
                var seg = Instantiate(segmentTemplate, parent);
                seg.gameObject.SetActive(true);
                seg.name = $"Segment_{i}";

                var rt = (RectTransform)seg.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(segmentWidth, segmentHeight);
                rt.anchoredPosition = new Vector2(i * (segmentWidth + segmentSpacing), 0f);

                seg.Init(this, i, options[i].text, labelTracking);
                segments.Add(seg);
            }

            Highlight(source.value);
        }

        /// <summary>ผู้ใช้กดช่องที่ <paramref name="index"/> — เขียนกลับเข้า dropdown ให้ตรรกะเดิมทำงาน</summary>
        public void Choose(int index)
        {
            if (source == null || index < 0 || index >= source.options.Count) return;
            if (source.value == index) return;
            source.value = index;          // ยิง onValueChanged ของ SettingsMenuUI ตามปกติ
            Highlight(index);
        }

        private void OnSourceChanged(int index) => Highlight(index);

        private void Highlight(int index)
        {
            for (int i = 0; i < segments.Count; i++)
                if (segments[i] != null) segments[i].SetSelected(i == index);
        }
    }
}
