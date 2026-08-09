using UnityEngine;

/// <summary>
/// Augment ที่แถมอาวุธหรือสกิลให้ทันที
/// Assets &gt; Create &gt; LoL Swarm/Augment/Weapon Grant Augment
///
/// ใช้ทำ augment แนว "ปลดล็อกของใหม่" เช่น Prismatic ที่ให้ Super weapon เลย
/// ถ้า slot เต็ม อาวุธจะไม่ถูกเพิ่ม — ใส่ statFallback ไว้กันการ์ดเปล่า
/// </summary>
[CreateAssetMenu(fileName = "Aug_Grant_New", menuName = "LoL Swarm/Augment/Weapon Grant Augment")]
public class WeaponGrantAugment : AugmentData
{
    [Header("Grant")]
    [Tooltip("อาวุธที่แถม — กิน weapon slot ตามปกติ")]
    public WeaponData weaponToGrant;
    [Tooltip("เลเวลเริ่มต้นของอาวุธที่แถม (1-based)")]
    [Min(1)]
    public int grantAtLevel = 1;
    [Tooltip("สกิลที่แถม (Q/E) — ใส่ได้พร้อมอาวุธหรือใส่อย่างเดียว")]
    public AbilityData abilityToGrant;

    [Header("Fallback (ถ้า slot เต็ม / แถมไม่สำเร็จ)")]
    public StatType fallbackStat  = StatType.Damage;
    public float    fallbackValue = 0.15f;

    public override void OnAcquire(PlayerAugmentManager ctx)
    {
        if (ctx == null) return;

        bool granted = false;

        if (weaponToGrant != null && ctx.Weapons != null)
        {
            // สำเนาฝั่ง server อาจได้อาวุธไปแล้วจาก AddWeaponServerRpc ของ owner
            // → นับว่า "แถมสำเร็จ" ด้วย ไม่งั้น server จะไปใช้ fallback แล้วค่าสองฝั่งไม่ตรงกัน
            if (ctx.Weapons.HasWeapon(weaponToGrant))
            {
                granted = true;
            }
            else if (ctx.Weapons.AddWeapon(weaponToGrant))
            {
                granted = true;
                for (int i = 1; i < grantAtLevel; i++)
                    ctx.Weapons.UpgradeWeapon(weaponToGrant);
            }
        }

        if (abilityToGrant != null && ctx.Abilities != null)
        {
            ctx.Abilities.AddAbility(abilityToGrant);
            granted = true;
        }

        if (!granted)
        {
            Debug.Log($"[Augment] {augmentName}: แถมของไม่ได้ (slot เต็ม?) → ให้ fallback stat แทน");
            ctx.Stats?.AddPermanentBonus(fallbackStat, fallbackValue, ctx.Move);
        }
    }
}
