using TMPro;
using UnityEngine;
// GetBindingDisplayString เป็น extension ใน InputActionRebindingExtensions — ต้อง using นี้
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// ชิปสกิลบนจอ Lobby — `[ปุ่ม] ชื่อสกิล`
    ///
    /// **ปุ่มต้องอ่านจาก binding จริง ห้ามเขียนตายตัว**
    /// design handoff วาดไว้เป็น `Q` / `E` ซึ่งล้าสมัยตั้งแต่ commit `28154a4f`
    /// ที่ย้ายสกิลไปคลิกซ้าย/ขวา · ชื่อช่องยังเป็น SlotQ/SlotE/SlotR อยู่ในฐานะ
    /// **ชื่อช่อง** ไม่ใช่ชื่อปุ่ม (ดู CLAUDE.md ข้อ 12)
    ///
    /// ที่นี่จึงถาม <c>AbilityInput.Find(slot)</c> แล้วให้ Input System บอกป้ายเอง
    /// แก้ binding ใน `AbilityInputActions.inputactions` แล้วจอนี้เปลี่ยนตาม
    /// โดยไม่ต้องแก้โค้ดหรือซีน
    ///
    /// ต่างจาก <c>AbilityBase.AbilityKeyLabel</c> ตรงที่ตัวนั้นอ่านจาก component ที่เกิดแล้ว
    /// แต่ในล็อบบี้ตัวละครยังไม่ถูก spawn จึงต้องถามจากชื่อช่องตรงๆ
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyAbilityChipUI : MonoBehaviour
    {
        [Header("── Wiring ─────────────────────────────")]
        public Image           background;
        public TextMeshProUGUI keyLabel;
        public TextMeshProUGUI nameLabel;

        [Tooltip("กล่องปุ่มทั้งก้อน (พื้น + ป้าย) — ซ่อนทั้งก้อนเมื่อสกิลนี้ไม่มีปุ่ม\n" +
                 "ถ้าซ่อนแค่ป้าย พื้นสีน้ำเงินจะค้างเป็นกล่องเปล่าอยู่ข้างชื่อ")]
        public GameObject keyGroup;

        [Header("── Type ───────────────────────────────")]
        [Tooltip("ระยะถ่างของป้ายปุ่ม · ป้ายชื่อสกิลไม่ถ่าง เพราะอาจเป็นภาษาไทย")]
        public float keyTracking = 14f;

        /// <summary>
        /// <paramref name="slotKey"/> คือชื่อช่อง — "Q" / "E" / "R" (ไม่ใช่ชื่อปุ่ม)
        /// ปล่อยว่างได้ถ้าเป็นอาวุธประจำตัวที่ไม่มีปุ่ม เช่นพาสซีฟ
        /// </summary>
        public void Bind(string slotKey, string abilityName)
        {
            string label = ResolveKeyLabel(slotKey);
            bool   hasKey = !string.IsNullOrEmpty(label);

            if (keyGroup != null)                keyGroup.SetActive(hasKey);
            else if (keyLabel != null)           keyLabel.gameObject.SetActive(hasKey);

            if (keyLabel != null && hasKey) P3RText.SetTextAndTracking(keyLabel, label, keyTracking);

            // ชื่อสกิลมาจาก String Table จึงเป็นไทยได้ — ห้ามถ่างระยะ
            if (nameLabel != null) P3RText.SetTextAndTracking(nameLabel, abilityName, 0f);
        }

        /// <summary>คืนป้ายปุ่มจริง · คืนสตริงว่างเมื่อช่องนี้ไม่มีปุ่มผูกอยู่</summary>
        public static string ResolveKeyLabel(string slotKey)
        {
            if (string.IsNullOrEmpty(slotKey)) return "";

            var action = AbilityInput.Find(slotKey);
            if (action == null || action.bindings.Count == 0)
            {
                // ไม่ถือเป็นความผิดพลาด — ช่องที่ยังไม่ผูกปุ่มก็มีได้
                // แต่โชว์ชื่อช่องดีกว่าโชว์ว่าง เพราะอย่างน้อยยังบอกได้ว่าเป็นช่องไหน
                return slotKey;
            }
            return action.GetBindingDisplayString(0);
        }
    }
}
