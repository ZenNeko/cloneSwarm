using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// จอโหลดสไตล์ P3R — ภาพแมพเต็มจอ · หัวเรื่องสองบรรทัด · แถบความคืบหน้า · TIP ล่างจอ
    ///
    /// **แถบความคืบหน้า: ทำไมถึงเป็นแบบกวาด ไม่ใช่เปอร์เซ็นต์**
    /// ซีนเกมโหลดผ่าน <c>NetworkManager.SceneManager.LoadScene()</c> (ดู `GameSessionManager`)
    /// ซึ่งรายงานเป็น <c>OnSceneEvent</c> เป็นช่วงๆ — Load แล้วก็ LoadComplete — **ไม่มีค่า
    /// ความคืบหน้าต่อเนื่องให้ดึง** ต่างจาก <c>SceneManager.LoadSceneAsync().progress</c>
    ///
    /// แถบที่วิ่งตามเลขปลอมคือการโกหกผู้เล่นเรื่องเวลาที่เหลือ จึงเลือกแบบกวาดไปมา
    /// ซึ่งสื่อว่า "ยังทำงานอยู่" โดยไม่อ้างว่ารู้ว่าเหลืออีกเท่าไร
    /// วันที่มีแหล่งความคืบหน้าจริงแล้วให้เรียก <see cref="SetProgress"/> — มันสลับเป็นโหมด
    /// เดินหน้าอย่างเดียวให้เอง
    ///
    /// **ที่ยังไม่ได้ตัดสิน:** TIP รายตัวละคร/รายแมพยังไม่มีที่เก็บในข้อมูล
    /// ตอนนี้อ่านจาก <see cref="fallbackTips"/> บน component · ถ้าจะทำจริงต้องเลือกระหว่าง
    /// เพิ่มฟิลด์ใน `CharacterData` (แตะทุกไฟล์ Char_*) หรือทำ SO รวมชุดทิปแยกต่างหาก
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("── Art ────────────────────────────────")]
        [Tooltip("ภาพแมพเต็มจอ · ตอนนี้ยืม MapData.previewImage มาใช้ก่อน\n" +
                 "แบบขอภาพ art เฉพาะจอโหลดซึ่งสัดส่วนคนละแบบกับภาพพรีวิวในจอเลือกแมพ")]
        public Image artImage;

        [Header("── Text ───────────────────────────────")]
        public TextMeshProUGUI contextLabel;   // ARENA 01 · NORMAL
        public TextMeshProUGUI headlineTop;    // NOW
        public TextMeshProUGUI headlineBottom; // LOADING
        public TextMeshProUGUI tipLabel;
        public TextMeshProUGUI skipLabel;

        [Header("── Progress ───────────────────────────")]
        public RectTransform progressTrack;
        public RectTransform progressFill;

        [Tooltip("กวาดไปมาแทนการโชว์เปอร์เซ็นต์ — ค่าเริ่มต้นเพราะ NGO ไม่มี progress ให้ดึง\n" +
                 "SetProgress() จะปิดโหมดนี้ให้เองเมื่อมีค่าจริงมาป้อน")]
        public bool  indeterminate = true;
        [Tooltip("เวลาที่ใช้กวาดครบหนึ่งรอบ (วินาที)")]
        public float sweepDuration = 1.15f;
        [Tooltip("ความยาวของช่วงที่วิ่ง เทียบกับความยาวรางทั้งหมด")]
        [Range(0.05f, 0.9f)] public float sweepWidth = 0.28f;

        [Header("── Tips ───────────────────────────────")]
        [Tooltip("สุ่มมาโชว์หนึ่งอันตอนเปิดจอ · ปุ่ม SKIP เปลี่ยนไปอันถัดไป")]
        [TextArea] public string[] fallbackTips =
        {
            "Riven สะสมพลังจากการเคลื่อนที่ — ยืนนิ่งคือทิ้งพาสซีฟของเธอไปเปล่าๆ",
            "ศัตรูเข้ามาจากทุกขอบของสนาม การขยับสำคัญกว่าการหาที่กำบัง",
            "อาวุธ Super สองชิ้นที่เข้าคู่กันได้ จะรวมร่างเป็นอาวุธ Fusion",
        };

        [Tooltip("ระยะห่างตัวอักษรของ TIP — ข้อความไทยจะถูกบังคับเป็น 0 ให้เองโดย P3RText")]
        public float tipTracking = 0f;

        // ── runtime ────────────────────────────────────────────────────────
        private float sweepT;
        private int   tipIndex = -1;

        private void OnEnable()
        {
            NextTip();
            sweepT = 0f;
            if (indeterminate) ApplySweep(0f);
        }

        private void Update()
        {
            if (!indeterminate || progressFill == null) return;

            // unscaledDeltaTime — จอนี้ขึ้นตอนที่ timeScale อาจถูกหยุดไว้อยู่
            sweepT += Time.unscaledDeltaTime / Mathf.Max(0.05f, sweepDuration);
            if (sweepT >= 1f) sweepT -= 1f;
            ApplySweep(sweepT);
        }

        /// <summary>
        /// ป้อนความคืบหน้าจริง 0..1 — เรียกแล้วเลิกกวาด เปลี่ยนเป็นแถบเดินหน้าอย่างเดียว
        /// </summary>
        public void SetProgress(float value01)
        {
            indeterminate = false;
            if (progressFill == null) return;
            float v = Mathf.Clamp01(value01);
            progressFill.anchorMin = new Vector2(0f, 0f);
            progressFill.anchorMax = new Vector2(v, 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }

        /// <summary>ตั้งบรรทัดบอกบริบท — "ARENA 01 · NORMAL"</summary>
        public void SetContext(string mapName, string difficulty, float tracking = 26f)
        {
            if (contextLabel == null) return;
            string text = string.IsNullOrEmpty(difficulty)
                        ? mapName
                        : $"{mapName} · {difficulty}";
            // ผ่าน P3RText เสมอ — ชื่อแมพเป็นข้อความที่แปลแล้ว จึงเป็นไทยได้
            P3RText.SetTextAndTracking(contextLabel, text, tracking);
        }

        /// <summary>ตั้งภาพพื้นหลัง — ปล่อย null แล้วมันซ่อน Image ให้เอง ไม่ทิ้งสี่เหลี่ยมขาวไว้</summary>
        public void SetArt(Sprite sprite)
        {
            if (artImage == null) return;
            artImage.sprite  = sprite;
            artImage.enabled = sprite != null;
        }

        /// <summary>เปลี่ยนไปทิปถัดไป — ปุ่ม SKIP ต่อมาที่นี่</summary>
        public void NextTip()
        {
            if (tipLabel == null || fallbackTips == null || fallbackTips.Length == 0) return;
            tipIndex = tipIndex < 0
                     ? Random.Range(0, fallbackTips.Length)
                     : (tipIndex + 1) % fallbackTips.Length;
            SetTip(fallbackTips[tipIndex]);
        }

        public void SetTip(string tip)
        {
            if (tipLabel == null) return;
            P3RText.SetTextAndTracking(tipLabel, $"TIP — {tip}", tipTracking);
        }

        private void ApplySweep(float t)
        {
            // t เดินจาก 0 ถึง 1 · ช่วงที่วิ่งโผล่จากขอบซ้ายแล้วหายที่ขอบขวา
            float span  = Mathf.Clamp01(sweepWidth);
            float head  = Mathf.Lerp(-span, 1f, t);
            float left  = Mathf.Clamp01(head);
            float right = Mathf.Clamp01(head + span);

            progressFill.anchorMin = new Vector2(left, 0f);
            progressFill.anchorMax = new Vector2(right, 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }
    }
}
