using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD แสดง Weapon Slots (6) และ Stat Slots (6) ของ local player
///
/// ใช้ Inspector references ทั้งหมด — ไม่สร้าง UI ที่ runtime อีกต่อไป
///
/// Slot Hierarchy แนะนำ (ทำซ้ำ × 6 สำหรับทั้ง Weapon และ Stat):
///   SlotRoot (Image = bg)
///   ├── Icon      (Image)
///   ├── LevelText (TMP)   ← มุมบนซ้าย
///   └── NameText  (TMP)   ← แถบล่าง
/// </summary>
public class WeaponStatHUD : MonoBehaviour
{
    // ── Slot descriptor ───────────────────────────────────────────────────
    [System.Serializable]
    public class SlotUI
    {
        public Image           bg;
        public Image           icon;
        public TextMeshProUGUI nameTxt;
        public TextMeshProUGUI levelTxt;
    }

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Weapon Slots (6) ──────────────────────")]
    public SlotUI[] weaponSlots = new SlotUI[PlayerWeaponManager.MaxWeaponSlots];

    [Header("── Stat Slots (6) ────────────────────────")]
    public SlotUI[] statSlots = new SlotUI[PlayerStatManager.MaxStatSlots];

    // ═══════════════════════════════════════════════════════════════════════
    [Header("── Colors ──────────────────────────────────")]
    public Color colorNormal  = new Color(0.10f, 0.30f, 0.60f, 0.85f);
    public Color colorSuper   = new Color(0.80f, 0.70f, 0.10f, 0.85f);
    public Color colorFusion  = new Color(0.60f, 0.10f, 0.80f, 0.85f);
    public Color colorStat    = new Color(0.10f, 0.45f, 0.10f, 0.85f);
    public Color colorEmpty   = new Color(0.12f, 0.12f, 0.12f, 0.70f);

    // ── Internal ──────────────────────────────────────────────────────────
    private PlayerWeaponManager weaponManager;
    private PlayerStatManager   statManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Start()
    {
        ClearAllSlots();
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

        InvokeRepeating(nameof(Refresh), 0f, 0.4f);
    }

    // ── Refresh ───────────────────────────────────────────────────────────
    void Refresh()
    {
        if (weaponManager == null || statManager == null) return;

        // ── Weapon Slots ──
        var equipped = weaponManager.GetEquippedWeapons();
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) continue;

            if (i < equipped.Count && equipped[i] != null)
            {
                var w  = equipped[i];
                int lv = weaponManager.GetWeaponLevel(w) + 1;   // 1-based
                SetSlotWeapon(weaponSlots[i], w, lv);
            }
            else
            {
                SetSlotEmpty(weaponSlots[i]);
            }
        }

        // ── Stat Slots ──
        var equippedStats = statManager.GetEquippedStats();
        for (int i = 0; i < statSlots.Length; i++)
        {
            if (statSlots[i] == null) continue;

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
    void ClearAllSlots()
    {
        foreach (var s in weaponSlots) if (s != null) SetSlotEmpty(s);
        foreach (var s in statSlots)   if (s != null) SetSlotEmpty(s);
    }

    void SetSlotEmpty(SlotUI s)
    {
        if (s.bg)       s.bg.color      = colorEmpty;
        if (s.icon)   { s.icon.sprite   = null; s.icon.color = new Color(0f, 0f, 0f, 0f); }
        if (s.nameTxt)  s.nameTxt.text  = "—";
        if (s.levelTxt) s.levelTxt.text = "";
    }

    void SetSlotWeapon(SlotUI s, WeaponData w, int level)
    {
        if (s.bg) s.bg.color = w.tier switch
        {
            WeaponTier.Super  => colorSuper,
            WeaponTier.Fusion => colorFusion,
            _                 => colorNormal
        };

        if (s.icon)
        {
            s.icon.sprite = w.icon;
            s.icon.color  = w.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        }

        if (s.nameTxt)  s.nameTxt.text  = w.DisplayName;   // ชื่อที่โชว์ ไม่ใช่ weaponName ที่เป็น ID
        if (s.levelTxt) s.levelTxt.text = w.tier switch
        {
            WeaponTier.Super  => "★",
            WeaponTier.Fusion => "★★",
            _                 => $"Lv{level}"
        };
    }

    void SetSlotStat(SlotUI s, StatData sd, int level)
    {
        if (s.bg) s.bg.color = colorStat;

        if (s.icon)
        {
            s.icon.sprite = sd.icon;
            s.icon.color  = sd.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
        }

        if (s.nameTxt)  s.nameTxt.text  = sd.statName;
        if (s.levelTxt) s.levelTxt.text = $"Lv{level}";
    }
}
