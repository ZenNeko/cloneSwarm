using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หนึ่งบรรทัดของแถบ Evolution Synergy บนการ์ดอัปเกรด
///
/// ═══ ทำไมต้องเป็นคลาสของตัวเองในไฟล์ของตัวเอง ═══
///
/// Unity ผูก script reference ในซีน/prefab ให้เฉพาะคลาสที่ **ชื่อตรงกับชื่อไฟล์**
/// คลาส MonoBehaviour ตัวที่สองในไฟล์เดียวกันได้ reference ที่ไม่รอดการก๊อปข้ามซีน
/// (`BuildStripSlot` เคยกลายเป็น `m_Script: {fileID: 0}` ทั้งสิบ object ด้วยเหตุนี้)
///
/// ═══ หน้าที่ ═══
///
/// เป็นแค่ที่เก็บ reference ให้ <see cref="UpgradeCardUI"/> สั่ง ไม่มีตรรกะของตัวเอง
/// ข้อความที่ได้รับมาเป็น **ข้อเท็จจริงล้วน** — ระบบ "แนะนำ" ถูกถอดออกไปแล้ว (ADR-009)
/// ที่นี่จึงไม่มีสถานะ "ใบที่ควรกด" ให้ย้อมสี มีแค่ "เงื่อนไขนี้ครบ/ยังไม่ครบ"
/// </summary>
[DisallowMultipleComponent]
public class SynergyLineUI : MonoBehaviour
{
    [Tooltip("รูปของที่เกี่ยว — ซ่อนทั้งช่องเมื่อไม่มีรูป")]
    public Image           icon;
    [Tooltip("ชื่อ เช่น 'Laser' หรือ 'Armor'")]
    public TextMeshProUGUI label;
    [Tooltip("สภาพตอนนี้ เช่น 'Lv5 · ขาด Armor อีก 2'")]
    public TextMeshProUGUI detail;

    public void SetData(SynergyLine data, Color metColor, Color pendingColor)
    {
        if (icon != null)
        {
            // **Image ที่ไม่มี sprite วาดสี่เหลี่ยมทึบ ไม่ได้วาดเปล่า** — ต้องปิด enabled
            // ไม่ใช่แค่ปล่อย sprite เป็น null ไม่งั้นได้กล่องขาวแทนที่จะได้ที่ว่าง
            icon.sprite  = data.icon;
            icon.enabled = data.icon != null;
            icon.color   = Color.white;   // งานศิลป์ห้ามย้อม
        }

        if (label != null)
        {
            label.text = data.label ?? "";
            label.gameObject.SetActive(!string.IsNullOrEmpty(data.label));
        }

        if (detail != null)
        {
            detail.text  = data.detail ?? "";
            detail.color = data.met ? metColor : pendingColor;
            detail.gameObject.SetActive(!string.IsNullOrEmpty(data.detail));
        }
    }
}
