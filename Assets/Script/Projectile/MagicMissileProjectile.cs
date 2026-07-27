using Unity.Netcode;
using UnityEngine;

/// <summary>
/// MagicMissileProjectile — NetworkObject กระสุนเวทมนตร์นำวิถีซิงก์เน็ตเวิร์ก
/// ประมวลผลการคำนวณ Homing บินตามศัตรูบนเซิร์ฟเวอร์ และกระจายความเสียหายพร้อมติดสถานะผิดปกติแบบ AoE เมื่อกระทบเป้าหมาย
/// </summary>
public class MagicMissileProjectile : NetworkBehaviour
{
    [HideInInspector] public float   damage;
    [HideInInspector] public float   speed;
    [HideInInspector] public float   impactRadius = 1.5f;
    [HideInInspector] public bool    isCrit;
    [HideInInspector] public string  weaponName = "Unknown";
    [HideInInspector] public PlayerWeaponManager ownerManager;

    [Header("Movement")]
    [Tooltip("องศาที่เลี้ยวได้สูงสุดต่อวินาที")]
    public float turnSpeed = 240f;

    [Header("Debuffs & Evo")]
    [HideInInspector] public float slowPercent = 0.5f;
    [HideInInspector] public float slowDuration = 2f;
    [HideInInspector] public bool  evoEnabled = false;
    [HideInInspector] public float freezeChance = 0.15f;
    [HideInInspector] public float freezeDuration = 1.5f;

    [HideInInspector]
    public string hitVfxKey = "HitEffect";

    private Transform targetTransform;
    private Vector3   moveDirection;
    private float     maxLifetime = 8f;
    private float     lifeTimer;

    public void Init(Transform target, Vector3 dir)
    {
        targetTransform = target;
        moveDirection   = dir.normalized;
        lifeTimer       = 0f;

        if (moveDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDirection);
    }

    void Update()
    {
        if (!IsServer || !NetworkObject.IsSpawned) return;

        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetime)
        {
            SafeDespawn();
            return;
        }

        float step = speed * Time.deltaTime;

        // target หาย -> ระเบิดทันที
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy)
        {
            Explode(transform.position);
            return;
        }

        // ตรวจจับชนศัตรูตัวอื่นก่อนถึงเป้าหมายหลัก
        var cols = PlayerWeaponManager.OverlapEnemy(transform.position, 0.6f);
        foreach (var c in cols)
        {
            if (c != null && c.gameObject.activeInHierarchy && c.transform != targetTransform)
            {
                Explode(transform.position);
                return;
            }
        }

        // นำวิถี (Homing): หักเลี้ยวและบินเข้าหาศัตรู
        Vector3 targetPos = targetTransform.position + Vector3.up * 0.5f;
        Vector3 dir       = targetPos - transform.position;

        if (dir.magnitude <= step + 0.2f)
        {
            // ชนเข้าเป้าหมายอย่างเป็นทางการ!
            transform.position = targetPos;
            Explode(targetPos);
            return;
        }

        // หักเลี้ยวตามความเร็ว turnSpeed แบบโค้งมน
        Vector3 desiredDir = dir.normalized;
        moveDirection = Vector3.RotateTowards(moveDirection, desiredDir,
                           turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);

        transform.position += moveDirection * step;
        transform.rotation = Quaternion.LookRotation(moveDirection);
    }

    void Explode(Vector3 pos)
    {
        if (ownerManager != null)
        {
            // 1. ความเสียหาย (Server-authoritative)
            ownerManager.FireMeleeServerRpc(pos, impactRadius, damage, isCrit, weaponName);

            // 2. สถานะผิดปกติ (Slow / Freeze)
            ownerManager.ApplySlowToEnemiesServerRpc(pos, impactRadius, slowDuration, slowPercent);

            if (evoEnabled && Random.value < freezeChance)
            {
                ownerManager.ApplyFreezeToEnemiesServerRpc(pos, impactRadius, freezeDuration);
            }

            // 3. กระจายเอฟเฟกต์การชนและเล่นเสียง Hit ให้กับผู้เล่นทุกคน (ClientRpc)
            float vfxScale = 1f;
            if (NetworkedVFXPool.Instance != null && impactRadius > 0f)
            {
                float designed = NetworkedVFXPool.Instance.GetDesignedRadius(hitVfxKey);
                if (designed > 0f) vfxScale = impactRadius / designed;
                else vfxScale = impactRadius;
            }
            ownerManager.BroadcastVfxTypeClientRpc(pos, hitVfxKey, vfxScale);
            ownerManager.PlayWeaponHitSfxClientRpc(weaponName, pos);
        }

        SafeDespawn();
    }

    void SafeDespawn()
    {
        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private Transform FindNearestEnemy()
    {
        // สแกนหาศัตรูในระยะ 25 เมตร
        float checkRange = 25f;
        var cols = PlayerWeaponManager.OverlapEnemy(transform.position, checkRange);
        Transform nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in cols)
        {
            if (c == null) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < minDist) { minDist = d; nearest = c.transform; }
        }
        return nearest;
    }
}
