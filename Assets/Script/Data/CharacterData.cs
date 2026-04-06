using UnityEngine;

/// <summary>
/// ข้อมูลตัวละคร — Assets > Create > LoL Swarm > Character Data
/// </summary>
[CreateAssetMenu(fileName = "Char_New", menuName = "LoL Swarm/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed";
    [TextArea(1, 3)]
    public string description;
    public Sprite portrait;
    public Sprite icon;

    [Header("Base Stats")]
    public float baseHealth    = 100f;
    public float baseMoveSpeed = 5f;

    [Header("Starting Weapon")]
    public WeaponData startingWeapon;

    [Header("Passive Weapons (ไม่นับ Weapon Slot)")]
    [Tooltip("Passive scripts เช่น HunterPassiveWeapon, GunnerPassiveWeapon\n" +
             "Spawn เป็น child player แต่ไม่นับ Weapon Slot และไม่แสดงใน Weapon UI")]
    [UnityEngine.Serialization.FormerlySerializedAs("additionalWeapons")]
    public WeaponData[] passiveWeapons;

    [Header("Abilities (Q / E / R)")]
    [Tooltip("Ability ของ character — ใช้ AbilityData (ไม่ใช่ WeaponData)\n" +
             "บริหารโดย PlayerAbilityManager ไม่นับ Weapon Slot\n" +
             "เช่น Riven: ใส่ Valor (Q) และ Blade of Exile (E) ที่นี่")]
    public AbilityData[] abilities;

    // ─────────────────────────────────────────────────────────────────────
    [Header("Passive")]
    public Sprite  passiveIcon;
    public string  passiveName;
    [TextArea(1, 3)]
    public string  passiveDescription;

    [Header("Weapon Ability")]
    public Sprite  weaponIcon;
    public string  weaponAbilityName;
    [TextArea(1, 3)]
    public string  weaponAbilityDescription;

    [Header("Ability (Q)")]
    public Sprite  abilityIcon;
    public string  abilityName;
    [TextArea(1, 3)]
    public string  abilityDescription;

    [Header("Ultimate (E / R)")]
    public Sprite  ultimateIcon;
    public string  ultimateName;
    [TextArea(1, 3)]
    public string  ultimateDescription;
}
