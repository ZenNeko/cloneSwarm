using UnityEngine;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ม่านรอผู้เล่นในซีนเกม — กาง `P3R_Loading` ไว้จนกว่าทุกคนจะต่อเสร็จ
    ///
    /// ═══ ทำไมต้องมี ═══
    ///
    /// `GameTimeline` รอให้ทุก client มี player object ก่อนค่อยเริ่มนับเวลา
    /// (`TryBeginRun`) แต่ **ไม่มีอะไรบอกผู้เล่นว่ากำลังรออยู่** — คนที่โหลดเสร็จก่อน
    /// เห็นสนามเปล่าๆ ตัวเองยังไม่โผล่ ไม่รู้ว่าเกมค้างหรือกำลังรอเพื่อน
    ///
    /// ═══ ทำไม component อยู่บน HUDCanvas ไม่ใช่บน P3R_Loading ═══
    ///
    /// **เพราะ component บน panel ที่ปิดอยู่ไม่มีวันทำงาน** — `Awake`/`OnEnable`
    /// ไม่วิ่งบน GameObject ที่ปิด · ถ้าเอาไปวางบน `P3R_Loading` ซึ่งเริ่มมาแบบปิด
    /// ม่านจะไม่มีวันกางเลยสักครั้ง
    ///
    /// นี่คือบั๊กเดียวกับที่ `WinLoseUI` เพิ่งโดนมา (แผงปิดตัวเองใน `Awake` แล้ว
    /// `OnEnable` ไม่วิ่ง จอจบเกมจึงไม่เคยขึ้น) · ตัวคุมทุกตัวในซีนนี้อยู่บน
    /// `HUDCanvas` ด้วยเหตุผลเดียวกัน
    ///
    /// ═══ อ่านสัญญาณเดียว ไม่เดาเอง ═══
    ///
    /// ใช้ `GameTimeline.hasStarted` อย่างเดียว — เป็น `NetworkVariable` ที่
    /// server เขียน ทุกคนอ่านได้ จึงเชื่อถือได้เท่ากันทั้ง host และ client
    ///
    /// **ไม่นับหัวเองจาก `NetworkManager.ConnectedClientsList`** เพราะลิสต์นั้น
    /// เต็มเฉพาะฝั่ง server — client ไปอ่านแล้ว NGO log error (เหตุผลเดียวกับที่
    /// `ResultPartyRowUI` เขียนเตือนเรื่อง `GetPlayerNetworkObject` ไว้)
    ///
    /// แถบจึงเป็นการกวาดแบบไม่บอกเปอร์เซ็นต์ (`LoadingScreenUI.indeterminate`)
    /// — ตัวเลขที่ไม่มีแหล่งจริงจะหลอกว่ารู้ทั้งที่ไม่รู้
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayLoadingGate : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("แผง P3R_Loading ในซีนเกม — ตัวนี้เปิด/ปิดให้")]
        public GameObject loadingPanel;

        [Tooltip("ปล่อยว่างได้ — มีไว้ตั้งข้อความบอกว่ากำลังรออะไร")]
        public LoadingScreenUI screen;

        [Header("── ข้อความ ────────────────────────────")]
        public string waitingContext = "กำลังรอผู้เล่นทุกคน";

        [Header("── กันค้าง ────────────────────────────")]
        [Tooltip("ยอมกางม่านได้นานสุดกี่วินาที · ต้องมากกว่า GameTimeline.startWaitTimeout (20)\n" +
                 "กันกรณี GameTimeline ไม่ spawn เลย เช่นเปิดซีนเกมตรงๆ ใน Editor\n" +
                 "ซึ่งถ้าไม่มีตัวนี้ ม่านจะค้างบังจอตลอดกาลโดยไม่มีอะไรฟ้อง")]
        public float safetyTimeout = 25f;

        private float elapsed;
        private bool  covering;

        private void Awake()
        {
            // ตั้งต้นคือ "ยังไม่พร้อม" เสมอ — เริ่มจากกางไว้แล้วค่อยเปิดออก
            // ปลอดภัยกว่าเริ่มจากเปิดแล้วค่อยกาง เพราะถ้าอ่านสัญญาณพลาด
            // อย่างแรกคือรอเก้อ อย่างหลังคือผู้เล่นเห็นสนามเปล่าแล้วงงว่าเกมค้าง
            SetCovering(true);
        }

        private void Update()
        {
            if (!covering) return;

            elapsed += Time.unscaledDeltaTime;   // ม่านต้องเดินแม้เกมถูก pause

            if (RunHasStarted())
            {
                SetCovering(false);
                return;
            }

            if (elapsed >= safetyTimeout)
            {
                Debug.LogWarning(
                    $"[LoadingGate] รอ {safetyTimeout:F0}s แล้ว GameTimeline ยังไม่บอกว่าเริ่ม — " +
                    "เปิดม่านออกก่อน · ถ้าเจอบ่อยแปลว่า GameTimeline ไม่ได้ spawn ในซีนนี้");
                SetCovering(false);
            }
        }

        /// <summary>
        /// `GameTimeline` ยังไม่ spawn = ยังไม่เริ่ม (ไม่ใช่เริ่มแล้ว) —
        /// ตอนโหลดซีนใหม่ๆ `Instance` เป็น null อยู่พักหนึ่งเสมอ
        /// </summary>
        private static bool RunHasStarted()
            => GameTimeline.Instance != null && GameTimeline.Instance.hasStarted.Value;

        private void SetCovering(bool on)
        {
            covering = on;
            if (loadingPanel != null && loadingPanel.activeSelf != on)
                loadingPanel.SetActive(on);

            if (on && screen != null && !string.IsNullOrEmpty(waitingContext))
                screen.SetContext(waitingContext, "");
        }
    }
}
