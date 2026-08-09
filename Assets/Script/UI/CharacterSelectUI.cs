using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Character Select UI — ใช้ Inspector references ทั้งหมด
///
/// Left Panel:
///   cardsContainer → Content ใน Scroll View
///   cardTemplate   → CharacterCard prefab (SetActive = false)
///
/// Right Panel:
///   Portrait row   → detailPortrait, detailName, detailDesc
///   Passive row    → detailPassiveIcon, detailPassiveName, detailPassiveDesc
///   Weapon row     → detailWeaponIcon, detailWeaponName, detailWeaponDesc
///   Ability row    → detailAbilityIcon, detailAbilityName, detailAbilityDesc
///   Ultimate row   → detailUltimateIcon, detailUltimateName, detailUltimateDesc
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    // ── Static (อ่านโดย PlayerWeaponManager ข้าม scene) ──────────────────
    public static CharacterData SelectedCharacter { get; private set; }
    public static event System.Action<CharacterData> OnCharacterConfirmed;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Data ────────────────────────────────")]
    public List<CharacterData> characters = new();

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Card List (Left Panel) ─────────────")]
    [Tooltip("Content ใน Scroll View")]
    public Transform  cardsContainer;
    [Tooltip("Card template — SetActive = false ไว้")]
    public GameObject cardTemplate;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Portrait Row ────────────────────────")]
    public Image           detailPortrait;
    public TextMeshProUGUI detailName;
    public TextMeshProUGUI detailDesc;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Passive Row ─────────────────────────")]
    public Image           detailPassiveIcon;
    public TextMeshProUGUI detailPassiveName;
    public TextMeshProUGUI detailPassiveDesc;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Weapon Row ──────────────────────────")]
    public Image           detailWeaponIcon;
    public TextMeshProUGUI detailWeaponName;
    public TextMeshProUGUI detailWeaponDesc;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Ability Row (Q) ─────────────────────")]
    public Image           detailAbilityIcon;
    public TextMeshProUGUI detailAbilityName;
    public TextMeshProUGUI detailAbilityDesc;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Ultimate Row (E/R) ──────────────────")]
    public Image           detailUltimateIcon;
    public TextMeshProUGUI detailUltimateName;
    public TextMeshProUGUI detailUltimateDesc;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Buttons ─────────────────────────────")]
    public Button confirmButton;
    [Tooltip("Text บนปุ่ม Confirm — สลับเป็น \"ปลดล็อก 1,000 G\" เมื่อเลือกตัวที่ยังล็อก")]
    public TextMeshProUGUI confirmButtonLabel;
    [Tooltip("ข้อความบอกสถานะ เช่น \"ทองไม่พอ\" — ปล่อยว่างได้")]
    public TextMeshProUGUI lockStatusText;

    [Header("── Gold ────────────────────────────────")]
    [Tooltip("ยอดทองปัจจุบัน — ปล่อยว่างได้")]
    public TextMeshProUGUI goldText;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Card Colors ─────────────────────────")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);

    // ── Internal ──────────────────────────────────────────────────────────
    private CharacterData         selected;
    private List<CharacterCardUI> spawnedCards = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (SelectedCharacter == null)
            SelectedCharacter = FirstUnlocked();
    }

    void Start()
    {
        BuildCards();
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);

        var start = SelectedCharacter;
        if (start == null || !CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(start))
            start = FirstUnlocked() ?? (characters.Count > 0 ? characters[0] : null);

        SelectCharacter(start);
    }

    /// <summary>ตัวละครตัวแรกที่ปลดล็อกแล้ว — null ถ้าไม่มีเลย (ควรมีอย่างน้อย 1 ตัว unlockedByDefault)</summary>
    CharacterData FirstUnlocked()
    {
        foreach (var cd in characters)
            if (cd != null && CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(cd)) return cd;
        return null;
    }

    // ── Build Cards ───────────────────────────────────────────────────────
    void BuildCards()
    {
        // ── Debug checks ──────────────────────────────────────────────────
        if (cardsContainer == null)
        {
            Debug.LogError("[CharSelect] cardsContainer ยังไม่ได้ assign! ลาก Content (ใน Scroll View) มาใส่");
            return;
        }
        if (cardTemplate == null)
        {
            Debug.LogError("[CharSelect] cardTemplate ยังไม่ได้ assign! ลาก CharacterCard template มาใส่");
            return;
        }
        if (characters.Count == 0)
        {
            Debug.LogWarning("[CharSelect] characters list ว่าง — ลาก CharacterData assets มาใส่ใน Inspector");
            return;
        }

        // ซ่อน template (ใช้เป็นต้นแบบเท่านั้น)
        cardTemplate.SetActive(false);

        // ลบ card เก่าออก (กัน duplicate ถ้า BuildCards ถูกเรียกซ้ำ)
        foreach (var old in spawnedCards)
            if (old != null) Destroy(old.gameObject);
        spawnedCards.Clear();

        for (int i = 0; i < characters.Count; i++)
        {
            var cd  = characters[i];
            if (cd == null) continue;
            var idx = i;

            var go  = Instantiate(cardTemplate, cardsContainer);
            go.name = $"Card_{cd.characterName}";
            go.SetActive(true);

            var card = go.GetComponent<CharacterCardUI>();
            if (card == null) card = go.AddComponent<CharacterCardUI>();
            card.SetData(cd, selectedColor, normalColor);

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectCharacter(characters[idx]));

            spawnedCards.Add(card);
        }

        Debug.Log($"[CharSelect] สร้าง {spawnedCards.Count} cards สำเร็จ");
    }

    // ── Select ────────────────────────────────────────────────────────────
    void SelectCharacter(CharacterData cd)
    {
        selected = cd;
        RefreshDetail();
        RefreshCardHighlights();
        RefreshLockState();

        if (cd != null && CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(cd))
        {
            SelectedCharacter = cd;
            OnCharacterConfirmed?.Invoke(cd);
        }
    }

    /// <summary>
    /// ปุ่ม Confirm ทำหน้าที่ปลดล็อกตัวละครด้วยทองเมื่อยังไม่ได้ปลดล็อก
    /// </summary>
    void OnConfirm()
    {
        if (selected == null) return;

        if (!CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(selected))
        {
            if (CloneSwarm.Meta.MetaProgression.TryUnlockCharacter(selected))
            {
                RefreshCardLocks();
                RefreshLockState();
                SelectedCharacter = selected;
                OnCharacterConfirmed?.Invoke(selected);
            }
            else if (lockStatusText != null)
            {
                lockStatusText.text = $"ทองไม่พอ — ต้องการ {selected.unlockCost:N0} G";
            }
        }
    }

    // ── Lock / Gold display ───────────────────────────────────────────────
    void RefreshLockState()
    {
        if (goldText != null)
            goldText.text = $"{CloneSwarm.Meta.MetaProgression.Gold:N0} G";

        if (selected == null) return;

        bool unlocked = CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(selected);

        if (confirmButtonLabel != null)
            confirmButtonLabel.text = $"ปลดล็อก {selected.unlockCost:N0} G";

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(!unlocked);
            confirmButton.interactable = !unlocked && CloneSwarm.Meta.MetaProgression.CanUnlockCharacter(selected);
        }

        if (lockStatusText != null)
            lockStatusText.text = unlocked ? "" : selected.description;
    }

    void RefreshCardLocks()
    {
        for (int i = 0; i < spawnedCards.Count && i < characters.Count; i++)
        {
            var cd = characters[i];
            if (cd == null || spawnedCards[i] == null) continue;
            spawnedCards[i].SetLocked(
                !CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(cd), cd.unlockCost);
        }
    }

    // ── Refresh Detail Panel ──────────────────────────────────────────────
    void RefreshDetail()
    {
        if (selected == null) return;

        // Portrait row
        SetImage(detailPortrait,   selected.portrait);
        SetText(detailName,        selected.characterName);
        SetText(detailDesc,        selected.description);

        // Passive row
        SetImage(detailPassiveIcon, selected.passiveIcon);
        SetText(detailPassiveName,  selected.passiveName);
        SetText(detailPassiveDesc,  selected.passiveDescription);

        // Weapon row
        Sprite wIcon = null;
        string wName = "—";
        string wDesc = "";

        if (selected.startingWeapon != null)
        {
            wIcon = selected.startingWeapon.icon;
            wName = selected.startingWeapon.weaponName;
            wDesc = selected.startingWeapon.description;
        }
        SetImage(detailWeaponIcon, wIcon);
        SetText(detailWeaponName,  wName);
        SetText(detailWeaponDesc,  wDesc);

        // Ability row (Q)
        AbilityData qAbility = null;
        if (selected.abilities != null)
        {
            qAbility = System.Array.Find(selected.abilities, a => a != null && a.slotType == AbilitySlotType.Q);
            if (qAbility == null && selected.abilities.Length > 0) qAbility = selected.abilities[0];
        }

        Sprite qIcon = qAbility != null ? qAbility.icon : null;
        string qName = qAbility != null ? qAbility.abilityName : "";
        string qDesc = qAbility != null ? qAbility.description : "";

        SetImage(detailAbilityIcon, qIcon);
        SetText(detailAbilityName,  qName);
        SetText(detailAbilityDesc,  qDesc);

        // Ultimate row (E/R)
        AbilityData ultAbility = null;
        if (selected.abilities != null)
        {
            ultAbility = System.Array.Find(selected.abilities, a => a != null && a.slotType == AbilitySlotType.E);
            if (ultAbility == null && selected.abilities.Length > 1) ultAbility = selected.abilities[1];
        }

        Sprite ultIcon = ultAbility != null ? ultAbility.icon : null;
        string ultName = ultAbility != null ? ultAbility.abilityName : "";
        string ultDesc = ultAbility != null ? ultAbility.description : "";

        SetImage(detailUltimateIcon, ultIcon);
        SetText(detailUltimateName,  ultName);
        SetText(detailUltimateDesc,  ultDesc);
    }

    void RefreshCardHighlights()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (i >= characters.Count) continue;
            spawnedCards[i].SetSelected(characters[i] == selected);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    static void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp == null) return;
        tmp.text = text ?? "";
    }

    static void SetImage(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite  = sprite;
        img.enabled = sprite != null;
    }
}
