using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// หน้าเลือกตัวละครก่อนเริ่มเกม
///
/// Setup:
///   1. วาง GameObject ใน MainMenu Scene (หรือ lobby scene)
///   2. Assign characters[] ใน Inspector
///   3. Assign confirmButton + readyButton
///   4. ผลลัพธ์ที่เลือกเก็บไว้ใน CharacterSelectUI.SelectedCharacter
///      — PlayerWeaponManager อ่านค่านี้ใน OnNetworkSpawn
///
/// ถ้า build UI แบบ runtime (ไม่ assign) จะสร้าง Canvas อัตโนมัติ
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    // ── Static selection (อ่านโดย PlayerWeaponManager ข้าม scene) ─────────
    public static CharacterData SelectedCharacter { get; private set; }

    [Header("Data")]
    public List<CharacterData> characters = new();

    [Header("UI References (optional — สร้าง auto ถ้าไม่ assign)")]
    public GameObject        panelRoot;
    public Button            confirmButton;

    // ── Internal ──────────────────────────────────────────────────────────
    private CharacterData   selected;
    private List<Button>    cardButtons = new();
    private TextMeshProUGUI detailName;
    private TextMeshProUGUI detailDesc;
    private TextMeshProUGUI detailPassive;
    private Image           detailPortrait;
    private bool            builtUI;

    // ── Events ────────────────────────────────────────────────────────────
    public static event System.Action<CharacterData> OnCharacterConfirmed;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (characters.Count > 0 && SelectedCharacter == null)
            SelectedCharacter = characters[0];
    }

    void Start()
    {
        if (panelRoot == null) BuildUI();
        else                   PopulateCards();

        SelectCharacter(SelectedCharacter ?? (characters.Count > 0 ? characters[0] : null));
    }

    // ── Public API ────────────────────────────────────────────────────────
    public void Show() { if (panelRoot) panelRoot.SetActive(true); }
    public void Hide() { if (panelRoot) panelRoot.SetActive(false); }

    // ── Select ────────────────────────────────────────────────────────────
    void SelectCharacter(CharacterData cd)
    {
        selected = cd;
        RefreshDetail();
        RefreshCardHighlight();
    }

    void OnConfirm()
    {
        if (selected == null) return;
        SelectedCharacter = selected;
        OnCharacterConfirmed?.Invoke(selected);
        Debug.Log($"[CharSelect] ✅ เลือก {selected.characterName}");
        Hide();
    }

    // ── UI Refresh ────────────────────────────────────────────────────────
    void RefreshDetail()
    {
        if (selected == null) return;
        if (detailName)    detailName.text    = selected.characterName;
        if (detailDesc)    detailDesc.text    = string.IsNullOrEmpty(selected.description)
                                               ? "No description." : selected.description;
        if (detailPassive) detailPassive.text = string.IsNullOrEmpty(selected.passiveDescription)
                                               ? "" : $"<color=#AAFFAA>Passive:</color> {selected.passiveDescription}";
        if (detailPortrait)
        {
            detailPortrait.sprite  = selected.portrait;
            detailPortrait.enabled = selected.portrait != null;
        }
    }

    void RefreshCardHighlight()
    {
        for (int i = 0; i < cardButtons.Count; i++)
        {
            if (i >= characters.Count) continue;
            var img = cardButtons[i].GetComponent<Image>();
            if (img == null) continue;
            img.color = characters[i] == selected
                ? new Color(0.3f, 0.7f, 1f, 1f)
                : new Color(0.2f, 0.2f, 0.25f, 1f);
        }
    }

    void PopulateCards()
    {
        // ถ้า assign panelRoot จาก Inspector — ใช้ layout ที่มีอยู่แล้ว
        // ไม่ทำ auto-build card แบบ detailed (ใช้ BuildUI path แทน)
    }

    // ── Build UI at Runtime ───────────────────────────────────────────────
    void BuildUI()
    {
        if (builtUI) return;
        builtUI = true;

        // Canvas
        var canvasGO = new GameObject("CharSelectCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel (full-screen dark overlay)
        panelRoot = new GameObject("CharSelectPanel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelRoot.AddComponent<Image>();
        panelImg.color = new Color(0.05f, 0.05f, 0.1f, 0.97f);
        SetStretch(panelRoot.GetComponent<RectTransform>());

        // ── Left: character card list ──
        var leftPanel = MakePanel(panelRoot, "LeftPanel",
            new Vector2(0f, 0f), new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero);
        var leftLayout = leftPanel.AddComponent<VerticalLayoutGroup>();
        leftLayout.padding               = new RectOffset(12, 12, 12, 12);
        leftLayout.spacing               = 8;
        leftLayout.childForceExpandWidth  = true;
        leftLayout.childForceExpandHeight = false;
        leftLayout.childControlWidth      = true;
        leftLayout.childControlHeight     = true;

        // Title
        var title = AddTMPLabel(leftPanel, "SELECT CHARACTER", 20, Color.white);
        title.GetComponent<LayoutElement>().preferredHeight = 32;

        // Character cards
        for (int i = 0; i < characters.Count; i++)
        {
            var cd  = characters[i];
            var idx = i; // capture

            var cardGO = new GameObject($"Card_{cd.characterName}");
            cardGO.transform.SetParent(leftPanel.transform, false);
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var cardBtn = cardGO.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            cardBtn.onClick.AddListener(() => SelectCharacter(characters[idx]));
            cardButtons.Add(cardBtn);

            var le = cardGO.AddComponent<LayoutElement>();
            le.preferredHeight = 64;

            var cardLayout = cardGO.AddComponent<HorizontalLayoutGroup>();
            cardLayout.padding              = new RectOffset(8, 8, 6, 6);
            cardLayout.spacing              = 10;
            cardLayout.childForceExpandWidth  = false;
            cardLayout.childForceExpandHeight = true;
            cardLayout.childControlHeight     = true;

            // Miniature icon
            var iconGO  = new GameObject("Icon");
            iconGO.transform.SetParent(cardGO.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite          = cd.icon ?? cd.portrait;
            iconImg.preserveAspect  = true;
            iconImg.color           = cd.icon != null || cd.portrait != null
                                      ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth  = 48;
            iconLE.preferredHeight = 48;

            // Name + weapon row
            var infoGO = new GameObject("Info");
            infoGO.transform.SetParent(cardGO.transform, false);
            var infoLayout = infoGO.AddComponent<VerticalLayoutGroup>();
            infoLayout.childForceExpandWidth  = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childControlWidth      = true;
            infoLayout.childControlHeight     = true;

            var nameLbl = AddTMPLabel(infoGO, cd.characterName, 13, Color.white);
            nameLbl.GetComponent<LayoutElement>().preferredHeight = 20;

            string wepName = cd.startingWeapon != null ? cd.startingWeapon.weaponName : "—";
            var wepLbl  = AddTMPLabel(infoGO, $"<color=#88CCFF>Weapon: {wepName}</color>", 10, Color.white);
            wepLbl.GetComponent<LayoutElement>().preferredHeight = 16;
        }

        // ── Right: detail panel ──
        var rightPanel = MakePanel(panelRoot, "RightPanel",
            new Vector2(0.38f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var rightLayout = rightPanel.AddComponent<VerticalLayoutGroup>();
        rightLayout.padding               = new RectOffset(20, 20, 20, 20);
        rightLayout.spacing               = 10;
        rightLayout.childForceExpandWidth  = true;
        rightLayout.childForceExpandHeight = false;
        rightLayout.childControlWidth      = true;
        rightLayout.childControlHeight     = true;

        // Portrait placeholder
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(rightPanel.transform, false);
        detailPortrait = portraitGO.AddComponent<Image>();
        detailPortrait.color = new Color(0.2f, 0.2f, 0.3f, 1f);
        var portLE = portraitGO.AddComponent<LayoutElement>();
        portLE.preferredHeight = 200;

        detailName    = AddTMPLabel(rightPanel, "",   18, Color.white);
        detailName.GetComponent<LayoutElement>().preferredHeight    = 28;

        detailDesc    = AddTMPLabel(rightPanel, "",   12, new Color(0.85f, 0.85f, 0.85f));
        detailDesc.GetComponent<LayoutElement>().preferredHeight    = 60;

        detailPassive = AddTMPLabel(rightPanel, "",   11, Color.white);
        detailPassive.GetComponent<LayoutElement>().preferredHeight = 40;
        detailPassive.textWrappingMode = TextWrappingModes.Normal;

        // Spacer
        var spacer = new GameObject("Spacer");
        spacer.transform.SetParent(rightPanel.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1;

        // Confirm button
        var btnGO  = new GameObject("ConfirmBtn");
        btnGO.transform.SetParent(rightPanel.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.7f, 0.3f, 1f);
        confirmButton = btnGO.AddComponent<Button>();
        confirmButton.targetGraphic = btnImg;
        confirmButton.onClick.AddListener(OnConfirm);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 44;

        var btnTxt = AddTMPLabel(btnGO, "CONFIRM", 15, Color.white);
        btnTxt.alignment = TextAlignmentOptions.Center;
        var btnTxtRT = btnTxt.GetComponent<RectTransform>();
        btnTxtRT.anchorMin = Vector2.zero;
        btnTxtRT.anchorMax = Vector2.one;
        btnTxtRT.offsetMin = btnTxtRT.offsetMax = Vector2.zero;
        btnTxt.GetComponent<LayoutElement>().ignoreLayout = true;
    }

    // ── UI Helpers ────────────────────────────────────────────────────────
    void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    GameObject MakePanel(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = Color.clear;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    TextMeshProUGUI AddTMPLabel(GameObject parent, string text, float size, Color color)
    {
        var go  = new GameObject("Label");
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        go.AddComponent<LayoutElement>();
        return tmp;
    }
}
