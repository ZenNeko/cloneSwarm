using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Magic Missile — Homing Missile ที่ Slow enemy + Evo: โอกาส Freeze
///
/// กลไก:
///   • OnFire → สร้าง homing missiles (local visual + coroutine)
///   • Missile ติดตาม enemy ใกล้สุด → เมื่อถึงเป้า: FireMelee + ApplySlowDebuff
///   • Evo: เมื่อ hit → roll chance → ApplyFreeze
///
/// Level data แนะนำ:
///   Lv1: dmg=25,  cd=2.0s, count=2, range=10, speed=12
///   Lv2: dmg=30,  cd=1.8s, count=2, range=11, speed=13
///   Lv3: dmg=35,  cd=1.6s, count=3, range=12, speed=14
///   Lv4: dmg=45,  cd=1.4s, count=3, range=13, speed=15
///   Lv5: dmg=55,  cd=1.2s, count=4, range=14, speed=16
/// </summary>
public class MagicMissileWeapon : WeaponBase
{
    [Header("Magic Missile Settings")]
    [Tooltip("Prefab visual ของ missile (optional — ถ้าไม่ assign ใช้ sphere สีม่วง)")]
    public GameObject missilePrefab;

    [Tooltip("รัศมี AoE เมื่อ missile กระทบ")]
    public float impactRadius = 1.5f;

    [Header("Slow Debuff")]
    [Tooltip("ลดความเร็ว enemy กี่ % (0.5 = ลดเหลือ 50%)")]
    public float slowPercent = 0.5f;
    [Tooltip("Slow duration (วินาที)")]
    public float slowDuration = 2f;

    [Header("Evolution — Freeze")]
    [Tooltip("เปิด Freeze chance (evo)")]
    public bool evoEnabled = false;
    [Tooltip("โอกาส freeze per hit (0–1)")]
    [Range(0f, 1f)]
    public float freezeChance = 0.15f;
    [Tooltip("Freeze duration (วินาที)")]
    public float freezeDuration = 1.5f;

    private readonly List<GameObject> _activeVisuals = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        int count = Mathf.Max(1, ld.projectileCount);
        float dmg = RollDamage(ld.damage, out bool isCrit);

        for (int i = 0; i < count; i++)
        {
            // หา enemy target
            Transform target = FindNearestEnemy(ld.range);
            Vector3  spawnPos = transform.position + Vector3.up * 0.8f;

            // สุ่ม spawn offset เล็กน้อย (ไม่ spawn ทับกัน)
            Vector2 offset = Random.insideUnitCircle * 0.5f;
            spawnPos += new Vector3(offset.x, 0f, offset.y);

            StartCoroutine(MissileCoroutine(spawnPos, target, dmg, ld.projectileSpeed, ld.range, isCrit));
        }
    }

    IEnumerator MissileCoroutine(Vector3 startPos, Transform target, float dmg, float speed, float maxRange, bool isCrit)
    {
        // ── Visual ────────────────────────────────────────────────────────
        GameObject visual = missilePrefab != null
            ? Instantiate(missilePrefab, startPos, Quaternion.identity)
            : CreateFallbackMissile(startPos);

        if (visual != null) _activeVisuals.Add(visual);

        float effectiveSpeed = speed > 0f ? speed : 12f;
        float travelDist     = 0f;
        Vector3 currentPos   = startPos;

        while (travelDist < maxRange)
        {
            // Re-acquire target ถ้า target ตายหรือหายไป
            if (target == null)
            {
                target = FindNearestEnemy(maxRange);
                if (target == null)
                {
                    // ไม่มี target → บินตรงต่อไป
                    Vector3 fwd = visual != null ? visual.transform.forward : Vector3.forward;
                    currentPos += fwd * effectiveSpeed * Time.deltaTime;
                    travelDist += effectiveSpeed * Time.deltaTime;
                    if (visual != null) visual.transform.position = currentPos;
                    yield return null;
                    continue;
                }
            }

            // Homing: บินเข้าหา target
            Vector3 targetPos = target.position + Vector3.up * 0.5f;
            Vector3 dir       = targetPos - currentPos;
            float   step      = effectiveSpeed * Time.deltaTime;

            if (dir.magnitude <= step + 0.2f)
            {
                // Hit target
                currentPos = targetPos;
                if (visual != null) visual.transform.position = currentPos;
                break;
            }

            currentPos += dir.normalized * step;
            travelDist += step;

            if (visual != null)
            {
                visual.transform.position = currentPos;
                visual.transform.forward  = dir.normalized;
            }
            yield return null;
        }

        if (visual != null)
        {
            _activeVisuals.Remove(visual);
            Destroy(visual);
        }

        // ── Damage (server-authoritative) ─────────────────────────────────
        FireMelee(currentPos, impactRadius, dmg, isCrit);

        // ── Slow Debuff ──────────────────────────────────────────────────
        manager.ApplySlowToEnemiesServerRpc(currentPos, impactRadius, slowDuration, slowPercent);

        // ── Evo: Freeze chance ───────────────────────────────────────────
        if (evoEnabled && Random.value < freezeChance)
        {
            manager.ApplyFreezeToEnemiesServerRpc(currentPos, impactRadius, freezeDuration);
        }

        // ── VFX ──────────────────────────────────────────────────────────
        ShowVfx(ResolveHitVfx("HitEffect"), currentPos, impactRadius, isCrit, isAttackHit: false);
        PlayHitSfx(currentPos);
    }

    void OnDestroy()
    {
        foreach (var go in _activeVisuals)
            if (go != null) Destroy(go);
        _activeVisuals.Clear();
    }

    static GameObject CreateFallbackMissile(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.25f;

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.6f, 0.2f, 1f); // purple
            mr.material = mat;
        }
        return go;
    }
}
