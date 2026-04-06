using UnityEngine;

/// <summary>
/// Base class สำหรับทุก Ability script (ValorWeapon, BladeOfExileWeapon ฯลฯ)
/// — แยกจาก WeaponBase โดยสมบูรณ์
/// — ใช้ AbilityData แทน WeaponData
/// — ไม่มี cooldown timer อัตโนมัติ (subclass จัดการ input + timing เอง)
/// — FireMeleeServerRpc / FireRaycastServerRpc ยังเรียกผ่าน PlayerWeaponManager
///
/// วิธีใช้ subclass:
///   public class MyAbility : AbilityBase { ... }
///   override OnInit()   — setup (หา components, set state)
///   override OnLevelUp() — ปรับค่าตาม level ใหม่
///   void Update()        — input + cooldown tick (ต้อง check manager.IsOwner เอง)
/// </summary>
public abstract class AbilityBase : MonoBehaviour
{
    [HideInInspector] public AbilityData          data;
    [HideInInspector] public int                  currentLevel;   // 0-indexed
    [HideInInspector] public PlayerWeaponManager  manager;

    protected LayerMask enemyLayer;

    // ── Init ──────────────────────────────────────────────────────────────
    public void Init(AbilityData abilityData, int level, PlayerWeaponManager mgr)
    {
        data         = abilityData;
        currentLevel = Mathf.Clamp(level, 0, Mathf.Max(0, abilityData.levels.Length - 1));
        manager      = mgr;
        enemyLayer   = LayerMask.GetMask("Enemy");
        OnInit();
    }

    public void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 0, data.levels.Length - 1);
        OnLevelUp();
    }

    // ── Virtual / Abstract ────────────────────────────────────────────────
    protected virtual void OnInit()    { }
    protected virtual void OnLevelUp() { }

    // ── Crit Roll ─────────────────────────────────────────────────────────
    protected float RollDamage(float baseDamage)
    {
        var sm = manager?.statManager;
        if (sm != null && Random.value < sm.GetCritChance())
            return baseDamage * 2f;
        return baseDamage;
    }

    // ── Enemy Finders ─────────────────────────────────────────────────────
    protected Transform FindNearestEnemy(float range)
    {
        var cols    = PlayerWeaponManager.OverlapEnemy(transform.position, range);
        Transform nearest = null;
        float     minDist = float.MaxValue;
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
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, data.GetLevelData(currentLevel).range);
    }
}
