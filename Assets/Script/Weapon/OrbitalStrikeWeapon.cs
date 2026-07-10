using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbital Strike — สุ่มยิง AoE จากฟ้าใส่ตำแหน่ง enemy
///
/// กลไก:
///   • OnFire → หา enemy ใน range → สุ่มเลือก N ตำแหน่ง
///   • แต่ละจุด: แสดง warning indicator → หน่วง delay → FireMelee AoE
///   • ถ้าไม่มี enemy → สุ่มตำแหน่งรอบ player
///
/// Level data แนะนำ:
///   Lv1: dmg=40,  cd=3.0s, count=2, range=12
///   Lv2: dmg=50,  cd=2.8s, count=3, range=13
///   Lv3: dmg=60,  cd=2.5s, count=3, range=14
///   Lv4: dmg=75,  cd=2.3s, count=4, range=15
///   Lv5: dmg=90,  cd=2.0s, count=5, range=16
/// </summary>
public class OrbitalStrikeWeapon : WeaponBase
{
    [Header("Orbital Strike Settings")]
    [Tooltip("รัศมีระเบิดแต่ละจุด (scale ตาม AreaSize stat)")]
    public float explosionRadius = 3.5f;

    [Tooltip("วินาทีหน่วงก่อนระเบิด (warning indicator time)")]
    public float strikeDelay = 0.6f;

    [Tooltip("Prefab visual ของ warning circle (optional — ถ้าไม่ assign ใช้ cylinder สีแดง)")]
    public GameObject warningPrefab;

    private readonly List<GameObject> _activeVisuals = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);

        // หาตำแหน่ง enemy ทั้งหมดใน range
        var enemies = FindAllEnemiesInRange(ld.range);

        for (int i = 0; i < count; i++)
        {
            Vector3 targetPos;
            if (enemies.Length > 0)
            {
                // สุ่มเลือก enemy + offset เล็กน้อย
                var    picked = enemies[Random.Range(0, enemies.Length)];
                Vector2 jitter = Random.insideUnitCircle * 1.5f;
                targetPos = picked.transform.position + new Vector3(jitter.x, 0f, jitter.y);
            }
            else
            {
                // ไม่มี enemy → สุ่มรอบ player
                Vector2 rnd = Random.insideUnitCircle.normalized * (ld.range * Random.Range(0.3f, 0.8f));
                targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            }

            StartCoroutine(StrikeCoroutine(targetPos, dmg, isCrit, radius));
        }
    }

    IEnumerator StrikeCoroutine(Vector3 pos, float dmg, bool isCrit, float radius)
    {
        // ── Warning Indicator ─────────────────────────────────────────────
        GameObject warning = warningPrefab != null
            ? Instantiate(warningPrefab, pos, Quaternion.identity)
            : CreateFallbackWarning(pos, radius);

        if (warning != null) _activeVisuals.Add(warning);

        yield return new WaitForSeconds(strikeDelay);

        if (warning != null)
        {
            _activeVisuals.Remove(warning);
            Destroy(warning);
        }

        // ── Damage (server-authoritative) ─────────────────────────────────
        FireMelee(pos + Vector3.up * 0.5f, radius, dmg, isCrit);

        // ── VFX ──────────────────────────────────────────────────────────
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), pos, radius, isCrit, isAttackHit: false);
    }

    void OnDestroy()
    {
        foreach (var go in _activeVisuals)
            if (go != null) Destroy(go);
        _activeVisuals.Clear();
    }

    static GameObject CreateFallbackWarning(Vector3 pos, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.position   = pos + Vector3.up * 0.02f;
        go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.2f, 0.1f, 0.4f);
            mr.material = mat;
        }
        return go;
    }
}
