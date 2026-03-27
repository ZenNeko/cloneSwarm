using UnityEngine;

/// <summary>
/// ข้อมูลตัวละคร — สร้างผ่าน Assets > Create > LoL Swarm > Character Data
/// PlayerWeaponManager อ่าน startingWeapon จาก CharacterData ที่เลือก
/// </summary>
[CreateAssetMenu(fileName = "Char_New", menuName = "LoL Swarm/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed";
    [TextArea(1, 3)]
    public string description;
    public Sprite portrait;   // ภาพใหญ่ใน select screen
    public Sprite icon;       // ภาพเล็กใน HUD

    [Header("Starting Weapons")]
    [Tooltip("Weapon หลักที่ตัวละครเริ่มต้นมา")]
    public WeaponData startingWeapon;
    [Tooltip("Weapon เพิ่มเติมที่ได้พร้อมกัน — ใช้สำหรับ Kit characters เช่น Riven (Valor + Blade of Exile)")]
    public WeaponData[] additionalWeapons;

    [Header("Base Stats")]
    public float baseHealth     = 100f;
    public float baseMoveSpeed  = 5f;

    [Header("Passive Trait (Optional — ใส่ข้อความอธิบาย passive)")]
    [TextArea(1, 3)]
    public string passiveDescription;
}
