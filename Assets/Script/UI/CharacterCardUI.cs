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

    private Color selectedColor = new Color(0.3f, 0.7f, 1f);
    private Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);
    private bool  isSelected;

    // ── Init ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // Auto-find ถ้าไม่ได้ assign
        if (bgImage  == null) bgImage  = GetComponent<Image>();
        if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (nameText  == null) nameText  = transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        if (weaponText == null) weaponText = transform.Find("WeaponText")?.GetComponent<TextMeshProUGUI>();
    }

    // ── Setup ─────────────────────────────────────────────────────────────
    public void SetData(CharacterData cd, Color selColor, Color normColor)
    {
        selectedColor = selColor;
        normalColor   = normColor;

        if (nameText != null)
            nameText.text = cd.characterName;

        if (weaponText != null)
        {
            string wName = cd.startingWeapon != null ? cd.startingWeapon.weaponName : "—";
            weaponText.text = wName;
        }

        if (iconImage != null)
        {
            var spr = cd.icon ?? cd.portrait;
            iconImage.sprite  = spr;
            iconImage.enabled = spr != null;
        }

        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        isSelected = on;
        if (bgImage != null)
            bgImage.color = on ? selectedColor : normalColor;
    }
}
