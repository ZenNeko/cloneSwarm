using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

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

    [Header("VFX (Per-Weapon Prefab)")]
    [Tooltip("VFX หลักของ weapon (slash arc, explosion shape, beam ฯลฯ)\n" +
             "None = ใช้ default ของ script (มี fallback hardcoded)\n" +
             "Type prefab กำหนดใน NetworkedVFXPool.vfxTypeMappings\n\n" +
             "หมายเหตุ: HitEffect / CritHitEffect ของ enemy (impact spark) — Enemy.cs จัดการเอง\n" +
             "ผ่าน NotifyHitClientRpc → ไม่ต้อง config ที่ weapon")]
    [FormerlySerializedAs("weaponVfxKey")]
    [VFXKey]
    public string weaponVfxType = "None";

    [Tooltip("VFX รอง (optional) — สำหรับ weapon ที่มี VFX 2 ตัว\n" +
             "เช่น CycloneBlade (AoE 360 + per-hit slash) / DeathField (main + chain explosion)\n" +
             "None = ใช้ default ของ script")]
    [FormerlySerializedAs("secondaryVfxKey")]
    [VFXKey]
    public string secondaryVfxType = "None";

    [Header("SFX (Per-Weapon Prefab)")]
    [Tooltip("เสียงตอน weapon ยิง / โจมตี — ใส่ได้หลายเสียง สุ่มเล่นทีละอัน\n" +
             "เรียกผ่าน PlayFireSfx() ใน OnFire() ของ weapon script\n" +
             "Array ว่าง = ไม่มีเสียง")]
    public AudioClip[] fireSfx;
    [Tooltip("เสียงเมื่อ projectile/attack กระทบศัตรู — ใส่ได้หลายเสียง สุ่มเล่นทีละอัน\n" +
             "เรียกผ่าน PlayHitSfx() จาก projectile หรือ weapon\n" +
             "Array ว่าง = ไม่มีเสียง")]
    public AudioClip[] hitSfx;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ fireSfx (0 = เงียบ, 1 = เต็ม)")]
    public float fireVolume = 0.7f;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ hitSfx")]
    public float hitVolume  = 0.6f;
    [Range(0f, 0.5f)]
    [Tooltip("Pitch range สุ่มต่อครั้ง (0 = ไม่สุ่ม) — สร้าง variety แม้ใช้ clip เดียว\n" +
             "0.1 = สุ่ม pitch ±10% (0.9 → 1.1)")]
    public float pitchVariance = 0.05f;

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
        // Block weapon firing while still in MenuScene (Online Session lobby) —
        // player prefab อาจ spawn ใน lobby ก่อนเริ่มเกมจริง
        if (!PlayerWeaponManager.WeaponsEnabledInScene) return;
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
        PlayFireSfx();        // auto fire SFX สำหรับ weapon ทุกตัว
        OnFire(effective);
    }

    // ── Build Effective Level Data ────────────────────────────────────────
    /// <summary>Fire ทันที โดยไม่ผ่าน cooldown timer — เรียกโดย HunterPassive หรือ charge weapons</summary>
    public void ExecuteFire()
    {
        var ld        = data.GetLevelData(currentLevel);
        var effective = BuildEffectiveLevelData(ld);
        PlayFireSfx();        // auto fire SFX สำหรับ HunterPassive / charge weapons
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
            piercing, projId, maxRange, isCrit, data != null ? data.weaponName : "Unknown");
    }

    protected void FireMelee(Vector3 center, float radius, float damage, bool isCrit = false)
    {
        manager.FireMeleeServerRpc(center, radius, damage, isCrit, data != null ? data.weaponName : "Unknown");
    }

    protected void FireArcMelee(Vector3 center, Vector3 forward, float radius, float arcAngle, float damage, bool isCrit = false)
    {
        manager.FireArcMeleeServerRpc(center, forward, radius, arcAngle, damage, isCrit, data != null ? data.weaponName : "Unknown");
    }

    protected void FireLineAoE(Vector3 origin, Vector3 direction, float damage, float range, float width = 1.5f, bool isCrit = false, float knockbackForce = 0f, string vfxKey = "None")
    {
        manager.FireLineAoEServerRpc(origin, direction, damage, range, width, isCrit, knockbackForce, vfxKey, data != null ? data.weaponName : "Unknown");
    }

    protected void FireRaycast(Vector3 origin, Vector3 direction, float damage, float maxDist = 50f, string vfxKey = "None", bool isCrit = false, bool playHitVfx = true)
    {
        manager.FireRaycastServerRpc(origin, direction, damage, maxDist, vfxKey, isCrit, playHitVfx, data != null ? data.weaponName : "Unknown");
    }

    protected void SpawnBoomerang(Vector3 spawnPos, Vector3 direction, float damage, float speed, float maxRange, bool isCrit = false)
    {
        manager.SpawnBoomerangServerRpc(spawnPos, direction, damage, speed, maxRange, isCrit, data != null ? data.weaponName : "Unknown");
    }

    protected void ThrowGrenade(Vector3 spawnPos, Vector3 targetPos, float damage, float radius, float fuseTime = 1.5f, bool cluster = false)
    {
        manager.ThrowGrenadeServerRpc(spawnPos, targetPos, damage, radius, fuseTime, cluster, data != null ? data.weaponName : "Unknown");
    }

    protected void DropMine(Vector3 position, float damage, float triggerRadius)
    {
        manager.DropMineServerRpc(position, damage, triggerRadius, data != null ? data.weaponName : "Unknown");
    }

    protected void SpawnStickyRocket(Vector3 spawnPos, Vector3 direction, float damage, float speed, float explosionRadius)
    {
        manager.SpawnStickyRocketServerRpc(spawnPos, direction, damage, speed, explosionRadius, data != null ? data.weaponName : "Unknown");
    }

    // ── VFX Helpers — broadcast ผ่าน Pool ไปทุก client ──────────────────

    /// <summary>
    /// คำนวณ VFX scale จาก actualRange เทียบกับ designedRadius ของ prefab
    /// actualRange = radius/range จริงที่ weapon ใช้ (หลัง stat multiplier)
    /// คืน 1f ถ้า designedRadius = 0 (fixed size) หรือไม่พบ type
    /// </summary>
    protected float ComputeVfxScale(string key, float actualRange)
    {
        var pool = NetworkedVFXPool.Instance;
        if (pool == null) return 1f;
        float designed = pool.GetDesignedRadius(key);
        if (designed <= 0f) return 1f;
        return actualRange / designed;
    }

    // NOTE: HitEffect / CritHitEffect ของ enemy ตอนโดน — Enemy.cs จัดการเอง
    //       ผ่าน NotifyHitClientRpc ใน EnemyTakeDamage. weapon ไม่ต้อง spawn ซ้ำ

    // ── SFX Helpers (delegate to SoundManager) ───────────────────────────
    /// <summary>เล่น random clip จาก fireSfx[] ที่ตำแหน่งผู้เล่น</summary>
    protected void PlayFireSfx()
    {
        SoundManager.Instance.PlayRandomSfx(fireSfx, transform.position, fireVolume, pitchVariance);
    }

    /// <summary>เล่น random clip จาก hitSfx[] ที่จุดกระทบ</summary>
    protected void PlayHitSfx(Vector3 pos)
    {
        SoundManager.Instance.PlayRandomSfx(hitSfx, pos, hitVolume, pitchVariance);
    }

    // ── VFX Resolvers (อ่านจาก field ของ weapon prefab) ──────────────────
    /// <summary>
    /// คืน weaponVfxType (Inspector field) ถ้าตั้งไว้ ไม่งั้น fallback
    /// ใช้ใน weapon script แทน hardcode เพื่อให้ designer override ผ่าน Inspector ของ weapon prefab ได้
    /// </summary>
    protected string ResolveHitVfx(string fallback)
        => (!string.IsNullOrEmpty(weaponVfxType) && weaponVfxType != "None") ? weaponVfxType : fallback;

    /// <summary>
    /// คืน secondaryVfxType (Inspector field) ถ้าตั้งไว้ ไม่งั้น fallback
    /// ใช้กับ weapon ที่มี VFX 2 ตัว (เช่น CycloneBlade, DeathField)
    /// </summary>
    protected string ResolveSecondaryVfx(string fallback)
        => (!string.IsNullOrEmpty(secondaryVfxType) && secondaryVfxType != "None") ? secondaryVfxType : fallback;

    /// <summary>
    /// แสดง weaponVfxType — ใช้สำหรับ projectile weapon ที่ VFX อยู่ใน weapon prefab
    /// HitEffect/CritHitEffect ของ enemy spawn จาก Enemy.cs เอง — ไม่ทับซ้อน
    /// </summary>
    protected void ShowHitVfx(Vector3 pos, float actualRange = 0f, bool isCrit = false)
    {
        if (string.IsNullOrEmpty(weaponVfxType) || weaponVfxType == "None") return;
        if (weaponVfxType == "HitEffect" || weaponVfxType == "CritHitEffect") return;
        float scale = actualRange > 0f ? ComputeVfxScale(weaponVfxType, actualRange) : 1f;
        manager.BroadcastVfxTypeServerRpc(pos, weaponVfxType, scale);
    }

    /// <summary>
    /// แสดง VFX จาก string key บนทุก client
    /// actualRange = 0 → scale=1f | actualRange > 0 → auto scale จาก designedRadius
    /// direction = ทิศที่ VFX หันหน้าไป — ใช้กับ Slash/Melee VFX Graph (default = ไม่หมุน)
    /// isAttackHit เก็บไว้เพื่อ backward-compat (ไม่ใช้แล้ว — Enemy.cs spawn HitEffect/CritHitEffect เอง)
    /// </summary>
    protected void ShowVfx(string key, Vector3 pos, float actualRange = 0f,
                           bool isCrit = false, bool isAttackHit = true,
                           Vector3 direction = default, float arcAngle = 360f, float roll = 0f)
    {
        if (string.IsNullOrEmpty(key) || key == "None") return;
        float scale = actualRange > 0f ? ComputeVfxScale(key, actualRange) : 1f;
        manager.BroadcastVfxTypeServerRpc(pos, key, scale, direction, arcAngle, roll);
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
    protected virtual void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.GetLevelData(currentLevel).range);
    }
}
