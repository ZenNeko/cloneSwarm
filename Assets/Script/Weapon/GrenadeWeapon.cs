using System.Collections;
using UnityEngine;

/// <summary>
/// Grenade — โยน grenade ไปสุ่มตำแหน่งรอบ player → ระเบิด AoE
/// ใช้ local coroutine + visual GameObject (ไม่ต้องการ NetworkObject prefab)
/// Damage ผ่าน FireMeleeServerRpc เมื่อถึง fuseTime
/// </summary>
public class GrenadeWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิด (scale ตาม AreaSize stat)")]
    public float explosionRadius = 3f;
    [Tooltip("เวลาบินก่อนระเบิด (วินาที)")]
    public float fuseTime        = 1.5f;
    [Tooltip("Prefab visual ของ grenade (optional — ถ้าไม่ assign ใช้ sphere สีเหลือง)")]
    public GameObject grenadePrefab;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage, out bool isCrit);
        float radius = explosionRadius;
        if (manager.statManager != null)
            dmg    *= manager.statManager.GetPowerMultiplier();
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        int count = Mathf.Max(1, ld.projectileCount);
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Vector2 rnd       = Random.insideUnitCircle.normalized * (ld.range * Random.Range(0.4f, 1f));
            Vector3 targetPos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            StartCoroutine(ThrowGrenade(spawnPos, targetPos, dmg, isCrit, radius));
        }
    }

    IEnumerator ThrowGrenade(Vector3 from, Vector3 to, float dmg, bool isCrit, float radius)
    {
        // ── Visual ────────────────────────────────────────────────────────
        GameObject visual = grenadePrefab != null
            ? Instantiate(grenadePrefab, from, Quaternion.identity)
            : CreateFallbackVisual(from);

        float elapsed    = 0f;
        float arcHeight  = 1.8f;

        while (elapsed < fuseTime)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / fuseTime);

            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y += arcHeight * Mathf.Sin(t * Mathf.PI);

            if (visual != null) visual.transform.position = pos;
            yield return null;
        }

        if (visual != null) Destroy(visual);

        // ── Damage (server-authoritative) ─────────────────────────────────
        manager.FireMeleeServerRpc(to, radius, dmg, isCrit);

        // ── VFX ──────────────────────────────────────────────────────────
        ShowVfx(VFXType.GrenadeExplosion, to, radius, isAttackHit: false);
    }

    static GameObject CreateFallbackVisual(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * 0.3f;

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(1f, 0.85f, 0.1f);
            mr.material = mat;
        }
        return go;
    }
}
