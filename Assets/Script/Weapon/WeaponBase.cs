using UnityEngine;
using UnityEngine.InputSystem;

public enum AimMode { AutoNearest, MouseAim }

/// <summary>
/// Base class สำหรับทุก weapon script
/// — อยู่บน weapon prefab ที่ถูก Instantiate เป็น child ของ player
/// — Update/Fire รันเฉพาะบน Owner client
/// — Fire จริงผ่าน PlayerWeaponManager ServerRpc
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    [HideInInspector] public WeaponData          data;
    [HideInInspector] public int                 currentLevel;   // 0-indexed (0 = Lv1)
    [HideInInspector] public PlayerWeaponManager manager;

    [HideInInspector] public AimMode aimMode = AimMode.AutoNearest;

    /// <summary>cooldown multiplier ชั่วคราว — 1 = ปกติ, 0.5 = เร็ว 2× (set โดย HunterUltimate)</summary>
    [HideInInspector] public float tempCooldownMult = 1f;

    protected float     attackTimer;
    protected LayerMask enemyLayer;

    /// <summary>false → subclass จัดการ fire timing เอง (เช่น Charge-based weapons)</summary>
    protected virtual bool UsesCooldownTimer => true;

    // ── Init ──────────────────────────────────────────────────────────────
    public void Init(WeaponData weaponData, int level, PlayerWeaponManager mgr)
    {
        data         = weaponData;
        currentLevel = Mathf.Clamp(level, 0, weaponData.levels.Length - 1);
        manager      = mgr;
        enemyLayer   = LayerMask.GetMask("Enemy");
        attackTimer  = 0f;
        aimMode      = weaponData.aimMode;   // อ่านจาก WeaponData — ตั้งค่าได้ใน Inspector
        OnInit();
    }

    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, data.levels.Length - 1);
        OnLevelUp();
    }

    // ── Update ────────────────────────────────────────────────────────────
    protected virtual void Update()
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;
        if (!UsesCooldownTimer) return;

        attackTimer += Time.deltaTime;

        var   ld              = data.GetLevelData(currentLevel);
        float effectiveCooldown = ld.cooldown;

        if (manager.statManager != null)
            effectiveCooldown *= manager.statManager.GetCooldownMultiplier();
        effectiveCooldown *= tempCooldownMult;

        if (attackTimer < effectiveCooldown) return;

        attackTimer = 0f;

        // Build effective level data (stat-modified copy)
        var effective = BuildEffectiveLevelData(ld);
        OnFire(effective);
    }

    // ── Build Effective Level Data ────────────────────────────────────────
    /// <summary>Fire ทันที โดยไม่ผ่าน cooldown timer — เรียกโดย HunterPassive หรือ charge weapons</summary>
    public void ExecuteFire()
    {
        var ld        = data.GetLevelData(currentLevel);
        var effective = BuildEffectiveLevelData(ld);
        OnFire(effective);
    }

    protected WeaponLevelData BuildEffectiveLevelData(WeaponLevelData base_ld)
    {
        var sm = manager.statManager;
        if (sm == null) return base_ld;

        return new WeaponLevelData
        {
            damage           = base_ld.damage * sm.GetPowerMultiplier(),
            cooldown         = base_ld.cooldown * sm.GetCooldownMultiplier(),
            projectileCount  = base_ld.projectileCount + sm.GetBonusProjectileCount(),
            range            = base_ld.range * sm.GetAreaMultiplier(),
            projectileSpeed  = base_ld.projectileSpeed,
            piercing         = base_ld.piercing
        };
    }

    // ── Fire Helper ───────────────────────────────────────────────────────
    /// <summary>
    /// ยิง projectile ผ่าน NetworkedVFXPool registry
    /// Server lookup prefab จาก projPrefabId — ไม่ต้องพึ่ง weapon children บน server
    /// </summary>
    protected void FireProjectile(
        Vector3 pos, Vector3 dir,
        float damage, float speed,
        int   count     = 1,
        float spreadDeg = 0f,
        bool  piercing  = false,
        float maxRange  = -1f)
    {
        var pool   = NetworkedVFXPool.Instance;
        int projId = pool != null && data?.projectilePrefab != null
            ? pool.GetProjectileId(data.projectilePrefab)
            : -1;
        manager.FireProjectileServerRpc(
            pos, dir, damage, speed, count, spreadDeg,
            piercing, projId, maxRange);
    }

    // ── VFX Helpers — broadcast ผ่าน Pool ไปทุก client ──────────────────

    /// <summary>
    /// แสดง WeaponData.hitVfxPrefab บนทุก client ผ่าน NetworkedVFXPool
    /// prefab ต้องอยู่ใน NetworkedVFXPool.vfxEntries
    /// </summary>
    protected void ShowHitVfx(Vector3 pos, float vfxScale = 1f)
    {
        if (data?.hitVfxPrefab == null) return;
        ShowVfx(data.hitVfxPrefab, pos, vfxScale);
    }

    /// <summary>
    /// แสดง VFX prefab บนทุก client ผ่าน NetworkedVFXPool
    /// prefab ต้องอยู่ใน NetworkedVFXPool.vfxEntries
    /// </summary>
    protected void ShowVfx(GameObject prefab, Vector3 pos, float vfxScale = 1f)
    {
        if (prefab == null) return;
        var pool = NetworkedVFXPool.Instance;
        if (pool == null) { Debug.LogWarning("[VFX] NetworkedVFXPool not found in scene"); return; }
        int id = pool.GetVfxId(prefab);
        if (id < 0) { Debug.LogWarning($"[VFX] '{prefab.name}' ไม่อยู่ใน NetworkedVFXPool.vfxEntries"); return; }
        manager.RequestVfxServerRpc(pos, id, vfxScale);
    }

    // ── Crit Roll ─────────────────────────────────────────────────────────
    protected float RollDamage(float baseDamage)
    {
        var sm = manager.statManager;
        if (sm != null && Random.value < sm.GetCritChance())
            return baseDamage * 2f;
        return baseDamage;
    }

    // ── Abstract / Virtual ────────────────────────────────────────────────
    /// <summary>Implement ใน subclass — เรียก manager.FireProjectileServerRpc หรือ MeleeServerRpc</summary>
    protected abstract void OnFire(WeaponLevelData ld);

    protected virtual void OnInit()    { }
    protected virtual void OnLevelUp() { }

    // ── Aim Direction ─────────────────────────────────────────────────────
    protected Vector3 GetAimDirection()
    {
        if (aimMode == AimMode.MouseAim && Camera.main != null)
        {
            // New Input System
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray   = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 dir = ray.GetPoint(dist) - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f) return dir.normalized;
                }
            }
        }

        // Fallback: Auto-Nearest
        Transform enemy = FindNearestEnemy(data.GetLevelData(currentLevel).range);
        if (enemy == null) return transform.forward;
        Vector3 d = enemy.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
    }

    // ── Enemy Finders ─────────────────────────────────────────────────────
    protected Transform FindNearestEnemy(float range)
    {
        var   cols    = PlayerWeaponManager.OverlapEnemy(transform.position, range);
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in cols)
        {
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearest = c.transform; }
        }
        return nearest;
    }

    protected Collider[] FindAllEnemiesInRange(float range)
        => PlayerWeaponManager.OverlapEnemy(transform.position, range);

    // ── Gizmos ────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.GetLevelData(currentLevel).range);
    }
}
