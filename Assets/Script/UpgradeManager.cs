using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-player upgrade manager — อยู่บน Player Prefab
/// ทำงานเฉพาะ Owner เท่านั้น (non-owner จะ disable)
/// </summary>
public class UpgradeManager : NetworkBehaviour
{
    [Header("Upgrade Pool")]
    [Tooltip("ลาก WeaponUpgradeData ScriptableObjects ทั้งหมดมาใส่ที่นี่")]
    public List<WeaponUpgradeData> allUpgrades = new();
    public int cardsPerLevel = 3;

    // ── References (เอาจาก Player prefab ตัวเดียวกัน) ─────────────────────
    private PlayerWeapon     playerWeapon;
    private playermove       playerMove;
    private ExperienceManager expManager;

    // ── State ─────────────────────────────────────────────────────────────
    private Dictionary<WeaponUpgradeData, int> appliedStacks = new();
    private Queue<int>                          pendingLevelUps = new();
    private bool                                isShowingUI;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        playerWeapon = GetComponent<PlayerWeapon>();
        playerMove   = GetComponent<playermove>();
        expManager   = GetComponent<ExperienceManager>();

        ExperienceManager.OnLocalLevelUp += OnLevelUp;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            ExperienceManager.OnLocalLevelUp -= OnLevelUp;
    }

    // ── Level-Up Queue ────────────────────────────────────────────────────
    void OnLevelUp(int newLevel)
    {
        pendingLevelUps.Enqueue(newLevel);
        if (!isShowingUI) ShowNextUpgrade();
    }

    void ShowNextUpgrade()
    {
        if (pendingLevelUps.Count == 0) return;

        pendingLevelUps.Dequeue();
        List<WeaponUpgradeData> options = PickRandomUpgrades(cardsPerLevel);

        if (options.Count == 0) { ShowNextUpgrade(); return; }

        int currentLevel = GetComponent<ExperienceManager>()?.GetCurrentLevel() ?? 0;

        isShowingUI    = true;
        Time.timeScale = 0f;
        LevelUpUI.Instance.Show(options, GetStacks, ApplyUpgrade, currentLevel);
    }

    // ── Apply Upgrade ─────────────────────────────────────────────────────
    public void ApplyUpgrade(WeaponUpgradeData data)
    {
        if (data == null) return;

        appliedStacks.TryGetValue(data, out int stacks);
        appliedStacks[data] = stacks + 1;

        switch (data.upgradeType)
        {
            // ── stats ที่ Owner ถือ (ส่งไปใน ShootServerRpc ทุกครั้ง) ──
            case UpgradeType.Damage:
                if (playerWeapon) playerWeapon.damage = ApplyValue(playerWeapon.damage, data);
                break;
            case UpgradeType.AttackSpeed:
                if (playerWeapon) playerWeapon.attackSpeed = ApplyValue(playerWeapon.attackSpeed, data);
                break;
            case UpgradeType.AttackRange:
                if (playerWeapon) playerWeapon.attackRange = ApplyValue(playerWeapon.attackRange, data);
                break;
            case UpgradeType.ProjectileSpeed:
                if (playerWeapon) playerWeapon.projectileSpeed = ApplyValue(playerWeapon.projectileSpeed, data);
                break;
            case UpgradeType.MultiProjectile:
                if (playerWeapon) playerWeapon.multiProjectileCount += (int)data.value;
                break;
            case UpgradeType.MoveSpeed:
                if (playerMove) playerMove.moveSpeed = ApplyValue(playerMove.moveSpeed, data);
                break;

            // ── stats ที่ Server ถือ → ต้องใช้ ServerRpc ──
            case UpgradeType.MaxHealth:
                float healthAmount = data.mode == UpgradeApplicationMode.Additive
                    ? data.value
                    : playerMove.maxHealth * data.value;
                ApplyMaxHealthServerRpc(healthAmount);
                break;
            case UpgradeType.HealthRegen:
                float newRegen = ApplyValue(playerMove.healthRegenPerSecond, data);
                ApplyHealthRegenServerRpc(newRegen);
                break;

            // ── EXP multiplier (local per-player) ──
            case UpgradeType.ExpBonus:
                if (expManager) expManager.expMultiplier = ApplyValue(expManager.expMultiplier, data);
                break;
        }

        Debug.Log($"[Upgrade] {data.upgradeName} applied (stack {appliedStacks[data]}/{data.maxStacks})");

        isShowingUI = false;
        LevelUpUI.Instance.Hide();

        if (pendingLevelUps.Count > 0) ShowNextUpgrade();
        else Time.timeScale = 1f;
    }

    // ── ServerRpc สำหรับ health stats ────────────────────────────────────
    [ServerRpc]
    void ApplyMaxHealthServerRpc(float amount) => playerMove?.GainMaxHealth(amount);

    [ServerRpc]
    void ApplyHealthRegenServerRpc(float newValue)
    {
        if (playerMove) playerMove.healthRegenPerSecond = newValue;
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    public int GetStacks(WeaponUpgradeData data) =>
        appliedStacks.TryGetValue(data, out int s) ? s : 0;

    List<WeaponUpgradeData> PickRandomUpgrades(int count)
    {
        var pool = new List<WeaponUpgradeData>();
        foreach (var u in allUpgrades)
        {
            int s = GetStacks(u);
            if (u.maxStacks <= 0 || s < u.maxStacks) pool.Add(u);
        }

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        return pool.GetRange(0, Mathf.Min(count, pool.Count));
    }

    static float ApplyValue(float current, WeaponUpgradeData data) =>
        data.mode == UpgradeApplicationMode.Additive
            ? current + data.value
            : current * (1f + data.value);
}
