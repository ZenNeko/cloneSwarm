using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbiter — วน Orb รอบตัวผู้เล่น
/// ใช้สำหรับ Normal (Orbiter) และ Super (Star Ring)
///
/// Cycle:
///   1. ซ่อน orb → รอ summonCooldown วินาที
///   2. ร่าย → orb ปรากฏ activeDuration * DurationStat วินาที + ส่ง damage ทุก level.cooldown
///   3. ซ่อนอีกครั้ง → วนซ้ำ
///
/// Orb เป็น Local GameObject (ไม่ใช่ NetworkObject) — visual only บน Owner
/// Damage ส่งผ่าน FireMeleeServerRpc ที่ตำแหน่ง orb ทุก cooldown
///
/// StarRing ต่างกัน: orbCount มากขึ้น, aoeOnHit = true
///
/// Level data แนะนำ:
///   Lv1: dmg=18, cd=1.5s, count=2, range=3 (orbit radius)
///   Lv2: dmg=22, cd=1.4s, count=2, range=3
///   Lv3: dmg=28, cd=1.3s, count=3, range=3.5
///   Lv4: dmg=34, cd=1.2s, count=3, range=4
///   Lv5: dmg=42, cd=1.0s, count=4, range=4.5
/// </summary>
public class OrbiterWeapon : WeaponBase
{
    [Header("Orb Visual")]
    public GameObject orbPrefab;      // Simple sphere mesh (no NetworkObject)
    public float      orbSize  = 0.4f;
    public float      rotSpeed = 90f; // องศา/วินาที

    [Header("Summon Cycle")]
    [Tooltip("วินาทีระหว่างการร่าย orb แต่ละรอบ (นับจากที่ orb ซ่อนจนถึงร่ายครั้งถัดไป)")]
    public float summonCooldown = 8f;
    [Tooltip("วินาทีที่ orb แสดงผลต่อรอบ (สเกลตาม Duration stat)")]
    public float activeDuration = 3f;

    [Header("Star Ring Super")]
    public bool  aoeOnHit  = false;
    public float aoeRadius = 1.5f;   // radius ของ AoE เมื่อ orb ชนศัตรู

    // ── State ──────────────────────────────────────────────────────────────
    protected override bool UsesCooldownTimer => false;   // เราจัดการ timer เอง

    private List<Transform> orbs        = new();
    private float           orbitAngle;
    private bool            isActive;      // orb กำลังแสดงผลอยู่
    private float           summonTimer;   // นับขึ้นสู่ summonCooldown (inactive phase)
    private float           activeTimer;   // นับลง — เวลาที่เหลือขณะ active
    private float           damageTimer;   // damage tick ขณะ active
    private float           orbSyncTimer;  // sync orb positions ไปยัง non-owner clients
    private const float     OrbSyncInterval = 0.05f;

    // ── Init ───────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        SpawnOrbs(data.GetLevelData(currentLevel).projectileCount);
        SetOrbsVisible(false);   // เริ่มซ่อน — รอ summonCooldown แรก
    }

    protected override void OnLevelUp()
    {
        int needed = data.GetLevelData(currentLevel).projectileCount;
        if (orbs.Count < needed) SpawnOrbs(needed - orbs.Count);
    }

    // ── Update (override ถูกต้อง — ไม่ hide base) ─────────────────────────
    protected override void Update()
    {
        // base.Update() ไม่ต้องเรียก — UsesCooldownTimer=false ทำให้ base ออกเร็ว
        // แต่ต้องการ ownership + alive check เหมือน base
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        // Rotate orbs every frame (visual)
        orbitAngle += rotSpeed * Time.deltaTime;
        UpdateOrbPositions();

        if (isActive)
            TickActive();
        else
            TickInactive();
    }

    // ── Active Phase ───────────────────────────────────────────────────────
    void TickActive()
    {
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f) { Deactivate(); return; }

        // Damage tick
        var   ld = data.GetLevelData(currentLevel);
        float cd = ld.cooldown * (manager.statManager != null ? manager.statManager.GetCooldownMultiplier() : 1f);

        damageTimer += Time.deltaTime;
        if (damageTimer >= cd)
        {
            damageTimer = 0f;
            var effective = BuildEffectiveLevelData(ld);
            OnFire(effective);
        }
    }

    // ── Inactive Phase ─────────────────────────────────────────────────────
    void TickInactive()
    {
        summonTimer += Time.deltaTime;
        if (summonTimer >= summonCooldown)
        {
            summonTimer = 0f;
            Summon();
        }
    }

    void Summon()
    {
        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        activeTimer = activeDuration * durationMult;
        damageTimer = 0f;
        isActive    = true;
        SetOrbsVisible(true);
        Debug.Log($"[Orbiter] ✨ Summoned — active {activeTimer:F1}s");
    }

    void Deactivate()
    {
        isActive = false;
        SetOrbsVisible(false);
        summonTimer = 0f;
        manager.HideRemoteOrbsServerRpc();
        Debug.Log($"[Orbiter] 💤 Deactivated — next summon in {summonCooldown:F0}s");
    }

    // ── Damage ─────────────────────────────────────────────────────────────
    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = aoeOnHit ? aoeRadius : 0.6f;

        foreach (var orb in orbs)
        {
            if (orb == null) continue;
            manager.FireMeleeServerRpc(orb.position, radius, dmg);
            ShowBaseHitVfx(orb.position, isCrit);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    void UpdateOrbPositions()
    {
        float r = data.GetLevelData(currentLevel).range;
        for (int i = 0; i < orbs.Count; i++)
        {
            if (orbs[i] == null) continue;
            float a = orbitAngle + (360f / orbs.Count) * i;
            float rad = Mathf.Deg2Rad * a;
            orbs[i].localPosition = new Vector3(Mathf.Cos(rad) * r, 0.5f, Mathf.Sin(rad) * r);
        }

        // Sync ตำแหน่ง world-space ไปยัง client อื่น (เฉพาะตอน active)
        if (!isActive) return;
        orbSyncTimer += Time.deltaTime;
        if (orbSyncTimer < OrbSyncInterval) return;
        orbSyncTimer = 0f;

        var positions = new Vector3[orbs.Count];
        for (int i = 0; i < orbs.Count; i++)
            positions[i] = orbs[i] != null ? orbs[i].position : Vector3.zero;
        manager.SyncOrbPositionsServerRpc(positions);
    }

    void SpawnOrbs(int count)
    {
        if (orbPrefab == null) return;
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(orbPrefab, transform);
            go.transform.localScale = Vector3.one * orbSize;
            go.SetActive(false);   // เริ่มซ่อน
            orbs.Add(go.transform);
        }
    }

    void SetOrbsVisible(bool visible)
    {
        foreach (var orb in orbs)
            if (orb != null) orb.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        foreach (var o in orbs)
            if (o != null) Destroy(o.gameObject);
        manager?.HideRemoteOrbsServerRpc();
    }
}
