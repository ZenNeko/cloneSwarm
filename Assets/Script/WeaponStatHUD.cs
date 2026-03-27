using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD แสดง Weapon Slots (6) และ Stat Slots (6) ของ local player
/// — วาง Component นี้บน Canvas GameObject ใน SampleScene
/// — สร้าง slot UI อัตโนมัติถ้าไม่ได้ assign ใน Inspector
/// — Refresh ทุก 0.5 วินาที (ไม่ใช้ event เพื่อลด coupling)
/// </summary>
public class WeaponStatHUD : MonoBehaviour
{
    [Header("Optional: กำหนด parent เอง (ถ้าไม่กำหนดจะสร้าง auto)")]
    public RectTransform weaponSlotsParent;
    public RectTransform statSlotsParent;

    [Header("Layout")]
    public float slotSize      = 56f;
    public float slotSpacing   = 6f;
    public float iconPadding   = 6f;

    // ── Internal references ───────────────────────────────────────────────
    private PlayerWeaponManager weaponManager;
    private PlayerStatManager   statManager;

    private SlotUI[] weaponSlots = new SlotUI[PlayerWeaponManager.MaxWeaponSlots];
    private SlotUI[] statSlots   = new SlotUI[PlayerStatManager.MaxStatSlots];

    private bool builtUI;

    // ── Slot data class ───────────────────────────────────────────────────
    class SlotUI
    {
        public GameObject root;
        public Image      bg;
        public Image      icon;
        public TextMeshProUGUI nameTxt;
        public TextMeshProUGUI levelTxt;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        StartCoroutine(WaitForLocalPlayer());
    }

    IEnumerator WaitForLocalPlayer()
    {
        while (weaponManager == null)
        {
            foreach (var pm in FindObjectsByType<PlayerWeaponManager>(FindObjectsSortMode.None))
            {
                if (pm.IsOwner)
                {
                    weaponManager = pm;
                    statManager   = pm.GetComponent<PlayerStatManager>();
                    break;
                }
            }
            yield return new WaitForSeconds(0.3f);
        }

        EnsureUI();
        InvokeRepeating(nameof(Refresh), 0f, 0.4f);
    }

    // ── Build UI ──────────────────────────────────────────────────────────
    void EnsureUI()
    {
        if (builtUI) return;
        builtUI = true;

        // ── Root Canvas (ถ้ายังไม่มี Canvas บน this GO) ──
        if (GetComponent<Canvas>() == null && GetComponentInParent<Canvas>() == null)
        {
            var canvasGO = new GameObject("WeaponStatHUDCanvas");
            canvasGO.transform.SetParent(transform, false);
            var c = canvasGO.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        var root = GetComponentInChildren<Canvas>()?.gameObject ?? gameObject;

        // ── Weapon Slot Row (bottom-left) ──
        if (weaponSlotsParent == null)
            weaponSlotsParent = CreateRow(root, "WeaponRow",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f, 10f),
                new Vector2(10f + (slotSize + slotSpacing) * PlayerWeaponManager.MaxWeaponSlots, 10f + slotSize + 24f));

        // ── Stat Slot Row (bottom-right) ──
        if (statSlotsParent == null)
            statSlotsParent = CreateRow(root, "StatRow",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f - (slotSize + slotSpacing) * PlayerStatManager.MaxStatSlots, 10f),
                new Vector2(-10f, 10f + slotSize + 24f));

        // ── Header labels ──
        AddHeaderLabel(weaponSlotsParent, "WEAPONS");
        AddHeaderLabel(statSlotsParent,   "STATS");

        // ── Slots ──
        for (int i = 0; i < PlayerWeaponManager.MaxWeaponSlots; i++)
            weaponSlots[i] = BuildSlot(weaponSlotsParent, i);

        for (int i = 0; i < PlayerStatManager.MaxStatSlots; i++)
            statSlots[i] = BuildSlot(statSlotsParent, i);
    }

    RectTransform CreateRow(GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding              = new RectOffset(8, 8, 22, 4);
        layout.spacing              = slotSpacing;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = false;
        layout.childControlHeight     = false;
        layout.childAlignment         = TextAnchor.LowerLeft;

        return rt;
    }

    void AddHeaderLabel(RectTransform parent, string text)
    {
        var go = new GameObject("Header");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 9;
        tmp.color     = new Color(0.8f, 0.8f, 0.8f, 0.9f);
        tmp.alignment = TextAlignmentOptions.Center;

        // Position at top of parent
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -18f);
        rt.offsetMax = new Vector2(0f, 0f);

        // Remove from layout group influence
        var le = go.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }

    SlotUI BuildSlot(RectTransform parent, int index)
    {
        var slot = new SlotUI();

        // Root
        slot.root = new GameObject($"Slot_{index}");
        slot.root.transform.SetParent(parent, false);

        var le = slot.root.AddComponent<LayoutElement>();
        le.preferredWidth  = slotSize;
        le.preferredHeight = slotSize;

        // BG
        slot.bg       = slot.root.AddComponent<Image>();
        slot.bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var rtRoot = slot.root.GetComponent<RectTransform>();
        rtRoot.sizeDelta = new Vector2(slotSize, slotSize);

        // Icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slot.root.transform, false);
        slot.icon = iconGO.AddComponent<Image>();
        slot.icon.color = new Color(1f, 1f, 1f, 0.85f);
        slot.icon.preserveAspect = true;
        var rtIcon = iconGO.GetComponent<RectTransform>();
        rtIcon.anchorMin = Vector2.zero;
        rtIcon.anchorMax = Vector2.one;
        rtIcon.offsetMin = new Vector2(iconPadding, 14f);
        rtIcon.offsetMax = new Vector2(-iconPadding, -iconPadding);

        // Level badge (top-left)
        var lvGO = new GameObject("Level");
        lvGO.transform.SetParent(slot.root.transform, false);
        slot.levelTxt = lvGO.AddComponent<TextMeshProUGUI>();
        slot.levelTxt.fontSize  = 9f;
        slot.levelTxt.color     = Color.yellow;
        slot.levelTxt.alignment = TextAlignmentOptions.TopLeft;
        var rtLv = lvGO.GetComponent<RectTransform>();
        rtLv.anchorMin = new Vector2(0f, 1f);
        rtLv.anchorMax = new Vector2(1f, 1f);
        rtLv.pivot     = new Vector2(0f, 1f);
        rtLv.offsetMin = new Vector2(2f, -14f);
        rtLv.offsetMax = new Vector2(0f, 0f);

        // Name label (bottom strip)
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(slot.root.transform, false);
        slot.nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
        slot.nameTxt.fontSize   = 7.5f;
        slot.nameTxt.color      = Color.white;
        slot.nameTxt.alignment  = TextAlignmentOptions.Bottom;
        slot.nameTxt.textWrappingMode = TextWrappingModes.NoWrap;
        slot.nameTxt.overflowMode     = TextOverflowModes.Ellipsis;
        var rtName = nameGO.GetComponent<RectTransform>();
        rtName.anchorMin = new Vector2(0f, 0f);
        rtName.anchorMax = new Vector2(1f, 0f);
        rtName.pivot     = new Vector2(0.5f, 0f);
        rtName.offsetMin = new Vector2(1f, 0f);
        rtName.offsetMax = new Vector2(-1f, 14f);

        SetSlotEmpty(slot);
        return slot;
    }

    // ── Refresh ───────────────────────────────────────────────────────────
    void Refresh()
    {
        if (weaponManager == null || statManager == null) return;

        var equipped = weaponManager.GetEquippedWeapons();

        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (i < equipped.Count && equipped[i] != null)
            {
                var w  = equipped[i];
                int lv = weaponManager.GetWeaponLevel(w); // 0-based
                SetSlotWeapon(weaponSlots[i], w, lv + 1);
            }
            else
            {
                SetSlotEmpty(weaponSlots[i]);
            }
        }

        // Stat slots — iterate equipped stat types
        var equippedStats = statManager.GetEquippedStats(); // returns list of (StatData, level)
        for (int i = 0; i < statSlots.Length; i++)
        {
            if (i < equippedStats.Count)
            {
                var (sd, lv) = equippedStats[i];
                SetSlotStat(statSlots[i], sd, lv);
            }
            else
            {
                SetSlotEmpty(statSlots[i]);
            }
        }
    }

    // ── Slot Setters ──────────────────────────────────────────────────────
    void SetSlotEmpty(SlotUI s)
    {
        s.bg.color      = new Color(0.12f, 0.12f, 0.12f, 0.7f);
        s.icon.sprite   = null;
        s.icon.color    = new Color(0f, 0f, 0f, 0f);
        s.nameTxt.text  = "—";
        s.levelTxt.text = "";
    }

    void SetSlotWeapon(SlotUI s, WeaponData w, int level)
    {
        // Colour by tier
        s.bg.color = w.tier switch
        {
            WeaponTier.Super  => new Color(0.8f, 0.7f, 0.1f, 0.85f),
            WeaponTier.Fusion => new Color(0.6f, 0.1f, 0.8f, 0.85f),
            _                 => new Color(0.1f, 0.3f, 0.6f, 0.85f)
        };

        if (w.icon != null)
        {
            s.icon.sprite = w.icon;
            s.icon.color  = Color.white;
        }
        else
        {
            s.icon.sprite = null;
            s.icon.color  = new Color(1f, 1f, 1f, 0.25f);
        }

        s.nameTxt.text  = w.weaponName;
        s.levelTxt.text = w.tier == WeaponTier.Normal ? $"Lv{level}" :
                          w.tier == WeaponTier.Super   ? "★" : "★★";
    }

    void SetSlotStat(SlotUI s, StatData sd, int level)
    {
        s.bg.color = new Color(0.1f, 0.45f, 0.1f, 0.85f);

        if (sd.icon != null)
        {
            s.icon.sprite = sd.icon;
            s.icon.color  = Color.white;
        }
        else
        {
            s.icon.sprite = null;
            s.icon.color  = new Color(1f, 1f, 1f, 0.25f);
        }

        s.nameTxt.text  = sd.statName;
        s.levelTxt.text = $"Lv{level}";
    }
}
