using UnityEngine;

/// <summary>
/// Grenade — MouseAim, ขว้างไปยังตำแหน่ง mouse → ระเบิด AoE
///
/// Level data แนะนำ:
///   Lv1: dmg=50,  cd=3.0s, count=1, range=10 (throw range max)
///   Lv2: dmg=65,  cd=2.7s, count=1, range=11
///   Lv3: dmg=82,  cd=2.5s, count=2, range=12
///   Lv4: dmg=100, cd=2.3s, count=2, range=13
///   Lv5: dmg=125, cd=2.0s, count=3, range=14
/// </summary>
public class GrenadeWeapon : WeaponBase
{
    [Tooltip("รัศมีระเบิด (จะ scale ตาม AreaSize stat)")]
    public float explosionRadius = 3f;
    [Tooltip("เวลา fuse ก่อนระเบิด (วินาที)")]
    public float fuseTime        = 1.5f;

    protected override void OnInit() => aimMode = AimMode.MouseAim;

    protected override void OnFire(WeaponLevelData ld)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;

        // หาตำแหน่ง mouse บน ground plane
        Vector3 targetPos = GetMouseWorldPosition();

        // จำกัด range
        Vector3 toTarget = targetPos - transform.position;
        if (toTarget.magnitude > ld.range)
            targetPos = transform.position + toTarget.normalized * ld.range;

        float dmg    = RollDamage(ld.damage);
        float radius = explosionRadius;
        if (manager.statManager != null)
            radius *= manager.statManager.GetAreaMultiplier();

        // โยน count ลูก ในมุมต่างกันเล็กน้อย
        for (int i = 0; i < ld.projectileCount; i++)
        {
            float   spreadRad = (ld.projectileCount > 1)
                ? Mathf.Deg2Rad * ((i - (ld.projectileCount - 1) * 0.5f) * 12f)
                : 0f;
            Vector3 offset    = new Vector3(Mathf.Sin(spreadRad), 0, Mathf.Cos(spreadRad)) * 1.5f;
            manager.ThrowGrenadeServerRpc(spawnPos, targetPos + offset, dmg, radius, fuseTime);
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
