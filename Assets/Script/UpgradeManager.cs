using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Per-player upgrade manager — อยู่บน Player Prefab
/// ทำงานเฉพาะ Owner เท่านั้น (non-owner จะ disable)
///
/// Flow:
///   SharedExperienceManager.OnUpgradePhaseStart → ShowUpgradeUI()
///   Player เลือก card → ApplyUpgrade() → PlayerUpgradePickedServerRpc()
///   SharedExperienceManager.OnForceAutoPick → AutoPick() ถ้ายังไม่ได้เลือก
///   SharedExperienceManager.OnUpgradePhaseEnd → HideUI, reset state
/// </summary>
public class UpgradeManager : NetworkBehaviour
{
    [Header("Upgrade Pool")]
    [Tooltip("ลาก WeaponUpgradeData ScriptableObjects ทั้งหมดมาใส่ที่นี่")]
    public List<WeaponUpgradeData> allUpgrades = new();
    public int cardsPerLevel = 3;

    // ── References ────────────────────────────────────────────────────────
    private PlayerWeapon playerWeapon;
    private playermove   playerMove;

    // ── State ─────────────────────────────────────────────────────────────
    private Dictionary<WeaponUpgradeData, int> appliedStacks  = new();
    private List<WeaponUpgradeData>             currentOptions = new();   // options ที่แสดงอยู่ตอนนี้
    private bool                                hasPicked;                // เลือกไปแล้วในรอบนี้

    // ── Lifecycle ─────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner) { enabled = false; return; }

        playerWeapon = GetComponent<PlayerWeapon>() ?? GetComponentInChildren<PlayerWeapon>();
        playerMove   = GetComponent<playermove>()   ?? GetComponentInChildren<playermove>();

        if (!playerWeapon) Debug.LogError("[UpgradeManager] ❌ PlayerWeapon not found!");
        if (!playerMove)   Debug.LogError("[UpgradeManager] ❌ playermove not found!");

        Debug.Log($"[UpgradeManager] ✅ Init — weapon:{playerWeapon != null} move:{playerMove != null}");

        SharedExperienceManager.OnUpgradePhaseStart += OnUpgradePhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   += OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     += OnForceAutoPick;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        SharedExperienceManager.OnUpgradePhaseStart -= OnUpgradePhaseStart;
        SharedExperienceManager.OnUpgradePhaseEnd   -= OnUpgradePhaseEnd;
        SharedExperienceManager.OnForceAutoPick     -= OnForceAutoPick;
    }

    // ── Phase Events ──────────────────────────────────────────────────────

    void OnUpgradePhaseStart(int newLevel)
    {
        hasPicked      = false;
        currentOptions = PickRandomUpgrades(cardsPerLevel);

        if (currentOptions.Count == 0)
        {
            // ไม่มี upgrade ให้เลือก → แจ้ง server ทันที
            Debug.Log("[UpgradeManager] ไม่มี upgrade ให้เลือก — skip");
            NotifyPicked();
            return;
        }

        LevelUpUI.Instance.Show(currentOptions, GetStacks, ApplyUpgrade, newLevel);
    }

    void OnUpgradePhaseEnd()
    {
        // ทุกคนเลือกเสร็จแล้ว — ปิด panel ทั้งหมด
        LevelUpUI.Instance?.Hide();
        hasPicked = false;
    }

    void OnForceAutoPick()
    {
        if (hasPicked) return;   // เลือกไปแล้ว ไม่ต้อง auto

        if (currentOptions.Count > 0)
        {
            Debug.Log($"[UpgradeManager] Auto-pick: {currentOptions[0].upgradeName}");
            ApplyUpgrade(currentOptions[0]);
        }
        else
        {
            // ไม่มีตัวเลือก — notify เฉยๆ
            NotifyPicked();
        }
    }

    // ── Apply Upgrade ─────────────────────────────────────────────────────
    public void ApplyUpgrade(WeaponUpgradeData data)
    {
        if (data == null || hasPicked) return;

        hasPicked = true;                          // กัน double-call
        LevelUpUI.Instance?.HideCards();          // ซ่อนการ์ด แต่คง panel ไว้รอคนอื่น

        appliedStacks.TryGetValue(data, out int stacks);
        appliedStacks[data] = stacks + 1;

        switch (data.upgradeType)
        {
            case UpgradeType.Damage:
                if (playerWeapon) { float b = playerWeapon.damage; playerWeapon.damage = ApplyValue(b, data); Debug.Log($"[Upgrade] Damage {b:F1} → {playerWeapon.damage:F1}"); }
                else Debug.LogError("[Upgrade] ❌ playerWeapon null");
                break;

            case UpgradeType.AttackSpeed:
                if (playerWeapon) { float b = playerWeapon.attackSpeed; playerWeapon.attackSpeed = ApplyValue(b, data); Debug.Log($"[Upgrade] AttackSpeed {b:F2} → {playerWeapon.attackSpeed:F2}"); }
                else Debug.LogError("[Upgrade] ❌ playerWeapon null");
                break;

            case UpgradeType.AttackRange:
                if (playerWeapon) { float b = playerWeapon.attackRange; playerWeapon.attackRange = ApplyValue(b, data); Debug.Log($"[Upgrade] AttackRange {b:F1} → {playerWeapon.attackRange:F1}"); }
                else Debug.LogError("[Upgrade] ❌ playerWeapon null");
                break;

            case UpgradeType.ProjectileSpeed:
                if (playerWeapon) { float b = playerWeapon.projectileSpeed; playerWeapon.projectileSpeed = ApplyValue(b, data); Debug.Log($"[Upgrade] ProjSpeed {b:F1} → {playerWeapon.projectileSpeed:F1}"); }
                else Debug.LogError("[Upgrade] ❌ playerWeapon null");
                break;

            case UpgradeType.MultiProjectile:
                if (playerWeapon) { playerWeapon.multiProjectileCount += (int)data.value; Debug.Log($"[Upgrade] MultiProj → {playerWeapon.multiProjectileCount}"); }
                else Debug.LogError("[Upgrade] ❌ playerWeapon null");
                break;

            case UpgradeType.MoveSpeed:
                if (playerMove) { float b = playerMove.moveSpeed; playerMove.moveSpeed = ApplyValue(b, data); Debug.Log($"[Upgrade] MoveSpeed {b:F2} → {playerMove.moveSpeed:F2}"); }
                else Debug.LogError("[Upgrade] ❌ playerMove null");
                break;

            case UpgradeType.MaxHealth:
                if (playerMove) { float amt = data.mode == UpgradeApplicationMode.Additive ? data.value : playerMove.maxHealth * data.value; ApplyMaxHealthServerRpc(amt); Debug.Log($"[Upgrade] MaxHealth +{amt}"); }
                else Debug.LogError("[Upgrade] ❌ playerMove null");
                break;

            case UpgradeType.HealthRegen:
                if (playerMove) { float r = ApplyValue(playerMove.healthRegenPerSecond, data); ApplyHealthRegenServerRpc(r); Debug.Log($"[Upgrade] HealthRegen → {r:F2}/s"); }
                else Debug.LogError("[Upgrade] ❌ playerMove null");
                break;

            case UpgradeType.ExpBonus:
            {
                var sem = SharedExperienceManager.Instance;
                if (sem != null)
                {
                    float b       = sem.expMultiplier.Value;
                    float newMult = ApplyValue(b, data);
                    sem.ApplyExpMultiplierServerRpc(newMult);
                    Debug.Log($"[Upgrade] ExpBonus (shared) {b:F2}x → {newMult:F2}x");
                }
                else Debug.LogError("[Upgrade] ❌ SharedExperienceManager null");
                break;
            }
        }

        Debug.Log($"[Upgrade] ✅ {data.upgradeName} — stack {appliedStacks[data]}/{data.maxStacks}");

        // แจ้ง Server ว่า player นี้เลือกเสร็จแล้ว
        NotifyPicked();
    }

    void NotifyPicked()
    {
        SharedExperienceManager.Instance?.PlayerUpgradePickedServerRpc();
    }

    // ── ServerRpc สำหรับ stats ที่ต้องเปลี่ยนบน Server ─────────────────
    [ServerRpc]
    void ApplyMaxHealthServerRpc(float amount)  => playerMove?.GainMaxHealth(amount);

    [ServerRpc]
    void ApplyHealthRegenServerRpc(float value)
    {
        if (playerMove) playerMove.healthRegenPerSecond = value;
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
