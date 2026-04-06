using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// จัดการ Ability Slots ของ player (Q / E / R ฯลฯ)
/// — แยกจาก PlayerWeaponManager โดยสมบูรณ์ ไม่นับต่อ MaxWeaponSlots
/// — ใช้ AbilityData (ไม่ใช่ WeaponData)
/// — Abilities ยัง Instantiate เป็น child ของ player
///   → GameHUD ค้นหา ValorWeapon / BladeOfExileWeapon ผ่าน GetComponentInChildren ได้ตามปกติ
///
/// วิธีใช้:
///   1. Add component PlayerAbilityManager บน Player Prefab (เดียวกับ PlayerWeaponManager)
///   2. ใน CharacterData.abilities[] ใส่ AbilityData assets
///   3. PlayerWeaponManager.OnNetworkSpawn จะเรียก InitAbilities() ให้อัตโนมัติ
/// </summary>
public class PlayerAbilityManager : MonoBehaviour
{
    // ── Internal slot ─────────────────────────────────────────────────────
    private class AbilitySlot
    {
        public AbilityData data;
        public AbilityBase script;
        public GameObject  go;
    }

    private readonly List<AbilitySlot> abilities = new();

    private PlayerWeaponManager weaponManager;

    // ── Lifecycle ─────────────────────────────────────────────────────────
    void Awake()
    {
        weaponManager = GetComponent<PlayerWeaponManager>();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>เรียกโดย PlayerWeaponManager.OnNetworkSpawn หลัง weapon init</summary>
    public void InitAbilities(CharacterData cd)
    {
        if (cd == null || cd.abilities == null) return;
        foreach (var ab in cd.abilities)
            if (ab != null) AddAbility(ab);
    }

    /// <summary>เพิ่ม ability — คืน false ถ้า null หรือมีแล้ว</summary>
    public bool AddAbility(AbilityData data)
    {
        if (data == null || HasAbility(data)) return false;
        SpawnAbility(data);
        return true;
    }

    public bool HasAbility(AbilityData data) => abilities.Exists(a => a.data == data);

    public List<AbilityData> GetAbilities()
    {
        var result = new List<AbilityData>();
        foreach (var a in abilities) result.Add(a.data);
        return result;
    }

    // ── Internal ──────────────────────────────────────────────────────────
    void SpawnAbility(AbilityData data)
    {
        var slot = new AbilitySlot { data = data };

        if (data.prefab != null)
        {
            slot.go     = Instantiate(data.prefab, transform);
            slot.script = slot.go.GetComponent<AbilityBase>();

            if (slot.script != null)
            {
                if (weaponManager == null) weaponManager = GetComponent<PlayerWeaponManager>();
                slot.script.Init(data, 0, weaponManager);
            }
            else
            {
                Debug.LogWarning($"[AbilityManager] {data.abilityName}: ไม่พบ AbilityBase บน prefab");
            }
        }
        else
        {
            Debug.LogWarning($"[AbilityManager] {data.abilityName}: ไม่มี prefab");
        }

        abilities.Add(slot);
        Debug.Log($"[AbilityManager] ➕ {data.abilityName}");
    }

    public void RemoveAbility(AbilityData data)
    {
        var slot = abilities.Find(a => a.data == data);
        if (slot == null) return;
        if (slot.go != null) Destroy(slot.go);
        abilities.Remove(slot);
    }
}
