using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Support Arena — วาง pillar สร้าง buff zone ให้ผู้เล่นทุกคนที่อยู่ใน zone
///
/// กลไก:
///   • OnFire → Instantiate pillar visual ที่ตำแหน่ง player
///   • Pillar มี buff zone (radius) → ตรวจ player ใน zone ทุก frame → apply temp buffs
///   • +20% Damage, +20% Move Speed, +20% Ability Haste (as 20 haste), +10 HP/s Regen
///   • เมื่อ player ออกนอก zone → ลบ buff
///   • Pillar มี lifetime → Destroy
///
///   Evo: +5% ทุกอย่าง (รวม 25%) + เมื่อ HP เต็ม → เติมโล่ (ปริมาณ = maxHP)
///
/// Level data แนะนำ:
///   Lv1: cd=12s, range=6
///   Lv2: cd=11s, range=7
///   Lv3: cd=10s, range=8
///   Lv4: cd=9s,  range=9
///   Lv5: cd=8s,  range=10
/// </summary>
public class SupportArenaWeapon : WeaponBase
{
    [Header("Support Arena Settings")]
    [Tooltip("Prefab visual ของ pillar (optional — ถ้าไม่ assign ใช้ cylinder)")]
    public GameObject pillarPrefab;

    [Tooltip("อายุ pillar (วินาที) — scale ตาม Duration stat")]
    public float arenaDuration = 10f;

    [Header("Buff Values")]
    public float damageBuff     = 0.20f;    // +20%
    public float moveSpeedBuff  = 0.20f;    // +20%
    public float abilityHaste   = 20f;      // +20 haste
    public float hpRegenBuff    = 10f;      // +10 HP/s

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

    private readonly List<ArenaPillar> _pillars = new();

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 pos = transform.position;
        SpawnArena(pos, ld);
    }

    void SpawnArena(Vector3 position, WeaponLevelData ld)
    {
        // Visual
        float radius = ld.range;
        GameObject go = pillarPrefab != null
            ? Instantiate(pillarPrefab, position, Quaternion.identity)
            : CreateFallbackPillar(position);

        float durationMult = manager.statManager != null ? manager.statManager.GetDurationMultiplier() : 1f;
        float lifetime     = arenaDuration * durationMult;

        var arena = new ArenaPillar
        {
            go            = go,
            position      = position,
            radius        = radius,
            spawnTime     = Time.time,
            lifetime      = lifetime,
            buffedPlayers = new HashSet<playermove>(),
        };
        _pillars.Add(arena);

        StartCoroutine(ArenaCoroutine(arena));
    }

    IEnumerator ArenaCoroutine(ArenaPillar arena)
    {
        // คำนวณ buff values (รวม evo ถ้าเปิด)
        float dmgBuff   = damageBuff    + (evoEnabled ? evoBonusPercent : 0f);
        float moveBuff  = moveSpeedBuff + (evoEnabled ? evoBonusPercent : 0f);
        float hasteBuff = abilityHaste  + (evoEnabled ? evoHasteBonus   : 0f);
        float regenBuff = hpRegenBuff   + (evoEnabled ? evoRegenBonus   : 0f);

        float shieldTimer = 0f;

        while (Time.time - arena.spawnTime < arena.lifetime)
        {
            // หาผู้เล่นทุกคนใน zone
            var players = FindPlayersInRadius(arena.position, arena.radius);
            var currentInZone = new HashSet<playermove>();

            foreach (var pm in players)
            {
                currentInZone.Add(pm);

                // ถ้ายังไม่ได้ buff → apply
                if (!arena.buffedPlayers.Contains(pm))
                {
                    ApplyBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
                    arena.buffedPlayers.Add(pm);
                }

                // Evo: Shield fill เมื่อ HP เต็ม
                if (evoEnabled)
                {
                    shieldTimer += Time.deltaTime;
                    if (shieldTimer >= shieldTickInterval)
                    {
                        shieldTimer = 0f;
                        if (pm.netHealth.Value >= pm.maxHealth)
                        {
                            manager.AddShieldServerRpc(pm.maxHealth);
                        }
                    }
                }
            }

            // ผู้เล่นที่ออกนอก zone → ลบ buff
            var leftPlayers = new List<playermove>();
            foreach (var pm in arena.buffedPlayers)
            {
                if (!currentInZone.Contains(pm))
                    leftPlayers.Add(pm);
            }
            foreach (var pm in leftPlayers)
            {
                RemoveBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
                arena.buffedPlayers.Remove(pm);
            }

            yield return null;
        }

        // Cleanup: ลบ buff จากทุกคนที่ยังอยู่ใน zone
        foreach (var pm in arena.buffedPlayers)
        {
            if (pm != null)
                RemoveBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
        }
        arena.buffedPlayers.Clear();

        _pillars.Remove(arena);
        if (arena.go != null) Destroy(arena.go);
    }

    void ApplyBuffs(playermove pm, float dmg, float move, float haste, float regen)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus  += move;
        pm.tempHealthRegenBonus += regen;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult += dmg;
            sm.tempAbilityHaste   += haste;
        }
    }

    void RemoveBuffs(playermove pm, float dmg, float move, float haste, float regen)
    {
        if (pm == null) return;

        pm.tempMoveSpeedBonus   -= move;
        pm.tempHealthRegenBonus -= regen;

        var sm = pm.GetComponent<PlayerStatManager>();
        if (sm != null)
        {
            sm.tempDamageBonusMult -= dmg;
            sm.tempAbilityHaste   -= haste;
        }
    }

    /// <summary>หาผู้เล่นทุกคนใน radius (ใช้ OverlapSphere กับ player layer)</summary>
    List<playermove> FindPlayersInRadius(Vector3 center, float radius)
    {
        var result = new List<playermove>();
        var cols = Physics.OverlapSphere(center, radius, LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            var pm = c.GetComponent<playermove>();
            if (pm != null && !pm.isDead.Value)
                result.Add(pm);
        }

        // Fallback: ถ้า Player layer ไม่ match → ลองหาจาก NetworkManager
        if (result.Count == 0 && Unity.Netcode.NetworkManager.Singleton != null)
        {
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var pm = client.PlayerObject.GetComponent<playermove>();
                if (pm == null || pm.isDead.Value) continue;
                float dist = Vector3.Distance(center, pm.transform.position);
                if (dist <= radius)
                    result.Add(pm);
            }
        }
        return result;
    }

    void OnDestroy()
    {
        // Cleanup ทั้งหมด
        foreach (var arena in _pillars)
        {
            foreach (var pm in arena.buffedPlayers)
            {
                if (pm != null)
                {
                    float dmgBuff   = damageBuff    + (evoEnabled ? evoBonusPercent : 0f);
                    float moveBuff  = moveSpeedBuff + (evoEnabled ? evoBonusPercent : 0f);
                    float hasteBuff = abilityHaste  + (evoEnabled ? evoHasteBonus   : 0f);
                    float regenBuff = hpRegenBuff   + (evoEnabled ? evoRegenBonus   : 0f);
                    RemoveBuffs(pm, dmgBuff, moveBuff, hasteBuff, regenBuff);
                }
            }
            if (arena.go != null) Destroy(arena.go);
        }
        _pillars.Clear();
    }

    static GameObject CreateFallbackPillar(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.position   = pos + Vector3.up * 1.5f;
        go.transform.localScale = new Vector3(0.6f, 3f, 0.6f);

        var col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.2f, 1f, 0.4f); // green glow
            mr.material = mat;
        }
        return go;
    }

    private class ArenaPillar
    {
        public GameObject          go;
        public Vector3             position;
        public float               radius;
        public float               spawnTime;
        public float               lifetime;
        public HashSet<playermove> buffedPlayers;
    }
}
