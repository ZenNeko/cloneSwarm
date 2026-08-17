using System.Collections;
using System.Collections.Generic;
using CloneSwarm.Meta;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ถือ Augment ที่ผู้เล่นเลือกไว้ใน run ปัจจุบัน + รัน trigger ให้
/// วางบน Player Prefab (ตัวเดียวกับ PlayerStatManager / UpgradeManager)
///
/// Authority: ตรรกะเลือกการ์ดรันบน **owner** เหมือน PlayerStatManager
/// แล้ว mirror ไป server ผ่าน ServerRpc ที่ส่งแค่ **index** ของ augment
/// (server อ่านค่าจาก MetaDatabase ของตัวเอง → client แต่งค่าไม่ได้)
/// </summary>
[RequireComponent(typeof(PlayerStatManager))]
public class PlayerAugmentManager : NetworkBehaviour
{
    // ── Component shortcuts (subclass ของ AugmentData เรียกใช้) ────────────
    public PlayerStatManager    Stats     { get; private set; }
    public playermove           Move      { get; private set; }
    public PlayerWeaponManager  Weapons   { get; private set; }
    public PlayerAbilityManager Abilities { get; private set; }

    /// <summary>ยิงบน owner เมื่อได้ augment ใหม่ — HUD subscribe เพื่อโชว์ไอคอน</summary>
    public static event System.Action<AugmentData> OnAugmentAcquired;

    // ── State ─────────────────────────────────────────────────────────────
    readonly List<AugmentData>    acquired = new();
    readonly List<RuntimeTrigger> triggers = new();

    class RuntimeTrigger
    {
        public TriggerAugment def;
        public float          progress;      // นับ kill สะสม / วินาทีสะสม
        public float          nextAllowed;   // Time.time ที่ยิงได้อีกครั้ง
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════════
    public override void OnNetworkSpawn()
    {
        Stats     = GetComponent<PlayerStatManager>();
        Move      = GetComponent<playermove>();
        Weapons   = GetComponent<PlayerWeaponManager>();
        Abilities = GetComponent<PlayerAbilityManager>();

        if (!IsOwner) return;

        Enemy.OnAnyEnemyDied                     += HandleAnyEnemyDied;
        SharedExperienceManager.OnSharedLevelChanged += HandleLevelChanged;
        if (Move != null) Move.netHealth.OnValueChanged += HandleHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        Enemy.OnAnyEnemyDied                     -= HandleAnyEnemyDied;
        SharedExperienceManager.OnSharedLevelChanged -= HandleLevelChanged;
        if (Move != null) Move.netHealth.OnValueChanged -= HandleHealthChanged;
    }

    void Update()
    {
        if (!IsOwner || triggers.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < triggers.Count; i++)
        {
            var rt = triggers[i];
            if (rt.def.trigger != AugmentTrigger.Periodic) continue;

            rt.progress += dt;
            if (rt.progress >= rt.def.triggerAmount)
            {
                rt.progress -= rt.def.triggerAmount;
                Fire(rt);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Acquire
    // ═══════════════════════════════════════════════════════════════════════
    public bool HasAugment(AugmentData a) => a != null && acquired.Contains(a);

    public int GetStackCount(AugmentData a)
    {
        if (a == null) return 0;
        int n = 0;
        for (int i = 0; i < acquired.Count; i++) if (acquired[i] == a) n++;
        return n;
    }

    /// <summary>คืน augment ทั้งหมดที่ถืออยู่ (สำหรับ HUD)</summary>
    public IReadOnlyList<AugmentData> GetAcquired() => acquired;

    /// <summary>เลือก augment — เรียกจาก UpgradeManager บน owner</summary>
    public void Acquire(AugmentData a)
    {
        if (a == null) return;
        if (GetStackCount(a) >= a.maxStacks) return;

        ApplyLocal(a);

        // mirror ไป server เพื่อให้สำเนาฝั่ง server มีค่าเดียวกัน
        if (IsOwner && !IsServer)
        {
            int idx = IndexOf(a);
            if (idx >= 0) AcquireServerRpc(idx);
        }
    }

    void ApplyLocal(AugmentData a)
    {
        acquired.Add(a);
        a.OnAcquire(this);

        if (IsOwner) OnAugmentAcquired?.Invoke(a);

        Debug.Log($"[Augment] {(IsServer ? "[Server]" : "[Client]")} ✨ {a.augmentName} ({a.rarity})");
    }

    [Rpc(SendTo.Server)]
    void AcquireServerRpc(int augmentIndex)
    {
        var db = MetaDatabase.Instance;
        if (db == null || augmentIndex < 0 || augmentIndex >= db.augments.Count)
        {
            Debug.LogWarning($"[Augment] server ปฏิเสธ index {augmentIndex} (นอกช่วง)");
            return;
        }

        var a = db.augments[augmentIndex];
        if (a == null || GetStackCount(a) >= a.maxStacks) return;

        ApplyLocal(a);
    }

    static int IndexOf(AugmentData a)
    {
        var db = MetaDatabase.Instance;
        if (db == null) return -1;
        return db.augments.IndexOf(a);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Triggers
    // ═══════════════════════════════════════════════════════════════════════
    /// <summary>เรียกจาก TriggerAugment.OnAcquire</summary>
    public void RegisterTrigger(TriggerAugment def)
    {
        if (def == null) return;
        triggers.Add(new RuntimeTrigger { def = def });
    }

    void HandleAnyEnemyDied()
    {
        for (int i = 0; i < triggers.Count; i++)
        {
            var rt = triggers[i];
            if (rt.def.trigger != AugmentTrigger.EveryNKills) continue;

            rt.progress += 1f;
            if (rt.progress >= rt.def.triggerAmount)
            {
                rt.progress -= rt.def.triggerAmount;
                Fire(rt);
            }
        }
    }

    void HandleLevelChanged(int _)
    {
        for (int i = 0; i < triggers.Count; i++)
            if (triggers[i].def.trigger == AugmentTrigger.OnLevelUp) Fire(triggers[i]);
    }

    void HandleHealthChanged(float prev, float curr)
    {
        if (curr >= prev) return;   // ฟื้น HP ไม่นับ

        for (int i = 0; i < triggers.Count; i++)
            if (triggers[i].def.trigger == AugmentTrigger.OnTakeDamage) Fire(triggers[i]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Effects
    // ═══════════════════════════════════════════════════════════════════════
    void Fire(RuntimeTrigger rt)
    {
        var def = rt.def;

        if (def.internalCooldown > 0f)
        {
            if (Time.time < rt.nextAllowed) return;
            rt.nextAllowed = Time.time + def.internalCooldown;
        }

        switch (def.effect)
        {
            case AugmentEffect.HealFlat:
                RequestHealServerRpc(Mathf.Max(0f, def.magnitude), isPercent: false);
                break;

            case AugmentEffect.HealPercent:
                RequestHealServerRpc(Mathf.Clamp01(def.magnitude), isPercent: true);
                break;

            case AugmentEffect.Shield:
                RequestShieldServerRpc(Mathf.Max(0f, def.magnitude));
                break;

            case AugmentEffect.TempHaste:
                StartCoroutine(TempBuff(v => Stats.tempAbilityHaste += v, def.magnitude, def.duration));
                break;

            case AugmentEffect.TempDamage:
                StartCoroutine(TempBuff(v => Stats.tempDamageBonusMult += v, def.magnitude, def.duration));
                break;

            case AugmentEffect.TempMoveSpeed:
                if (Move != null)
                    StartCoroutine(TempBuff(v => Move.tempMoveSpeedBonus += v, def.magnitude, def.duration));
                break;

            case AugmentEffect.PlayVfx:
                // ไม่มีผลตัวเลข — VFX ด้านล่างจัดการให้
                break;
        }

        if (def.vfxOnTrigger != null && NetworkedVFXPool.Instance != null && NetworkedVFXPool.Instance.vfxDatabase != null)
        {
            int id = NetworkedVFXPool.Instance.vfxDatabase.GetIdForAsset(def.vfxOnTrigger);
            NetworkedVFXPool.Instance.PlayById(id, transform.position);
        }
    }

    /// <summary>บวกค่าเข้า field ชั่วคราว แล้วคืนค่าเดิมเมื่อหมดเวลา (บวก/ลบเป็นคู่เสมอ)</summary>
    IEnumerator TempBuff(System.Action<float> apply, float amount, float duration)
    {
        apply(amount);
        yield return new WaitForSeconds(Mathf.Max(0.1f, duration));
        apply(-amount);
    }

    // ── Server-side effects ───────────────────────────────────────────────
    [Rpc(SendTo.Server)]
    void RequestHealServerRpc(float amount, bool isPercent)
    {
        if (Move == null) return;

        // clamp ฝั่ง server — กัน client ส่งค่าเว่อร์
        if (isPercent) Move.HealPercent(Mathf.Clamp01(amount));
        else           Move.Heal(Mathf.Clamp(amount, 0f, 500f));
    }

    [Rpc(SendTo.Server)]
    void RequestShieldServerRpc(float amount)
    {
        Move?.AddShield(Mathf.Clamp(amount, 0f, 1000f));
    }
}
