using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component บน CharacterCard prefab
/// Assign ใน Inspector หรือ auto-find ถ้าชื่อ child ตรงกัน
///
/// Card Hierarchy แนะนำ:
///   CharacterCard (Button + Image BG)
///   ├── Icon      (Image)
///   ├── NameText  (TMP)
///   └── WeaponText (TMP)  ← optional
/// </summary>
public class CharacterCardUI : MonoBehaviour
{
    [Header("Card Sub-Elements (auto-find ถ้าไม่ assign)")]
    public Image           bgImage;
    public Image           iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI weaponText;

    [Tooltip("แถบสีบางที่ขอบซ้ายการ์ด — ลูกชื่อ Accent · เปลี่ยนสีตอนถูกเลือก · " +
             "เป็นตัวที่บอกว่าใบไหนถูกเลือกได้ชัดกว่าสีพื้น ซึ่งต่างกันน้อยมากบนพื้นมืด")]
    public Image           accentImage;
    public Color           accentSelected = new Color32(0x2C, 0x3C, 0xFF, 0xFF);
    public Color           accentNormal   = new Color(1f, 1f, 1f, 0.10f);

    [Header("Status Badge (auto-find: child ชื่อ State)")]
    [Tooltip("ป้ายมุมขวาของการ์ด — บอกว่าปลดล็อกแล้วหรือยัง")]
    public TextMeshProUGUI stateText;
    [Tooltip("ข้อความตอนปลดล็อกแล้ว · ปล่อยว่าง = ซ่อนป้าย")]
    public string stateOwnedLabel  = "OWNED";
    [Tooltip("ข้อความตอนยังล็อกอยู่ · ปล่อยว่าง = ซ่อนป้าย")]
    public string stateLockedLabel = "LOCKED";
    public Color  stateOwnedColor  = new Color32(0x2C, 0xC5, 0xA0, 0xFF);
    public Color  stateLockedColor = new Color32(0x8C, 0x86, 0x78, 0xFF);

    [Header("Lock Overlay (auto-find: child ชื่อ LockOverlay / LockCostText)")]
    [Tooltip("แผ่นทึบ + ไอคอนกุญแจ ที่คลุมการ์ดตอนยังไม่ปลดล็อก")]
    public GameObject      lockOverlay;
    [Tooltip("ข้อความราคาบน overlay เช่น \"1,000 G\"")]
    public TextMeshProUGUI lockCostText;

    private Color selectedColor = new Color(0.3f, 0.7f, 1f);
    private Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);
    private bool  isLocked;

    // ── Init ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // Auto-find ถ้าไม่ได้ assign
        if (bgImage  == null) bgImage  = GetComponent<Image>();
        if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (nameText  == null) nameText  = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (weaponText == null) weaponText = transform.Find("WeaponText")?.GetComponent<TextMeshProUGUI>();
        if (accentImage == null) accentImage = transform.Find("Accent")?.GetComponent<Image>();
        if (stateText == null) stateText = transform.Find("State")?.GetComponent<TextMeshProUGUI>();
        if (lockOverlay == null) lockOverlay = transform.Find("LockOverlay")?.gameObject;
        if (lockCostText == null)
            lockCostText = transform.Find("LockCostText")?.GetComponent<TextMeshProUGUI>()
                        ?? lockOverlay?.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    // ── Setup ─────────────────────────────────────────────────────────────
    public void SetData(CharacterData cd, Color selColor, Color normColor)
    {
        selectedColor = selColor;
        normalColor   = normColor;

        if (nameText != null)
            nameText.text = cd.DisplayName;   // ชื่อที่โชว์ ไม่ใช่ characterName ที่เป็น ID

        if (weaponText != null)
        {
            string wName = cd.startingWeapon != null ? cd.startingWeapon.DisplayName : "—";
            weaponText.text = wName;
        }

        if (iconImage != null)
        {
            var spr = cd.icon ?? cd.portrait;
            iconImage.sprite  = spr;
            iconImage.enabled = spr != null;
        }

        SetLocked(!CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(cd), cd.unlockCost);
        // ไม่ตั้งสถานะเลือกที่นี่ — ผู้เรียกสั่ง SetSelected ด้วยค่าจริงต่อทันทีเสมอ
        // ตั้ง false ทิ้งไว้ก่อนทำให้การ์ดใบกลางกะพริบเป็นสีปกติหนึ่งเฟรมตอนถูกผูกใหม่
    }

    public void SetSelected(bool on)
    {
        if (bgImage != null)
            bgImage.color = on ? selectedColor : normalColor;
        if (accentImage != null)
            accentImage.color = on ? accentSelected : accentNormal;
    }

    /// <summary>แสดง/ซ่อน overlay ล็อก + ราคา</summary>
    public void SetLocked(bool locked, int cost)
    {
        isLocked = locked;

        if (lockOverlay  != null) lockOverlay.SetActive(locked);
        if (lockCostText != null) lockCostText.text = locked ? $"{cost:N0} G" : "";

        // ป้ายสถานะ — แม่แบบเขียน "OWNED" ไว้ตายตัว ใบที่ล็อกอยู่จึงเคยขึ้น OWNED ทับ
        // overlay กุญแจของตัวเอง · ไม่มี "COMING SOON" เพราะไม่มีข้อมูลไหนในเกมบอกได้ว่า
        // ตัวละครไหน "กำลังจะมา" — ป้ายที่ไม่มีที่มาคือการบอกผู้เล่นว่ามีระบบที่ยังไม่มีจริง
        if (stateText != null)
        {
            string label = locked ? stateLockedLabel : stateOwnedLabel;
            stateText.text    = label;
            stateText.color   = locked ? stateLockedColor : stateOwnedColor;
            stateText.enabled = !string.IsNullOrEmpty(label);
        }

        // **ไม่ย้อมภาพตัวละคร** — สีของ Image คูณเข้ากับพิกเซลของ sprite
        // ภาพที่ถูกย้อมคือภาพที่ไม่ตรงกับที่คนวาดส่งมา · ความต่างตอนล็อกมาจาก
        // lockOverlay ซึ่งเป็นแผ่นทึบทับทั้งใบอยู่แล้ว ไม่ต้องแตะตัวภาพ
        if (iconImage != null) iconImage.color = Color.white;
    }

}
