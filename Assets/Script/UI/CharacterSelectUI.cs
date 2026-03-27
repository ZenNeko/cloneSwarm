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
        if (characters.Count > 0 && SelectedCharacter == null)
            SelectedCharacter = characters[0];
    }

    void Start()
    {
        BuildCards();
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        SelectCharacter(SelectedCharacter ?? (characters.Count > 0 ? characters[0] : null));
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
    }

    void OnConfirm()
    {
        if (selected == null) return;
        SelectedCharacter = selected;
        OnCharacterConfirmed?.Invoke(selected);
        Debug.Log($"[CharSelect] ✅ {selected.characterName}");
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
        string wName = selected.startingWeapon != null ? selected.startingWeapon.weaponName : "—";
        Sprite wIcon = selected.weaponIcon
                    ?? (selected.startingWeapon != null ? selected.startingWeapon.icon : null);
        SetImage(detailWeaponIcon, wIcon);
        SetText(detailWeaponName,  string.IsNullOrEmpty(selected.weaponAbilityName)
                                   ? wName : selected.weaponAbilityName);
        SetText(detailWeaponDesc,  selected.weaponAbilityDescription);

        // Ability row
        SetImage(detailAbilityIcon, selected.abilityIcon);
        SetText(detailAbilityName,  selected.abilityName);
        SetText(detailAbilityDesc,  selected.abilityDescription);

        // Ultimate row
        SetImage(detailUltimateIcon, selected.ultimateIcon);
        SetText(detailUltimateName,  selected.ultimateName);
        SetText(detailUltimateDesc,  selected.ultimateDescription);
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
