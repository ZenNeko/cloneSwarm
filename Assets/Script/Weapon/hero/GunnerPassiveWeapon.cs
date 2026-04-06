using System.Collections;
using UnityEngine;

/// <summary>
/// Gunner Passive — WeaponBase (UsesCooldownTimer = false)
/// ทุก 30 kills → MOVE SPEED +50% และ AbilityHaste +50 เป็นเวลา 10 วิ
///
/// ฟัง Enemy.OnAnyEnemyDied (static event, ClientRpc broadcast)
/// นับบน Owner client เท่านั้น — buff ก็ apply บน Owner
/// </summary>
public class GunnerPassiveWeapon : WeaponBase, IHUDPassiveBar
{
    [Header("Passive Config")]
    [Tooltip("จำนวนศัตรูที่ต้องฆ่าก่อน trigger buff")]
    public int killsPerTrigger = 30;
    [Tooltip("Move Speed bonus เพิ่มชั่วคราว (+0.5 = +50%)")]
    public float moveSpeedBonus = 0.5f;
    [Tooltip("Ability Haste bonus เพิ่มชั่วคราว")]
    public float abilityHasteBonus = 50f;
    [Tooltip("ระยะเวลา buff (วินาที)")]
    public float buffDuration = 10f;

    // ── IHUDPassiveBar ────────────────────────────────────────────────────
    public bool   IsActivePassive  => true;   // spawn เฉพาะ Gunner
    public float  NormalizedValue  => (float)KillCount / killsPerTrigger;
    public bool   IsTriggered      => IsBuffActive;
    public string BarText          => IsBuffActive ? "BUFFED!"
                                    : $"{KillCount} / {killsPerTrigger}";
    public UnityEngine.Color BarColor       => new UnityEngine.Color(1f, 0.4f, 0.1f);  // ส้ม
    public UnityEngine.Color TriggeredColor => new UnityEngine.Color(1f, 0.9f, 0f);    // ทอง

    // ── Events (UI ฟัง) ───────────────────────────────────────────────────
    public static event System.Action<GunnerPassiveWeapon, bool> OnBuffStateChanged;
    public static event System.Action<GunnerPassiveWeapon, int>  OnKillCountChanged;

    // ── State ─────────────────────────────────────────────────────────────
    public bool IsBuffActive  { get; private set; }
    public int  KillCount     { get; private set; }

    protected override bool UsesCooldownTimer => false;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        // Subscribe เมื่อ init (ทุก client ที่เป็น owner จะ subscribe)
        if (manager.IsOwner)
            Enemy.OnAnyEnemyDied += OnEnemyKilled;
    }

    protected override void OnFire(WeaponLevelData ld) { /* passive ไม่ยิง */ }

    void OnDestroy()
    {
        Enemy.OnAnyEnemyDied -= OnEnemyKilled;
    }

    // ── Kill Tracking ─────────────────────────────────────────────────────
    void OnEnemyKilled()
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        KillCount++;
        OnKillCountChanged?.Invoke(this, KillCount);

        if (KillCount >= killsPerTrigger)
        {
            KillCount = 0;
            OnKillCountChanged?.Invoke(this, KillCount);
            TriggerBuff();
        }
    }

    // ── Buff ──────────────────────────────────────────────────────────────
    void TriggerBuff()
    {
        // ถ้า buff กำลัง active อยู่ ให้ refresh duration (restart coroutine)
        if (IsBuffActive) StopAllCoroutines();

        StartCoroutine(BuffCoroutine());
    }

    IEnumerator BuffCoroutine()
    {
        // Apply
        IsBuffActive = true;
        if (manager.playerMove  != null) manager.playerMove.tempMoveSpeedBonus  += moveSpeedBonus;
        if (manager.statManager != null) manager.statManager.tempAbilityHaste   += abilityHasteBonus;
        OnBuffStateChanged?.Invoke(this, true);
        Debug.Log($"[GunnerPassive] 🔥 BUFF ACTIVE — MoveSpeed+50% AbilityHaste+50 for {buffDuration}s");

        yield return new UnityEngine.WaitForSeconds(buffDuration);

        // Remove
        IsBuffActive = false;
        if (manager.playerMove  != null) manager.playerMove.tempMoveSpeedBonus  -= moveSpeedBonus;
        if (manager.statManager != null) manager.statManager.tempAbilityHaste   -= abilityHasteBonus;
        OnBuffStateChanged?.Invoke(this, false);
        Debug.Log("[GunnerPassive] Buff ended");
    }
}
