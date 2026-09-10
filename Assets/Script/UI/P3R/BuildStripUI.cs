using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.UI.P3R
{
    /// <summary>
    /// แถบ "ของที่ถืออยู่ตอนนี้" สองแถว (WEAPONS / PASSIVES) มุมขวาล่างของจอ Level Up
    ///
    /// **ทำไมต้องมีในจอนี้**
    /// จอ Level Up มีเวลาจำกัด ผู้เล่นต้องตัดสินใจโดยไม่ต้องนึกเองว่าถืออะไรอยู่กี่เลเวล
    /// (design handoff §"แถบ build") ถ้าไม่มีแถบนี้ ผู้เล่นจะเลือกซ้ำของที่เต็มเลเวลแล้ว
    ///
    /// **ที่มาของข้อมูล — สองทาง**
    /// 1. <see cref="RefreshFromLocalPlayer"/> ดึงเองจาก player ที่เป็น owner ในซีน
    ///    (<c>PlayerWeaponManager.GetEquippedWeapons()</c> + <c>PlayerAugmentManager.GetAcquired()</c>)
    ///    ทั้งคู่เป็น public API ที่มีอยู่แล้ว จึงไม่ต้องแก้ฝั่ง gameplay เลย
    /// 2. <see cref="SetEntries"/> ป้อนข้อมูลเข้ามาตรงๆ — ไว้ใช้ในซีนต้นแบบ/เทสต์ที่ไม่มี player จริง
    ///
    /// ตัวนี้ **ไม่ subscribe event ใดๆ** เพราะ PlayerWeaponManager ไม่มี event แจ้ง "ของเปลี่ยน"
    /// คนเรียกจึงต้องสั่ง refresh เอง — ในทางปฏิบัติคือ <c>LevelUpUI.Show()</c> ซึ่งเกิดตอนที่ข้อมูลนิ่งพอดี
    ///
    /// โครงที่ builder สร้างให้ (ดู P3RLevelUpSceneBuilder):
    ///   BuildStrip
    ///   ├─ Row_Weapons   (พื้น + เส้นซ้าย 6px + ป้าย + SlotArea)
    ///   ├─ Row_Passives  (เหมือนกัน)
    ///   └─ SlotTemplate  (ปิดไว้ — ถูก clone ลง SlotArea ทั้งสองแถว)
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildStripUI : MonoBehaviour
    {
        /// <summary>ข้อมูลหนึ่งช่อง — ตั้งใจให้เป็น struct เปล่าๆ ไม่ผูกกับ WeaponData/AugmentData
        /// เพื่อให้ซีนต้นแบบป้อน placeholder ได้โดยไม่ต้องมี ScriptableObject จริง</summary>
        [System.Serializable]
        public struct Entry
        {
            public Sprite icon;
            [Tooltip("ตัวย่อสามตัวอักษรแบบเดียวกับ HUD (BLD / ARC / ORB) — โชว์ตอนไม่มีไอคอน")]
            public string abbrev;
            [Tooltip("เลเวลที่โชว์มุมล่างขวา · 0 = ไม่โชว์")]
            public int    level;
            [Tooltip("ช่องเด่น (Super/Fusion หรือ augment หายาก) — เลข Lv เป็นสีอำพัน")]
            public bool   highlight;
        }

        // ═══════════════════════════════════════════════════════════════════
        // WIRING
        // ═══════════════════════════════════════════════════════════════════
        [Header("── Wiring ─────────────────────────────")]
        [Tooltip("ช่องต้นแบบที่จะถูก clone — ปิด (SetActive false) ไว้เสมอ")]
        public BuildStripSlot slotTemplate;

        [Tooltip("ที่วางช่องของแถว WEAPONS")]
        public RectTransform weaponSlotArea;
        [Tooltip("ที่วางช่องของแถว PASSIVES")]
        public RectTransform passiveSlotArea;

        [Header("── Layout ─────────────────────────────")]
        [Tooltip("จำนวนช่องที่โชว์เสมอ — ช่องเกินของที่มีจะเป็นกรอบเส้นประ\n" +
                 "6 = PlayerWeaponManager.MaxWeaponSlots")]
        public int weaponSlotCount  = 6;
        public int passiveSlotCount = 6;

        [Tooltip("ขนาดช่อง (px ที่กรอบ 1920) — design handoff ระบุ 62")]
        public float slotSize = 62f;
        [Tooltip("ระยะห่างระหว่างช่อง")]
        public float slotGap = 8f;

        [Header("── Colors ─────────────────────────────")]
        [Tooltip("สีประเภท WEAPON — พื้นช่องใช้สีนี้ที่ 20% alpha")]
        public Color weaponColor  = new Color32(0x40, 0x73, 0xD9, 0xFF);
        public Color passiveColor = new Color32(0x4D, 0xA6, 0x59, 0xFF);
        [Tooltip("กรอบช่องที่มีของ")]
        public Color filledBorder = new Color(1f, 1f, 1f, 0.26f);
        [Tooltip("กรอบเส้นประของช่องว่าง")]
        public Color emptyBorder  = new Color(1f, 1f, 1f, 0.20f);
        [Tooltip("เลข Lv ปกติ")]
        public Color levelColor     = Color.white;
        [Tooltip("เลข Lv ของช่องเด่น (Super/Fusion) — Amber ตาม design token")]
        public Color levelHighlight = new Color32(0xD9, 0x9A, 0x1A, 0xFF);

        // ── runtime ────────────────────────────────────────────────────────
        private readonly List<BuildStripSlot> weaponSlots  = new();
        private readonly List<BuildStripSlot> passiveSlots = new();
        private bool built;

        private void Awake()
        {
            if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
            EnsureSlots();
        }

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>ป้อนข้อมูลตรงๆ — ใช้เมื่อคนเรียกมีข้อมูลอยู่แล้ว หรือในซีนต้นแบบ</summary>
        public void SetEntries(IList<Entry> weapons, IList<Entry> passives)
        {
            EnsureSlots();
            FillRow(weaponSlots,  weapons,  weaponColor);
            FillRow(passiveSlots, passives, passiveColor);
        }

        /// <summary>
        /// ดึงของจาก player ที่เป็น owner ในซีนแล้วอัปเดตแถบ
        /// คืน false เมื่อยังหา player ไม่เจอ (เช่นเปิดซีนต้นแบบเดี่ยวๆ) — คนเรียกจะได้ปล่อยค่า placeholder ไว้
        /// </summary>
        public bool RefreshFromLocalPlayer()
        {
            var pwm = FindLocalWeaponManager();
            if (pwm == null) return false;

            var weapons = new List<Entry>();
            foreach (var w in pwm.GetEquippedWeapons())
            {
                if (w == null) continue;
                weapons.Add(new Entry
                {
                    icon      = w.icon,
                    abbrev    = Abbrev(w.weaponName),
                    // GetWeaponLevel คืน 0-indexed ทั้งโค้ดเบส — ที่ตาเห็นต้อง +1 เสมอ
                    level     = pwm.GetWeaponLevel(w) + 1,
                    highlight = w.tier != WeaponTier.Normal
                });
            }

            // "PASSIVES" ในแบบ = augment ที่เก็บได้ใน run นี้
            // (passive weapon ของตัวละครไม่ถูกนับใน PlayerWeaponManager.slots จึงดึงไม่ได้จากที่นี่ — ดูรายงาน)
            var passives = new List<Entry>();
            var pam = pwm.GetComponent<PlayerAugmentManager>();
            if (pam != null)
            {
                foreach (var a in pam.GetAcquired())
                {
                    if (a == null) continue;
                    passives.Add(new Entry
                    {
                        icon      = a.icon,
                        abbrev    = Abbrev(a.augmentName),
                        level     = pam.GetStackCount(a),
                        highlight = a.rarity >= AugmentRarity.Gold
                    });
                }
            }

            SetEntries(weapons, passives);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        // INTERNAL
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>สร้างช่องครั้งเดียวแล้วใช้ซ้ำ — clone/destroy ทุกครั้งที่ refresh จะกระตุก GC ตอนเกมหยุด</summary>
        private void EnsureSlots()
        {
            if (built || slotTemplate == null) return;
            built = true;

            slotTemplate.gameObject.SetActive(false);
            SpawnRow(weaponSlotArea,  weaponSlotCount,  weaponSlots,  "W");
            SpawnRow(passiveSlotArea, passiveSlotCount, passiveSlots, "P");
        }

        private void SpawnRow(RectTransform area, int count, List<BuildStripSlot> into, string tag)
        {
            if (area == null) return;
            for (int i = 0; i < count; i++)
            {
                var slot = Instantiate(slotTemplate, area);
                slot.name = $"Slot_{tag}{i}";
                slot.gameObject.SetActive(true);

                var rt = (RectTransform)slot.transform;
                // วางจากซ้ายไปขวาด้วยมือ ไม่ใช้ LayoutGroup — ตำแหน่งต้องตรงกับ design ที่ px
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(slotSize, slotSize);
                rt.anchoredPosition = new Vector2(i * (slotSize + slotGap), 0f);

                into.Add(slot);
            }
        }

        private void FillRow(List<BuildStripSlot> slots, IList<Entry> data, Color typeColor)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (data != null && i < data.Count)
                    slots[i].ShowEntry(data[i], typeColor, filledBorder,
                                       data[i].highlight ? levelHighlight : levelColor);
                else
                    slots[i].ShowEmpty(emptyBorder);
            }
        }

        /// <summary>ตัวย่อสามตัวอักษรแบบ HUD — ตัดช่องว่าง/อักขระพิเศษออกก่อนเพื่อไม่ให้ได้ "B_ " แปลกๆ</summary>
        private static string Abbrev(string full)
        {
            if (string.IsNullOrEmpty(full)) return "???";
            var sb = new System.Text.StringBuilder(3);
            foreach (char c in full)
            {
                if (!char.IsLetterOrDigit(c)) continue;
                sb.Append(char.ToUpperInvariant(c));
                if (sb.Length == 3) break;
            }
            return sb.Length > 0 ? sb.ToString() : "???";
        }

        /// <summary>
        /// หา PlayerWeaponManager ของเครื่องตัวเอง
        /// ใช้แพตเทิร์นเดียวกับ StatusHUDUI/QuestCarryHUD — วนหา owner แทนที่จะพึ่ง NetworkManager
        /// เพราะ UI ตัวนี้ต้องทำงานได้แม้เปิดซีนเดี่ยวๆ ที่ไม่มี NetworkManager
        /// </summary>
        private static PlayerWeaponManager FindLocalWeaponManager()
        {
            var all = FindObjectsByType<PlayerWeaponManager>(FindObjectsSortMode.None);
            foreach (var p in all)
                if (p != null && p.IsOwner) return p;
            return null;
        }
    }

    /// <summary>
    /// ช่องของหนึ่งช่องในแถบ build — เป็นแค่ที่เก็บ reference ให้ <see cref="BuildStripUI"/> สั่ง
    ///
    /// แยกเป็นคลาส (ไม่ใช่ struct ในตัวแม่) เพราะช่องถูก clone จาก template ในซีน
    /// การผูก reference จึงต้องอยู่ติดกับ GameObject ไม่ใช่ในลิสต์ของตัวแม่
    ///
    /// **กรอบเส้นประ:** uGUI ไม่มีเส้นประในตัว และโปรเจกต์นี้ไม่มี sprite เส้นประ
    /// จึงทำเป็น <see cref="Image"/> สี่ด้าน แล้วสลับ sprite เป็น texture ลายขีดที่สร้างในโค้ด
    /// (<see cref="DashSprite"/>) ตอน runtime · texture ที่สร้างในโค้ดไม่ถูก serialize ลงซีน
    /// จึงต้องสร้างใหม่ทุกครั้งที่โหลด — static cache ทำให้ทั้งเกมมีใบเดียว
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildStripSlot : MonoBehaviour
    {
        [Tooltip("พื้นช่อง — สีประเภทที่ 20% alpha")]
        public Image fill;
        [Tooltip("ไอคอนของ — ซ่อนเมื่อไม่มี sprite แล้วโชว์ตัวย่อแทน")]
        public Image icon;
        [Tooltip("เส้นขอบสี่ด้าน เรียง บน/ล่าง/ซ้าย/ขวา")]
        public Image[] borderEdges = new Image[4];
        [Tooltip("ตัวย่อสามตัวอักษร — โชว์เมื่อไม่มีไอคอน")]
        public TextMeshProUGUI abbrevLabel;
        [Tooltip("เลข Lv มุมล่างขวา")]
        public TextMeshProUGUI levelLabel;

        private static Sprite dashSprite;

        public void ShowEntry(BuildStripUI.Entry e, Color typeColor, Color border, Color levelColor)
        {
            if (fill != null)
            {
                fill.enabled = true;
                fill.color   = new Color(typeColor.r, typeColor.g, typeColor.b, 0.20f);
            }

            bool hasIcon = e.icon != null;
            if (icon != null)
            {
                icon.enabled = hasIcon;
                icon.sprite  = e.icon;
                icon.color   = Color.white;
            }
            if (abbrevLabel != null)
            {
                abbrevLabel.enabled = !hasIcon;
                abbrevLabel.text    = e.abbrev;
            }

            if (levelLabel != null)
            {
                levelLabel.enabled = e.level > 0;
                levelLabel.text    = e.level > 0 ? $"Lv{e.level}" : "";
                levelLabel.color   = levelColor;
            }

            SetBorder(border, dashed: false);
        }

        public void ShowEmpty(Color border)
        {
            if (fill != null)        fill.enabled        = false;
            if (icon != null)        icon.enabled        = false;
            if (abbrevLabel != null) abbrevLabel.enabled = false;
            if (levelLabel != null)  levelLabel.enabled  = false;
            SetBorder(border, dashed: true);
        }

        private void SetBorder(Color c, bool dashed)
        {
            var sprite = dashed ? DashSprite() : null;
            foreach (var edge in borderEdges)
            {
                if (edge == null) continue;
                edge.enabled = true;
                edge.color   = c;
                edge.sprite  = sprite;
                // Tiled ทำให้ลายขีดซ้ำตามความยาวจริงของด้าน แทนที่จะยืดขีดเดียวจนเบลอ
                edge.type    = dashed ? Image.Type.Tiled : Image.Type.Simple;
                edge.pixelsPerUnitMultiplier = 1f;
            }
        }

        /// <summary>ลายขีด 6 ทึบ / 5 โปร่ง — สร้างครั้งเดียวต่อการรันหนึ่งครั้ง</summary>
        private static Sprite DashSprite()
        {
            if (dashSprite != null) return dashSprite;

            const int period = 11, solid = 6;
            var tex = new Texture2D(period, 1, TextureFormat.RGBA32, false)
            {
                name       = "P3R_DashPattern",
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Repeat,
                hideFlags  = HideFlags.HideAndDontSave
            };
            for (int x = 0; x < period; x++)
                tex.SetPixel(x, 0, x < solid ? Color.white : Color.clear);
            tex.Apply();

            dashSprite = Sprite.Create(tex, new Rect(0, 0, period, 1), new Vector2(0.5f, 0.5f), 1f);
            dashSprite.name      = "P3R_DashSprite";
            dashSprite.hideFlags = HideFlags.HideAndDontSave;
            return dashSprite;
        }
    }
}
