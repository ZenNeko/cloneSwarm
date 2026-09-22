using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ป้ายที่ข้อความมาจากตารางแปล แทนที่จะพิมพ์ไว้ในซีน
    ///
    /// ═══ ทำไมไม่ใช้ LocalizeStringEvent ของ Unity ═══
    ///
    /// ตัวนั้นทำงานได้และเป็นของมาตรฐาน · แต่ผูกผ่าน UnityEvent ที่ต้องเก็บ
    /// persistent listener ลงซีน ซึ่งอ่านใน diff ไม่ออกเลยว่าป้ายไหนชี้ key อะไร
    /// และเวลาพังจะพังแบบ "ไม่มีอะไรเกิดขึ้น"
    ///
    /// ที่สำคัญกว่าคือข้อความตอนหา key ไม่เจอ — ของ Unity เขียนว่า
    /// `No translation found for '...'` ซึ่งไม่บอกว่าต้องไปแก้ที่ไหน · ตัวนี้บอก
    /// ชื่อเมนูที่ต้องรันตรงๆ ตามกติกาของโปรเจกต์ที่ว่าความล้มเหลวต้องบอกทางออกด้วย
    ///
    /// ═══ ต้องตามตอนสลับภาษา ไม่ใช่อ่านครั้งเดียวตอนเปิด ═══
    ///
    /// `PlayerPrefLocaleSelector` มีอยู่ในโปรเจกต์ แปลว่าวันหนึ่งจะมีปุ่มสลับภาษา
    /// ป้ายที่อ่านครั้งเดียวตอน OnEnable จะค้างเป็นภาษาเดิมจนกว่าจะปิดเปิดจอใหม่
    /// — ดูเหมือนบางจอแปลไม่ครบ ทั้งที่ตารางมีครบ
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class P3RLocalizedText : MonoBehaviour
    {
        [Tooltip("ชื่อตารางใน Localization · ปกติคือ UI")]
        public string table = "UI";

        [Tooltip("key ในตาราง เช่น ui.pause.title")]
        public string key = "";

        private TMP_Text label;

        private void Awake() => label = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            Refresh();
        }

        private void OnDisable()
            => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        private void OnLocaleChanged(Locale _) => Refresh();

        /// <summary>
        /// อ่านข้อความจากตารางมาใส่ป้าย — หาไม่เจอให้โชว์ key แล้วบ่น
        ///
        /// โชว์ key ไม่ใช่ปล่อยว่าง เพราะจอที่มีช่องว่างอ่านไม่ออกว่าตั้งใจหรือพัง
        /// ส่วน key ที่โผล่มาบนจอบอกทั้งว่าพังและพังที่ไหน
        /// </summary>
        public void Refresh()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label == null || string.IsNullOrEmpty(key)) return;

            label.text = P3RStrings.Resolve(table, key, P3RStrings.UiFixHint);
        }
    }
}
