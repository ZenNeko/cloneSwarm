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

    [Header("Starting Weapons")]
    public WeaponData   startingWeapon;
    [Tooltip("Kit weapons เพิ่มเติม เช่น Riven มี Valor + Blade of Exile")]
    public WeaponData[] additionalWeapons;

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
