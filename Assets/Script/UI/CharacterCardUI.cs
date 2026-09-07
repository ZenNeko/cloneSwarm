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

    [Header("Lock Overlay (auto-find: child ชื่อ LockOverlay / LockCostText)")]
    [Tooltip("แผ่นทึบ + ไอคอนกุญแจ ที่คลุมการ์ดตอนยังไม่ปลดล็อก")]
    public GameObject      lockOverlay;
    [Tooltip("ข้อความราคาบน overlay เช่น \"1,000 G\"")]
    public TextMeshProUGUI lockCostText;
    [Tooltip("สีไอคอนตอนล็อก — ทำให้เป็นเงาดำ")]
    public Color           lockedIconTint = new Color(0.12f, 0.12f, 0.14f, 1f);

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
    }

    /// <summary>แสดง/ซ่อน overlay ล็อก + ราคา</summary>
    public void SetLocked(bool locked, int cost)
    {
        isLocked = locked;

        if (lockOverlay  != null) lockOverlay.SetActive(locked);
        if (lockCostText != null) lockCostText.text = locked ? $"{cost:N0} G" : "";

        // ถ้าไม่มี overlay prefab ก็ยังเห็นความต่างได้จากไอคอนที่มืดลง
        if (iconImage != null)
            iconImage.color = locked ? lockedIconTint : Color.white;
    }

}
