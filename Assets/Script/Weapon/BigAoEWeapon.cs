using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Big AoE — AoE ขนาดใหญ่มาก ดาเมจสูง CD สูง ("Nuke")
///
/// กลไก:
///   • OnFire → charge-up visual 0.5s → FireMelee (huge radius + damage)
///   • ศูนย์กลางที่ตำแหน่ง player (RadiantAura-style)
///
/// Level data แนะนำ:
///   Lv1: dmg=200, cd=8.0s, range=8
///   Lv2: dmg=280, cd=7.5s, range=9
///   Lv3: dmg=360, cd=7.0s, range=10
///   Lv4: dmg=450, cd=6.5s, range=11
///   Lv5: dmg=550, cd=6.0s, range=12
/// </summary>
public class BigAoEWeapon : WeaponBase
{
    [Header("Big AoE Settings")]
    [Tooltip("วินาทีหน่วง charge-up ก่อนระเบิด (warning visual)")]
    public float chargeTime = 0.5f;



    private readonly List<GameObject> _activeVisuals = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        float radius = ld.range;
        float dmg    = RollDamage(ld.damage, out bool isCrit);

        StartCoroutine(ChargeAndExplode(radius, dmg, isCrit));
    }

    IEnumerator ChargeAndExplode(float radius, float dmg, bool isCrit)
    {
        Vector3 center = transform.position + Vector3.up * 0.5f;

        // ── Resolve Charge Prefab dynamically ──────────────────────────────
        GameObject resolvedPrefab = null;
        if (NetworkedVFXPool.Instance != null && !string.IsNullOrEmpty(secondaryVfxType) && secondaryVfxType != "None")
        {
            resolvedPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(secondaryVfxType);
        }

        GameObject chargeVisual = resolvedPrefab != null
            ? Instantiate(resolvedPrefab, center, Quaternion.identity, transform)
            : null;

        if (chargeVisual != null) _activeVisuals.Add(chargeVisual);

        // ── Check if the visual uses custom shader with _FillProgress ────────
        Renderer rend = chargeVisual != null ? chargeVisual.GetComponentInChildren<Renderer>() : null;
        Material mat = rend != null ? rend.material : null;
        bool hasFillProgress = mat != null && mat.HasProperty("_FillProgress");

        Vector3 baseScale = chargeVisual != null ? chargeVisual.transform.localScale : Vector3.one;

        // Calculate designed scale factor (fallback to radius * 2f if designedRadius <= 0)
        float designed = NetworkedVFXPool.Instance != null ? NetworkedVFXPool.Instance.GetDesignedRadius(secondaryVfxType) : -1f;
        float scaleFactor = (designed > 0f) ? (radius / designed) : (radius * 2f);

        if (chargeVisual != null && hasFillProgress)
        {
            // Set scale immediately for warning zone style
            chargeVisual.transform.localScale = new Vector3(baseScale.x * scaleFactor, baseScale.y, baseScale.z * scaleFactor);
        }

        float elapsed = 0f;
        while (elapsed < chargeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / chargeTime);
            if (chargeVisual != null)
            {
                chargeVisual.transform.position = transform.position + Vector3.up * 0.5f;

                if (hasFillProgress)
                {
                    mat.SetFloat("_FillProgress", t);
                }
                else
                {
                    // Scale up fallback
                    float scale = Mathf.Lerp(0.1f, scaleFactor, t);
                    chargeVisual.transform.localScale = new Vector3(baseScale.x * scale, baseScale.y, baseScale.z * scale);
                }
            }
            yield return null;
        }

        if (chargeVisual != null)
        {
            _activeVisuals.Remove(chargeVisual);
            Destroy(chargeVisual);
        }

        // ── ตำแหน่งล่าสุดของ player (ไม่ใช่ตอน charge เริ่ม) ─────────────
        center = transform.position + Vector3.up * 0.5f;

        // ── Explode (virtual to support Super subclass overriding) ───────
        Explode(center, radius, dmg, isCrit);
    }

    protected virtual void Explode(Vector3 center, float radius, float dmg, bool isCrit)
    {
        // ── Damage (server-authoritative) ─────────────────────────────────
        FireMelee(center, radius, dmg, isCrit);

        // ── VFX ──────────────────────────────────────────────────────────
        ShowVfx(ResolveHitVfx("MeteorAoE"), center, radius, isCrit, isAttackHit: false);
    }

    void OnDestroy()
    {
        foreach (var go in _activeVisuals)
            if (go != null) Destroy(go);
        _activeVisuals.Clear();
    }
}
