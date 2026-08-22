using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Character Select UI — ใช้ Inspector references ทั้งหมด
///
/// Left Panel:
///   cardsContainer → Content ใน Scroll View
///   cardTemplate   → CharacterCard prefab asset (แนะนำ) หรือ object ในซีนที่ถูกซ่อนให้อัตโนมัติ
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

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Stats Panel (แท็บ Stats) ─────────────")]
    [Tooltip("ช่องตัวเลขฝั่งขวา — ลาก TMP ของแต่ละแถวมาใส่ ปล่อยว่างได้ทีละช่อง")]
    public TextMeshProUGUI statHpValue;
    public TextMeshProUGUI statAtkValue;
    public TextMeshProUGUI statDefValue;
    public TextMeshProUGUI statSpdValue;
    public TextMeshProUGUI statCritRateValue;
    public TextMeshProUGUI statCritDmgValue;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Tabs (Stats / Ability) ───────────────")]
    [Tooltip("กลุ่มแถวตัวเลข stat — เปิดเมื่ออยู่แท็บ Stats")]
    public GameObject statsPanel;
    [Tooltip("กลุ่มแถว passive / อาวุธ / Q / E เดิม — เปิดเมื่ออยู่แท็บ Ability")]
    public GameObject abilityPanel;
    public Button statsTabButton;
    public Button abilityTabButton;
    [Tooltip("สีป้ายแท็บที่กำลังเปิด / ที่ปิดอยู่ — ปล่อยว่างได้ถ้าไม่มี label")]
    public TextMeshProUGUI statsTabLabel;
    public TextMeshProUGUI abilityTabLabel;
    public Color tabActiveColor   = Color.white;
    public Color tabInactiveColor = new Color(0.6f, 0.6f, 0.65f, 1f);

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Select Button ────────────────────────")]
    [Tooltip("ปุ่ม Select ใหญ่ด้านล่าง — คนละตัวกับ confirmButton ที่ใช้ปลดล็อกด้วยทอง")]
    public Button selectButton;
    [Tooltip("ติ๊ก = คลิกการ์ดแค่ดูก่อน ต้องกด Select ถึงจะยืนยัน - ไม่ติ๊ก = คลิกการ์ดยืนยันทันทีเหมือนเดิม")]
    public bool requireSelectToConfirm = false;

    [Header("── Gold ────────────────────────────────")]
    [Tooltip("ยอดทองปัจจุบัน — ปล่อยว่างได้")]
    public TextMeshProUGUI goldText;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Debug ───────────────────────────────")]
    [Tooltip("พิมพ์ log ทุกขั้นของการเลือกตัวละคร — ปิดได้เมื่อไม่ต้องไล่บั๊กแล้ว")]
    public bool verboseLog = true;

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Card Colors ─────────────────────────")]
    public Color selectedColor = new Color(0.3f, 0.7f, 1f);
    public Color normalColor   = new Color(0.2f, 0.2f, 0.25f, 1f);

    // ── Internal ──────────────────────────────────────────────────────────
    private CharacterData     selected;

    /// <summary>
    /// วงหมุนฝั่งซ้าย — อยู่บนแผง ไม่ใช่บน object นี้ เพราะ event ลาก/ลูกกลิ้ง
    /// ไปไม่ถึง Canvas (panel ระหว่างทางรับไปก่อน) ดู CharacterCarousel
    /// </summary>
    private CharacterCarousel carousel;

    static bool IsUnlocked(CharacterData cd)
        => CloneSwarm.Meta.MetaProgression.IsCharacterUnlocked(cd);

    static int Gold => CloneSwarm.Meta.MetaProgression.Gold;

    void Log(string msg)
    {
        if (verboseLog) Debug.Log($"[CharSelect] {msg}");
    }

    /// <summary>ชื่อที่อ่านออกเสมอ — cd ที่เป็น null คือกรณีที่ต้องเห็นใน log มากที่สุด</summary>
    static string Name(CharacterData cd) => cd != null ? cd.characterName : "(null)";

    /// <summary>จำนวน subscriber ของ OnCharacterConfirmed — 0 แปลว่า LobbyUI ยังไม่ได้ subscribe</summary>
    static int ConfirmedSubscriberCount
        => OnCharacterConfirmed?.GetInvocationList().Length ?? 0;


    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (SelectedCharacter == null)
            SelectedCharacter = FirstUnlocked();
    }

    void Start()
    {
        BuildCarousel();
        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        if (selectButton)  selectButton.onClick.AddListener(OnSelectClicked);
        if (abilityTabButton) abilityTabButton.onClick.AddListener(() => ShowTab(false));
        if (statsTabButton)   statsTabButton.onClick.AddListener(()   => ShowTab(true));
        
        ShowTab(false);   // เปิดมาที่แท็บ Stats เหมือนภาพอ้างอิง
        ResolveOptionalRefs();

        var start = SelectedCharacter;
        Log($"Start — SelectedCharacter ที่ค้างมาจากซีนก่อน = '{Name(start)}' · ทอง {Gold} G");

        if (start == null || !IsUnlocked(start))
        {
            var fallback = FirstUnlocked() ?? (characters.Count > 0 ? characters[0] : null);
            Log($"'{Name(start)}' ใช้ไม่ได้ (null หรือยังล็อก) → fallback เป็น '{Name(fallback)}'");
            start = fallback;
        }

        // วาง carousel ให้ตัวเริ่มต้นอยู่กลางก่อน — JumpTo ไม่ยิง OnSettled
        // จึงไม่คอมมิตซ้ำกับบรรทัดล่างที่เดินสาย select ตามปกติ
        if (carousel != null && start != null)
        {
            int idx = carousel.IndexOf(start);
            if (idx >= 0) carousel.JumpTo(idx);
        }

        SelectCharacter(start, "เริ่มต้น");
    }


    /// <summary>
    /// MenuScene ไม่ได้ assign สามช่องล่างของ Inspector — ถ้าไม่หาให้ ผู้เล่นจะเห็นการ์ดล็อก
    /// โดยไม่มีราคา ไม่มียอดทอง และไม่มีคำอธิบายว่าทำไมกดเลือกแล้วไม่มีอะไรเกิดขึ้น
    /// </summary>
    void ResolveOptionalRefs()
    {
        if (confirmButtonLabel == null && confirmButton != null)
            confirmButtonLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>ตัวละครตัวแรกที่ปลดล็อกแล้ว — null ถ้าไม่มีเลย (ควรมีอย่างน้อย 1 ตัว unlockedByDefault)</summary>
    CharacterData FirstUnlocked()
    {
        foreach (var cd in characters)
            if (cd != null && IsUnlocked(cd)) return cd;

        // ไม่มีตัวฟรีสักตัว = เข้าเกมไม่ได้ถาวร ทองเริ่มที่ 0 และหาเพิ่มได้จากการจบเกมเท่านั้น
        // ซึ่งต้องมีตัวละครก่อน — วนเป็นวงปิด อาการที่เห็นคือ "คลิกการ์ดแล้วไม่มีอะไรเกิดขึ้น"
        Debug.LogError(
            "[CharSelect] ไม่มีตัวละครที่ปลดล็อกแล้วสักตัว — คลิกการ์ดจะไม่มีผล\n" +
            "แก้: ติ๊ก unlockedByDefault บน CharacterData อย่างน้อยหนึ่งตัว");
        return null;
    }

    // ── Build Cards ───────────────────────────────────────────────────────

    // ── Select ────────────────────────────────────────────────────────────

    // ── Carousel ──────────────────────────────────────────────────────────
    /// <summary>
    /// หา (หรือสร้าง) CharacterCarousel บน "แผง" ซึ่งคือ parent ของ cardsContainer
    /// วางบน object นี้ไม่ได้ — CharacterSelectUI อยู่บน Canvas และ event ลาก/ลูกกลิ้ง
    /// ของ uGUI ไปไม่ถึง เพราะ panel ระหว่างทางรับไปก่อน
    /// </summary>
    void BuildCarousel()
    {
        if (cardsContainer == null)
        {
            Debug.LogError("[CharSelect] cardsContainer ยังไม่ได้ assign! ลาก container ของการ์ดมาใส่");
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

        var rect = cardsContainer as RectTransform;
        var host = rect != null ? rect.parent as RectTransform : null;
        if (host == null)
        {
            Debug.LogError("[CharSelect] cardsContainer ต้องเป็น RectTransform ที่มีพ่อเป็นแผง — วาง carousel ไม่ได้");
            return;
        }

        carousel = host.GetComponent<CharacterCarousel>();
        if (carousel == null)
        {
            carousel = host.gameObject.AddComponent<CharacterCarousel>();
            Debug.LogWarning("[CharSelect] ไม่พบ CharacterCarousel บน '" + host.name + "' จึงเพิ่มให้ตอนรัน — " +
                             "ช่อง hero กับปุ่มลูกศรจะว่าง ถ้าต้องการใช้ ให้เพิ่ม component นี้ใน Editor แล้วลาก reference");
        }

        carousel.OnSettled -= OnCarouselSettled;
        carousel.OnSettled += OnCarouselSettled;
        carousel.Setup(characters, cardTemplate, rect, selectedColor, normalColor);
    }

    void OnDestroy()
    {
        if (carousel != null) carousel.OnSettled -= OnCarouselSettled;
    }

    /// <summary>carousel เข้าช่องนิ่งแล้ว — จุดเดียวที่การหมุนกลายเป็นการเลือก</summary>
    void OnCarouselSettled(int index)
    {
        var cd = carousel != null ? carousel.GetCharacter(index) : null;
        SelectCharacter(cd, "carousel");
    }

    // ── Select ────────────────────────────────────────────────────────────
    void SelectCharacter(CharacterData cd, string source = "คลิกการ์ด")
    {
        Log($"── SelectCharacter('{Name(cd)}') · จาก: {source} ──");

        selected = cd;
        RefreshDetail();
        RefreshLockState();

        if (cd == null)
        {
            Log("  ✗ cd เป็น null — characters list ว่างหรือเป็น null ทั้งหมด · SelectedCharacter ไม่เปลี่ยน");
            return;
        }

        if (!IsUnlocked(cd))
        {
            // จุดนี้คืออาการ "เลือกตัวละครไม่ได้" — การ์ดคลิกติด แต่ selection ไม่ถูกคอมมิต
            Log($"  ✗ '{cd.characterName}' ยังล็อกอยู่ — ไม่คอมมิต selection\n" +
                $"    ราคา {cd.unlockCost:N0} G · ทองที่มี {Gold} G · unlockedByDefault ไม่ติ๊ก และไม่มีในไฟล์เซฟ\n" +
                $"    SelectedCharacter ยังเป็น '{Name(SelectedCharacter)}'");
            return;
        }

        // โหมด "กด Select ถึงยืนยัน" — คลิกการ์ดอัปเดตแค่แผงขวา ไม่แตะ SelectedCharacter
        // ยกเว้นทางที่ตั้งใจยืนยัน (ปุ่ม Select / หลังปลดล็อก / ค่าเริ่มต้นตอนเปิดหน้า)
        if (requireSelectToConfirm && source == "คลิกการ์ด")
        {
            Log($"  ○ พรีวิว '{cd.characterName}' — ยังไม่คอมมิต (requireSelectToConfirm เปิดอยู่ ต้องกด Select)");
            return;
        }

        SelectedCharacter = cd;
        Log($"  ✓ คอมมิตแล้ว — SelectedCharacter = '{cd.characterName}'");

        int subs = ConfirmedSubscriberCount;
        OnCharacterConfirmed?.Invoke(cd);
        Log($"  → ยิง OnCharacterConfirmed ให้ subscriber {subs} ตัว" +
            (subs == 0 ? " · 0 = LobbyUI ยังไม่ subscribe ล็อบบี้จะไม่รู้ว่าเลือกอะไร" : ""));
    }

    /// <summary>
    /// ปุ่ม Confirm ทำหน้าที่ปลดล็อกตัวละครด้วยทองเมื่อยังไม่ได้ปลดล็อก
    /// </summary>
    void OnConfirm()
    {
        if (selected == null)
        {
            Log("กด Confirm แต่ยังไม่มีตัวที่เลือก");
            return;
        }
        if (IsUnlocked(selected))
        {
            Log($"กด Confirm ทั้งที่ '{selected.characterName}' ปลดล็อกแล้ว — ปุ่มควรถูกซ่อนไปตั้งแต่ RefreshLockState");
            return;
        }

        int goldBefore = Gold;
        Log($"กด Confirm — ขอปลดล็อก '{selected.characterName}' ราคา {selected.unlockCost:N0} G · ทองก่อนจ่าย {goldBefore:N0} G");

        if (CloneSwarm.Meta.MetaProgression.TryUnlockCharacter(selected))
        {
            Log($"  ✓ ปลดล็อกสำเร็จ — ทอง {goldBefore:N0} → {Gold:N0} G");
            RefreshCardLocks();
            // ปลดล็อกแล้วเดินสาย select ปกติ — ไม่คอมมิตเอง ไม่งั้นสองทางนี้จะค่อยๆ เพี้ยนจากกัน
            SelectCharacter(selected, "หลังปลดล็อก");
        }
        else
        {
            Log($"  ✗ ปลดล็อกไม่สำเร็จ — ต้องการ {selected.unlockCost:N0} G มี {Gold:N0} G");
            RefreshLockState();   // ยอดทองอาจเพิ่งเปลี่ยน
            if (lockStatusText != null)
                lockStatusText.text = $"ทองไม่พอ — ต้องการ {selected.unlockCost:N0} G";
        }
    }

    /// <summary>ปุ่ม Select ใหญ่ — ยืนยันตัวที่กำลังพรีวิวอยู่</summary>
    void OnSelectClicked()
    {
        if (selected == null)
        {
            Log("กด Select แต่ยังไม่มีตัวที่พรีวิวอยู่");
            return;
        }
        if (!IsUnlocked(selected))
        {
            Log($"กด Select แต่ '{selected.characterName}' ยังล็อก — ต้องปลดล็อกก่อน");
            return;
        }
        SelectCharacter(selected, "ปุ่ม Select");
    }

    // ── Tabs ──────────────────────────────────────────────────────────────
    /// <summary>สลับแท็บ Stats / Ability — ปล่อยช่องไหนว่างใน Inspector ช่องนั้นถูกข้าม</summary>
    void ShowTab(bool stats)
    {
        if (statsPanel   != null) statsPanel.SetActive(stats);
        if (abilityPanel != null) abilityPanel.SetActive(!stats);

        if (statsTabLabel   != null) statsTabLabel.color   = stats ? tabActiveColor : tabInactiveColor;
        if (abilityTabLabel != null) abilityTabLabel.color = stats ? tabInactiveColor : tabActiveColor;
    }

    // ── Stats Panel ───────────────────────────────────────────────────────
    /// <summary>
    /// เติมตัวเลขแผง stat จาก CharacterData — ATK อ่านจากอาวุธเริ่มต้นถ้าไม่ได้ตั้ง baseAttack ไว้
    /// (ยังไม่มีค่าไหนมีผลในเกมจริงนอกจาก HP กับ SPD — ดูคอมเมนต์ใน CharacterData)
    /// </summary>
    void RefreshStats()
    {
        if (selected == null) return;

        float atk = selected.baseAttack;
        if (atk <= 0f && selected.startingWeapon != null)
            atk = selected.startingWeapon.GetLevelData(0).damage;

        SetText(statHpValue,       $"{selected.baseHealth:0}");
        SetText(statAtkValue,      $"{atk:0}");
        SetText(statDefValue,      $"{selected.baseDefense:0}");
        SetText(statSpdValue,      $"{selected.baseMoveSpeed:0.#}");
        SetText(statCritRateValue, $"{selected.baseCritRate  * 100f:0.#}%");
        SetText(statCritDmgValue,  $"{selected.baseCritDamage * 100f:0.#}%");
    }

    // ── Lock / Gold display ───────────────────────────────────────────────
    void RefreshLockState()
    {
        if (goldText != null)
            goldText.text = $"{CloneSwarm.Meta.MetaProgression.Gold:N0} G";

        if (selected == null) return;

        bool unlocked = IsUnlocked(selected);

        if (confirmButtonLabel != null)
            confirmButtonLabel.text = selected.unlockCost > 0
                ? $"ปลดล็อก {selected.unlockCost:N0} G"
                : "ปลดล็อก";

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(!unlocked);
            confirmButton.interactable = !unlocked && CloneSwarm.Meta.MetaProgression.CanUnlockCharacter(selected);
        }

        // Select กดได้เฉพาะตัวที่ปลดล็อกแล้ว — ตัวที่ยังล็อกใช้ปุ่มปลดล็อกแทน
        // ไม่ซ่อนแต่ปิดการกด เพื่อให้ปุ่มหลักไม่กระพริบหายตอนเลื่อนดูตัวที่ยังไม่มี
        if (selectButton != null)
            selectButton.interactable = unlocked;

        if (lockStatusText != null)
        {
            if (unlocked)
                lockStatusText.text = "";
            else if (!CloneSwarm.Meta.MetaProgression.CanUnlockCharacter(selected))
                lockStatusText.text = $"ทองไม่พอ — ต้องการ {selected.unlockCost:N0} G";
            else
                lockStatusText.text = selected.description;
        }
    }

    void RefreshCardLocks() => carousel?.RefreshLocks();

    // ── Refresh Detail Panel ──────────────────────────────────────────────
    void RefreshDetail()
    {
        if (selected == null) return;

        RefreshStats();

        // Portrait row
        // ภาพ portrait ต้องมีเจ้าของเดียว — carousel วาดตัวเด่นจาก portrait ตัวเดียวกัน
        // ถ้าปล่อยให้ทั้งคู่วาด ภาพตัวละครจะโผล่สองที่พร้อมกันบนจอ
        bool heroOwnsPortrait = carousel != null && carousel.heroImage != null;
        if (!heroOwnsPortrait)
        {
            SetImage(detailPortrait, selected.portrait);
        }
        else if (detailPortrait != null && detailPortrait != carousel.heroImage)
        {
            detailPortrait.enabled = false;   // เจ้าของคือ hero — ปิดใบซ้ำทิ้ง
        }
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
