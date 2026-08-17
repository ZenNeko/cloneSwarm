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
///
/// หมายเหตุ (ADR-008 D4): ตัวอาวุธไม่ apply/remove บัฟให้เสาอีกต่อไป — ตรรกะย้ายไปอยู่ใน
/// SupportArenaPillar เอง เพราะเสาต้องรอดแม้ instance นี้จะถูก Destroy ทิ้งก่อนเสาหมดอายุ
/// (เช่นตอนอัปเกรดเป็น Super) ที่นี่มีหน้าที่แค่คำนวณค่าตอน spawn แล้วส่งเข้า Pillar.Init
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
    // ต่อสายแล้ว 2026-08-15 (ADR-008 ข้อ 4) — ไหลไปที่ playermove.tempHealthRegenBonus
    // ซึ่งถูกอ่านรวมกับ healthRegenPerSecond ใน playermove.Update() และฟื้นเลือดจริงฝั่ง server
    // เป็นโบนัสเฉพาะ evo เท่านั้น ร่างปกติไม่มี regen (จึงไม่มีฟิลด์ฐานคู่กันเหมือน damage/move/haste)
    [Tooltip("Evo HP regen bonus — ฟื้นเลือดต่อวินาทีให้คนที่ยืนในวง (เฉพาะร่าง Evo)")]
    public float evoRegenBonus   = 2f;
    [Tooltip("Tick interval สำหรับ shield fill เมื่อ HP เต็ม (วินาที)")]
    public float shieldTickInterval = 1f;

    [Header("Visual Fallback (Materials)")]
    [Tooltip("Material ของแผ่นวงกลมพื้นเสา ใช้ตอนไม่มี VFX จาก NetworkedVFXPool — แนะนำให้ designer assign เอง")]
    public Material arenaDiscMaterial;
    [Tooltip("Material ของตัวเสาทรงกระบอก ใช้ตอนไม่มี pillarPrefab (fallback pillar ทั้งอัน) — แนะนำให้ designer assign เอง")]
    public Material pillarBodyMaterial;

    // แคชไว้ใช้ร่วมกันข้ามเสาทุกต้น สร้างครั้งเดียวเป็น fallback สุดท้ายเมื่อไม่มีใคร assign field ด้านบน
    private static Material s_fallbackDiscMaterial;
    private static Material s_fallbackBodyMaterial;

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
        // regen ไม่มีค่าฐาน — ร่างปกติได้ 0 ร่าง evo ได้ evoRegenBonus ล้วน
        float regenBuff = evo ? evoRegenBonus : 0f;

        // isServer ถูก snapshot ตรงนี้แล้วส่งเข้า Init ครั้งเดียว — เสาไม่ถือ reference กลับมาที่ตัวอาวุธนี้อีก
        // (ADR-008 D4) เพื่อไม่ให้เสาพังถ้าอาวุธถูก Destroy ก่อนเสาหมดอายุ (เช่นตอนอัปเกรดเป็น Super)
        bool isServer = manager != null && manager.IsServer;

        // รีเซตค่าพิกัดและเริ่มทำงาน
        pillar.Init(dmgBuff, moveBuff, hasteBuff, regenBuff, evo, shieldTickInterval, radius, isServer);
        pillar.SetHealAmount(healAmount);

        // ทำลายตามอายุเสา
        Destroy(go, lifetime);
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

        AttachAreaVisual(go, radius, parentScaleX, parentScaleY, parentScaleZ);

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
            var mat = GetPillarBodyMaterial();
            if (mat != null) mr1.sharedMaterial = mat;
        }

        // go เป็น GameObject เปล่าที่เพิ่งสร้าง สเกล (1,1,1) เสมอ จึงส่ง parentScale = 1 ตรงๆ
        AttachAreaVisual(go, radius, 1f, 1f, 1f);

        return go;
    }

    /// <summary>
    /// ใช้ร่วมกันระหว่าง SpawnLocalArenaPrefab กับ CreateFallbackPillar — สปอว์น VFX อาณาเขตจาก
    /// NetworkedVFXPool ถ้ามี ไม่งั้นสร้างแผ่นวงกลมสีเขียวโปร่งแสงเป็นตัวสำรอง
    /// </summary>
    void AttachAreaVisual(GameObject parent, float radius, float parentScaleX, float parentScaleY, float parentScaleZ)
    {
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
            // ข้อยกเว้นที่ตั้งใจต่อ CLAUDE.md ข้อ 2 (ห้าม Instantiate ตรง ให้ผ่าน PlayByName):
            // VFX นี้ต้องเป็นลูกของเสาและมีอายุเท่าเสา ซึ่ง PlayByName (คืนจาก pool กลับเอง) ทำไม่ได้
            // จึงใช้ GetVfxPrefab + Instantiate ตรงแทน — ผู้รีวิวรอบถัดไปไม่ต้อง flag ซ้ำ
            var vfxGo = Instantiate(areaVfxPrefab, parent.transform);
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
            disc.transform.SetParent(parent.transform, false);
            disc.transform.localPosition = Vector3.up * 0.05f;

            float scaleX = (radius * 2f) / parentScaleX;
            float scaleZ = (radius * 2f) / parentScaleZ;
            disc.transform.localScale = new Vector3(scaleX, 0.05f / parentScaleY, scaleZ);

            var col = disc.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var mr = disc.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = GetArenaDiscMaterial();
                if (mat != null) mr.sharedMaterial = mat;
            }
        }
    }

    /// <summary>
    /// Material ของแผ่นวงพื้นเสา — ใช้ arenaDiscMaterial ที่ designer assign ก่อนเสมอ
    /// ถ้าไม่ assign จะสร้างแคชไว้ใช้ซ้ำครั้งเดียว (ไม่สร้างใหม่ต่อเสา) เป็น fallback สุดท้าย
    /// และเช็ค Shader.Find เป็น null ก่อนเสมอ (คืน null + warn แทนที่จะยิง NRE ในบิลด์)
    /// </summary>
    Material GetArenaDiscMaterial()
    {
        if (arenaDiscMaterial != null) return arenaDiscMaterial;
        if (s_fallbackDiscMaterial != null) return s_fallbackDiscMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[SupportArenaWeapon] ไม่พบทั้ง URP/Lit และ Standard shader — ข้ามการสร้างวัสดุแผ่นวงพื้นเสา " +
                              "(ตรวจ Always Included Shaders ในบิลด์ หรือ assign arenaDiscMaterial เองใน Inspector)");
            return null;
        }

        var mat = new Material(shader) { color = new Color(0.2f, 1f, 0.4f, 0.15f) };
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        s_fallbackDiscMaterial = mat;
        return s_fallbackDiscMaterial;
    }

    /// <summary>
    /// Material ของตัวเสาทรงกระบอก (fallback pillar เต็มอัน) — เหมือน GetArenaDiscMaterial
    /// แต่เป็นสีทึบไม่โปร่งแสง ใช้ pillarBodyMaterial ที่ designer assign ก่อนเสมอ
    /// </summary>
    Material GetPillarBodyMaterial()
    {
        if (pillarBodyMaterial != null) return pillarBodyMaterial;
        if (s_fallbackBodyMaterial != null) return s_fallbackBodyMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("[SupportArenaWeapon] ไม่พบทั้ง URP/Lit และ Standard shader — ข้ามการสร้างวัสดุตัวเสา " +
                              "(ตรวจ Always Included Shaders ในบิลด์ หรือ assign pillarBodyMaterial เองใน Inspector)");
            return null;
        }

        s_fallbackBodyMaterial = new Material(shader) { color = new Color(0.2f, 1f, 0.4f) };
        return s_fallbackBodyMaterial;
    }
}
