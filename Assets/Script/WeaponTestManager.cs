using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Test manager สำหรับ WeaponTestScene — singleton, server-only logic
///
/// **Features:**
///   • Quick equip weapon/ability ผ่าน button list
///   • DPS counter (auto-reset ทุก 1s)
///   • Respawn dummies (single + bulk)
///   • Stat boost sliders → set ค่าใน PlayerStatManager
///
/// **Setup (Editor):**
///   1. วาง GameObject "WeaponTestManager" ใน WeaponTestScene
///   2. Assign:
///      - allWeapons[] — ลาก WeaponData ทุกตัวมาใส่
///      - allAbilities[] — ลาก AbilityData ทุกตัวมาใส่
///      - dummyPrefab — TargetDummy prefab
///      - UI refs (canvas elements)
///   3. Build button list dynamically จาก allWeapons (ใน Start)
/// </summary>
public class WeaponTestManager : MonoBehaviour
{
    public static WeaponTestManager Instance { get; private set; }

    [Header("Asset Refs (drag all)")]
    public WeaponData[] allWeapons;
    public AbilityData[] allAbilities;

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

    [Header("UI — Stat Sliders (optional)")]
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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildWeaponButtons();
        if (respawnAllButton) respawnAllButton.onClick.AddListener(RespawnAllDummies);
        if (resetStatsButton) resetStatsButton.onClick.AddListener(ResetStats);

        if (damageSlider) damageSlider.onValueChanged.AddListener(OnDamageSlider);
        if (hasteSlider)  hasteSlider.onValueChanged.AddListener(OnHasteSlider);
        if (critSlider)   critSlider.onValueChanged.AddListener(OnCritSlider);
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
        if (weaponButtonContainer == null || buttonPrefab == null) return;

        foreach (var w in allWeapons)
        {
            if (w == null) continue;
            var btn = Instantiate(buttonPrefab, weaponButtonContainer);
            btn.gameObject.SetActive(true);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = $"{w.tier} — {w.weaponName}";

            var captured = w;   // closure
            btn.onClick.AddListener(() => EquipWeapon(captured));
        }
    }

    void EquipWeapon(WeaponData wd)
    {
        var pm = GetLocalPlayer()?.GetComponent<PlayerWeaponManager>();
        if (pm == null)
        {
            Debug.LogWarning("[WeaponTestManager] ยังไม่มี local player");
            return;
        }

        // Clear existing weapons + equip new one (Lv5 for max-tier testing)
        // PlayerWeaponManager จะมี method AddWeapon — ถ้าไม่มี ให้ใช้ดักผ่าน ServerRpc
        // ตรงนี้ขึ้นกับ API ที่มีจริง — ดูใน PlayerWeaponManager.cs
        pm.AddWeapon(wd);
        Debug.Log($"[WeaponTestManager] equipped {wd.weaponName}");
        ResetDamageStats();
    }

    // ── Dummy respawn ─────────────────────────────────────────────────────
    public void RespawnDummy(Vector3 pos, Quaternion rot, float maxHp)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (dummyPrefab == null) return;

        var go = Instantiate(dummyPrefab, pos, rot);
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null) enemy.maxHealth = maxHp;

        var no = go.GetComponent<NetworkObject>();
        if (no != null) no.Spawn(true);
    }

    void RespawnAllDummies()
    {
        // หา TargetDummy ทั้งหมดใน scene (ที่ตายแล้ว — auto-respawn) — manual reset
        var dummies = FindObjectsByType<TargetDummy>(FindObjectsSortMode.None);
        foreach (var d in dummies)
        {
            var e = d.GetComponent<Enemy>();
            if (e != null && NetworkManager.Singleton.IsServer)
                e.netHealth.Value = e.maxHealth;
        }
        ResetDamageStats();
    }

    // ── Stat sliders ──────────────────────────────────────────────────────
    void OnDamageSlider(float v) => ApplyStat("damage", v);
    void OnHasteSlider(float v)  => ApplyStat("haste", v);
    void OnCritSlider(float v)   => ApplyStat("crit", v);

    void ApplyStat(string statName, float value)
    {
        // ปรับ PlayerStatManager — API ขึ้นกับ project
        // วิธีตรงไปตรงมาคือเรียก method ของ stat manager
        // ตัวอย่าง pseudo: pm.statManager.SetMultiplierOverride(...)
        // อันนี้ user implement เพิ่มถ้าต้องการ
        if (statsLabel)
            statsLabel.text = $"Dmg ×{damageSlider?.value:F1} | Haste +{hasteSlider?.value:F0} | Crit {critSlider?.value:P0}";
    }

    void ResetStats()
    {
        if (damageSlider) damageSlider.value = 1f;
        if (hasteSlider)  hasteSlider.value  = 0f;
        if (critSlider)   critSlider.value   = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    Transform GetLocalPlayer()
    {
        if (NetworkManager.Singleton == null) return null;
        var po = NetworkManager.Singleton.LocalClient?.PlayerObject;
        return po != null ? po.transform : null;
    }
}
