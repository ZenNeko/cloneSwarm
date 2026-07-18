using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Support Arena — วาง pillar สร้าง buff zone ให้ผู้เล่นทุกคนที่อยู่ใน zone (เชื่อมโยงกับ SupportArenaPillar component)
///
/// กลไก:
///   • OnFire → Instantiate pillar visual ที่ตำแหน่ง player
///   • Pillar มีวัตถุหรือสคริปต์ SupportArenaPillar คอยตรวจจับฟิสิกส์การชนและจ่ายบัฟ
///   • +20% Damage, +20% Move Speed, +20% Ability Haste (as 20 haste)
///   • ฮีลผู้เล่นที่อยู่ในรัศมีแบบรายวินาที (Tick-based) ฮีลตรงๆ บน Server
///   • Evo: +5% ทุกอย่าง (รวม 25%) + เมื่อ HP เต็ม → เติมโล่ (ปริมาณ = maxHP)
/// </summary>
public class SupportArenaWeapon : WeaponBase
{
    [Header("Support Arena Settings")]
    [Tooltip("Prefab visual ของ pillar (optional — ถ้าไม่ assign ใช้ cylinder fallback)")]
    public GameObject pillarPrefab;

    [Tooltip("อายุ pillar (วินาที) — scale ตาม Duration stat")]
    public float arenaDuration = 10f;

    [Header("Buff Values")]
    public float damageBuff     = 0.20f;    // +20%
    public float moveSpeedBuff  = 0.20f;    // +20%
    public float abilityHaste   = 20f;      // +20 haste

    [Header("Evolution")]
    [Tooltip("เปิด Evo mode")]
    public bool evoEnabled = false;
    [Tooltip("โบนัสเพิ่มเติม (additive %) — เช่น 0.05 = +5% รวมเป็น 25%")]
    public float evoBonusPercent = 0.05f;
    [Tooltip("Evo haste bonus เพิ่มเติม")]
    public float evoHasteBonus   = 5f;
    [Tooltip("Evo HP regen bonus เพิ่มเติม")]
    public float evoRegenBonus   = 2f;
    [Tooltip("Tick interval สำหรับ shield fill เมื่อ HP เต็ม (วินาที)")]
    public float shieldTickInterval = 1f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position;
        SpawnArena(pos, ld);
    }

    void SpawnArena(Vector3 position, WeaponLevelData ld)
    {
        float areaMult = manager.statManager != null ? manager.statManager.GetAreaMultiplier() : 1f;
        float radius = ld.radius * areaMult;

        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        float baseDuration = ld.duration > 0f ? ld.duration : arenaDuration;
        float lifetime     = baseDuration * durationMult;

        // คำนวณปริมาณการฮีล: ดึงมาจาก ld.damage และสเกลตามพลังโจมตีตัวละคร
        float powerMult = manager.statManager != null ? manager.statManager.GetPowerMultiplier() : 1f;
        float finalHealAmount = ld.damage * powerMult;

        // สปอว์นเสาบัฟโซนที่เครื่องตัวเอง
        SpawnLocalArena(position, radius, lifetime, evoEnabled, finalHealAmount);

        // บรอดแคสต์ผ่าน ServerRpc ไปยัง Server พร้อมปริมาณการฮีล
        if (manager != null && manager.IsOwner)
        {
            manager.SpawnSupportArenaServerRpc(position, radius, lifetime, evoEnabled, finalHealAmount);
            
            // แสดง VFX ของอาวุธ (MeteorAoE วงเตือนและระเบิดลงพื้น)
            ShowVfx(ResolveHitVfx("MeteorAoE"), position, radius, isAttackHit: false);
        }
    }

    public void SpawnLocalArena(Vector3 position, float radius, float lifetime, bool evo, float healAmount)
    {
        GameObject go = SpawnLocalArenaPrefab(position, radius);
        if (go == null) return;

        // ดึงสคริปต์ SupportArenaPillar หรือแอดอัตโนมัติหากไม่มีอยู่ใน Prefab
        var pillar = go.GetComponent<SupportArenaPillar>();
        if (pillar == null)
        {
            pillar = go.AddComponent<SupportArenaPillar>();
        }

        float dmgBuff   = damageBuff    + (evo ? evoBonusPercent : 0f);
        float moveBuff  = moveSpeedBuff + (evo ? evoBonusPercent : 0f);
        float hasteBuff = abilityHaste  + (evo ? evoHasteBonus   : 0f);

        // รีเซตค่าพิกัดและเริ่มทำงาน
        pillar.Init(this, dmgBuff, moveBuff, hasteBuff, 0f, evo, shieldTickInterval, radius);
        pillar.SetHealAmount(healAmount);

        // ทำลายตามอายุเสา
        Destroy(go, lifetime);
    }

    public void ApplyBuffs(playermove pm, float dmg, float move, float haste, float regen)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus  += move;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult += dmg;
            sm.tempAbilityHaste   += haste;
        }

        bool isServer = manager != null && manager.IsServer;
        Debug.Log($"[SupportArena] ApplyBuffs to {pm.name} on {(isServer ? "Server" : "Client")}: Speed={pm.tempMoveSpeedBonus}, Damage={sm?.tempDamageBonusMult}");
    }

    public void RemoveBuffs(playermove pm, float dmg, float move, float haste, float regen)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus   -= move;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult -= dmg;
            sm.tempAbilityHaste   -= haste;
        }

        bool isServer = manager != null && manager.IsServer;
        Debug.Log($"[SupportArena] RemoveBuffs from {pm.name} on {(isServer ? "Server" : "Client")}: Speed={pm.tempMoveSpeedBonus}, Damage={sm?.tempDamageBonusMult}");
    }

    GameObject SpawnLocalArenaPrefab(Vector3 position, float radius)
    {
        // 1. สปอว์นตัวเสาหลัก (Physical Pillar) จาก pillarPrefab
        GameObject go = pillarPrefab != null
            ? Instantiate(pillarPrefab, position, Quaternion.identity)
            : null;

        if (go == null)
        {
            return CreateFallbackPillar(position, radius);
        }

        // หารล้างสเกลจากเสาแม่เพื่อให้ขนาดวงแหวนและ VFX แสดงผลได้ตามค่าจริงของ WD เสมอ
        float parentScaleX = go.transform.lossyScale.x;
        float parentScaleY = go.transform.lossyScale.y;
        float parentScaleZ = go.transform.lossyScale.z;
        if (parentScaleX <= 0f) parentScaleX = 1f;
        if (parentScaleY <= 0f) parentScaleY = 1f;
        if (parentScaleZ <= 0f) parentScaleZ = 1f;

        // 2. ดึงพรีแฟบเอฟเฟกต์อาณาเขต (Arena Zone VFX) จาก NetworkedVFXPool ตาม weaponVfxType
        GameObject areaVfxPrefab = null;
        if (NetworkedVFXPool.Instance != null)
        {
            string vfxKey = ResolveHitVfx("None");
            if (!string.IsNullOrEmpty(vfxKey) && vfxKey != "None")
            {
                areaVfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(vfxKey);
            }
        }

        if (areaVfxPrefab != null)
        {
            // สปอว์นเอฟเฟกต์ไดนามิกเป็นลูกของเสา
            var vfxGo = Instantiate(areaVfxPrefab, go.transform);
            vfxGo.transform.localPosition = Vector3.zero;
            vfxGo.transform.localRotation = Quaternion.identity;

            // คำนวณอัตราสเกลของ VFX ตามจริงเทียบกับขนาดออกแบบเดิม (designedRadius) และล้างสเกลแม่
            float designedRad = NetworkedVFXPool.Instance.GetDesignedRadius(ResolveHitVfx("None"));
            float targetWorldScale = designedRad > 0f ? (radius / designedRad) : radius;
            float localScaleVal = targetWorldScale / parentScaleX;
            vfxGo.transform.localScale = new Vector3(localScaleVal, localScaleVal, localScaleVal);

            // สั่งเล่นเอฟเฟกต์ (ค้นหาทั้ง root และ child)
            var vfxGraph = vfxGo.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            if (vfxGraph != null) vfxGraph.Play();
            else
            {
                foreach (var ps in vfxGo.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                }
            }
        }
        else
        {
            // หากไม่ได้เซตหรือไม่มีเอฟเฟกต์ในฐานข้อมูล จะใช้แผ่นวงกลมสีเขียวโปร่งแสงเป็นตัวสำรอง (Fallback)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = Vector3.up * 0.05f;
            
            float scaleX = (radius * 2f) / parentScaleX;
            float scaleZ = (radius * 2f) / parentScaleZ;
            disc.transform.localScale = new Vector3(scaleX, 0.05f / parentScaleY, scaleZ);
            
            var col = disc.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var mr = disc.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 1f, 0.4f, 0.15f);
                
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                
                mr.material = mat;
            }
        }

        return go;
    }

    GameObject CreateFallbackPillar(Vector3 pos, float radius)
    {
        var go = new GameObject("SupportArenaPillar");
        go.transform.position = pos;

        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.transform.SetParent(go.transform, false);
        cyl.transform.localPosition = Vector3.up * 1.5f;
        cyl.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
        var col1 = cyl.GetComponent<Collider>();
        if (col1 != null) Object.Destroy(col1);
        var mr1 = cyl.GetComponent<MeshRenderer>();
        if (mr1 != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 1f, 0.4f);
            mr1.material = mat;
        }

        // ดึงพรีแฟบเอฟเฟกต์อาณาเขตใส่เสาสำรอง
        GameObject areaVfxPrefab = null;
        if (NetworkedVFXPool.Instance != null)
        {
            string vfxKey = ResolveHitVfx("None");
            if (!string.IsNullOrEmpty(vfxKey) && vfxKey != "None")
            {
                areaVfxPrefab = NetworkedVFXPool.Instance.GetVfxPrefab(vfxKey);
            }
        }

        if (areaVfxPrefab != null)
        {
            var vfxGo = Instantiate(areaVfxPrefab, go.transform);
            vfxGo.transform.localPosition = Vector3.zero;
            vfxGo.transform.localRotation = Quaternion.identity;

            float designedRad = NetworkedVFXPool.Instance.GetDesignedRadius(ResolveHitVfx("None"));
            float targetWorldScale = designedRad > 0f ? (radius / designedRad) : radius;
            vfxGo.transform.localScale = new Vector3(targetWorldScale, targetWorldScale, targetWorldScale);

            var vfxGraph = vfxGo.GetComponentInChildren<UnityEngine.VFX.VisualEffect>();
            if (vfxGraph != null) vfxGraph.Play();
            else
            {
                foreach (var ps in vfxGo.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Play();
                }
            }
        }
        else
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = Vector3.up * 0.05f;
            disc.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            var col2 = disc.GetComponent<Collider>();
            if (col2 != null) Object.Destroy(col2);
            var mr2 = disc.GetComponent<MeshRenderer>();
            if (mr2 != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 1f, 0.4f, 0.15f);
                
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                
                mr2.material = mat;
            }
        }

        return go;
    }
}
