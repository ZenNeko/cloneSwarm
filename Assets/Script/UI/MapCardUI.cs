using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// การ์ดแมพหนึ่งใบในวงหมุน — คู่ขนานกับ CharacterCardUI แต่ข้อมูลมาจาก MapData
///
/// ทุกช่องเป็น optional ปล่อยว่างได้หมด auto-find จะหาจากชื่อลูกให้ก่อน
/// ถ้าอยากวาง mask ให้ภาพล้นกรอบตอนอยู่กลาง ใส่ RectMask2D บน wrapper ที่ครอบ previewImage
/// (CarouselBase ค้นทั้งกิ่ง ไม่จำเป็นต้องอยู่บน root)
/// </summary>
public class MapCardUI : MonoBehaviour
{
    [Header("Refs (ปล่อยว่าง = auto-find จากชื่อลูก)")]
    [Tooltip("พื้นหลังการ์ด — สลับสีตอนอยู่กลาง")]
    public Image           bgImage;
    [Tooltip("ภาพพรีวิวแมพ — ลูกชื่อ Preview หรือ Icon")]
    public Image           previewImage;
    [Tooltip("ชื่อแมพ — ลูกชื่อ NameText")]
    public TextMeshProUGUI nameText;

    [Header("Colors")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);

    void Awake()
    {
        if (bgImage == null) bgImage = GetComponent<Image>();
        if (previewImage == null)
            previewImage = transform.Find("Preview")?.GetComponent<Image>()
                        ?? transform.Find("Icon")?.GetComponent<Image>();
        if (nameText == null) nameText = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
    }

    public void SetData(MapData map, Color selColor, Color normColor)
    {
        selectedColor = selColor;
        normalColor   = normColor;

        if (map == null) return;

        if (nameText != null) nameText.text = map.DisplayName;

        if (previewImage != null)
        {
            previewImage.sprite  = map.previewImage;
            previewImage.enabled = map.previewImage != null;
        }
        // ไม่ตั้งสถานะเลือกที่นี่ — ผู้เรียกสั่ง SetSelected ด้วยค่าจริงต่อทันทีเสมอ
    }

    public void SetSelected(bool on)
    {
        if (bgImage != null)
            bgImage.color = on ? selectedColor : normalColor;
    }
}
