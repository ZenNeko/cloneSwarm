using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloneSwarm.Meta
{
    /// <summary>
    /// ร้านอัปเกรดถาวร (Talent Shop) ใน MenuScene — ทรงเดียวกับร้าน meta ของ LoL Swarm
    ///
    ///   ซ้าย  = grid ของ TalentTileUI (คลิก = เลือก ไม่ใช่ซื้อ)
    ///   ขวา   = panel รายละเอียดของช่องที่เลือก + ปุ่มซื้อปุ่มเดียว
    ///
    /// Setup:
    ///   1. สร้าง Panel ชื่อ TalentShopPanel ใต้ Canvas ของ MenuScene
    ///   2. ใส่ component นี้
    ///   3. ฝั่งซ้าย: GameObject ที่มี **Grid Layout Group** (3 คอลัมน์) → tilesContainer
    ///      + TalentTile prefab → tileTemplate (SetActive = false)
    ///   4. ฝั่งขวา: ลาก detail* ตาม Inspector
    ///   5. ที่ MenuManager: ลาก panel → Talent Shop Panel + ปุ่มเปิดร้าน → Talent Shop Button
    /// </summary>
    public class TalentShopUI : MonoBehaviour
    {
        [Header("── Grid (ซ้าย) ────────────────────────")]
        [Tooltip("GameObject ที่มี Grid Layout Group — แนะนำ 3 คอลัมน์")]
        public Transform  tilesContainer;
        [Tooltip("TalentTile prefab — SetActive = false ไว้")]
        public GameObject tileTemplate;

        [Header("── Header ─────────────────────────────")]
        public TextMeshProUGUI goldText;
        public TextMeshProUGUI statsText;    // "Runs 12 · Wins 3 · Best 14:22"

        [Header("── Detail Panel (ขวา) ─────────────────")]
        public Image           detailIcon;
        public TextMeshProUGUI detailName;
        public TextMeshProUGUI detailDescription;
        public TextMeshProUGUI detailLevelText;      // "Lv 3 / 5"
        [Tooltip("ค่าปัจจุบัน เช่น \"+9% Damage\"")]
        public TextMeshProUGUI detailCurrentValue;
        [Tooltip("ลูกศร › ระหว่างค่าปัจจุบันกับค่าถัดไป — ซ่อนเองตอนตัน")]
        public GameObject      detailArrow;
        [Tooltip("ค่าหลังอัป เช่น \"+12% Damage\"")]
        public TextMeshProUGUI detailNextValue;
        [Tooltip("ข้อความ \"ไปถึงเลเวลสูงสุดแล้ว\" — โชว์เฉพาะตอนตัน")]
        public GameObject      detailMaxedNote;

        [Header("── Buy Button ─────────────────────────")]
        public Button          buyButton;
        public TextMeshProUGUI buyCostText;
        public Image           buyCoinIcon;
        public Color           buyAffordableColor = new Color(0.173f, 0.773f, 0.627f);
        public Color           buyTooPoorColor    = new Color(0.141f, 0.165f, 0.212f);

        [Header("── Buttons ────────────────────────────")]
        [Tooltip("ปุ่มลบข้อมูลทั้งหมด (dev/settings) — ปล่อยว่างได้")]
        public Button resetButton;
        [Tooltip("Panel ยืนยันก่อนลบข้อมูล — ปล่อยว่าง = ลบทันที")]
        public GameObject resetConfirmPanel;
        public Button     resetConfirmYesButton;
        public Button     resetConfirmNoButton;

        readonly List<TalentTileUI> tiles = new();
        TalentData selected;

        // ═══════════════════════════════════════════════════════════════════
        void Awake()
        {
            if (buyButton)   buyButton.onClick.AddListener(OnBuyClicked);
            if (resetButton) resetButton.onClick.AddListener(OnResetClicked);
            if (resetConfirmYesButton) resetConfirmYesButton.onClick.AddListener(OnResetConfirmed);
            if (resetConfirmNoButton)  resetConfirmNoButton.onClick.AddListener(HideResetConfirm);
            HideResetConfirm();
        }

        void OnEnable()
        {
            MetaProgression.OnGoldChanged += HandleGoldChanged;
            BuildTiles();
            RefreshAll();
        }

        void OnDisable()
        {
            MetaProgression.OnGoldChanged -= HandleGoldChanged;
            SaveManager.FlushIfDirty();
        }

        void HandleGoldChanged(int _) => RefreshAll();

        // ═══════════════════════════════════════════════════════════════════
        // Build
        // ═══════════════════════════════════════════════════════════════════
        void BuildTiles()
        {
            if (tilesContainer == null || tileTemplate == null)
            {
                Debug.LogError("[TalentShop] ยังไม่ได้ assign tilesContainer / tileTemplate");
                return;
            }

            var db = MetaDatabase.Instance;
            if (db == null) return;

            tileTemplate.SetActive(false);

            foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
            tiles.Clear();

            foreach (var t in db.GetSortedTalents())
            {
                var go = Instantiate(tileTemplate, tilesContainer);
                go.name = $"Tile_{t.talentId}";
                go.SetActive(true);

                var tile = go.GetComponent<TalentTileUI>() ?? go.AddComponent<TalentTileUI>();
                tile.SetData(t, Select);
                tiles.Add(tile);
            }

            // เลือกช่องแรกไว้ก่อน — panel ขวาจะได้ไม่ว่างตอนเปิดร้าน
            if (selected == null && tiles.Count > 0)
                selected = tiles[0].Talent;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Select
        // ═══════════════════════════════════════════════════════════════════
        void Select(TalentData t)
        {
            selected = t;
            RefreshSelection();
            RefreshDetail();
        }

        void RefreshSelection()
        {
            foreach (var tile in tiles)
                if (tile != null) tile.SetSelected(tile.Talent == selected);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Refresh
        // ═══════════════════════════════════════════════════════════════════
        void RefreshAll()
        {
            if (goldText != null) goldText.text = $"{MetaProgression.Gold:N0}";

            if (statsText != null)
            {
                var d = SaveManager.Data;
                int m = Mathf.FloorToInt(d.bestTimeSec / 60f);
                int s = Mathf.FloorToInt(d.bestTimeSec % 60f);
                statsText.text = $"Runs {d.totalRuns} · Wins {d.totalWins} · Best {m:00}:{s:00}";
            }

            foreach (var tile in tiles) tile?.Refresh();

            RefreshSelection();
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if (selected == null) return;

            int  level = MetaProgression.GetTalentLevel(selected);
            int  maxLv = selected.MaxLevel;
            bool maxed = level >= maxLv;
            int  cost  = selected.GetCostToUpgrade(level);
            bool canBuy = !maxed && cost >= 0 && MetaProgression.Gold >= cost;

            if (detailIcon != null)
            {
                detailIcon.sprite  = selected.icon;
                detailIcon.enabled = selected.icon != null;
                detailIcon.color   = selected.tintColor;
            }
            if (detailName        != null) detailName.text        = selected.talentName;
            if (detailDescription != null) detailDescription.text = selected.Description;
            if (detailLevelText   != null) detailLevelText.text   = $"Lv {level} / {maxLv}";

            // ค่าปัจจุบัน  ›  ค่าหลังอัป   (ตันแล้วเหลือแค่ค่าปัจจุบัน + หมายเหตุ)
            if (detailCurrentValue != null) detailCurrentValue.text = selected.FormatValue(level);
            if (detailNextValue    != null)
            {
                detailNextValue.text = selected.FormatNextValue(level);
                detailNextValue.gameObject.SetActive(!maxed);
            }
            if (detailArrow     != null) detailArrow.SetActive(!maxed);
            if (detailMaxedNote != null) detailMaxedNote.SetActive(maxed);

            // ปุ่มซื้อ
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(!maxed);
                buyButton.interactable = canBuy;

                var img = buyButton.GetComponent<Image>();
                if (img != null) img.color = canBuy ? buyAffordableColor : buyTooPoorColor;
            }
            if (buyCostText != null) buyCostText.text    = maxed ? "" : $"{cost:N0}";
            if (buyCoinIcon != null) buyCoinIcon.enabled = !maxed;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Buy
        // ═══════════════════════════════════════════════════════════════════
        void OnBuyClicked()
        {
            if (selected == null) return;

            // TryUpgradeTalent → OnGoldChanged → RefreshAll ให้เอง
            // แต่เรียกตรงๆ ด้วยกัน edge case ที่ราคาเป็น 0
            if (MetaProgression.TryUpgradeTalent(selected))
                RefreshAll();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Reset profile
        // ═══════════════════════════════════════════════════════════════════
        void OnResetClicked()
        {
            if (resetConfirmPanel != null) { resetConfirmPanel.SetActive(true); return; }
            OnResetConfirmed();
        }

        void OnResetConfirmed()
        {
            SaveManager.ResetProfile();
            HideResetConfirm();
            RefreshAll();
        }

        void HideResetConfirm()
        {
            if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
        }
    }
}
