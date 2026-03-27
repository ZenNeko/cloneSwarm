using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tools > Build Swarm HUD
/// สร้าง Canvas HUD ทั้งหมดใน SampleScene อัตโนมัติ
/// — GameHUD (HP, EXP, Timer, Announcement, Respawn)
/// — ChargeBar + Ability Slots Q/E (Riven)
/// — WeaponStatHUD (Weapon + Stat slots แบบ auto-generate ตอน runtime)
/// รันซ้ำได้ — จะลบ HUDCanvas เก่าแล้วสร้างใหม่
/// </summary>
public static class SwarmHUDBuilder
{
    // ── Palette ────────────────────────────────────────────────────────────
    static readonly Color BG_PANEL    = new Color(0.04f, 0.04f, 0.06f, 0.82f);
    static readonly Color BG_SLOT     = new Color(0.10f, 0.10f, 0.14f, 0.90f);
    static readonly Color BG_SLOT_RIM = new Color(0.20f, 0.20f, 0.28f, 1.00f);
    static readonly Color CLR_HP      = new Color(0.18f, 0.80f, 0.28f, 1f);
    static readonly Color CLR_SHIELD  = new Color(0.30f, 0.65f, 1.00f, 1f);
    static readonly Color CLR_EXP     = new Color(0.45f, 0.25f, 0.85f, 1f);
    static readonly Color CLR_CHARGE  = new Color(0.15f, 0.80f, 1.00f, 1f);
    static readonly Color CLR_YELLOW  = new Color(1.00f, 0.90f, 0.20f, 1f);
    static readonly Color CLR_WHITE   = new Color(1.00f, 1.00f, 1.00f, 0.92f);
    static readonly Color CLR_GREY    = new Color(0.65f, 0.65f, 0.70f, 0.85f);
    static readonly Color CLR_RED     = new Color(0.95f, 0.20f, 0.20f, 1f);

    // ── Entry Point ────────────────────────────────────────────────────────
    [MenuItem("Tools/Build Swarm HUD")]
    public static void BuildHUD()
    {
        // ลบ Canvas เก่า
        var old = GameObject.Find("HUDCanvas");
        if (old != null)
        {
            Object.DestroyImmediate(old);
            Debug.Log("[HUDBuilder] Removed old HUDCanvas");
        }

        // ── Root Canvas ──────────────────────────────────────────────────
        var canvasGO = new GameObject("HUDCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        var hud = canvasGO.AddComponent<GameHUD>();
        var wsh = canvasGO.AddComponent<WeaponStatHUD>();

        // ── Sections ─────────────────────────────────────────────────────
        BuildTimer(canvasGO, hud);
        BuildAnnouncement(canvasGO, hud);
        BuildRespawnOverlay(canvasGO, hud);
        BuildBottomLeft(canvasGO, hud);
        BuildAbilityZone(canvasGO, hud);

        // WeaponStatHUD parents (จะ auto-fill ตอน runtime)
        var weaponRow = BuildWeaponStatRows(canvasGO);
        wsh.weaponSlotsParent = weaponRow.weapon;
        wsh.statSlotsParent   = weaponRow.stat;

        // Mark dirty
        EditorUtility.SetDirty(canvasGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[HUDBuilder] ✅ HUDCanvas built successfully!");
        EditorUtility.DisplayDialog("HUD Built",
            "HUDCanvas สร้างเสร็จแล้ว\n\n" +
            "ตรวจสอบ:\n" +
            "• Assign icon ให้ qSlot.iconImage / eSlot.iconImage\n" +
            "• WeaponStatHUD จะสร้าง weapon/stat slots อัตโนมัติตอน Play\n" +
            "• Charge Bar / Ability Slots ซ่อนอัตโนมัติถ้าไม่ใช่ Riven",
            "OK");
    }

    // ══════════════════════════════════════════════════════════════════════
    // TIMER — top center
    // ══════════════════════════════════════════════════════════════════════
    static void BuildTimer(GameObject canvas, GameHUD hud)
    {
        var root = MakePanel(canvas, "Timer_Panel",
            anchorMin: new Vector2(0.35f, 1f), anchorMax: new Vector2(0.65f, 1f),
            pivot:     new Vector2(0.5f, 1f),
            offsetMin: new Vector2(0, -56f),   offsetMax: new Vector2(0, -6f));
        root.GetComponent<Image>().color = BG_PANEL;

        hud.timerLabel = MakeTMP(root, "TimerText", "00:00",
            fontSize: 28f, color: CLR_WHITE, bold: true,
            anchor: Vector4.one * 0,
            offsetMin: Vector2.zero, offsetMax: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
        StretchFull(hud.timerLabel.rectTransform, new Vector2(12, 4), new Vector2(-12, -4));
    }

    // ══════════════════════════════════════════════════════════════════════
    // ANNOUNCEMENT — screen center
    // ══════════════════════════════════════════════════════════════════════
    static void BuildAnnouncement(GameObject canvas, GameHUD hud)
    {
        var go = new GameObject("Announcement");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.2f, 0.55f);
        rt.anchorMax = new Vector2(0.8f, 0.70f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "";
        tmp.fontSize  = 48f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = CLR_RED;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        go.SetActive(false);

        hud.announcementLabel = tmp;
    }

    // ══════════════════════════════════════════════════════════════════════
    // RESPAWN OVERLAY — fullscreen
    // ══════════════════════════════════════════════════════════════════════
    static void BuildRespawnOverlay(GameObject canvas, GameHUD hud)
    {
        var panel = new GameObject("RespawnOverlay");
        panel.transform.SetParent(canvas.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.72f);
        StretchFull(panel.GetComponent<RectTransform>());
        panel.SetActive(false);
        hud.respawnPanel = panel;

        // Countdown text
        var txt = MakeTMP(panel, "RespawnText", "RESPAWN IN: 10",
            fontSize: 42f, color: CLR_WHITE, bold: true,
            anchor: Vector4.zero,
            offsetMin: Vector2.zero, offsetMax: Vector2.zero,
            alignment: TextAlignmentOptions.Center);
        StretchFull(txt.rectTransform, new Vector2(40, 40), new Vector2(-40, -40));
        hud.respawnCountdownText = txt;
    }

    // ══════════════════════════════════════════════════════════════════════
    // BOTTOM-LEFT — HP + Shield + EXP
    // ══════════════════════════════════════════════════════════════════════
    static void BuildBottomLeft(GameObject canvas, GameHUD hud)
    {
        // Container panel
        var root = MakePanel(canvas, "BottomLeft_Panel",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
            pivot:     new Vector2(0f, 0f),
            offsetMin: new Vector2(16f, 100f), offsetMax: new Vector2(380f, 178f));
        root.GetComponent<Image>().color = BG_PANEL;

        // ── HP row ───────────────────────────────────────────────────────
        var hpRow = MakeChild(root, "HPRow",
            anchorMin: new Vector2(0, 0.55f), anchorMax: new Vector2(1, 1f),
            offsetMin: new Vector2(10, 4), offsetMax: new Vector2(-10, -6));

        // HP label
        var hpLabel = MakeTMP(hpRow, "HP_Label", "HP",
            fontSize: 11f, color: CLR_WHITE,
            alignment: TextAlignmentOptions.Left);
        SetRect(hpLabel.rectTransform,
            new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(0, 0), new Vector2(28, 0));

        // HP bar bg
        var hpBarBG = MakeBar(hpRow, "HP_BarBG", new Color(0.12f, 0.12f, 0.12f),
            new Vector2(28, 0), new Vector2(-80, 0),
            new Vector2(0, 2), new Vector2(0, -2));

        // HP fill
        var hpFillGO = MakeBarFill(hpBarBG, "HP_Fill", CLR_HP);
        hud.hpFill = hpFillGO.GetComponent<Image>();

        // Shield fill (overlays HP fill, same bar)
        var shieldFillGO = MakeBarFill(hpBarBG, "Shield_Fill", CLR_SHIELD);
        shieldFillGO.GetComponent<Image>().color = CLR_SHIELD;
        shieldFillGO.GetComponent<Image>().fillAmount = 0f;
        var shieldRoot = shieldFillGO;          // root = the fill itself
        hud.shieldFill     = shieldFillGO.GetComponent<Image>();
        hud.shieldBarRoot  = shieldFillGO;

        // HP text
        hud.hpText = MakeTMP(hpRow, "HP_Text", "100/100",
            fontSize: 10f, color: CLR_WHITE,
            alignment: TextAlignmentOptions.Right);
        SetRect(hud.hpText.rectTransform,
            new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-78, 0), new Vector2(0, 0));

        // ── EXP row ──────────────────────────────────────────────────────
        var expRow = MakeChild(root, "EXPRow",
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0.50f),
            offsetMin: new Vector2(10, 4), offsetMax: new Vector2(-10, -2));

        // Level text
        hud.levelText = MakeTMP(expRow, "Level_Text", "Lv 1",
            fontSize: 11f, color: CLR_YELLOW, bold: true,
            alignment: TextAlignmentOptions.Left);
        SetRect(hud.levelText.rectTransform,
            new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(0, 0), new Vector2(38, 0));

        // EXP bar bg
        var expBarBG = MakeBar(expRow, "EXP_BarBG", new Color(0.10f, 0.08f, 0.18f),
            new Vector2(40, 0), new Vector2(0, 0),
            new Vector2(0, 3), new Vector2(0, -3));

        // EXP fill
        var expFillGO = MakeBarFill(expBarBG, "EXP_Fill", CLR_EXP);
        hud.expFill = expFillGO.GetComponent<Image>();
    }

    // ══════════════════════════════════════════════════════════════════════
    // ABILITY ZONE — bottom center: [Q] [CHARGE] [E]
    // ══════════════════════════════════════════════════════════════════════
    static void BuildAbilityZone(GameObject canvas, GameHUD hud)
    {
        // Root container (hidden until Riven spawns — GameHUD จะ SetActive)
        var root = new GameObject("AbilityZone");
        root.transform.SetParent(canvas.transform, false);
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot     = new Vector2(0.5f, 0f);
        rootRT.offsetMin = new Vector2(-210f, 100f);
        rootRT.offsetMax = new Vector2( 210f, 170f);
        root.SetActive(false);       // GameHUD จะเปิดเมื่อตรวจพบ Riven

        // ── Q Slot ───────────────────────────────────────────────────────
        var qRoot = BuildAbilitySlot(root, "Q_Slot", "Q", out var qSlot);
        var qRT   = qRoot.GetComponent<RectTransform>();
        qRT.anchorMin = new Vector2(0f, 0f);
        qRT.anchorMax = new Vector2(0f, 1f);
        qRT.pivot     = new Vector2(0f, 0f);
        qRT.offsetMin = new Vector2(0f,   0f);
        qRT.offsetMax = new Vector2(64f,  0f);

        // ── Charge Bar (center) ──────────────────────────────────────────
        var chargePanel = MakePanel(root, "ChargeBar_Panel",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
            pivot:     new Vector2(0.5f, 0.5f),
            offsetMin: new Vector2(70f, 18f), offsetMax: new Vector2(-70f, -18f));
        chargePanel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.9f);

        // Charge bar BG
        var chargeBG = MakeChild(chargePanel, "ChargeBG",
            anchorMin: new Vector2(0, 0.35f), anchorMax: new Vector2(1, 0.85f),
            offsetMin: new Vector2(8, 0), offsetMax: new Vector2(-8, 0));
        var chargeBGImg = chargeBG.AddComponent<Image>();
        chargeBGImg.color = new Color(0.06f, 0.06f, 0.08f);

        // Charge fill
        var chargeFillGO = MakeBarFill(chargeBG, "Charge_Fill", CLR_CHARGE);
        hud.chargeBarFill = chargeFillGO.GetComponent<Image>();
        hud.chargeBarRoot = root;   // ซ่อน/แสดงทั้ง zone

        // Charge text (label above bar)
        hud.chargeBarText = MakeTMP(chargePanel, "ChargeText", "0",
            fontSize: 13f, color: CLR_WHITE, bold: true,
            alignment: TextAlignmentOptions.Center);
        SetRect(hud.chargeBarText.rectTransform,
            new Vector2(0, 0.85f), new Vector2(1, 1f),
            new Vector2(0, 0), new Vector2(0, 0));

        // "CHARGE" label under bar
        var chargeLabelTMP = MakeTMP(chargePanel, "ChargeLabel", "CHARGE",
            fontSize: 9f, color: CLR_GREY,
            alignment: TextAlignmentOptions.Center);
        SetRect(chargeLabelTMP.rectTransform,
            new Vector2(0, 0f), new Vector2(1, 0.32f),
            new Vector2(0, 0), new Vector2(0, 0));

        // ── E Slot ───────────────────────────────────────────────────────
        var eRoot = BuildAbilitySlot(root, "E_Slot", "E", out var eSlot);
        var eRT   = eRoot.GetComponent<RectTransform>();
        eRT.anchorMin = new Vector2(1f, 0f);
        eRT.anchorMax = new Vector2(1f, 1f);
        eRT.pivot     = new Vector2(1f, 0f);
        eRT.offsetMin = new Vector2(-64f, 0f);
        eRT.offsetMax = new Vector2( 0f,  0f);

        // Assign to GameHUD
        hud.qSlot = qSlot;
        hud.eSlot = eSlot;
    }

    // ── Build single ability slot: icon + cooldown overlay ───────────────
    static GameObject BuildAbilitySlot(GameObject parent, string name, string keyHint,
                                       out GameHUD.AbilitySlotUI slotData)
    {
        slotData = new GameHUD.AbilitySlotUI();

        // Root
        var root = new GameObject(name);
        root.transform.SetParent(parent.transform, false);
        slotData.root = root;

        // Rim (outline)
        var rimImg = root.AddComponent<Image>();
        rimImg.color = BG_SLOT_RIM;

        // BG
        var bgGO  = MakeChild(root, "BG", stretch: true, inset: 2f);
        bgGO.AddComponent<Image>().color = BG_SLOT;

        // Icon
        var iconGO = MakeChild(root, "Icon", stretch: true, inset: 8f);
        slotData.iconImage = iconGO.AddComponent<Image>();
        slotData.iconImage.color          = new Color(1f, 1f, 1f, 0.85f);
        slotData.iconImage.preserveAspect = true;

        // Cooldown fill (Radial360 overlay)
        var cdGO  = MakeChild(root, "CooldownFill", stretch: true, inset: 0f);
        var cdImg = cdGO.AddComponent<Image>();
        cdImg.color       = new Color(0f, 0f, 0f, 0.72f);
        cdImg.type        = Image.Type.Filled;
        cdImg.fillMethod  = Image.FillMethod.Radial360;
        cdImg.fillOrigin  = (int)Image.Origin360.Top;
        cdImg.fillClockwise = true;
        cdImg.fillAmount  = 0f;
        slotData.cooldownFill = cdImg;

        // CD text (center)
        slotData.cooldownText = MakeTMP(root, "CDText", "",
            fontSize: 16f, color: CLR_WHITE, bold: true,
            alignment: TextAlignmentOptions.Center);
        StretchFull(slotData.cooldownText.rectTransform, new Vector2(2, 2), new Vector2(-2, -18));

        // Key hint (bottom center)
        slotData.keyHintText = MakeTMP(root, "KeyHint", keyHint,
            fontSize: 10f, color: CLR_YELLOW,
            alignment: TextAlignmentOptions.Center);
        SetRect(slotData.keyHintText.rectTransform,
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 0), new Vector2(0, 16));

        // ExileGlow (orange border — shown when Exile active)
        if (keyHint == "E")
        {
            var glowGO  = MakeChild(root, "ExileGlow", stretch: true, inset: -2f);
            var glowImg = glowGO.AddComponent<Image>();
            glowImg.color = new Color(1f, 0.55f, 0.1f, 0.85f);
            glowGO.SetActive(false);
            slotData.exileGlow = glowGO;

            // Push icon to front
            iconGO.transform.SetAsLastSibling();
            cdGO.transform.SetAsLastSibling();
            slotData.cooldownText.transform.SetAsLastSibling();
            slotData.keyHintText.transform.SetAsLastSibling();
        }

        return root;
    }

    // ══════════════════════════════════════════════════════════════════════
    // WEAPON / STAT ROWS — bottom bar
    // ══════════════════════════════════════════════════════════════════════
    static (RectTransform weapon, RectTransform stat) BuildWeaponStatRows(GameObject canvas)
    {
        // Weapon row — bottom left
        var weaponPanel = MakePanel(canvas, "WeaponRow",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
            pivot:     new Vector2(0f, 0f),
            offsetMin: new Vector2(16f, 12f), offsetMax: new Vector2(16f + 6 * 62f + 20f, 12f + 78f));
        var wImg = weaponPanel.GetComponent<Image>();
        wImg.color = BG_PANEL;

        var wLabel = MakeTMP(weaponPanel, "WeaponLabel", "WEAPONS",
            fontSize: 9f, color: CLR_GREY, alignment: TextAlignmentOptions.Top);
        SetRect(wLabel.rectTransform,
            new Vector2(0, 1f), new Vector2(1, 1f),
            new Vector2(4, -16), new Vector2(-4, 0));

        var wRT = weaponPanel.GetComponent<RectTransform>();

        // Stat row — bottom right
        var statPanel = MakePanel(canvas, "StatRow",
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot:     new Vector2(1f, 0f),
            offsetMin: new Vector2(-16f - 6 * 62f - 20f, 12f), offsetMax: new Vector2(-16f, 12f + 78f));
        statPanel.GetComponent<Image>().color = BG_PANEL;

        var sLabel = MakeTMP(statPanel, "StatLabel", "STATS",
            fontSize: 9f, color: CLR_GREY, alignment: TextAlignmentOptions.Top);
        SetRect(sLabel.rectTransform,
            new Vector2(0, 1f), new Vector2(1, 1f),
            new Vector2(4, -16), new Vector2(-4, 0));

        var sRT = statPanel.GetComponent<RectTransform>();

        return (wRT, sRT);
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════

    static GameObject MakePanel(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot     = pivot;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    static GameObject MakeChild(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    // stretch child (no Image)
    static GameObject MakeChild(GameObject parent, string name,
        bool stretch = true, float inset = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rt = go.AddComponent<RectTransform>();
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2( inset,  inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
        return go;
    }

    static GameObject MakeBar(GameObject parent, string name, Color bg,
        Vector2 anchorOffMin, Vector2 anchorOffMax,
        Vector2 offsetMin,    Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = new Vector2(anchorOffMin.x, offsetMin.y);
        rt.offsetMax = new Vector2(anchorOffMax.x, offsetMax.y);
        return go;
    }

    static GameObject MakeBarFill(GameObject parent, string name, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color       = fillColor;
        img.type        = Image.Type.Filled;
        img.fillMethod  = Image.FillMethod.Horizontal;
        img.fillAmount  = 0.7f;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static TextMeshProUGUI MakeTMP(GameObject parent, string name, string text,
        float fontSize, Color color, bool bold = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        Vector4 anchor = default,
        Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = alignment;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static void StretchFull(RectTransform rt,
        Vector2 min = default, Vector2 max = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = min;
        rt.offsetMax = max;
    }

    static void SetRect(RectTransform rt,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
