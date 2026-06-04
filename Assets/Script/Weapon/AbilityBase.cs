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

    [Header("SFX (Per-Ability Prefab)")]
    [Tooltip("เสียงตอนใช้ ability — ใส่ได้หลายเสียง สุ่มเล่น\n" +
             "เรียกผ่าน PlayCastSfx() ใน OnActivate() ของ ability script")]
    public AudioClip[] castSfx;
    [Tooltip("เสียงตอน ability ทำดาเมจ / hit — ใส่ได้หลายเสียง สุ่มเล่น\n" +
             "(เช่น exile activate, missile explode)")]
    public AudioClip[] hitSfx;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ castSfx")]
    public float castVolume = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("ความดังของ hitSfx")]
    public float hitVolume  = 0.7f;
    [Range(0f, 0.5f)]
    [Tooltip("Pitch variance สุ่มต่อครั้ง (0 = ไม่สุ่ม) — 0.1 = ±10%")]
    public float pitchVariance = 0.05f;

    protected LayerMask enemyLayer;

    // ── Scene-based gating (auto disable in MenuScene/lobby) ──────────────
    // Ability subclass override Update เอง → ไม่มีจุดเดียว patch ได้
    // วิธีนี้: ใช้ `enabled` property → Unity ไม่เรียก Update ของ component นี้
    // เมื่อ active scene เปลี่ยน → recompute ทันที (ลด overhead, ไม่ต้อง check ทุก frame)
    //
    // ⚠ ใช้ Awake/OnDestroy (NOT OnEnable/OnDisable):
    //    ถ้า subscribe ใน OnEnable แล้ว ApplySceneGate set enabled=false ทันที,
    //    OnDisable จะวิ่ง unsubscribe → ไม่มีใคร re-enable ตอน scene เปลี่ยนกลับ
    void Awake()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ApplySceneGate();
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene __)
        => ApplySceneGate();

    void ApplySceneGate()
    {
        // อยู่ใน Menu/Lobby → component disabled → Update/input ไม่วิ่งเลย
        // กลับเข้า gameplay scene → re-enabled อัตโนมัติ
        enabled = PlayerWeaponManager.WeaponsEnabledInScene;
    }

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

    /// <summary>RollDamage + crit flag — ใช้เมื่อต้องรู้ว่า crit หรือไม่ (เช่น เลือก VFX)</summary>
    protected float RollDamage(float baseDamage, out bool isCrit)
    {
        var sm = manager?.statManager;
        if (sm != null && Random.value < sm.GetCritChance())
        { isCrit = true; return baseDamage * 2f; }
        isCrit = false; return baseDamage;
    }

    // ── SFX Helpers (delegate to SoundManager) ───────────────────────────
    /// <summary>เล่น random clip จาก castSfx[] ที่ตำแหน่งผู้เล่น</summary>
    protected void PlayCastSfx()
    {
        SoundManager.Instance.PlayRandomSfx(castSfx, transform.position, castVolume, pitchVariance);
    }

    /// <summary>เล่น random clip จาก hitSfx[] ที่จุดกระทบ</summary>
    protected void PlayHitSfx(Vector3 pos)
    {
        SoundManager.Instance.PlayRandomSfx(hitSfx, pos, hitVolume, pitchVariance);
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
