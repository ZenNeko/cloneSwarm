using UnityEngine;

[CreateAssetMenu(fileName = "FusionRecipe_New", menuName = "LoL Swarm/Weapon Fusion Recipe")]
public class WeaponFusionRecipe : ScriptableObject
{
    public string    recipeName;
    [Tooltip("Super Weapon ตัวแรก (tier = Super)")]
    public WeaponData superWeaponA;
    [Tooltip("Super Weapon ตัวที่สอง (tier = Super)")]
    public WeaponData superWeaponB;
    [Tooltip("ผลลัพธ์ที่ได้ (tier = Fusion)")]
    public WeaponData fusionResult;

    /// <summary>ตรวจว่า a+b ตรงกับ recipe นี้ไหม (ลำดับไม่สำคัญ)</summary>
    public bool IsSatisfied(WeaponData a, WeaponData b)
        => (superWeaponA == a && superWeaponB == b)
        || (superWeaponA == b && superWeaponB == a);
}
