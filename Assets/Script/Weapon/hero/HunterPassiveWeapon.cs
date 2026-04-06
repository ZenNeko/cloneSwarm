using UnityEngine;

/// <summary>
/// Hunter Passive — WeaponBase (UsesCooldownTimer = false)
/// ทุก 50 ครั้งที่ deal damage (hit) → force-fire ทุก weapon ที่ equipped ทันที
///
/// ฟัง Enemy.OnAnyEnemyHit (static event, ClientRpc broadcast)
/// นับบน Owner client เท่านั้น — trigger ก็ apply บน Owner
/// </summary>
public class HunterPassiveWeapon : WeaponBase, IHUDPassiveBar
{
    [Header("Passive Config")]
    [Tooltip("จำนวน damage hit ก่อน trigger")]
    public int hitsPerTrigger = 50;

    // ── Effective threshold (scale กับ Ability Haste) ────────────────────
    /// <summary>
    /// Ability Haste สูง → CooldownMultiplier ต่ำกว่า 1 → hits ที่ต้องการน้อยลง
    /// ตัวอย่าง: hitsPerTrigger=50, Haste=100 → mult=0.5 → ต้องการแค่ 25 hits
    /// </summary>
    private int EffectiveHitsPerTrigger
    {
        get
        {
            if (manager?.statManager == null) return hitsPerTrigger;
            float mult = manager.statManager.GetCooldownMultiplier();  // ≤1 เมื่อมี Haste
            return Mathf.Max(1, Mathf.RoundToInt(hitsPerTrigger * mult));
        }
    }

    // ── IHUDPassiveBar ────────────────────────────────────────────────────
    public bool   IsActivePassive  => true;   // spawn เฉพาะ Hunter
    public float  NormalizedValue  => (float)HitCount / EffectiveHitsPerTrigger;
    public bool   IsTriggered      => false;   // trigger ทันที ไม่มี "ready" state
    public string BarText          => $"{HitCount} / {EffectiveHitsPerTrigger}";
    public UnityEngine.Color BarColor       => new UnityEngine.Color(0.1f, 0.95f, 1f);  // ฟ้า cyan
    public UnityEngine.Color TriggeredColor => new UnityEngine.Color(1f, 0.9f, 0f);     // ทอง

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<HunterPassiveWeapon, int>  OnHitCountChanged;
    public static event System.Action<HunterPassiveWeapon>       OnTriggered;

    // ── State ─────────────────────────────────────────────────────────────
    public int HitCount { get; private set; }

    protected override bool UsesCooldownTimer => false;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        if (manager.IsOwner)
            Enemy.OnAnyEnemyHit += OnEnemyHit;
    }

    protected override void OnFire(WeaponLevelData ld) { /* passive ไม่ยิงเอง */ }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyHit -= OnEnemyHit;
    }

    // ── Hit Tracking ──────────────────────────────────────────────────────
    void OnEnemyHit()
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        HitCount++;
        OnHitCountChanged?.Invoke(this, HitCount);

        if (HitCount >= EffectiveHitsPerTrigger)
        {
            HitCount = 0;
            OnHitCountChanged?.Invoke(this, HitCount);
            TriggerAllWeapons();
        }
    }

    // ── Trigger ───────────────────────────────────────────────────────────
    void TriggerAllWeapons()
    {
        manager.ForceFireAllWeapons();
        OnTriggered?.Invoke(this);
        UnityEngine.Debug.Log("[HunterPassive] ⚡ TRIGGER — force-fired all weapons");
    }
}
