using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hunter Q Ability — Homing Missiles
/// กด Q → ยิง Homing Missile ไปยัง enemy 5 ตัวที่ใกล้ที่สุด (unique)
/// แต่ละ missile ระเบิด AoE เมื่อถึงเป้าหมาย
///
/// AbilityData แนะนำ:
///   Lv1: damage=150, range=30, cooldown=12s
/// </summary>
public class HunterMissileAbility : AbilityBase, IHUDAbility
{

    [Header("Missile Config")]
    public int   missileCount      = 5;
    public float explosionRadius   = 3f;
    public float spawnSpread       = 0.6f;   // ระยะห่างระหว่างจุด spawn แต่ละลูก

    // ── IHUDAbility ───────────────────────────────────────────────────────
    public string HUDSlotKey      => "Q";   // Missile ของ Hunter อยู่ Q เสมอ
    public string HUDKeyLabel     => AbilityKeyLabel;   // อ่านจาก binding จริงใน AbilityInputActions
    public bool   IsActiveMode    => false;
    public float  ActiveRemaining => 0f;
    public float  ActiveMax       => 0f;

    // ── Cooldown ──────────────────────────────────────────────────────────
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<HunterMissileAbility, float> OnCooldownChanged;
    public static event System.Action<HunterMissileAbility>        OnFired;

    // ── Update ────────────────────────────────────────────────────────────
    void Update()
    {
        if (IsOnCooldown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            OnCooldownChanged?.Invoke(this, CooldownRemaining / CooldownMax);
            if (CooldownRemaining <= 0f) IsOnCooldown = false;
        }

        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (IsOnCooldown) return;

        if (AbilityPressedThisFrame)
            Fire();
    }

    // ── Fire ──────────────────────────────────────────────────────────────
    void Fire()
    {
        var ld = data.GetLevelData(currentLevel);

        float cd = ld.cooldown * (manager.statManager != null
            ? manager.statManager.GetCooldownMultiplier() : 1f);
        CooldownMax       = cd;
        CooldownRemaining = cd;
        IsOnCooldown      = true;
        OnCooldownChanged?.Invoke(this, 1f);
        OnFired?.Invoke(this);

        float searchRange = ld.range * (manager.statManager != null
            ? manager.statManager.GetAreaMultiplier() : 1f);
        float radius = explosionRadius * (manager.statManager != null
            ? manager.statManager.GetAreaMultiplier() : 1f);
        float dmg = RollDamage(ld.damage * (manager.statManager != null
            ? manager.statManager.GetPowerMultiplier() : 1f), out bool isCrit);

        // หา enemy — base missileCount + bonus จาก ProjectileCount stat
        int totalCount = missileCount + (manager.statManager != null
            ? manager.statManager.GetBonusProjectileCount() : 0);
        var targets = FindNearestUniqueEnemies(searchRange, totalCount);
        if (targets.Count == 0)
        {
            // คืน cooldown ถ้าไม่มี target
            IsOnCooldown      = false;
            CooldownRemaining = 0f;
            return;
        }

        var spawnPositions = new Vector3[targets.Count];
        var targetNetIds   = new ulong[targets.Count];
        Vector3 origin     = transform.position + Vector3.up * 0.8f;

        for (int i = 0; i < targets.Count; i++)
        {
            // Spread spawn position เล็กน้อยรอบๆ ผู้เล่น
            float angle   = i * (360f / targets.Count);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spawnSpread;
            spawnPositions[i] = origin + offset;

            var netObj = targets[i].GetComponent<Unity.Netcode.NetworkObject>();
            targetNetIds[i] = netObj != null ? netObj.NetworkObjectId : 0;
        }

        SpawnMissiles(spawnPositions, targetNetIds, dmg, radius, isCrit);
        Debug.Log($"[HunterMissile] 🚀 FIRED {targets.Count} missiles dmg={dmg:F0} radius={radius:F1}");
    }

    // ── Helper: หา N enemy ที่ใกล้ที่สุด (unique, ไม่ซ้ำ) ────────────────
    List<UnityEngine.GameObject> FindNearestUniqueEnemies(float range, int maxCount)
    {
        var cols   = PlayerWeaponManager.OverlapEnemy(transform.position, range);
        var sorted = new List<(float dist, UnityEngine.GameObject go)>();

        foreach (var c in cols)
        {
            if (c == null) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            sorted.Add((d, c.gameObject));
        }
        sorted.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<UnityEngine.GameObject>();
        var seen   = new HashSet<int>();
        foreach (var entry in sorted)
        {
            int id = entry.go.GetId();
            if (seen.Contains(id)) continue;
            seen.Add(id);
            result.Add(entry.go);
            if (result.Count >= maxCount) break;
        }
        return result;
    }
}
