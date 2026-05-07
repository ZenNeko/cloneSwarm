using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Death Field — Super version ของ Radiant Aura
/// field ใหญ่ขึ้น + enemy ที่ตายใน field ระเบิด mini AoE รอบตัว
///
/// Super tier — 1 level เท่านั้น
///   dmg=30/tick, cd=0.5s, range=6.0
///
/// Fusion: Death Field + Minefield = ExplosiveAuraWeapon
///
/// **Anti-recursion:** Enemy.OnAnyEnemyDiedAt event fires synchronously ขณะ
/// FireMeleeServerRpc กำลัง process damage → ถ้า explode ใน callback ตรงๆ
/// จะ recursive call กันจน stack overflow ตอน chain kill หลายตัว
/// → defer ใส่ queue → process ใน LateUpdate (frame ถัดไป)
/// </summary>
public class DeathFieldWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิดเมื่อ enemy ตายใน field")]
    public float deathExplosionRadius = 3f;
    [Tooltip("ดาเมจของ mini explosion")]
    public float deathExplosionDamage = 40f;
    [Tooltip("จำกัดจำนวน explosion ต่อ frame กัน lag spike (0 = ไม่จำกัด)")]
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

        manager.FireMeleeServerRpc(center, radius, dmg);
        // Main field hit VFX
        ShowVfx(ResolveHitVfx(VFXType.OrbiterHit), center, radius, isCrit);
    }

    void OnEnemyDiedAt(Vector3 deathPos)
    {
        if (manager == null || !manager.IsOwner) return;

        var ld = data?.GetLevelData(currentLevel);
        if (ld == null) return;

        float fieldRadius = ld.range * (manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f);

        // ระเบิดเฉพาะ enemy ที่ตายอยู่ใน field ของเรา
        float dist = Vector3.Distance(transform.position, deathPos);
        if (dist > fieldRadius) return;

        // Defer — กัน recursive call ทำ stack overflow ตอน chain kill
        _pendingExplosions.Add(deathPos);
    }

    void LateUpdate()
    {
        if (_pendingExplosions.Count == 0) return;
        if (manager == null || !manager.IsOwner) { _pendingExplosions.Clear(); return; }

        // Process ทีละ batch — กัน lag spike จาก chain explosion จำนวนมาก
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
        manager.FireMeleeServerRpc(explosionCenter, deathExplosionRadius, deathExplosionDamage);
        // Chain explosion VFX (เมื่อ enemy ตายในฟิลด์)
        ShowVfx(ResolveSecondaryVfx(VFXType.GrenadeExplosion), explosionCenter, deathExplosionRadius);
    }
}
