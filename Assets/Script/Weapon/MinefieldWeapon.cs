using UnityEngine;

/// <summary>
/// Minefield — Super Grenade
/// วาง Mine รอบๆ ตัวผู้เล่นแบบ random ทุก cooldown
/// Mine จะระเบิดเมื่อศัตรูเข้าใกล้
///
/// Level data (Super tier, 1 level):
///   dmg=120, cd=2.5s, count=3 (จำนวน mine ต่อ volley), range=6 (วางใน radius นี้)
/// </summary>
public class MinefieldWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิดของ mine แต่ละลูก")]
    public float mineExplosionRadius = 2.5f;

    protected override void OnInit() => aimMode = AimMode.AutoNearest;

    protected override void OnFire(WeaponLevelData ld)
    {
        float dmg    = RollDamage(ld.damage);
        float radius = mineExplosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        for (int i = 0; i < ld.projectileCount; i++)
        {
            // random position รอบตัวผู้เล่นใน range
            Vector2 rnd    = Random.insideUnitCircle * ld.range;
            Vector3 minePos = transform.position + new Vector3(rnd.x, 0f, rnd.y);
            manager.DropMineServerRpc(minePos, dmg, triggerRadius: 1.5f);
        }
    }
}
