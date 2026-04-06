using UnityEngine;

/// <summary>
/// Cluster Bomb — FUSION: Blunderbuss (Super Shotgun) + Minefield (Super Grenade)
///
/// กลไก:
///   • ขว้าง Grenade ไปที่ mouse (เหมือน Grenade)
///   • เมื่อระเบิด: ปล่อย pellets รอบทิศทาง (เหมือน Shotgun)
///   • cluster = true ใน ThrowGrenadeServerRpc → GrenadeProjectile จัดการ
///
/// Level data (Fusion tier, 1 level):
///   dmg=100, cd=2.5s, count=2 (grenade), range=14, projSpeed=18
/// </summary>
public class ClusterBombWeapon : WeaponBase
{
    public float explosionRadius = 4f;
    public float fuseTime        = 1.2f;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos  = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = GetMouseWorldPosition();

        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.magnitude > ld.range)
            targetPos = transform.position + toTarget.normalized * ld.range;

        float dmg    = RollDamage(ld.damage);
        float radius = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        for (int i = 0; i < ld.projectileCount; i++)
        {
            float   a   = (ld.projectileCount > 1) ? (i - (ld.projectileCount - 1) * 0.5f) * 10f : 0f;
            Vector3 off = Quaternion.Euler(0, a, 0) * (targetPos - spawnPos).normalized * 1.5f;
            manager.ThrowGrenadeServerRpc(spawnPos, targetPos + off, dmg, radius, fuseTime, cluster: true);
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        if (Camera.main == null) return transform.position + transform.forward * 5f;
        var plane = new Plane(Vector3.up, transform.position);
        var ray   = Camera.main.ScreenPointToRay(
            UnityEngine.InputSystem.Mouse.current?.position.ReadValue()
            ?? new UnityEngine.Vector2(Screen.width / 2f, Screen.height / 2f));
        if (plane.Raycast(ray, out float d)) return ray.GetPoint(d);
        return transform.position + transform.forward * 5f;
    }
}
