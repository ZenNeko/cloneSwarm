using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hunter Q Ability â€” Homing Missiles
/// à¸à¸” Q â†’ à¸¢à¸´à¸‡ Homing Missile à¹„à¸›à¸¢à¸±à¸‡ enemy 5 à¸•à¸±à¸§à¸—à¸µà¹ˆà¹ƒà¸à¸¥à¹‰à¸—à¸µà¹ˆà¸ªà¸¸à¸” (unique)
/// à¹à¸•à¹ˆà¸¥à¸° missile à¸£à¸°à¹€à¸šà¸´à¸” AoE à¹€à¸¡à¸·à¹ˆà¸­à¸–à¸¶à¸‡à¹€à¸›à¹‰à¸²à¸«à¸¡à¸²à¸¢
///
/// AbilityData à¹à¸™à¸°à¸™à¸³:
///   Lv1: damage=150, range=30, cooldown=12s
/// </summary>
public class HunterMissileAbility : AbilityBase, IHUDAbility
{
    [Header("Input Key")]
    public Key activateKey = Key.Q;

    [Header("Missile Config")]
    public int   missileCount      = 5;
    public float explosionRadius   = 3f;
    public float spawnSpread       = 0.6f;   // à¸£à¸°à¸¢à¸°à¸«à¹ˆà¸²à¸‡à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡à¸ˆà¸¸à¸” spawn à¹à¸•à¹ˆà¸¥à¸°à¸¥à¸¹à¸

    // â”€â”€ IHUDAbility â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public string HUDSlotKey      => "Q";   // Missile à¸‚à¸­à¸‡ Hunter à¸­à¸¢à¸¹à¹ˆ Q à¹€à¸ªà¸¡à¸­
    public string HUDKeyLabel     => activateKey.ToString();
    public bool   IsActiveMode    => false;
    public float  ActiveRemaining => 0f;
    public float  ActiveMax       => 0f;

    // â”€â”€ Cooldown â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool  IsOnCooldown      { get; private set; }
    public float CooldownRemaining { get; private set; }
    public float CooldownMax       { get; private set; }

    // â”€â”€ Events (UI à¸Ÿà¸±à¸‡) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static event System.Action<HunterMissileAbility, float> OnCooldownChanged;
    public static event System.Action<HunterMissileAbility>        OnFired;

    // â”€â”€ Update â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        if (Keyboard.current != null && Keyboard.current[activateKey].wasPressedThisFrame)
            Fire();
    }

    // â”€â”€ Fire â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            ? manager.statManager.GetPowerMultiplier() : 1f), out bool _);

        // à¸«à¸² enemy â€” base missileCount + bonus à¸ˆà¸²à¸ ProjectileCount stat
        int totalCount = missileCount + (manager.statManager != null
            ? manager.statManager.GetBonusProjectileCount() : 0);
        var targets = FindNearestUniqueEnemies(searchRange, totalCount);
        if (targets.Count == 0)
        {
            // à¸„à¸·à¸™ cooldown à¸–à¹‰à¸²à¹„à¸¡à¹ˆà¸¡à¸µ target
            IsOnCooldown      = false;
            CooldownRemaining = 0f;
            return;
        }

        var spawnPositions = new Vector3[targets.Count];
        var targetNetIds   = new ulong[targets.Count];
        Vector3 origin     = transform.position + Vector3.up * 0.8f;

        for (int i = 0; i < targets.Count; i++)
        {
            // Spread spawn position à¹€à¸¥à¹‡à¸à¸™à¹‰à¸­à¸¢à¸£à¸­à¸šà¹† à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™
            float angle   = i * (360f / targets.Count);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spawnSpread;
            spawnPositions[i] = origin + offset;

            var netObj = targets[i].GetComponent<Unity.Netcode.NetworkObject>();
            targetNetIds[i] = netObj != null ? netObj.NetworkObjectId : 0;
        }

        SpawnMissiles(spawnPositions, targetNetIds, dmg, radius);
        Debug.Log($"[HunterMissile] ðŸš€ FIRED {targets.Count} missiles dmg={dmg:F0} radius={radius:F1}");
    }

    // â”€â”€ Helper: à¸«à¸² N enemy à¸—à¸µà¹ˆà¹ƒà¸à¸¥à¹‰à¸—à¸µà¹ˆà¸ªà¸¸à¸” (unique, à¹„à¸¡à¹ˆà¸‹à¹‰à¸³) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            int id = entry.go.GetInstanceID();
            if (seen.Contains(id)) continue;
            seen.Add(id);
            result.Add(entry.go);
            if (result.Count >= maxCount) break;
        }
        return result;
    }
}
