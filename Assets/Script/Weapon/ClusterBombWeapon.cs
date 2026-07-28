using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cluster Blunderbuss — FUSION: Blunderbuss (Super Shotgun) + Splitter Bomb (Super Grenade)
///
/// OnFire: ยิง pellets + grenade พร้อมกัน (MouseAim)
///   — Pellets: กระจาย spread เหมือน Shotgun
///   — Grenade: บินไปที่ mouse ระเบิด AoE (cluster = false)
///
/// On Kill: เมื่อ enemy ตาย → ระเบิด AoE รอบตัว enemy
///          + spawn Cluster Bombs (child grenades) กระจายรอบจุดที่ตาย
///
/// Level data (Fusion tier, 1 level):
///   dmg=80, cd=1.5s, count=5, range=12, projSpeed=18
/// </summary>
public class ClusterBombWeapon : WeaponBase
{
    [Header("Settings (Per-Weapon Prefab)")]
    [Tooltip("Projectile prefab สำหรับ weapon นี้ (ต้องมี NetworkObject + Projectile script)")]
    public GameObject projectilePrefab;

    protected override GameObject GetProjectilePrefab() => projectilePrefab;
    [Header("Shotgun Part")]
    [Tooltip("มุมกระจาย pellets ทั้งหมด (องศา)")]
    public float spreadAngle     = 40f;

    [Header("Grenade Part")]
    public float grenadeRadius   = 3.5f;
    public float fuseTime        = 1.0f;

    [Header("On-Kill Explosion")]
    [Tooltip("รัศมี AoE ทันทีที่ enemy ตาย")]
    public float killExplosionRadius  = 3f;
    [Tooltip("ดาเมจ AoE on-kill")]
    public float killExplosionDamage  = 60f;
    [Tooltip("จำนวน child grenades ที่ spawn รอบจุดที่ตาย")]
    public int   childGrenadeCount    = 3;
    [Tooltip("รัศมีกระจาย child grenades")]
    public float childGrenadeSpread   = 4f;
    [Tooltip("รัศมีระเบิด child grenade")]
    public float childGrenadeRadius   = 2f;
    [Tooltip("fuse time ของ child grenade")]
    public float childGrenadeFuse     = 0.6f;
    [Tooltip("จำกัดจำนวน on-kill explosion ต่อ frame กัน lag spike (0 = ไม่จำกัด)")]
    [Min(0)] public int maxKillExplosionsPerFrame = 32;

    // Anti-recursion: defer on-kill explosion ไป LateUpdate กัน stack overflow
    // เมื่อ chain kill หลายตัวพร้อมกัน (FireMelee → kill → event → FireMelee → ...)
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

        // ── Pellets (Shotgun) ─────────────────────────────────────────────
        int pellets = Mathf.Max(1, ld.projectileCount);
        FireProjectile(spawnPos, dir, dmg / pellets, ld.projectileSpeed,
            count: pellets, spreadDeg: spreadAngle / Mathf.Max(1, pellets - 1),
            isCrit: isCrit);

        // ── Grenade (สุ่มรอบ player) ──────────────────────────────────────
        float radius = grenadeRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        Vector2 rnd         = Random.insideUnitCircle * ld.range;
        Vector3 grenadePos  = transform.position + new Vector3(rnd.x, 0f, rnd.y);
        ThrowGrenade(spawnPos, grenadePos, dmg, radius, fuseTime, cluster: false, isCrit: isCrit);
    }

    void OnEnemyKilled(Vector3 deathPos)
    {
        if (manager == null || !manager.IsOwner) return;

        var ld = data != null ? data.GetLevelData(currentLevel) : null;
        if (ld == null) return;

        // ระเบิดเฉพาะ enemy ที่ตายในระยะของอาวุธเรา — ไม่ใช่ทั้งแมพ
        float triggerRadius = ld.range * (manager.statManager != null
            ? manager.statManager.GetAreaMultiplier() : 1f);
        if (Vector3.Distance(transform.position, deathPos) > triggerRadius) return;

        // Defer — กัน recursive call ทำ stack overflow ตอน chain kill หลายตัว
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
        // ── AoE ทันทีที่จุดตาย ────────────────────────────────────────────
        float radius = killExplosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        float dmg = RollDamage(killExplosionDamage, out bool isCrit);
        FireMelee(deathPos + Vector3.up * 0.5f, radius, dmg, isCrit);
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), deathPos, radius, isCrit);

        // ── Cluster Bombs รอบจุดตาย ───────────────────────────────────────
        Vector3 spawnPos = deathPos + Vector3.up * 0.5f;
        float   childRad = childGrenadeRadius;
        if (manager.statManager != null)
            childRad *= manager.statManager.GetAreaMultiplier();

        for (int i = 0; i < childGrenadeCount; i++)
        {
            Vector2 rnd      = Random.insideUnitCircle * childGrenadeSpread;
            Vector3 childPos = deathPos + new Vector3(rnd.x, 0f, rnd.y);
            ThrowGrenade(spawnPos, childPos, dmg * 0.6f,
                childRad, childGrenadeFuse, cluster: false, isCrit: isCrit);
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
