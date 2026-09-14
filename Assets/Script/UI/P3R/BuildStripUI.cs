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
                 "ต้องเท่า MaxWeaponSlots / MaxStatSlots — สโมกเทสต์เช็คให้\n" +
                 "ตั้งไม่ตรง = ช่องที่โชว์ไม่ตรงกับจำนวนที่ถือได้จริง")]
        public int weaponSlotCount  = PlayerWeaponManager.MaxWeaponSlots;
        public int passiveSlotCount = PlayerStatManager.MaxStatSlots;

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

        [Header("── Auto refresh ───────────────────────")]
        [Tooltip("วินาทีต่อการดึงของจากผู้เล่นเอง · 0 = ไม่ดึงเอง\n" +
                 "จอ Level Up ใช้ 0 เพราะ LevelUpUI.Show() สั่ง refresh ให้ตอนเปิด\n" +
                 "HUD ตอนเล่นต้องมากกว่า 0 เพราะไม่มีใครสั่ง และของเปลี่ยนระหว่างเล่น")]
        public float autoRefreshInterval = 0f;

        // ── runtime ────────────────────────────────────────────────────────
        private readonly List<BuildStripSlot> weaponSlots  = new();
        private readonly List<BuildStripSlot> passiveSlots = new();
        private bool built;

        private void Awake()
        {
            HideSceneTemplate();
            EnsureSlots();
        }

        /// <summary>
        /// ปิดแม่แบบ **เฉพาะตอนที่มันเป็น object ในซีน**
        ///
        /// `slotTemplate` ควรเป็น prefab asset (builder เซฟไว้ที่
        /// <c>Assets/Prefab/UI/P3R/BuildStripSlot.prefab</c>) ซึ่งปิดมาในตัวอยู่แล้ว
        /// **สั่ง SetActive ใส่ prefab asset = ไปแก้ไฟล์ต้นฉบับ** ทุกซีนที่ใช้แม่แบบนี้
        /// โดนไปด้วย และใน Editor มันทำให้ไฟล์ dirty ทั้งที่ไม่มีใครตั้งใจแก้
        ///
        /// `gameObject.scene.IsValid()` เป็น false สำหรับ asset — เช็คนี้ใช้ได้ทั้งใน
        /// เอดิเตอร์และในบิลด์ ไม่ต้องพึ่ง UnityEditor API
        ///
        /// ที่ยังต้องปิดให้กรณีซีน เพราะซีนที่ยังไม่ถูกสร้างใหม่จาก builder ยังชี้
        /// แม่แบบที่เป็นลูกของแถบอยู่ — ถ้าไม่ปิดมันจะโผล่เป็นช่องเปล่าค้างในแถว
        /// </summary>
        private void HideSceneTemplate()
        {
            if (slotTemplate == null) return;
            var go = slotTemplate.gameObject;
            if (!go.scene.IsValid()) return;      // prefab asset — ห้ามแตะ
            if (go.activeSelf) go.SetActive(false);
        }

        /// <summary>
        /// ดึงเองเป็นจังหวะเมื่อ <see cref="autoRefreshInterval"/> มากกว่า 0
        ///
        /// เดินจังหวะแทนการ subscribe event เพราะ `PlayerWeaponManager` กับ
        /// `PlayerStatManager` ไม่มี event แจ้ง "ของเปลี่ยน" — ตัวที่มีคือ HUD เดิม
        /// (`WeaponStatHUD`) ซึ่งก็เดินจังหวะ 0.4s เหมือนกัน ไม่ใช่ท่าใหม่
        ///
        /// `RefreshFromLocalPlayer` คืน false ตอนยังไม่มีผู้เล่นในซีน (ต้นเกม / กำลังโหลด)
        /// รอบถัดไปจะเจอเอง จึงไม่ต้องมีสถานะ "รอผู้เล่น" แยกต่างหาก
        /// </summary>
        private void OnEnable()
        {
            if (autoRefreshInterval <= 0f) return;
            InvokeRepeating(nameof(RefreshTick), 0f, autoRefreshInterval);
        }

        private void OnDisable() => CancelInvoke(nameof(RefreshTick));

        private void RefreshTick() => RefreshFromLocalPlayer();

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

            // ── แถว PASSIVES = สเตตัส ตามที่ HUD ตอนเล่นเรียก ────────────────
            //
            // เดิมแถวนี้ดึงจาก `PlayerAugmentManager` อย่างเดียว ซึ่ง **ไม่ใช่ของเดียวกับ
            // ที่ HUD เรียกว่า PASSIVES** — HUD โชว์สเตตัส (DAMAGE · MAXHEALTH · …)
            // ส่วน augment ได้เฉพาะเลเวลที่กำหนดไว้เท่านั้น การเล่นปกติจึงไม่มีสักใบ
            // ผลคือแถวนี้ว่างเปล่าทั้งที่ผู้เล่นถือสเตตัสอยู่ห้าตัว — จอ Level Up
            // ซึ่งมีไว้ให้ตัดสินใจ กลับไม่บอกว่าตัวเองถืออะไรอยู่
            //
            // สเตตัสมาก่อน แล้วต่อด้วย augment ถ้ายังเหลือช่อง
            var passives = new List<Entry>();

            var psm = pwm.GetComponent<PlayerStatManager>();
            if (psm != null)
            {
                foreach (var (sd, lv) in psm.GetEquippedStats())
                {
                    if (sd == null) continue;
                    passives.Add(new Entry
                    {
                        // `sd.Icon` ไม่ใช่ `sd.icon` — รูปจริงอยู่ที่ StatIcons.asset
                        // ช่อง icon ของ StatData ทุกใบในโปรเจกต์ว่างอยู่ (ดู StatData.Icon)
                        icon      = sd.Icon,
                        abbrev    = Abbrev(sd.statName),
                        level     = lv,
                        highlight = false
                    });
                }
            }

            var pam = pwm.GetComponent<PlayerAugmentManager>();
            if (pam != null)
            {
                // GetAcquired คืนใบซ้ำหนึ่งใบต่อหนึ่ง stack — ต้องยุบเอง
                // ไม่งั้น augment ที่ซ้อนสาม stack จะกินสามช่องด้วยรูปเดียวกัน
                var seen = new HashSet<AugmentData>();
                foreach (var a in pam.GetAcquired())
                {
                    if (a == null || !seen.Add(a)) continue;
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

            HideSceneTemplate();
            SpawnRow(weaponSlotArea,  weaponSlotCount,  weaponSlots,  "W");
            SpawnRow(passiveSlotArea, passiveSlotCount, passiveSlots, "P");
        }

        private void SpawnRow(RectTransform area, int count, List<BuildStripSlot> into, string tag)
        {
            if (area == null) return;

            // ── ล้างช่องที่ค้างมาจากตอนสร้างซีนก่อน ────────────────────────────
            //
            // builder เรียก SetEntries ด้วยข้อมูลจำลองตอน build เพื่อให้ภาพต้นแบบดูมีของ
            // ซึ่งทำให้ EnsureSlots สร้าง object ช่องจริงลงซีนแล้วถูกเซฟติดไปด้วย
            // พอเกมรัน `built` เป็น false อีกครั้ง มันจึงสร้าง **ชุดที่สองซ้อนทับ**
            // ชุดเก่ายังอยู่ข้างใต้ ค่าจำลอง (ARC Lv2 · ORB Lv1 · ATK Lv2 · HST Lv1)
            // จึงโผล่ออกมาตามช่องที่ชุดใหม่เป็นช่องว่าง — ผู้เล่นเห็นของที่ตัวเองไม่มี
            //
            // ล้างที่นี่แทนการไปห้าม builder ใส่ตัวอย่าง เพราะกันได้ทุกที่มา
            // ไม่ใช่แค่กรณีที่นึกออกตอนนี้
            foreach (var stale in area.GetComponentsInChildren<BuildStripSlot>(true))
            {
                if (stale == slotTemplate) continue;

                // **ปิดก่อนแล้วค่อยสั่งทำลาย** — `Destroy` ใน play mode เลื่อนไปปลายเฟรม
                // ของเก่าจึงยังวาดอยู่และยังถูกนับเจอตลอดเฟรมนั้น · การปิดมีผลทันที
                stale.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(stale.gameObject);
                else                       DestroyImmediate(stale.gameObject);
            }

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
}
