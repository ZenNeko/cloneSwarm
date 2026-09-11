using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test manager สำหรับ WeaponTestScene — singleton
///
/// **Features:**
///   • Quick equip weapon/ability ผ่าน button list (ล้างของเดิมก่อน แล้วดันขึ้นเลเวลสูงสุด)
///   • DPS counter (auto-reset ทุก 1s)
///   • Respawn dummies (single + bulk)
///   • Stat override sliders → Damage ×, Ability Haste, Crit chance
///
/// **Setup:** ไม่ต้องลากอะไรเอง — `Tools → Clone Swarm → Weapon Test → Setup Scene`
/// สร้าง UI ให้ครบและ assign ทุกช่องอัตโนมัติ
///
/// `allWeapons` / `allAbilities` เติมเองจากโปรเจกต์ตอน Start (Editor เท่านั้น) จึง**ไม่มีวันเก่า**
/// เดิมเป็น array ที่ต้องลากมือ ซึ่งว่างเปล่ามาตลอดและไม่มีอะไรบอกว่ามันว่าง
/// </summary>
public class WeaponTestManager : MonoBehaviour
{
    public static WeaponTestManager Instance { get; private set; }

    [Header("Asset Refs")]
    [Tooltip("ปล่อยว่างได้ — ตอน Start จะดึงจากโปรเจกต์ให้เองใน Editor")]
    public WeaponData[] allWeapons;
    public AbilityData[] allAbilities;

    [Tooltip("ดึงรายชื่อจากโปรเจกต์ใหม่ทุกครั้งที่ Start (Editor เท่านั้น) — " +
             "ปิดถ้าอยากล็อกรายการที่ลากไว้เอง")]
    public bool autoRefreshFromProject = true;

    [Header("Dummy Spawn")]
    [Tooltip("TargetDummy prefab — ใช้ respawn เมื่อ dummy ตาย")]
    public GameObject dummyPrefab;

    [Header("UI — Weapon Buttons")]
    [Tooltip("Container ใน Canvas ที่ buttons จะ spawn เข้าไป (VerticalLayoutGroup)")]
    public Transform weaponButtonContainer;
    [Tooltip("Button prefab — มี TextMeshProUGUI child")]
    public Button buttonPrefab;

    [Header("UI — DPS Counter")]
    public TextMeshProUGUI dpsLabel;
    public TextMeshProUGUI totalDamageLabel;

    [Header("UI — Stat Sliders")]
    public Slider damageSlider;        // 1×–10× multiplier
    public Slider hasteSlider;         // 0–200 haste
    public Slider critSlider;          // 0–1.0
    public TextMeshProUGUI statsLabel;

    [Header("UI — Reset")]
    public Button respawnAllButton;
    public Button resetStatsButton;

    // ── Runtime ───────────────────────────────────────────────────────────
    float _totalDamage;
    float _dpsAccum;
    float _dpsTimer;
    float _currentDps;

    // crit ไม่มีช่อง temp ใน PlayerStatManager จึงต้องจำค่าที่ใส่ไปแล้ว
    // แล้วบวกเฉพาะส่วนต่าง ไม่งั้นเลื่อนสไลเดอร์ทีเดียวจะสะสมทบไปเรื่อยๆ
    float _appliedCrit;

    // Start ทำงานก่อนผู้เล่น spawn เสมอ — ค่าสไลเดอร์รอบแรกจึงตกพื้นและป้ายค้างอยู่ที่
    // "ยังไม่มีผู้เล่น" ตลอดกาล เพราะเดิมป้ายอัปเดตเฉพาะตอนขยับสไลเดอร์
    bool _hadPlayer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        RefreshAssetListsInEditor();
        BuildWeaponButtons();

        if (respawnAllButton) respawnAllButton.onClick.AddListener(RespawnAllDummies);
        if (resetStatsButton) resetStatsButton.onClick.AddListener(ResetStats);

        if (damageSlider) damageSlider.onValueChanged.AddListener(_ => PushStats());
        if (hasteSlider)  hasteSlider.onValueChanged.AddListener(_ => PushStats());
        if (critSlider)   critSlider.onValueChanged.AddListener(_ => PushStats());

        PushStats();
    }

    void Update()
    {
        _dpsTimer += Time.deltaTime;
        if (_dpsTimer >= 1f)
        {
            _currentDps = _dpsAccum;
            _dpsAccum   = 0f;
            _dpsTimer   = 0f;
        }
        if (dpsLabel)          dpsLabel.text         = $"DPS: {_currentDps:F0}";
        if (totalDamageLabel)  totalDamageLabel.text = $"Total: {_totalDamage:F0}";

        // ผู้เล่นเพิ่งเข้ามา → ดันค่าสไลเดอร์ที่ตั้งไว้เข้าไปให้ครั้งหนึ่ง
        bool hasPlayer = GetLocalPlayer() != null;
        if (hasPlayer != _hadPlayer)
        {
            _hadPlayer = hasPlayer;
            if (hasPlayer) _appliedCrit = 0f;   // stat manager ตัวใหม่ เริ่มนับส่วนต่างใหม่
            PushStats();
        }
    }

    // ── Asset lists ───────────────────────────────────────────────────────
    /// <summary>
    /// ดึง WeaponData / AbilityData ทุกตัวในโปรเจกต์ — Editor เท่านั้น
    /// AssetDatabase ไม่มีในบิลด์ จึงครอบ UNITY_EDITOR ไว้ · harness ตัวนี้ใช้ใน Editor อยู่แล้ว
    /// </summary>
    void RefreshAssetListsInEditor()
    {
#if UNITY_EDITOR
        if (!autoRefreshFromProject && allWeapons != null && allWeapons.Length > 0) return;

        var weapons = new List<WeaponData>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:WeaponData"))
        {
            var w = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (w != null) weapons.Add(w);
        }
        // เรียง tier ก่อน (Normal → Super → Fusion) แล้วค่อยชื่อ — ลิสต์ยาว 46 ปุ่ม หาไม่เจอถ้าไม่เรียง
        weapons.Sort((a, b) => a.tier != b.tier
            ? a.tier.CompareTo(b.tier)
            : string.Compare(a.weaponName, b.weaponName, System.StringComparison.OrdinalIgnoreCase));
        allWeapons = weapons.ToArray();

        var abilities = new List<AbilityData>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:AbilityData"))
        {
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) abilities.Add(a);
        }
        allAbilities = abilities.ToArray();

        Debug.Log($"[WeaponTest] โหลดจากโปรเจกต์ — weapon {allWeapons.Length} · ability {allAbilities.Length}");
#endif
    }

    // ── Damage tracking (called from TargetDummy) ─────────────────────────
    public void AddDamage(float amount)
    {
        _totalDamage += amount;
        _dpsAccum    += amount;
    }

    public void ResetDamageStats()
    {
        _totalDamage = 0f;
        _dpsAccum    = 0f;
        _currentDps  = 0f;
    }

    // ── Weapon equip buttons ──────────────────────────────────────────────
    void BuildWeaponButtons()
    {
        if (weaponButtonContainer == null || buttonPrefab == null)
        {
            // เดิมตรงนี้ return เงียบ ทำให้ซีนที่ยังไม่ได้ต่อสายดูเหมือน "ยังไม่มีอาวุธ"
            Debug.LogError("[WeaponTest] ยังไม่ได้ต่อ weaponButtonContainer/buttonPrefab — " +
                           "รัน Tools → Clone Swarm → Weapon Test → Setup Scene");
            return;
        }

        if (allWeapons == null || allWeapons.Length == 0)
        {
            Debug.LogError("[WeaponTest] allWeapons ว่าง — ไม่มีปุ่มให้สร้าง " +
                           "(ถ้าลิสต์ว่างบนจอแต่ log บอกว่าสร้างไปแล้ว แปลว่าเป็นปัญหาการมองเห็น ไม่ใช่ข้อมูล)");
            return;
        }

        int made = 0;
        foreach (var w in allWeapons)
        {
            if (w == null) continue;
            var btn = Instantiate(buttonPrefab, weaponButtonContainer);
            btn.gameObject.SetActive(true);

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            string shown = string.IsNullOrWhiteSpace(w.DisplayName) ? w.name : w.DisplayName;
            if (label != null) label.text = $"{w.tier} — {shown}";

            var captured = w;   // closure
            btn.onClick.AddListener(() => EquipWeapon(captured));
            made++;
        }

        Debug.Log($"[WeaponTest] สร้างปุ่มอาวุธ {made} ปุ่มใต้ '{weaponButtonContainer.name}'");
    }

    /// <summary>
    /// ล้างอาวุธที่ถืออยู่ทั้งหมด แล้วติดตั้งตัวที่เลือกและดันขึ้นเลเวลสูงสุด
    ///
    /// ต้องล้างก่อน ไม่งั้นพอกดครบ 6 ปุ่ม slot จะเต็มแล้ว AddWeapon คืน false เงียบๆ
    /// ปุ่มที่เหลือจะกดไม่ติดโดยไม่มีอะไรบอกว่าทำไม
    /// ดันเลเวลสูงสุดเพราะจุดประสงค์ของ harness คือดูพฤติกรรมเต็มรูปแบบของอาวุธ
    /// </summary>
    void EquipWeapon(WeaponData wd)
    {
        var pm = GetLocalPlayer()?.GetComponent<PlayerWeaponManager>();
        if (pm == null)
        {
            Debug.LogWarning("[WeaponTest] ยังไม่มี local player — กด Host ก่อน");
            return;
        }

        foreach (var old in pm.GetEquippedWeapons())
            pm.RemoveWeapon(old);

        if (!pm.AddWeapon(wd))
        {
            Debug.LogWarning($"[WeaponTest] ติดตั้ง {wd.weaponName} ไม่สำเร็จ");
            return;
        }

        // AddWeapon เริ่มที่ Lv1 เสมอ — ไต่ขึ้นจนสุดตามจำนวน level ที่ asset มีจริง
        // (Super/Fusion มี level เดียว วนแล้วจะคืน false ทันที ซึ่งถูกต้อง)
        for (int i = 1; i < wd.MaxLevel; i++)
            if (!pm.UpgradeWeapon(wd)) break;

        Debug.Log($"[WeaponTest] equipped {wd.weaponName} (Lv{wd.MaxLevel})");
        ResetDamageStats();
    }

    // ── Dummy respawn ─────────────────────────────────────────────────────
    public void RespawnDummy(Vector3 pos, Quaternion rot, float maxHp)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (dummyPrefab == null)
        {
            Debug.LogError("[WeaponTest] dummyPrefab ว่าง — dummy ที่ตายแล้วจะไม่กลับมา");
            return;
        }

        var go = Instantiate(dummyPrefab, pos, rot);
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null) enemy.maxHealth = maxHp;

        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn(true);
    }

    void RespawnAllDummies()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[WeaponTest] เติมเลือด dummy ได้เฉพาะฝั่ง server");
            return;
        }

        var dummies = FindObjectsByType<TargetDummy>(FindObjectsSortMode.None);
        foreach (var d in dummies)
        {
            var e = d.GetComponent<Enemy>();
            if (e != null) e.netHealth.Value = e.maxHealth;
        }
        ResetDamageStats();
    }

    // ── Stat sliders ──────────────────────────────────────────────────────
    /// <summary>
    /// ดันค่าสไลเดอร์เข้า PlayerStatManager จริง — เดิมเมธอดนี้อัปเดตแค่ข้อความบนป้าย
    /// สไลเดอร์ทั้งสามจึงไม่เคยมีผลกับเกมเลย
    ///
    /// Damage กับ Haste ใช้ช่อง temp ที่มีอยู่แล้ว (`tempDamageBonusMult` / `tempAbilityHaste`)
    /// ซึ่งเป็นค่า absolute เขียนทับได้ตรงๆ · Crit ไม่มีช่อง temp จึงบวกเฉพาะส่วนต่างเข้า total
    /// </summary>
    void PushStats()
    {
        var pm = GetLocalPlayer()?.GetComponent<PlayerWeaponManager>();
        var sm = pm != null ? pm.statManager : null;

        if (sm != null)
        {
            // GetPowerMultiplier() = 1 + total + tempDamageBonusMult → สไลเดอร์ 1× ต้องได้ bonus 0
            if (damageSlider) sm.tempDamageBonusMult = Mathf.Max(0f, damageSlider.value - 1f);
            if (hasteSlider)  sm.tempAbilityHaste    = hasteSlider.value;

            if (critSlider)
            {
                float target = Mathf.Clamp01(critSlider.value);
                sm.AddPermanentBonus(StatType.CriticalChance, target - _appliedCrit);
                _appliedCrit = target;
            }
        }

        if (statsLabel)
        {
            float d = damageSlider ? damageSlider.value : 1f;
            float h = hasteSlider  ? hasteSlider.value  : 0f;
            float c = critSlider   ? critSlider.value   : 0f;
            string live = sm != null ? "" : "  (ยังไม่มีผู้เล่น)";
            statsLabel.text = $"Dmg ×{d:F1} | Haste +{h:F0} | Crit {c:P0}{live}";
        }
    }

    void ResetStats()
    {
        if (damageSlider) damageSlider.value = 1f;
        if (hasteSlider)  hasteSlider.value  = 0f;
        if (critSlider)   critSlider.value   = 0f;
        PushStats();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return null;
        var po = NetworkManager.Singleton.LocalClient?.PlayerObject;
        return po != null ? po.transform : null;
    }
}
