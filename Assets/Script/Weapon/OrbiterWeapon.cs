using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbiter — วน Orb รอบตัวผู้เล่น
/// ใช้สำหรับ Normal (Orbiter) และ Super (Star Ring)
///
/// Cycle:
///   1. ซ่อน orb → รอ summonCooldown วินาที
///   2. ร่าย → orb ปรากฏ activeDuration * DurationStat วินาที
///      → ตรวจ collision ทุก hitCheckInterval (continuous)
///      → enemy แต่ละตัวมี hit cooldown ป้องกัน spam
///   3. ซ่อนอีกครั้ง → วนซ้ำ
///
/// Orb เป็น Local GameObject (ไม่ใช่ NetworkObject) — visual บน Owner
///   → ปลด parent ออกจาก player → orbit angle ไม่ผูกกับ rotation ของผู้เล่น
/// Damage ส่งผ่าน FireMeleeServerRpc ที่ตำแหน่ง orb เมื่อ overlap enemy จริง
///
/// Star Ring Redesign: useTwoRings = true, aoeOnHit removed, 2 concentric rings
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
    public float      rotSpeed = 90f; // องศา/วินาที (คงที่ — ไม่ขึ้นกับ player)

    [Header("Summon Cycle")]
    [Tooltip("วินาทีระหว่างการร่าย orb แต่ละรอบ (นับจากที่ orb ซ่อนจนถึงร่ายครั้งถัดไป)")]
    public float summonCooldown = 8f;
    [Tooltip("วินาทีที่ orb แสดงผลต่อรอบ (สเกลตาม Duration stat)")]
    public float activeDuration = 3f;

    [Header("Damage on Contact")]
    [Tooltip("รัศมีตรวจ enemy รอบ orb (ควรใกล้กับขนาด visual)")]
    public float orbHitRadius = 0.8f;
    [Tooltip("ตรวจ collision ทุกกี่วินาที (ค่าน้อย = ตอบสนองไว แต่ใช้ CPU มากขึ้น)")]
    public float hitCheckInterval = 0.08f;
    [Tooltip("เวลาที่ enemy แต่ละตัวต้องรอก่อนถูก orb เดิมตีอีก (กัน multi-hit ต่อรอบ)")]
    public float perEnemyHitCooldown = 0.5f;

    [Header("Two Rings Settings")]
    [Tooltip("เปิดใช้งาน 2 วงโคจร (สำหรับ Star Ring หรืออาวุธซูเปอร์)")]
    public bool useTwoRings = false;
    [Tooltip("ตัวคูณระยะสำหรับวงนอก (เทียบกับระยะของระดับเลเวลปกติ)")]
    public float outerRadiusMultiplier = 1.5f;
    [Tooltip("ทิศทางและความเร็วในการหมุนของวงนอก (องศา/วินาที)")]
    public float outerRingSpeed = -90f;
    [Tooltip("อัตราส่วนจำนวนลูกแก้ววงนอกต่อวงใน (เช่น 1.0 = จำนวนเท่ากัน)")]
    public float outerOrbRatio = 1.0f;

    // ── State ──────────────────────────────────────────────────────────────
    protected override bool UsesCooldownTimer => false;   // เราจัดการ timer เอง

    private List<Transform>          innerOrbs     = new();
    private List<Transform>          outerOrbs     = new();
    private float                    orbitAngle;
    private float                    outerOrbitAngle;
    private bool                     isActive;
    private float                    summonTimer;
    private float                    activeTimer;
    private float                    hitCheckTimer;
    private float                    orbSyncTimer;
    private const float              OrbSyncInterval = 0.05f;

    // ป้องกัน multi-hit: enemyInstanceID → unscaledTime ที่จะ hit ได้อีก
    private readonly Dictionary<int, float> _hitCooldowns = new();

    // ── Init ───────────────────────────────────────────────────────────────
    protected override void OnInit()
    {
        RebuildOrbs();
        SetOrbsVisible(false);
        Summon(); // แสดง orb ทันทีที่ได้ weapon
    }

    protected override void OnLevelUp()
    {
        RebuildOrbs();
    }

    // OnFire ไม่ใช้ — เราจัดการ damage ผ่าน CheckHits() ทุก frame (continuous)
    protected override void OnFire(WeaponLevelData ld) { }

    // ── Update ─────────────────────────────────────────────────────────────
    protected override void Update()
    {
        if (manager == null || !manager.IsOwner) return;
        if (manager.playerMove != null && manager.playerMove.isDead.Value) return;

        // หมุน orb ด้วยอัตราคงที่ — ไม่ขึ้นกับการหมุน player
        orbitAngle += rotSpeed * Time.deltaTime;
        outerOrbitAngle += outerRingSpeed * Time.deltaTime;
        UpdateOrbPositions();

        if (isActive) TickActive();
        else          TickInactive();
    }

    // ── Active Phase ───────────────────────────────────────────────────────
    void TickActive()
    {
        activeTimer -= Time.deltaTime;
        if (activeTimer <= 0f) { Deactivate(); return; }

        // ตรวจ collision อย่างต่อเนื่อง
        hitCheckTimer += Time.deltaTime;
        if (hitCheckTimer < hitCheckInterval) return;
        hitCheckTimer = 0f;

        CheckHits();
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
        activeTimer  = activeDuration * durationMult;
        hitCheckTimer = 0f;
        isActive     = true;
        _hitCooldowns.Clear();
        SetOrbsVisible(true);
        int totalOrbs = innerOrbs.Count + outerOrbs.Count;
        Debug.Log($"[Orbiter] ✨ Summoned — {totalOrbs} orbs (Inner: {innerOrbs.Count}, Outer: {outerOrbs.Count}), active {activeTimer:F1}s, hitRadius={orbHitRadius}");
    }

    void Deactivate()
    {
        isActive = false;
        SetOrbsVisible(false);
        summonTimer = 0f;
        manager.HideRemoteOrbsServerRpc();
        Debug.Log($"[Orbiter] 💤 Deactivated — next summon in {summonCooldown:F0}s");
    }

    // ── Damage on Contact ─────────────────────────────────────────────────
    void CheckHits()
    {
        var ld         = data.GetLevelData(currentLevel);
        var sm         = manager.statManager;
        float dmgMult  = sm != null ? sm.GetPowerMultiplier() : 1f;
        float areaMult = sm != null ? sm.GetAreaMultiplier() : 1f;
        float baseDmg  = ld.damage * dmgMult;
        float now      = Time.time;
        float radius   = orbHitRadius * areaMult;

        // ลบ entry ที่หมดอายุ (กัน dictionary โต)
        if (_hitCooldowns.Count > 32)
            CleanupCooldowns(now);

        // Check hits for inner orbs
        foreach (var orb in innerOrbs)
        {
            if (orb == null || !orb.gameObject.activeSelf) continue;
            CheckOrbCollision(orb, radius, baseDmg, now);
        }

        // Check hits for outer orbs
        foreach (var orb in outerOrbs)
        {
            if (orb == null || !orb.gameObject.activeSelf) continue;
            CheckOrbCollision(orb, radius, baseDmg, now);
        }
    }

    private void CheckOrbCollision(Transform orb, float radius, float baseDmg, float now)
    {
        var hits = Physics.OverlapSphere(orb.position, radius);
        if (hits.Length == 0) return;

        foreach (var c in hits)
        {
            if (!c.CompareTag("Enemy")) continue;
            var e = c.GetComponent<Enemy>();
            if (e == null) continue;

            int id = e.GetInstanceID();
            if (_hitCooldowns.TryGetValue(id, out float nextHitAt) && now < nextHitAt) continue;
            _hitCooldowns[id] = now + perEnemyHitCooldown;

            float dmg = RollDamage(baseDmg, out bool isCrit);

            // ตีเฉพาะตัวที่ชน — No more AoE explode
            FireMelee(e.transform.position, 0.5f, dmg, isCrit);
            Debug.Log($"[Orbiter] 💥 Hit enemy {e.name} — dmg {dmg:F0}");
        }
    }

    void CleanupCooldowns(float now)
    {
        var stale = new List<int>();
        foreach (var kv in _hitCooldowns)
            if (kv.Value < now) stale.Add(kv.Key);
        foreach (var id in stale) _hitCooldowns.Remove(id);
    }

    // ── Orb Position (world-space, ไม่ผูก parent rotation) ────────────────
    void UpdateOrbPositions()
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        float r_inner = data.GetLevelData(currentLevel).range * areaMult;
        float r_outer = r_inner * outerRadiusMultiplier;

        // Position inner orbs
        for (int i = 0; i < innerOrbs.Count; i++)
        {
            if (innerOrbs[i] == null) continue;
            float a   = orbitAngle + (360f / innerOrbs.Count) * i;
            float rad = Mathf.Deg2Rad * a;
            innerOrbs[i].position = center + new Vector3(Mathf.Cos(rad) * r_inner, 0f, Mathf.Sin(rad) * r_inner);
            innerOrbs[i].localScale = Vector3.one * (orbSize * areaMult);
        }

        // Position outer orbs
        for (int i = 0; i < outerOrbs.Count; i++)
        {
            if (outerOrbs[i] == null) continue;
            float a   = outerOrbitAngle + (360f / outerOrbs.Count) * i;
            float rad = Mathf.Deg2Rad * a;
            outerOrbs[i].position = center + new Vector3(Mathf.Cos(rad) * r_outer, 0f, Mathf.Sin(rad) * r_outer);
            outerOrbs[i].localScale = Vector3.one * (orbSize * areaMult);
        }

        // Sync ตำแหน่ง world-space ไปยัง client อื่น (เฉพาะตอน active)
        if (!isActive) return;
        orbSyncTimer += Time.deltaTime;
        if (orbSyncTimer < OrbSyncInterval) return;
        orbSyncTimer = 0f;

        var positions = new Vector3[innerOrbs.Count + outerOrbs.Count];
        int idx = 0;
        for (int i = 0; i < innerOrbs.Count; i++)
            positions[idx++] = innerOrbs[i] != null ? innerOrbs[i].position : Vector3.zero;
        for (int i = 0; i < outerOrbs.Count; i++)
            positions[idx++] = outerOrbs[i] != null ? outerOrbs[i].position : Vector3.zero;

        manager.SyncOrbPositionsServerRpc(positions);
    }

    private void RebuildOrbs()
    {
        int targetInnerCount = data.GetLevelData(currentLevel).projectileCount;
        int targetOuterCount = useTwoRings ? Mathf.RoundToInt(targetInnerCount * outerOrbRatio) : 0;

        // 1. Maintain inner orbs list
        while (innerOrbs.Count < targetInnerCount)
        {
            var go = CreateOrbInstance();
            if (go != null) innerOrbs.Add(go.transform);
            else break;
        }
        while (innerOrbs.Count > targetInnerCount)
        {
            int last = innerOrbs.Count - 1;
            if (innerOrbs[last] != null) Destroy(innerOrbs[last].gameObject);
            innerOrbs.RemoveAt(last);
        }

        // 2. Maintain outer orbs list
        while (outerOrbs.Count < targetOuterCount)
        {
            var go = CreateOrbInstance();
            if (go != null) outerOrbs.Add(go.transform);
            else break;
        }
        while (outerOrbs.Count > targetOuterCount)
        {
            int last = outerOrbs.Count - 1;
            if (outerOrbs[last] != null) Destroy(outerOrbs[last].gameObject);
            outerOrbs.RemoveAt(last);
        }
    }

    private GameObject CreateOrbInstance()
    {
        if (orbPrefab == null)
        {
            Debug.LogError($"[OrbiterWeapon] orbPrefab ไม่ถูก assign บน weapon prefab — orb จะไม่ปรากฏ");
            return null;
        }

        // parent = null → ไม่รับการหมุนของ player
        var go = Instantiate(orbPrefab);
        go.transform.localScale = Vector3.one * orbSize;
        go.SetActive(false);

        // Strip components
        var no = go.GetComponent<Unity.Netcode.NetworkObject>();
        if (no != null) Destroy(no);
        var proj = go.GetComponent<Projectile>();
        if (proj != null) Destroy(proj);
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        return go;
    }

    void SetOrbsVisible(bool visible)
    {
        foreach (var orb in innerOrbs)
            if (orb != null) orb.gameObject.SetActive(visible);
        foreach (var orb in outerOrbs)
            if (orb != null) orb.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        foreach (var orb in innerOrbs)
            if (orb != null) Destroy(orb.gameObject);
        foreach (var orb in outerOrbs)
            if (orb != null) Destroy(orb.gameObject);

        innerOrbs.Clear();
        outerOrbs.Clear();

        if (manager != null)
        {
            manager.HideRemoteOrbsServerRpc();
        }
    }
}
