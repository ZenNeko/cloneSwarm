using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Death Field â€” Super version à¸‚à¸­à¸‡ Radiant Aura
/// field à¹ƒà¸«à¸à¹ˆà¸‚à¸¶à¹‰à¸™ + enemy à¸—à¸µà¹ˆà¸•à¸²à¸¢à¹ƒà¸™ field à¸£à¸°à¹€à¸šà¸´à¸” mini AoE à¸£à¸­à¸šà¸•à¸±à¸§
///
/// Super tier â€” 1 level à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™
///   dmg=30/tick, cd=0.5s, range=6.0
///
/// Fusion: Death Field + Minefield = ExplosiveAuraWeapon
///
/// **Anti-recursion:** Enemy.OnAnyEnemyDiedAt event fires synchronously à¸‚à¸“à¸°
/// FireMeleeServerRpc à¸à¸³à¸¥à¸±à¸‡ process damage â†’ à¸–à¹‰à¸² explode à¹ƒà¸™ callback à¸•à¸£à¸‡à¹†
/// à¸ˆà¸° recursive call à¸à¸±à¸™à¸ˆà¸™ stack overflow à¸•à¸­à¸™ chain kill à¸«à¸¥à¸²à¸¢à¸•à¸±à¸§
/// â†’ defer à¹ƒà¸ªà¹ˆ queue â†’ process à¹ƒà¸™ LateUpdate (frame à¸–à¸±à¸”à¹„à¸›)
/// </summary>
public class DeathFieldWeapon : WeaponBase
{
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸£à¸°à¹€à¸šà¸´à¸”à¹€à¸¡à¸·à¹ˆà¸­ enemy à¸•à¸²à¸¢à¹ƒà¸™ field")]
    public float deathExplosionRadius = 3f;
    [Tooltip("à¸”à¸²à¹€à¸¡à¸ˆà¸‚à¸­à¸‡ mini explosion")]
    public float deathExplosionDamage = 40f;
    [Tooltip("à¸ˆà¸³à¸à¸±à¸”à¸ˆà¸³à¸™à¸§à¸™ explosion à¸•à¹ˆà¸­ frame à¸à¸±à¸™ lag spike (0 = à¹„à¸¡à¹ˆà¸ˆà¸³à¸à¸±à¸”)")]
    [Min(0)] public int maxExplosionsPerFrame = 32;

    private readonly List<Vector3> _pendingExplosions = new();

    protected override void OnInit()
    {
        Enemy.OnAnyEnemyDiedAt += OnEnemyDiedAt;
    }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyDiedAt -= OnEnemyDiedAt;
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float   radius = ld.range;
        float   dmg    = RollDamage(ld.damage, out bool isCrit);

        if (manager.statManager != null)
        {
            radius *= manager.statManager.GetAreaMultiplier();
            dmg    *= manager.statManager.GetPowerMultiplier();
        }

        FireMelee(center, radius, dmg);
        // Main field hit VFX (parented to player, looping)
        string vfxKey = ResolveHitVfx("OrbiterHit");
        if (!string.IsNullOrEmpty(vfxKey) && vfxKey != "None")
        {
            float scale = radius > 0f ? ComputeVfxScale(vfxKey, radius) : 1f;
            manager.BroadcastVfxParentedServerRpc(vfxKey, scale, isLoop: true);
        }
    }

    void OnEnemyDiedAt(Vector3 deathPos)
    {
        if (manager == null || !manager.IsOwner) return;

        var ld = data?.GetLevelData(currentLevel);
        if (ld == null) return;

        float fieldRadius = ld.range * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);

        // à¸£à¸°à¹€à¸šà¸´à¸”à¹€à¸‰à¸žà¸²à¸° enemy à¸—à¸µà¹ˆà¸•à¸²à¸¢à¸­à¸¢à¸¹à¹ˆà¹ƒà¸™ field à¸‚à¸­à¸‡à¹€à¸£à¸²
        float dist = Vector3.Distance(transform.position, deathPos);
        if (dist > fieldRadius) return;

        // Defer â€” à¸à¸±à¸™ recursive call à¸—à¸³ stack overflow à¸•à¸­à¸™ chain kill
        _pendingExplosions.Add(deathPos);
    }

    void LateUpdate()
    {
        if (_pendingExplosions.Count == 0) return;
        if (manager == null || !manager.IsOwner) { _pendingExplosions.Clear(); return; }

        // Process à¸—à¸µà¸¥à¸° batch â€” à¸à¸±à¸™ lag spike à¸ˆà¸²à¸ chain explosion à¸ˆà¸³à¸™à¸§à¸™à¸¡à¸²à¸
        int processCount = maxExplosionsPerFrame > 0
            ? Mathf.Min(_pendingExplosions.Count, maxExplosionsPerFrame)
            : _pendingExplosions.Count;

        for (int i = 0; i < processCount; i++)
            ExplodeAt(_pendingExplosions[i]);

        _pendingExplosions.RemoveRange(0, processCount);
    }

    void ExplodeAt(Vector3 deathPos)
    {
        Vector3 explosionCenter = deathPos + Vector3.up * 0.5f;
        FireMelee(explosionCenter, deathExplosionRadius, deathExplosionDamage);
        // Chain explosion VFX (à¹€à¸¡à¸·à¹ˆà¸­ enemy à¸•à¸²à¸¢à¹ƒà¸™à¸Ÿà¸´à¸¥à¸”à¹Œ)
        ShowVfx(ResolveSecondaryVfx("GrenadeExplosion"), explosionCenter, deathExplosionRadius);
    }
}
