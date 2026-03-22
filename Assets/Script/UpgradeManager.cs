using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// จัดการ pool ของ upgrades, สุ่มเลือก, และ apply ค่าให้ player components
/// เป็นเจ้าของ Time.timeScale — ไม่มี class อื่น touch timeScale
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Upgrade Pool")]
    [Tooltip("ลาก WeaponUpgradeData ScriptableObjects ทั้งหมดมาใส่ที่นี่")]
    public List<WeaponUpgradeData> allUpgrades = new();

    [Tooltip("จำนวน card ที่แสดงต่อครั้ง")]
    public int cardsPerLevel = 3;

    // ── References ──────────────────────────────────────────────────────
    private PlayerWeapon playerWeapon;
    private playermove   playerMove;

    // ── Runtime state ───────────────────────────────────────────────────
    private Dictionary<WeaponUpgradeData, int> appliedStacks = new();

    // Queue รองรับ level-up หลายครั้งพร้อมกัน
    private Queue<int> pendingLevelUps = new();
    private bool isShowingUI = false;

    // ── Lifecycle ────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        playerWeapon = FindObjectOfType<PlayerWeapon>();
        playerMove   = FindObjectOfType<playermove>();

        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.onLevelUp.AddListener(OnLevelUp);
    }

    void OnDestroy()
    {
        if (ExperienceManager.Instance != null)
            ExperienceManager.Instance.onLevelUp.RemoveListener(OnLevelUp);
    }

    // ── Level-Up trigger ─────────────────────────────────────────────────
    void OnLevelUp(int newLevel)
    {
        pendingLevelUps.Enqueue(newLevel);
        if (!isShowingUI)
            ShowNextUpgrade();
    }

    void ShowNextUpgrade()
    {
        if (pendingLevelUps.Count == 0) return;

        pendingLevelUps.Dequeue();
        List<WeaponUpgradeData> options = PickRandomUpgrades(cardsPerLevel);

        if (options.Count == 0)
        {
            // ไม่มี upgrade เหลือ ข้ามได้เลย
            ShowNextUpgrade();
            return;
        }

        isShowingUI = true;
        Time.timeScale = 0f;
        LevelUpUI.Instance.Show(options);
    }

    // ── Apply upgrade (เรียกจาก LevelUpUI หลัง player เลือก card) ────────
    public void ApplyUpgrade(WeaponUpgradeData data)
    {
        if (data == null) return;

        // เพิ่ม stack
        appliedStacks.TryGetValue(data, out int stacks);
        appliedStacks[data] = stacks + 1;

        // Apply ค่า
        switch (data.upgradeType)
        {
            case UpgradeType.Damage:
                if (playerWeapon)
                    playerWeapon.damage = ApplyValue(playerWeapon.damage, data);
                break;

            case UpgradeType.AttackSpeed:
                if (playerWeapon)
                    playerWeapon.attackSpeed = ApplyValue(playerWeapon.attackSpeed, data);
                break;

            case UpgradeType.AttackRange:
                if (playerWeapon)
                    playerWeapon.attackRange = ApplyValue(playerWeapon.attackRange, data);
                break;

            case UpgradeType.MoveSpeed:
                if (playerMove)
                    playerMove.moveSpeed = ApplyValue(playerMove.moveSpeed, data);
                break;

            case UpgradeType.MaxHealth:
                if (playerMove)
                    playerMove.GainMaxHealth(data.mode == UpgradeApplicationMode.Additive
                        ? data.value
                        : playerMove.maxHealth * data.value);
                break;

            case UpgradeType.ProjectileSpeed:
                if (playerWeapon)
                    playerWeapon.projectileSpeed = ApplyValue(playerWeapon.projectileSpeed, data);
                break;

            case UpgradeType.ExpBonus:
                if (ExperienceManager.Instance)
                    ExperienceManager.Instance.expMultiplier =
                        ApplyValue(ExperienceManager.Instance.expMultiplier, data);
                break;

            case UpgradeType.MultiProjectile:
                if (playerWeapon)
                    playerWeapon.multiProjectileCount += (int)data.value;
                break;

            case UpgradeType.HealthRegen:
                if (playerMove)
                    playerMove.healthRegenPerSecond = ApplyValue(playerMove.healthRegenPerSecond, data);
                break;
        }

        Debug.Log($"[Upgrade] {data.upgradeName} applied (stack {appliedStacks[data]}/{data.maxStacks})");

        // ปิด UI และดู queue
        isShowingUI = false;
        LevelUpUI.Instance.Hide();

        if (pendingLevelUps.Count > 0)
            ShowNextUpgrade();
        else
            Time.timeScale = 1f;
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    public int GetStacks(WeaponUpgradeData data) =>
        appliedStacks.TryGetValue(data, out int s) ? s : 0;

    List<WeaponUpgradeData> PickRandomUpgrades(int count)
    {
        // กรองที่ maxed out
        var pool = new List<WeaponUpgradeData>(allUpgrades.Count);
        foreach (var u in allUpgrades)
        {
            int stacks = GetStacks(u);
            if (u.maxStacks <= 0 || stacks < u.maxStacks)
                pool.Add(u);
        }

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int take = Mathf.Min(count, pool.Count);
        return pool.GetRange(0, take);
    }

    static float ApplyValue(float current, WeaponUpgradeData data) =>
        data.mode == UpgradeApplicationMode.Additive
            ? current + data.value
            : current * (1f + data.value);
}
