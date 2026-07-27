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
    [Tooltip("วินาทีหน่วงก่อนระเบิด (warning indicator time)")]
    public float strikeDelay = 0.6f;



    protected readonly List<GameObject> _activeVisuals = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;

        // 1. ดึงรัศมีวงเตือนภัย (ถ้ากำหนดใน WeaponData จะใช้ค่าจาก WeaponData หากไม่มีจึงใช้ designedRadius จากคลัง VFX)
        float wRadius;
        if (ld.radius > 0f)
        {
            wRadius = ld.radius * areaMult;
        }
        else
        {
            string warningVfxKey = !string.IsNullOrEmpty(secondaryVfxType) && secondaryVfxType != "None" ? secondaryVfxType : "";
            float baseWarningRadius = 3.5f;
            if (NetworkedVFXPool.Instance != null && !string.IsNullOrEmpty(warningVfxKey))
            {
                float d = NetworkedVFXPool.Instance.GetDesignedRadius(warningVfxKey);
                if (d > 0f) baseWarningRadius = d;
            }
            wRadius = baseWarningRadius * areaMult;
        }

        // 2. ดึงรัศมีระเบิดจาก VFX Database
        string explosionVfxKey = ResolveHitVfx("GrenadeExplosion");
        float baseExplosionRadius = 3.5f;
        if (NetworkedVFXPool.Instance != null && !string.IsNullOrEmpty(explosionVfxKey))
        {
            float d = NetworkedVFXPool.Instance.GetDesignedRadius(explosionVfxKey);
            if (d > 0f) baseExplosionRadius = d;
        }
        float eRadius = baseExplosionRadius * areaMult;

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

            StartCoroutine(StrikeCoroutine(targetPos, dmg, isCrit, wRadius, eRadius));
        }
    }

    private readonly List<GameObject> _localWarningsOnly = new();

    protected virtual IEnumerator StrikeCoroutine(Vector3 pos, float dmg, bool isCrit, float wRadius, float eRadius)
    {
        // ── Broadcast Warning Indicator to other clients ──────────────────
        if (manager != null)
        {
            manager.SpawnOrbitalWarningClientRpc(pos, wRadius, strikeDelay);
        }

        // ── Warning Indicator (Local for Owner) ───────────────────────────
        GameObject warning = SpawnWarningObject(pos, wRadius);

        if (warning != null) _activeVisuals.Add(warning);

        // อนิเมชันจำลองการชาร์จแบบเดียวกับ BigAoE
        Coroutine anim = StartCoroutine(AnimateWarning(warning, wRadius, strikeDelay));

        yield return new WaitForSeconds(strikeDelay);

        if (anim != null) StopCoroutine(anim);

        if (warning != null)
        {
            _activeVisuals.Remove(warning);
            Destroy(warning);
        }

        // ── Damage (server-authoritative) ─────────────────────────────────
        FireMelee(pos + Vector3.up * 0.5f, eRadius, dmg, isCrit);

        // ── VFX ──────────────────────────────────────────────────────────
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), pos, eRadius, isCrit, isAttackHit: false);
    }

    public void SpawnLocalWarningVisualOnly(Vector3 position, float radius, float duration)
    {
        GameObject warning = SpawnWarningObject(position, radius);

        if (warning != null)
        {
            _localWarningsOnly.Add(warning);
            StartCoroutine(AnimateAndDestroyLocalWarning(warning, radius, duration));
        }
    }

    IEnumerator AnimateAndDestroyLocalWarning(GameObject warning, float radius, float duration)
    {
        yield return StartCoroutine(AnimateWarning(warning, radius, duration));
        if (warning != null)
        {
            _localWarningsOnly.Remove(warning);
            Destroy(warning);
        }
    }

    protected GameObject SpawnWarningObject(Vector3 pos, float radius)
    {
        GameObject resolvedPrefab = null;
        if (NetworkedVFXPool.Instance != null && !string.IsNullOrEmpty(secondaryVfxType) && secondaryVfxType != "None")
        {
            resolvedPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(secondaryVfxType);
        }

        if (resolvedPrefab != null)
        {
            return Instantiate(resolvedPrefab, pos, Quaternion.identity);
        }
        else
        {
            return CreateFallbackWarning(pos, radius);
        }
    }

    protected IEnumerator AnimateWarning(GameObject warning, float radius, float duration)
    {
        if (warning == null) yield break;

        // ── Check if the visual uses custom shader with _FillProgress ────────
        Renderer rend = warning.GetComponentInChildren<Renderer>();
        Material mat = rend != null ? rend.material : null;
        bool hasFillProgress = mat != null && mat.HasProperty("_FillProgress");

        Vector3 baseScale = warning.transform.localScale;

        // Calculate designed scale factor (fallback to radius * 2f if designedRadius <= 0)
        float designed = -1f;
        if (NetworkedVFXPool.Instance != null && !string.IsNullOrEmpty(secondaryVfxType) && secondaryVfxType != "None")
        {
            designed = NetworkedVFXPool.Instance.GetDesignedRadius(secondaryVfxType);
        }

        float scaleFactor = (designed > 0f) ? (radius / designed) : (radius * 2f);

        if (hasFillProgress)
        {
            // Set scale immediately for warning zone style
            warning.transform.localScale = new Vector3(baseScale.x * scaleFactor, baseScale.y, baseScale.z * scaleFactor);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (warning == null) yield break;

            if (hasFillProgress)
            {
                mat.SetFloat("_FillProgress", t);
            }
            else
            {
                // Scale up fallback
                float scale = Mathf.Lerp(0.1f, scaleFactor, t);
                warning.transform.localScale = new Vector3(baseScale.x * scale, baseScale.y, baseScale.z * scale);
            }
            yield return null;
        }
    }

    void OnDestroy()
    {
        foreach (var go in _activeVisuals)
            if (go != null) Destroy(go);
        _activeVisuals.Clear();

        foreach (var go in _localWarningsOnly)
            if (go != null) Destroy(go);
        _localWarningsOnly.Clear();
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
