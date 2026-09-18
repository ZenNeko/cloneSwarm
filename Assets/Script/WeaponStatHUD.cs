using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD แสดง Weapon Slots (PlayerWeaponManager.MaxWeaponSlots)
/// และ Stat Slots (PlayerStatManager.MaxStatSlots) ของ local player
///
/// ใช้ Inspector references ทั้งหมด — ไม่สร้าง UI ที่ runtime อีกต่อไป
///
/// Slot Hierarchy แนะนำ (ทำซ้ำต่อหนึ่งช่อง ทั้ง Weapon และ Stat):
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
    [Header("── Weapon Slots ────────────────────────")]
    public SlotUI[] weaponSlots = new SlotUI[PlayerWeaponManager.MaxWeaponSlots];

    [Header("── Stat Slots ──────────────────────────")]
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
        SetIcon(s, null);
        if (s.nameTxt)  s.nameTxt.text  = "—";
        if (s.levelTxt) s.levelTxt.text = "";
    }

    /// <summary>
    /// ใส่รูปลงช่อง — **ต้องสลับ `enabled` ด้วย ไม่ใช่แค่ sprite กับสี**
    ///
    /// `Image` ที่ไม่มี sprite ไม่ได้วาดเปล่า มันวาดสี่เหลี่ยมทึบเต็มช่อง
    /// (`Graphic.OnPopulateMesh` ไม่สนใจว่ามี sprite ไหม) จอจึงเคยมีกล่องขาว
    /// เรียงเป็นแถว · builder ของ HUD v2 จึงปิด icon ไว้ตั้งแต่แรกแล้วฝากให้ที่นี่เปิด
    /// ซึ่งโค้ดเดิม **ไม่เคยเปิดเลย** — ไอคอนทุกช่องเลยไม่เคยโผล่สักครั้ง
    /// </summary>
    void SetIcon(SlotUI s, Sprite sprite)
    {
        if (s.icon == null) return;
        s.icon.sprite  = sprite;
        s.icon.enabled = sprite != null;
        s.icon.color   = Color.white;
    }

    void SetSlotWeapon(SlotUI s, WeaponData w, int level)
    {
        if (s.bg) s.bg.color = w.tier switch
        {
            WeaponTier.Super  => colorSuper,
            WeaponTier.Fusion => colorFusion,
            _                 => colorNormal
        };

        SetIcon(s, w.icon);

        if (s.nameTxt)  s.nameTxt.text  = w.DisplayName;   // ชื่อที่โชว์ ไม่ใช่ weaponName ที่เป็น ID

        // ★ / ★★ ถูกถอดออก — ไม่มีฟอนต์ไหนในโปรเจกต์มีอักขระนี้ TMP วาดเป็นกล่องสี่เหลี่ยม
        // ซึ่งอ่านเหมือนฟอนต์เสียมากกว่าเหมือนดาว · ระดับชั้นบอกด้วยสีพื้นช่องอยู่แล้ว
        if (s.levelTxt) s.levelTxt.text = w.tier switch
        {
            WeaponTier.Super  => "SUP",
            WeaponTier.Fusion => "FUS",
            _                 => $"Lv{level}"
        };
    }

    void SetSlotStat(SlotUI s, StatData sd, int level)
    {
        if (s.bg) s.bg.color = colorStat;

        // **`sd.Icon` ไม่ใช่ `sd.icon`** — ช่อง icon ของ StatData ทุกใบในโปรเจกต์ว่างอยู่
        // รูปจริงอยู่ที่ StatIcons.asset คีย์ด้วย StatType ซึ่ง property ตัวใหญ่เป็นคนไปหยิบให้
        // อ่าน field ตรงๆ = ได้ null ทุกใบ แล้วช่องสเตตัสไม่มีรูปสักช่อง (อาการที่เคยเป็น)
        SetIcon(s, sd.Icon);

        // DisplayName ไม่ใช่ statName — statName เป็น ID ไว้ใช้ใน log
        // จอเคยขึ้น 'CRITICALCHANCE' ทับกันจนอ่านไม่ออกเพราะหยิบ ID มาโชว์
        if (s.nameTxt)  s.nameTxt.text  = sd.DisplayName;
        if (s.levelTxt) s.levelTxt.text = $"Lv{level}";
    }
}
