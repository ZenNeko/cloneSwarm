using System.Collections;
using UnityEngine;

/// <summary>
/// Grenade â€” à¹‚à¸¢à¸™ grenade à¹„à¸›à¸ªà¸¸à¹ˆà¸¡à¸•à¸³à¹à¸«à¸™à¹ˆà¸‡à¸£à¸­à¸š player â†’ à¸£à¸°à¹€à¸šà¸´à¸” AoE
/// à¹ƒà¸Šà¹‰ local coroutine + visual GameObject (à¹„à¸¡à¹ˆà¸•à¹‰à¸­à¸‡à¸à¸²à¸£ NetworkObject prefab)
/// Damage à¸œà¹ˆà¸²à¸™ FireMeleeServerRpc à¹€à¸¡à¸·à¹ˆà¸­à¸–à¸¶à¸‡ fuseTime
/// </summary>
public class GrenadeWeapon : WeaponBase
{
    [Tooltip("à¸£à¸±à¸¨à¸¡à¸µà¸£à¸°à¹€à¸šà¸´à¸” (scale à¸•à¸²à¸¡ AreaSize stat)")]
    public float explosionRadius = 3f;
    [Tooltip("à¹€à¸§à¸¥à¸²à¸šà¸´à¸™à¸à¹ˆà¸­à¸™à¸£à¸°à¹€à¸šà¸´à¸” (à¸§à¸´à¸™à¸²à¸—à¸µ)")]
    public float fuseTime        = 1.5f;
    [Tooltip("Prefab visual à¸‚à¸­à¸‡ grenade (optional â€” à¸–à¹‰à¸²à¹„à¸¡à¹ˆ assign à¹ƒà¸Šà¹‰ sphere à¸ªà¸µà¹€à¸«à¸¥à¸·à¸­à¸‡)")]
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
        // â”€â”€ Visual â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€ Damage (server-authoritative) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        FireMelee(to, radius, dmg, isCrit);

        // â”€â”€ VFX â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        ShowVfx(ResolveHitVfx("GrenadeExplosion"), to, radius, isAttackHit: false);
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
