using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cluster Blunderbuss â€” FUSION: Blunderbuss (Super Shotgun) + Minefield (Super Grenade)
///
/// OnFire: à¸¢à¸´à¸‡ pellets + grenade à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™ (MouseAim)
///   â€” Pellets: à¸à¸£à¸°à¸ˆà¸²à¸¢ spread à¹€à¸«à¸¡à¸·à¸­à¸™ Shotgun
///   â€” Grenade: à¸šà¸´à¸™à¹„à¸›à¸—à¸µà¹ˆ mouse à¸£à¸°à¹€à¸šà¸´à¸” AoE (cluster = false)
///
/// On Kill: à¹€à¸¡à¸·à¹ˆà¸­ enemy à¸•à¸²à¸¢ â†’ à¸£à¸°à¹€à¸šà¸´à¸” AoE à¸£à¸­à¸šà¸•à¸±à¸§ enemy
///          + spawn Cluster Bombs (child grenades) à¸à¸£à¸°à¸ˆà¸²à¸¢à¸£à¸­à¸šà¸ˆà¸¸à¸”à¸—à¸µà¹ˆà¸•à¸²à¸¢
///
/// Level data (Fusion tier, 1 level):
///   dmg=80, cd=1.5s, count=5, range=12, projSpeed=18
/// </summary>
public class ClusterBombWeapon : WeaponBase
{
    [Header("Shotgun Part")]
    [Tooltip("à¸¡à¸¸à¸¡à¸à¸£à¸°à¸ˆà¸²à¸¢ pellets à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸” (à¸­à¸‡à¸¨à¸²)")]
    public float spreadAngle     = 40f;

    [Header("Grenade Part")]
    public float grenadeRadius   = 3.5f;
    public float fuseTime        = 1.0f;

    [Header("On-Kill Explosion")]
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µ AoE à¸—à¸±à¸™à¸—à¸µà¸—à¸µà¹ˆ enemy à¸•à¸²à¸¢")]
    public float killExplosionRadius  = 3f;
    [Tooltip("à¸”à¸²à¹€à¸¡à¸ˆ AoE on-kill")]
    public float killExplosionDamage  = 60f;
    [Tooltip("à¸ˆà¸³à¸™à¸§à¸™ child grenades à¸—à¸µà¹ˆ spawn à¸£à¸­à¸šà¸ˆà¸¸à¸”à¸—à¸µà¹ˆà¸•à¸²à¸¢")]
    public int   childGrenadeCount    = 3;
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸à¸£à¸°à¸ˆà¸²à¸¢ child grenades")]
    public float childGrenadeSpread   = 4f;
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸£à¸°à¹€à¸šà¸´à¸” child grenade")]
    public float childGrenadeRadius   = 2f;
    [Tooltip("fuse time à¸‚à¸­à¸‡ child grenade")]
    public float childGrenadeFuse     = 0.6f;
    [Tooltip("à¸ˆà¸³à¸à¸±à¸”à¸ˆà¸³à¸™à¸§à¸™ on-kill explosion à¸•à¹ˆà¸­ frame à¸à¸±à¸™ lag spike (0 = à¹„à¸¡à¹ˆà¸ˆà¸³à¸à¸±à¸”)")]
    [Min(0)] public int maxKillExplosionsPerFrame = 32;

    // Anti-recursion: defer on-kill explosion à¹„à¸› LateUpdate à¸à¸±à¸™ stack overflow
    // à¹€à¸¡à¸·à¹ˆà¸­ chain kill à¸«à¸¥à¸²à¸¢à¸•à¸±à¸§à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™ (FireMelee â†’ kill â†’ event â†’ FireMelee â†’ ...)
    private readonly List<Vector3> _pendingKillExplosions = new();

    protected override void OnInit()
    {
        Enemy.OnAnyEnemyDiedAt += OnEnemyKilled;
    }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyDiedAt -= OnEnemyKilled;
    }

    protected override void OnFire(WeaponLevelData ld)
    {
        if (manager == null || !manager.IsOwner) return;

        Vector3 spawnPos  = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = GetMouseWorldPosition();

        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.magnitude > ld.range)
            targetPos = transform.position + toTarget.normalized * ld.range;

        Vector3 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) dir.Normalize(); else dir = transform.forward;

        float dmg = RollDamage(ld.damage, out bool isCrit);

        // â”€â”€ Pellets (Shotgun) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        int pellets = Mathf.Max(1, ld.projectileCount);
        FireProjectile(spawnPos, dir, dmg / pellets, ld.projectileSpeed,
            count: pellets, spreadDeg: spreadAngle / Mathf.Max(1, pellets - 1),
            isCrit: isCrit);

        // â”€â”€ Grenade (à¸ªà¸¸à¹ˆà¸¡à¸£à¸­à¸š player) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float radius = grenadeRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        Vector2 rnd         = Random.insideUnitCircle * ld.range;
        Vector3 grenadePos  = transform.position + new Vector3(rnd.x, 0f, rnd.y);
        ThrowGrenade(spawnPos, grenadePos, dmg, radius, fuseTime, cluster: false);
    }

    void OnEnemyKilled(Vector3 deathPos)
    {
        if (manager == null || !manager.IsOwner) return;

        // Defer â€” à¸à¸±à¸™ recursive call à¸—à¸³ stack overflow à¸•à¸­à¸™ chain kill à¸«à¸¥à¸²à¸¢à¸•à¸±à¸§
        _pendingKillExplosions.Add(deathPos);
    }

    void LateUpdate()
    {
        if (_pendingKillExplosions.Count == 0) return;
        if (manager == null || !manager.IsOwner) { _pendingKillExplosions.Clear(); return; }

        int processCount = maxKillExplosionsPerFrame > 0
            ? Mathf.Min(_pendingKillExplosions.Count, maxKillExplosionsPerFrame)
            : _pendingKillExplosions.Count;

        for (int i = 0; i < processCount; i++)
            ProcessKillExplosion(_pendingKillExplosions[i]);

        _pendingKillExplosions.RemoveRange(0, processCount);
    }

    void ProcessKillExplosion(Vector3 deathPos)
    {
        // â”€â”€ AoE à¸—à¸±à¸™à¸—à¸µà¸—à¸µà¹ˆà¸ˆà¸¸à¸”à¸•à¸²à¸¢ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float radius = killExplosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        FireMelee(deathPos + Vector3.up * 0.5f, radius, killExplosionDamage);
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), deathPos, radius);

        // â”€â”€ Cluster Bombs à¸£à¸­à¸šà¸ˆà¸¸à¸”à¸•à¸²à¸¢ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        Vector3 spawnPos = deathPos + Vector3.up * 0.5f;
        float   childRad = childGrenadeRadius;
        if (manager.statManager != null)
            childRad *= manager.statManager.GetAreaMultiplier();

        for (int i = 0; i < childGrenadeCount; i++)
        {
            Vector2 rnd      = Random.insideUnitCircle * childGrenadeSpread;
            Vector3 childPos = deathPos + new Vector3(rnd.x, 0f, rnd.y);
            ThrowGrenade(spawnPos, childPos, killExplosionDamage * 0.6f,
                childRad, childGrenadeFuse, cluster: false);
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        if (Camera.main == null) return transform.position + transform.forward * 5f;
        var plane = new Plane(Vector3.up, transform.position);
        var ray   = Camera.main.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current?.position.ReadValue()
            ?? new UnityEngine.Vector2(Screen.width / 2f, Screen.height / 2f));
        if (plane.Raycast(ray, out float d)) return ray.GetPoint(d);
        return transform.position + transform.forward * 5f;
    }
}
