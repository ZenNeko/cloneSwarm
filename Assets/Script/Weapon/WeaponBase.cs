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
        float maxRange  = -1f,
        bool  isCrit    = false)
    {
        var pool   = NetworkedVFXPool.Instance;
        int projId = pool != null && data?.projectilePrefab != null
            ? pool.GetProjectileId(data.projectilePrefab)
            : -1;
        manager.FireProjectileServerRpc(
            pos, dir, damage, speed, count, spreadDeg,
            piercing, projId, maxRange, isCrit);
    }

    // ── VFX Helpers — broadcast ผ่าน Pool ไปทุก client ──────────────────

    /// <summary>
    /// คำนวณ VFX scale จาก actualRange เทียบกับ designedRadius ของ prefab
    /// actualRange = radius/range จริงที่ weapon ใช้ (หลัง stat multiplier)
    /// คืน 1f ถ้า designedRadius = 0 (fixed size) หรือไม่พบ type
    /// </summary>
    protected float ComputeVfxScale(VFXType type, float actualRange)
    {
        var pool = NetworkedVFXPool.Instance;
        if (pool == null) return 1f;
        float designed = pool.GetDesignedRadius(type);
        if (designed <= 0f) return 1f;
        return actualRange / designed;
    }

    /// <summary>
    /// แสดง base hit VFX (HitEffect / CritHitEffect) ที่ตำแหน่ง pos
    /// ทุก weapon ควรเรียกเมื่อโจมตีโดน enemy เพื่อให้มี feedback พื้นฐาน
    /// </summary>
    protected void ShowBaseHitVfx(Vector3 pos, bool isCrit = false)
    {
        VFXType baseHit = isCrit ? VFXType.CritHitEffect : VFXType.HitEffect;
        manager.BroadcastVfxTypeServerRpc(pos, (int)baseHit);
    }

    /// <summary>
    /// แสดง WeaponData.hitVfxType — ใช้สำหรับ projectile weapon ที่ hit VFX อยู่ใน data
    /// ถ้า isCrit → แสดง CritHitEffect แทน | ปกติ → แสดง hitVfxType
    /// </summary>
    protected void ShowHitVfx(Vector3 pos, float actualRange = 0f, bool isCrit = false)
    {
        // base hit effect เสมอ
        ShowBaseHitVfx(pos, isCrit);

        // weapon-specific hit VFX (ถ้ามี + ไม่ซ้ำกับ base)
        if (data == null || data.hitVfxType == VFXType.None) return;
        if (data.hitVfxType == VFXType.HitEffect || data.hitVfxType == VFXType.CritHitEffect) return;
        float scale = actualRange > 0f ? ComputeVfxScale(data.hitVfxType, actualRange) : 1f;
        manager.BroadcastVfxTypeServerRpc(pos, (int)data.hitVfxType, scale);
    }

    /// <summary>
    /// แสดง VFX จาก VFXType บนทุก client + base HitEffect/CritHitEffect ซ้อนทับ
    /// actualRange = 0 → scale=1f | actualRange > 0 → auto scale จาก designedRadius
    /// isAttackHit = true → เพิ่ม base HitEffect/CritHitEffect (default)
    /// isAttackHit = false → แสดงเฉพาะ type (สำหรับ non-hit เช่น DashTrail, VortexSpawn)
    /// </summary>
    /// <summary>
    /// แสดง VFX จาก VFXType บนทุก client + base HitEffect/CritHitEffect ซ้อนทับ
    /// direction = ทิศที่ VFX หันหน้าไป — ใช้กับ Slash/Melee VFX Graph (default = ไม่หมุน)
    /// </summary>
    protected void ShowVfx(VFXType type, Vector3 pos, float actualRange = 0f,
                           bool isCrit = false, bool isAttackHit = true,
                           Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
    {
        if (type == VFXType.None) return;
        float scale = actualRange > 0f ? ComputeVfxScale(type, actualRange) : 1f;
        manager.BroadcastVfxTypeServerRpc(pos, (int)type, scale, direction, arcAngle, roll);

        // base hit effect ซ้อนทับ — ทุก attack hit ต้องมี
        if (isAttackHit && type != VFXType.HitEffect && type != VFXType.CritHitEffect)
            ShowBaseHitVfx(pos, isCrit);
    }

    // ── Crit Roll ─────────────────────────────────────────────────────────
    protected float RollDamage(float baseDamage)
    {
        var sm = manager.statManager;
        if (sm != null && Random.value < sm.GetCritChance())
            return baseDamage * 2f;
        return baseDamage;
    }

    /// <summary>RollDamage + crit flag — ใช้เมื่อต้องรู้ว่า crit หรือไม่ (เช่น เลือก VFX)</summary>
    protected float RollDamage(float baseDamage, out bool isCrit)
    {
        var sm = manager.statManager;
        if (sm != null && Random.value < sm.GetCritChance())
        {
            isCrit = true;
            return baseDamage * 2f;
        }
        isCrit = false;
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
