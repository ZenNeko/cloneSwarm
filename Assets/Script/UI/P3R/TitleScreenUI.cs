using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// จอ Title — รอผู้เล่นกดอะไรก็ได้แล้วไปต่อ
    ///
    /// **ยังไม่ได้ตัดสินว่าเป็นซีนแยกหรือ panel** — ตัวนี้ทำงานได้ทั้งสองแบบ
    /// เพราะมันไม่ได้โหลดซีนเอง แค่ยิง <see cref="OnAdvance"/> ออกไป
    ///   เป็น panel ใน `MenuScene` → ต่อ event เข้า `MenuManager.ShowPanel(mainPanel)`
    ///   เป็นซีนแยก → ต่อเข้า `SceneManager.LoadScene("MenuScene")`
    /// ข้อเสนอคือแบบ panel เพราะ `MenuManager` มีระบบสลับ panel อยู่แล้ว
    /// และไม่ต้องเสียเวลาโหลดซีนเพิ่มอีกหนึ่งจังหวะก่อนถึงเมนูหลัก
    ///
    /// ใช้ Input System อย่างเดียว (`activeInputHandler = 1` · ไม่มี `Input.anyKeyDown`)
    /// ทุกอย่างใช้ <c>unscaledDeltaTime</c> เผื่อจอนี้ถูกเปิดตอน timeScale ยังไม่ใช่ 1
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleScreenUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("ข้อความ PRESS ANY KEY — ตัวที่หายใจเข้าออก")]
        public CanvasGroup promptGroup;

        public TextMeshProUGUI versionLabel;

        [Header("── Prompt pulse ───────────────────────")]
        public float pulseMin    = 0.35f;
        public float pulseMax    = 1f;
        public float pulseSpeed  = 1.6f;

        [Header("── Gate ───────────────────────────────")]
        [Tooltip("หน่วงสั้นๆ ก่อนเริ่มรับปุ่ม — กันปุ่มที่ค้างมาจากจอก่อนหน้าทะลุมายิงทันที")]
        public float inputDelay = 0.35f;

        [Header("── Events ─────────────────────────────")]
        public UnityEvent onAdvanceEvent;

        /// <summary>ยิงครั้งเดียวตอนผู้เล่นกดอะไรก็ได้</summary>
        public event Action OnAdvance;

        private float elapsed;
        private bool  fired;

        private void OnEnable()
        {
            elapsed = 0f;
            fired   = false;
            if (versionLabel != null)
                P3RText.SetTextAndTracking(versionLabel, $"v{Application.version}", 18f);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;

            if (promptGroup != null)
            {
                float t = (Mathf.Sin(elapsed * pulseSpeed) + 1f) * 0.5f;
                promptGroup.alpha = Mathf.Lerp(pulseMin, pulseMax, t);
            }

            if (fired || elapsed < inputDelay) return;
            if (!AnyPressed()) return;

            fired = true;
            OnAdvance?.Invoke();
            onAdvanceEvent?.Invoke();
        }

        /// <summary>
        /// "อะไรก็ได้" = ปุ่มคีย์บอร์ด · คลิกเมาส์ · ปุ่มจอย
        /// จงใจไม่รับการ **ขยับ** เมาส์หรือ analog stick — คนขยับเมาส์โดยไม่ตั้งใจได้ง่าย
        /// แล้วจอจะกระโดดข้ามไปเองโดยผู้เล่นยังไม่ทันอ่าน
        /// </summary>
        private static bool AnyPressed()
        {
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;

            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame ||
                              m.rightButton.wasPressedThisFrame)) return true;

            var g = Gamepad.current;
            if (g != null && (g.buttonSouth.wasPressedThisFrame ||
                              g.startButton.wasPressedThisFrame)) return true;

            return false;
        }
    }
}
