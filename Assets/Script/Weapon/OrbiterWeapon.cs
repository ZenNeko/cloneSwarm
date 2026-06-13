using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbiter â€” à¸§à¸™ Orb à¸£à¸­à¸šà¸•à¸±à¸§à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™
/// à¹ƒà¸Šà¹‰à¸ªà¸³à¸«à¸£à¸±à¸š Normal (Orbiter) à¹à¸¥à¸° Super (Star Ring)
///
/// Cycle:
///   1. à¸‹à¹ˆà¸­à¸™ orb â†’ à¸£à¸­ summonCooldown à¸§à¸´à¸™à¸²à¸—à¸µ
///   2. à¸£à¹ˆà¸²à¸¢ â†’ orb à¸›à¸£à¸²à¸à¸ activeDuration * DurationStat à¸§à¸´à¸™à¸²à¸—à¸µ
///      â†’ à¸•à¸£à¸§à¸ˆ collision à¸—à¸¸à¸ hitCheckInterval (continuous)
///      â†’ enemy à¹à¸•à¹ˆà¸¥à¸°à¸•à¸±à¸§à¸¡à¸µ hit cooldown à¸›à¹‰à¸­à¸‡à¸à¸±à¸™ spam
///   3. à¸‹à¹ˆà¸­à¸™à¸­à¸µà¸à¸„à¸£à¸±à¹‰à¸‡ â†’ à¸§à¸™à¸‹à¹‰à¸³
///
/// Orb à¹€à¸›à¹‡à¸™ Local GameObject (à¹„à¸¡à¹ˆà¹ƒà¸Šà¹ˆ NetworkObject) â€” visual à¸šà¸™ Owner
///   â†’ à¸›à¸¥à¸” parent à¸­à¸­à¸à¸ˆà¸²à¸ player â†’ orbit angle à¹„à¸¡à¹ˆà¸œà¸¹à¸à¸à¸±à¸š rotation à¸‚à¸­à¸‡à¸œà¸¹à¹‰à¹€à¸¥à¹ˆà¸™
/// Damage à¸ªà¹ˆà¸‡à¸œà¹ˆà¸²à¸™ FireMeleeServerRpc à¸—à¸µà¹ˆà¸•à¸³à¹à¸«à¸™à¹ˆà¸‡ orb à¹€à¸¡à¸·à¹ˆà¸­ overlap enemy à¸ˆà¸£à¸´à¸‡
///
/// StarRing à¸•à¹ˆà¸²à¸‡à¸à¸±à¸™: orbCount à¸¡à¸²à¸à¸‚à¸¶à¹‰à¸™, aoeOnHit = true
///
/// Level data à¹à¸™à¸°à¸™à¸³:
///   Lv1: dmg=18, cd=1.5s, count=2, range=3 (orbit radius)
///   Lv2: dmg=22, cd=1.4s, count=2, range=3
///   Lv3: dmg=28, cd=1.3s, count=3, range=3.5
///   Lv4: dmg=34, cd=1.2s, count=3, range=4
///   Lv5: dmg=42, cd=1.0s, count=4, range=4.5
/// </summary>
public class OrbiterWeapon : WeaponBase
{
    [Header("Orb Visual")]
    public GameObject orbPrefab;      // Simple sphere mesh (no NetworkObject)
    public float      orbSize  = 0.4f;
    public float      rotSpeed = 90f; // à¸­à¸‡à¸¨à¸²/à¸§à¸´à¸™à¸²à¸—à¸µ (à¸„à¸‡à¸—à¸µà¹ˆ â€” à¹„à¸¡à¹ˆà¸‚à¸¶à¹‰à¸™à¸à¸±à¸š player)

    [Header("Summon Cycle")]
    [Tooltip("à¸§à¸´à¸™à¸²à¸—à¸µà¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡à¸à¸²à¸£à¸£à¹ˆà¸²à¸¢ orb à¹à¸•à¹ˆà¸¥à¸°à¸£à¸­à¸š (à¸™à¸±à¸šà¸ˆà¸²à¸à¸—à¸µà¹ˆ orb à¸‹à¹ˆà¸­à¸™à¸ˆà¸™à¸–à¸¶à¸‡à¸£à¹ˆà¸²à¸¢à¸„à¸£à¸±à¹‰à¸‡à¸–à¸±à¸”à¹„à¸›)")]
    public float summonCooldown = 8f;
    [Tooltip("à¸§à¸´à¸™à¸²à¸—à¸µà¸—à¸µà¹ˆ orb à¹à¸ªà¸”à¸‡à¸œà¸¥à¸•à¹ˆà¸­à¸£à¸­à¸š (à¸ªà¹€à¸à¸¥à¸•à¸²à¸¡ Duration stat)")]
    public float activeDuration = 3f;

    [Header("Damage on Contact")]
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸•à¸£à¸§à¸ˆ enemy à¸£à¸­à¸š orb (à¸„à¸§à¸£à¹ƒà¸à¸¥à¹‰à¸à¸±à¸šà¸‚à¸™à¸²à¸” visual)")]
    public float orbHitRadius = 0.8f;
    [Tooltip("à¸•à¸£à¸§à¸ˆ collision à¸—à¸¸à¸à¸à¸µà¹ˆà¸§à¸´à¸™à¸²à¸—à¸µ (à¸„à¹ˆà¸²à¸™à¹‰à¸­à¸¢ = à¸•à¸­à¸šà¸ªà¸™à¸­à¸‡à¹„à¸§ à¹à¸•à¹ˆà¹ƒà¸Šà¹‰ CPU à¸¡à¸²à¸à¸‚à¸¶à¹‰à¸™)")]
    public float hitCheckInterval = 0.08f;
    [Tooltip("à¹€à¸§à¸¥à¸²à¸—à¸µà¹ˆ enemy à¹à¸•à¹ˆà¸¥à¸°à¸•à¸±à¸§à¸•à¹‰à¸­à¸‡à¸£à¸­à¸à¹ˆà¸­à¸™à¸–à¸¹à¸ orb à¹€à¸”à¸´à¸¡à¸•à¸µà¸­à¸µà¸ (à¸à¸±à¸™ multi-hit à¸•à¹ˆà¸­à¸£à¸­à¸š)")]
    public float perEnemyHitCooldown = 0.5f;

    [Header("Star Ring Super")]
    public bool  aoeOnHit  = false;
    public float aoeRadius = 1.5f;   // radius à¸‚à¸­à¸‡ AoE à¹€à¸¡à¸·à¹ˆà¸­ orb à¸Šà¸™à¸¨à¸±à¸•à¸£à¸¹

    // â”€â”€ State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    protected override bool UsesCooldownTimer => false;   // à¹€à¸£à¸²à¸ˆà¸±à¸”à¸à¸²à¸£ timer à¹€à¸­à¸‡

    private List<Transform>          orbs     = new();
    private float                    orbitAngle;
    private bool                     isActive;
    private float                    summonTimer;
    private float                    activeTimer;
    private float                    hitCheckTimer;
    private float                    orbSyncTimer;
    private const float              OrbSyncInterval = 0.05f;

    // à¸›à¹‰à¸­à¸‡à¸à¸±à¸™ multi-hit: enemyInstanceID â†’ unscaledTime à¸—à¸µà¹ˆà¸ˆà¸° hit à¹„à¸”à¹‰à¸­à¸µà¸
    private readonly Dictionary<int, float> _hitCooldowns = new();

    // â”€â”€ Init â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    protected override void OnInit()
    {
        SpawnOrbs(data.GetLevelData(currentLevel).projectileCount);
        SetOrbsVisible(false);
        Summon(); // à¹à¸ªà¸”à¸‡ orb à¸—à¸±à¸™à¸—à¸µà¸—à¸µà¹ˆà¹„à¸”à¹‰ weapon
    }

    protected override void OnLevelUp()
    {
        int needed = data.GetLevelData(currentLevel).projectileCount;
        if (orbs.Count < needed) SpawnOrbs(needed - orbs.Count);
    }

    // OnFire à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ â€” à¹€à¸£à¸²à¸ˆà¸±à¸”à¸à¸²à¸£ damage à¸œà¹ˆà¸²à¸™ CheckHits() à¸—à¸¸à¸ frame (continuous)
    protected override void OnFire(WeaponLevelData ld) { }

    // â”€â”€ Update â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    protected override void Update()
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        // à¸«à¸¡à¸¸à¸™ orb à¸”à¹‰à¸§à¸¢à¸­à¸±à¸•à¸£à¸²à¸„à¸‡à¸—à¸µà¹ˆ â€” à¹„à¸¡à¹ˆà¸‚à¸¶à¹‰à¸™à¸à¸±à¸šà¸à¸²à¸£à¸«à¸¡à¸¸à¸™ player
        orbitAngle += rotSpeed * Time.deltaTime;
        UpdateOrbPositions();

        if (isActive) TickActive();
        else          TickInactive();
    }

    // â”€â”€ Active Phase â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void TickActive()
    {
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f) { Deactivate(); return; }

        // à¸•à¸£à¸§à¸ˆ collision à¸­à¸¢à¹ˆà¸²à¸‡à¸•à¹ˆà¸­à¹€à¸™à¸·à¹ˆà¸­à¸‡
        hitCheckTimer += Time.deltaTime;
        if (hitCheckTimer < hitCheckInterval) return;
        hitCheckTimer = 0f;

        CheckHits();
    }

    // â”€â”€ Inactive Phase â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void TickInactive()
    {
        summonTimer += Time.deltaTime;
        if (summonTimer >= summonCooldown)
        {
            summonTimer = 0f;
            Summon();
        }
    }

    void Summon()
    {
        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        activeTimer  = activeDuration * durationMult;
        hitCheckTimer = 0f;
        isActive     = true;
        _hitCooldowns.Clear();
        SetOrbsVisible(true);
        Debug.Log($"[Orbiter] âœ¨ Summoned â€” {orbs.Count} orbs, active {activeTimer:F1}s, hitRadius={orbHitRadius}");
    }

    void Deactivate()
    {
        isActive = false;
        SetOrbsVisible(false);
        summonTimer = 0f;
        manager.HideRemoteOrbsServerRpc();
        Debug.Log($"[Orbiter] ðŸ’¤ Deactivated â€” next summon in {summonCooldown:F0}s");
    }

    // â”€â”€ Damage on Contact â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void CheckHits()
    {
        var ld         = data.GetLevelData(currentLevel);
        var sm         = manager.statManager;
        float dmgMult  = sm != null ? sm.GetPowerMultiplier() : 1f;
        float baseDmg  = ld.damage * dmgMult;
        float now      = Time.time;
        float radius   = orbHitRadius;

        // à¸¥à¸š entry à¸—à¸µà¹ˆà¸«à¸¡à¸”à¸­à¸²à¸¢à¸¸ (à¸à¸±à¸™ dictionary à¹‚à¸•)
        if (_hitCooldowns.Count > 32)
            CleanupCooldowns(now);

        foreach (var orb in orbs)
        {
            if (orb == null || !orb.gameObject.activeSelf) continue;

            var hits = Physics.OverlapSphere(orb.position, radius);
            if (hits.Length == 0) continue;

            foreach (var c in hits)
            {
                if (!c.CompareTag("Enemy")) continue;
                var e = c.GetComponent<Enemy>();
                if (e == null) continue;

                int id = e.GetInstanceID();
                if (_hitCooldowns.TryGetValue(id, out float nextHitAt) && now < nextHitAt) continue;
                _hitCooldowns[id] = now + perEnemyHitCooldown;

                float dmg = RollDamage(baseDmg, out bool isCrit);

                if (aoeOnHit)
                {
                    // StarRing: à¸—à¸³ AoE à¸£à¸­à¸š orb â€” Enemy.cs spawn HitEffect à¹€à¸­à¸‡à¸•à¸­à¸™ TakeDamage
                    FireMelee(orb.position, aoeRadius, dmg, isCrit);
                    Debug.Log($"[Orbiter] ðŸ’¥ AoE hit at orb pos â€” dmg {dmg:F0}");
                    break; // 1 hit à¸•à¹ˆà¸­ orb à¸•à¹ˆà¸­ tick (AoE à¸„à¸£à¸­à¸šà¹„à¸›à¸—à¸±à¹‰à¸‡à¸à¸¥à¸¸à¹ˆà¸¡à¹à¸¥à¹‰à¸§)
                }
                else
                {
                    // à¸•à¸µà¹€à¸‰à¸žà¸²à¸°à¸•à¸±à¸§à¸—à¸µà¹ˆà¸Šà¸™ â€” Enemy.cs spawn HitEffect à¹€à¸­à¸‡à¸•à¸­à¸™ TakeDamage
                    FireMelee(e.transform.position, 0.5f, dmg, isCrit);
                    Debug.Log($"[Orbiter] ðŸ’¥ Hit enemy {e.name} â€” dmg {dmg:F0}");
                }
            }
        }
    }

    void CleanupCooldowns(float now)
    {
        var stale = new List<int>();
        foreach (var kv in _hitCooldowns)
            if (kv.Value < now) stale.Add(kv.Key);
        foreach (var id in stale) _hitCooldowns.Remove(id);
    }

    // â”€â”€ Orb Position (world-space, à¹„à¸¡à¹ˆà¸œà¸¹à¸ parent rotation) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void UpdateOrbPositions()
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float r = data.GetLevelData(currentLevel).range;

        for (int i = 0; i < orbs.Count; i++)
        {
            if (orbs[i] == null) continue;
            float a   = orbitAngle + (360f / orbs.Count) * i;
            float rad = Mathf.Deg2Rad * a;
            // world-space position â€” à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ localPosition à¹€à¸žà¸·à¹ˆà¸­à¸à¸±à¸™à¸à¸²à¸£à¸«à¸¡à¸¸à¸™à¸•à¸²à¸¡ player
            orbs[i].position = center + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
        }

        // Sync à¸•à¸³à¹à¸«à¸™à¹ˆà¸‡ world-space à¹„à¸›à¸¢à¸±à¸‡ client à¸­à¸·à¹ˆà¸™ (à¹€à¸‰à¸žà¸²à¸°à¸•à¸­à¸™ active)
        if (!isActive) return;
        orbSyncTimer += Time.deltaTime;
        if (orbSyncTimer < OrbSyncInterval) return;
        orbSyncTimer = 0f;

        var positions = new Vector3[orbs.Count];
        for (int i = 0; i < orbs.Count; i++)
            positions[i] = orbs[i] != null ? orbs[i].position : Vector3.zero;
        manager.SyncOrbPositionsServerRpc(positions);
    }

    void SpawnOrbs(int count)
    {
        if (orbPrefab == null)
        {
            Debug.LogError($"[OrbiterWeapon] orbPrefab à¹„à¸¡à¹ˆà¸–à¸¹à¸ assign à¸šà¸™ weapon prefab â€” orb à¸ˆà¸°à¹„à¸¡à¹ˆà¸›à¸£à¸²à¸à¸");
            return;
        }
        for (int i = 0; i < count; i++)
        {
            // parent = null â†’ à¹„à¸¡à¹ˆà¸£à¸±à¸šà¸à¸²à¸£à¸«à¸¡à¸¸à¸™à¸‚à¸­à¸‡ player
            var go = Instantiate(orbPrefab);
            go.transform.localScale = Vector3.one * orbSize;
            go.SetActive(false);

            // â”€â”€ Strip components à¸—à¸µà¹ˆà¸­à¸²à¸ˆ interfere â”€â”€
            // OrbiterWeapon à¸ˆà¸±à¸”à¸à¸²à¸£ damage + position à¹€à¸­à¸‡ à¹„à¸¡à¹ˆà¸•à¹‰à¸­à¸‡à¸à¸²à¸£ network/physics behavior
            var no = go.GetComponent<Unity.Netcode.NetworkObject>();
            if (no != null) Destroy(no);
            var proj = go.GetComponent<Projectile>();
            if (proj != null) Destroy(proj);
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);
            // à¹€à¸à¹‡à¸š Collider isTrigger à¹„à¸§à¹‰à¸à¹‡à¹„à¸”à¹‰ â€” à¹„à¸¡à¹ˆà¸à¸£à¸°à¸—à¸š Physics.OverlapSphere

            orbs.Add(go.transform);
        }
    }

    void SetOrbsVisible(bool visible)
    {
        foreach (var orb in orbs)
            if (orb != null) orb.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        foreach (var o in orbs)
            if (o != null) Destroy(o.gameObject);
        manager?.HideRemoteOrbsServerRpc();
    }
}
